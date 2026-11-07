using Duofy.Schemas;
using Duofy.Shared.Resources;
using Microsoft.Extensions.Localization;

namespace Duofy.Shared.Services;

/// <summary>Eine Buchung, soweit sie zu einem Posten gehört: Betrag ist der dieser Zeilen, nicht der ganzen Buchung.</summary>
public sealed record PositionBookingRow(Guid BookingId, DateOnly Date, decimal Amount, string Purpose);

/// <summary>Sagt, wofür eine Buchung war (Spec 51, R8, R10), überall gleich benannt.</summary>
public static class BookingPurpose
{
    /// <summary>
    /// Der Text einer Zeile: der Posten, sonst die Kategorie, sonst die Notiz, sonst das Wort der Art; eine aufgeteilte
    /// Buchung sagt das dazu. Eine Umbuchung ohne Posten nennt ihre Konten.
    /// </summary>
    public static string For(
        BookingResponse booking,
        IReadOnlyList<PlanPositionResponse> positions,
        Func<Guid, string?> categoryName,
        Func<Guid, string?> accountName,
        IStringLocalizer<SharedResources> localizer)
    {
        var first = booking.Lines.FirstOrDefault();
        var position = first?.PositionId is { } id ? positions.FirstOrDefault(p => p.Id == id)?.Label : null;
        if (booking.Kind == BookingKinds.Transfer && position is null)
        {
            return localizer["Bookings_Transfer", accountName(booking.AccountId) ?? "", booking.CounterAccountId is { } to ? accountName(to) ?? "" : ""];
        }

        var name = position
            ?? (first?.CategoryId is { } category ? categoryName(category) : null)
            ?? booking.Note
            ?? localizer[$"BookingKind_{booking.Kind}"].Value;
        return booking.Lines.Count > 1 ? $"{name} · {localizer["Bookings_Split"]}" : name;
    }

    /// <summary>Die Buchungen eines Postens: je Buchung die Summe ihrer Zeilen auf diesem Posten, in der Reihenfolge der API.</summary>
    public static IReadOnlyList<PositionBookingRow> LinesOf(
        IEnumerable<BookingResponse> bookings,
        Guid positionId,
        Func<Guid, string?> categoryName,
        IStringLocalizer<SharedResources> localizer) =>
        bookings
            .Select(b => (Booking: b, Lines: b.Lines.Where(l => l.PositionId == positionId).ToList()))
            .Where(x => x.Lines.Count > 0)
            .Select(x => new PositionBookingRow(
                x.Booking.Id,
                x.Booking.Date,
                x.Lines.Sum(l => l.Amount),
                (x.Lines[0].CategoryId is { } category ? categoryName(category) : null)
                    ?? x.Booking.Note
                    ?? localizer[$"BookingKind_{x.Booking.Kind}"].Value))
            .ToList();

    /// <summary>
    /// Die Buchungen ohne Posten in einem Topf (Issue #94, R8): je Buchung die Summe ihrer Zeilen ohne Posten in diesem Topf, in der
    /// Reihenfolge der API. Dieselbe Regel wie <see cref="PlanView.Unplanned"/>, darum ergibt die Summe dieselbe Zahl; Umbuchungen
    /// sind nie ungeplant und fehlen hier.
    /// </summary>
    public static IReadOnlyList<PositionBookingRow> UnplannedLinesOf(
        IEnumerable<BookingResponse> bookings,
        string pot,
        Func<Guid, string?> categoryName,
        IStringLocalizer<SharedResources> localizer) =>
        bookings
            .Where(b => b.Kind != BookingKinds.Transfer)
            .Select(b => (Booking: b, Lines: b.Lines.Where(l => l.PositionId is null && l.Pot == pot).ToList()))
            .Where(x => x.Lines.Count > 0)
            .Select(x => new PositionBookingRow(
                x.Booking.Id,
                x.Booking.Date,
                x.Lines.Sum(l => l.Amount),
                (x.Lines[0].CategoryId is { } category ? categoryName(category) : null)
                    ?? x.Booking.Note
                    ?? localizer[$"BookingKind_{x.Booking.Kind}"].Value))
            .ToList();

    /// <summary>Dasselbe im Haushaltsplan: mit dem Vornamen der Person, ohne Notiz.</summary>
    public static IReadOnlyList<PositionBookingRow> LinesOf(IEnumerable<HouseholdBookingResponse> bookings, Guid positionId) =>
        bookings
            .Select(b => (Booking: b, Lines: (b.Lines ?? []).Where(l => l.PositionId == positionId).ToList()))
            .Where(x => x.Lines.Count > 0)
            .Select(x => new PositionBookingRow(
                x.Booking.Id,
                x.Booking.Date,
                x.Lines.Sum(l => l.Amount),
                string.Join(" · ", new[] { x.Lines[0].Category, x.Booking.OwnerFirstName }.OfType<string>())))
            .ToList();
}
