/* Local UI QA only. Synthetic credentials never leave the isolated browser. */
const fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const assert=require('node:assert/strict');
function dependency(name){try{return require(name);}catch{return require(path.join(os.homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules',name));}}
const {chromium}=dependency('playwright');
const base=process.env.LUP_QA_URL||'http://localhost:3000';
const output=process.env.LUP_QA_OUTPUT?path.resolve(process.env.LUP_QA_OUTPUT):path.resolve(__dirname,'../../design/robot-avatar-platform');
const results=[];results.push=(...values)=>{console.log(...values);return Array.prototype.push.apply(results,values);};const errors=[],failedAssets=[];let browser,page;
(async()=>{
  console.log("Starting isolated avatar UI QA");
  await fs.mkdir(output,{recursive:true});
  browser=await chromium.launch({executablePath:process.env.BIP_BROWSER||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
  // An API double shared by otherwise isolated browser/device contexts.
  // Real relational persistence and concurrency are covered by AvatarProfileTests.
  const profiles=new Map(),calls=[]; let rejectMutation=false,rejectLoad=false;
  const first='11111111-1111-1111-1111-111111111111';
  const second='22222222-2222-2222-2222-222222222222';
  const legacy='33333333-3333-3333-3333-333333333333';
  const raymondAccount='44444444-4444-4444-4444-444444444444';
  const avatarCatalog={starter:{level:1,cost:0},bip01:{level:2,cost:150},raymond:{level:3,cost:150}};
  const clone=value=>JSON.parse(JSON.stringify(value));
  const profileFor=subject=>{
    if(!profiles.has(subject))profiles.set(subject,{selectedAvatarId:'starter',ownedAvatarIds:['starter'],gold:320,level:7,ownedItemIds:['desk','bed','chair','plant','board'],canImportLegacy:true,revision:0});
    return profiles.get(subject);
  };
  async function device(subject,oldProfile=null){
    const context=await browser.newContext({viewport:{width:1440,height:1000}});
    await context.route('**/*',async route=>{
      const req=route.request(),url=new URL(req.url());
      if(url.pathname.startsWith('/api/')||url.port==='8080'){
        const token=req.headers().authorization?.split(' ')[1];
        const account=token?JSON.parse(Buffer.from(token.split('.')[1],'base64url')).sub:null;
        const reply=(status,value)=>route.fulfill({status,contentType:'application/json',headers:{'access-control-allow-origin':'*'},body:JSON.stringify(value)});
        if(req.method()==='OPTIONS')return route.fulfill({status:204,headers:{'access-control-allow-origin':'*','access-control-allow-methods':'GET,POST,PUT,OPTIONS','access-control-allow-headers':'*'}});
        if(!account)return reply(401,{detail:'Log ind'});
        if(!url.pathname.includes('/me/avatars'))return reply(200,{sub:account,name:'Avatar QA'});
        assert.equal(req.headers()['x-lup-avatar-subject'],account);
        console.log('API',req.method(),url.pathname);calls.push({account,method:req.method(),path:url.pathname});
        if(req.method()==='GET'&&rejectLoad)return reply(503,{detail:'Midlertidig testfejl'});
        if(req.method()!=='GET'&&rejectMutation){rejectMutation=false;return reply(503,{detail:'Gemning mislykkedes. Prøv igen.'});}
        const state=profileFor(account);let changed=false;
        const body=req.postDataJSON();
        const unlockMatch=url.pathname.match(/\/me\/avatars\/([^/]+)\/unlock$/);
        if(unlockMatch){
          const id=decodeURIComponent(unlockMatch[1]),avatar=Object.hasOwn(avatarCatalog,id)?avatarCatalog[id]:null;
          if(!avatar)return reply(404,{code:'avatar_not_found',detail:'Avataren findes ikke'});
          if(!state.ownedAvatarIds.includes(id)){
            if(state.level<avatar.level)return reply(409,{code:'level_required',detail:'Levelkravet er ikke opfyldt'});
            if(state.gold<avatar.cost)return reply(409,{code:'insufficient_gold',detail:'Ikke nok guld'});
            state.gold-=avatar.cost;state.ownedAvatarIds.push(id);state.selectedAvatarId=id;changed=true;
          }
        }else if(url.pathname.endsWith('/selection')){
          if(!Object.hasOwn(avatarCatalog,body.avatarId))return reply(404,{code:'avatar_not_found',detail:'Avataren findes ikke'});
          if(!state.ownedAvatarIds.includes(body.avatarId))return reply(409,{code:'avatar_not_owned',detail:'Lås op først'});
          state.selectedAvatarId=body.avatarId;changed=true;
        }else if(url.pathname.endsWith('/import')){
          if(!state.canImportLegacy)return reply(409,{code:'legacy_import_closed',detail:'Allerede overført'});
          // Raymond and future additions cannot be claimed through old browser data.
          state.ownedAvatarIds=['starter',...(body.ownedAvatarIds.includes('bip01')?['bip01']:[])];
          state.selectedAvatarId=state.ownedAvatarIds.includes(body.selectedAvatarId)?body.selectedAvatarId:'starter';
          state.gold=Math.max(0,Math.min(body.gold,state.ownedAvatarIds.includes('bip01')?170:320));changed=true;
        }else if(url.pathname.endsWith('/items/lamp/buy')){
          if(!state.ownedItemIds.includes('lamp')){state.gold-=80;state.ownedItemIds.push('lamp');changed=true;}
        }
        if(changed){state.canImportLegacy=false;state.revision++;}
        return reply(200,clone(state));
      }
      if(url.origin===base||url.protocol==='data:'||url.protocol==='blob:')return route.continue();
      return route.fulfill({status:200,body:'',contentType:url.pathname.endsWith('.css')?'text/css':'text/plain'});
    });
    await context.addInitScript(({subject,oldProfile})=>{
      const encode=o=>btoa(JSON.stringify(o)).replace(/=/g,'').replace(/\+/g,'-').replace(/\//g,'_');
      const now=Math.floor(Date.now()/1000),token=encode({alg:'HS256',typ:'JWT'})+'.'+encode({sub:subject,name:'Avatar QA',role:'Student',iss:'local-test-only',iat:now,exp:now+7200})+'.bG9jYWwtdGVzdC1vbmx5';
      sessionStorage.setItem('mt_access_token',token);sessionStorage.setItem('mt_refresh_token','');sessionStorage.setItem('mt_expires_at',String(now+7200));
      if(oldProfile&&!localStorage.getItem('avatar-qa-seeded')){
        localStorage.setItem('lup-avatar-profile-v1:user:'+encodeURIComponent(subject),JSON.stringify(oldProfile));
        localStorage.setItem('lup-demo-world',JSON.stringify({gold:80,player:40,room:{9:'desk'},ownedWear:['basic'],ownedFurniture:['desk']}));
        localStorage.setItem('avatar-qa-seeded','yes');
      }
      if(!localStorage.getItem('lup-theme'))localStorage.setItem('lup-theme','light');
      window.__avatarAudio=[];const play=HTMLMediaElement.prototype.play;
      HTMLMediaElement.prototype.play=function(...args){window.__avatarAudio.push(this.src);return play.apply(this,args);};
    },{subject,oldProfile});
    return context;
  }
  const observePage=p=>{
    p.on('console',m=>{if(m.type()==='error')console.error('Browser console',m.text());});
    p.on('pageerror',e=>{console.error('Page error',e.message);errors.push(e.message);});
    p.on('response',r=>{if((r.url().includes('/assets/avatars/')||r.url().includes('/images/characters/raymond/'))&&r.status()>=400)failedAssets.push({url:r.url(),status:r.status()});});
    return p;
  };
  const context=await device(first);
  console.log("Browser context ready");
  page=observePage(await context.newPage());page.setDefaultTimeout(90000);
  const visit=async route=>{console.log('Visiting',route);await page.goto(base+route,{waitUntil:'domcontentloaded'});};
  const ready=()=>page.getByTestId('selected-avatar-name').waitFor({state:'visible',timeout:90000});
  await visit('/profile/character');await ready();
  assert.equal(await page.locator('.avatar-mystery').count(),12);
  assert.equal(await page.locator('.avatar-collectible').count(),3);
  assert.equal(await page.locator('.v2-swatch,.v4-wardrobe').count(),0);
  assert.equal(await page.getByTestId('selected-avatar-name').textContent(),'Din første avatar');
  assert.equal(await page.getByTestId('avatar-gold').textContent(),'320');
  assert.equal(await page.getByTestId('unlock-robot').isEnabled(),true);
  assert.equal(await page.evaluate(()=>window.__avatarAudio.length),0);
  results.push('Collection replaces customization with three figures and 12 noninteractive coming-soon slots.');
  await page.getByTestId('unlock-robot').click();
  await page.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]').textContent==='BIP-01');
  assert.equal(await page.getByTestId('avatar-gold').textContent(),'170');
  assert.equal(await page.locator('.lup-account>summary [data-avatar-icon="bip01"]').count(),1);
  assert.equal(await page.locator('[data-avatar-id="bip01"] button.avatar-selected-button').isDisabled(),true);
  assert.equal(profileFor(first).selectedAvatarId,'bip01');
  assert.equal(await page.evaluate(()=>Object.keys(localStorage).some(k=>k.startsWith('lup-avatar-profile-v1:'))),false);
  assert.equal(calls.some(c=>c.path.endsWith('/import')),false);
  await page.reload();await ready();
  assert.equal(await page.getByTestId('selected-avatar-name').textContent(),'BIP-01');
  assert.equal(await page.getByTestId('avatar-gold').textContent(),'170');
  await page.locator('[data-avatar-id="starter"]').getByRole('button',{name:'Brug avatar',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]').textContent==='Din første avatar');
  await page.locator('[data-avatar-id="bip01"]').getByRole('button',{name:'Brug avatar',exact:true}).click();
  assert.equal(await page.getByTestId('avatar-gold').textContent(),'170');
  await page.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]').textContent==='BIP-01');
  results.push('Unlock costs 150 gold exactly once; selected robot and ownership persist after reload and switching avatars.');
  for(const expression of ['happy','wink','think','celebrate']){
    const img=page.locator('.avatar-emoji-row img[src$="emoji-'+expression+'.png"]');
    assert.equal(await img.evaluate(el=>el.complete&&el.naturalWidth>0),true,expression+' icon missing');
  }
  await page.getByRole('button',{name:'Hør replikken',exact:false}).click();
  await page.waitForFunction(()=>window.__avatarAudio.length>0);
  results.push('Face-only header icon, four emoji downloads and explicit speech playback are available.');
  await page.emulateMedia({reducedMotion:'reduce'});
  assert.equal(await page.locator('.avatar-preview .avatar-robot').evaluate(el=>getComputedStyle(el).animationName),'none');
  await page.getByRole('button',{name:'Stå klar',exact:true}).click();
  await page.evaluate(()=>window.scrollTo(0,0));
  await page.screenshot({path:path.join(output,'collection-desktop.png'),fullPage:true});
  const light=await page.locator('.avatar-preview').evaluate(el=>getComputedStyle(el).backgroundColor);
  await page.evaluate(()=>lupTheme.set('dark'));
  const dark=await page.locator('.avatar-preview').evaluate(el=>getComputedStyle(el).backgroundColor);assert.notEqual(light,dark);
  await page.screenshot({path:path.join(output,'collection-dark.png'),fullPage:true});
  await page.evaluate(()=>lupTheme.set('light'));
  await page.setViewportSize({width:390,height:844});
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Horizontal mobile overflow');
  await page.screenshot({path:path.join(output,'collection-mobile.png'),fullPage:true});
  results.push('Desktop, dark theme,390px mobile layout and reduced-motion rendering pass.');
  await page.setViewportSize({width:1440,height:1000});
  await visit('/games/avatar-demo');
  await page.locator('.v2-gamegrid [data-avatar="bip01"]').waitFor();
  await page.getByRole('button',{name:'Højre',exact:true}).click();
  assert.equal(await page.locator('.v2-gamegrid [data-avatar="bip01"]').count(),1);
  await visit('/apartment');await page.locator('.v2-home-sprite [data-avatar="bip01"]').waitFor();
  await visit('/profile');await page.locator('.panel [data-avatar-icon="bip01"]').waitFor();
  results.push('Selected body follows the player in Rune Run and apartment; profile uses the matching face icon.');
  const secondDevice=await device(first),otherPage=observePage(await secondDevice.newPage());otherPage.setDefaultTimeout(90000);
  await otherPage.goto(base+'/profile/character');await otherPage.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await otherPage.getByTestId('selected-avatar-name').textContent(),'BIP-01');
  assert.equal(await otherPage.getByTestId('avatar-gold').textContent(),'170');
  assert.equal(await otherPage.evaluate(()=>lupAvatars.get('11111111-1111-1111-1111-111111111111')),null);
  await otherPage.locator('[data-avatar-id="starter"]').getByRole('button',{name:'Brug avatar',exact:true}).click();
  await otherPage.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]').textContent==='Din første avatar');
  await visit('/profile/character');await ready();assert.equal(await page.getByTestId('selected-avatar-name').textContent(),'Din første avatar');
  rejectMutation=true;
  await page.locator('[data-avatar-id="bip01"]').getByRole('button',{name:'Brug avatar',exact:true}).click();
  await page.getByRole('alert').waitFor();
  assert.equal(await page.getByTestId('selected-avatar-name').textContent(),'Din første avatar');
  assert.equal(profileFor(first).selectedAvatarId,'starter');
  await page.getByRole('button',{name:'Hent samlingen igen'}).click();
  await page.locator('[data-avatar-id="bip01"]').getByRole('button',{name:'Brug avatar',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]').textContent==='BIP-01');
  results.push('Two isolated device contexts share server ownership and selection; failed saves leave selection unchanged and retry recovers.');
  const unrelated=await device(second),unrelatedPage=observePage(await unrelated.newPage());unrelatedPage.setDefaultTimeout(90000);
  rejectLoad=true;await unrelatedPage.goto(base+'/profile/character');await unrelatedPage.getByRole('alert').waitFor();
  assert.equal(await unrelatedPage.getByTestId('selected-avatar-name').count(),0);
  rejectLoad=false;await unrelatedPage.getByRole('button',{name:'Prøv igen',exact:true}).click();
  await unrelatedPage.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await unrelatedPage.getByTestId('selected-avatar-name').textContent(),'Din første avatar');
  assert.equal(await unrelatedPage.getByTestId('avatar-gold').textContent(),'320');
  results.push('Another account has separate ownership and wallet; initial API outage offers retry without local fallback.');
  const oldDevice=await device(legacy,{ownedAvatarIds:['starter','bip01','raymond'],selectedAvatarId:'bip01'}),oldPage=observePage(await oldDevice.newPage());oldPage.setDefaultTimeout(90000);
  await oldPage.goto(base+'/profile/character');
  await oldPage.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]')?.textContent==='BIP-01');
  assert.equal(await oldPage.getByTestId('avatar-gold').textContent(),'80');
  await oldPage.waitForFunction(()=>!Object.keys(localStorage).some(k=>k.startsWith('lup-avatar-profile-v1:')));
  assert.equal(calls.filter(c=>c.account===legacy&&c.path.endsWith('/import')).length,1);
  assert.equal(profileFor(legacy).ownedAvatarIds.includes('raymond'),false);
  await oldPage.reload();await oldPage.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await oldPage.getByTestId('selected-avatar-name').textContent(),'BIP-01');
  assert.equal(calls.filter(c=>c.account===legacy&&c.path.endsWith('/import')).length,1);
  profileFor(second).gold=80;await unrelatedPage.reload();await unrelatedPage.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await unrelatedPage.getByTestId('unlock-robot').isDisabled(),true);
  await page.evaluate(()=>localStorage.setItem('lup-demo-world',JSON.stringify({gold:99999})));
  await page.reload();await ready();assert.equal(await page.getByTestId('avatar-gold').textContent(),'170');
  results.push('Existing scoped browser profile migrates once and is removed; browser gold changes cannot overwrite server balance.');
  await visit('/universe/shop');
  await page.locator('.v4-product').filter({has:page.getByRole('heading',{name:'Bordlampe',exact:true})}).getByRole('button',{name:'Køb · 80 guld',exact:true}).click();
  await page.waitForFunction(()=>document.body.innerText.includes('90 guld'));
  await otherPage.reload();await otherPage.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await otherPage.getByTestId('avatar-gold').textContent(),'90');
  results.push('Avatar and furniture purchases use the same server wallet across devices.');
  await Promise.all([context.close(),secondDevice.close(),unrelated.close(),oldDevice.close()]);

  const raymondDevice=await device(raymondAccount),raymondPage=observePage(await raymondDevice.newPage());
  page=raymondPage;raymondPage.setDefaultTimeout(90000);
  await raymondPage.goto(base+'/profile/character',{waitUntil:'domcontentloaded'});
  await raymondPage.getByTestId('selected-avatar-name').waitFor();
  await raymondPage.getByRole('button',{name:'Se Raymond',exact:true}).click();
  await raymondPage.locator('.avatar-preview [data-avatar="raymond"]').waitFor();
  assert.equal(await raymondPage.locator('#avatar-preview-title').textContent(),'Raymond');
  assert.match(await raymondPage.locator('.avatar-included').textContent(),/8 bevægelser/);
  assert.match(await raymondPage.locator('.avatar-included').textContent(),/5 replikker/);
  assert.match(await raymondPage.locator('.avatar-included').textContent(),/4 emojis/);
  assert.equal(await raymondPage.getByRole('button',{name:'Hør replikken',exact:false}).count(),0);
  assert.equal(await raymondPage.locator('.avatar-character-link').getAttribute('href'),'/characters/raymond');
  const raymondPreview=raymondPage.locator('.avatar-preview .avatar-raymond');
  const spriteStyle=await raymondPreview.evaluate(el=>{const s=getComputedStyle(el);return{timing:s.animationTimingFunction,size:s.backgroundSize,image:s.backgroundImage};});
  assert.match(spriteStyle.timing,/^steps\(4(?:,\s*end)?\)$/);
  assert.equal(spriteStyle.size,'400% 100%');
  assert.ok(spriteStyle.image.includes('/assets/avatars/raymond/sprites/idle.png'));
  const raymondMoves=[['idle','Stå klar'],['walk','Gå'],['run','Løb'],['jump','Hop'],['wave','Vink'],['celebrate','Jubel'],['think','Tænk'],['talk','Tal']];
  const spriteDimensions=await raymondPage.evaluate(async animations=>Promise.all(animations.map(animation=>new Promise((resolve,reject)=>{
    const image=new Image();image.onload=()=>resolve({animation,width:image.naturalWidth,height:image.naturalHeight});image.onerror=()=>reject(new Error('Missing Raymond sprite: '+animation));image.src='/assets/avatars/raymond/sprites/'+animation+'.png';
  }))),raymondMoves.map(([id])=>id));
  for(const sprite of spriteDimensions){assert.equal(sprite.width,1024,sprite.animation+' width');assert.equal(sprite.height,256,sprite.animation+' height');}
  for(const [animation,label] of raymondMoves){
    await raymondPage.getByRole('button',{name:label,exact:true}).click();
    await raymondPage.waitForFunction(id=>document.querySelector('.avatar-preview [data-avatar="raymond"]')?.dataset.animation===id,animation);
  }
  const raymondReactions=['/images/characters/raymond/reactions/success.png','/images/characters/raymond/reactions/laughing.png?v=097b5eeb','/images/characters/raymond/reactions/thinking.png','/images/characters/raymond/reactions/celebration.png'];
  assert.deepEqual(await raymondPage.locator('.avatar-emoji-row a').evaluateAll(links=>links.map(link=>link.getAttribute('href'))),raymondReactions);
  for(const img of await raymondPage.locator('.avatar-emoji-row img').all())assert.equal(await img.evaluate(async el=>{await el.decode();return el.naturalWidth>0;}),true);
  assert.equal(await raymondPage.locator('.avatar-icon-download').getAttribute('href'),'/images/characters/raymond/neutral.webp');
  const raymondPhrases=new Set();
  for(let i=0;i<5;i++){
    const phrase=await raymondPage.locator('.avatar-phrases blockquote').textContent();raymondPhrases.add(phrase);
    await raymondPage.getByRole('button',{name:'Næste replik',exact:true}).click();
    await raymondPage.waitForFunction(previous=>document.querySelector('.avatar-phrases blockquote')?.textContent!==previous,phrase);
  }
  assert.equal(raymondPhrases.size,5);
  await raymondPage.getByTestId('unlock-raymond').click();
  await raymondPage.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]')?.textContent==='Raymond');
  assert.equal(await raymondPage.getByTestId('avatar-gold').textContent(),'170');
  assert.equal(profileFor(raymondAccount).selectedAvatarId,'raymond');
  assert.ok(profileFor(raymondAccount).ownedAvatarIds.includes('raymond'));
  assert.equal(await raymondPage.locator('.lup-account>summary [data-avatar-icon="raymond"] img').getAttribute('src'),'/images/characters/raymond/neutral.webp');
  assert.equal(await raymondPage.evaluate(()=>Object.keys(localStorage).some(key=>key.startsWith('lup-avatar-profile-v1:'))),false);
  results.push('Raymond has eight four-frame sheets, five distinct text phrases, canonical head/reactions and a paid server-backed unlock.');

  const raymondSecondDevice=await device(raymondAccount),raymondOther=observePage(await raymondSecondDevice.newPage());raymondOther.setDefaultTimeout(90000);
  await raymondOther.goto(base+'/profile/character',{waitUntil:'domcontentloaded'});await raymondOther.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await raymondOther.getByTestId('selected-avatar-name').textContent(),'Raymond');
  assert.equal(await raymondOther.getByTestId('avatar-gold').textContent(),'170');
  await raymondOther.locator('[data-avatar-id="starter"]').getByRole('button',{name:'Brug avatar',exact:true}).click();
  await raymondOther.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]')?.textContent==='Din første avatar');
  await raymondPage.reload();await raymondPage.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await raymondPage.getByTestId('selected-avatar-name').textContent(),'Din første avatar');
  assert.equal(await raymondPage.getByTestId('unlock-raymond').count(),0);
  await raymondPage.locator('[data-avatar-id="raymond"]').getByRole('button',{name:'Brug avatar',exact:true}).click();
  await raymondPage.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]')?.textContent==='Raymond');
  await raymondOther.reload();await raymondOther.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await raymondOther.getByTestId('selected-avatar-name').textContent(),'Raymond');
  assert.equal(await raymondOther.getByTestId('avatar-gold').textContent(),'170');
  await raymondPage.emulateMedia({reducedMotion:'reduce'});
  assert.equal(await raymondPage.locator('.avatar-preview .avatar-raymond').evaluate(el=>getComputedStyle(el).animationName),'none');
  await raymondPage.screenshot({path:path.join(output,'raymond-desktop.png'),fullPage:true});
  await raymondPage.setViewportSize({width:390,height:844});
  assert.ok(await raymondPage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Raymond collection has horizontal mobile overflow');
  await raymondPage.screenshot({path:path.join(output,'raymond-mobile.png'),fullPage:true});
  await raymondPage.setViewportSize({width:1440,height:1000});
  for(const [route,selector] of [['/profile','.panel [data-avatar-icon="raymond"]'],['/apartment','.v2-home-sprite [data-avatar="raymond"]'],['/games/avatar-demo','.v2-gamegrid [data-avatar="raymond"]']]){
    await raymondPage.goto(base+route,{waitUntil:'domcontentloaded'});await raymondPage.locator(selector).waitFor();
  }
  await raymondPage.getByRole('button',{name:'Højre',exact:true}).click();
  assert.equal(await raymondPage.locator('.v2-gamegrid [data-avatar="raymond"]').count(),1);
  results.push('Raymond selection follows two isolated devices and appears in the header, profile, apartment and Rune Run; mobile and reduced motion pass.');
  await Promise.all([raymondDevice.close(),raymondSecondDevice.close()]);
  assert.deepEqual(failedAssets,[]);assert.deepEqual(errors,[]);
  const report={passed:true,results,browserErrors:errors,failedAssets,scope:'Isolated UI wiring test with shared API double across device contexts. Actual relational persistence and concurrency verified separately by AvatarProfileTests; no real accounts or external requests.'};
  await fs.writeFile(path.join(output,'qa-report.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));
})().catch(async error=>{console.error(error);if(page&&!page.isClosed()){console.error((await page.locator('body').innerText()).slice(-6000));await page.screenshot({path:path.join(output,'avatar-qa-failure.png'),fullPage:true}).catch(()=>{});}await fs.mkdir(output,{recursive:true});await fs.writeFile(path.join(output,'qa-report.json'),JSON.stringify({passed:false,results,browserErrors:errors,failedAssets,error:error.stack},null,2));process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();});
