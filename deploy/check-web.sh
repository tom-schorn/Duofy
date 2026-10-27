#!/bin/sh
# Prüft nach jedem Start oder Update, ob die Web-Variante wirklich benutzbar ist (Spec 79, docs/betrieb.md Schritt 8).
#
#   deploy/check-web.sh                              prüft https://portal.plagevo.de
#   deploy/check-web.sh http://localhost:8080        prüft eine andere Adresse
#
# Geprüft wird zweierlei: /health muss antworten, und /_framework/blazor.web.js muss da sein. Ohne dieses Skript lädt
# jede Seite, aber kein Knopf reagiert, denn Blazor kommt nie in Gang; der HEALTHCHECK des Containers merkt davon nichts.
# Jede Prüfung schreibt eine Zeile; das Skript endet mit 1, sobald eine scheitert.

set -eu

BASE_URL="${1:-https://portal.plagevo.de}"
BASE_URL="${BASE_URL%/}"
failed=0

# Ruft eine Adresse ab und vergleicht den HTTP-Status mit 200. Ein Netzfehler zählt als Status 000.
check() {
    path="$1"
    what="$2"
    status="$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 15 "$BASE_URL$path" || true)"
    if [ "$status" = "200" ]; then
        echo "ok      $path ($what)"
    else
        echo "FEHLER  $path ($what): HTTP $status" >&2
        failed=1
    fi
}

check /health "die Web-Variante läuft"
check /_framework/blazor.web.js "das Skript, das die Seiten interaktiv macht"

if [ "$failed" -ne 0 ]; then
    echo "check-web: $BASE_URL ist nicht benutzbar (docs/betrieb.md, Schritt 13)" >&2
    exit 1
fi
echo "check-web: $BASE_URL ist benutzbar"
