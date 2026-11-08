// Gemessene Prüfung (Spec 109, Review): Fokus rund um das Rechtstext-Fenster in einem echten Browser.
// Geprüft wird die echte Skriptdatei Shared/Duofy.Shared/wwwroot/legal-overlay.js mit dem echten duofy.css auf einer Seite,
// die die Registrierung nachbildet. Warum nicht die echte Web-Seite: Das Fenster gibt es nur in der App (Host.IsApp, Spec 109 R5);
// im Browser öffnet der Link einen neuen Tab, und die Web-Variante kennt keinen Schalter, der sie zur App macht.
// Der Aufbau des Fensters wird gegen LegalTextOverlay.razor abgeglichen, damit die Nachbildung nicht still abweicht.
// Der Rechtstext ist ein ausgedachter Text; es läuft kein Server außer dem kleinen Dateiserver dieses Skripts.
// Keine neue Abhängigkeit im Repository: Playwright kommt von der Maschine.
//
//   NODE_PATH=<Ordner mit node_modules/playwright-core> [CHECK_BROWSER=<chrome.exe>] node Tests/legal-overlay-focus/check.mjs
import { createRequire } from 'module';
import http from 'http';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright-core');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const wwwroot = path.join(root, 'Shared', 'Duofy.Shared', 'wwwroot');

function fail(message) {
  console.error('FAIL: ' + message);
  process.exit(1);
}

// Die Nachbildung muss dieselben Merkmale haben wie das echte Fenster.
const razor = fs.readFileSync(path.join(root, 'Shared', 'Duofy.Shared', 'Layout', 'LegalTextOverlay.razor'), 'utf8');
for (const needle of ['role="dialog"', 'aria-labelledby="legal-overlay-title"', 'tabindex="-1"', 'legal-overlay-backdrop', 'data-testid="legal-overlay"']) {
  if (!razor.includes(needle)) fail(`LegalTextOverlay.razor enthält ${needle} nicht mehr; die Nachbildung in check.mjs anpassen.`);
}

const page = `<!doctype html><html lang="de"><head><meta charset="utf-8"><link rel="stylesheet" href="/duofy.css"></head><body>
<header><a href="#top" id="before">Kopf</a></header>
<main><form id="form"><input id="email" aria-label="E-Mail"><label><input type="checkbox" id="accept"> Ich stimme den
<a href="/datenschutz" id="privacy-link">Datenschutzhinweisen</a> und den <a href="/agb" id="terms-link">AGB</a> zu.</label>
<button type="button" id="submit">Registrieren</button></form></main>
<footer><a href="#end" id="after">Fuß</a></footer>
<div id="host"></div>
<script type="module">
  const module = await import('/legal-overlay.js');
  function show(link, passOpener) {
    const host = document.getElementById('host');
    host.innerHTML = '<div class="legal-overlay-backdrop"><div class="legal-overlay" role="dialog" aria-modal="true" aria-labelledby="legal-overlay-title" tabindex="-1" data-testid="legal-overlay">'
      + '<h2 class="legal-overlay-title" id="legal-overlay-title">Datenschutz</h2><div class="legal-overlay-body"><div class="plain-text">Ausgedachter Rechtstext.</div></div>'
      + '<button type="button" data-testid="legal-overlay-close">Schließen</button></div></div>';
    const panel = host.querySelector('[data-testid=legal-overlay]');
    const close = () => { host.innerHTML = ''; module.close(); };
    panel.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });
    panel.querySelector('button').addEventListener('click', close);
    module.open(panel, passOpener ? link : undefined);
  }
  for (const id of ['privacy-link', 'terms-link']) {
    const link = document.getElementById(id);
    link.addEventListener('click', e => { e.preventDefault(); show(link, true); });
  }
  window.openWithoutOpener = () => show(document.getElementById('privacy-link'), false);
</script></body></html>`;

const files = { '/duofy.css': 'duofy.css', '/legal-overlay.js': 'legal-overlay.js' };
const server = http.createServer((req, res) => {
  if (req.url === '/' || req.url === '/registrieren') {
    res.setHeader('content-type', 'text/html; charset=utf-8');
    res.end(page);
    return;
  }
  const file = files[req.url];
  if (!file) {
    res.statusCode = 404;
    res.end();
    return;
  }
  res.setHeader('content-type', file.endsWith('.js') ? 'text/javascript' : 'text/css');
  res.end(fs.readFileSync(path.join(wwwroot, file)));
});

let browser;
const failures = [];
function check(ok, message) {
  console.log((ok ? 'ok   ' : 'FAIL ') + message);
  if (!ok) failures.push(message);
}

try {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  browser = await chromium.launch({ headless: true, executablePath: process.env.CHECK_BROWSER || undefined });
  const tab = await browser.newPage();
  await tab.goto(base + '/registrieren');
  await tab.waitForFunction(() => typeof window.openWithoutOpener === 'function');

  const insideDialog = () => tab.evaluate(() => !!document.activeElement?.closest('[data-testid=legal-overlay]'));
  const inertCount = () => tab.evaluate(() => document.querySelectorAll('[inert]').length);
  const activeId = () => tab.evaluate(() => document.activeElement?.id || document.activeElement?.tagName);

  const cases = [
    ['Klick mit Fokus', 'privacy-link', () => tab.click('#privacy-link')],
    // Safari fokussiert einen Link beim Klick nicht: ein Skript-Klick bewegt den Fokus ebenfalls nicht.
    ['Klick ohne Fokuswechsel', 'terms-link', () => tab.evaluate(() => { document.activeElement?.blur(); document.getElementById('terms-link').click(); })],
  ];
  for (const [mode, openerId, open] of cases) {
    await open();
    await tab.waitForSelector('[data-testid=legal-overlay]');
    check(await insideDialog(), `${mode}: Fokus liegt nach dem Öffnen im Fenster`);
    check(await inertCount() > 0, `${mode}: die Seite dahinter ist inert`);
    let escaped = false;
    for (let i = 0; i < 12; i++) {
      await tab.keyboard.press(i % 3 === 2 ? 'Shift+Tab' : 'Tab');
      if (!(await insideDialog())) {
        escaped = true;
        console.error(`  Fokus bei Druck ${i + 1} auf ${await activeId()}`);
        break;
      }
    }
    check(!escaped, `${mode}: der Fokus verlässt das Fenster bei wiederholtem Tab nie`);
    await tab.keyboard.press('Escape');
    await tab.waitForSelector('[data-testid=legal-overlay]', { state: 'detached' });
    check(await activeId() === openerId, `${mode}: nach Escape liegt der Fokus auf dem öffnenden Link (ist: ${await activeId()})`);
    check(await inertCount() === 0, `${mode}: nach Escape hat kein Element mehr inert`);
  }

  // Ohne bekannten Öffner gilt das Element mit dem Fokus (Eintrag in der Fußzeile).
  await tab.focus('#after');
  await tab.evaluate(() => window.openWithoutOpener());
  await tab.waitForSelector('[data-testid=legal-overlay]');
  await tab.keyboard.press('Escape');
  check(await activeId() === 'after', `ohne Öffner: der Fokus kehrt zum Element mit dem Fokus beim Öffnen zurück (ist: ${await activeId()})`);
  check(await inertCount() === 0, 'ohne Öffner: nach Escape hat kein Element mehr inert');
} finally {
  if (browser) await browser.close();
  server.close();
}
if (failures.length) {
  console.error(`FAIL: ${failures.length} Prüfung(en) gescheitert.`);
  process.exit(1);
}
console.log('OK');
