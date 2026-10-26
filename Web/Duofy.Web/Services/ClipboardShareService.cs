using Duofy.Shared.Services;
using Microsoft.JSInterop;

namespace Duofy.Web.Services;

/// <summary>
/// Teilen im Browser: Der Link kommt in die Zwischenablage, von dort fügt die Person ihn in jeden
/// Messenger ein. Ein Teilen-Dialog wie in der App fehlt vielen Desktop-Browsern.
/// </summary>
public sealed class ClipboardShareService(IJSRuntime js) : IShareService
{
    public async Task ShareAsync(string title, string link) =>
        await js.InvokeVoidAsync("navigator.clipboard.writeText", link);
}
