namespace Duofy.Shared.Services;

/// <summary>Die Art einer Meldung. Ein eigener Typ, damit die Meldung nicht an eine Oberflächenbibliothek hängt.</summary>
public enum NoticeSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>Eine Meldung für die Person: der Text und ob es ein Erfolg oder ein Fehler ist.</summary>
public sealed record NoticeMessage(string Text, NoticeSeverity Severity);
