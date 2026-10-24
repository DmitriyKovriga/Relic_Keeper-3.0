/* Offline review artifact QA; does not launch or modify Unity. */
const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Users/dima4/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[],requests=[];
  page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url()))requests.push(r.url());});
  await page.goto(pathToFileURL(path.join(__dirname,'visual-atmosphere-plan.html')).href);
  assert.equal(await page.locator('.option').count(),20);
  assert.equal(await page.locator('img').evaluateAll(images=>images.filter(x=>x.complete&&x.naturalWidth>0).length),2);
  await page.locator('[data-id="V06"] summary').click();
  assert.match(await page.locator('[data-id="V06"]').innerText(),/HubArt/);
  await page.locator('#filter').selectOption('low');
  const n=await page.evaluate(()=>window.atmospherePlan.data.options.filter(o=>o.maintenance<=2).length);
  assert.equal(await page.locator('.option').count(),n);
  await page.locator('[data-package="base"]').click();assert.equal(await page.locator('.option').count(),7);
  assert.equal(await page.locator('.option.selected').count(),7);
  await page.locator('label[for="pick-V04"]').click();assert.equal(await page.locator('.option').count(),6);
  await page.locator('#filter').selectOption('all');await page.locator('#search').fill('normal maps');
  assert.ok(await page.locator('[data-id="V19"]').isVisible());
  await page.locator('#search').fill('');await page.locator('#reset').click();
  assert.equal(await page.locator('.option.selected').count(),0);
  await page.evaluate(()=>scrollTo(0,0));await page.screenshot({path:path.join(__dirname,'preview.png'),fullPage:false});
  await page.setViewportSize({width:760,height:1000});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
  assert.deepEqual(errors,[]);assert.deepEqual(requests,[]);
  const report={status:'passed',date:'2026-10-24',checks:['20 full options','2 original reference images load','expanded prerequisites','maintenance filter','package selection and manual deselection','search','reset','responsive width','no JavaScript errors or external requests']};
  fs.writeFileSync(path.join(__dirname,'browser-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
