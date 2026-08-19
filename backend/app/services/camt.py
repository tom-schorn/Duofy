"""Reading a CAMT account report into plain objects.

No database, no HTTP, no user — a file goes in, a report comes out. That is
deliberate: this is the piece with the highest chance of being wrong, and the
only one that can be checked against a real bank export without setting
anything up. Everything the importer does with the result lives elsewhere.

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


@dataclass(frozen=True)
class _Account:
    """Whose account the report is about — needed to tell the other side apart."""

    iban: str
    owner: str | None


class CamtError(ValueError):
    """The file is not a CAMT report this parser can read.

    A `ValueError`, because that is what a caller handling bad input expects.
    The message names what is missing, not what the user should do — the API
    layer turns it into an error code and the frontend into a sentence.
    """


@dataclass(frozen=True)
class CamtEntry:
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
class CamtReport:
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

    entries: list[CamtEntry]


def read_upload(data: bytes) -> list[CamtReport]:
    """Read what the user uploaded: one XML file, or a ZIP holding several.

    Returns the pages in page order. A ZIP is not assumed to be sorted — banks
    name the files by sequence, but nothing guarantees the archive follows.
    """
    if data[:4] == _ZIP_MAGIC:
        reports = _read_archive(data)
    else:
        reports = [parse_report(data)]

    return sorted(reports, key=lambda report: report.page)


def parse_report(data: bytes) -> CamtReport:
    """Read one CAMT XML file."""
    try:
        root = ElementTree.fromstring(data)
    except ElementTree.ParseError as error:
        raise CamtError(f"not valid XML: {error}") from error

    namespace = _namespace(root)
    tag = _local_name(root.tag)
    if tag != _DOCUMENT:
        raise CamtError(f"root element is <{tag}>, expected <{_DOCUMENT}>")

    report = _container(root, namespace)
    element = _required(report, "Acct", namespace)
    account = _Account(
        iban=_text(element, "Id/IBAN", namespace) or "",
        owner=_text(element, "Ownr/Nm", namespace),
    )
    pagination = report.find(_path("RptPgntn", namespace))

    return CamtReport(
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
) -> CamtEntry | None:
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

    return CamtEntry(
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

    raise CamtError("entry without AcctSvcrRef — cannot be recognised on re-import")


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
            iban = _text(detail, f"RltdPties/{role}Acct/Id/IBAN", namespace)
            if name or iban:
                sides.append((role, name, iban))

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

    raise CamtError(f"report has no {code} balance")


def _date(element: ElementTree.Element, name: str, namespace: str) -> date:
    """A booking or value date.

    The element holds either `Dt` (a day) or `DtTm` (a timestamp). Only the day
    is kept — the plan works in days, and a time would suggest a precision the
    booking does not have.
    """
    text = _text(element, f"{name}/Dt", namespace) or _text(element, f"{name}/DtTm", namespace)
    if not text:
        raise CamtError(f"entry without {name}")

    return date.fromisoformat(text[:10])


def _decimal(text: str | None) -> Decimal:
    """An amount. `Decimal`, never float — see the rule in CLAUDE.md."""
    if not text:
        raise CamtError("amount is missing")

    try:
        return Decimal(text.strip())
    except InvalidOperation as error:
        raise CamtError(f"amount {text!r} is not a number") from error


# ---------------------------------------------------------------------------
# Several files
# ---------------------------------------------------------------------------


def _read_archive(data: bytes) -> list[CamtReport]:
    """Every XML file inside a ZIP.

    Non-XML members are skipped rather than rejected: archives carry the
    occasional readme or checksum file, and refusing the whole upload over one
    of them would be unhelpful.
    """
    try:
        archive = zipfile.ZipFile(io.BytesIO(data))
    except zipfile.BadZipFile as error:
        raise CamtError("archive cannot be read") from error

    reports = [
        parse_report(archive.read(name))
        for name in sorted(archive.namelist())
        if name.lower().endswith(".xml")
    ]
    if not reports:
        raise CamtError("archive holds no XML file")

    return reports


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
        raise CamtError(f"report has no <{path}>")
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
            raise CamtError(f"document holds {len(reports)} reports, expected exactly one")
        return reports[0]

    raise CamtError("document is neither camt.052 nor camt.053")
