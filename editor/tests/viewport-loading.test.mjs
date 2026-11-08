import test from 'node:test';
import assert from 'node:assert/strict';
import { observeViewport } from '../src/viewport-loading.mjs';

test('blocks activate near the viewport, pause in background tabs and clean up observers', () => {
  const originalDocument=globalThis.document,originalObserver=globalThis.IntersectionObserver;
  const listeners=new Map(),observers=[];
  const document={visibilityState:'visible',addEventListener:(name,handler)=>listeners.set(name,handler),removeEventListener:name=>listeners.delete(name)};
  globalThis.document=document;
  globalThis.IntersectionObserver=class {
    targets=new Set();
    constructor(callback,options){this.callback=callback;this.options=options;observers.push(this);}
    observe(element){this.targets.add(element);}
    unobserve(element){this.targets.delete(element);}
    disconnect(){this.targets.clear();this.disconnected=true;}
  };
  let stopA,stopB;
  try {
    const first={},second={},statesA=[],statesB=[];
    stopA=observeViewport(first,active=>statesA.push(active));
    stopB=observeViewport(second,active=>statesB.push(active));
    assert.equal(observers.length,1,'all blocks share one native observer');
    const observer=observers[0];
    assert.equal(observer.options.rootMargin,'240px 0px');
    assert.deepEqual(statesA,[false]);
    assert.deepEqual(statesB,[false]);
    observer.callback([{target:first,isIntersecting:true}]);
    assert.deepEqual(statesA,[false,true]);
    assert.deepEqual(statesB,[false],'offscreen blocks stay idle');
    document.visibilityState='hidden';listeners.get('visibilitychange')();
    assert.deepEqual(statesA,[false,true,false]);
    document.visibilityState='visible';listeners.get('visibilitychange')();
    assert.deepEqual(statesA,[false,true,false,true]);
    observer.callback([{target:first,isIntersecting:false}]);
    assert.equal(statesA.at(-1),false);
    stopA();stopA=undefined;
    observer.callback([{target:first,isIntersecting:true}]);
    assert.equal(statesA.at(-1),false,'unmounted blocks ignore late observations');
    stopB();stopB=undefined;
    assert.equal(observer.disconnected,true);
    assert.equal(listeners.size,0);
  } finally {
    stopA?.();stopB?.();
    globalThis.document=originalDocument;globalThis.IntersectionObserver=originalObserver;
  }
});

test('browsers without IntersectionObserver can still open media', () => {
  const originalDocument=globalThis.document,originalObserver=globalThis.IntersectionObserver;
  globalThis.document={visibilityState:'visible',addEventListener(){},removeEventListener(){}};
  globalThis.IntersectionObserver=undefined;
  let stop;
  try {
    const states=[];stop=observeViewport({},active=>states.push(active));
    assert.deepEqual(states,[true]);
  } finally {
    stop?.();globalThis.document=originalDocument;globalThis.IntersectionObserver=originalObserver;
  }
});
