"""Reading a bank statement into plain objects — CAMT today, CSV beside it.

The shape is the same for both: a file goes in, a report with entries comes
out. Everything the importer does afterwards — parking, recognising, booking —
never learns which format it came from, and that is the point. FinTS and MT940
would slot in here as further readers without touching anything downstream.

No database, no HTTP, no user. That is deliberate: this is the piece with the
highest chance of being wrong, and the only one that can be checked against a
real bank export without setting anything up.

## CAMT

CAMT is ISO 20022, so the same structure comes out of every bank. What differs
is which optional elements a bank fills, and that is where the surprises are.
The four that a real quarterly export produced:

* **`RmtInf/Ustrd` is often absent.** Card payments carry their text only in
  `AddtlNtryInf`. Reading `Ustrd` alone hands the user empty rows.
* **camt.052 may contain pending entries.** They still change, and their
  reference is not final, so they are dropped rather than imported.
* **A report can span several files**, shipped together in a ZIP. Page number
  and last-page flag say so.
* **One entry may hold several transaction details** — a batch booking. It
  stays one entry here: the closing balance follows the entry, not its parts,
  so splitting it would break the balance check.

Both `camt.052` (interim, "show me the turnover") and `camt.053` (the statement)
are accepted. They differ in two element names and nothing else that matters
here.
"""

import csv
import hashlib
import io
import zipfile
from dataclasses import dataclass
from datetime import date
from decimal import Decimal, InvalidOperation
from xml.etree import ElementTree

#: The whole file is one namespace, and its version is part of the URI. Reading
#: it off the root element rather than hard-coding it means a bank on a newer
#: revision does not need a code change.
_DOCUMENT = "Document"

#: camt.052 calls them report, camt.053 calls them statement. Same shape.
_CONTAINERS = (("BkToCstmrAcctRpt", "Rpt"), ("BkToCstmrStmt", "Stmt"))

#: A ZIP always starts with these four bytes.
_ZIP_MAGIC = b"PK\x03\x04"

#: Upper bounds for what is unpacked out of an archive.
#:
#: A ZIP states the uncompressed size of each member in its own directory, and it
#: can lie about nothing else: reading a member is what allocates the memory. A
#: 1 MB archive of one billion zero bytes exhausts the process, which is why the
#: size is checked **before** reading rather than after.
#:
#: A quarter of turnover on two accounts came to roughly 200 KB of XML, so a
#: yearly statement stays far below these numbers.
_MAX_MEMBER_BYTES = 50 * 1024 * 1024
_MAX_TOTAL_BYTES = 200 * 1024 * 1024


@dataclass(frozen=True)
class _Account:
    """Whose account the report is about — needed to tell the other side apart."""

    iban: str
    owner: str | None


def normalise_iban(value: str | None) -> str:
    """An IBAN reduced to what two of them have to share to be the same one.

    Banks print it grouped ("DE12 3456 …"), export it unspaced, and occasionally
    in lower case. Duofy compares IBANs to decide whether a counterparty is one
    of the user's **own** accounts — a stray space there would silently turn a
    transfer back into an expense, which is exactly the mistake #71 is about.

    So every IBAN is put through here at the edge where it enters: the reader,
    and the account schema. Nothing downstream has to remember.
    """
    if not value:
        return ""
    return "".join(character for character in value if not character.isspace()).upper()


class StatementError(ValueError):
    """The file is not a CAMT report this parser can read.

    A `ValueError`, because that is what a caller handling bad input expects.
    The message names what is missing, not what the user should do — the API
    layer turns it into an error code and the frontend into a sentence.
    """


@dataclass(frozen=True)
class StatementEntry:
    """One movement on the account.

    Shaped like `Transaction` on purpose: **no sign on the amount**, direction
    as a flag. A sign would have to be interpreted twice — once here and once
    when the booking is written — and the two interpretations would drift.
    """

    #: `AcctSvcrRef`, the bank's own identifier. Unique on every entry of a real
    #: export, which is what makes duplicate detection exact instead of a guess
    #: over date, amount and purpose. Goes into `Transaction.external_ref`.
    external_ref: str

    booked_on: date
    #: When the money counts for interest. Differs from the booking date often
    #: enough that keeping only one of them loses information.
    value_on: date

    amount: Decimal
    #: `CdtDbtInd` — money coming in rather than going out.
    incoming: bool

    #: The other side. `Cdtr` on an outgoing payment, `Dbtr` on an incoming one;
    #: reading only one of them loses half of them.
    counterparty_name: str | None
    #: The key the recognition works on, where the bank supplies it. Absent on
    #: most card payments.
    counterparty_iban: str | None

    #: `RmtInf/Ustrd` where present, `AddtlNtryInf` otherwise. Several lines are
    #: joined — a bank splits one sentence across `Ustrd` elements freely.
    purpose: str | None


