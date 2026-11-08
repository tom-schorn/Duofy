// Der Fokus rund um das Fenster mit einem Rechtstext (Spec 109). Wird nur von LegalTextOverlay aufgerufen.
// Das Fenster ist modal: Alles, was nicht zu seinem Zweig gehört, wird inert (kein Tab, kein Klick, für Screenreader versteckt).
// Beim Schließen wird das zurückgenommen, und der Fokus geht an den Link, der das Fenster geöffnet hat.
let opener = null;
let blocked = [];
let trapped = null;

// Tab bleibt im Fenster: inert allein lässt den Fokus am Ende der Reihe in die Browseroberfläche (document.body) wandern.
const focusable = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
function trapTab(event) {
    if (event.key !== 'Tab') return;
    const panel = event.currentTarget;
    const stops = [...panel.querySelectorAll(focusable)];
    if (stops.length === 0) {
        event.preventDefault();
        panel.focus();
        return;
    }
    const first = stops[0];
    const last = stops[stops.length - 1];
    const active = document.activeElement;
    if (event.shiftKey && (active === first || active === panel)) {
        event.preventDefault();
        last.focus();
    } else if (!event.shiftKey && active === last) {
        event.preventDefault();
        first.focus();
    }
}

// opened ist der Link, der angeklickt wurde (vom Aufrufer beim Klick festgehalten). Safari fokussiert Links beim Klick nicht,
// deshalb ist document.activeElement erst der Ersatz, wenn kein Öffner bekannt ist (zum Beispiel beim Eintrag in der Fußzeile).
export function open(panel, opened) {
    release();
    opener = opened ?? document.activeElement;
    for (let node = panel; node && node !== document.body && node.parentElement; node = node.parentElement) {
        for (const sibling of node.parentElement.children) {
            if (sibling !== node && !sibling.inert) {
                sibling.inert = true;
                blocked.push(sibling);
            }
        }
    }
    trapped = panel;
    panel.addEventListener('keydown', trapTab);
    panel.focus();
}

export function close() {
    const back = opener;
    release();
    if (back && back !== document.body && back.isConnected) {
        back.focus();
    }
}

function release() {
    trapped?.removeEventListener('keydown', trapTab);
    trapped = null;
    for (const element of blocked) {
        element.inert = false;
    }
    blocked = [];
    opener = null;
}
