using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Buchungen über die Duofy-API (/api/v1/bookings). Ohne Anmeldung ist der Fehlercode
/// <see cref="ClientErrorCodes.NotSignedIn"/>; eine unbekannte oder fremde Kennung ergibt
/// <see cref="ClientErrorCodes.NotFound"/>.
/// </summary>
public interface IBookingApi
{
    /// <summary>
    /// Liefert die Buchungen eines Monats, die neueste zuerst, mit den Summen für Einnahmen und Ausgaben.
    /// Mit <paramref name="accountId"/> nur die Buchungen, die dieses Konto berühren; mit <paramref name="owner"/>
    /// die Buchungen dieses Mitglieds, wenn es sie freigegeben hat (Spec 68).
    /// </summary>
    Task<ApiResult<BookingMonthResponse>> ListMonthAsync(
        int year, int month, Guid? accountId, Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Legt eine Buchung an. Verletzt sie eine Regel, ist der Fehlercode der Code der API, zum Beispiel <c>lines_sum_mismatch</c>.</summary>
    Task<ApiResult<BookingResponse>> CreateAsync(SaveBookingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ändert eine Buchung; es gelten dieselben Regeln wie beim Anlegen.</summary>
    Task<ApiResult<BookingResponse>> UpdateAsync(Guid id, SaveBookingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Löscht eine Buchung. Die Rückfrage "Buchung löschen?" stellt die Oberfläche vorher.</summary>
    Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
