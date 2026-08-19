import type { ReactNode } from 'react'

/**
 * The texts of the explanation column, one set per page.
 *
 * They live here and not in the components because the column shows them all at
 * once: what belongs together has to be written together, otherwise the entries
 * drift apart in tone and start repeating each other.
 *
 * TODO: move the texts into the wiki and load an excerpt from there. Written twice
 * — once here, once in an article — they will contradict each other within weeks.
 * Needs a public wiki page first, see the note in the project docs.
 */

export type HelpEntry = {
  /** Stable key — decides which entry stays open across a re-render. */
  id: string
  title: string
  body: ReactNode
}

/** Which page a set of entries belongs to. */
export type HelpKey =
  | 'import'
  | 'plans'
  | 'plan'
  | 'book'
  | 'commitments'
  | 'accounts'
  | 'household'

const Code = ({ children }: { children: ReactNode }) => (
  <code className="bg-muted rounded px-1 py-0.5 text-[0.95em]">{children}</code>
)

const PLAN_BLOCKS: HelpEntry[] = [
  {
    id: 'needs',
    title: 'Fixkosten',
    body: (
      <>
        <p>
          Ausgaben, die zur Aufrechterhaltung des Haushalts erforderlich sind: Miete,
          Energie, Lebensmittel, Versicherungen, Arbeitsweg.
        </p>
        <p>
          Die Zuordnung triffst du, nicht die Kategorie. Kraftstoff für den Arbeitsweg
          gehört zu den Fixkosten, Kraftstoff für Ausflüge zu den Wünschen — dieselbe
          Kategorie, zwei Bewertungen.
        </p>
        <p className="text-foreground font-medium">Richtwert: 50 % des Budgets.</p>
      </>
    ),
  },
  {
    id: 'wants',
    title: 'Wünsche',
    body: (
      <>
        <p>
          Ausgaben, die den Lebensstandard betreffen, ohne notwendig zu sein:
          Gastronomie, Hobbys, Abonnements, Reisen.
        </p>
        <p>
          Investitionen zählen ebenfalls hierher. Ein eigener vierter Bereich würde den
          Richtwert 50/30/20 aufheben; sichtbar bleiben sie über die Kategorie.
        </p>
        <p className="text-foreground font-medium">Richtwert: 30 % des Budgets.</p>
      </>
    ),
  },
  {
    id: 'savings',
    title: 'Sparen',
    body: (
      <>
        <p>
          Beträge, die dem Vermögen erhalten bleiben: Rücklagen, Sparziele und Tilgung.
          Eine Tilgungsrate verringert eine Verbindlichkeit und gilt deshalb nicht als
          Verbrauch.
        </p>
        <p>
          Sparziele und Schulden ordnet Duofy diesem Bereich selbst zu, unabhängig von
          ihrer Kategorie. Es ist die einzige Zuordnung, die nicht zur Wahl steht.
        </p>
        <p className="text-foreground font-medium">Richtwert: 20 % des Budgets.</p>
      </>
    ),
  },
]

