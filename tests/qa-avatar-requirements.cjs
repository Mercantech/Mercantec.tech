/* Actual local Blazor UI, synthetic gift/profile API; no real gifts or accounts. */
'use strict';
const fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const assert=require('node:assert/strict');
function dependency(name){try{return require(name);}catch{return require(path.join(os.homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules',name));}}
const {chromium}=dependency('playwright');
const base=process.argv[2]||'http://localhost:3003';
const output=path.resolve(process.argv[3]||path.join(__dirname,'../../avatar-requirements-20261109'));
const first='11111111-1111-4111-8111-111111111111',second='99999999-9999-4999-8999-999999999999';
const recipients=['22222222-2222-4222-8222-222222222222','33333333-3333-4333-8333-333333333333','44444444-4444-4444-8444-444444444444'];
const profiles=new Map(),gifts=new Map(),drafts=[],calls=[],errors=[],failedAssets=[],checks=[];
let browser,page,failNextSend=false;
const clone=x=>JSON.parse(JSON.stringify(x));
function profileFor(subject){if(!profiles.has(subject))profiles.set(subject,{selectedAvatarId:'starter',ownedAvatarIds:['starter'],gold:320,level:7,ownedItemIds:['desk','bed','chair','plant','board'],canImportLegacy:false,revision:0});return profiles.get(subject);}
function count(subject){return new Set([...gifts.values()].filter(g=>g.senderSub===subject&&g.theme==='christmas'&&g.recipientSub!==subject).map(g=>g.recipientSub)).size;}
function snapshot(subject){return {...clone(profileFor(subject)),unlockProgress:[{avatarId:'santa',requirementId:'christmas-gift-recipients',current:count(subject),required:3,isMet:count(subject)>=3}]};}
function giftDto(g){return {id:g.id,senderName:'Syntetisk afsender',recipientName:'Syntetisk modtager',kind:g.kind,wrapping:g.wrapping,message:g.message,createdAtUtc:g.createdAtUtc,openedAtUtc:null,theme:g.theme};}
async function device(subject,viewport={width:1440,height:1000}){
  const context=await browser.newContext({viewport});
  await context.route('**/*',async route=>{
    const request=route.request(),url=new URL(request.url());
    if(url.pathname.startsWith('/api/')||url.port==='8080'){
      const reply=(status,value)=>route.fulfill({status,contentType:'application/json',headers:{'access-control-allow-origin':'*'},body:JSON.stringify(value)});
      if(request.method()==='OPTIONS')return route.fulfill({status:204,headers:{'access-control-allow-origin':'*','access-control-allow-methods':'GET,POST,PUT,OPTIONS','access-control-allow-headers':'*'}});
      const token=request.headers().authorization?.split(' ')[1];
      const account=token?JSON.parse(Buffer.from(token.split('.')[1],'base64url')).sub:null;
      if(!account)return reply(401,{detail:'Syntetisk login kræves'});
      calls.push({account,method:request.method(),path:url.pathname});
      if(url.pathname.endsWith('/gifts/profile'))return route.fulfill({status:204,headers:{'access-control-allow-origin':'*'}});
      if(url.pathname.endsWith('/gifts/recipients'))return reply(200,recipients.map(id=>({userSub:id,displayName:'Testperson',isTeacher:false})));
      if(url.pathname.endsWith('/gifts')){
        if(request.method()==='GET')return reply(200,{received:[],sent:[...gifts.values()].filter(g=>g.senderSub===account).map(giftDto),unread:0});
        assert.equal(request.headers()['x-lup-gift-subject'],account);
        const draft=request.postDataJSON();drafts.push({account,...clone(draft)});
        if(failNextSend){failNextSend=false;return reply(503,{error:'unavailable'});}
        const existing=gifts.get(draft.id);
        if(existing){
          if(existing.senderSub!==account||['recipientSub','kind','wrapping','message','theme'].some(key=>existing[key]!==draft[key]))return reply(409,{error:'request_conflict'});
          return reply(200,giftDto(existing));
        }
        assert.ok(recipients.includes(draft.recipientSub));
        const gift={...draft,senderSub:account,createdAtUtc:new Date().toISOString()};gifts.set(draft.id,gift);
        return reply(201,giftDto(gift));
      }
      if(url.pathname.includes('/me/avatars')){
        assert.equal(request.headers()['x-lup-avatar-subject'],account);
        const profile=profileFor(account),body=request.postDataJSON();
        if(url.pathname.endsWith('/santa/unlock')){
          if(!profile.ownedAvatarIds.includes('santa')){
            if(count(account)<3)return reply(409,{code:'avatar_requirement_not_met',detail:'Send julegaver til tre forskellige brugere.'});
            if(profile.gold<150)return reply(409,{code:'insufficient_gold',detail:'Ikke nok guld'});
            profile.ownedAvatarIds.push('santa');profile.gold-=150;profile.selectedAvatarId='santa';profile.revision++;
          }
        }else if(url.pathname.endsWith('/selection')){
          if(!profile.ownedAvatarIds.includes(body.avatarId))return reply(409,{code:'avatar_not_owned',detail:'Lås figuren op først'});
          profile.selectedAvatarId=body.avatarId;profile.revision++;
        }else if(request.method()!=='GET')return reply(400,{detail:'Uventet fixturekald'});
        return reply(200,snapshot(account));
      }
      return reply(200,{sub:account,name:'Avatar krav QA'});
    }
    if(url.origin===base||url.protocol==='data:'||url.protocol==='blob:')return route.continue();
    return route.fulfill({status:200,body:'',contentType:url.pathname.endsWith('.css')?'text/css':'text/plain'});
  });
  await context.addInitScript(subject=>{
    const encode=x=>btoa(JSON.stringify(x)).replace(/=/g,'').replace(/\+/g,'-').replace(/\//g,'_');
    const now=Math.floor(Date.now()/1000),token=encode({alg:'HS256',typ:'JWT'})+'.'+encode({sub:subject,name:'Avatar krav QA',role:'Student',iss:'local-test-only',iat:now,exp:now+7200})+'.bG9jYWwtdGVzdC1vbmx5';
    sessionStorage.setItem('mt_access_token',token);sessionStorage.setItem('mt_refresh_token','');sessionStorage.setItem('mt_expires_at',String(now+7200));localStorage.setItem('lup-theme','light');
  },subject);
  return context;
}
function observe(p){p.setDefaultTimeout(60000);p.on('pageerror',error=>errors.push(error.message));p.on('response',response=>{if(response.url().includes('/assets/avatars/')&&response.status()>=400)failedAssets.push({url:response.url(),status:response.status()});});return p;}
async function ready(p){await p.getByTestId('selected-avatar-name').waitFor();}
async function progress(p,current){await p.waitForFunction(n=>new RegExp('^\\s*'+n+'\\s*/\\s*3\\b').test(document.querySelector('[data-testid="requirement-progress-santa-christmas-gift-recipients"]')?.textContent||''),current);assert.equal(await p.getByTestId('unlock-santa').isDisabled(),current<3);assert.equal(await p.getByTestId('avatar-gold').textContent(),'320');}
async function showSend(p,theme){await p.evaluate(t=>lupTheme.set(t),theme);if(!await p.locator('#lup-gift-dialog').evaluate(el=>el.open))await p.locator('.gift-launcher').click();await p.locator('#lup-gift-dialog').getByRole('button',{name:'Send en gave',exact:true}).first().click();await p.locator('.gift-recipient input').first().waitFor();}
async function send(p,recipient,theme){await showSend(p,theme);await p.locator(`.gift-recipient input[value="${recipient}"]`).check();await p.locator('#gift-message').fill('Syntetisk julegave til avatar-kontrol.');await p.locator('.gift-actions .gift-primary').click();await p.locator('#gift-feedback').waitFor();}
(async()=>{
  await fs.mkdir(output,{recursive:true});browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
  const one=await device(first);page=observe(await one.newPage());await page.goto(base+'/profile/character',{waitUntil:'domcontentloaded'});await ready(page);await progress(page,0);
  assert.ok(await page.locator('[data-avatar-id="santa"]').innerText().then(text=>text.includes('juletemaet')));
  checks.push('Santa starts locked at0/3 despite level7 and320gold; the exact gift condition is visible.');
  await send(page,recipients[0],'christmas');await progress(page,1);
  await send(page,recipients[0],'christmas');await progress(page,1);
  assert.equal(count(first),1);checks.push('Two Christmas gifts to the same user leave progress at1/3.');
  await send(page,recipients[1],'newyear');await progress(page,1);assert.equal(drafts.at(-1).theme,'newyear');
  checks.push('A successfully sent NewYear gift does not advance the Christmas requirement.');
  await send(page,recipients[1],'christmas');await progress(page,2);
  await page.locator('.gift-close').click();await page.screenshot({path:path.join(output,'santa-requirement-2-desktop.png'),fullPage:true});
  await page.setViewportSize({width:390,height:844});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));await page.screenshot({path:path.join(output,'santa-requirement-2-mobile.png'),fullPage:true});await page.setViewportSize({width:1440,height:1000});
  checks.push('The2/3 condition remains readable and locked on desktop and390px mobile without overflow.');
  failNextSend=true;await showSend(page,'christmas');await page.locator(`.gift-recipient input[value="${recipients[2]}"]`).check();await page.locator('#gift-message').fill('Syntetisk genforsøg.');await page.locator('.gift-actions .gift-primary').click();await page.locator('#gift-error').waitFor();
  const firstAttempt=drafts.at(-1);await progress(page,2);await page.evaluate(()=>lupTheme.set('newyear'));await page.locator('.gift-actions .gift-primary').click();await page.locator('#gift-feedback').waitFor();
  assert.equal(drafts.at(-1).id,firstAttempt.id);assert.equal(drafts.at(-1).theme,'christmas');assert.equal(firstAttempt.theme,'christmas');await progress(page,3);await page.locator('.gift-close').click();
  checks.push('A failed send preserves the original requestID/theme across a later theme switch; its successful third-recipient retry refreshes progress to3/3 automatically.');
  const another=await device(first),anotherPage=observe(await another.newPage());await anotherPage.goto(base+'/profile/character',{waitUntil:'domcontentloaded'});await ready(anotherPage);await progress(anotherPage,3);
  await page.getByTestId('unlock-santa').click();await page.waitForFunction(()=>document.querySelector('[data-testid="selected-avatar-name"]')?.textContent==='Julemanden');assert.equal(profileFor(first).gold,170);assert.equal(await page.getByTestId('avatar-gold').textContent(),'170');
  await anotherPage.reload();await ready(anotherPage);assert.equal(await anotherPage.getByTestId('selected-avatar-name').textContent(),'Julemanden');assert.equal(await anotherPage.getByTestId('avatar-gold').textContent(),'170');
  assert.equal(calls.filter(call=>call.account===first&&call.path.endsWith('/santa/unlock')).length,1);
  checks.push('Qualified progress follows a second isolated device; purchasing charges150gold once and ownership/selection follow the account.');
  const other=await device(second),otherPage=observe(await other.newPage());await otherPage.goto(base+'/profile/character',{waitUntil:'domcontentloaded'});await ready(otherPage);await progress(otherPage,0);assert.equal(await otherPage.getByTestId('selected-avatar-name').textContent(),'Din første avatar');
  checks.push('An unrelated account starts at0/3 with its own wallet and cannot select Santa.');
  assert.deepEqual(errors,[]);assert.deepEqual(failedAssets,[]);
  await page.screenshot({path:path.join(output,'santa-unlocked-desktop.png'),fullPage:true});
  const report={passed:true,scope:'Actual local Blazor UI with synthetic persistent gift/avatar API fixtures. No real users, gifts or external services; relational distinct-recipient enforcement is covered by API tests.',checks,browserErrors:errors,failedAssets,drafts:drafts.map(({account,id,recipientSub,theme})=>({account,id,recipientSub,theme})),unlockCalls:calls.filter(call=>call.path.endsWith('/santa/unlock')).length};
  await fs.writeFile(path.join(output,'qa-report.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));await Promise.all([one.close(),another.close(),other.close()]);
})().catch(async error=>{console.error(error);await fs.mkdir(output,{recursive:true});if(page&&!page.isClosed()){console.error((await page.locator('body').innerText()).slice(-4500));await page.screenshot({path:path.join(output,'requirements-qa-failure.png'),fullPage:true}).catch(()=>{});}await fs.writeFile(path.join(output,'qa-report.json'),JSON.stringify({passed:false,checks,browserErrors:errors,failedAssets,error:error.stack},null,2)+'\n');process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();});
