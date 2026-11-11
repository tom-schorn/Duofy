// Gemessene Prüfung (Spec 124, A4 und R2): Der Buchungsdialog in der laufenden App bei 360 und 800 px Breite, hell und dunkel.
// Sie scheitert, wenn
//  - der Dialog (oder sein Inhalt) höher oder breiter als das Fenster ist,
//  - ein Knopf im Fuß oder die Knöpfe der Art (Ausgabe, Einnahme, Umbuchung) nicht ganz im Fenster stehen,
//  - der Inhalt eines Knopfs (Text oder Häkchen) breiter als der Knopf ist, links wie rechts (das fängt "Umbuchung" mit Häkchen),
//  - die drei Zeilen im Schritt Aufteilen nicht in einem rollenden Inhalt stehen, der im Fenster bleibt,
//  - eine neue Schnellausgabe (Betrag 0,01 €) mit Konto, Topf und Kategorie aus Schritt 2 nicht mit dem frühen "Speichern" gespeichert wird
//    (nur bei 360 px; legt je Lauf eine Buchung an, CHECK_NO_SAVE=1 lässt das aus).
// Sie nutzt das echte Markup der laufenden App, kein nachgebautes. Keine neue Abhängigkeit: Playwright kommt von der Maschine.
//
// Voraussetzung: Backend, Web und Datenbank laufen; das Konto hat mindestens ein aktives Standardkonto (der Dialog kommt sonst
// nicht über Schritt 2). Zugangsdaten nie einchecken.
//
//   NODE_PATH=<Ordner mit node_modules/playwright-core> \
//   CHECK_BASE_URL=http://127.0.0.1:5491 CHECK_EMAIL=... CHECK_PASSWORD=... [CHECK_BROWSER=<Pfad zur Chromium-Datei>] \
//   /opt/node-22/bin/node Tests/booking-dialog-viewport/check.mjs
//
// Selbsttest: Vorab muss die Messung einen absichtlich zu breiten Knopfinhalt finden (sonst Exit-Code 3).
// Absichtlicher Fehlerfall: CHECK_DEMO_CLIPPED=1 macht die Art-Knöpfe im Dialog zu schmal; die Prüfung muss mit "FAIL" und Exit-Code 1 enden.
import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright-core');

const base = process.env.CHECK_BASE_URL ?? 'http://127.0.0.1:5491';
const demoClipped = process.env.CHECK_DEMO_CLIPPED === '1';
const noSave = process.env.CHECK_NO_SAVE === '1';
// Breite und eine übliche Höhe dazu: Handy hochkant und Tablet hochkant.
const viewports = [{ width: 360, height: 640 }, { width: 800, height: 700 }];
const schemes = ['light', 'dark'];

for (const name of ['CHECK_EMAIL', 'CHECK_PASSWORD']) {
  if (!process.env[name]) {
    console.error(`Umgebungsvariable ${name} fehlt.`);
    process.exit(2);
  }
}

// Läuft im Browser: liefert jeden Knopf in `root`, dessen Inhalt über den Knopf hinausragt. Der Inhalt ist der Text (Range über die
// Textknoten) und das Häkchen (::before; seine Breite und sein Abstand kommen aus dem berechneten Stil). scrollWidth allein reicht nicht:
// Ragt der Inhalt eines zentrierten Knopfs über, ragt er auch nach links, und das zählt scrollWidth nicht.
const findOverflowingButtons = `((rootSelector) => {
  const root = document.querySelector(rootSelector) ?? document.body;
  const problems = [];
  for (const b of root.querySelectorAll('button, a.duofy-button')) {
    if (b.offsetParent === null) continue;
    const box = b.getBoundingClientRect();
    const style = getComputedStyle(b);
    const innerLeft = box.left + parseFloat(style.borderLeftWidth);
    const innerRight = box.right - parseFloat(style.borderRightWidth);
    let left = Infinity, right = -Infinity;
    const range = document.createRange();
    range.selectNodeContents(b);
    for (const r of range.getClientRects()) {
      if (r.width === 0) continue;
      left = Math.min(left, r.left);
      right = Math.max(right, r.right);
    }
    const mark = getComputedStyle(b, '::before');
    if (mark.content !== 'none' && mark.content !== 'normal') {
      const width = parseFloat(mark.width) + parseFloat(mark.marginRight) + parseFloat(mark.marginLeft);
      if (Number.isFinite(width)) left = Math.min(left, left - width);
    }
    const text = b.textContent.trim().replace(/\\s+/g, ' ');
    if (left < innerLeft - 1 || right > innerRight + 1 || b.scrollWidth > b.clientWidth + 1) {
      problems.push('Inhalt "' + text + '" ragt aus dem Knopf (Knopf ' + Math.round(box.left) + '-' + Math.round(box.right) +
        ', Inhalt ' + Math.round(left) + '-' + Math.round(right) + ')');
    }
  }
  return problems;
})`;

