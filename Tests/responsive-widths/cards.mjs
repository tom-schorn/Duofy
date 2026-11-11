// Gemessene Prüfung der Kartenansicht (Spec 123, R1; Review 4 und 5): Jede Karte (Buchung mit kurzem Posten, Überweisung, ungeplante
// Buchung, Übertrag), im persönlichen Plan und im Haushaltsplan, hat Datum und Titel in Zeile 1 und Person, Posten, Topf und Betrag in
// Zeile 2 – bei jeder Textbreite, bei 360 und 412 px, in normaler und großer Schrift. Die Überweisungskarte zeigt den leeren Strich der
// Postenspalte nicht, eine ungeplante Buchung behält "Ungeplant", der Betrag einer Karte ohne Person rückt nicht ein; die Tabellenansicht
// (1100 px) behält den Strich und bleibt eine Tabelle.
//
// Das Markup stammt aus den echten Komponenten: PlanBookingsTableTests.A1_the_real_table_markup_is_exported_for_the_browser_check
// rendert MonthBookings (mit BookingKindBadge) per bUnit und schreibt personal.html und household.html, hier wird es mit dem echten
// duofy.css gemessen. So kann die Prüfung nicht von der App abweichen (data-kind, Klassen, Zellen). Keine Messung in der laufenden App.
// Keine neue Abhängigkeit im Repository: Playwright kommt von der Maschine.
//
//   DUOFY_CARD_MARKUP_DIR=<Ordner> dotnet test Tests/Duofy.Shared.Tests --filter A1_the_real_table_markup
//   NODE_PATH=<Ordner mit node_modules/playwright-core> [CHECK_BROWSER=<chrome>] [CHECK_SHOTS=<Ordner>] \
//     node Tests/responsive-widths/cards.mjs <markup-Ordner>
import { createRequire } from 'module';
import { fileURLToPath, pathToFileURL } from 'url';
import path from 'path';
import fs from 'fs';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright-core');

const here = path.dirname(fileURLToPath(import.meta.url));
const cssPath = path.resolve(here, '../../Shared/Duofy.Shared/wwwroot/duofy.css');
const markupDir = process.argv[2] ?? process.env.DUOFY_CARD_MARKUP_DIR;
const shotDir = process.env.CHECK_SHOTS;
if (!markupDir) { console.log('FAIL: Ordner mit personal.html und household.html fehlt (Argument oder DUOFY_CARD_MARKUP_DIR).'); process.exit(2); }

const scratch = fs.mkdtempSync(path.join(process.env.CLAUDE_JOB_DIR ?? process.env.TMPDIR ?? '.', 'cards-'));
const files = {};
for (const plan of ['personal', 'household']) {
  const table = fs.readFileSync(path.join(markupDir, `${plan}.html`), 'utf8');
  files[plan] = path.join(scratch, `${plan}.html`);
  fs.writeFileSync(files[plan], `<!doctype html><html lang="de"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<link rel="stylesheet" href="${pathToFileURL(cssPath)}"></head><body><div class="bookings-scroll">${table}</div></body></html>`);
}

// Läuft im Browser. Jede Zeile wird an ihrem Titel erkannt.
const measure = `(() => [...document.querySelectorAll('tr.bookings-row')].map(tr => {
  const q = c => tr.querySelector('.' + c), box = e => e.getBoundingClientRect(), row = box(tr);
  const shown = e => getComputedStyle(e).display !== 'none';
  const t = box(q('bookings-name')), d = box(q('bookings-date')), a = box(q('bookings-amount')), pos = q('bookings-position');
  const line2 = ['bookings-account', 'bookings-position', 'bookings-pot'].map(q).filter(shown).filter(e => box(e).height > 0);
  const cells = [...tr.children].filter(shown).map(box);
  return {
    id: q('bookings-title').textContent.trim(), kind: q('booking-kind').dataset.kind,
    dateTitleLine1: Math.abs(d.top - t.top) < 4,
    line2BelowLine1: a.top >= t.bottom - 1 && a.top >= d.bottom - 1 && line2.every(e => box(e).top >= t.bottom - 1 && box(e).top >= d.bottom - 1),
    // Passt der Inhalt der zweiten Zeile (mit Abständen) in die Karte, muss er auch in einer Zeile stehen; sonst bricht er wie jeder Text um.
    line2Fits: [...line2, q('bookings-amount')].reduce((w, e) => w + box(e).width + 8, -8) <= row.width - 28,
    line2Together: line2.every(e => Math.abs(box(e).top - a.top) < 4),
    amountLeft: Math.round(a.left - row.left), line2Count: line2.length,
    overflow: cells.some(c => c.right > row.right + 0.5 || c.left < row.left - 0.5), wrapFree: tr.scrollWidth <= tr.clientWidth + 1,
    emptyShown: ['bookings-account', 'bookings-pot'].map(q).filter(e => shown(e) && e.textContent.trim() === '').length,
    posVisible: shown(pos), rowHeight: Math.round(row.height), isTable: getComputedStyle(tr).display === 'table-row',
  };
}))()`;

