#!/usr/bin/env bash
# Prüft, ob das Release-Bundle (AAB) der Android-App für Geräte mit 16-KB-Speicherseiten
# taugt (Google-Play-Regel für Apps mit Ziel Android 15 und höher).
#
# Zwei Prüfungen:
#   1. zipalign mit der 16-KB-Prüfung (-P 16) über das ganze Bundle.
#   2. Bei jeder nativen Bibliothek (.so) für 64-Bit steht im ELF-Kopf eine
#      Ausrichtung von mindestens 16384 Byte (so prüft auch Google Play).
#      zipalign allein reicht dafür nicht, weil es komprimierte Einträge nicht prüfen kann.
#
# Aufruf:  scripts/check-16kb-alignment.sh [Pfad-zur-.aab]
# Ohne Pfad wird das neueste Bundle unter Mobile/Duofy/bin/Release gesucht.
# zipalign liegt im Android-SDK (build-tools); gesucht wird in ANDROID_SDK_ROOT,
# ANDROID_HOME und %LOCALAPPDATA%/Android/Sdk.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

aab="${1:-}"
if [[ -z "$aab" ]]; then
    aab="$(find "$root/Mobile/Duofy/bin/Release" -name '*-Signed.aab' -printf '%T@ %p\n' 2>/dev/null | sort -rn | head -n1 | cut -d' ' -f2- || true)"
fi
if [[ -z "$aab" || ! -f "$aab" ]]; then
    echo "FEHLER: Kein Bundle gefunden. Zuerst bauen: dotnet publish Mobile/Duofy -f net10.0-android -c Release" >&2
    exit 2
fi

find_zipalign() {
    local sdk
    for sdk in "${ANDROID_SDK_ROOT:-}" "${ANDROID_HOME:-}" "${LOCALAPPDATA:+$(cygpath -u "$LOCALAPPDATA" 2>/dev/null || echo "$LOCALAPPDATA")/Android/Sdk}"; do
        [[ -n "$sdk" && -d "$sdk/build-tools" ]] || continue
        ls "$sdk"/build-tools/*/zipalign* 2>/dev/null | sort -V | tail -n1 && return 0
    done
    return 1
}

zipalign="$(find_zipalign || true)"
if [[ -z "$zipalign" ]]; then
    echo "FEHLER: zipalign nicht gefunden. Android-SDK-Build-Tools installieren oder ANDROID_SDK_ROOT setzen." >&2
    exit 2
fi

echo "Bundle:   $aab"
echo "zipalign: $zipalign"

echo
echo "1. zipalign -c -P 16"
if "$zipalign" -c -P 16 -v 4 "$aab" > /dev/null; then
    echo "   OK: zipalign meldet das Bundle als ausgerichtet."
else
    echo "   FEHLER: zipalign meldet Einträge, die nicht auf 16 KB ausgerichtet sind." >&2
    exit 1
fi

echo
echo "2. ELF-Ausrichtung der nativen 64-Bit-Bibliotheken"
python="$(command -v python3 || command -v python || true)"
if [[ -z "$python" ]]; then
    echo "   FEHLER: Python wird für die ELF-Prüfung gebraucht." >&2
    exit 2
fi
"$python" -I "$root/scripts/check_elf_alignment.py" "$aab"
