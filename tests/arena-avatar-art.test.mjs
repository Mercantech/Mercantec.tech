import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const source=await readFile(path.join(root,'web/wwwroot/js/arena-avatar-art.js'),'utf8');
const art=await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'));

test('original fighters load each battle action from its own eight-frame sprite sheet',async()=>{
 const assets=art.originalAvatarAssets();
 assert.equal(assets.length,51);
 assert.equal(new Set(assets.map(([key])=>key)).size,51);
 for(const [key,url] of assets){
  assert.match(key,/^avatar-(bip01|raymond|santa)-/);
  assert.match(url,/^\/assets\/avatars\/(bip-01|raymond|santa)\/sprites\/[^/]+\.png$/);
  assert.doesNotMatch(url,/arena\/v2/);
  const file=path.join(root,'web/wwwroot',url);
  assert.ok((await stat(file)).size>0);
  const png=await readFile(file);
  assert.equal(png.readUInt32BE(16),1024,'authored sheet width');
  assert.equal(png.readUInt32BE(20),512,'authored sheet height');
 }
});

test('every authored attack and ultimate frame is preserved for every original fighter',()=>{
 for(const avatarId of ['bip01','raymond','santa'])for(const action of ['ability_1_pulse','ability_2_magnet','ability_4_dash','ability_5_repair','ultimate_charge','ultimate_release']){
  const timeline=new Map();const frames=[];
  for(let phase=0;phase<8;phase++){const actual=art.originalAvatarFrame({id:'p1',avatarId,action,actionFrame:phase},1000+phase*17,timeline);frames.push(actual.frame);assert.equal(actual.action,action);assert.equal(actual.columns,4);assert.equal(actual.rows,2);assert.equal(actual.originY,224/256);}
  assert.deepEqual(frames,[0,1,2,3,4,5,6,7]);
 }
});

test('movement follows authored fps and one-shot state changes start at frame zero',()=>{
 const timeline=new Map(),fighter={id:'p1',avatarId:'raymond',action:'run',actionFrame:7};
 assert.equal(art.originalAvatarFrame(fighter,1000,timeline).frame,0);
 assert.equal(art.originalAvatarFrame(fighter,1250,timeline).frame,3);
 fighter.action='idle';assert.equal(art.originalAvatarFrame(fighter,1500,timeline).frame,0);
 assert.equal(art.originalAvatarFrame(fighter,1750,timeline).frame,2);
 fighter.action='hit';assert.equal(art.originalAvatarFrame(fighter,2000,timeline).frame,0);
 assert.equal(art.originalAvatarFrame(fighter,4000,timeline).frame,7);
 fighter.action='ability_3_shield';assert.equal(art.originalAvatarFrame(fighter,5000,timeline).frame,0);
 assert.equal(art.originalAvatarFrame(fighter,7000,timeline).frame,7);
});

test('starter atlases stay separate and invalid original actions safely use idle',()=>{
 assert.equal(art.originalAvatarFrame({id:'p1',avatarId:'starter',action:'run'},0,new Map()),null);
 assert.equal(art.originalAvatarSource('starter','run'),null);
 assert.equal(art.originalAvatarSource('bip01','../../evil'),'/assets/avatars/bip-01/sprites/idle.png');
 assert.equal(art.originalAvatarFrame({id:'p2',avatarId:'bip01',action:'not-an-action'},0,new Map()).action,'idle');
});
