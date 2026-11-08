const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const source=fs.readFileSync(path.join(__dirname,'../web/wwwroot/js/avatar-layer-preview.js'),'utf8');

function setup(action='pickup'){
    let nextRaf=1;const frames=new Map(),listeners=new Map(),mediaListeners=new Set();
    class Element{
        constructor(tag='div'){this.tag=tag;this.dataset={};this.style={};this.children=[];this.isConnected=true;}
        append(node){node.parentElement=this;this.children.push(node);}
        remove(){this.removed=true;this.isConnected=false;if(this.parentElement)this.parentElement.children=this.parentElement.children.filter(node=>node!==this);}
        setAttribute(){}
        get firstElementChild(){return this.children[0];}
    }
    let animation={currentTime:0};
    const actor=new Element('span');actor.dataset={frames:'8',fps:'8',loop:'false'};actor.style.backgroundImage='url(original-character-sheet.png)';actor.getAnimations=()=>[animation];
    const overlay=new Element(),host=new Element(),viewport=new Element();
    host.querySelector=selector=>selector.includes('avatar-sheet')?actor:selector.includes('avatar-world-overlay')?overlay:null;
    host.closest=()=>viewport;host.getBoundingClientRect=()=>({left:0,right:256,width:256});viewport.getBoundingClientRect=()=>({left:0,right:800,width:800});
    const media={matches:false,addEventListener(name,fn){mediaListeners.add(fn);},removeEventListener(name,fn){mediaListeners.delete(fn);}};
    const document={hidden:false,createElement:tag=>new Element(tag),addEventListener(name,fn){if(!listeners.has(name))listeners.set(name,new Set());listeners.get(name).add(fn);},removeEventListener(name,fn){listeners.get(name)?.delete(fn);}};
    const assets={schemaVersion:1,frameSize:{width:512,height:512},items:['book','coin'].map(id=>({id,standalone:`items/${id}.png`,naturalSize:{width:32,height:32},pivot:{x:.5,y:.5},gripOffset:{x:0,y:-12}})),projectiles:[{id:'pulse_blue',standalone:'projectiles/pulse_blue.png',animationFrames:4,fps:12,width:64,height:64,speed:230,lifetimeMs:1400,pivot:{x:.5,y:.5}}],effects:[{id:'shield',standalone:'effects/shield.png',naturalSize:{width:512,height:512},pivot:{x:.5,y:.7},durationMs:850,attachment:'follow_anchor'}]};
    const events={schemaVersion:1,avatarId:'raymond',coordinateSystem:{width:256,height:256,origin:{x:128,y:224}},defaultAnchors:{root:{samples:[{frame:0,x:128,y:224}]}},actions:{
        pickup:{anchors:{right_hand:{interpolation:'linear',samples:Array.from({length:8},(_,frame)=>({frame,x:150+frame*2,y:210-frame*5}))}},events:[{frame:4,type:'attach_item',payload:{anchor:'right_hand'}}]},
        ability_1_pulse:{anchors:{muzzle:{samples:[{frame:4,x:215.5,y:74}]}},events:[{frame:4,type:'spawn_projectile',payload:{anchor:'muzzle'}}]},
        ability_3_shield:{events:[{frame:3,type:'spawn_effect',payload:{anchor:'root',effectSlot:'shield',attachment:'follow_anchor'}}]}
    }};
    const context={document,matchMedia:()=>media,AbortController,fetch:async url=>({ok:true,json:async()=>url.endsWith('external-assets.json')?assets:events}),requestAnimationFrame:fn=>{const id=nextRaf++;frames.set(id,fn);return id;},cancelAnimationFrame:id=>frames.delete(id),console};
    vm.createContext(context);vm.runInContext(source.replace(/^export /gm,'')+'\nglobalThis.api={mount,update,dispose,frameAt,sampleAnchor,safeAsset};',context);
    const options={instanceId:'test',assetRoot:'/assets/avatars/raymond',avatarId:'raymond',action,replayKey:0,paused:false,size:256,enabled:true,itemId:'book',projectileId:'pulse_blue'};
    return{api:context.api,host,actor,overlay,options,frames,listeners,mediaListeners,document,media,
        step(wall,actorTime){animation.currentTime=actorTime;const work=[...frames.values()];frames.clear();for(const fn of work)fn(wall);},
        entities(role){return overlay.children.filter(node=>node.dataset.layer===role);},
        update(change){Object.assign(options,change);context.api.update(host,{...options});},
        restart(){animation={currentTime:0};}
    };
}