const browser = await chromium.launch(process.env.CHECK_BROWSER ? { executablePath: process.env.CHECK_BROWSER } : {});
let failures = 0, checks = 0;
const fail = m => { failures++; console.log('FAIL ' + m); };
const variants = [['normal', ''], ['large-text', '.bookings-table td, .bookings-table .bookings-date, .bookings-table .bookings-account { font-size: 18px !important; }']];
for (const plan of ['personal', 'household']) {
  for (const width of [360, 412]) {
    for (const [label, extra] of variants) {
      const page = await browser.newPage({ viewport: { width, height: 900 }, locale: 'de-DE' });
      await page.goto(pathToFileURL(files[plan]).href);
      if (extra) await page.addStyleTag({ content: extra });
      const rows = await page.evaluate(measure);
      if (rows.length !== 4) fail(`${plan} ${width}px ${label}: ${rows.length} statt 4 Karten`);
      for (const r of rows) {
        const tag = `${plan} ${width}px ${label} "${r.id}" (data-kind=${r.kind})`;
        checks++;
        if (!r.dateTitleLine1) fail(`${tag}: Datum und Titel stehen nicht in einer Zeile`);
        if (!r.line2BelowLine1) fail(`${tag}: Person/Posten/Topf/Betrag stehen nicht unter Datum und Titel`);
        if (r.line2Fits && !r.line2Together) fail(`${tag}: Person/Posten/Topf stehen nicht in der Zeile des Betrags`);
        if (r.overflow || !r.wrapFree) fail(`${tag}: eine Zelle ragt aus der Karte`);
        if (r.emptyShown) fail(`${tag}: eine leere Personen- oder Topfzelle ist in der Kartenansicht sichtbar und kostet einen Abstand`);
        if (r.kind === 'transfer' && r.posVisible) fail(`${tag}: der leere Strich der Überweisung ist in der Kartenansicht sichtbar`);
        if (r.id === 'Kiosk' && !r.posVisible) fail(`${tag}: "Ungeplant" fehlt`);
        if (r.line2Count === 0 && r.amountLeft > 15) fail(`${tag}: der Betrag ohne Person ist um ${r.amountLeft - 14}px eingerückt`);
      }
      if (shotDir) { fs.mkdirSync(shotDir, { recursive: true }); await page.screenshot({ path: path.join(shotDir, `cards-${plan}-${width}-${label}.png`), fullPage: true }); }
      await page.close();
    }
  }
  // Tabellenansicht (Bereich ab 720 px): bleibt eine Tabelle, der Strich der Überweisung bleibt.
  const page = await browser.newPage({ viewport: { width: 1100, height: 900 }, locale: 'de-DE' });
  await page.goto(pathToFileURL(files[plan]).href);
  for (const r of await page.evaluate(measure)) {
    checks++;
    if (!r.isTable) fail(`${plan} 1100px "${r.id}": keine Tabellenzeile mehr`);
    if (!r.posVisible) fail(`${plan} 1100px "${r.id}": die Postenspalte fehlt in der Tabellenansicht`);
  }
  if (shotDir) await page.screenshot({ path: path.join(shotDir, `cards-${plan}-1100.png`), fullPage: true });
  await page.close();
}
await browser.close();
fs.rmSync(scratch, { recursive: true, force: true });
console.log(failures === 0 ? `OK: ${checks} Karten und Zeilen geprüft; alle Karten haben Datum und Titel in Zeile 1, den Rest in Zeile 2, die Tabelle bleibt unverändert.` : `FAIL: ${failures} Abweichung(en).`);
process.exit(failures === 0 ? 0 : 1);
