"""What the CAMT parser has to do, written before the parser exists.

Every case here comes from measuring a real quarterly export, not from the
specification — these are the places where an export differs from what one
would assume while reading the schema.

The fixtures are invented: same tags, same codes, same edge cases, made-up
names and test IBANs. Real exports never enter the repository.
"""

from decimal import Decimal
from pathlib import Path

import pytest

from app.services.statements import parse_report, read_upload

FIXTURES = Path(__file__).parent / "fixtures" / "camt"


def load(name: str) -> bytes:
    return (FIXTURES / name).read_bytes()


# --------------------------------------------------------------------------
# The report itself
# --------------------------------------------------------------------------


def test_report_carries_account_and_balances():
    """The account and both balances come from the report, not from the user.

    The balances are the reason to read them: opening plus every booked entry
    has to equal closing, and that is a check the import can run on itself.
    """
    report = parse_report(load("report_page1.xml"))

    assert report.iban == "DE02120300000000202051"
    assert report.currency == "EUR"
    assert report.opening_balance == Decimal("1500.00")
    assert report.closing_balance == Decimal("721.19")


def test_report_knows_its_page():
    """A report can arrive in several files. Page one is not the last one."""
    first = parse_report(load("report_page1.xml"))
    second = parse_report(load("report_page2.xml"))

    assert (first.page, first.last_page) == (1, False)
    assert (second.page, second.last_page) == (2, True)
    assert first.id == second.id


def test_balances_match_the_entries_across_both_pages():
    """The self-check: opening plus all booked entries equals closing.

    Pending entries are not part of it — they are not booked yet, and the
    closing balance does not contain them.
    """
    pages = [parse_report(load("report_page1.xml")), parse_report(load("report_page2.xml"))]
    entries = [entry for page in pages for entry in page.entries]

    moved = sum((entry.amount if entry.incoming else -entry.amount) for entry in entries)
    assert pages[0].opening_balance + moved == pages[0].closing_balance


# --------------------------------------------------------------------------
# The entries
# --------------------------------------------------------------------------


def test_pending_entries_are_left_out():
    """camt.052 may carry pending entries, and those still change.

    Importing one means importing something that is not true yet — and its
    reference is not final either, so the duplicate check would not hold.
    """
    report = parse_report(load("report_page1.xml"))

    assert len(report.entries) == 3
    assert all(entry.external_ref != "9100000000000000003" for entry in report.entries)


def test_amount_has_no_sign_and_direction_is_a_flag():
    """Same shape as `Transaction`, which stores no sign either."""
    report = parse_report(load("report_page1.xml"))
    rent = next(e for e in report.entries if e.external_ref == "9100000000000000001")

    assert rent.amount == Decimal("890.00")
    assert rent.incoming is False

    income = parse_report(load("report_page2.xml")).entries[0]
    assert income.amount == Decimal("255.00")
    assert income.incoming is True


def test_counterparty_comes_from_creditor_or_debtor():
    """Which side holds the other party depends on the direction.

    On an outgoing payment it is `Cdtr`, on an incoming one `Dbtr`. Reading only
    one of them loses half the counterparties — and the counterparty is the key
    the recognition (#63) is built on.
    """
    outgoing = parse_report(load("report_page1.xml")).entries[0]
    assert outgoing.counterparty_name == "Wohnungsgesellschaft Musterstadt"
    assert outgoing.counterparty_iban == "DE02500105170137075030"

    incoming = parse_report(load("report_page2.xml")).entries[0]
    assert incoming.counterparty_name == "Familienkasse Musterstadt"
    assert incoming.counterparty_iban == "DE02100500000054540402"


