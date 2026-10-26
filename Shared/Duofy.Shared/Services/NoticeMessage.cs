using MudBlazor;

namespace Duofy.Shared.Services;

/// <summary>Eine Meldung für die Person: der Text und ob es ein Erfolg oder ein Fehler ist.</summary>
public sealed record NoticeMessage(string Text, Severity Severity);
