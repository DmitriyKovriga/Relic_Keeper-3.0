/* Offline review QA only. No Unity scenes/assets or gameplay are touched. */
const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/dima4/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1100}}),errors=[],remote=[];
  page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url()))remote.push(r.url());});
  await page.goto(pathToFileURL(path.join(__dirname,'combat-movement-plan.html')).href);
  assert.equal(await page.locator('.card').count(),21);assert.equal(await page.locator('#sources li').count(),7);
  for(const id of ['C03','C06','C08'])assert.equal(await page.locator(`[data-id="${id}"]`).count(),0);
  await page.locator('#filter').selectOption('parked');assert.equal(await page.locator('.card').count(),3);
  assert.match(await page.locator('[data-id="C08"]').innerText(),/Отложено пользователем/);
  await page.locator('#filter').selectOption('all');assert.equal(await page.locator('.card').count(),24);
  await page.locator('#filter').selectOption('movement');assert.equal(await page.locator('.card').count(),6);
  await page.locator('#filter').selectOption('balance');assert.equal(await page.locator('.card').count(),4);
  await page.locator('[data-package="core"]').click();assert.equal(await page.locator('.card.selected').count(),8);
  await page.locator('label[for="pick-C04"]').click();assert.equal(await page.locator('.card').count(),7);
  await page.locator('#filter').selectOption('all');await page.locator('#search').fill('C02');
  assert.equal(await page.locator('.card').count(),1);await page.locator('[data-id="C02"] summary').click();
  assert.match(await page.locator('[data-id="C02"]').innerText(),/PlayerRenderAnchor/);
  const heroCollider=await page.locator('#hero-collider').getAttribute('x');
  async function phase(value){await page.locator('#phase-slider').evaluate((e,v)=>{e.value=String(v);e.dispatchEvent(new Event('input',{bubbles:true}));},value);}
  await phase(55);const weaponOn=await page.locator('#hero-weapon').evaluate(e=>{const m=e.getCTM();return [m.a,m.b,m.c,m.d,m.e,m.f];});const bodyOn=await page.locator('#hero-body').getAttribute('transform');
  assert.notEqual(await page.locator('#enemy-body').getAttribute('transform'),'translate(420 190) rotate(0)');
  assert.equal(await page.locator('#contact-mark').getAttribute('opacity')!=='0',true);
  await page.locator('#body-on').uncheck();assert.notEqual(await page.locator('#hero-body').getAttribute('transform'),bodyOn);
  assert.equal(await page.locator('#hero-collider').getAttribute('x'),heroCollider);
  assert.deepEqual(await page.locator('#hero-weapon').evaluate(e=>{const m=e.getCTM();return [m.a,m.b,m.c,m.d,m.e,m.f];}),weaponOn);
  assert.equal(await page.locator('#hero-weapon').evaluate(e=>e.closest('#hero-body')===null),true);
  await page.locator('#hit-on').uncheck();assert.equal(await page.locator('#contact-mark').getAttribute('opacity'),'0');
  assert.equal(await page.locator('#flecks rect').evaluateAll(nodes=>nodes.every(n=>n.getAttribute('opacity')==='0')),true);
  assert.equal(await page.locator('#enemy-body').getAttribute('transform'),'translate(420 190) rotate(0)');
  await page.locator('#hit-on').check();await page.locator('#body-on').check();
  await page.locator('#play-demo').click();await page.waitForFunction(()=>Number(document.getElementById('phase-slider').value)>8);
  await page.locator('#play-demo').click();await phase(53);
  await page.locator('#search').fill('');await page.locator('#filter').selectOption('all');await page.locator('#reset').click();
  assert.equal(await page.locator('.card.selected').count(),0);
  await page.evaluate(()=>scrollTo(0,0));await page.screenshot({path:path.join(__dirname,'preview.png'),fullPage:false});
  await page.locator('.demo').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(__dirname,'preview-demo.png'),fullPage:false});
  await page.setViewportSize({width:760,height:1000});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);
  const report={status:'passed',date:'2026-10-27',checks:['revision 2: 21 active proposals, 3 excluded/deferred/optional, 24 total','4 mechanics-change proposals isolated','7 research statuses','package selection and manual deselection','search and expanded reuse notes','interactive body/recoil/contact diagram','miss suppresses target feedback','body-follow changes neither collider nor independent weapon transform','play and scrub controls','responsive width','no JS errors or external requests'],boundary:'Browser artifact verification only; not Unity runtime/Play Mode validation.'};
  fs.writeFileSync(path.join(__dirname,'browser-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
