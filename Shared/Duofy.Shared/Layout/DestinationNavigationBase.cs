using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Duofy.Shared.Layout;

/// <summary>
/// Der gemeinsame Teil der unteren Leiste und der Seitenleiste (Spec 10, R5): Beide lesen ihre Ziele aus
/// AppDestinations und hören selbst auf Adresswechsel, weil das Layout sie sonst nicht neu zeichnet.
/// </summary>
public abstract class DestinationNavigationBase : ComponentBase, IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>Die Ziele in der Reihenfolge der Leiste; ohne Angabe die Liste der App.</summary>
    [Parameter]
    public IReadOnlyList<(AppDestination Destination, string Href, string LabelKey, string Icon)> Destinations { get; set; } = AppDestinations.All;

    /// <summary>Das Ziel der offenen Seite; null bei Adressen ohne Ziel.</summary>
    protected AppDestination? Current { get; private set; }

    /// <summary>True, wenn die offene Seite diese Adresse oder eine Unterseite davon ist; "/konto" ist nicht "/konten".</summary>
    protected bool IsOpen(string href)
    {
        var open = "/" + Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0].Trim('/');
        return open == href || (href != "/" && open.StartsWith(href + "/", StringComparison.Ordinal));
    }

    protected override void OnInitialized()
    {
        Current = AppDestinations.For(Navigation.ToBaseRelativePath(Navigation.Uri));
        Navigation.LocationChanged += OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        Current = AppDestinations.For(Navigation.ToBaseRelativePath(e.Location));
        InvokeAsync(StateHasChanged);
    }

    public virtual void Dispose() => Navigation.LocationChanged -= OnLocationChanged;
}
