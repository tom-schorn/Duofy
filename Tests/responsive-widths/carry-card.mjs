// Gemessene Prüfung (Spec 123, Review 4): Auf der Übertragskarte stehen Vorname und Betrag in derselben Zeile, unterhalb von
// Datum und Titel – bei jeder Namenslänge (auch "Jo", "Li", "Ida") und mit größerer Schrift, bei 360 und 412 px.
// Zusätzlich: Die Überweisungskarte mit dem Strich in der Postenspalte bricht sauber um und ragt nirgends heraus.
//
// Statische Seite: lädt das echte duofy.css und das Markup aus MonthBookings.razor (Zeilen, Zellen, Klassen), ohne laufende
// App und ohne Anmeldung. Das ist keine Messung in der laufenden App; die Layout-Regeln hängen nur an diesen Klassen.
// Keine neue Abhängigkeit im Repository: Playwright kommt von der Maschine.
//
//   NODE_PATH=<Ordner mit node_modules/playwright-core> [CHECK_BROWSER=<chrome>] node Tests/responsive-widths/carry-card.mjs
import { createRequire } from 'module';
import { fileURLToPath, pathToFileURL } from 'url';
import path from 'path';
import fs from 'fs';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright-core');

const here = path.dirname(fileURLToPath(import.meta.url));
const cssPath = path.resolve(here, '../../Shared/Duofy.Shared/wwwroot/duofy.css');
const shotDir = process.env.CHECK_SHOTS;

const names = ['Jo', 'Li', 'Ida', 'Tom', 'Anna', 'Maximiliane'];
const carryRow = (name, i) => `
  <tr class="bookings-row bookings-row--carry" data-testid="carry-${i}">
    <td class="bookings-date">01.11.2026</td>
    <td class="bookings-name"><span class="bookings-title">Übertrag aus Oktober</span><span class="pill booking-kind" data-kind="CarryOver"><span class="dot" aria-hidden="true"></span>Übertrag</span></td>
    <td class="bookings-position muted">—</td>
    <td class="bookings-account">${name}</td>
    <td class="bookings-pot muted">—</td>
    <td class="bookings-amount num">1.234,56 €</td>
  </tr>`;
const bookingRow = `
  <tr class="bookings-row" data-testid="booking">
    <td class="bookings-date">03.11.2026</td>
    <td class="bookings-name"><span class="bookings-title">Wocheneinkauf</span><span class="pill booking-kind" data-kind="Expense"><span class="dot dot--needs" aria-hidden="true"></span>Ausgabe</span></td>
    <td class="bookings-position">Lebensmittel</td>
    <td class="bookings-account">Tom</td>
    <td class="bookings-pot"><span class="bookings-pot-item"><span class="dot dot--needs" aria-hidden="true"></span>Bedarf</span></td>
    <td class="bookings-amount num amount-expense">−87,40 €</td>
  </tr>`;
const transferRow = `
  <tr class="bookings-row" data-testid="transfer">
    <td class="bookings-date">05.11.2026</td>
    <td class="bookings-name"><span class="bookings-title">Umbuchung aufs Sparkonto</span><span class="pill booking-kind" data-kind="Transfer"><span class="dot dot--wants" aria-hidden="true"></span>Überweisung</span></td>
    <td class="bookings-position"><span class="muted">—</span></td>
    <td class="bookings-account">Ida</td>
    <td class="bookings-pot"></td>
    <td class="bookings-amount num">200,00 €</td>
  </tr>`;

const html = `<!doctype html><html lang="de"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<link rel="stylesheet" href="${pathToFileURL(cssPath)}"></head><body>
<div class="bookings-scroll"><table class="bookings-table"><thead><tr><th>Datum</th><th>Name</th><th>Posten</th><th>Person</th><th>Topf</th><th class="bookings-amount">Betrag</th></tr></thead>
<tbody>${names.map(carryRow).join('')}${bookingRow}${transferRow}</tbody></table></div></body></html>`;

const scratch = fs.mkdtempSync(path.join(process.env.CLAUDE_JOB_DIR ?? process.env.TMPDIR ?? '.', 'carry-card-'));
const file = path.join(scratch, 'card.html');
fs.writeFileSync(file, html);

