import { ArenaRenderer } from './arena-renderer.js?v=2';
let canvas, bridge, renderer, socket, animation, heartbeat, inputTimer, resizeObserver;
let practice = false, intentionalClose = false, reconnecting = false, disposed = true, phase = '', playerId = '', sequence = 0;
let latest, previous, receivedAt = 0, previousAt = 0, reconnectTimer, reconnectAttempt = 0;
const keys = new Set(), touchKeys = new Set(), listeners = [];
let inputPending = false, soundEnabled = false, audio;
const supported = new Set(['KeyA','KeyD','KeyW','KeyS','ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Space','KeyJ','KeyK','KeyL','KeyU','KeyI','ShiftLeft','ShiftRight']);
const call = (name, ...args) => { if (!disposed && bridge) return bridge.invokeMethodAsync(name, ...args).catch(() => {}); return Promise.resolve(); };
function on(target,event,handler,options) { target.addEventListener(event,handler,options); listeners.push(()=>target.removeEventListener(event,handler,options)); }
export async function mount(element, reference, options) {
    dispose(); disposed = false; intentionalClose = true; canvas = element; bridge = reference;
    renderer = new ArenaRenderer(canvas, options, cue); renderer.preview(options.stages[0].id,options.selectedAvatarId);
    const fit = () => { const r=canvas.getBoundingClientRect(); const ratio=Math.min(devicePixelRatio||1,2); canvas.width=Math.max(1,Math.round(r.width*ratio)); canvas.height=Math.max(1,Math.round(r.height*ratio)); };
    resizeObserver=new ResizeObserver(fit); resizeObserver.observe(canvas); fit();
    on(window,'keydown',e=>{ if(!supported.has(e.code) || !canvas?.offsetWidth || !canvas?.offsetHeight || /INPUT|TEXTAREA|SELECT|BUTTON/.test(document.activeElement?.tagName||'')) return; if (!practice && (!socket || !['playing','countdown'].includes(phase))) return; e.preventDefault(); if(!keys.has(e.code)) { keys.add(e.code); transmitInput(); } });
    on(window,'keyup',e=>{if(keys.delete(e.code)) {e.preventDefault();transmitInput();}});
    on(window,'blur',clearInput); on(document,'visibilitychange',()=>{if(document.hidden)clearInput();});
    on(document,'pointerdown',e=>{const b=e.target.closest('[data-arena-key]');if(!b)return;e.preventDefault();b.setPointerCapture?.(e.pointerId);touchKeys.add(b.dataset.arenaKey);transmitInput();});
    const release=e=>{const b=e.target.closest('[data-arena-key]');if(b){touchKeys.delete(b.dataset.arenaKey);transmitInput();}};on(document,'pointerup',release);on(document,'pointercancel',release);
    on(canvas,'pointerdown',()=>canvas.focus({preventScroll:true}));
    inputTimer=setInterval(transmitInput,1000/60);
    let last=performance.now(); const draw=now=>{if(disposed)return;const dt=Math.min(.05,(now-last)/1000);last=now;if(canvas.width>1&&canvas.height>1)renderer.draw(now,dt,latest,previous,receivedAt,previousAt,playerId,practice);animation=requestAnimationFrame(draw);};animation=requestAnimationFrame(draw);
    return await renderer.preload();
}
function clearInput(){keys.clear();touchKeys.clear();transmitInput();}
function input(){ if(!canvas?.offsetWidth||!canvas?.offsetHeight){keys.clear();touchKeys.clear();} const pressed=c=>keys.has(c)||touchKeys.has(c);const pad=navigator.getGamepads?.()?.find?.(p=>p?.connected);const axis=n=>pad && Math.abs(pad.axes[n]||0)>.23?pad.axes[n]:0;const b=n=>pad?.buttons[n]?.pressed||false;return {type:'input',sequence:++sequence,horizontal:Math.max(-1,Math.min(1,(pressed('KeyD')||pressed('ArrowRight')?1:0)-(pressed('KeyA')||pressed('ArrowLeft')?1:0)+axis(0))),vertical:Math.max(-1,Math.min(1,(pressed('KeyS')||pressed('ArrowDown')?1:0)-(pressed('KeyW')||pressed('ArrowUp')?1:0)+axis(1))),jump:pressed('Space')||b(0),attack:pressed('KeyJ')||b(2),special:pressed('KeyK')||b(1),shield:pressed('KeyL')||b(6)||b(7),grab:pressed('KeyU')||b(3),smash:pressed('KeyI')||b(5),dodge:pressed('ShiftLeft')||pressed('ShiftRight')||b(4)}; }
function transmitInput(){if(disposed||phase==='finished'||(!practice&&(socket?.readyState!==WebSocket.OPEN||!['countdown','playing'].includes(phase))))return;const packet=input();if(practice){if(inputPending)return;inputPending=true;call('PracticeInput',packet).finally(()=>inputPending=false);}else if(socket.bufferedAmount<16384)socket.send(JSON.stringify(packet));}
export function preview(stage,id){if(!renderer)return;latest=previous=null;phase='';renderer.preview(stage,id);}
export async function beginPractice(id){if(!renderer||!(await renderer.preload()))throw new Error('Grafikken kunne ikke indlæses.');disconnect();practice=true;playerId=id;latest=previous=null;phase='';renderer.reset();requestAnimationFrame(()=>requestAnimationFrame(()=>canvas?.focus({preventScroll:true})));}
export function stopPractice(stage,id){practice=false;clearInput();preview(stage,id);}
export function pushState(state){previous=latest;previousAt=receivedAt;latest=state;receivedAt=performance.now();if(state.phase!==phase){phase=state.phase;if(!practice)call('PhaseChanged',state).then(()=>{if(['countdown','playing'].includes(state.phase))requestAnimationFrame(()=>requestAnimationFrame(()=>canvas?.focus({preventScroll:true})));});}renderer?.events(state.events||[]);}
export function connect(url,id){
    intentionalClose=false;practice=false;disposed=false;playerId=id;phase="";latest=previous=null;renderer?.reset();
    if(socket){socket.onclose=null;socket.close();} clearTimeout(reconnectTimer);clearInterval(heartbeat);
    const parsed=new URL(url,location.href);parsed.protocol=parsed.protocol==='https:'?'wss:':'ws:';
    const current=new WebSocket(parsed.href);socket=current;
    const openTimeout=setTimeout(()=>{if(current.readyState!==WebSocket.OPEN)current.close();},10000);
    current.onopen=()=>{clearTimeout(openTimeout);reconnecting=false;reconnectAttempt=0;call('ConnectionChanged',false);canvas?.focus({preventScroll:true});heartbeat=setInterval(()=>send({type:'ping',sentAt:Date.now()}),3000);};
    current.onmessage=e=>{let message;try{message=JSON.parse(e.data);}catch{return;}if(message.type==='state')pushState(message.state);else if(message.type==='room'){call('RoomChanged',message.room);if(message.room.phase==='lobby'){phase='lobby';latest=previous=null;renderer?.preview(message.room.stageId,message.room.players.find(p=>p.id===playerId)?.avatarId||'starter',message.room.players);}}else if(message.type==='error')call('ArenaError',message.message||'Handlingen kunne ikke gennemføres.');else if(message.type==='pong'){const el=document.getElementById('arena-ping');if(el)el.textContent=Math.max(0,Date.now()-message.sentAt)+' ms';}};
    current.onerror=()=>{};
    current.onclose=()=>{clearTimeout(openTimeout);clearInterval(heartbeat);if(socket!==current||intentionalClose||disposed)return;socket=null;clearInput();reconnecting=true;call('ConnectionChanged',true);retryConnection();};
}
function retryConnection(){if(disposed||intentionalClose)return;if(reconnectAttempt>=7){reconnecting=false;call('ConnectionChanged',false);call('ArenaError','Forbindelsen kunne ikke gendannes. Forlad rummet, og deltag igen med rumkoden.');return;}const wait=Math.min(4000,500*2**reconnectAttempt++);reconnectTimer=setTimeout(async()=>{const ok=await call('Reconnect');if(!ok)retryConnection();},wait);}
export function send(message){if(socket?.readyState===WebSocket.OPEN){socket.send(JSON.stringify(message));return true;}return false;}
export function disconnect(){intentionalClose=true;reconnecting=false;clearTimeout(reconnectTimer);clearInterval(heartbeat);if(socket){send({type:'leave'});socket.onclose=null;socket.close(1000,'leave');socket=null;}keys.clear();touchKeys.clear();}
export async function fullscreen(){const target=document.querySelector(".arena-shell")||canvas?.closest(".arena-play");if(!target)return;try{if(document.fullscreenElement)await document.exitFullscreen();else await target.requestFullscreen();if(canvas?.offsetWidth)canvas.focus({preventScroll:true});}catch{call("ArenaError","Fuld skærm er ikke tilgængelig i denne browser. Du kan stadig spille i vinduet.");}}
export async function copyInvite(url){try{await navigator.clipboard.writeText(url);return true;}catch{return false;}}
export function sound(enabled){soundEnabled=enabled;if(enabled){audio??=new (window.AudioContext||window.webkitAudioContext)();audio.resume().catch(()=>{});}}
function cue(type,strength=1){if(!soundEnabled||!audio||audio.state!=='running')return;const settings={hit:[160,.07,'triangle'],ko:[90,.3,'sawtooth'],jump:[440,.08,'sine'],shield:[640,.07,'sine'],projectile:[700,.08,'triangle'],win:[520,.45,'sine'],break:[140,.2,'square'],dodge:[330,.05,'sine']};const s=settings[type];if(!s)return;const osc=audio.createOscillator(),gain=audio.createGain();osc.type=s[2];osc.frequency.setValueAtTime(s[0],audio.currentTime);osc.frequency.exponentialRampToValueAtTime(s[0]*(type==='ko'?.25:1.8),audio.currentTime+s[1]);gain.gain.setValueAtTime(.025*Math.min(2,strength),audio.currentTime);gain.gain.exponentialRampToValueAtTime(.001,audio.currentTime+s[1]);osc.connect(gain);gain.connect(audio.destination);osc.start();osc.stop(audio.currentTime+s[1]);}
export function dispose(){disposed=true;disconnect();practice=false;cancelAnimationFrame(animation);clearInterval(inputTimer);resizeObserver?.disconnect();listeners.splice(0).forEach(fn=>fn());renderer?.dispose();renderer=null;bridge=null;canvas=null;latest=previous=null;audio?.close().catch(()=>{});audio=null;soundEnabled=false;}
