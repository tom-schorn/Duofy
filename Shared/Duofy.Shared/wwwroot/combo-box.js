// Hilfe für die Auswahl mit Suchfeld (ComboBox): Die mit den Pfeiltasten angesteuerte Option soll in der Liste zu sehen sein.
export function reveal(id) {
    document.getElementById(id)?.scrollIntoView({ block: "nearest" });
}
