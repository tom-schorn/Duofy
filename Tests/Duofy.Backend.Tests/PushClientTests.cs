using System.Net;
using System.Text.Json;
using Duofy.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duofy.Backend.Tests;

/// <summary>
/// Spec 84, A1 und A4: Die echten Clients für FCM und WNS bauen genau das Signal, das die App erwartet (nur Kennung und
/// Art), und sprechen die Anbieter richtig an. Getestet ohne Zugangsdaten: FCM nur bis zur gebauten Nachricht, WNS über
/// einen gespielten HTTP-Weg.
/// </summary>
public class PushClientTests
{
    private static readonly Guid ReminderId = Guid.Parse("0f7c6f0e-5c1b-4b5e-9d63-3c8b2f2a7a11");

    private const string Channel = "https://db5p.notify.windows.com/?token=AwYAAABPp1";

    // A4: Das Signal hat nur die Kennung und die Art.
    [Fact]
    public void The_fcm_message_is_a_high_priority_data_message_with_only_the_id_and_the_type()
    {
        var message = FcmClient.BuildMessage("fcm-token", new PushPayload(ReminderId, PushPayload.Due));

        Assert.Equal("fcm-token", message.Token);
        Assert.Equal(new Dictionary<string, string> { ["reminderId"] = ReminderId.ToString(), ["type"] = "due" }, message.Data);
        Assert.Null(message.Notification);
        Assert.Equal(FirebaseAdmin.Messaging.Priority.High, message.Android.Priority);
    }

