'use strict';
const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const { chromium } = require(process.env.USERPROFILE + '/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const albumRoot = 'D:/ArcGIS-Pro-MCP 2.0/3.0最终方案/宣传/宣传图集';
const qaRoot = path.join(albumRoot,'制作与核验','画廊浏览核验');
const tempRoot = path.join(qaRoot,'临时');
fs.mkdirSync(tempRoot,{recursive:true});
for (const key of ['TEMP','TMP','TMPDIR','APPDATA','LOCALAPPDATA']) {
  const dir = path.join(tempRoot,key.toLowerCase());
  fs.mkdirSync(dir,{recursive:true});
  process.env[key] = dir;
}
const expectedFiles = ['01_全链路主视觉','02_自然语言任务','03_PS自动成图','04_参考风格学习','05_GP持续适配','06_成果同步更新','07_多业务场景','08_完整成果交付'];
const pngFiles = expectedFiles.map(name => path.join(albumRoot,'01_PNG分享图',name+'.png'));
for (const file of pngFiles) if (!fs.existsSync(file)) throw new Error('Image not yet ready: '+file);
const report = {scope:'Offline gallery browser checks only; not product acceptance',startedAt:new Date().toISOString(),browser:'Microsoft Edge via bundled Playwright',allProjectOutputRoot:albumRoot,checks:{},screenshots:[]};
(async () => {
  const context = await chromium.launchPersistentContext(path.join(qaRoot,'浏览器独立配置'),{
    executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    headless:true,
    viewport:{width:1440,height:1080},
    args:['--disable-background-networking','--disable-component-update','--disable-default-apps','--no-first-run','--disable-breakpad','--no-service-autorun','--disable-gpu','--disk-cache-dir='+path.join(tempRoot,'browser-cache'),'--crash-dumps-dir='+path.join(tempRoot,'crash-dumps')]
  });
  try {
    await context.route('**/*',route => /^(file|data|blob):/.test(route.request().url()) ? route.continue() : route.abort());
    const page = context.pages()[0] || await context.newPage();
    const pageErrors = [];
    page.on('pageerror',error => pageErrors.push(error.message));
    await page.goto(pathToFileURL(path.join(albumRoot,'图集浏览.html')).href);
    await page.evaluate(()=>document.querySelectorAll('.image-button img').forEach(image=>{image.loading='eager';}));
    await page.locator('.tile').last().scrollIntoViewIfNeeded();
    await page.waitForFunction(() => [...document.querySelectorAll('.image-button img')].every(image=>image.complete && image.naturalWidth>0));
    await page.evaluate(async()=>{ await Promise.all([...document.querySelectorAll('.image-button img')].map(image=>image.decode())); await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))); });
    await page.evaluate(()=>window.scrollTo(0,0));
    await page.waitForTimeout(350);
    report.checks.desktop = await page.evaluate(()=>({images:[...document.querySelectorAll('.image-button img')].map(image=>({width:image.naturalWidth,height:image.naturalHeight})),horizontalOverflow:document.documentElement.scrollWidth>window.innerWidth}));
    const desktop = path.join(qaRoot,'01_桌面完整画廊.png');
    await page.screenshot({path:desktop,fullPage:true});
    report.screenshots.push(desktop);
    await page.locator('.image-button').first().click();
    if (!await page.locator('#viewer').evaluate(element=>element.open)) throw new Error('Lightbox did not open');
    await page.waitForFunction(()=>document.getElementById('viewer-image').complete && document.getElementById('viewer-image').naturalWidth>0);
    const lightbox = path.join(qaRoot,'02_大图浏览.png');
    await page.screenshot({path:lightbox});
    report.screenshots.push(lightbox);
    await page.keyboard.press('ArrowRight');
    if ((await page.locator('#viewer-title').textContent()) !== '自然语言任务') throw new Error('Right key navigation failed');
    await page.keyboard.press('ArrowLeft');
    if ((await page.locator('#viewer-title').textContent()) !== '全链路主视觉') throw new Error('Left key navigation failed');
    await page.locator('#next').click();
    if ((await page.locator('#viewer-title').textContent()) !== '自然语言任务') throw new Error('Next button navigation failed');
    await page.locator('#previous').click();
    if ((await page.locator('#viewer-title').textContent()) !== '全链路主视觉') throw new Error('Previous button navigation failed');
    await page.keyboard.press('End');
    if ((await page.locator('#viewer-title').textContent()) !== '完整成果交付') throw new Error('End navigation failed');
    await page.keyboard.press('Home');
    if ((await page.locator('#viewer-title').textContent()) !== '全链路主视觉') throw new Error('Home navigation failed');
    await page.keyboard.press('Escape');
    if (await page.locator('#viewer').evaluate(element=>element.open)) throw new Error('Escape did not close lightbox');
    report.checks.lightbox = {open:true,rightKey:true,leftKey:true,nextButton:true,previousButton:true,endKey:true,homeKey:true,escapeClose:true};
    await page.setViewportSize({width:390,height:844});
    await page.evaluate(async()=>{ document.activeElement.blur(); window.scrollTo(0,0); await Promise.all([...document.querySelectorAll('.image-button img')].map(image=>image.decode())); await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))); });
    await page.waitForTimeout(350);
    const mobile = path.join(qaRoot,'03_手机完整画廊.png');
    await page.screenshot({path:mobile,fullPage:true});
    report.screenshots.push(mobile);
    report.checks.mobile = await page.evaluate(()=>({viewportWidth:window.innerWidth,pageWidth:document.documentElement.scrollWidth,horizontalOverflow:document.documentElement.scrollWidth>window.innerWidth}));
    const links = await page.locator('a[href]').evaluateAll(anchors=>anchors.map(anchor=>anchor.getAttribute('href')));
    report.checks.localLinks = links.filter(link=>link && !link.startsWith('#')).map(link=>({link,exists:fs.existsSync(path.join(albumRoot,link))}));
    report.checks.pageErrors = pageErrors;
    report.status = !report.checks.desktop.horizontalOverflow && !report.checks.mobile.horizontalOverflow && pageErrors.length===0 && report.checks.localLinks.every(link=>link.exists) ? 'PASS' : 'REVIEW_NEEDED';
    report.finishedAt = new Date().toISOString();
    fs.writeFileSync(path.join(qaRoot,'画廊浏览核验.json'),JSON.stringify(report,null,2),'utf8');
    process.stdout.write(JSON.stringify({status:report.status,screenshots:report.screenshots,checks:report.checks},null,2));
  } finally { await context.close(); }
})().catch(error=>{ process.stderr.write(error.stack+'\n'); process.exitCode=1; });
