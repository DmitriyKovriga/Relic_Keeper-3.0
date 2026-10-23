/* Offline functional review: requires the bundled playwright package and installed Chrome. */
const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('C:/Users/dima4/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const {pathToFileURL}=require('url');
(async()=>{
 const data=JSON.parse(fs.readFileSync(path.join(__dirname,'rogue-passive-tree.json'),'utf8'));
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',downloadsPath:path.join(__dirname,'.qa-downloads')});
 const page=await browser.newPage({viewport:{width:1660,height:1100},deviceScaleFactor:1});
 const errors=[],remote=[];page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url()))remote.push(r.url());});
 const checks=[];
 try{
  await page.goto(pathToFileURL(path.join(__dirname,'rogue-passive-tree.html')).href);
  assert.equal(await page.locator('.node').count(),130);assert.equal(await page.locator('.edge').count(),150);
  assert.equal(await page.locator('.edge.patch').count(),2);assert.equal(await page.locator('#rows tr').count(),130);
  checks.push('130 nodes, 150 edges, 2 proposed connections, all table rows');
  assert.equal(await page.locator('#map title').count(),0);
  assert.equal(await page.locator('.notable-ring').count(),25);
  for(const n of data.nodes){
   const body=page.locator(`[data-node="${n.id}"] .body`);
   if(n.nodeType==='Small')assert.equal(await body.getAttribute('r'),'11');
   if(n.nodeType==='Notable')assert.equal(await body.getAttribute('r'),'22');
  }
  for(const id of ['n60','n63','n67','n87','n129','n150'])assert.equal(data.nodes.find(n=>n.id===id).nodeType,'Notable');
  assert.notEqual(data.nodes.find(n=>n.id==='n173').visualStyle.color,data.nodes.find(n=>n.id==='n38').visualStyle.color);
  assert.notEqual(data.nodes.find(n=>n.id==='n173').visualStyle.symbol,data.nodes.find(n=>n.id==='n38').visualStyle.symbol);
  checks.push('All 130 sizes match source types, 25 double-ring notables, six promoted nodes, distinct defense symbols/colors, no native SVG tooltip');
  assert.equal(data.designRevision,4);
  const travel=data.nodes.filter(n=>n.designRole==='transport');
  assert.equal(travel.length,24);assert.equal(await page.locator('.node.transport').count(),24);
  for(const n of travel){
   assert.equal(n.nodeType,'Small');assert.equal(n.templateMode,'reuse-existing');
   assert.equal(n.templatePath,data.transportTemplate.path);assert.equal(n.modifiers.length,2);
   assert.deepEqual(n.modifiers.map(m=>[m.Stat,m.Value,m.Type]),[['MaxHealth',3,'Flat'],['Evasion',5,'Flat']]);
   assert.deepEqual(n.defenseFamilies,[]);assert.deepEqual(n.statScalingRules,[]);
  }
  await page.locator('[data-node="n14"] .body').hover();
  const travelTip=await page.locator('#tooltip').innerText();assert.match(travelTip,/Транспорт/);
  for(const effect of travel[0].effectsRu)assert.ok(travelTip.includes(effect));
  assert.doesNotMatch(travelTip,/скорость атаки|критического|урон/);
  await page.locator('[data-node="n14"] .body').click();assert.match(await page.locator('#detail').innerText(),/transportRogue/);
  await page.locator('#filter').selectOption('transport');
  assert.equal(await page.locator('#rows tr').count(),24);assert.equal(await page.locator('.node:not(.dim)').count(),24);
  await page.locator('#show-ids').check();await page.screenshot({path:path.join(__dirname,'preview-transport.png'),fullPage:true});
  await page.locator('#show-ids').uncheck();await page.locator('#filter').selectOption('all');
  checks.push('24 transport roads: exactly +3 HP Flat and +5 Evasion Flat; shared template, distinct rendering, hover, role and filter; no specialized bonuses');
  await page.locator('[data-node="n168"]').hover();
  assert.match(await page.locator('#tooltip').innerText(),/Обожжённая сталь/);
  assert.match(await page.locator('#tooltip').innerText(),/40 п.п./);checks.push('Hover displays the actual node and effects');
  await page.locator('[data-node="n3"]').click();
  assert.match(await page.locator('#detail').innerText(),/1000/);assert.match(await page.locator('#detail').innerText(),/CritMultiplier/);
  checks.push('Click pins scaling rule, coordinates and implementation fields');
  assert.equal(data.validation.newSmallTemplateCount,10);assert.equal(data.templateRegistry.length,43);
  for(const group of data.designGroups){
   const members=group.smallIds.map(id=>data.nodes.find(n=>n.id===id));
   assert.equal(new Set(members.map(n=>JSON.stringify(n.modifiers))).size,1);
   assert.equal(new Set(members.map(n=>n.templatePath)).size,1);
  }
  await page.locator('[data-node="n38"] .body').click();
  assert.match(await page.locator('#detail').innerText(),/Единый подход кластера/);
  assert.match(await page.locator('#detail').innerText(),/n33, n34, n35, n73/);
  checks.push('21 homogeneous feeder groups cover all 72 specialized smalls; 10 shared small templates; group explanation shown');
  for(const a of data.archetypes){
   await page.locator(`#archetypes [data-id="${a.id}"]`).click();
   assert.equal(await page.locator('.edge.core').count(),a.corePath.length-1);
   assert.equal(await page.locator('#options .option').count(),a.defenseOptions.length);
   assert.equal(await page.locator('#remote-goals .option').count(),data.portfolioAudit.find(p=>p.family===a.id).goals.length);
   assert.ok((await page.locator('#detail').innerText()).includes(`+${a.coreTransportHealth} HP / +${a.coreTransportEvasion}`));
   for(let i=0;i<a.defenseOptions.length;i++){
    for(const id of a.defenseOptions[i].targets)assert.equal(data.nodes.find(n=>n.id===id).designRole,'specialized');
    await page.locator('#options .option').nth(i).click();
    assert.equal(await page.locator('.edge.defpath').count(),a.defenseOptions[i].additionalCost);
   }
   const portfolio=data.portfolioAudit.find(p=>p.family===a.id);
   await page.locator('#portfolio-safe').click();
   assert.equal(await page.locator('.node.portfolio').count(),portfolio.withoutKeystones.minimumPoints+1);
   assert.equal(await page.locator('.edge.core').count(),portfolio.withoutKeystones.minimumPoints);
   if(portfolio.withKeystones.minimumPoints<portfolio.withoutKeystones.minimumPoints){
    await page.locator('#portfolio-outer').click();
    assert.equal(await page.locator('.node.portfolio').count(),portfolio.withKeystones.minimumPoints+1);
    assert.equal(await page.locator('.edge.core').count(),portfolio.withKeystones.minimumPoints);
   }
  }
  checks.push('All six archetypes, defense detours, remote goal lists, exact shared allocation plans and keystone shortcuts highlight correctly');
  await page.screenshot({path:path.join(__dirname,'preview-route.png'),fullPage:true});
  await page.locator('#search').fill('n73');assert.equal(await page.locator('#rows tr').count(),1);
  assert.equal(await page.locator('.node:not(.dim)').count(),1);
  await page.locator('#search-results button').click();assert.match(await page.locator('#detail').innerText(),/Уклонение/);
  checks.push('ID search finds and focuses the formerly isolated n73');
  await page.locator('#search').fill('');await page.locator('#filter').selectOption('defense');
  assert.equal(await page.locator('#rows tr').count(),data.nodes.filter(n=>n.family==='defense').length);
  await page.locator('#tab-table').click();assert.equal(await page.locator('#table-panel').isVisible(),true);
  await page.locator('#rows tr[data-id="n75"]').click();assert.match(await page.locator('#detail').innerText(),/Право на ошибку/);
  checks.push('Family filter and complete node table support pinned node inspection');
  await page.locator('#filter').selectOption('all');await page.locator('#tab-map').click();await page.locator('#fit').click();
  const originalBox=await page.locator('#map').getAttribute('viewBox');await page.locator('#zoom-in').click();
  assert.notEqual(await page.locator('#map').getAttribute('viewBox'),originalBox);
  await page.locator('#fit').click();assert.equal(await page.locator('#map').getAttribute('viewBox'),originalBox);
  await page.locator('#show-ids').check();assert.equal(await page.locator('#map').evaluate(e=>e.classList.contains('ids')),true);
  await page.locator('#show-ids').uncheck();checks.push('Zoom/reset and ID display controls');
  const downloaded=page.waitForEvent('download');await page.locator('#download').click();const download=await downloaded;
  assert.equal(download.suggestedFilename(),'rogue-passive-tree.json');
  const payload=JSON.parse(fs.readFileSync(await download.path(),'utf8'));assert.equal(payload.nodes.length,130);
  assert.equal(payload.sourceSha256,data.sourceSha256);checks.push('JSON export retains all 130 specifications and source fingerprint');
  await download.delete();
  await page.locator('#clear').click();await page.screenshot({path:path.join(__dirname,'preview.png'),fullPage:true});
  await page.setViewportSize({width:760,height:1000});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
  await page.screenshot({path:path.join(__dirname,'preview-mobile.png'),fullPage:true});checks.push('760px responsive layout has no horizontal page overflow');
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);checks.push('No JavaScript errors or external network requests');
  const report={status:'passed',designRevision:data.designRevision,date:'2026-10-23',engine:'Installed Chrome through Playwright; offline file URL',checks,errors,externalRequests:remote};
  fs.writeFileSync(path.join(__dirname,'browser-validation.json'),JSON.stringify(report,null,2)+'\n');
  console.log(JSON.stringify(report));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
