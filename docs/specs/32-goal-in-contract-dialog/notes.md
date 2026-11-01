# Spec-Vermerk #32: Sparziel im Vertragsdialog verknüpfen

- Im Schritt "Wer und Wohin" kann bei einem Vertrag mit Zielkonto ein Sparziel gewählt werden. Der Vertrag merkt sich die Zielkennung (`savings_goal_id`).
- Wird der Plan-Posten abgehakt und dabei gebucht, trägt die Buchungszeile das Ziel; der Stand des Ziels wächst um die Rate.
- Das Ziel muss derselben Person gehören. Fremde oder unbekannte Ziele: `400 savings_goal_not_found`.
