"""Prüft die ELF-Ausrichtung der 64-Bit-Bibliotheken (.so) in einem Android-Bundle.

Google Play verlangt für Geräte mit 16-KB-Speicherseiten, dass jedes LOAD-Segment
einer 64-Bit-Bibliothek auf mindestens 16384 Byte ausgerichtet ist (p_align).
Aufruf: python check_elf_alignment.py <datei.aab|datei.apk>
"""
import struct
import sys
import zipfile

REQUIRED = 16384  # 16 KB
PT_LOAD = 1
ELF_MAGIC = b"\x7fELF"
ELFCLASS64 = 2


def load_alignments(data: bytes) -> list[int]:
    """Liefert p_align aller LOAD-Segmente einer 64-Bit-ELF-Datei (little endian)."""
    if data[:4] != ELF_MAGIC or data[4] != ELFCLASS64:
        raise ValueError("keine 64-Bit-ELF-Datei")
    e_phoff = struct.unpack_from("<Q", data, 0x20)[0]
    e_phentsize, e_phnum = struct.unpack_from("<HH", data, 0x36)
    result = []
    for i in range(e_phnum):
        offset = e_phoff + i * e_phentsize
        p_type = struct.unpack_from("<I", data, offset)[0]
        if p_type == PT_LOAD:
            result.append(struct.unpack_from("<Q", data, offset + 0x30)[0])
    return result


def main(path: str) -> int:
    failed = []
    checked = 0
    with zipfile.ZipFile(path) as archive:
        for name in archive.namelist():
            if not name.endswith(".so"):
                continue
            data = archive.read(name)
            try:
                alignments = load_alignments(data)
            except ValueError:
                # 32-Bit-Bibliotheken sind von der Regel nicht betroffen.
                print(f"   übersprungen (kein 64-Bit-ELF): {name}")
                continue
            checked += 1
            if alignments and min(alignments) >= REQUIRED:
                print(f"   OK  {name} (Ausrichtung {min(alignments)})")
            else:
                print(f"   FEHLER {name} (Ausrichtung {min(alignments) if alignments else 'unbekannt'})", file=sys.stderr)
                failed.append(name)
    if checked == 0:
        print("   FEHLER: keine 64-Bit-Bibliotheken gefunden", file=sys.stderr)
        return 1
    if failed:
        print(f"   {len(failed)} von {checked} Bibliotheken sind nicht auf 16 KB ausgerichtet.", file=sys.stderr)
        return 1
    print(f"   Alle {checked} Bibliotheken sind auf 16 KB ausgerichtet.")
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1]))
