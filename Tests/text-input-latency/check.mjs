// Gemessene Prüfung (Issue 111): Schnelles Tippen in ein Textfeld der echten Web-Seite mit künstlicher Verzögerung.
// Blazor Server spricht über einen WebSocket mit dem Browser; hier wird jede Nachricht des Servers an den Browser um
// DELAY_MS verzögert, so wie bei einer langsamen Leitung. Dann tippt das Skript ein Passwort schnell in das Feld der Anmeldung
// und prüft, dass jedes Zeichen in der richtigen Reihenfolge im Feld steht, auch nachdem alle Antworten angekommen sind.
// Der Server der Web-Seite muss laufen; es wird nichts abgeschickt.
// Keine neue Abhängigkeit im Repository: Playwright kommt von der Maschine.
//
//   NODE_PATH=<Ordner mit node_modules/playwright-core> CHECK_URL=http://localhost:5199 \
//   [CHECK_BROWSER=<chrome-headless-shell.exe>] [DELAY_MS=300] node Tests/text-input-latency/check.mjs
import { createRequire } from 'module';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright-core');
const base = process.env.CHECK_URL || 'http://localhost:5195';
const delay = Number(process.env.DELAY_MS || 300);
const secret = 'Geheim-Passwort_2026!x';
const pasted = 'Eingefuegt-Wert_77#z';

function fail(message) {
  console.error('FAIL: ' + message);
  process.exit(1);
}

const browser = await chromium.launch({ headless: true, executablePath: process.env.CHECK_BROWSER || undefined });
try {
  const page = await browser.newPage();
  let fromServer = 0;
  const toServer = []; // alles, was der Browser an den Server schickt, in der Reihenfolge
  await page.routeWebSocket(/_blazor/, ws => {
    const server = ws.connectToServer();
    // Browser -> Server sofort, Server -> Browser verzögert (in der Reihenfolge der Ankunft).
    server.onMessage(message => { fromServer++; setTimeout(() => ws.send(message), delay); });
    ws.onMessage(message => { toServer.push(Buffer.from(message)); server.send(message); });
  });
  const lastIndexWith = text => toServer.findLastIndex(m => m.includes(Buffer.from(text)));
  await page.goto(base + '/anmelden', { waitUntil: 'load' });
  const field = page.locator('[data-testid=login-password]');
  await field.waitFor();
  await page.waitForTimeout(delay * 4 + 1000); // die Schaltung der Seite abwarten

  await field.click();
  await field.pressSequentially(secret, { delay: Number(process.env.KEY_MS || 25) });
  const during = await field.inputValue();
  await page.waitForTimeout(delay * 6 + 1000); // alle Antworten sind angekommen
  const after = await field.inputValue();

  console.log('Nachrichten vom Server: ' + fromServer);
  console.log(`getippt:   ${secret}
während:   ${during}
nachher:   ${after}`);
  if (fromServer === 0) fail('Die verzögerte Verbindung war gar nicht aktiv (keine Nachricht vom Server gezählt).');
  if (during !== secret) fail('Während der Antworten des Servers wurde das Feld überschrieben.');
  if (after !== secret) fail('Nach den Antworten des Servers steht nicht mehr, was getippt wurde.');
  if (lastIndexWith(secret) < 0) fail('Der Server hat den getippten Wert nie bekommen.');

  // Einfügen (ein einziges input-Ereignis, wie Einfügen oder Autofill) ersetzt den Inhalt; das Feld behält es.
  await field.press('Control+A');
  await page.keyboard.insertText(pasted);
  const pastedDuring = await field.inputValue();
  await page.waitForTimeout(delay * 6 + 1000);
  const pastedAfter = await field.inputValue();
  console.log(`eingefügt: ${pasted}
während:   ${pastedDuring}
nachher:   ${pastedAfter}`);
  if (pastedDuring !== pasted || pastedAfter !== pasted) fail('Der eingefügte Wert steht nicht im Feld.');

  // Enter schickt den letzten Wert ab: Der Server bekommt den Wert vor dem Enter-Tastendruck, also nicht den älteren getippten.
  const before = toServer.length;
  await field.press('Enter');
  await page.waitForTimeout(delay * 2 + 500);
  // Die Ereignisse kommen in der Reihenfolge an: erst das input-Ereignis mit dem eingefügten Wert, dann der Enter-Tastendruck.
  const valueAt = toServer.findIndex(m => m.includes(Buffer.from('"value":"' + pasted + '"')));
  const enterAt = toServer.findIndex((m, i) => i >= before && m.includes(Buffer.from('"key":"Enter"')));
  if (valueAt < 0 || valueAt >= before) fail('Der Server hat den eingefügten Wert nicht vor dem Absenden bekommen.');
  if (enterAt < 0) fail('Der Server hat den Enter-Tastendruck nicht bekommen.');
  if (lastIndexWith('"value":"' + secret + '"') > valueAt) fail('Nach dem eingefügten Wert kam noch der ältere getippte beim Server an.');
  console.log('OK: Jedes Zeichen steht in der richtigen Reihenfolge im Feld; Einfügen gilt; Enter kommt nach dem letzten Wert beim Server an.');
} finally {
  await browser.close();
}
