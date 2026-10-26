using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Shared.Tests;

/// <summary>
/// Eine Vertrags-API im Speicher. Sie merkt sich, was die Seite aufgerufen hat, und filtert die
/// beendeten Verträge wie die echte API über <see cref="CommitmentResponse.Running"/>; Fälligkeiten
/// rechnet sie nicht, die geben die Tests vor.
/// </summary>
public sealed class FakeCommitmentApi : ICommitmentApi
{
    public List<CommitmentResponse> Commitments { get; } = [];
    public List<bool> ListCalls { get; } = [];
    public List<CreateCommitmentRequest> Created { get; } = [];
    public List<(Guid Id, UpdateCommitmentRequest Request)> Updated { get; } = [];
    public List<Guid> Ended { get; } = [];
    public string? Error { get; set; }

    /// <summary>Der Besitzer jeder Abfrage der Liste, null für die eigenen Verträge.</summary>
    public List<Guid?> ListOwners { get; } = [];

    public Task<ApiResult<IReadOnlyList<CommitmentResponse>>> ListAsync(bool includeEnded = false, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        ListCalls.Add(includeEnded);
        ListOwners.Add(owner);
        return Task.FromResult(ApiResult<IReadOnlyList<CommitmentResponse>>.Success(
            Commitments.Where(c => includeEnded || c.Running).ToList()));
    }

    public Task<ApiResult<CommitmentResponse>> CreateAsync(CreateCommitmentRequest request, CancellationToken cancellationToken = default)
    {
        Created.Add(request);
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<CommitmentResponse>.Failure(Error));
        }
        var saved = new CommitmentResponse(
            Guid.NewGuid(), request.Type!, request.Name!.Trim(), request.Amount, request.IntervalMonths, request.FirstDueDate!.Value,
            request.LastDueDate, request.CategoryId!.Value, CommitmentTypes.FixedPot(request.Type) ?? request.Pot!, request.AccountId,
            request.CounterAccountId, request.PassThrough ?? false, request.PaymentMethod!, request.TargetAmount, request.TargetDate,
            request.FirstDueDate, true);
        Commitments.Add(saved);
        return Task.FromResult(ApiResult<CommitmentResponse>.Success(saved));
    }

    public Task<ApiResult<CommitmentResponse>> UpdateAsync(Guid id, UpdateCommitmentRequest request, CancellationToken cancellationToken = default)
    {
        Updated.Add((id, request));
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<CommitmentResponse>.Failure(Error));
        }
        var index = Commitments.FindIndex(c => c.Id == id);
        var saved = Commitments[index] with { Name = request.Name ?? Commitments[index].Name };
        Commitments[index] = saved;
        return Task.FromResult(ApiResult<CommitmentResponse>.Success(saved));
    }

    public Task<ApiResult<CommitmentResponse>> EndAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ended.Add(id);
        var index = Commitments.FindIndex(c => c.Id == id);
        var saved = Commitments[index] with { LastDueDate = new DateOnly(2026, 10, 1), NextDueDate = null, Running = false };
        Commitments[index] = saved;
        return Task.FromResult(ApiResult<CommitmentResponse>.Success(saved));
    }

    /// <summary>Ein Vertrag für die Liste; läuft, solange <paramref name="running"/> true ist.</summary>
    public static CommitmentResponse Commitment(
        string name, decimal amount = 890m, int interval = 1, string type = CommitmentTypes.Contract, bool running = true,
        DateOnly? nextDue = null) =>
        new(Guid.NewGuid(), type, name, amount, interval, new DateOnly(2026, 1, 1), running ? null : new DateOnly(2026, 5, 1),
            FakeCategoryApi.Groceries.Id, CommitmentTypes.FixedPot(type) ?? PotKeys.Needs, null, null, false,
            PaymentMethods.StandingOrder, null, null, running ? nextDue ?? new DateOnly(2026, 11, 1) : null, running);
}
