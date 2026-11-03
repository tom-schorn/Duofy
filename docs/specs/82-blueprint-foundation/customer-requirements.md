# Customer requirements: Oberfläche von Grund auf neu mit Blazor Blueprint

Format: 2

## Background

Die mit Radzen gebaute Oberfläche weicht in Aussehen, Layout und Responsive deutlich vom alten Duofy ab. Tom: "es sieht billig aus", "unsers weicht einfach sehr krass ab". Ein Versuch mit Blazor Blueprint ging in die richtige Richtung. Tom am 02.11.2026: "Radzn kann doch dann weg", "Alles auf einmal und das ui Thema von 0 anfangen. Du weißt wie es aussehen soll und gut ist. Braucht ja keine Backend Änderung. Ist ja im Grunde eine responsive Website in einer App bzw im Browser".

## Goal

Portal und App haben eine von Grund auf neue Oberfläche, die aussieht und sich aufbaut wie das alte Duofy, mit den Funktionen des heutigen Backends. Radzen und die alte `app.css` sind vollständig entfernt.

## Users

Tom und Jasmin täglich am Handy und am PC, später weitere Haushalte im gehosteten Plagevo und im Selbstbetrieb.

## Requirements

R1 (must): Alle Seiten sind neu gebaut und Radzen ist aus dem Code und den Paketen entfernt — because Tom keinen Mischbetrieb und keine Reste will.
R2 (must): Aussehen und Layout folgen dem alten Duofy, das lokal als Vergleich läuft, hell und dunkel, mit Werten aus einer einzigen Stelle — because Tom genau dieses Aussehen meint.
R3 (must): Dieselben Seiten laufen im Browser und in der Android-App — because es im Grunde eine responsive Website in einer App ist.
R4 (must): Jede Seite funktioniert am Handy, am Tablet und am PC ohne waagerechtes Scrollen — because Layout und Responsive genauso wichtig sind wie Farben.
R5 (must): Datenbank und API bleiben unverändert und alle heutigen Funktionen bleiben erreichbar — because Tom keine Backend-Änderung braucht und nichts verloren gehen darf.
R6 (must): Die Tests bleiben grün oder sind sinngleich auf die neue Oberfläche umgestellt — because sonst niemand belegen kann, dass Funktionen noch gehen.
R7 (must): Der Stand geht erst als Ganzes online, nach Toms Ja — because kein Mischstand live gehen soll.
R8 (should): Was das alte Duofy zeigt und sich aus den vorhandenen Daten ergibt, wird genauso gezeigt: Monatskarten, Topf-Balken, Verplanbar, Anstehend, Ungeplant, Haken-Dialog mit Datum und Betrag — because das zum Aussehen und zur Bedienung gehört.
R9 (should): Tastaturbedienung und Beschriftungen für Screenreader sind mindestens so gut wie heute — because Radzen hier schon geprüft wurde.
R10 (wont): Neue fachliche Regeln, die das Backend ändern würden, zum Beispiel Limit-Posten — because sie einzeln entschieden werden.
R11 (must): Die Navigation ist eindeutig: keine doppelten Titel, Personenauswahl unten über das Profil, Mitglieder unter „Unser Haushalt“ mit dem Namen des Haushalts, kein zweiter Haushalt-Eintrag unter Mehr, ein einziger Knopf für Hell und Dunkel — because Tom am 03.11.2026: „Generell immer doppelte titel“, „Die Personenauswahl sollte unten übers Profil gehen“, „Mitglieder sind teil des haushaltes“, „1 hätte geriecht“.
R12 (must): Am Handy ist jede Seite für das Handy gestaltet und nicht nur das PC-Layout untereinander; die untere Leiste hat Start, Plan, Haushalt, Sparen, Mehr — because Tom: „Ui ux für Handy da hast du garnix gemacht. Es war ein Pflichtteil“, „Sparen gehörte auch unten ins menü rein“.
R13 (must): Am PC zeigt eine erklärende Seitenleiste wie im alten Duofy, was die Seite bedeutet, mit Texten, die nur unsere tatsächlichen Regeln beschreiben — because Tom: „Auf dem PC wird die seitenleiste benötigt. das ist heutzutage normal im bereich ui ux“.
R14 (must): Die Auswertung besteht aus Diagrammen: Soll und Ist je Topf und je Kategorie als runde Diagramme und eine Tageskurve, die zeigt, ob und wann eine Lücke entsteht, aus Plan und echten Buchungen — because Tom: „0 UIUX. Hier würde ich Diagramme erwarten“, „Das diagram auf Plan kann in Auswertung“, Lücke „im grunde vom PLan und realen buchungen“.
R15 (must): Listen nutzen die Breite: Buchungen als übersichtliche Liste mit Ausgaben rot und mit Minus, Buchungsanzahl eines Postens in seiner Zeile, gleich große Kennzahl-Kästen, eine einzige Schrift für Beträge und Texte, kein Monatsumschalter oben rechts — because Tom: „wir haben genügend fläche um die breite auszunutzen“, „ausgaben bekommen rote zahlen mit einem minus davor“, „Die Boxen haben unterschiedliche größen“, „Schriftarten teils total unterschiedlich“, „Oben rechts der monatswitch kann weg“.
R16 (must): Im Haushaltsplan erreicht man die Monate des Haushalts, und die Posten sind nach Haushaltsmitglied gruppiert — because Tom: „ICh komme nicht auf die monatsauswahl“, „gruppieren auf namen des haushaltsmitglied“.
R17 (must): Ein Topf zeigt auf allen Seiten denselben Betrag, und Soll und Ist ergeben keine 0 %, nur weil die Einnahmen des Monats noch nicht gebucht sind — because der Lead beim Durchklicken drei verschiedene Beträge für Wünsche und überall Ist 0 % fand.
R19 (must): Am Tablet ist jede Seite für das Tablet gestaltet, hochkant und quer — because Tom: „und Tablet auch beachten“.
R20 (must): Die Druckversion eines Monats ist ein eigenes Druckbild und keine Kopie der Seite — because Tom: „Die Druckversion sollte net einfach eine seitenkopie sein. Da kannste auch mal komplett überarbeiten“.
R18 (wont): Name und Logo der Instanz in einer Einstellungsseite — because Tom das als „einstellungsseite für die instanz“ mit weiteren Einstellungen will; das braucht Backend und wird eine eigene Aufgabe nach diesem Umbau.

## Constraints

Lizenzen frei und kommerziell nutzbar, Versionen fest und gepatcht, .NET 10. Der PC hat 16 GB, deshalb höchstens eine Arbeitssitzung neben der des Leads. Toms frühere Entscheidung zur unteren Leiste am Handy (Start, Plan, Haushalt, Mehr) und zu Buchungen nur im Monat bleibt gültig.

## Open questions

None.
