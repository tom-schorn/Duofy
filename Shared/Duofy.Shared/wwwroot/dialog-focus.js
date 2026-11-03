// Der Fokus rund um Dialoge. Wird nur von FormDialogs aufgerufen.
// Unter Last kommt der Dialog spät: Es wird weiter gesucht, bis es etwas zum Fokussieren gibt, sonst bliebe der Fokus
// auf der Seite hinter dem Dialog.
const triggers = [];

const dialogSelector = "[role='dialog'], [role='alertdialog']";

function topDialog() {
    const all = document.querySelectorAll(dialogSelector);
    return all.length ? all[all.length - 1] : null;
}

const visible = element => element.offsetParent !== null;

export function rememberTrigger() {
    triggers.push(document.activeElement);
}

export async function restoreTrigger() {
    const trigger = triggers.pop();
    // Kurz warten, bis der Dialog weg ist; sonst zöge sein Fokusfang den Fokus zurück.
    await new Promise(resolve => setTimeout(resolve, 50));
    if (trigger?.isConnected && trigger !== document.body) {
        trigger.focus();
    }
}

export async function focusFirstField() {
    const deadline = performance.now() + 6000;
    const before = topDialog();
    while (performance.now() < deadline) {
        const dialog = topDialog();
        if (dialog && dialog !== before) {
            // Kurz warten, bis die Schritte des Dialogs fertig gezeichnet sind.
            await new Promise(resolve => setTimeout(resolve, 80));
            const field = [...dialog.querySelectorAll("input:not([type=hidden]):not([disabled]), select:not([disabled]), textarea:not([disabled])")].find(visible);
            const target = field ?? [...dialog.querySelectorAll("button:not([disabled])")].find(visible);
            if (target) {
                target.focus();
                return;
            }
        }
        await new Promise(resolve => setTimeout(resolve, 50));
    }
}

// Eine Rückfrage beginnt auf dem sicheren Knopf: dem mit dem Text für Nein, der in Blueprint vor Ja steht.
export async function focusSafeButton() {
    const deadline = performance.now() + 2000;
    const before = topDialog();
    while (performance.now() < deadline) {
        const dialog = topDialog();
        if (dialog && dialog !== before) {
            const buttons = [...dialog.querySelectorAll("button:not([disabled])")].filter(visible);
            if (buttons.length) {
                await new Promise(resolve => setTimeout(resolve, 80));
                buttons[0].focus();
                return;
            }
        }
        await new Promise(resolve => requestAnimationFrame(resolve));
    }
}