export const HELP: Record<HelpKey, { title: string; entries: HelpEntry[] }> = {
  import: {
    title: 'Import',
    entries: [
      {
        id: 'parking',
        title: 'Parkposition',
        body: (
          <>
            <p>
              Was aus einer Bankdatei kommt, wird nicht sofort gebucht, sondern
              landet hier. Erst wenn du einer Zeile eine Kategorie gibst, wird
              eine Buchung daraus.
            </p>
            <p>
              Der Zwischenschritt ist Absicht: die Zeilen dürfen liegen bleiben.
              Du kannst heute zehn zuordnen und den Rest nächste Woche.
            </p>
          </>
        ),
      },
      {
        id: 'file',
        title: 'Welche Datei',
        body: (
          <>
            <p>
              Das Format heißt <strong>CAMT</strong> und ist genormt — jede Bank
              liefert dieselbe Struktur. Im Online-Banking heißt der Export meist
              „Umsätze exportieren“ und bietet CAMT neben CSV und PDF an.
            </p>
            <p>
              Duofy verbindet sich <strong>nicht</strong> mit deiner Bank und will
              keine Zugangsdaten. Du lädst die Datei selbst herunter und hier hoch.
            </p>
          </>
        ),
      },
      {
        id: 'account',
        title: 'Welches Konto',
        body: (
          <p>
            Steht in der Datei: ein Auszug nennt die IBAN, deren Umsätze er
            enthält. Beim ersten Mal fragt Duofy einmal, welches deiner Konten
            gemeint ist, und merkt es sich dort. Danach kommt die Frage nicht
            wieder.
          </p>
        ),
      },
      {
        id: 'twice',
        title: 'Dieselbe Datei zweimal',
        body: (
          <p>
            Schadet nichts. Jede Buchung trägt eine Kennung der Bank; was schon im
            Buch steht oder schon hier liegt, wird übersprungen. Auch was du
            verworfen hast, kommt nicht zurück.
          </p>
        ),
      },
      {
        id: 'balances',
        title: 'Salden gehen nicht auf',
        body: (
          <p>
            Ein Auszug nennt Anfangs- und Schlussstand. Duofy rechnet nach: beide
            plus alle Buchungen dazwischen müssen zusammenpassen. Wenn nicht,
            fehlt meist eine Seite der Datei — größere Auszüge kommen manchmal als
            ZIP mit mehreren Teilen.
          </p>
        ),
      },
      {
        id: 'nothing-changed',
        title: 'Was der Import nicht tut',
        body: (
          <p>
            Er ändert deinen Plan nicht und legt keine Verträge an. Er trägt nur
            ein, was tatsächlich passiert ist. Was passieren <em>soll</em>, steht
            weiterhin in deinen Verträgen und im Monatsplan.
          </p>
        ),
      },
    ],
  },
  plans: {
    title: 'Planung',
    entries: [
      {
        id: 'overview',
        title: 'Die Monatsübersicht',
        body: (
          <>
            <p>
              Jeder Monat, den du angelegt hast, mit seinen Kennzahlen: Budget, was noch
              offen ist, wie die drei Bereiche gegen ihre Richtwerte stehen.
            </p>
            <p>
              Monate entstehen nicht von selbst. Beim Anlegen erzeugt Duofy die Posten
              aus allen Verpflichtungen, die in diesem Monat fällig sind.
            </p>
          </>
        ),
      },
      {
        id: 'switch',
        title: 'Eine andere Person ansehen',
        body: (
          <>
            <p>
              Der Umschalter unter dem Logo richtet die ganze Anwendung auf eine andere
              Person aus — sofern sie dir Einblick gegeben hat.
            </p>
            <p>
              Wie viel du siehst, entscheidet sie, getrennt für Planung, Verträge und
              Konten. Was du nicht ändern darfst, wird ohne Knöpfe angezeigt.
            </p>
          </>
        ),
      },
      {
        id: 'ritual',
        title: 'Wann geplant wird',
        body: (
          <p>
            Ein Monat wird nicht abgeschlossen — einen Bestätigungsschritt gibt es
            nicht. Duofy bildet einen wiederkehrenden Termin ab, keinen Vorgang mit
            Ende: geplant wird vor dem Monat, nachgetragen wird laufend.
          </p>
        ),
      },
    ],
  },

  plan: {
    title: 'Monatsplan',
    entries: [
      {
        id: 'month',
        title: 'Monatsplan',
        body: (
          <>
            <p>
              Der Monatsplan enthält alle Ein- und Auszahlungen, die für einen Monat
              vorgesehen sind. Er beschreibt den Soll-Zustand; was tatsächlich gebucht
              wurde, steht im Buch.
            </p>
            <p>
              Grundlage der Verteilung ist das Budget: Einnahmen abzüglich Puffer,
              aufgeteilt auf die drei folgenden Bereiche.
            </p>
          </>
        ),
      },
      ...PLAN_BLOCKS,
      {
        id: 'tick',
        title: 'Posten abhaken',
        body: (
          <>
            <p>
              Das Abhaken erzeugt eine Buchung. Datum und Betrag sind mit dem heutigen
              Datum und dem geplanten Betrag vorbelegt und lassen sich vor dem
              Bestätigen anpassen.
            </p>
            <ul className="mt-2 flex list-disc flex-col gap-1 pl-4">
              <li>Der Posten entfällt aus „Noch offen“.</li>
              <li>
                Bestehen zum Posten bereits Buchungen — etwa bei einem Budget, auf das
                über den Monat eingekauft wurde —, entsteht keine weitere. Der Haken
                bedeutet dann „Monat erledigt“; andernfalls würden die Beträge doppelt
                erfasst.
              </li>
              <li>
                Bei Sparzielen mit hinterlegtem Zielkonto entsteht eine Umbuchung statt
                einer Ausgabe: das Geld verbleibt im Haushalt.
              </li>
              <li>
                Bei Einnahmen bedeutet der Haken „eingegangen“ und wirkt sich auf „Noch
                offen“ nicht aus.
              </li>
            </ul>
            <p className="mt-2">
              Ein abweichendes Buchungsdatum verschiebt den Posten nicht: Ein
              Augustposten mit Julidatum ergibt eine Julibuchung auf einem Augustposten.
            </p>
          </>
        ),
      },
    ],
  },

  book: {
    title: 'Buch',
    entries: [
      {
        id: 'book',
        title: 'Was das Buch ist',
        body: (
          <p>
            Das Buch enthält die tatsächlichen Buchungen eines Monats. Der Plan
            beschreibt den Soll-Zustand, das Buch den Ist-Zustand; die Gegenüberstellung
            beider zeigt, wie tragfähig die Planung war.
          </p>
        ),
      },
      {
        id: 'assignment',
        title: 'Buchung mit und ohne Posten',
        body: (
          <>
            <p>
              Eine Buchung kann einem Posten zugeordnet sein und wird dann gegen dessen
              geplanten Betrag gerechnet. Buchungen ohne Zuordnung sind zulässig — nicht
              jede Ausgabe ist geplant.
            </p>
            <p>
              Kategorie und Bereich übernimmt die Buchung vom zugeordneten Posten. Eine
              nachträgliche Kategorisierung entfällt damit.
            </p>
          </>
        ),
      },
      {
        id: 'transfer',
        title: 'Umbuchung statt Ausgabe',
        body: (
          <p>
            Ein Übertrag zwischen zwei eigenen Konten ist keine Ausgabe: das Vermögen
            bleibt unverändert. Als Ausgabe erfasst, wäre die Monatssumme zu hoch.
            Umbuchungen führen deshalb zwei Konten und gehen nicht in die Ausgaben ein.
          </p>
        ),
      },
      {
        id: 'shared-book',
        title: 'Gemeinsames Buch: wer fehlt',
        body: (
          <p>
            Das gemeinsame Buch enthält nur die Buchungen der Mitglieder, die Einblick
            in ihre Konten gewährt haben. Mitglieder ohne diese Freigabe fehlen in der
            Summe. Duofy weist darauf hin, statt eine unvollständige Zahl unkommentiert
            anzuzeigen.
          </p>
        ),
      },
    ],
  },

  commitments: {
    title: 'Verträge',
    entries: [
      {
        id: 'commitment',
        title: 'Was hier hineingehört',
        body: (
          <>
            <p>
              Alles Wiederkehrende: Verträge, selbst gesetzte Budgets, Sparziele und
              Schulden. Einmal angelegt, erzeugen sie ihre Posten in jedem Monat selbst,
              in dem sie fällig sind.
            </p>
            <p>
              Gruppiert wird nach Bereich, nicht nach Typ. Ein Vertrag kann in jedem
              Bereich liegen — die Miete in den Fixkosten, das Streaming-Abo in den
              Wünschen.
            </p>
          </>
        ),
      },
      {
        id: 'types',
        title: 'Die fünf Typen',
        body: (
          <ul className="flex list-disc flex-col gap-1 pl-4">
            <li>
              <strong>Läuft weiter</strong> — Vertrag ohne festes Ende: Miete, Handy,
              Versicherung.
            </li>
            <li>
              <strong>Hat ein Ziel</strong> — Sparziel mit Zielbetrag, zählt zu Sparen.
            </li>
            <li>
              <strong>Wird abbezahlt</strong> — Kredit oder Rückstand, läuft auf null
              und zählt ebenfalls zu Sparen.
            </li>
            <li>
              <strong>Setze ich selbst</strong> — kein Vertrag, du legst den Betrag
              fest: Lebensmittel, Sprit, Taschengeld.
            </li>
            <li>
              <strong>Kommt rein</strong> — Gehalt, Transferleistungen, Zinsen.
            </li>
          </ul>
        ),
      },
      {
        id: 'rhythm',
        title: 'Rhythmus und Fälligkeit',
        body: (
          <p>
            „Feb, Mai, Aug, Nov“ ist gerechnet, nicht eingetippt: vierteljährlich ab
            Februar. Du gibst den Rhythmus und den ersten Termin an, die Monate ergeben
            sich daraus. Deshalb steht ein Jahresbeitrag nur in dem Monat im Plan, in
            dem er wirklich abgebucht wird.
          </p>
        ),
      },
      {
        id: 'privacy',
        title: 'Wer deine Verträge sieht',
        body: (
          <p>
            Niemand, solange du nichts freigibst. Andere sehen den Posten, den ein
            Vertrag im gemeinsamen Plan erzeugt — den Vertrag selbst mit Anbieter,
            Betrag und Rhythmus nur, wenn du im Haushalt <Code>Verträge</Code> freigibst.
          </p>
        ),
      },
    ],
  },

  accounts: {
    title: 'Konten',
    entries: [
      {
        id: 'account',
        title: 'Wofür Konten da sind',
        body: (
          <>
            <p>
              Konten sind Zahlungskonten: Giro, Tagesgeld, Karte, Bargeld. Ihr Stand
              ergibt sich aus den Buchungen darauf.
            </p>
            <p>
              Für die Planung selbst braucht es sie nicht — der Posten weiß, wer ihn
              zahlt. Sie tragen das Buch und später den Abgleich mit der Bank.
            </p>
          </>
        ),
      },
      {
        id: 'no-depot',
        title: 'Warum kein Depot',
        body: (
          <p>
            Der Wert eines Depots ändert sich mit dem Kurs, nicht mit Buchungen. Ein
            Stand aus Anfangsbetrag plus Buchungen wäre dauerhaft falsch. Ein Wertpapier
            zu kaufen ist deshalb eine Umbuchung auf das Verrechnungskonto.
          </p>
        ),
      },
      {
        id: 'available',
        title: 'Verfügbar und Kontostand',
        body: (
          <p>
            Nicht jedes Konto zählt zum verfügbaren Geld. Ein Tagesgeldkonto, auf dem
            eine Rücklage liegt, hat einen Stand, ist aber nicht das, was diesen Monat
            zur Verfügung steht — dafür gibt es den Schalter am Konto.
          </p>
        ),
      },
    ],
  },

  household: {
    title: 'Haushalt',
    entries: [
      {
        id: 'household',
        title: 'Was ein Haushalt ist',
        body: (
          <>
            <p>
              Eine Planungsebene, mehr nicht. Ein Haushalt besitzt nichts: keine Konten,
              keine Verträge, keine Posten. Er sagt nur, wer zusammen plant.
            </p>
            <p>
              Der gemeinsame Plan ist keine eigene Tabelle, sondern die Zusammenstellung
              aller Posten, die ihr als gemeinsam markiert habt. Der Posten existiert
              genau einmal — es gibt nichts abzugleichen.
            </p>
          </>
        ),
      },
      {
        id: 'grants',
        title: 'Freigabe je Bereich',
        body: (
          <>
            <p>
              Was die anderen von dir sehen, entscheidest du getrennt für drei
              Bereiche. Die Stufe vergibst nur du selbst und nur für dich;
              zurücknehmen kannst du sie jederzeit, ohne jemanden zu fragen.
            </p>
            <p>
              Jeder Bereich hat dieselben vier Stufen, und sie bauen aufeinander
              auf — wer ändern darf, darf auch sehen.
            </p>
          </>
        ),
      },
      {
        id: 'areas',
        title: 'Die drei Bereiche',
        body: (
          <>
            <p>
              <strong>Planung</strong> — deine Monatspläne und die Posten darin.
              Auf der untersten Stufe sieht der andere nur, was ihr gemeinsam
              plant; darüber auch deine privaten Posten.
            </p>
            <p>
              <strong>Verträge</strong> — was bei dir dauerhaft läuft: Verträge,
              Sparziele, Schulden, Einnahmen. Das ist der Bereich, aus dem sich
              deine Monate von selbst füllen.
            </p>
            <p>
              <strong>Konten</strong> — deine Konten <em>und</em> das Haushaltsbuch.
              Beides zusammen, mit Absicht: einen Kontostand zu sehen, ohne die
              Buchungen zu kennen, aus denen er entsteht, wäre ein Rätsel und
              keine Auskunft.
            </p>
          </>
        ),
      },
      {
        id: 'levels',
        title: 'Die vier Stufen',
        body: (
          <>
            <p>
              <strong>Nichts</strong> — bei der Planung heißt das: nur die
              gemeinsamen Posten. Bei Verträgen und Konten: gar nichts.
            </p>
            <p>
              <strong>Sehen</strong> — der andere sieht alles in diesem Bereich,
              auch das Private, und kann nichts davon anfassen.
            </p>
            <p>
              <strong>Ändern</strong> — dazu anlegen, ändern und abhaken. Das ist
              die Stufe für jemanden, der dir beim Einrichten oder beim Planen
              hilft. Jede Änderung an einem Posten steht mit Namen im Protokoll.
            </p>
            <p>
              <strong>Löschen</strong> — endgültig wegnehmen. Eine eigene Stufe,
              weil sich die beiden im Schaden unterscheiden: eine falsche
              Änderung steht im Protokoll und lässt sich zurücknehmen, eine
              Löschung steht nirgends und ist weg. Die meisten brauchen sie nie.
            </p>
          </>
        ),
      },
      {
        id: 'areas-hang-together',
        title: 'Wie die Bereiche zusammenhängen',
        body: (
          <>
            <p>
              Die drei sind unabhängig — <em>Ändern</em> bei der Planung sagt
              nichts über deine Verträge. Aber sie hängen inhaltlich zusammen,
              und das merkt man beim Benutzen:
            </p>
            <p>
              Ein Monat entsteht aus den <strong>Verträgen</strong>. Wer dir beim
              Planen helfen soll, aber keinen Zugriff auf die Verträge hat, kann
              den Monat anlegen und sieht doch nicht, woher die Posten kommen.
            </p>
            <p>
              Ein Posten abhaken erzeugt eine Buchung auf einem{' '}
              <strong>Konto</strong>. Ohne den Bereich Konten sieht der andere
              also die Wirkung seines eigenen Häkchens nicht.
            </p>
            <p>
              Für echte Vertretung — jemand hilft dir, Duofy einzurichten und zu
              führen — braucht es <em>Ändern</em> in allen dreien. Für einen Blick
              auf den gemeinsamen Monat reicht die unterste Stufe bei der Planung.
            </p>
          </>
        ),
      },
      {
        id: 'invitations',
        title: 'Einladungen',
        body: (
          <p>
            Eine Einladung geht an eine E-Mail-Adresse, nicht an ein Konto — die
            eingeladene Person muss noch keins haben. Sie findet die Einladung nach der
            Anmeldung hier auf dieser Seite. Es wird keine Mail verschickt.
          </p>
        ),
      },
      {
        id: 'leaving',
        title: 'Austreten',
        body: (
          <p>
            Die Posten, die du eingebracht hast, bleiben im Haushalt stehen — was
            gemeinsam geplant war, war gemeinsam geplant, und ein Austritt schreibt
            vergangene Monate nicht um. In neue Pläne fließt nichts mehr.
          </p>
        ),
      },
    ],
  },
}
