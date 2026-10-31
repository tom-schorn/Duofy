using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Duofy.Shared.Layout;

/// <summary>
/// Der gemeinsame Teil der unteren Leiste und der Seitenleiste (Spec 10, R2 und R5): Beide lesen dieselbe Liste
/// der Ziele und leuchten für dasselbe Ziel. Sie hören selbst auf Adresswechsel, weil das Layout sie sonst nicht
/// neu zeichnet.
/// </summary>
public abstract class DestinationNavigationBase : ComponentBase, IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>Die Ziele in der Reihenfolge der Leiste; ohne Angabe die Liste der App.</summary>
    [Parameter]
    public IReadOnlyList<(AppDestination Destination, string Href, string LabelKey, string Icon)> Destinations { get; set; } = AppDestinations.All;

    /// <summary>Das Ziel der offenen Seite; null bei Adressen ohne Ziel.</summary>
    protected AppDestination? Current { get; private set; }

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

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;
}
