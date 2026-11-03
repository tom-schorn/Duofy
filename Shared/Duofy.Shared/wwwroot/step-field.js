// Setzt Pflicht und Fehler an das Eingabefeld eines StepField. Das input eines Felds im Inhalt erreicht StepField
// nur über die Id. Wird nur von StepField aufgerufen.
export function markField(id, required, invalid, errorId) {
    const field = document.getElementById(id) ?? document.querySelector(`[aria-labelledby="${id}-label"]`);
    if (!field) {
        return;
    }
    field.setAttribute("aria-required", required ? "true" : "false");
    field.setAttribute("aria-invalid", invalid ? "true" : "false");
    if (invalid && !field.hasAttribute("aria-describedby")) {
        field.setAttribute("aria-describedby", errorId);
    }
}
