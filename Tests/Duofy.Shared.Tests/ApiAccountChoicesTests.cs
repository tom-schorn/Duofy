using Duofy.ApiClient;
using Duofy.Schemas;
using Duofy.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.Shared.Tests;

/// <summary>Die Buchungsseite bekommt die Konten aus dem Konten-Client (Spec 59, R10).</summary>
public class ApiAccountChoicesTests
{
    [Fact]
    public async Task Choices_are_the_accounts_of_the_api_with_default_and_closed_ones()
    {
        var api = new FakeAccountApi();
        var giro = FakeAccountApi.Account("Giro", isDefault: true);
        var old = FakeAccountApi.Account("Altes Konto", active: false);
        api.Accounts.AddRange([giro, old]);

        var result = await new ApiAccountChoices(api).ListAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(
            [new AccountChoice(giro.Id, "Giro", IsDefault: true, Active: true), new AccountChoice(old.Id, "Altes Konto", IsDefault: false, Active: false)],
            result.Value);
    }

    [Fact]
    public async Task An_error_of_the_api_is_passed_on()
    {
        var api = new FailingAccountApi();

        var result = await new ApiAccountChoices(api).ListAsync();

        Assert.Equal(ClientErrorCodes.ServerUnreachable, result.ErrorCode);
    }

    [Fact]
    public void The_shared_ui_uses_the_api_for_the_choices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDuofyUi();
        services.AddDuofyApiClient(new Uri("https://api.test"));
        // Die Token-Ablage meldet sonst der Host an (Web: Sitzung, App: SecureStorage).
        services.AddSingleton<ITokenStore>(new FakeTokenStore());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<ApiAccountChoices>(scope.ServiceProvider.GetRequiredService<IAccountChoices>());
    }

    private sealed class FailingAccountApi : IAccountApi
    {
        public Task<ApiResult<IReadOnlyList<AccountResponse>>> ListAsync(bool activeOnly = false, Guid? owner = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(ApiResult<IReadOnlyList<AccountResponse>>.Failure(ClientErrorCodes.ServerUnreachable));

        public Task<ApiResult<AccountResponse>> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResult<AccountResponse>> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
