using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Die Prüfung und Übernahme einer Serveradresse (R2 bis R4, R6, R7).</summary>
public class ServerSelectorTests
{
    private static readonly Uri Hosted = new("https://api.duofy.invalid/");

    private sealed class Rig : IDisposable
    {
        public FakeApi Api { get; } = new();
        public InMemoryTokenStore Tokens { get; } = new();
        public ServerStore Store { get; } = new(Hosted, new InMemoryServerPreference());
        private readonly ServiceProvider provider;
        private readonly IServiceScope scope;

        public Rig(bool allowHttp = false, TimeSpan? timeout = null, HttpMessageHandler? handler = null)
        {
            Api.Respond = request => request.Path.EndsWith("/api/v1/instance", StringComparison.Ordinal)
                ? FakeApi.Json(HttpStatusCode.OK, new InstanceResponse(["password"]))
                : new HttpResponseMessage(HttpStatusCode.OK);
            provider = TestHost.Create(handler ?? Api, Tokens, Store, new ServerSelectorOptions
            {
                AllowHttp = allowHttp,
                CheckTimeout = timeout ?? TimeSpan.FromSeconds(10),
            });
            scope = provider.CreateScope();
            Selector = scope.ServiceProvider.GetRequiredService<IServerSelector>();
        }

        public IServerSelector Selector { get; }

        public void Dispose()
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    // A2
    [Fact]
    public async Task An_address_whose_health_check_answers_200_is_taken()
    {
        using var rig = new Rig();

        var outcome = await rig.Selector.TryUseAsync("https://mein.server.test/duofy");

        Assert.Equal(ServerChoiceOutcome.Taken, outcome);
        Assert.Equal(new Uri("https://mein.server.test/duofy/"), rig.Store.CurrentAddress);
        Assert.Contains(rig.Api.Requests, r => r.Path == "/duofy/api/v1/health");
    }

    [Fact]
    public async Task A_missing_trailing_slash_is_added_and_query_and_fragment_are_dropped()
    {
        using var rig = new Rig();

        await rig.Selector.TryUseAsync("  https://mein.server.test?x=1#top  ");

        Assert.Equal(new Uri("https://mein.server.test/"), rig.Store.CurrentAddress);
    }

    // A3
    [Fact]
    public async Task An_address_that_answers_404_is_refused_and_the_old_server_stays()
    {
        using var rig = new Rig();
        rig.Api.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        var outcome = await rig.Selector.TryUseAsync("https://mein.server.test/");

        Assert.Equal(ServerChoiceOutcome.NoServer, outcome);
        Assert.True(rig.Store.IsHosted);
    }

    // A3
    [Fact]
    public async Task An_address_that_does_not_answer_in_time_is_refused_and_the_old_server_stays()
    {
        using var rig = new Rig(timeout: TimeSpan.FromMilliseconds(50), handler: new NeverAnswers());

        var outcome = await rig.Selector.TryUseAsync("https://mein.server.test/");

        Assert.Equal(ServerChoiceOutcome.NoServer, outcome);
        Assert.True(rig.Store.IsHosted);
    }

    [Fact]
    public async Task An_address_that_cannot_be_reached_is_refused()
    {
        using var rig = new Rig(handler: new Unreachable());

        var outcome = await rig.Selector.TryUseAsync("https://mein.server.test/");

        Assert.Equal(ServerChoiceOutcome.NoServer, outcome);
    }

    // A4, Release
    [Theory]
    [InlineData("http://192.168.2.67:5080")]
    [InlineData("mein.server.test")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://mein.server.test")]
    public async Task Without_https_the_answer_is_needs_https_and_nothing_is_checked(string input)
    {
        using var rig = new Rig(allowHttp: false);

        var outcome = await rig.Selector.TryUseAsync(input);

        Assert.Equal(ServerChoiceOutcome.NeedsHttps, outcome);
        Assert.Empty(rig.Api.Requests);
        Assert.True(rig.Store.IsHosted);
    }

    // A4, Debug
    [Fact]
    public async Task In_debug_builds_an_http_address_is_checked_and_taken()
    {
        using var rig = new Rig(allowHttp: true);

        var outcome = await rig.Selector.TryUseAsync("http://192.168.2.67:5080");

        Assert.Equal(ServerChoiceOutcome.Taken, outcome);
        Assert.Contains(rig.Api.Requests, r => r.Path == "/api/v1/health");
        Assert.Equal(new Uri("http://192.168.2.67:5080/"), rig.Store.CurrentAddress);
    }

    // A7
    [Fact]
    public async Task Taking_a_different_server_removes_the_stored_tokens()
    {
        using var rig = new Rig();
        rig.Tokens.Tokens = new AuthTokens("access", "refresh");

        await rig.Selector.TryUseAsync("https://mein.server.test/");

        Assert.Null(rig.Tokens.Tokens);
    }

    [Fact]
    public async Task A_refused_address_keeps_the_stored_tokens()
    {
        using var rig = new Rig();
        rig.Api.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        rig.Tokens.Tokens = new AuthTokens("access", "refresh");

        await rig.Selector.TryUseAsync("https://mein.server.test/");

        Assert.NotNull(rig.Tokens.Tokens);
    }

    [Fact]
    public async Task Taking_the_same_server_again_keeps_the_stored_tokens()
    {
        using var rig = new Rig();
        await rig.Selector.TryUseAsync("https://mein.server.test/");
        rig.Tokens.Tokens = new AuthTokens("access", "refresh");

        await rig.Selector.TryUseAsync("https://mein.server.test");

        Assert.NotNull(rig.Tokens.Tokens);
    }

    [Fact]
    public async Task Going_back_to_the_hosted_service_needs_no_check_and_removes_the_tokens()
    {
        using var rig = new Rig();
        await rig.Selector.TryUseAsync("https://mein.server.test/");
        rig.Api.Requests.Clear();
        rig.Tokens.Tokens = new AuthTokens("access", "refresh");

        await rig.Selector.UseHostedAsync();

        Assert.True(rig.Store.IsHosted);
        Assert.Null(rig.Tokens.Tokens);
        Assert.DoesNotContain(rig.Api.Requests, r => r.Path.EndsWith("/health", StringComparison.Ordinal));
    }

    // R6
    [Fact]
    public async Task After_a_server_is_taken_the_sign_in_methods_are_kept()
    {
        using var rig = new Rig();
        rig.Api.Respond = request => request.Path.EndsWith("/instance", StringComparison.Ordinal)
            ? FakeApi.Json(HttpStatusCode.OK, new InstanceResponse(["password", "google"]))
            : new HttpResponseMessage(HttpStatusCode.OK);

        await rig.Selector.TryUseAsync("https://mein.server.test/");

        Assert.Equal(["password", "google"], rig.Store.SignInMethods);
    }

    [Fact]
    public async Task A_server_without_the_instance_endpoint_offers_the_password()
    {
        using var rig = new Rig();
        rig.Api.Respond = request => new HttpResponseMessage(
            request.Path.EndsWith("/health", StringComparison.Ordinal) ? HttpStatusCode.OK : HttpStatusCode.NotFound);

        await rig.Selector.TryUseAsync("https://mein.server.test/");

        Assert.Equal(["password"], rig.Store.SignInMethods);
    }

    private sealed class NeverAnswers : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("kein Netz");
    }
}