// Läuft im Browser: Dialog, Inhalt und Fuß liegen im Fenster; die Knöpfe des Fußes sind sichtbar und liegen ganz darin.
const measureDialog = `(() => {
  const vw = document.documentElement.clientWidth, vh = window.innerHeight;
  const problems = [];
  const inside = (name, el) => {
    if (!el) { problems.push(name + ' fehlt'); return; }
    const r = el.getBoundingClientRect();
    if (r.top < -1 || r.left < -1 || r.bottom > vh + 1 || r.right > vw + 1) {
      problems.push(name + ' liegt nicht im Fenster (' + Math.round(r.left) + ',' + Math.round(r.top) + ' bis ' + Math.round(r.right) + ',' + Math.round(r.bottom) + ' bei ' + vw + 'x' + vh + ')');
    }
  };
  const content = document.querySelector('.step-dialog');
  inside('Dialoginhalt', content);
  inside('Dialogfenster', content?.closest('[role="dialog"]'));
  inside('Fuß', document.querySelector('.step-dialog-foot'));
  const buttons = [...document.querySelectorAll('.step-dialog-foot button')].filter(b => b.offsetParent !== null);
  if (buttons.length === 0) problems.push('keine Knöpfe im Fuß sichtbar');
  buttons.forEach(b => inside('Fußknopf "' + b.textContent.trim() + '"', b));
  const scroll = document.querySelector('.step-dialog-scroll');
  return { problems, foot: buttons.map(b => b.textContent.trim()), scrolls: scroll ? scroll.scrollHeight > scroll.clientHeight + 1 : false,
    contentHeight: content ? Math.round(content.getBoundingClientRect().height) : 0, vh, vw };
})()`;

// Läuft im Browser: baut einen zu breiten Knopfinhalt ein (Text in einem schmalen Knopf ohne Umbruch) für den Selbsttest.
const injectOverflow = () => {
  const box = document.createElement('div');
  box.id = 'selftest';
  const b = document.createElement('button');
  b.style.cssText = 'display:inline-flex;justify-content:center;width:40px;white-space:nowrap;position:absolute;top:0;left:100px';
  b.textContent = 'Umbuchung';
  box.append(b);
  document.body.append(box);
};

// Wählt in Schritt 2 die erste Kategorie der Liste; der Topf folgt ihr (der Dialog hat keine Wahl "Keine Kategorie").
const chooseCategory = async (page) => {
  await page.locator('[data-testid="booking-line-category-0-input"]').click();
  await page.locator('[data-testid="booking-line-category-0"] [role="option"]').first().click();
  await page.waitForTimeout(200);
};

const browser = await chromium.launch(process.env.CHECK_BROWSER ? { executablePath: process.env.CHECK_BROWSER } : {});
const failures = [];
const note = (label, problems) => {
  console.log(`${problems.length ? 'FAIL' : 'ok'} ${label}${problems.length ? ' ' + problems.join('; ') : ''}`);
  if (problems.length) failures.push(label);
};

