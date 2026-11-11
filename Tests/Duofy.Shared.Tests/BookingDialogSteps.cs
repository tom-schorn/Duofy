using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>
/// Handgriffe für den Buchungsdialog in Schritten (Spec 124): zum letzten Schritt gehen und in einer Auswahl mit Suchfeld
/// (ComboBox) wählen, so wie es eine Person tut.
/// </summary>
internal static class BookingDialogSteps
{
    /// <summary>Klickt Weiter, bis der Knopf Speichern da ist (nur bei bestandener Prüfung geht es weiter).</summary>
    public static void ToLast(IRenderedComponent<IComponent> cut)
    {
        for (var i = 0; i < 6 && cut.FindAll("[data-testid='booking-next']").Count > 0; i++)
        {
            cut.Find("[data-testid='booking-next']").Click();
        }
    }

    /// <summary>Wählt in der Auswahl mit Suchfeld die Option mit diesem Wert.</summary>
    public static void Choose(IRenderedComponent<IComponent> cut, string testId, string value) =>
        cut.Find($"[data-testid='{testId}-option-{value}']").Click();

    /// <summary>Der gewählte Wert einer Auswahl mit Suchfeld ("" = nichts gewählt).</summary>
    public static string Chosen(IRenderedComponent<IComponent> cut, string testId) =>
        cut.Find($"[data-testid='{testId}']").GetAttribute("data-value") ?? "";

    /// <summary>Die Namen der Optionen einer Auswahl mit Suchfeld, in der Reihenfolge der Liste.</summary>
    public static IReadOnlyList<string> OptionLabels(IRenderedComponent<IComponent> cut, string testId) =>
        cut.FindAll($"[data-testid='{testId}'] [role='option'] .combo-option-label").Select(e => e.TextContent.Trim()).ToList();
}
