const lifetime = 60_000;
const pending = new Map();
const queue = [];
let worker = null;
let current = null;

function retireWorker() { worker?.terminate(); worker = null; }

function finish(job, error, result) {
    if (!pending.has(job.id)) return;
    clearTimeout(job.timeout);
    pending.delete(job.id);
    job.dataUrl = null;
    if (current === job) current = null;
    error ? job.reject(error) : job.resolve(result);
    pump();
}

function pump() {
    if (current) return;
    let next;
    while (queue.length && !next) {
        const candidate = queue.shift();
        if (pending.has(candidate.id)) next = candidate;
    }
    if (!next) return;
    current = next;
    try {
        if (!worker) {
            worker = new Worker(new URL('./emoji-background-worker.js', import.meta.url), { type: 'module' });
            const owner = worker;
            worker.addEventListener('message', event => {
                if (worker !== owner || !current || event.data?.jobId !== current.id) return;
                const job = current;
                if (event.data.error) finish(job, new Error(event.data.error));
                else if (typeof event.data.result?.base64 === 'string' && event.data.result.contentType === 'image/png')
                    finish(job, null, event.data.result);
                else { retireWorker(); finish(job, new Error('Baggrundsfjernelsen gav et ugyldigt svar.')); }
            });
            worker.addEventListener('error', event => {
                event.preventDefault();
                if (worker !== owner) return;
                const job = current;
                retireWorker();
                if (job) finish(job, new Error('Baggrundsfjernelsen kunne ikke startes. Genindlæs siden, og prøv igen.'));
            });
        }
        worker.postMessage({ type: 'remove', jobId: next.id, dataUrl: next.dataUrl });
        // Worker now owns its copy; never retain the image longer than the request.
        next.dataUrl = null;
    } catch {
        retireWorker();
        finish(next, new Error('Baggrundsfjernelsen kunne ikke startes. Det oprindelige billede er bevaret.'));
    }
}

export function removeBackground(dataUrl, jobId) {
    if (pending.size >= 4)
        return Promise.reject(new Error('Der er allerede flere billeder i kø. Vent på den igangværende behandling.'));
    if (typeof jobId !== 'string' || !jobId.length || jobId.length > 128 || pending.has(jobId))
        return Promise.reject(new Error('Baggrundsfjernelsen har et ugyldigt eller allerede aktivt id.'));
    if (typeof dataUrl !== 'string' || dataUrl.length > Math.ceil(8 * 1024 * 1024 / 3) * 4 + 50 ||
        !/^data:image\/(png|jpeg|gif|webp);base64,/.test(dataUrl))
        return Promise.reject(new Error('Vælg et PNG-, JPEG-, GIF- eller WebP-billede på højst 8 MB.'));
    return new Promise((resolve, reject) => {
        const job = { id: jobId, dataUrl, resolve, reject, timeout: null };
        job.timeout = setTimeout(() => {
            if (current === job) retireWorker();
            finish(job, new Error('Baggrundsfjernelsen tog for lang tid. Det oprindelige billede er bevaret. Prøv igen.'));
        }, lifetime);
        pending.set(jobId, job);
        queue.push(job);
        pump();
    });
}

export function cancel(jobId) {
    const job = pending.get(jobId);
    if (!job) return;
    if (current === job) retireWorker();
    finish(job, new DOMException('Baggrundsfjernelsen blev stoppet. Det oprindelige billede er bevaret.', 'AbortError'));
}
