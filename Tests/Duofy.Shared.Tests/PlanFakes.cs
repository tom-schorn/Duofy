using Duofy.ApiClient;
using Duofy.Schemas;

namespace Duofy.Shared.Tests;

/// <summary>
/// Eine Plan-API im Speicher: Sie hält je Monat einen Plan, den der Test vorgibt (sonst einen leeren),
/// und merkt sich jeden Aufruf. Abhaken und Löschen ändern den gespeicherten Plan so, wie die echte
/// API es täte, damit die Seite nach dem Neuladen den neuen Stand zeigt.
/// </summary>
public sealed class FakePlanApi : IPlanApi
{
    public Dictionary<(int Year, int Month), PlanResponse> Plans { get; } = [];
    public List<(int Year, int Month)> GetCalls { get; } = [];
    public List<CreatePositionRequest> CreatedPositions { get; } = [];
    public List<(Guid Id, UpdatePositionRequest Request)> UpdatedPositions { get; } = [];
    public List<Guid> DeletedPositions { get; } = [];
    public List<(Guid Id, TickPositionRequest Request)> Ticks { get; } = [];
    public List<Guid> Unticks { get; } = [];
    public List<ApplyPlanChangesRequest> AppliedChanges { get; } = [];
    public List<CreateCarryOverRequest> CarryOvers { get; } = [];
    public List<(int Year, int Month)> DeletedCarryOvers { get; } = [];
    public string? Error { get; set; }

    /// <summary>Der Besitzer jedes Aufrufs, null für den eigenen Plan (Spec 68).</summary>
    public List<Guid?> Owners { get; } = [];

    /// <summary>Der Besitzer jedes schreibenden Aufrufs, getrennt von den Abfragen davor und danach.</summary>
    public List<Guid?> WriteOwners { get; } = [];

    public static PlanResponse Empty(int year, int month) => new(
        Guid.NewGuid(), year, month, [], 0m, 0m, 0m, 0m,
        [new PlanQuotaResponse(PotKeys.Needs, 50, 0m), new PlanQuotaResponse(PotKeys.Wants, 30, 0m), new PlanQuotaResponse(PotKeys.Savings, 20, 0m)],
        [], new PlanCarryOverResponse(0m, false, null, []));

    public static PlanPositionResponse Position(
        string label, decimal planned, string pot = PotKeys.Needs, decimal booked = 0m, bool done = false, int day = 1, Guid? commitmentId = null) =>
        new(Guid.NewGuid(), commitmentId ?? Guid.NewGuid(), label, planned, day,
            pot == PotKeys.Income ? CommitmentTypes.Income : CommitmentTypes.Contract, FakeCategoryApi.Groceries.Id, pot,
            null, null, false, PaymentMethods.Other, booked, done ? DateTimeOffset.UnixEpoch : null, false, null);

    public PlanResponse PlanFor(int year, int month) =>
        Plans.TryGetValue((year, month), out var plan) ? plan : Plans[(year, month)] = Empty(year, month);

    /// <summary>Die Monate, die die Übersicht (Spec 23) vorfindet; null heißt: die Monate der gespeicherten Pläne.</summary>
    public List<PlanMonthResponse>? Months { get; set; }

    public int ListMonthsCalls { get; private set; }

    public Task<ApiResult<IReadOnlyList<PlanMonthResponse>>> ListMonthsAsync(Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        ListMonthsCalls++;
        if (Error is not null)
        {
            return Task.FromResult(ApiResult<IReadOnlyList<PlanMonthResponse>>.Failure(Error));
        }
        IReadOnlyList<PlanMonthResponse> months = Months
            ?? Plans.Keys.Select(k => new PlanMonthResponse(k.Year, k.Month, 0m)).OrderByDescending(m => m.Year).ThenByDescending(m => m.Month).ToList();
        return Task.FromResult(ApiResult<IReadOnlyList<PlanMonthResponse>>.Success(months));
    }

    public Task<ApiResult<PlanResponse>> GetAsync(int year, int month, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        GetCalls.Add((year, month));
        return Task.FromResult(Error is null ? ApiResult<PlanResponse>.Success(PlanFor(year, month)) : ApiResult<PlanResponse>.Failure(Error));
    }

