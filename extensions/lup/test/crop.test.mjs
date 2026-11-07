import test from 'node:test';
import assert from 'node:assert/strict';
import { cropRectangle, zoomCrop, dragCrop } from '../crop.mjs';

test('landscape and portrait crops are centered squares using full source resolution', () => {
  assert.deepEqual(cropRectangle(1600, 900), { x: 350, y: 0, size: 900, zoom: 1 });
  assert.deepEqual(cropRectangle(600, 1200), { x: 0, y: 300, size: 600, zoom: 1 });
  assert.deepEqual(cropRectangle(512, 512), { x: 0, y: 0, size: 512, zoom: 1 });
});

test('all pan/zoom extremes stay inside the original image', () => {
  for (const [width, height] of [[1600,900], [600,1200], [512,512], [1,1], [20,400]])
    for (const x of [-100,-1,0,1,100]) for (const y of [-100,-1,0,1,100]) for (const zoom of [-1,1,2,4,99]) {
      const r = cropRectangle(width, height, {x,y,zoom});
      assert(r.x >= 0 && r.y >= 0 && r.size > 0);
      assert(r.x + r.size <= width + 1e-9 && r.y + r.size <= height + 1e-9);
      assert(r.zoom >= 1 && r.zoom <= 4);
    }
});

test('zoom keeps the chosen source center until an edge prevents it', () => {
  const original = {zoom:1,x:.5,y:0};
  const before = cropRectangle(1600,900,original);
  const changed = zoomCrop(1600,900,original,3);
  const after = cropRectangle(1600,900,changed);
  assert.equal(before.x + before.size/2, after.x + after.size/2);
  assert.equal(before.y + before.size/2, after.y + after.size/2);
  assert.deepEqual(zoomCrop(512,512,{zoom:4,x:1,y:1},1),{zoom:1,x:0,y:0});
});

test('dragging uses the displayed scale, follows the image, and clamps to edges', () => {
  const moved = dragCrop(1600,900,{zoom:1,x:0,y:0},35,20,300);
  assert.equal(moved.x,-.3); assert.equal(moved.y,0);
  assert.deepEqual(dragCrop(512,512,{zoom:1,x:0,y:0},10,10,300),{zoom:1,x:0,y:0});
  assert.deepEqual(dragCrop(1600,900,{zoom:4,x:0,y:0},100000,-100000,256),{zoom:4,x:-1,y:1});
});

test('invalid dimensions are rejected before canvas work', () => {
  for (const [w,h] of [[0,3],[-1,3],[1.5,3],[100000,100000],[NaN,2]]) assert.throws(()=>cropRectangle(w,h));
  assert.deepEqual(cropRectangle(500,500,{zoom:NaN,x:NaN,y:Infinity}),{x:0,y:0,size:500,zoom:1});
});
