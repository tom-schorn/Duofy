// Eine Meldung auf der Seite holt den Fokus nur, wenn er verloren ging: Die Zeile oder der Knopf, auf dem er stand, ist nach der
// Aktion weg (gelöscht, ersetzt), und der Browser setzt ihn auf BODY. Steht er noch irgendwo (ein Feld, ein Dialog), bleibt er dort.
// Radzen stellt den Fokus beim Schließen eines Dialogs erst nach einem Augenblick wieder her; deshalb wird kurz gewartet.
export async function focusIfLost(element) {
    await new Promise(resolve => setTimeout(resolve, 200));
    const active = document.activeElement;
    if (element?.isConnected && (!active || active === document.body || !active.isConnected)) {
        element.focus();
    }
}