def test_purpose_falls_back_to_additional_information():
    """`RmtInf/Ustrd` is missing on roughly a third of a real export.

    Card payments in particular carry their text only in `AddtlNtryInf`. A
    parser that reads `Ustrd` alone hands the user empty rows.
    """
    card = next(
        entry
        for entry in parse_report(load("report_page1.xml")).entries
        if entry.external_ref == "9100000000000000002"
    )

    assert card.purpose == "Kartenzahlung 18.08 09:14 Terminal 4471"
    assert card.counterparty_name == "Lebensmittelmarkt Nord"
    assert card.counterparty_iban is None


def test_several_remittance_lines_are_joined():
    """A purpose may be split over several `Ustrd` elements."""
    mobile = parse_report(load("report_page2.xml")).entries[1]

    assert mobile.purpose == "Rechnung 08/2026 Vertrag 4471902"


def test_a_batch_booking_stays_one_entry():
    """One `Ntry` with several `TxDtls` is one movement on the account.

    Splitting it into its parts would break the balance check, because the
    closing balance follows the entry, not its details. The purposes are joined;
    splitting a booking is a separate feature, not the importer's job.
    """
    batch = next(
        entry
        for entry in parse_report(load("report_page1.xml")).entries
        if entry.external_ref == "9100000000000000004"
    )

    assert batch.amount == Decimal("45.00")
    assert "Mitgliedsbeitrag 3. Quartal" in batch.purpose
    assert "Trikotumlage" in batch.purpose


def test_dates_are_read_separately():
    """Booking date and value date are two dates, and they differ in practice."""
    rent = parse_report(load("report_page1.xml")).entries[0]

    assert rent.booked_on.isoformat() == "2026-08-15"
    assert rent.value_on.isoformat() == "2026-08-15"


def test_external_ref_is_unique_within_a_report():
    """`AcctSvcrRef` is what makes duplicate detection exact rather than a guess.

    It was unique on every entry of a real export — that is the whole reason to
    start with CAMT instead of CSV.
    """
    entries = parse_report(load("report_page1.xml")).entries
    refs = [entry.external_ref for entry in entries]

    assert all(refs)
    assert len(set(refs)) == len(refs)


# --------------------------------------------------------------------------
# What the user uploads
# --------------------------------------------------------------------------


def test_a_zip_yields_every_page():
    """Banks ship a multi-page report as one ZIP."""
    reports = read_upload(load("report_both_pages.zip"))

    assert [report.page for report in reports] == [1, 2]
    assert sum(len(report.entries) for report in reports) == 5


def test_a_single_xml_is_accepted_too():
    reports = read_upload(load("report_page1.xml"))

    assert len(reports) == 1
    assert reports[0].page == 1


def test_a_small_csv_is_read_rather_than_refused():
    """This used to be the "not a bank file" case, and is no longer.

    Three columns with a date and an amount are a statement, even without a
    metadata block above them. The test stays as a reminder that adding a reader
    changes what counts as unreadable.
    """
    reports = read_upload(b"Datum;Betrag;Verwendungszweck\n01.08.2026;-12,99;Abo\n")

    assert len(reports[0].entries) == 1
    assert reports[0].entries[0].amount == Decimal("12.99")


def test_counterparty_is_the_side_that_is_not_this_account():
    """Banks do not fill the roles the way the roles are named.

    On a real report every outgoing entry named the other party under `Dbtr`
    and left `Cdtr` out; on a second account both were filled and either could
    be the account holder. Reading the role would have lost the counterparty on
    exactly the entries that matter.

    The rent entry names both sides — the account holder as debtor, the landlord
    as creditor. The batch booking names only a debtor, although money goes out.
    Both have to yield the other party.
    """
    entries = parse_report(load("report_page1.xml")).entries

    rent = next(e for e in entries if e.external_ref == "9100000000000000001")
    assert rent.counterparty_name == "Wohnungsgesellschaft Musterstadt"
    assert rent.counterparty_iban == "DE02500105170137075030"

    batch = next(e for e in entries if e.external_ref == "9100000000000000004")
    assert batch.incoming is False
    assert batch.counterparty_name == "Sportverein Musterstadt"
    assert batch.counterparty_iban == "DE02300209000106531065"


