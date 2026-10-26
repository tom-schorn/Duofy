using System.Net;
using System.Net.Http.Json;
using Duofy.Schemas;

namespace Duofy.Api.Tests;

/// <summary>Die Auskunft der API über sich selbst: welche Anmeldewege sie anbietet (R6).</summary>
[Collection(ApiCollection.Name)]
public class InstanceTests
{
    private readonly HttpClient client;

    public InstanceTests(ApiFixture fixture)
    {
        client = fixture.Factory.CreateClient();
    }

    // A6
    [Fact]
    public async Task Instance_answers_without_a_token_with_the_password_sign_in()
    {
        var response = await client.GetAsync("/api/v1/instance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InstanceResponse>();
        Assert.Equal(["password"], body!.SignInMethods);
    }
}
