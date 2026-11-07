// Gemessene Prüfung (Spec 101, A4): Hauptseiten bei fünf Breiten; scheitert, wenn die Anmeldung nicht gelingt, eine Seite nicht
// auf der erwarteten Route landet, eine Seite breiter als das Fenster ist, ein Betrag (auch durch einen engeren Vorfahren)
// abgeschnitten wird oder die vier Monats-Kennzahlen nicht so stehen wie beschlossen (unter 600 px zwei mal zwei, ab 600 px eine Reihe).
// Keine neue Abhängigkeit im Repository: Playwright kommt von der Maschine.
//
// Linux/macOS/Git Bash:
//   NODE_PATH=<Ordner mit node_modules/playwright-core> \
//   CHECK_BASE_URL=http://127.0.0.1:5090 CHECK_EMAIL=... CHECK_PASSWORD=... [CHECK_BROWSER=<chrome.exe>] \
//   node Tests/responsive-widths/check.mjs
//
// Windows PowerShell:
//   $env:NODE_PATH = 'C:\Pfad\zu\node_modules'
//   $env:CHECK_BASE_URL = 'http://127.0.0.1:5090'; $env:CHECK_EMAIL = '...'; $env:CHECK_PASSWORD = '...'
//   $env:CHECK_BROWSER = 'C:\Program Files\Google\Chrome\Application\chrome.exe'   # optional
//   node Tests/responsive-widths/check.mjs
//
// Absichtlich abgeschnittener Fall: CHECK_DEMO_CLIPPED=1 setzen; die Prüfung muss dann mit "FAIL" und Exit-Code 1 enden.
// Ohne die Variable läuft vorab immer ein Selbsttest, der den Fehlerfall einbaut und erkannt werden muss.
//
// Das Konto muss zu einer laufenden Web-Variante (mit API) gehören; Zugangsdaten nie einchecken.
import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright-core');

const base = process.env.CHECK_BASE_URL ?? 'http://127.0.0.1:5090';
const widths = [360, 412, 600, 840, 1008];
// Route -> erwarteter Pfad nach dem Laden (/buchungen leitet auf den aktuellen Monat um).
const pages = [
  ['/', /^\/$/], ['/plan', /^\/plan$/], ['/plan/2026/11', /^\/plan\/2026\/11$/], ['/buchungen', /^\/plan\/\d{4}\/\d{1,2}$/],
  ['/konten', /^\/konten$/], ['/vertraege', /^\/vertraege$/], ['/haushaltsplan', /^\/haushaltsplan$/], ['/sparen', /^\/sparen$/],
];
const demoClipped = process.env.CHECK_DEMO_CLIPPED === '1';

for (const name of ['CHECK_EMAIL', 'CHECK_PASSWORD']) {
  if (!process.env[name]) {
    console.error(`Umgebungsvariable ${name} fehlt.`);
    process.exit(2);
  }
}

// Läuft im Browser: liefert jeden sichtbaren Betrag, der breiter als sein Vorfahre ist, der den Überstand abschneidet
// (overflow nicht "visible"), oder der über den rechten/linken Fensterrand ragt. Als Quelltext, damit Selbsttest und Prüfung dieselbe Funktion nutzen.
const findClipped = `(() => {
  const root = document.documentElement;
  const clips = v => v !== 'visible';
  return [...document.querySelectorAll('.num, .metric-amount, .bookings-amount, .start-amount, .chart-bar-value')]
    .filter(e => e.offsetParent !== null)
    .filter(e => {
      const r = e.getBoundingClientRect();
      if (e.scrollWidth > e.clientWidth + 1 || r.right > root.clientWidth + 1 || r.left < -1) return true;
      for (let a = e.parentElement; a && a !== root; a = a.parentElement) {
        const s = getComputedStyle(a);
        if (!clips(s.overflowX)) continue;
        const ar = a.getBoundingClientRect();
        if (r.right > ar.right + 1 || r.left < ar.left - 1) return true;
      }
      return false;
    })
    .map(e => e.className + ':"' + e.textContent.trim().slice(0, 24) + '"');
})()`;

// Läuft im Browser: baut einen absichtlich abgeschnittenen Betrag ein (.num in einem schmalen Vorfahren mit overflow: hidden).
const injectClipped = () => {
  const box = document.createElement('div');
  box.style.cssText = 'width:40px;overflow:hidden;position:absolute;top:0;left:0';
  const num = document.createElement('span');
  num.className = 'num';
  num.style.cssText = 'display:inline-block;white-space:nowrap';
  num.textContent = '1.234.567,89 €';
  box.append(num);
  document.body.append(box);
};

