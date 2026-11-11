using Duofy.Shared.Services;

namespace Duofy.Services;

/// <summary>
/// Teilen in der App: öffnet den Teilen-Dialog von Android mit dem Link, zum Beispiel für einen Messenger,
/// oder mit einer Datei, zum Beispiel dem PDF des Plans (Spec 125, R6).
/// </summary>
public sealed class MauiShareService : IShareService, IPdfShare
{
    public Task ShareAsync(string title, string link) =>
        Share.Default.RequestAsync(new ShareTextRequest
        {
            Uri = link,
            Title = title,
        });

    public Task ShareFileAsync(string title, string path, string contentType) =>
        Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(path, contentType),
        });
}
