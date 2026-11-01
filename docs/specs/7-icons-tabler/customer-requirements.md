# Customer requirements: Tabler-Symbole als Baustein mit Lizenzdatei

Format: 2

> **Alt (Stand vor der Radzen-Umstellung, Issue 76):** Diese Spezifikation nennt die frühere Oberflächenbibliothek MudBlazor und die Tabler-Symbole. Heute gelten Radzen und Material Symbols, siehe `docs/plans/radzen-plan.md`. Fachliche Regeln und Abläufe bleiben gültig.

## Background

Für Zahlart, Konten und Navigation sind Symbole gewählt (Tabler Icons, MIT). Heute nutzt die App die Symbole von MudBlazor (Material). Die Marken-Runde hat Tabler im Stil „outline“ bestätigt (29.10.2026).

## Goal

Ein Baustein zeigt Tabler-Symbole in der aktuellen Textfarbe, lokal ausgeliefert, mit Lizenz und nur den benutzten Symbolen.

## Users

Das Team beim Bauen der Seiten; mittelbar alle Personen, die Plagevo benutzen.

## Requirements

R1 (must): Ein Baustein zeigt ein Symbol anhand seines Namens als Inline-SVG in der aktuellen Textfarbe — because Symbole in Hell und Dunkel mitfärben müssen.
R2 (must): Der Lizenztext von Tabler liegt im Projekt, Quelle und Version stehen in der Doku — because die MIT-Lizenz den Hinweis bei Weitergabe verlangt.
R3 (must): Nur die benutzten Symbole werden ausgeliefert, nicht das ganze Paket — because die Auslieferung klein und überschaubar bleiben soll.
R4 (must): Jedes Symbol ist entweder beschriftet oder als dekorativ für Screenreader verborgen — because Barrierefreiheit gefordert ist.
R5 (should): Der Anfangsbestand enthält cash, building-bank, credit-card, pig-money, wallet, repeat, arrows-exchange und coins — because das die ersten Konto- und Zahlartsymbole sind.

## Constraints

Nur der Stil outline. Keine Anfrage an einen Fremdserver zur Laufzeit. Die Symbole kommen aus @tabler/icons Version 3.50.0 (MIT), die zum 29.10.2026 die aktuelle war; liegen bereit unter C:/Users/tomsc/AppData/Local/Temp/icons-preview/dl/x-tabler-icons-3.50.0.tgz/package/icons/outline/ (nur lesen). Kein Node auf diesem Rechner starten, solange er nicht aktualisiert ist.

## Open questions

None.
