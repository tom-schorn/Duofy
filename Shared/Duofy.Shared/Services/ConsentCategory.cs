namespace Duofy.Shared.Services;

/// <summary>Die Arten optionaler Fremddienste, für die eine Einwilligung gefragt wird (Issue #35).</summary>
public enum ConsentCategory
{
    /// <summary>Webauswertung (Cloudflare Web Analytics): zählt Seitenaufrufe.</summary>
    Statistics,

    /// <summary>Chat (Crisp): ein Fenster, über das Besucher Fragen stellen können (Issue #36).</summary>
    Chat,
}
