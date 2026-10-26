# Etappe 3 — Konto und Buchung, von Tom selbst entworfen

## Kontext

Etappe 1 (Vertrag) und Etappe 2 (Monatsplan) sind im Hauptzweig, 33 Tests grün.
Bisher habe ich Klassen und Tests vorgegeben, Tom hat die Methoden geschrieben.

Tom will es eine Stufe schwerer: Er überlegt selbst, welche Informationen ein
Objekt braucht und welche Funktionen daran nützlich sind. Dafür eignet sich das
Haushaltsbuch, also Konto und Buchung, weil dort mehrere echte
Entwurfsentscheidungen stecken, bei denen es kein eindeutiges Richtig gibt.

Diese Etappe bleibt in `Duofy.Domain`, ohne Datenbank und ohne Oberfläche.
Umbuchungen zwischen zwei Konten und die Verbindung zum Monatsplan (Abhaken
erzeugt Buchung) sind bewusst **nicht** drin, das sind die nächsten beiden Etappen.

## Was anders läuft als bisher

Ich gebe keine Klassen und keine Feldlisten vor, sondern nur Situationen aus dem
Alltag. Tom entwirft daraus die Klassen. Erst danach schreibe ich die Tests, und
zwar gegen **seinen** Entwurf.

## Die Situationen (Toms Aufgabenstellung)

1. Lena fängt mitten im Jahr mit Duofy an. Auf ihrem Girokonto sind an dem Tag
   1.240,50 Euro. Ältere Bewegungen will sie nicht nachtragen.
2. Sie hat außerdem ein Sparkonto und etwas Bargeld. Im Buch sollen die drei
   auseinanderzuhalten sein.
3. Am 3. Oktober kauft sie für 54,20 Euro ein und zahlt mit der Karte vom
   Girokonto. Am 28. Oktober kommt ihr Gehalt von 2.400 Euro auf dasselbe Konto.
4. Sie will wissen: Wie viel ist heute auf dem Girokonto? Und wie viel war es am
   15. Oktober?
5. Sie hat sich beim Einkauf vertippt, es waren 45,20 Euro. Sie korrigiert die
   Buchung. Danach muss der Kontostand stimmen, ohne dass sie sonst etwas tut.
6. Sie trägt versehentlich eine Buchung ein, die vor ihrem Start mit Duofy liegt.
7. Ein Konto, das sie aufgelöst hat, soll aus der Auswahl verschwinden, seine
   alten Buchungen sollen aber bleiben.

## Ablauf

1. **Tom entwirft.** Er legt die Klassen in `Duofy.Domain` an, nur mit
   Eigenschaften und den Köpfen der Methoden; die Rümpfe dürfen
   `throw new NotImplementedException();` sein. Alternativ beschreibt er den
   Entwurf in Stichworten im Chat. Zu klären hat er unter anderem:
   - Welche Klassen gibt es überhaupt, und was gehört in welche?
   - Wie merkt sich das Programm, ob Geld hereinkommt oder hinausgeht?
   - Woher kommt der Kontostand?
   - Wer kennt die Buchungen: das Konto selbst, oder bekommt eine Methode sie
     übergeben?
2. **Ich schaue drüber.** Ich stelle Rückfragen zu Stellen, an denen eine der
   Situationen nicht aufgeht, und zeige danach, wie das Original dieselben Fragen
   beantwortet hat und warum (`backend/app/models/account.py`,
   `transaction.py`, Architekturentscheidung 0001). Wo sein Entwurf abweicht,
   entscheidet Tom, ob er dabei bleibt.
3. **Ich schreibe die Tests** gegen den abgestimmten Entwurf, eine Testdatei pro
   Klasse, die Situationen oben als Fälle.
4. **Tom schreibt die Methoden**, bis alles grün ist. Wo es passt, mit Lambdas,
   weil er die üben will (eine Summe über gefilterte Buchungen ist dafür ideal).
5. Kommentare auf Deutsch und für Leser ohne Vorwissen; die schreibt Tom für
   seine Klassen selbst, ich lese gegen.
6. Diesen Plan nach `docs/plans/` kopieren, CHANGELOG ergänzen.
7. Wenn alles grün ist und Tom sagt, dass es passt: Pull Request und Übernahme
   in den Hauptzweig (stehende Freigabe).

Arbeitszweig: `feat/domain-accounts-bookings`, vom Hauptzweig.

## Vorhandenes, das wiederverwendet wird

- Die Regeln aus den ersten Etappen gelten weiter: Geld ist `decimal`, Datum ohne
  Uhrzeit ist `DateOnly`, Pflichtfelder sind `required`
- Aufbau der Tests wie in `Duofy.Domain.Tests/PlanCreationTests.cs` (eine
  Hilfsmethode am Ende, die ein Beispielobjekt baut)

## Prüfung

- `dotnet test Duofy.Domain.Tests`: die 33 alten Tests und die neuen sind grün
- Jede der sieben Situationen ist durch mindestens einen Test abgedeckt
- Jeder neue Test war vorher einmal rot