// Selbsttest: Die Messung muss den zu breiten Knopfinhalt finden, sonst taugt die Prüfung nichts.
{
  const page = await browser.newPage();
  await page.setContent('<body></body>');
  await page.evaluate(injectOverflow);
  const found = await page.evaluate(`${findOverflowingButtons}('#selftest')`);
  await page.close();
  if (found.length !== 1) {
    console.error(`Selbsttest fehlgeschlagen: zu breiter Knopfinhalt nicht erkannt (${JSON.stringify(found)}).`);
    await browser.close();
    process.exit(3);
  }
  console.log(`Selbsttest ok: ${found[0]}`);
}

for (const scheme of schemes) {
  for (const viewport of viewports) {
    const label = `${viewport.width}x${viewport.height} ${scheme}`;
    const context = await browser.newContext({ viewport, locale: 'de-DE', colorScheme: scheme });
    const page = await context.newPage();

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

    // Der Dialog zum Anlegen: auf der Monatsseite (/buchungen leitet auf den aktuellen Monat).
    await page.goto(base + '/buchungen');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(800);
    await page.locator('[data-testid="month-action-booking"]').click();
    await page.locator('[data-testid="step-dialog"]').waitFor({ timeout: 10000 });
    await page.waitForTimeout(500);
    if (demoClipped) {
      await page.addStyleTag({ content: '.booking-kinds button { padding-inline: 30px !important; white-space: nowrap !important; }' });
    }

    const check = async (what, expectSteps) => {
      const m = await page.evaluate(measureDialog);
      // Die Art-Knöpfe sollen ihr Wort ganz in einer Zeile zeigen, nicht mitten im Wort umbrechen.
      const broken = await page.evaluate(() => [...document.querySelectorAll('.booking-kinds button')].filter(b => {
        const r = document.createRange();
        r.selectNodeContents(b);
        return new Set([...r.getClientRects()].filter(x => x.width > 0).map(x => Math.round(x.top))).size > 1;
      }).map(b => b.textContent.trim()));
      m.problems.push(...broken.map(t => `Art-Knopf "${t}" bricht um`));
      const overflow = await page.evaluate(`${findOverflowingButtons}('[data-testid="step-dialog"]')`);
      const problems = [...m.problems, ...overflow];
      if (expectSteps && !(await page.locator('[data-testid="step-dialog-progress"]').textContent())?.includes(expectSteps)) {
        problems.push(`Fortschritt ist nicht "${expectSteps}"`);
      }
      note(`${label} ${what} (Dialog ${m.contentHeight}px von ${m.vh}px, Fuß: ${m.foot.join(' | ')}, Inhalt rollt: ${m.scrolls})`, problems);
      return m;
    };

    // Schritt 1: jede Art gewählt, mit Häkchen, in beiden Themes.
    for (const kind of ['expense', 'income', 'transfer']) {
      await page.locator(`[data-testid="booking-kind-${kind}"]`).click();
      await page.waitForTimeout(150);
      const pressed = await page.locator(`[data-testid="booking-kind-${kind}"]`).getAttribute('aria-pressed');
      if (pressed !== 'true') note(`${label} Art ${kind}`, ['gewählte Art trägt aria-pressed nicht']);
      await check(`Schritt 1, Art ${kind}`, 'Schritt 1');
    }
    await page.locator('[data-testid="booking-kind-expense"]').click();

    // Schritt 2 "Konto und Kategorie": Konto, Topf und Kategorie stehen zusammen; erst mit ihnen erscheint der frühe Speichern-Knopf.
    await page.locator('[data-testid="booking-amount"]').fill('90');
    await page.keyboard.press('Tab');
    await page.locator('[data-testid="booking-next"]').click();
    await page.waitForTimeout(300);
    const earlyBefore = await page.locator('[data-testid="booking-save-early"]').count();
    if (earlyBefore !== 0) note(`${label} früher Speichern-Knopf`, [`${earlyBefore} statt 0 vor der Wahl von Kategorie und Topf`]);
    await chooseCategory(page);
    const early = await page.locator('[data-testid="booking-save-early"]').count();
    await check('Schritt 2 mit frühem Speichern', 'Schritt 2');
    if (early !== 1) note(`${label} früher Speichern-Knopf`, [`${early} statt 1 in Schritt 2 mit Konto, Topf und Kategorie`]);
    // Die offene Liste der Kategorien liegt im Fluss des rollenden Inhalts; der Dialog bleibt trotzdem im Fenster.
    await page.locator('[data-testid="booking-line-category-0-input"]').click();
    await page.waitForTimeout(200);
    await check('Schritt 2, Kategorie-Liste offen', 'Schritt 2');
    await page.keyboard.press('Escape');

    // Schritt 3 "Posten": aufteilen; Schritt 4 zeigt drei Zeilen.
    await page.locator('[data-testid="booking-next"]').click();
    await page.waitForTimeout(300);
    await page.locator('[data-testid="booking-split"]').click();
    await check('Schritt 3, aufgeteilt', 'Schritt 3');
    await page.locator('[data-testid="booking-next"]').click();
    await page.waitForTimeout(300);
    await page.locator('[data-testid="booking-line-add"]').click();
    await page.waitForTimeout(300);
    const lines = await page.locator('[data-testid^="booking-line-"][class~="booking-line"]').count();
    const m = await check('Schritt 4, drei Zeilen', 'Aufteilen');
    if (lines !== 3) note(`${label} Zeilen`, [`${lines} statt 3 Zeilen im Schritt Aufteilen`]);
    if (!m.scrolls && viewport.height <= 700 && viewport.width < 600) {
      // Auf dem Handy müssen drei Zeilen den Inhalt zum Rollen bringen; sonst wäre die Messung nicht aussagekräftig.
      note(`${label} Rollen`, ['drei Zeilen rollen nicht im Inhalt (zu wenig Inhalt für die Messung?)']);
    }

    // Eine neue Schnellausgabe, im Schritt 2 mit dem frühen Speichern wirklich gespeichert (Spec 124, A6), nur bei 360 px.
    // Sie legt je Lauf eine Buchung über 0,01 € an; wer das nicht will, setzt CHECK_NO_SAVE=1.
    if (viewport.width === 360 && !noSave) {
      await page.goto(base + '/buchungen');
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(800);
      await page.locator('[data-testid="month-action-booking"]').click();
      await page.locator('[data-testid="step-dialog"]').waitFor({ timeout: 10000 });
      await page.waitForTimeout(500);
      await page.locator('[data-testid="booking-amount"]').fill('0,01');
      await page.keyboard.press('Tab');
      await page.locator('[data-testid="booking-next"]').click();
      await page.waitForTimeout(300);
      await chooseCategory(page);
      const problems = [];
      if (await page.locator('[data-testid="booking-save-early"]').count() !== 1) problems.push('kein früher Speichern-Knopf in Schritt 2');
      else {
        await page.locator('[data-testid="booking-save-early"]').click();
        try {
          await page.locator('[data-testid="step-dialog"]').waitFor({ state: 'detached', timeout: 10000 });
        } catch {
          const text = await page.locator('[data-testid="booking-error"]').first().textContent().catch(() => null);
          problems.push(`Dialog blieb offen nach dem frühen Speichern${text ? ` (Fehler: ${text.trim()})` : ''}`);
        }
      }
      note(`${label} neue Schnellausgabe mit frühem Speichern`, problems);
    }

    await context.close();
  }
}
await browser.close();
if (failures.length) {
  console.error(`\n${failures.length} Messung(en) fehlerhaft:\n${failures.join('\n')}`);
  process.exit(1);
}
console.log('\nDer Buchungsdialog bleibt bei allen Breiten und Themes im Fenster, alle Knopfinhalte passen.');