@dataclass(frozen=True)
class StatementReport:
    """One page of one report, for one account."""

    #: `Rpt/Id`. The pages of a report share it, which is how they are
    #: recognised as belonging together.
    id: str
    iban: str
    currency: str

    #: `OPBD` and `CLBD`. Their point is the self-check: opening plus every
    #: booked entry has to equal closing. If it does not, something was dropped,
    #: and the import should say so rather than quietly book a wrong balance.
    opening_balance: Decimal
    closing_balance: Decimal

    page: int
    last_page: bool

    entries: list[StatementEntry]


def read_upload(data: bytes, *, iban: str = "") -> list[StatementReport]:
    """Read what the user uploaded, whatever shape it arrived in.

    The format is recognised from the content, not from the file name: a
    `.csv` that is really XML happens, and a browser will call anything
    `application/octet-stream` given the chance.

    Returns the pages in page order. A ZIP is not assumed to be sorted — banks
    name the files by sequence, but nothing guarantees the archive follows.
    """
    if data[:4] == _ZIP_MAGIC:
        reports = _read_archive(data)
    elif _looks_like_xml(data):
        reports = [parse_report(data)]
    else:
        reports = [parse_csv(data, iban=normalise_iban(iban))]

    return sorted(reports, key=lambda report: report.page)


def _looks_like_xml(data: bytes) -> bool:
    """Does this start with an XML declaration or a tag?

    Only the first bytes are examined, past any byte-order mark and whitespace.
    A CSV can hold angle brackets in a remittance line, so looking further would
    be worse than looking less.
    """
    head = data[:512].lstrip(b"\xef\xbb\xbf").lstrip()
    return head.startswith(b"<")


def parse_report(data: bytes) -> StatementReport:
    """Read one CAMT XML file."""
    try:
        root = ElementTree.fromstring(data)
    except ElementTree.ParseError as error:
        raise StatementError(f"not valid XML: {error}") from error

    namespace = _namespace(root)
    tag = _local_name(root.tag)
    if tag != _DOCUMENT:
        raise StatementError(f"root element is <{tag}>, expected <{_DOCUMENT}>")

    report = _container(root, namespace)
    element = _required(report, "Acct", namespace)
    account = _Account(
        iban=normalise_iban(_text(element, "Id/IBAN", namespace)),
        owner=_text(element, "Ownr/Nm", namespace),
    )
    pagination = report.find(_path("RptPgntn", namespace))

    return StatementReport(
        id=_text(report, "Id", namespace) or "",
        iban=account.iban,
        currency=_text(element, "Ccy", namespace) or "",
        opening_balance=_balance(report, "OPBD", namespace),
        closing_balance=_balance(report, "CLBD", namespace),
        # Absent means the report is not paginated, so it is page one of one.
        page=int(_text(pagination, "PgNb", namespace) or 1) if pagination is not None else 1,
        last_page=(
            _text(pagination, "LastPgInd", namespace) != "false"
            if pagination is not None
            else True
        ),
        entries=[
            entry
            for booking in report.findall(_path("Ntry", namespace))
            if (entry := _entry(booking, account, namespace)) is not None
        ],
    )


# ---------------------------------------------------------------------------
# Inside one file
# ---------------------------------------------------------------------------


def _entry(
    element: ElementTree.Element, account: _Account, namespace: str
) -> StatementEntry | None:
    """One `Ntry`, or `None` if it is not a booked entry.

    Pending entries are dropped here rather than filtered by the caller: an
    entry that may still change has no business leaving this module, and every
    caller would otherwise have to remember the same rule.
    """
    if _status(element, namespace) != "BOOK":
        return None

    incoming = _text(element, "CdtDbtInd", namespace) == "CRDT"
    details = element.findall(_path("NtryDtls/TxDtls", namespace))
    name, iban = _counterparty(details, account, incoming, namespace)

    return StatementEntry(
        external_ref=_reference(element, details, namespace),
        booked_on=_date(element, "BookgDt", namespace),
        value_on=_date(element, "ValDt", namespace),
        amount=_decimal(_text(element, "Amt", namespace)),
        incoming=incoming,
        counterparty_name=name,
        counterparty_iban=iban,
        purpose=_purpose(element, details, namespace),
    )


