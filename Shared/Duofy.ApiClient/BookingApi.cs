using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>Der Client für /api/v1/bookings, siehe <see cref="IBookingApi"/>.</summary>
internal sealed class BookingApi(ApiConnection api) : IBookingApi
{
    private const string Path = "api/v1/bookings";

    public Task<ApiResult<BookingMonthResponse>> ListMonthAsync(
        int year, int month, Guid? accountId, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        var query = $"?year={year}&month={month}" + (accountId is { } id ? $"&accountId={id}" : "");
        return api.SendAsync<BookingMonthResponse>(HttpMethod.Get, OwnerQuery.With(Path + query, owner), body: null, anonymous: false, cancellationToken);
    }

    public Task<ApiResult<BookingResponse>> CreateAsync(SaveBookingRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<BookingResponse>(HttpMethod.Post, Path, request, anonymous: false, cancellationToken);

    public Task<ApiResult<BookingResponse>> UpdateAsync(Guid id, SaveBookingRequest request, CancellationToken cancellationToken = default) =>
        api.SendAsync<BookingResponse>(HttpMethod.Put, $"{Path}/{id}", request, anonymous: false, cancellationToken);

    public Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.SendAsync(HttpMethod.Delete, $"{Path}/{id}", body: null, anonymous: false, cancellationToken);
}
