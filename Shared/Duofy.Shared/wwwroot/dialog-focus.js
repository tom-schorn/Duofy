// Setzt beim Öffnen eines Dialogs den Fokus in das erste Feld. Radzens eigene Einstellung nimmt das erste
// fokussierbare Element, und das ist meist ein Knopf in der Kopfzeile. Wird nur von FormDialogs aufgerufen.
// Unter Last kommen Rahmen und Inhalt spät, auch nacheinander: Es wird weiter gesucht, bis es etwas zum Fokussieren gibt, sonst
// bliebe der Fokus auf der Seite hinter dem Dialog.
export async function focusFirstField(dialogsBefore) {
    const deadline = performance.now() + 6000;
    let settled = false;
    while (performance.now() < deadline) {
        if (document.querySelectorAll(".rz-dialog").length > dialogsBefore) {
            if (!settled) {
                // Kurz warten, bis Radzen und die Schritte des Dialogs fertig gezeichnet sind.
                await new Promise(resolve => setTimeout(resolve, 80));
                settled = true;
            }
            const dialog = document.querySelectorAll(".rz-dialog")[dialogsBefore];
            // Wer inzwischen selbst im Dialog ist, behält seinen Platz.
            if (dialog?.contains(document.activeElement) || focusIn(dialog)) {
                return;
            }
        }
        await new Promise(resolve => setTimeout(resolve, 50));
    }
}

// Eine Rückfrage beginnt auf dem sicheren Knopf, dem letzten der beiden (Nein, Abbrechen), nicht auf Ja.
export async function focusSafeButton(dialogsBefore) {
    const deadline = performance.now() + 2000;
    while (performance.now() < deadline) {
        const dialog = document.querySelectorAll(".rz-dialog")[dialogsBefore];
        const buttons = dialog?.querySelectorAll(".rz-dialog-confirm-buttons button");
        if (buttons?.length) {
            buttons[buttons.length - 1].focus();
            return;
        }
        await new Promise(resolve => requestAnimationFrame(resolve));
    }
}

export function countDialogs() {
    return document.querySelectorAll(".rz-dialog").length;
}

// Sagt, ob es etwas zum Fokussieren gab.
function focusIn(dialog) {
    if (!dialog) {
        return false;
    }
    const visible = element => element.offsetParent !== null;
    const field = [...dialog.querySelectorAll("input:not([type=hidden]):not([disabled]), select:not([disabled]), textarea:not([disabled])")]
        .find(visible);
    // Ohne Feld (eine Rückfrage) bleibt es beim ersten Knopf im Inhalt.
    const target = field ?? [...dialog.querySelectorAll(".rz-dialog-content button:not([disabled])")].find(visible);
    target?.focus();
    return target !== undefined;
}