def _status(element: ElementTree.Element, namespace: str) -> str | None:
    """`BOOK` or `PDNG`.

    Since camt.052.001.03 the status is a structure with a `Cd` inside; older
    revisions put the code straight into `Sts`. Both are read, because a bank
    decides which revision it ships and the difference is two lines.
    """
    status = element.find(_path("Sts", namespace))
    if status is None:
        # Only camt.053 may leave it out, and there everything is booked.
        return "BOOK"

    code = status.find(_path("Cd", namespace))
    if code is not None:
        return (code.text or "").strip()
    return (status.text or "").strip()


def _reference(
    element: ElementTree.Element,
    details: list[ElementTree.Element],
    namespace: str,
) -> str:
    """The bank's identifier for this entry.

    Normally on the entry itself. Some banks only fill it inside the
    transaction details, so that is checked second — without a reference the
    entry could not be recognised on a second import at all.
    """
    reference = _text(element, "AcctSvcrRef", namespace)
    if reference:
        return reference

    for detail in details:
        reference = _text(detail, "Refs/AcctSvcrRef", namespace)
        if reference:
            return reference

    raise StatementError("entry without AcctSvcrRef — cannot be recognised on re-import")


def _counterparty(
    details: list[ElementTree.Element],
    account: "_Account",
    incoming: bool,
    namespace: str,
) -> tuple[str | None, str | None]:
    """The other side of the payment, as (name, IBAN).

    ## Not "creditor on the way out, debtor on the way in"

    That is what the roles mean, and it is not what banks send. Measured against
    two real reports:

    * On **32 of 32** outgoing entries of one account the other party sat under
      `Dbtr`, and `Cdtr` was absent entirely.
    * On the second account both elements were filled on most entries, and which
      one held the account holder varied within the same file.

    Reading the role would therefore have lost every outgoing counterparty of
    the first account — the exact data the recognition (#63) is built on.

    ## The rule that holds

    Take the side that is **not this account**. Compared by IBAN, which is
    unambiguous; by name only where the bank supplied no IBAN. If neither side
    can be ruled out, fall back to the role — better a guess than nothing.
    """
    sides = []
    for detail in details:
        for role in ("Dbtr", "Cdtr"):
            name = _text(detail, f"RltdPties/{role}/Pty/Nm", namespace)
            iban = normalise_iban(_text(detail, f"RltdPties/{role}Acct/Id/IBAN", namespace))
            if name or iban:
                sides.append((role, name, iban or None))

    def is_own(name: str | None, iban: str | None) -> bool:
        if iban and account.iban:
            return iban == account.iban
        return bool(name and account.owner and name == account.owner)

    for _role, name, iban in sides:
        if not is_own(name, iban):
            return name, iban

    # Every side looks like this account — a transfer between own accounts, or a
    # bank that names nobody else. The role is then all there is to go on.
    wanted = "Dbtr" if incoming else "Cdtr"
    for role, name, iban in sides:
        if role == wanted:
            return name, iban

    return None, None


def _purpose(
    element: ElementTree.Element,
    details: list[ElementTree.Element],
    namespace: str,
) -> str | None:
    """What the payment was for.

    `Ustrd` first, `AddtlNtryInf` as the fallback — a third of a real export had
    no `Ustrd` at all. Several lines and several transaction details are joined
    into one text: a bank splits one sentence across elements at will, and for a
    batch booking the parts together are the description of the whole.
    """
    lines = [
        text
        for detail in details
        for element_text in detail.findall(_path("RmtInf/Ustrd", namespace))
        if (text := (element_text.text or "").strip())
    ]
    if not lines:
        fallback = _text(element, "AddtlNtryInf", namespace)
        return " ".join(fallback.split()) if fallback else None

    return " ".join(" ".join(lines).split())


