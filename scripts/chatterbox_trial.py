"""Reproducible Danish CPU/CUDA trial; never changes application speech settings."""
import argparse
import importlib.metadata
import json
import os
import platform
import random
import threading
import time
from pathlib import Path

TEXTS = [
    ("01-velkomst", "Velkommen til Mercantec. I dag skal vi arbejde med programmering."),
    ("02-danske-lyde", "Rødgrød med fløde. Søren og Åse mødes på banegården i Køge."),
    ("03-tal", "Undervisningen starter klokken 8.15 i lokale 203. Opgaven skal afleveres den 12. november."),
    ("04-fagtekst", "En database gemmer oplysninger i tabeller. En primærnøgle identificerer hver række, og en fremmednøgle forbinder tabellerne."),
    ("05-tekniske-ord", "Vi bruger C#, .NET, SQL og Docker Compose. API'et modtager en forespørgsel og sender et svar tilbage."),
]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default=".tools/chatterbox-trial/results")
    parser.add_argument("--device", choices=["cpu", "cuda"], default="cpu")
    parser.add_argument("--threads", type=int, default=4)
    parser.add_argument("--revision", default="5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18")
    parser.add_argument("--offline", action="store_true", help="Require a complete cached checkpoint")
    args = parser.parse_args()
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    if args.threads < 1:
        parser.error("--threads must be positive")
    for variable in ["OMP_NUM_THREADS", "MKL_NUM_THREADS", "NUMBA_NUM_THREADS", "OPENBLAS_NUM_THREADS"]:
        os.environ[variable] = str(args.threads)
    if args.offline:
        os.environ["HF_HUB_OFFLINE"] = "1"
    import psutil
    import numpy as np
    import soundfile as sf
    import torch
    from huggingface_hub import snapshot_download
    from chatterbox.mtl_tts import ChatterboxMultilingualTTS

    torch.set_num_threads(args.threads)
    repo = "ResembleAI/chatterbox"
    revision = args.revision
    print(f"{'Using cached' if args.offline else 'Fetching'} multilingual V2 checkpoint at {revision}", flush=True)
    checkpoint = snapshot_download(repo, revision=revision, local_files_only=args.offline, allow_patterns=[
        "ve.pt", "s3gen.pt", "t3_mtl23ls_v2.safetensors", "conds.pt",
        "grapheme_mtl_merged_expanded_v1.json", "Cangjie5_TC.json",
    ])
    report = {
        "model": repo, "variant": "multilingual-v2", "revision": revision,
        "device": args.device, "threads": args.threads,
        "platform": platform.platform(), "processor": platform.processor(),
        "logical_cpus": os.cpu_count(), "ram_bytes": psutil.virtual_memory().total,
        "packages": {p: importlib.metadata.version(p) for p in ["chatterbox-tts", "torch", "transformers"]},
        "seed": 42, "clips": [], "listening_review": "pending",
    }
    process = psutil.Process()
    peak = [0]
    stop = threading.Event()

    def monitor():
        while not stop.wait(0.1):
            peak[0] = max(peak[0], process.memory_info().rss)

    threading.Thread(target=monitor, daemon=True).start()
    def save():
        report["peak_process_rss_bytes"] = peak[0]
        (output / "results.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    try:
        started = time.perf_counter()
        model = ChatterboxMultilingualTTS.from_local(checkpoint, device=args.device)
        report["load_seconds"] = time.perf_counter() - started
        print(f"Model loaded in {report['load_seconds']:.1f}s; warming up", flush=True)
        started = time.perf_counter()
        torch.manual_seed(42)
        model.generate("Klar.", language_id="da")
        report["warmup_seconds"] = time.perf_counter() - started
        for clip_id, text in TEXTS:
            random.seed(42)
            np.random.seed(42)
            torch.manual_seed(42)
            if args.device == "cuda":
                torch.cuda.reset_peak_memory_stats()
                torch.cuda.synchronize()
            started = time.perf_counter()
            audio = model.generate(text, language_id="da")
            if args.device == "cuda":
                torch.cuda.synchronize()
            elapsed = time.perf_counter() - started
            samples = audio.detach().cpu().squeeze().numpy()
            if samples.size == 0 or not np.isfinite(samples).all():
                raise RuntimeError(f"Invalid audio for {clip_id}")
            filename = clip_id + ".wav"
            sf.write(output / filename, samples, model.sr, subtype="PCM_16")
            duration = len(samples) / model.sr
            clip = {"file": filename, "text": text, "generation_seconds": elapsed,
                    "audio_seconds": duration, "real_time_factor": elapsed / duration,
                    "sample_rate": model.sr,
                    "clipped_sample_fraction": float(np.mean(np.abs(samples) >= 1))}
            if args.device == "cuda":
                clip["peak_cuda_allocated_bytes"] = torch.cuda.max_memory_allocated()
            report["clips"].append(clip)
            save()
            print(json.dumps(clip, ensure_ascii=False), flush=True)
        report["completed"] = True
    except Exception as error:
        report["error"] = f"{type(error).__name__}: {error}"
        raise
    finally:
        stop.set()
        save()


if __name__ == "__main__":
    main()