// Läuft im Browser: pro Zeile die Zeilen-Nummer (nach Oberkante) von Person und Betrag, plus Überstand der Zellen über die Zeile.
const measure = `(() => [...document.querySelectorAll('tr.bookings-row')].map(tr => {
  const q = c => tr.querySelector('.' + c), box = e => e.getBoundingClientRect(), row = box(tr);
  const o = box(q('bookings-account')), a = box(q('bookings-amount')), t = box(q('bookings-name')), d = box(q('bookings-date'));
  const pos = q('bookings-position'), p = box(pos);
  const cells = [...tr.children].filter(c => getComputedStyle(c).display !== 'none').map(c => box(c));
  return {
    id: tr.dataset.testid, owner: q('bookings-account').textContent.trim(),
    sameLine: Math.abs(o.top - a.top) < 4, ownerBelowTitle: o.top >= t.bottom - 1 && o.top >= d.bottom - 1,
    overflow: cells.some(c => c.right > row.right + 0.5 || c.left < row.left - 0.5),
    posVisible: getComputedStyle(pos).display !== 'none', posOnOwnLine: p.height > 0 && Math.abs(p.top - o.top) > 4 && Math.abs(p.top - a.top) > 4,
    rowHeight: Math.round(row.height), ownerTop: Math.round(o.top - row.top),
    wrapFree: tr.scrollWidth <= tr.clientWidth + 1,
  };
}))()`;

const browser = await chromium.launch(process.env.CHECK_BROWSER ? { executablePath: process.env.CHECK_BROWSER } : {});
let failures = 0;
const fail = m => { failures++; console.log('FAIL ' + m); };
// Größere Schrift: das CSS arbeitet in px, deshalb wird die Zellenschrift hier per Stil vergrößert (Systemschrift "groß").
const variants = [['normal', ''], ['large-text', '.bookings-table td, .bookings-table .bookings-date, .bookings-table .bookings-account { font-size: 18px !important; }']];
for (const width of [360, 412]) {
  for (const [label, extra] of variants) {
    const page = await browser.newPage({ viewport: { width, height: 900 }, locale: 'de-DE' });
    await page.goto(pathToFileURL(file).href);
    if (extra) await page.addStyleTag({ content: extra });
    const rows = await page.evaluate(measure);
    for (const r of rows) {
      const tag = `${width}px ${label} ${r.id}${r.id.startsWith('carry') ? ' (' + r.owner + ')' : ''}`;
      if (r.id.startsWith('carry')) {
        if (!r.sameLine) fail(`${tag}: Person und Betrag stehen nicht in einer Zeile`);
        if (!r.ownerBelowTitle) fail(`${tag}: die Person steht nicht unter Datum und Titel`);
        if (r.posVisible) fail(`${tag}: der Strich der Postenspalte ist sichtbar`);
      }
      if (r.overflow || !r.wrapFree) fail(`${tag}: eine Zelle ragt aus der Karte`);
      if (r.id === 'transfer') console.log(`     ${tag}: Posten-Strich eigene Zeile=${r.posOnOwnLine}, Kartenhöhe ${r.rowHeight}px`);
    }
    // Die Übertragskarte darf höchstens so hoch sein wie eine Buchungskarte plus eine Zeile Abstand (kein doppelter Zeilenabstand).
    const b = rows.find(r => r.id === 'booking'), c = rows.filter(r => r.id.startsWith('carry'));
    for (const r of c) if (r.rowHeight > b.rowHeight + 1) fail(`${width}px ${label} carry (${r.owner}): Karte ${r.rowHeight}px höher als Buchungskarte ${b.rowHeight}px`);
    if (shotDir) { fs.mkdirSync(shotDir, { recursive: true }); await page.screenshot({ path: path.join(shotDir, `carry-${width}-${label}.png`), fullPage: true }); }
    await page.close();
  }
}
await browser.close();
fs.rmSync(scratch, { recursive: true, force: true });
console.log(failures === 0 ? 'OK: Übertragskarten brechen bei 360 und 412 px für jede Namenslänge um.' : `FAIL: ${failures} Abweichung(en).`);
process.exit(failures === 0 ? 0 : 1);