def _balance(report: ElementTree.Element, code: str, namespace: str) -> Decimal:
    """The opening or closing balance, signed.

    `CdtDbtInd` carries the sign here as well — an overdrawn account reports a
    positive number marked `DBIT`. Ignoring the indicator turns a debt into
    savings.
    """
    for balance in report.findall(_path("Bal", namespace)):
        if _text(balance, "Tp/CdOrPrtry/Cd", namespace) != code:
            continue

        amount = _decimal(_text(balance, "Amt", namespace))
        return amount if _text(balance, "CdtDbtInd", namespace) == "CRDT" else -amount

    raise StatementError(f"report has no {code} balance")


def _date(element: ElementTree.Element, name: str, namespace: str) -> date:
    """A booking or value date.

    The element holds either `Dt` (a day) or `DtTm` (a timestamp). Only the day
    is kept — the plan works in days, and a time would suggest a precision the
    booking does not have.
    """
    text = _text(element, f"{name}/Dt", namespace) or _text(element, f"{name}/DtTm", namespace)
    if not text:
        raise StatementError(f"entry without {name}")

    return date.fromisoformat(text[:10])


def _decimal(text: str | None) -> Decimal:
    """An amount. `Decimal`, never float — see the rule in CLAUDE.md."""
    if not text:
        raise StatementError("amount is missing")

    try:
        return Decimal(text.strip())
    except InvalidOperation as error:
        raise StatementError(f"amount {text!r} is not a number") from error


# ---------------------------------------------------------------------------
# Several files
# ---------------------------------------------------------------------------


def _read_archive(data: bytes) -> list[StatementReport]:
    """Every XML file inside a ZIP.

    Non-XML members are skipped rather than rejected: archives carry the
    occasional readme or checksum file, and refusing the whole upload over one
    of them would be unhelpful.
    """
    try:
        archive = zipfile.ZipFile(io.BytesIO(data))
    except zipfile.BadZipFile as error:
        raise StatementError("archive cannot be read") from error

    reports = []
    unpacked = 0

    for member in sorted(archive.infolist(), key=lambda entry: entry.filename):
        if not member.filename.lower().endswith(".xml"):
            continue

        if member.file_size > _MAX_MEMBER_BYTES:
            raise StatementError(
                f"{member.filename} unpacks to {member.file_size} bytes, "
                f"more than the {_MAX_MEMBER_BYTES} allowed"
            )

        unpacked += member.file_size
        if unpacked > _MAX_TOTAL_BYTES:
            raise StatementError(f"archive unpacks to more than {_MAX_TOTAL_BYTES} bytes")

        reports.append(parse_report(archive.read(member)))

    if not reports:
        raise StatementError("archive holds no XML file")

    return reports


# ---------------------------------------------------------------------------
# CSV
#
# Nothing about CSV is standardised — that is what CAMT exists for. But German
# banks all draw on the same vocabulary, handed down from DTAUS and MT940, so
# the columns can be matched **by name** instead of by position. One reader with
# a list of synonyms covers far more than one parser per bank, and it does not
# break the next time a bank adds a column.
# ---------------------------------------------------------------------------

#: What a column may be called. First match wins, compared case-insensitively
#: with everything but letters removed — "Auftraggeber/Empfänger" and
#: "Beguenstigter / Zahlungspflichtiger" differ only in punctuation.
_COLUMNS: dict[str, tuple[str, ...]] = {
    "booked_on": ("buchung", "buchungstag", "buchungsdatum", "datum"),
    "value_on": ("wertstellung", "wertstellungsdatum", "valuta", "valutadatum"),
    "counterparty": (
        "auftraggeberempfaenger",
        "empfaenger",
        "beguenstigterzahlungspflichtiger",
        "namezahlungsbeteiligter",
        "zahlungsbeteiligter",
    ),
    "purpose": ("verwendungszweck", "buchungstext", "vorgangverwendungszweck"),
    "amount": ("betrag", "umsatz", "betrageur"),
    "balance": ("saldo", "kontostand"),
    "iban": ("iban", "kontonummeriban", "ibanzahlungsbeteiligter"),
}

#: Encodings to try, in order. German bank exports are rarely UTF-8; cp1252 is
#: the usual one and latin-1 never fails, which makes it the last resort rather
#: than a choice.
_ENCODINGS = ("utf-8-sig", "cp1252", "latin-1")


