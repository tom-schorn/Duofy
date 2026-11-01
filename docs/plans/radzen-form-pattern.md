# Muster für Formulare und Dialoge mit Radzen

Entstanden in R5 (Dialogdienst). Gilt für alle folgenden Slices. Belegt durch `FormDialogsTests`, `ServerChoiceTests` und die Radzen-Doku (Context7, `/radzenhq/radzen-blazor`, geprüft am 01.11.2026) sowie Radzen.Blazor 12.1.8 selbst.

## Dialoge öffnen

- Seiten rufen nur `FormDialogs`: `OpenAsync<TDialog, TResult>(title, parameters)` und `ConfirmAsync(message, title, yesText, noText)`. Sie kennen weder `DialogOptions` noch `DialogService`.
- Das Ergebnis ist ein `DialogOutcome<TResult>`: erst `Saved` prüfen, dann `Value` lesen. Abbrechen, das X und Esc liefern `Saved == false` („nichts geändert“).
- Der Dialog schließt sich selbst: `DialogService.Close(ergebnis)` zum Speichern, `DialogService.Close()` zum Abbrechen. Er bekommt `DialogService` per `@inject`.
- Parameter gehen über `FormParameters<TDialog>().Add(d => d.Name, wert)`. Radzen nimmt ein `Dictionary<string, object>`; der Ausdruck prüft Name und Typ beim Übersetzen.
- Der Titel kommt nicht von allein im Dialog an. Braucht ihn der Dialog selbst (Kopfzeile in der App), bekommt er einen `Title`-Parameter.

## Wie der Dialog aufgeht

| | Browser | App |
|---|---|---|
| Form | Fenster, 600 px breit, Titelzeile mit X | Vollbild, keine Titelzeile von Radzen, die Kopfzeile zeichnet `FullScreenFormHeader` |
| Klick daneben | schließt | schließt nicht |
| Esc | schließt | schließt |
| Fokus | erstes Feld beim Öffnen, danach zurück | wie Browser |

Der einzige Inline-Stil ist `max-width: none; border-radius: 0;` für das Vollbild (Plan, Frage 1). Radzen hat keine Vollbild-Einstellung; `Width` und `Height` mit `100%` genügen, weil der Rahmen den Dialog zentriert und füllt.

Nicht im Test belegt, weil bUnit kein echtes JavaScript hat: Esc und die Fokusrückgabe. `CloseDialogOnEsc` ist gesetzt und getestet. Den Fokus ins erste Feld setzt `FormDialogs` selbst über `wwwroot/dialog-focus.js`, weil Radzens `AutoFocusFirstElement` das erste fokussierbare Element nimmt, oft einen Knopf der Kopfzeile (im Browser mit der Testseite /schritt-dialog geprüft).

## Eingabefelder

Befund aus dem gerenderten Markup:

| Komponente | `data-testid`, `id`, `aria-*`, `type`, `autocomplete` landen … |
|---|---|
| `RadzenTextBox` | am `input` selbst |
| `RadzenPassword` | am `input` selbst |
| `RadzenNumeric` | am Rahmen (`span`) um das `input`; die Tests finden das Feld mit `Field(cut, testId)`, das bei Bedarf in den Rahmen hineinsucht |
| `RadzenButton` | am `button` selbst |

- `RadzenTextBox` übernimmt `type="url"` und `autocomplete`.
- **`RadzenNumeric` lässt keine Id am `input` zu.** `InputAttributes` reicht `data-*`, `aria-labelledby`, `aria-describedby` und `inputmode` durch, verwirft aber `id`, `name` und `aria-label` (der Rahmen trägt eine eigene zufällige Id). Eine Beschriftung mit `for` kann also nicht auf das Feld zeigen. Muster: `<FieldLabel For="" id="x-label" Text="…" />` und am Feld `InputAttributes="@(new Dictionary<string, object> { ["aria-labelledby"] = "x-label" })"`. Wer die Id des Felds braucht (StepField, Skripte), muss das bedenken.
- Ein `RadzenFormField` mit `Text` zeichnet eine schwebende Beschriftung ohne `for` und ersetzt `Placeholder` durch ein Leerzeichen. Wo die Oberfläche die Beschriftung über dem Feld zeigt (`FieldLabel`), bleibt es bei `FieldLabel` mit `For="id"` und einem einfachen `RadzenTextBox`.
- Fehlertexte von Prüfungen stehen im `Helper` des `RadzenFormField` (zum Beispiel `RadzenRequiredValidator Component="Name" Text="…"`), das Feld braucht dafür einen `Name`. Der Fehler eines Aufrufs (Server antwortet nicht) steht als `RadzenAlert` oben im Dialog.
- Die Prüfungen mit `RadzenTemplateForm` kommen in R11; dort ist dieses Muster mit einem Validator zu ergänzen.

## Meldungen

- `RadzenAlert` zeichnet ein Symbol als Text in einem `i`-Element. Tests prüfen den Text deshalb mit `Contains`, nicht mit `Equal`.
- `AllowClose="false"`: Meldungen verschwinden mit der nächsten Aktion, ein Schließknopf wäre ein zusätzliches Bedienelement.