def test_an_archive_that_unpacks_too_far_is_refused():
    """A ZIP bomb: a small archive holding a huge member.

    An archive states each member's uncompressed size in its own directory, so
    the check happens **before** reading — reading is what allocates the memory.
    Without this, a 1 MB upload exhausted the process.
    """
    import io
    import zipfile

    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("bomb.xml", b"\0" * (200 * 1024 * 1024))

    with pytest.raises(ValueError, match="more than"):
        read_upload(buffer.getvalue())


# --------------------------------------------------------------------------
# CSV
# --------------------------------------------------------------------------

CSV_FIXTURES = Path(__file__).parent / "fixtures" / "csv"


def load_csv(name: str = "ing.csv") -> bytes:
    return (CSV_FIXTURES / name).read_bytes()


def test_the_format_is_recognised_from_the_content():
    """Not from the file name — a `.csv` that is really XML happens."""
    reports = read_upload(load_csv())

    assert len(reports) == 1
    assert len(reports[0].entries) == 6


def test_the_account_comes_out_of_the_metadata_block():
    """ING writes eight lines of account details above the table."""
    report = read_upload(load_csv())[0]

    assert report.iban == "DE02120300000000202051"


def test_the_header_is_found_below_the_metadata():
    """The table does not start on line one, and the header is not line one either."""
    entries = read_upload(load_csv())[0].entries

    assert entries[0].counterparty_name == "Mobilfunk Testanbieter"
    assert entries[0].booked_on.isoformat() == "2026-08-16"


def test_the_purpose_is_the_text_and_not_the_kind_of_booking():
    """`Buchungstext` sits before `Verwendungszweck` and holds "Lastschrift".

    Matching the first fitting **column** would file that word as the purpose of
    every direct debit. Priority follows the synonym list instead.
    """
    entries = read_upload(load_csv())[0].entries

    assert entries[0].purpose == "Rechnung 08/2026 Vertrag 4471902"
    assert all(entry.purpose != "Lastschrift" for entry in entries)


def test_umlauts_in_a_column_name():
    """"Auftraggeber/Empfänger" — one bank writes ä, the next writes ae."""
    entries = read_upload(load_csv())[0].entries

    assert all(entry.counterparty_name for entry in entries)


def test_german_numbers_and_dates():
    entries = read_upload(load_csv())[0].entries
    rent = entries[1]

    assert rent.amount == Decimal("890.00")
    assert rent.incoming is False
    assert rent.booked_on.isoformat() == "2026-08-15"

    benefit = entries[4]
    assert benefit.amount == Decimal("255.00")
    assert benefit.incoming is True


def test_the_balances_come_from_the_running_balance():
    """Opening is not stated anywhere — it follows from closing minus the sum."""
    report = read_upload(load_csv())[0]
    moved = sum(
        (entry.amount if entry.incoming else -entry.amount) for entry in report.entries
    )

    assert report.closing_balance == Decimal("721.19")
    assert report.opening_balance + moved == report.closing_balance


def test_two_identical_bookings_on_one_day_stay_apart():
    """The case that makes CSV workable at all.

    A CSV export carries no reference number. Two bookings of the same amount on
    the same day are indistinguishable by date and amount — but they leave the
    account at different running balances, and that is what the built key uses.
    """
    entries = read_upload(load_csv())[0].entries
    kiosk = [entry for entry in entries if entry.amount == Decimal("6.50")]

    assert len(kiosk) == 2
    assert kiosk[0].external_ref != kiosk[1].external_ref


def test_an_empty_purpose_is_none_rather_than_empty():
    entries = read_upload(load_csv())[0].entries

    assert entries[5].purpose is None
    assert entries[5].counterparty_name == "Lebensmittelmarkt Nord"


def test_something_that_is_neither_xml_nor_a_statement():
    with pytest.raises(ValueError):
        read_upload(b"Hallo;Welt\nkein;Kontoauszug\n")