#: Umlauts, because a bank writes "Empfänger" and the next one "Empfaenger".
_UMLAUTS = str.maketrans({"ä": "ae", "ö": "oe", "ü": "ue", "ß": "ss"})


def _key(name: str) -> str:
    """A column name reduced to plain letters, for comparing."""
    return "".join(c for c in name.lower().translate(_UMLAUTS) if c.isalpha())


def _decode(data: bytes) -> str:
    for encoding in _ENCODINGS:
        try:
            return data.decode(encoding)
        except UnicodeDecodeError:
            continue
    raise StatementError("the file uses an encoding this import cannot read")


def _find_header(rows: list[list[str]]) -> int:
    """The row the table starts on.

    Banks put a block of metadata above it — ING writes eight lines of account
    details first. The header is the first row that names at least a date and an
    amount, which is a stronger signal than counting separators: the metadata
    block has rows with two fields, and so would a badly split table.
    """
    for index, row in enumerate(rows[:40]):
        found = {
            field
            for field, names in _COLUMNS.items()
            for cell in row
            if _key(cell) in names
        }
        if "amount" in found and ("booked_on" in found or "value_on" in found):
            return index

    raise StatementError("no table header found — is this a statement export?")


def _decimal_de(text: str) -> Decimal:
    """`1.234,56` into a `Decimal`. Never a float, see the rule in CLAUDE.md."""
    cleaned = text.strip().replace(".", "").replace(",", ".").replace("+", "")
    if not cleaned:
        raise StatementError("empty amount")
    try:
        return Decimal(cleaned)
    except InvalidOperation as error:
        raise StatementError(f"amount {text!r} is not a number") from error


def _date_de(text: str) -> date:
    """`14.08.2026`, and the two-digit year some exports still write."""
    parts = text.strip().split(".")
    if len(parts) != 3:
        raise StatementError(f"date {text!r} is not in day.month.year form")

    day, month, year = (int(part) for part in parts)
    if year < 100:
        year += 2000
    return date(year, month, day)


def parse_csv(data: bytes, *, iban: str = "") -> StatementReport:
    """Read a CSV export.

    `iban` comes from the caller where the file itself does not say — some
    exports name the account only in the file name. ING writes it into the
    metadata block, and that wins.
    """
    text = _decode(data)
    dialect_delimiter = ";" if text.count(";") >= text.count(",") else ","
    rows = [
        row
        for row in csv.reader(io.StringIO(text), delimiter=dialect_delimiter)
        if any(cell.strip() for cell in row)
    ]
    if not rows:
        raise StatementError("the file is empty")

    header_at = _find_header(rows)
    header = rows[header_at]

    # Priority follows the **synonym list**, not the column order. ING places
    # "Buchungstext" — which holds the kind of booking, not its text — before
    # "Verwendungszweck"; taking the first matching column would file the word
    # "Lastschrift" as the purpose of every direct debit.
    keys = [_key(cell) for cell in header]
    index_of: dict[str, int] = {}
    for field, names in _COLUMNS.items():
        for name in names:
            if name in keys:
                index_of[field] = keys.index(name)
                break

    account_iban = _iban_from_metadata(rows[:header_at]) or iban

    entries: list[StatementEntry] = []
    for row in rows[header_at + 1 :]:
        if len(row) < len(header):
            continue
        entry = _csv_entry(row, index_of, account_iban)
        if entry is not None:
            entries.append(entry)

    if not entries:
        raise StatementError("the table holds no bookings")

    balances = [
        _decimal_de(row[index_of["balance"]])
        for row in rows[header_at + 1 :]
        if "balance" in index_of and len(row) >= len(header) and row[index_of["balance"]].strip()
    ]
    moved = sum(entry.amount if entry.incoming else -entry.amount for entry in entries)

    # The running balance turns "closing" into something known: the last row of
    # the table is where the account stood. Opening follows by subtraction, and
    # with both the balance check works exactly as it does for CAMT.
    closing = balances[0] if balances else Decimal("0.00")
    opening = closing - moved if balances else Decimal("0.00")

    return StatementReport(
        id="",
        iban=account_iban,
        currency="EUR",
        opening_balance=opening,
        closing_balance=closing,
        page=1,
        last_page=True,
        entries=entries,
    )


