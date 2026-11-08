// Setzt den Wert eines Textfelds, wenn die Seite ihn von außen ändert (zum Beispiel nach dem Absenden leert).
// Wird nur von TextInput aufgerufen. Was die Nutzerin gerade tippt, fasst es nicht an.
export function setValue(field, value) {
    if (field && field.value !== value) {
        field.value = value;
    }
}
