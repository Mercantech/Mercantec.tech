/* Local UI QA only. Synthetic credentials never leave the isolated browser. */
const fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const assert=require('node:assert/strict');
function dependency(name){try{return require(name);}catch{return require(path.join(os.homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules',name));}}
const {chromium}=dependency('playwright');
const base=process.env.LUP_QA_URL||'http://localhost:3000';
const output=path.resolve(__dirname,'../../design/robot-avatar-platform');
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
        if(url.pathname.endsWith('/unlock')){
          if(!state.ownedAvatarIds.includes('bip01')){if(state.gold<150)return reply(409,{code:'insufficient_gold',detail:'Ikke nok guld'});state.gold-=150;state.ownedAvatarIds.push('bip01');state.selectedAvatarId='bip01';changed=true;}
        }else if(url.pathname.endsWith('/selection')){
          if(!state.ownedAvatarIds.includes(body.avatarId))return reply(409,{code:'avatar_not_owned',detail:'Lås op først'});
          state.selectedAvatarId=body.avatarId;changed=true;
        }else if(url.pathname.endsWith('/import')){
          if(!state.canImportLegacy)return reply(409,{code:'legacy_import_closed',detail:'Allerede overført'});
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
  const context=await device(first);
  console.log("Browser context ready");
  page=await context.newPage();page.setDefaultTimeout(45000);
  page.on('console',m=>{if(m.type()==='error')console.error('Browser console',m.text());});
  page.on('pageerror',e=>{console.error('Page error',e.message);errors.push(e.message);});
  page.on('response',r=>{if(r.url().includes('/assets/avatars/')&&r.status()>=400)failedAssets.push({url:r.url(),status:r.status()});});
  const visit=async route=>{console.log('Visiting',route);await page.goto(base+route,{waitUntil:'domcontentloaded'});};
  const ready=()=>page.getByTestId('selected-avatar-name').waitFor({state:'visible',timeout:90000});
  await visit('/profile/character');await ready();
  assert.equal(await page.locator('.avatar-mystery').count(),12);
  assert.equal(await page.locator('.avatar-collectible').count(),2);
  assert.equal(await page.locator('.v2-swatch,.v4-wardrobe').count(),0);
  assert.equal(await page.getByTestId('selected-avatar-name').textContent(),'Din første avatar');
  assert.equal(await page.getByTestId('avatar-gold').textContent(),'320');
  assert.equal(await page.getByTestId('unlock-robot').isEnabled(),true);
  assert.equal(await page.evaluate(()=>window.__avatarAudio.length),0);
  results.push('Collection replaces customization with two figures and 12 noninteractive coming-soon slots.');
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
  const secondDevice=await device(first),otherPage=await secondDevice.newPage();otherPage.setDefaultTimeout(90000);
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
  const unrelated=await device(second),unrelatedPage=await unrelated.newPage();unrelatedPage.setDefaultTimeout(90000);
  rejectLoad=true;await unrelatedPage.goto(base+'/profile/character');await unrelatedPage.getByRole('alert').waitFor();
  assert.equal(await unrelatedPage.getByTestId('selected-avatar-name').count(),0);
  rejectLoad=false;await unrelatedPage.getByRole('button',{name:'Prøv igen',exact:true}).click();
  await unrelatedPage.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await unrelatedPage.getByTestId('selected-avatar-name').textContent(),'Din første avatar');
  assert.equal(await unrelatedPage.getByTestId('avatar-gold').textContent(),'320');
  results.push('Another account has separate ownership and wallet; initial API outage offers retry without local fallback.');
  const oldDevice=await device(legacy,{ownedAvatarIds:['starter','bip01'],selectedAvatarId:'bip01'}),oldPage=await oldDevice.newPage();oldPage.setDefaultTimeout(90000);
  await oldPage.goto(base+'/profile/character');
  await oldPage.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]')?.textContent==='BIP-01');
  assert.equal(await oldPage.getByTestId('avatar-gold').textContent(),'80');
  await oldPage.waitForFunction(()=>!Object.keys(localStorage).some(k=>k.startsWith('lup-avatar-profile-v1:')));
  assert.equal(calls.filter(c=>c.account===legacy&&c.path.endsWith('/import')).length,1);
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
  await Promise.all([secondDevice.close(),unrelated.close(),oldDevice.close()]);
  assert.deepEqual(failedAssets,[]);assert.deepEqual(errors,[]);
  const report={passed:true,results,browserErrors:errors,failedAssets,scope:'Isolated UI wiring test with shared API double across device contexts. Actual relational persistence and concurrency verified separately by AvatarProfileTests; no real accounts or external requests.'};
  await fs.writeFile(path.join(output,'qa-report.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));
})().catch(async error=>{console.error(error);if(page){console.error((await page.locator('body').innerText()).slice(-6000));await page.screenshot({path:path.join(output,'avatar-qa-failure.png'),fullPage:true}).catch(()=>{});}await fs.mkdir(output,{recursive:true});await fs.writeFile(path.join(output,'qa-report.json'),JSON.stringify({passed:false,results,browserErrors:errors,failedAssets,error:error.stack},null,2));process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();});