def _iban_from_metadata(rows: list[list[str]]) -> str:
    """The account IBAN out of the block above the table, if it is there."""
    for row in rows:
        for position, cell in enumerate(row):
            if _key(cell) == "iban" and position + 1 < len(row):
                return normalise_iban(row[position + 1])
    return ""


def _csv_entry(
    row: list[str], index_of: dict[str, int], account_iban: str
) -> StatementEntry | None:
    """One table row, or `None` where it carries no amount."""
    if "amount" not in index_of:
        raise StatementError("the table has no amount column")

    raw_amount = row[index_of["amount"]].strip()
    if not raw_amount:
        return None

    signed = _decimal_de(raw_amount)
    booked = row[index_of["booked_on"]] if "booked_on" in index_of else ""
    valued = row[index_of["value_on"]] if "value_on" in index_of else ""
    booked_on = _date_de(booked or valued)

    def cell(field: str) -> str | None:
        if field not in index_of:
            return None
        value = " ".join(row[index_of[field]].split())
        return value or None

    counterparty_iban = normalise_iban(cell("iban")) or None
    if counterparty_iban and counterparty_iban == account_iban:
        counterparty_iban = None

    return StatementEntry(
        # No reference number anywhere in a CSV export, so one is built. The
        # running balance is what makes it dependable: two identical bookings on
        # the same day still leave the account at different intermediate
        # balances. See `StatementEntry.external_ref`.
        external_ref=_csv_reference(row, index_of, booked_on, signed),
        booked_on=booked_on,
        value_on=_date_de(valued) if valued else booked_on,
        amount=abs(signed),
        incoming=signed > 0,
        counterparty_name=cell("counterparty"),
        counterparty_iban=counterparty_iban,
        purpose=cell("purpose"),
    )


def _csv_reference(
    row: list[str], index_of: dict[str, int], booked_on: date, signed: Decimal
) -> str:
    """A stand-in for the reference number a CSV export does not have.

    Date, amount and the **running balance** — the last of which is what makes
    it work. Two identical bookings on one day are indistinguishable by date and
    amount alone, but they leave the account at different balances.

    Prefixed so it cannot collide with a bank's own reference: importing the
    same account once as CAMT and once as CSV produces two different keys for
    one booking, and pretending otherwise would hide that rather than fix it.
    """
    parts = [booked_on.isoformat(), f"{signed:f}"]
    if "balance" in index_of:
        parts.append(row[index_of["balance"]].strip())

    digest = hashlib.sha256("|".join(parts).encode()).hexdigest()[:24]
    return f"csv:{digest}"


# ---------------------------------------------------------------------------
# Namespace handling
#
# ElementTree spells every tag `{namespace}name`, and the namespace carries the
# CAMT version. These four helpers keep that out of the code above.
# ---------------------------------------------------------------------------


def _namespace(root: ElementTree.Element) -> str:
    return root.tag[1:].split("}")[0] if root.tag.startswith("{") else ""


def _local_name(tag: str) -> str:
    return tag.split("}")[-1]


def _path(path: str, namespace: str) -> str:
    """`Acct/Id/IBAN` into the form ElementTree expects."""
    if not namespace:
        return path
    return "/".join(f"{{{namespace}}}{step}" for step in path.split("/"))


def _text(element: ElementTree.Element | None, path: str, namespace: str) -> str | None:
    if element is None:
        return None

    found = element.find(_path(path, namespace))
    if found is None or found.text is None:
        return None

    text = found.text.strip()
    return text or None


def _required(element: ElementTree.Element, path: str, namespace: str) -> ElementTree.Element:
    found = element.find(_path(path, namespace))
    if found is None:
        raise StatementError(f"report has no <{path}>")
    return found


def _container(root: ElementTree.Element, namespace: str) -> ElementTree.Element:
    """The single report or statement inside the document.

    A CAMT document may legally carry several. Duofy reads one file as one
    report, so a second one would be silently ignored — better to say so.
    """
    for outer, inner in _CONTAINERS:
        wrapper = root.find(_path(outer, namespace))
        if wrapper is None:
            continue

        reports = wrapper.findall(_path(inner, namespace))
        if len(reports) != 1:
            raise StatementError(f"document holds {len(reports)} reports, expected exactly one")
        return reports[0]

    raise StatementError("document is neither camt.052 nor camt.053")
