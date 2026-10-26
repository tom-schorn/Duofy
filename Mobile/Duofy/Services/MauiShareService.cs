using Duofy.Shared.Services;

namespace Duofy.Services;

/// <summary>
/// Teilen in der App: öffnet den Teilen-Dialog von Android mit dem Link, zum Beispiel für einen Messenger.
/// </summary>
public sealed class MauiShareService : IShareService
{
    public Task ShareAsync(string title, string link) =>
        Share.Default.RequestAsync(new ShareTextRequest
        {
            Uri = link,
            Title = title,
        });
}