    public Task<ApiResult<PlanPositionResponse>> CreatePositionAsync(int year, int month, CreatePositionRequest request, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        WriteOwners.Add(owner);
        CreatedPositions.Add(request);
        var position = Position(request.Label!, request.Amount, request.Pot!, day: request.DueDay) with { CommitmentId = null };
        var plan = PlanFor(year, month);
        Plans[(year, month)] = plan with { Positions = [.. plan.Positions, position] };
        return Task.FromResult(ApiResult<PlanPositionResponse>.Success(position));
    }

    public Task<ApiResult<PlanPositionResponse>> UpdatePositionAsync(int year, int month, Guid id, UpdatePositionRequest request, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        WriteOwners.Add(owner);
        UpdatedPositions.Add((id, request));
        return Task.FromResult(Change(year, month, id, p => p with { Label = request.Label ?? p.Label, AmountPlanned = request.Amount ?? p.AmountPlanned }));
    }

    /// <summary>Hält das Löschen an, bis der Test die Aufgabe abschließt; so bleibt die Anfrage "unterwegs".</summary>
    public Task? DeleteGate { get; set; }

    public async Task<ApiResult> DeletePositionAsync(int year, int month, Guid id, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        if (DeleteGate is not null)
        {
            await DeleteGate;
        }
        Owners.Add(owner);
        WriteOwners.Add(owner);
        DeletedPositions.Add(id);
        var plan = PlanFor(year, month);
        Plans[(year, month)] = plan with { Positions = plan.Positions.Where(p => p.Id != id).ToList() };
        return ApiResult.Success();
    }

    public Task<ApiResult<PlanPositionResponse>> TickAsync(int year, int month, Guid id, TickPositionRequest request, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        WriteOwners.Add(owner);
        Ticks.Add((id, request));
        return Task.FromResult(Change(year, month, id, p => p with
        {
            DoneAt = DateTimeOffset.UnixEpoch,
            Booked = p.Booked + (request.Amount ?? (p.Booked == 0m ? p.AmountPlanned : 0m)),
        }));
    }

    public Task<ApiResult<PlanPositionResponse>> UntickAsync(int year, int month, Guid id, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        WriteOwners.Add(owner);
        Unticks.Add(id);
        return Task.FromResult(Change(year, month, id, p => p with { DoneAt = null }));
    }

    public Task<ApiResult<PlanResponse>> ApplyChangesAsync(int year, int month, ApplyPlanChangesRequest request, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        WriteOwners.Add(owner);
        AppliedChanges.Add(request);
        var plan = PlanFor(year, month) with { Changes = [] };
        Plans[(year, month)] = plan;
        return Task.FromResult(ApiResult<PlanResponse>.Success(plan));
    }

    public Task<ApiResult<PlanResponse>> CreateCarryOverAsync(int year, int month, CreateCarryOverRequest request, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        WriteOwners.Add(owner);
        CarryOvers.Add(request);
        var plan = PlanFor(year, month);
        plan = plan with { CarryOver = plan.CarryOver with { Exists = true, Amount = request.Amount } };
        Plans[(year, month)] = plan;
        return Task.FromResult(ApiResult<PlanResponse>.Success(plan));
    }

    public Task<ApiResult> DeleteCarryOverAsync(int year, int month, Guid? owner = null, CancellationToken cancellationToken = default)
    {
        Owners.Add(owner);
        WriteOwners.Add(owner);
        DeletedCarryOvers.Add((year, month));
        var plan = PlanFor(year, month);
        Plans[(year, month)] = plan with { CarryOver = plan.CarryOver with { Exists = false, Amount = null } };
        return Task.FromResult(ApiResult.Success());
    }

    private ApiResult<PlanPositionResponse> Change(int year, int month, Guid id, Func<PlanPositionResponse, PlanPositionResponse> change)
    {
        if (Error is not null)
        {
            return ApiResult<PlanPositionResponse>.Failure(Error);
        }
        var plan = PlanFor(year, month);
        var changed = change(plan.Positions.Single(p => p.Id == id));
        Plans[(year, month)] = plan with { Positions = plan.Positions.Select(p => p.Id == id ? changed : p).ToList() };
        return ApiResult<PlanPositionResponse>.Success(changed);
    }
}
