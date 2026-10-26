using System.Net;
using Duofy.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace Duofy.ApiClient.Tests;

/// <summary>Der Server-Speicher und die Wirkung der Wahl auf die Anfragen (R5).</summary>
public class ServerStoreTests
{
    private static readonly Uri Hosted = new("https://api.duofy.invalid/");

    // A5, erster Teil
    [Fact]
    public void A_taken_server_comes_back_when_the_store_is_created_again_from_the_same_preferences()
    {
        var preference = new InMemoryServerPreference();
        new ServerStore(Hosted, preference).Set(new Uri("https://mein.server.test/duofy"));

        var again = new ServerStore(Hosted, preference);

        Assert.Equal(new Uri("https://mein.server.test/duofy/"), again.CurrentAddress);
        Assert.False(again.IsHosted);
    }

    [Fact]
    public void Without_a_choice_the_hosted_address_is_used()
    {
        var store = new ServerStore(Hosted, new InMemoryServerPreference());

        Assert.Equal(Hosted, store.CurrentAddress);
        Assert.True(store.IsHosted);
    }

    [Fact]
    public void Reset_goes_back_to_the_hosted_address_and_forgets_the_choice()
    {
        var preference = new InMemoryServerPreference();
        var store = new ServerStore(Hosted, preference);
        store.Set(new Uri("https://mein.server.test/"));

        store.Reset();

        Assert.True(store.IsHosted);
        Assert.Null(preference.Load());
        Assert.True(new ServerStore(Hosted, preference).IsHosted);
    }

    [Fact]
    public void Setting_the_hosted_address_counts_as_the_hosted_service()
    {
        var store = new ServerStore(Hosted, new InMemoryServerPreference());
        store.Set(new Uri("https://mein.server.test/"));

        store.Set(new Uri("https://api.duofy.invalid"));

        Assert.True(store.IsHosted);
    }

    [Fact]
    public void An_unreadable_stored_value_falls_back_to_the_hosted_address()
    {
        var store = new ServerStore(Hosted, new InMemoryServerPreference("das ist keine adresse"));

        Assert.True(store.IsHosted);
    }

    [Fact]
    public void Changes_are_announced()
    {
        var store = new ServerStore(Hosted, new InMemoryServerPreference());
        var announced = 0;
        store.Changed += () => announced++;

        store.Set(new Uri("https://mein.server.test/"));
        store.Reset();

        Assert.Equal(2, announced);
    }

    // A5, zweiter Teil
    [Fact]
    public async Task The_next_request_goes_to_the_taken_server_without_a_restart()
    {
        var api = new FakeApi { Respond = _ => FakeApi.Json(HttpStatusCode.OK, new MeResponse(Guid.NewGuid(), "a@b.de", "A", "B")) };
        var store = new ServerStore(Hosted, new InMemoryServerPreference());
        using var provider = TestHost.Create(api, new InMemoryTokenStore(), store);
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserApi>();

        await users.GetMeAsync();
        store.Set(new Uri("https://mein.server.test/duofy"));
        await users.GetMeAsync();

        Assert.Equal(["api.duofy.invalid", "mein.server.test"], api.Hosts);
        Assert.Equal("/duofy/api/v1/users/me", api.Requests[1].Path);
    }

    [Fact]
    public async Task The_token_refresh_goes_to_the_same_server_as_the_request()
    {
        var api = new FakeApi();
        api.Respond = request => (request.Path, request.Authorization) switch
        {
            ("/duofy/api/v1/users/me", "Bearer old") => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            ("/duofy/api/v1/auth/refresh", _) => FakeApi.Json(HttpStatusCode.OK, new TokenResponse("Bearer", "new", 900, "new-refresh")),
            ("/duofy/api/v1/users/me", "Bearer new") => FakeApi.Json(HttpStatusCode.OK, new MeResponse(Guid.NewGuid(), "a@b.de", "A", "B")),
            _ => new HttpResponseMessage(HttpStatusCode.BadRequest),
        };
        var store = new ServerStore(Hosted, new InMemoryServerPreference());
        store.Set(new Uri("https://mein.server.test/duofy"));
        using var provider = TestHost.Create(api, new InMemoryTokenStore { Tokens = new AuthTokens("old", "old-refresh") }, store);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IUserApi>().GetMeAsync();

        Assert.True(result.Succeeded);
        Assert.All(api.Hosts, host => Assert.Equal("mein.server.test", host));
        Assert.Equal(1, api.CountOf("/duofy/api/v1/auth/refresh"));
    }
}
