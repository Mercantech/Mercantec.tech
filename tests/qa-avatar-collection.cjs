/* Local UI QA only. Synthetic credentials never leave the isolated browser. */
const fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const assert=require('node:assert/strict');
function dependency(name){try{return require(name);}catch{return require(path.join(os.homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules',name));}}
const {chromium}=dependency('playwright');
const base=process.env.LUP_QA_URL||'http://localhost:3000';
// One isolated harness verifies any complete character pack without copying tests.
globalThis.__qaAvatar=process.env.LUP_QA_AVATAR==='lupp'
  ? {id:'lupp',name:'LUPP',profilePath:'/characters/lupp'}
  : process.env.LUP_QA_AVATAR==='santa'
  ? {id:'santa',name:'Julemanden',profilePath:null}
  : {id:'raymond',name:'Raymond',profilePath:'/characters/raymond'};
const output=process.env.LUP_QA_OUTPUT?path.resolve(process.env.LUP_QA_OUTPUT):path.resolve(__dirname,'../../design/robot-avatar-platform');
const results=[];results.push=(...values)=>{console.log(...values);return Array.prototype.push.apply(results,values);};const errors=[],failedAssets=[];let browser,page;
(async()=>{
  console.log("Starting isolated avatar UI QA");
  await fs.mkdir(output,{recursive:true});
  browser=await chromium.launch({executablePath:process.env.BIP_BROWSER||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
  // An API double shared by otherwise isolated browser/device contexts.
  // Real relational persistence and concurrency are covered by AvatarProfileTests.
  const profiles=new Map(),calls=[],games=new Map(),gameRequests=[]; let rejectMutation=false,rejectLoad=false,rejectGameUpload=false;
  const first='11111111-1111-1111-1111-111111111111';
  const second='22222222-2222-2222-2222-222222222222';
  const legacy='33333333-3333-3333-3333-333333333333';
  const fullAvatarAccount='44444444-4444-4444-4444-444444444444';
  const avatarCatalog={starter:{level:1,cost:0},bip01:{level:2,cost:150},raymond:{level:3,cost:150},santa:{level:4,cost:150},lupp:{level:1,cost:0}};
  const clone=value=>JSON.parse(JSON.stringify(value));
  const profileFor=subject=>{
    // Full-pack playback QA uses a qualified Santa account. Separate focused
    // requirement QA exercises real blocked/progress/third-recipient UI flows.
    if(!profiles.has(subject))profiles.set(subject,{selectedAvatarId:'starter',ownedAvatarIds:['starter'],gold:320,level:7,ownedItemIds:['desk','bed','chair','plant','board'],canImportLegacy:true,revision:0,unlockProgress:[{avatarId:'santa',requirementId:'christmas-gift-recipients',current:subject===fullAvatarAccount?3:0,required:3,isMet:subject===fullAvatarAccount},{avatarId:'lupp',requirementId:'uploaded-game',current:0,required:1,isMet:false}]});
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
        if(url.pathname.endsWith('/me/games')){
          if(req.method()==='GET')return reply(200,games.get(account)||[]);
          assert.equal(req.headers()['x-lup-game-subject'],account);
          assert.match(req.headers()['content-type'],/^multipart\/form-data/);
          const payload=req.postData();assert.ok(payload.includes('<button>Spil</button>'));
          const boundary=req.headers()['content-type'].match(/boundary="?([^";]+)/)?.[1];assert.ok(boundary);
          const requestPart=payload.split('--'+boundary).find(part=>/name="?requestId"?(?:\s|;)/.test(part));
          const requestId=requestPart?.split(/\r?\n\r?\n/)[1]?.trim();assert.ok(requestId);gameRequests.push(requestId);
          if(rejectGameUpload){rejectGameUpload=false;return reply(400,{error:'no_web_entrypoint'});}
          const game={id:'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',fileId:'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',title:'Mit prototypespil',originalName:'prototype.html',entryPoint:'index.html',createdAtUtc:new Date().toISOString()};
          games.set(account,[game]);profileFor(account).unlockProgress.find(p=>p.avatarId==='lupp').current=1;profileFor(account).unlockProgress.find(p=>p.avatarId==='lupp').isMet=true;
          return reply(201,game);
        }
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
            if(id==='santa'&&!state.unlockProgress.some(p=>p.avatarId==='santa'&&p.requirementId==='christmas-gift-recipients'&&p.current>=3&&p.required===3&&p.isMet))return reply(409,{code:'avatar_requirement_not_met',detail:'Send julegaver til mindst tre forskellige brugere'});
            if(id==='lupp'&&!state.unlockProgress.some(p=>p.avatarId==='lupp'&&p.requirementId==='uploaded-game'&&p.current>=1&&p.required===1&&p.isMet))return reply(409,{code:'avatar_requirement_not_met',detail:'Upload dit eget spil'});
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
    await context.addInitScript(avatar=>{window.__qaAvatar=avatar;},globalThis.__qaAvatar);
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
      window.__avatarAudio=[];window.__avatarAudioPlayed=[];window.__avatarAudioFailed=[];const play=HTMLMediaElement.prototype.play;
      HTMLMediaElement.prototype.play=function(...args){const source=this.src;window.__avatarAudio.push(source);return play.apply(this,args).then(()=>{window.__avatarAudioPlayed.push(source);}).catch(error=>{window.__avatarAudioFailed.push(source);throw error;});};
      window.__avatarLayerRafPending=new Set();
      const avatarRequestFrame=window.requestAnimationFrame.bind(window),avatarCancelFrame=window.cancelAnimationFrame.bind(window);
      window.requestAnimationFrame=function(callback){
        const fromLayers=(new Error().stack||'').includes('avatar-layer-preview');let id;
        id=avatarRequestFrame(time=>{if(fromLayers)window.__avatarLayerRafPending.delete(id);callback(time);});
        if(fromLayers)window.__avatarLayerRafPending.add(id);return id;
      };
      window.cancelAnimationFrame=function(id){window.__avatarLayerRafPending.delete(id);return avatarCancelFrame(id);};
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
  assert.equal(await page.locator('.avatar-collectible').count(),5);
  assert.equal(await page.locator('.v2-swatch,.v4-wardrobe').count(),0);
  assert.equal(await page.getByTestId('selected-avatar-name').textContent(),'Din første avatar');
  assert.equal(await page.getByTestId('avatar-gold').textContent(),'320');
  assert.equal(await page.getByTestId('unlock-robot').isEnabled(),true);
  assert.equal(await page.evaluate(()=>window.__avatarAudio.length),0);
  results.push('Collection replaces customization with five figures and 12 noninteractive coming-soon slots.');
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
  assert.equal(await page.locator('.avatar-preview .avatar-sheet[data-avatar="bip01"]').evaluate(el=>getComputedStyle(el).animationName),'none');
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

  const fullAvatarDevice=await device(fullAvatarAccount),fullAvatarPage=observePage(await fullAvatarDevice.newPage());
  page=fullAvatarPage;fullAvatarPage.setDefaultTimeout(90000);
  await fullAvatarPage.goto(base+'/profile/character',{waitUntil:'domcontentloaded'});
  await fullAvatarPage.getByTestId('selected-avatar-name').waitFor();
  if(globalThis.__qaAvatar.id==='lupp'){
    assert.equal(await fullAvatarPage.getByTestId('unlock-lupp').isDisabled(),true);
    assert.match(await fullAvatarPage.locator('[data-avatar-id="lupp"]').textContent(),/0\s*\/\s*1 uploadet spil/);
    await fullAvatarPage.locator('.avatar-requirements-help a[href="/games/mine"]').click();
    await fullAvatarPage.getByRole('heading',{name:'Mine spil',exact:true}).waitFor();
    await fullAvatarPage.getByLabel('Spillets navn',{exact:true}).fill('Mit prototypespil');
    await fullAvatarPage.locator('input[type="file"]').setInputFiles({name:'prototype.html',mimeType:'text/html',buffer:Buffer.from('<!doctype html><button>Spil</button>')});
    rejectGameUpload=true;await fullAvatarPage.getByRole('button',{name:'Upload spil',exact:true}).click();
    await fullAvatarPage.getByRole('alert').filter({hasText:'startside'}).waitFor();
    assert.equal(await fullAvatarPage.getByLabel('Spillets navn',{exact:true}).inputValue(),'Mit prototypespil');
    assert.equal(profileFor(fullAvatarAccount).unlockProgress.find(p=>p.avatarId==='lupp').current,0);
    await fullAvatarPage.getByRole('button',{name:'Upload spil',exact:true}).click();
    await fullAvatarPage.getByRole('status').filter({hasText:'Spillet er uploadet'}).waitFor();
    assert.equal(gameRequests.length,2);assert.equal(gameRequests[0],gameRequests[1]);
    assert.equal(profileFor(second).unlockProgress.find(p=>p.avatarId==='lupp').current,0);
    await fullAvatarPage.getByRole('link',{name:'Vælg LUPP',exact:true}).click();
    await fullAvatarPage.getByTestId('unlock-lupp').waitFor();
    assert.equal(await fullAvatarPage.getByTestId('unlock-lupp').isEnabled(),true);
    assert.equal((games.get(fullAvatarAccount)||[]).length,1);
    results.push('Real browser file input posts owner-bound multipart HTML; failed upload preserves selections and request id, successful private upload advances only the uploader’s LUPP requirement and enables free claim.');
  }
  await fullAvatarPage.getByRole('button',{name:`Se ${globalThis.__qaAvatar.name}`,exact:true}).click();
  await fullAvatarPage.locator(`.avatar-preview .avatar-sheet[data-avatar="${globalThis.__qaAvatar.id}"]`).waitFor();
  assert.equal(await fullAvatarPage.locator('#avatar-preview-title').textContent(),globalThis.__qaAvatar.name);
  assert.match(await fullAvatarPage.locator('.avatar-included').textContent(),/28 bevægelser/);
  assert.match(await fullAvatarPage.locator('.avatar-included').textContent(),/28 lydeffekter/);
  assert.match(await fullAvatarPage.locator('.avatar-included').textContent(),/5 indtalte replikker/);
  assert.match(await fullAvatarPage.locator('.avatar-included').textContent(),/4 emojis/);
  assert.equal(await fullAvatarPage.getByRole('button',{name:'Hør replikken',exact:false}).count(),1);
  if(globalThis.__qaAvatar.profilePath)assert.equal(await fullAvatarPage.locator('.avatar-character-link').getAttribute('href'),globalThis.__qaAvatar.profilePath);
  else assert.equal(await fullAvatarPage.locator('.avatar-character-link').count(),0);
  assert.equal(await fullAvatarPage.evaluate(()=>window.__avatarAudio.length),0,`${globalThis.__qaAvatar.name} preview must not autoplay audio`);
  const fullAvatarPreview=fullAvatarPage.locator(`.avatar-preview .avatar-sheet[data-avatar="${globalThis.__qaAvatar.id}"]`);
  const spriteStyle=await fullAvatarPreview.evaluate(el=>{const s=getComputedStyle(el);return{timing:s.animationTimingFunction,size:s.backgroundSize,image:s.backgroundImage,repeats:s.animationIterationCount};});
  assert.match(spriteStyle.timing,/^steps\(1(?:,\s*end)?\)$/);
  assert.equal(spriteStyle.size,'400% 200%');
  assert.equal(spriteStyle.repeats,'infinite');
  assert.ok(spriteStyle.image.includes(`/assets/avatars/${globalThis.__qaAvatar.id}/sprites/idle.png`));
  const fullAvatarActions=['idle','blink','wave','think','talk','point','celebrate','encourage','surprised','unlock','walk','run','jump','land','sit','sleep','pickup','interact','hit','knockout','respawn','ability_1_pulse','ability_2_magnet','ability_3_shield','ability_4_dash','ability_5_repair','ultimate_charge','ultimate_release'];
  assert.deepEqual(await fullAvatarPage.locator('.avatar-animation-groups [data-action-id]').evaluateAll(buttons=>buttons.map(button=>button.dataset.actionId)),fullAvatarActions);
  assert.deepEqual(await fullAvatarPage.locator('.avatar-action-group legend').allTextContents(),['Sociale udtryk','Bevægelse','Handlinger','Evner']);
  assert.equal(await fullAvatarPage.locator('.avatar-action-group').filter({has:fullAvatarPage.locator('legend').filter({hasText:'Evner'})}).locator('[data-action-id]').count(),7,'Five abilities plus both ultimate phases must be reachable');
  const spriteDimensions=await fullAvatarPage.evaluate(async animations=>Promise.all(animations.map(animation=>new Promise((resolve,reject)=>{
    const image=new Image();image.onload=()=>resolve({animation,width:image.naturalWidth,height:image.naturalHeight});image.onerror=()=>reject(new Error(`Missing ${globalThis.__qaAvatar.name} sprite: `+animation));image.src=`/assets/avatars/${globalThis.__qaAvatar.id}/sprites/`+animation+'.png';
  }))),fullAvatarActions);
  for(const sprite of spriteDimensions){assert.equal(sprite.width,1024,sprite.animation+' width');assert.equal(sprite.height,512,sprite.animation+' height');}
  for(const animation of fullAvatarActions){
    const audioBefore=await fullAvatarPage.evaluate(()=>window.__avatarAudio.length);
    await fullAvatarPage.locator('[data-action-id="'+animation+'"]').click();
    await fullAvatarPage.waitForFunction(id=>document.querySelector(`.avatar-preview .avatar-sheet[data-avatar="${globalThis.__qaAvatar.id}"]`)?.dataset.animation===id,animation);
    assert.equal(await fullAvatarPreview.getAttribute('data-frames'),'8',animation+' frame count');
    assert.ok((await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundImage)).includes('/sprites/'+animation+'.png'),animation+' rendered sheet');
    assert.equal(await fullAvatarPage.evaluate(()=>window.__avatarAudio.length),audioBefore,'Choosing '+animation+' must not autoplay its SFX');
    await fullAvatarPage.getByTestId('avatar-action-sound').click();
    await fullAvatarPage.waitForFunction(before=>window.__avatarAudio.length===before+1,audioBefore);
    assert.ok((await fullAvatarPage.evaluate(()=>window.__avatarAudio.at(-1))).endsWith(`/assets/avatars/${globalThis.__qaAvatar.id}/audio/`+animation+'.wav'),animation+' SFX mapping');
    await fullAvatarPage.waitForFunction(file=>window.__avatarAudioPlayed.some(source=>source.endsWith(file)),`/assets/avatars/${globalThis.__qaAvatar.id}/audio/`+animation+'.wav');
  }
  // Exercise the real CSS animation timeline: it must expose eight different cells, including both rows.
  await fullAvatarPage.locator('[data-action-id="walk"]').click();
  assert.equal(await fullAvatarPreview.getAttribute('data-loop'),'true');
  const framePositions=await fullAvatarPreview.evaluate(el=>{
    const animation=el.getAnimations()[0],duration=Number(animation.effect.getTiming().duration);animation.pause();
    return Array.from({length:8},(_,frame)=>{animation.currentTime=(frame/8+.001)*duration;return getComputedStyle(el).backgroundPosition;});
  });
  assert.equal(new Set(framePositions).size,8,'Animation must show eight distinct CSS cells');
  assert.ok(framePositions.slice(4).every(position=>position.endsWith('100%')),'Second row must actually play');
  await fullAvatarPage.getByRole('button',{name:'Afspil igen',exact:true}).click();
  await fullAvatarPage.getByRole('button',{name:'Pause animation',exact:true}).click();
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).animationPlayState),'paused');
  const pausedFrame=await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundPosition);
  await fullAvatarPage.waitForTimeout(180);
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundPosition),pausedFrame,'Pause must hold the visible frame');
  await fullAvatarPage.getByRole('button',{name:'Fortsæt animation',exact:true}).click();
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).animationPlayState),'running');
  await fullAvatarPage.locator('[data-action-id="hit"]').click();
  assert.equal(await fullAvatarPreview.getAttribute('data-loop'),'false');
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).animationIterationCount),'1');
  await fullAvatarPage.waitForFunction(()=>document.querySelector(`.avatar-preview .avatar-sheet[data-avatar="${globalThis.__qaAvatar.id}"]`)?.getAnimations()[0]?.playState==='finished');
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundPosition),'100% 100%','One-shot holds its final drawn frame');
  await fullAvatarPage.getByRole('button',{name:'Afspil igen',exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector(`.avatar-preview .avatar-sheet[data-avatar="${globalThis.__qaAvatar.id}"]`)?.getAnimations()[0]?.playState==='running');
  const phraseFiles=['phrase_1_welcome.wav','phrase_2_start.wav','phrase_3_success.wav','phrase_4_retry.wav','phrase_5_milestone.wav'];
  const audioHeaders=await fullAvatarPage.evaluate(async filenames=>Promise.all(filenames.map(async file=>{
    const response=await fetch(`/assets/avatars/${globalThis.__qaAvatar.id}/audio/`+file);const bytes=new Uint8Array(await response.arrayBuffer());
    return{file,status:response.status,length:bytes.length,riff:String.fromCharCode(...bytes.slice(0,4)),wave:String.fromCharCode(...bytes.slice(8,12))};
  })),[...fullAvatarActions.map(action=>action+'.wav'),...phraseFiles]);
  for(const audio of audioHeaders){assert.equal(audio.status,200,audio.file);assert.ok(audio.length>44,audio.file+' empty audio');assert.equal(audio.riff,'RIFF',audio.file);assert.equal(audio.wave,'WAVE',audio.file);}
  const fullAvatarReactions=[`/assets/avatars/${globalThis.__qaAvatar.id}/emoji-happy.png`,`/assets/avatars/${globalThis.__qaAvatar.id}/emoji-laugh.png`,`/assets/avatars/${globalThis.__qaAvatar.id}/emoji-think.png`,`/assets/avatars/${globalThis.__qaAvatar.id}/emoji-celebrate.png`];
  assert.deepEqual(await fullAvatarPage.locator('.avatar-emoji-row a').evaluateAll(links=>links.map(link=>link.getAttribute('href'))),fullAvatarReactions);
  for(const img of await fullAvatarPage.locator('.avatar-emoji-row img').all()){const size=await img.evaluate(async el=>{await el.decode();return{width:el.naturalWidth,height:el.naturalHeight};});assert.deepEqual(size,{width:512,height:512},'Face and reaction assets must retain full512px quality');}
  assert.equal(await fullAvatarPage.locator('.avatar-icon-download').getAttribute('href'),`/assets/avatars/${globalThis.__qaAvatar.id}/face.png`);
  assert.equal(await fullAvatarPage.locator('.avatar-icon-download').getAttribute('download'),`${globalThis.__qaAvatar.id}-avatar.png`);
  const fullAvatarPhrases=new Set();
  for(let i=0;i<5;i++){
    const phrase=await fullAvatarPage.locator('.avatar-phrases blockquote').textContent();fullAvatarPhrases.add(phrase);
    const audioBefore=await fullAvatarPage.evaluate(()=>window.__avatarAudio.length);
    await fullAvatarPage.getByRole('button',{name:'Hør replikken',exact:false}).click();
    await fullAvatarPage.waitForFunction(before=>window.__avatarAudio.length===before+1,audioBefore);
    const phrasePath=`/assets/avatars/${globalThis.__qaAvatar.id}/audio/`+phraseFiles[i];
    assert.equal(new URL(await fullAvatarPage.evaluate(()=>window.__avatarAudio.at(-1)),base).pathname,phrasePath,'Spoken phrase '+(i+1));
    await fullAvatarPage.waitForFunction(file=>window.__avatarAudioPlayed.some(source=>new URL(source,location.href).pathname===file),phrasePath);
    await fullAvatarPage.getByRole('button',{name:'Næste replik',exact:true}).click();
    await fullAvatarPage.waitForFunction(previous=>document.querySelector('.avatar-phrases blockquote')?.textContent!==previous,phrase);
    assert.equal(await fullAvatarPage.evaluate(()=>window.__avatarAudio.length),audioBefore+1,'Changing phrase must not autoplay speech');
  }
  assert.equal(fullAvatarPhrases.size,5);
  assert.deepEqual(await fullAvatarPage.evaluate(()=>window.__avatarAudioFailed),[],`Every explicitly requested ${globalThis.__qaAvatar.name} sound must decode and start successfully`);
  await fullAvatarPage.getByTestId(`unlock-${globalThis.__qaAvatar.id}`).click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]')?.textContent===globalThis.__qaAvatar.name);
  assert.equal(await fullAvatarPage.getByTestId('avatar-gold').textContent(),String(320-avatarCatalog[globalThis.__qaAvatar.id].cost));
  assert.equal(profileFor(fullAvatarAccount).selectedAvatarId,globalThis.__qaAvatar.id);
  assert.ok(profileFor(fullAvatarAccount).ownedAvatarIds.includes(globalThis.__qaAvatar.id));
  assert.equal(await fullAvatarPage.locator(`.lup-account>summary [data-avatar-icon="${globalThis.__qaAvatar.id}"] img`).getAttribute('src'),`/assets/avatars/${globalThis.__qaAvatar.id}/face.png`);
  assert.equal(await fullAvatarPage.evaluate(()=>Object.keys(localStorage).some(key=>key.startsWith('lup-avatar-profile-v1:'))),false);
  results.push(`${globalThis.__qaAvatar.name} has 28 eight-frame sheets, all five abilities and both ultimate phases, explicit playback of 28 SFX and five spoken phrases, working motion/pause/replay/one-shots and a server-backed unlock with the configured price.`);

  console.log('Layer QA: item attachment and replacement');
  // Real world entities must stay separate from the actor sheet and its pose clock.
  const layerStage=fullAvatarPage.locator('.avatar-layer-stage');
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage')?.dataset.layerStatus==='ready');
  const layerToggle=fullAvatarPage.getByLabel('Vis genstande og effekter',{exact:true});
  await layerToggle.check();
  await fullAvatarPage.getByRole('combobox',{name:/^Genstand/}).selectOption('book');
  await fullAvatarPage.locator('[data-action-id="pickup"]').click();
  const pickupSheet=await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundImage);
  await fullAvatarPage.waitForFunction(()=>{
    const stage=document.querySelector('.avatar-layer-stage'),item=stage?.querySelector('[data-layer="item"]');
    return item?.dataset.attached==='true'&&Number(stage.dataset.layerFrame)>=4;
  });
  await fullAvatarPage.getByRole('button',{name:'Pause animation',exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage')?.dataset.layerRunning==='false'&&document.querySelector('.avatar-layer-stage')?.dataset.layerPaused==='true');
  const heldBook=await layerStage.locator('[data-layer="item"]').evaluate(el=>({src:el.getAttribute('src'),x:Number(el.dataset.worldX),y:Number(el.dataset.worldY),attached:el.dataset.attached}));
  assert.ok(heldBook.src.endsWith('/items/book.png'));assert.equal(heldBook.attached,'true');
  await fullAvatarPage.getByRole('combobox',{name:/^Genstand/}).selectOption('coin');
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage [data-layer="item"]')?.dataset.entityId==='coin');
  const heldCoin=await layerStage.locator('[data-layer="item"]').evaluate(el=>({src:el.getAttribute('src'),x:Number(el.dataset.worldX),y:Number(el.dataset.worldY),attached:el.dataset.attached}));
  assert.ok(heldCoin.src.endsWith('/items/coin.png'));assert.equal(heldCoin.attached,'true');
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundImage),pickupSheet,'Changing the item must keep the same empty-handed actor sheet');
  assert.ok(Math.abs(heldBook.x-heldCoin.x)<1&&Math.abs(heldBook.y-heldCoin.y)<1,'A replacement item must use the same current hand anchor: '+JSON.stringify({heldBook,heldCoin}));
  const heldBodyFrame=await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundPosition);
  await fullAvatarPage.waitForTimeout(180);
  const stillCoin=await layerStage.locator('[data-layer="item"]').evaluate(el=>({x:Number(el.dataset.worldX),y:Number(el.dataset.worldY)}));
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundPosition),heldBodyFrame);
  assert.deepEqual(stillCoin,{x:heldCoin.x,y:heldCoin.y},'Pause must freeze attached world entities');
  await fullAvatarPage.getByRole('button',{name:'Fortsæt animation',exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>Number(document.querySelector('.avatar-layer-stage')?.dataset.layerFrame)>=7);
  const raisedCoinY=await layerStage.locator('[data-layer="item"]').evaluate(el=>Number(el.dataset.worldY));
  assert.ok(raisedCoinY<heldCoin.y-1,'The independent item must rise with the hand after attachment');
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundImage),pickupSheet);
  results.push('Book and coin share the same pickup actor sheet, attach to the same hand, follow its motion and freeze together when paused.');

  console.log('Layer QA: independent projectile motion');
  await fullAvatarPage.getByRole('combobox',{name:/^Projektil/}).selectOption('pulse_blue');
  await fullAvatarPage.locator('[data-action-id="ability_1_pulse"]').click();
  const pulseSheet=await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundImage);
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage')?.dataset.layerFrame==='2');
  assert.equal(await layerStage.locator('[data-layer="projectile"]').count(),0,'The projectile must not exist before the release pose');
  await fullAvatarPage.waitForFunction(()=>document.querySelectorAll('.avatar-layer-stage [data-layer="projectile"]').length===1);
  const projectileBirth=await layerStage.evaluate(stage=>({frame:Number(stage.dataset.layerFrame),src:stage.querySelector('[data-layer="projectile"] img').getAttribute('src')}));
  assert.equal(projectileBirth.frame,4,'The projectile must be created at release frame4');
  assert.ok(projectileBirth.src.endsWith('/projectiles/pulse_blue.png'));
  await fullAvatarPage.waitForFunction(()=>document.querySelector(`.avatar-preview .avatar-sheet[data-avatar="${globalThis.__qaAvatar.id}"]`)?.getAnimations()[0]?.playState==='finished');
  const endedPose=await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundPosition);
  const afterActor=await layerStage.locator('[data-layer="projectile"]').evaluate(el=>({x:Number(el.dataset.worldX),y:Number(el.dataset.worldY)}));
  await fullAvatarPage.waitForTimeout(120);
  const afterActorLater=await layerStage.locator('[data-layer="projectile"]').evaluate(el=>({x:Number(el.dataset.worldX),y:Number(el.dataset.worldY)}));
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundPosition),endedPose);
  assert.ok(afterActorLater.x>afterActor.x+1,'The projectile must keep moving after the actor has finished its one-shot');
  assert.equal(afterActorLater.y,afterActor.y);
  await layerStage.locator('[data-layer="projectile"]').evaluate(el=>window.__previousAvatarProjectile=el);
  await fullAvatarPage.getByRole('button',{name:'Afspil igen',exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>document.querySelectorAll('.avatar-layer-stage [data-layer="projectile"]').length===0);
  await fullAvatarPage.waitForFunction(()=>document.querySelectorAll('.avatar-layer-stage [data-layer="projectile"]').length===1);
  assert.equal(await fullAvatarPage.evaluate(()=>window.__previousAvatarProjectile.isConnected),false,'Replay must dispose the previous entity');
  assert.equal(await layerStage.locator('[data-layer="projectile"]').count(),1,'Replay must create exactly one fresh entity');
  await fullAvatarPage.getByRole('combobox',{name:/^Projektil/}).selectOption('pulse_gold');
  await fullAvatarPage.getByRole('button',{name:'Afspil igen',exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage [data-layer="projectile"]')?.dataset.entityId==='pulse_gold');
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundImage),pulseSheet,'Projectile selection must not replace the actor art');
  assert.ok((await layerStage.locator('[data-layer="projectile"] img').getAttribute('src')).endsWith('/projectiles/pulse_gold.png'));
  await fullAvatarPage.getByRole('button',{name:'Pause animation',exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage')?.dataset.layerRunning==='false'&&document.querySelector('.avatar-layer-stage')?.dataset.layerPaused==='true');
  const pausedProjectile=await layerStage.locator('[data-layer="projectile"]').evaluate(el=>({x:Number(el.dataset.worldX),y:Number(el.dataset.worldY)}));
  const pausedProjectilePose=await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundPosition);
  await fullAvatarPage.waitForTimeout(180);
  assert.deepEqual(await layerStage.locator('[data-layer="projectile"]').evaluate(el=>({x:Number(el.dataset.worldX),y:Number(el.dataset.worldY)})),pausedProjectile,'Pause must freeze projectile world motion');
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundPosition),pausedProjectilePose);
  await fullAvatarPage.getByRole('button',{name:'Fortsæt animation',exact:true}).click();
  await fullAvatarPage.waitForFunction(x=>Number(document.querySelector('.avatar-layer-stage [data-layer="projectile"]')?.dataset.worldX)>x+1,pausedProjectile.x);
  results.push('Separate pulse entities spawn at frame4, retain independent world motion after the actor finishes, support both designs and replay without duplication.');

  console.log('Layer QA: effect toggle and cleanup');
  await fullAvatarPage.locator('[data-action-id="ability_3_shield"]').click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage [data-layer="effect"]')?.dataset.entityId==='shield');
  const shieldSheet=await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundImage);
  assert.ok((await layerStage.locator('[data-layer="effect"]').getAttribute('src')).endsWith('/effects/shield.png'));
  await layerToggle.uncheck();
  await fullAvatarPage.waitForFunction(()=>document.querySelectorAll('.avatar-layer-stage [data-layer]').length===0);
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).backgroundImage),shieldSheet,'Disabling external effects must leave the actor intact');
  await layerToggle.check();
  await fullAvatarPage.getByRole('button',{name:'Afspil igen',exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage [data-layer="effect"]')?.dataset.entityId==='shield');
  await layerStage.evaluate(stage=>{window.__previousAvatarLayerStage=stage;window.__previousAvatarEffect=stage.querySelector('[data-layer="effect"]');});
  await fullAvatarPage.getByRole('button',{name:'Se BIP-01',exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>window.__avatarLayerRafPending.size===0,{},{timeout:2000});
  assert.equal(await fullAvatarPage.evaluate(()=>window.__previousAvatarLayerStage.isConnected||window.__previousAvatarEffect.isConnected),false,'Avatar switch must dispose the prior stage and its entities');
  await fullAvatarPage.getByRole('button',{name:`Se ${globalThis.__qaAvatar.name}`,exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage')?.dataset.layerStatus==='ready');
  await fullAvatarPage.locator('[data-action-id="pickup"]').click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage [data-layer="item"]')?.dataset.attached==='true');
  await fullAvatarPage.locator('[data-action-id="ability_1_pulse"]').click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage [data-layer="projectile"]'));
  await layerStage.evaluate(stage=>window.__previousAvatarLayerStage=stage);
  const audioBeforeNavigation=await fullAvatarPage.evaluate(()=>window.__avatarAudio.length);
  await fullAvatarPage.locator('.avatar-game-link').click();
  await fullAvatarPage.locator(`.v2-gamegrid .avatar-sheet[data-avatar="${globalThis.__qaAvatar.id}"]`).waitFor();
  await fullAvatarPage.waitForFunction(()=>window.__avatarLayerRafPending.size===0,{},{timeout:2000});
  assert.equal(await fullAvatarPage.evaluate(()=>window.__previousAvatarLayerStage.isConnected),false,'SPA navigation must dispose the layer stage');
  assert.equal(await fullAvatarPage.evaluate(()=>window.__avatarAudio.length),audioBeforeNavigation,'Layer changes and navigation must not autoplay audio');
  await fullAvatarPage.goto(base+'/profile/character',{waitUntil:'domcontentloaded'});await fullAvatarPage.getByTestId('selected-avatar-name').waitFor();
  await fullAvatarPage.getByRole('button',{name:`Se ${globalThis.__qaAvatar.name}`,exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('.avatar-layer-stage')?.dataset.layerStatus==='ready');
  const audioBeforeReducedMotion=await fullAvatarPage.evaluate(()=>window.__avatarAudio.length);
  await fullAvatarPage.emulateMedia({reducedMotion:'reduce'});
  await fullAvatarPage.waitForFunction(()=>document.querySelectorAll('.avatar-layer-stage [data-layer]').length===0&&window.__avatarLayerRafPending.size===0);
  assert.equal(await fullAvatarPreview.evaluate(el=>getComputedStyle(el).animationName),'none');
  assert.equal(await fullAvatarPage.evaluate(()=>window.__avatarAudio.length),audioBeforeReducedMotion);
  await fullAvatarPage.emulateMedia({reducedMotion:'no-preference'});
  await fullAvatarPage.locator('[data-action-id="idle"]').click();
  results.push('Effects can be disabled independently; avatar switching and SPA navigation clean up layer entities/RAF, and reduced motion stops decorative layers without autoplay audio.');

  const fullAvatarSecondDevice=await device(fullAvatarAccount),fullAvatarOther=observePage(await fullAvatarSecondDevice.newPage());fullAvatarOther.setDefaultTimeout(90000);
  await fullAvatarOther.goto(base+'/profile/character',{waitUntil:'domcontentloaded'});await fullAvatarOther.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await fullAvatarOther.getByTestId('selected-avatar-name').textContent(),globalThis.__qaAvatar.name);
  assert.equal(await fullAvatarOther.getByTestId('avatar-gold').textContent(),String(320-avatarCatalog[globalThis.__qaAvatar.id].cost));
  await fullAvatarOther.locator('[data-avatar-id="starter"]').getByRole('button',{name:'Brug avatar',exact:true}).click();
  await fullAvatarOther.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]')?.textContent==='Din første avatar');
  await fullAvatarPage.reload();await fullAvatarPage.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await fullAvatarPage.getByTestId('selected-avatar-name').textContent(),'Din første avatar');
  assert.equal(await fullAvatarPage.getByTestId(`unlock-${globalThis.__qaAvatar.id}`).count(),0);
  await fullAvatarPage.locator(`[data-avatar-id="${globalThis.__qaAvatar.id}"]`).getByRole('button',{name:'Brug avatar',exact:true}).click();
  await fullAvatarPage.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]')?.textContent===globalThis.__qaAvatar.name);
  await fullAvatarOther.reload();await fullAvatarOther.getByTestId('selected-avatar-name').waitFor();
  assert.equal(await fullAvatarOther.getByTestId('selected-avatar-name').textContent(),globalThis.__qaAvatar.name);
  assert.equal(await fullAvatarOther.getByTestId('avatar-gold').textContent(),String(320-avatarCatalog[globalThis.__qaAvatar.id].cost));
  await fullAvatarPage.emulateMedia({reducedMotion:'reduce'});
  assert.equal(await fullAvatarPage.locator(`.avatar-preview .avatar-sheet[data-avatar="${globalThis.__qaAvatar.id}"]`).evaluate(el=>getComputedStyle(el).animationName),'none');
  await fullAvatarPage.screenshot({path:path.join(output,`${globalThis.__qaAvatar.id}-desktop.png`),fullPage:true});
  await fullAvatarPage.setViewportSize({width:390,height:844});
  assert.ok(await fullAvatarPage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`${globalThis.__qaAvatar.name} collection has horizontal mobile overflow`);
  await fullAvatarPage.screenshot({path:path.join(output,`${globalThis.__qaAvatar.id}-mobile.png`),fullPage:true});
  await fullAvatarPage.setViewportSize({width:1440,height:1000});
  for(const [route,selector] of [['/profile',`.panel [data-avatar-icon="${globalThis.__qaAvatar.id}"]`],['/apartment',`.v2-home-sprite [data-avatar="${globalThis.__qaAvatar.id}"]`],['/games/avatar-demo',`.v2-gamegrid [data-avatar="${globalThis.__qaAvatar.id}"]`]]){
    await fullAvatarPage.goto(base+route,{waitUntil:'domcontentloaded'});await fullAvatarPage.locator(selector).waitFor();
  }
  await fullAvatarPage.getByRole('button',{name:'Højre',exact:true}).click();
  assert.equal(await fullAvatarPage.locator(`.v2-gamegrid [data-avatar="${globalThis.__qaAvatar.id}"]`).count(),1);
  results.push(`${globalThis.__qaAvatar.name} selection follows two isolated devices and appears in the header, profile, apartment and Rune Run; mobile and reduced motion pass.`);
  await Promise.all([fullAvatarDevice.close(),fullAvatarSecondDevice.close()]);
  assert.deepEqual(failedAssets,[]);assert.deepEqual(errors,[]);
  const report={passed:true,results,browserErrors:errors,failedAssets,scope:'Isolated UI wiring test with shared API double across device contexts. Actual relational persistence and concurrency verified separately by AvatarProfileTests; no real accounts or external requests.'};
  await fs.writeFile(path.join(output,'qa-report.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));
})().catch(async error=>{console.error(error);if(page&&!page.isClosed()){console.error((await page.locator('body').innerText()).slice(-6000));await page.screenshot({path:path.join(output,'avatar-qa-failure.png'),fullPage:true}).catch(()=>{});}await fs.mkdir(output,{recursive:true});await fs.writeFile(path.join(output,'qa-report.json'),JSON.stringify({passed:false,results,browserErrors:errors,failedAssets,error:error.stack},null,2));process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();});
