// Eine Meldung auf der Seite holt den Fokus nur, wenn er verloren ging: Die Zeile oder der Knopf, auf dem er stand, ist nach der
// Aktion weg (gelöscht, ersetzt), und der Browser setzt ihn auf BODY. Steht er noch irgendwo (ein Feld, ein Dialog), bleibt er dort.
// FormDialogs stellt den Fokus beim Schließen eines Dialogs erst nach einem Augenblick wieder her; deshalb wird kurz gewartet.
export async function focusIfLost(element) {
    await new Promise(resolve => setTimeout(resolve, 200));
    const active = document.activeElement;
    if (element?.isConnected && (!active || active === document.body || !active.isConnected)) {
        element.focus();
    }
}

// Setzt den Fokus auf ein Feld, das eine Seite über seine Id kennt.
export function focusById(id) {
    document.getElementById(id)?.focus();
}

// Eine Meldung, die sich gleich selbst schließt, gibt den Fokus vorher an den Inhalt der Seite weiter, wenn sie ihn hat;
// sonst fiele er beim Verschwinden wieder auf BODY.
export function handOver(element, id) {
    if (element?.isConnected && element.contains(document.activeElement)) {
        document.getElementById(id)?.focus();
    }
}