    [Fact]
    public void The_wns_content_is_json_with_only_the_id_and_the_type()
    {
        var json = JsonDocument.Parse(WnsClient.BuildContent(new PushPayload(ReminderId, PushPayload.Withdraw)));

        Assert.Equal(["reminderId", "type"], json.RootElement.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(ReminderId.ToString(), json.RootElement.GetProperty("reminderId").GetString());
        Assert.Equal("withdraw", json.RootElement.GetProperty("type").GetString());
    }

    private sealed class WnsHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];
        public HttpStatusCode PushStatus { get; set; } = HttpStatusCode.OK;
        public int Tokens { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            if (request.RequestUri!.Host == "login.live.com")
            {
                Tokens++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"wns-access\",\"token_type\":\"bearer\",\"expires_in\":86400}"),
                };
            }
            return new HttpResponseMessage(PushStatus);
        }
    }

    private static (WnsClient Client, WnsHandler Handler) Wns(string? clientId = "ms-app://s-1-15-2-1", string? secret = "secret-value")
    {
        var handler = new WnsHandler();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Duofy:Reminders:Wns:ClientId"] = clientId,
            ["Duofy:Reminders:Wns:ClientSecret"] = secret,
        }).Build();
        return (new WnsClient(new HttpClient(handler), configuration, TimeProvider.System, NullLogger<WnsClient>.Instance), handler);
    }

    // A1: Der WNS-Client holt ein Zugriffstoken mit Client-Kennung und Geheimnis und schickt eine Raw-Benachrichtigung.
    [Fact]
    public async Task Wns_gets_a_token_and_sends_a_raw_notification_to_the_channel()
    {
        var (client, handler) = Wns();

        await client.SendAsync(Channel, new PushPayload(ReminderId, PushPayload.Due), CancellationToken.None);

        var tokenRequest = handler.Requests[0];
        Assert.Equal("https://login.live.com/accesstoken.srf", tokenRequest.Request.RequestUri!.ToString());
        Assert.Contains("grant_type=client_credentials", tokenRequest.Body);
        Assert.Contains("client_id=ms-app%3A%2F%2Fs-1-15-2-1", tokenRequest.Body);
        Assert.Contains("client_secret=secret-value", tokenRequest.Body);
        Assert.Contains("scope=notify.windows.com", tokenRequest.Body);

        var push = handler.Requests[1];
        Assert.Equal(Channel, push.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", push.Request.Headers.Authorization!.Scheme);
        Assert.Equal("wns-access", push.Request.Headers.Authorization.Parameter);
        Assert.Equal("wns/raw", push.Request.Headers.GetValues("X-WNS-Type").Single());
        Assert.Equal("application/octet-stream", push.Request.Content!.Headers.ContentType!.MediaType);
        Assert.Contains(ReminderId.ToString(), push.Body);
    }

    [Fact]
    public async Task Wns_reuses_the_access_token_for_the_next_signals()
    {
        var (client, handler) = Wns();

        await client.SendAsync(Channel, new PushPayload(ReminderId, PushPayload.Due), CancellationToken.None);
        await client.SendAsync(Channel, new PushPayload(ReminderId, PushPayload.Withdraw), CancellationToken.None);

        Assert.Equal(1, handler.Tokens);
        Assert.Equal(3, handler.Requests.Count);
    }

    // Die Kanal-Adresse kommt vom Gerät, also von außen: Sie darf nur ein WNS-Rechner sein, sonst schickte der Server das
    // Zugriffstoken an einen fremden Rechner.
    [Theory]
    [InlineData("http://db5p.notify.windows.com/?token=x")]
    [InlineData("https://evil.example/?token=x")]
    [InlineData("https://notify.windows.com.evil.example/?token=x")]
    [InlineData("https://user@evil.example/")]
    [InlineData("not a url")]
    public async Task Wns_refuses_a_channel_that_is_not_a_wns_address_without_sending_the_token(string channel)
    {
        var (client, handler) = Wns();

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(channel, new PushPayload(ReminderId, PushPayload.Due), CancellationToken.None));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void The_channel_check_accepts_the_wns_hosts_only()
    {
        Assert.True(WnsClient.IsWnsChannel(Channel));
        Assert.True(WnsClient.IsWnsChannel("https://par02p.notify.windows.com/?token=x"));
        Assert.False(WnsClient.IsWnsChannel("https://notify.windows.com.evil.example/"));
        Assert.False(WnsClient.IsWnsChannel(null));
    }

    // Eine Absage des Anbieters ist ein Fehler, den der Sender loggt (ohne Token), und hält die anderen Geräte nicht auf.
    [Fact]
    public async Task Wns_reports_a_refused_push_as_a_failure()
    {
        var (client, handler) = Wns();
        handler.PushStatus = HttpStatusCode.Gone;

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Channel, new PushPayload(ReminderId, PushPayload.Due), CancellationToken.None));

        Assert.DoesNotContain("token=", failure.Message);
        Assert.DoesNotContain("secret-value", failure.Message);
    }

    [Fact]
    public async Task Wns_without_credentials_fails_with_a_clear_message_and_no_request()
    {
        var (client, handler) = Wns(clientId: null, secret: null);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(Channel, new PushPayload(ReminderId, PushPayload.Due), CancellationToken.None));

        Assert.Contains("Duofy:Reminders:Wns", failure.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Fcm_without_a_service_account_file_fails_with_a_clear_message()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Duofy:Reminders:Fcm:ServiceAccountFile"] = "does-not-exist.json",
        }).Build();
        var client = new FcmClient(configuration);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync("t", new PushPayload(ReminderId, PushPayload.Due), CancellationToken.None));

        Assert.Contains("Duofy:Reminders:Fcm:ServiceAccountFile", failure.Message);
    }
    // N2: Ein Fehler beim ersten Einrichten darf nicht für immer im Zwischenspeicher bleiben.
    [Fact]
    public async Task A_failed_fcm_setup_is_tried_again_on_the_next_send_and_not_remembered_forever()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Duofy:Reminders:Fcm:ServiceAccountFile"] = "does-not-exist.json",
        }).Build();
        var client = new FcmClient(configuration);
        var payload = new PushPayload(ReminderId, PushPayload.Due);
        var first = await Assert.ThrowsAnyAsync<Exception>(() => client.SendAsync("t", payload, CancellationToken.None));

        var file = Path.Combine(Path.GetTempPath(), $"fcm-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, "not a service account");
        try
        {
            configuration["Duofy:Reminders:Fcm:ServiceAccountFile"] = file;
            var second = await Assert.ThrowsAnyAsync<Exception>(() => client.SendAsync("t", payload, CancellationToken.None));

            Assert.NotSame(first, second);
            Assert.DoesNotContain("fehlt oder zeigt auf keine Datei", second.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