const browser = await chromium.launch(process.env.CHECK_BROWSER ? { executablePath: process.env.CHECK_BROWSER } : {});
const context = await browser.newContext({ viewport: { width: 1008, height: 900 }, locale: 'de-DE' });
const page = await context.newPage();

// Selbsttest: Die Erkennung muss den absichtlich abgeschnittenen Betrag finden, sonst taugt die Prüfung nichts.
await page.setContent('<body></body>');
await page.evaluate(injectClipped);
const selfTest = await page.evaluate(findClipped);
if (selfTest.length !== 1) {
  console.error(`Selbsttest fehlgeschlagen: absichtlich abgeschnittener Betrag nicht erkannt (${JSON.stringify(selfTest)}).`);
  await browser.close();
  process.exit(3);
}
console.log(`Selbsttest ok: abgeschnittener Betrag erkannt (${selfTest[0]}).`);

await page.goto(base + '/anmelden');
await page.waitForLoadState('networkidle');
await page.waitForTimeout(1200);
await page.locator('input').nth(0).fill(process.env.CHECK_EMAIL);
await page.locator('input[type=password]').fill(process.env.CHECK_PASSWORD);
await page.keyboard.press('Enter');
try {
  await page.waitForURL(u => !new URL(u).pathname.startsWith('/anmelden'), { timeout: 15000 });
} catch {
  console.error(`Anmeldung fehlgeschlagen: Seite steht weiter auf ${new URL(page.url()).pathname}.`);
  await browser.close();
  process.exit(2);
}
await page.waitForLoadState('networkidle');
console.log(`Angemeldet, Seite: ${new URL(page.url()).pathname}`);

const failures = [];
for (const width of widths) {
  await page.setViewportSize({ width, height: 900 });
  for (const [route, expected] of pages) {
    await page.goto(base + route);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);
    if (demoClipped) await page.evaluate(injectClipped);
    const result = await page.evaluate(clippedSource => {
      const root = document.documentElement;
      const cards = [...document.querySelectorAll('[data-testid="month-metrics"] > *')].map(e => {
        const r = e.getBoundingClientRect();
        return { x: Math.round(r.left), y: Math.round(r.top), w: Math.round(r.width) };
      });
      return { scrollWidth: root.scrollWidth, clientWidth: root.clientWidth, clipped: (0, eval)(clippedSource), cards, url: location.pathname };
    }, findClipped);

    const problems = [];
    if (!expected.test(result.url)) problems.push(`Route ${result.url} statt ${expected}`);
    if (result.scrollWidth > result.clientWidth) problems.push('Seite breiter als Fenster');
    if (result.clipped.length) problems.push(`abgeschnitten: ${result.clipped.join('|')}`);
    if (result.cards.some(c => c.x < 0 || c.x + c.w > result.clientWidth)) problems.push('Karte außerhalb des Fensters');

    // Der Monat (Route /plan/Jahr/Monat) muss genau vier Kennzahlen zeigen: unter 600 px 2x2, ab 600 px eine Reihe.
    if (/^\/plan\/\d{4}\/\d{1,2}$/.test(result.url)) {
      const { cards } = result;
      if (cards.length !== 4) {
        problems.push(`${cards.length} statt 4 Kennzahlen`);
      } else {
        const rows = new Set(cards.map(c => c.y)).size;
        const columns = new Set(cards.map(c => c.x)).size;
        const [wantRows, wantColumns] = width < 600 ? [2, 2] : [1, 4];
        if (rows !== wantRows || columns !== wantColumns) {
          problems.push(`Kennzahlen stehen in ${rows} Reihe(n) x ${columns} Spalte(n), erwartet ${wantRows} x ${wantColumns}`);
        }
      }
    }

    const status = problems.length ? 'FAIL' : 'ok';
    console.log(`${status} ${width} ${route} -> ${result.url} scroll=${result.scrollWidth}/${result.clientWidth}` +
      (result.cards.length ? ` cards=${JSON.stringify(result.cards)}` : '') + (problems.length ? ` ${problems.join('; ')}` : ''));
    if (problems.length) failures.push(`${width} ${route}`);
  }
}
await browser.close();
if (failures.length) {
  console.error(`\n${failures.length} Seite(n) fehlerhaft: ${failures.join(', ')}`);
  process.exit(1);
}
console.log('\nAlle Seiten bei allen Breiten in Ordnung.');
