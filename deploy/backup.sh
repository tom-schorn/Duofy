#!/bin/sh
# Die tägliche Sicherung der Datenbank (Spec 79, R9). Läuft im Dienst "backup" von docker-compose.yml.
#
# Jeden Tag um BACKUP_TIME (Ortszeit, Standard 03:00) schreibt pg_dump die ganze Datenbank als SQL, gepackt, nach
# /backups/duofy-JJJJ-MM-TT.sql.gz; ein zweiter Lauf am selben Tag ersetzt die Datei des Tages. Danach löscht das
# Skript die Sicherungen, die älter als BACKUP_KEEP_DAYS Tage sind (Standard 14), nach dem Datum im Namen.
# Beim Start sichert es sofort, wenn es für heute noch keine Sicherung gibt.
#
#   backup.sh          läuft dauerhaft (so startet es der Dienst)
#   backup.sh once     sichert einmal und räumt auf, zum Beispiel: docker compose run --rm backup once
#   backup.sh check    zeigt, wohin die Sicherung verbindet (ohne Passwort), und prüft, ob die Datenbank antwortet
#
# Die Verbindung kommt aus DUOFY_CONNECTION_STRING, derselben Verbindung wie die der API (ConnectionStrings__Duofy,
# Schreibweise von Npgsql: Host=...;Port=...;Database=...;Username=...;Password=...;SSL Mode=...). Ein Wert darf kein
# Semikolon enthalten. Ohne sie gelten PGHOST, PGPORT, PGDATABASE, PGUSER und PGPASSWORD direkt. Fehler stehen im Log
# (docker compose logs backup); das Passwort nie.

set -eu
# Scheitert pg_dump, soll die ganze Kette scheitern, nicht nur gzip zählen.
set -o pipefail
# Die Sicherungen enthalten Passwort-Hashes und persönliche Daten: nur für den Besitzer lesbar.
umask 077

BACKUP_DIR="${BACKUP_DIR:-/backups}"
BACKUP_KEEP_DAYS="${BACKUP_KEEP_DAYS:-14}"
BACKUP_TIME="${BACKUP_TIME:-03:00}"

log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') backup: $*"
}

# Übersetzt die Verbindung der API in die Variablen von pg_dump. Unbekannte Schlüssel bleiben unbeachtet.
use_connection_string() {
    rest="$1;"
    while [ -n "$rest" ]; do
        part="${rest%%;*}"
        rest="${rest#*;}"
        case "$part" in *=*) ;; *) continue ;; esac
        key="$(printf '%s' "${part%%=*}" | tr -d ' ' | tr '[:upper:]' '[:lower:]')"
        value="${part#*=}"
        case "$key" in
            host|server) export PGHOST="$value" ;;
            port) export PGPORT="$value" ;;
            database|db) export PGDATABASE="$value" ;;
            username|userid|user|uid) export PGUSER="$value" ;;
            password|pwd) export PGPASSWORD="$value" ;;
            sslmode) export PGSSLMODE="$(printf '%s' "$value" | tr '[:upper:]' '[:lower:]')" ;;
        esac
    done
}

if [ -n "${DUOFY_CONNECTION_STRING:-}" ]; then
    use_connection_string "$DUOFY_CONNECTION_STRING"
fi

# Eine Sicherung; erst unter einem vorläufigen Namen, damit nie eine halbe Datei als fertig gilt.
backup() {
    file="$BACKUP_DIR/duofy-$(date +%F).sql.gz"
    partial="$file.partial"
    if pg_dump --no-owner --no-privileges | gzip > "$partial"; then
        mv "$partial" "$file"
        log "gesichert: $file ($(wc -c < "$file") Bytes)"
    else
        rm -f "$partial"
        log "FEHLER: pg_dump ist gescheitert, keine Sicherung für heute"
        return 1
    fi
}

# Löscht die Sicherungen, deren Datum im Namen vor dem Stichtag liegt. Mit 14 bleiben heute und die 13 Tage davor.
prune() {
    cutoff="$(date -d "@$(( $(date +%s) - (BACKUP_KEEP_DAYS - 1) * 86400 ))" +%F)"
    for old in "$BACKUP_DIR"/duofy-*.sql.gz; do
        [ -e "$old" ] || continue
        day="$(basename "$old" .sql.gz)"
        day="${day#duofy-}"
        if [ "$day" \< "$cutoff" ]; then
            rm -f "$old"
            log "gelöscht (älter als $BACKUP_KEEP_DAYS Tage): $old"
        fi
    done
}

# Sekunden bis zum nächsten BACKUP_TIME.
seconds_until_next_run() {
    now="$(date +%s)"
    next="$(date -d "$(date +%F) $BACKUP_TIME" +%s)"
    if [ "$next" -le "$now" ]; then
        next=$(( next + 86400 ))
    fi
    echo $(( next - now ))
}

run_once() {
    backup || return 1
    prune
}

if [ "${1:-}" = "check" ]; then
    log "Verbindung: Host ${PGHOST:-?}, Port ${PGPORT:-5432}, Datenbank ${PGDATABASE:-?}, Benutzer ${PGUSER:-?}, SSL ${PGSSLMODE:-prefer}"
    pg_isready --timeout=10
    exit $?
fi

if [ "${1:-}" = "once" ]; then
    run_once
    exit $?
fi

log "gestartet: täglich um $BACKUP_TIME, $BACKUP_KEEP_DAYS Tage aufbewahrt, nach $BACKUP_DIR"
if [ ! -e "$BACKUP_DIR/duofy-$(date +%F).sql.gz" ]; then
    run_once || true
fi
while true; do
    sleep "$(seconds_until_next_run)"
    # Ein gescheiterter Lauf beendet den Dienst nicht; der nächste Tag versucht es wieder.
    run_once || true
done