test('frame clocks preserve eight-frame loop/one-shot semantics and interpolate calibrated hands',()=>{
    const {api}=setup();
    assert.equal(api.frameAt(1000,8,8,false).frame,7);
    assert.equal(api.frameAt(1000,8,8,false).finished,true);
    assert.equal(api.frameAt(1250,8,8,true).frame,2);
    assert.equal(api.frameAt(1250,8,8,true).cycle,1);
    const hand=api.sampleAnchor({interpolation:'linear',samples:[{frame:4,x:100,y:200},{frame:5,x:120,y:180}]},4.5);
    assert.equal(hand.x,110);assert.equal(hand.y,190);
});

test('a neutral pickup attaches only at frame4 and swaps independent items without touching the body sheet',async()=>{
    const env=setup();assert.equal(await env.api.mount(env.host,env.options),true);
    env.step(0,0);assert.equal(env.entities('item').length,1);assert.equal(env.entities('item')[0].dataset.attached,'false');
    env.step(375,375);assert.equal(env.entities('item')[0].dataset.attached,'false');
    env.step(500,500);assert.equal(env.entities('item')[0].dataset.attached,'true');
    const atContact=Number(env.entities('item')[0].dataset.worldY);
    env.step(750,750);assert.ok(Number(env.entities('item')[0].dataset.worldY)<atContact);
    env.update({itemId:'coin'});assert.equal(env.entities('item')[0].dataset.entityId,'coin');assert.equal(env.entities('item')[0].dataset.attached,'true');
    assert.equal(env.actor.style.backgroundImage,'url(original-character-sheet.png)');
    env.api.dispose('test');assert.equal(env.frames.size,0);assert.equal(env.overlay.children.length,0);
});

test('a projectile spawns once at frame4 then keeps moving after the actor one-shot has finished',async()=>{
    const env=setup('ability_1_pulse');await env.api.mount(env.host,env.options);
    env.step(0,0);env.step(375,375);assert.equal(env.entities('projectile').length,0);
    env.step(500,500);assert.equal(env.entities('projectile').length,1);
    const origin=Number(env.entities('projectile')[0].dataset.worldX);assert.equal(origin,215.5);
    env.step(1000,1000);const atFinish=Number(env.entities('projectile')[0].dataset.worldX);
    env.step(1100,1000);assert.equal(env.entities('projectile').length,1);assert.ok(Number(env.entities('projectile')[0].dataset.worldX)>atFinish);
    assert.equal(env.entities('projectile').length,1);assert.ok(env.frames.size>0);
    env.step(1901,1000);assert.equal(env.entities('projectile').length,0);assert.equal(env.frames.size,0);
    env.api.dispose(env.host);
});

test('pause freezes the independent world clock and replay removes the old projectile before a new release',async()=>{
    const env=setup('ability_1_pulse');await env.api.mount(env.host,env.options);env.step(0,0);env.step(550,550);
    const original=env.entities('projectile')[0],position=original.dataset.worldX;
    env.update({paused:true});assert.equal(env.frames.size,0);env.step(1550,550);assert.equal(original.dataset.worldX,position);
    env.update({paused:false});env.step(1600,550);assert.equal(original.dataset.worldX,position);
    env.step(1700,650);assert.ok(Number(original.dataset.worldX)>Number(position));
    env.restart();env.update({replayKey:1});assert.equal(env.entities('projectile').length,0);assert.equal(original.removed,true);
    env.step(1800,0);env.step(2300,500);assert.equal(env.entities('projectile').length,1);
    env.api.dispose('test');assert.equal(env.frames.size,0);assert.equal(env.listeners.get('visibilitychange').size,0);assert.equal(env.mediaListeners.size,0);
});

test('layer toggles and reduced motion clear entities and stop the RAF without replaying missed pulses',async()=>{
    const env=setup('ability_1_pulse');await env.api.mount(env.host,env.options);env.step(0,0);env.step(500,500);
    env.update({enabled:false});assert.equal(env.frames.size,0);assert.equal(env.overlay.children.length,0);
    env.step(1100,1000);env.update({enabled:true});env.step(1200,1000);assert.equal(env.entities('projectile').length,0);
    env.restart();env.update({replayKey:1});env.step(1300,0);env.step(1800,500);assert.equal(env.entities('projectile').length,1);
    env.media.matches=true;for(const fn of env.mediaListeners)fn();assert.equal(env.frames.size,0);assert.equal(env.overlay.children.length,0);
    env.api.dispose('test');
});

test('only curated local image paths are accepted',()=>{
    const {api}=setup();assert.equal(api.safeAsset('/assets/avatars/raymond','items/book.png'),'/assets/avatars/raymond/items/book.png');
    for(const file of ['https://example.com/a.png','items/../book.png','items/book.png?x=1','items/book.png\r','items/book.png\n','/items/book.png'])assert.equal(api.safeAsset('/assets/avatars/raymond',file),null);
    assert.equal(api.safeAsset('https://example.com','items/book.png'),null);
});
