namespace Duofy.Shared.Services;

/// <summary>
/// Die Erklärspalte am PC (Spec 82, R13): welche Seite welche Abschnitte zeigt. Die Texte stehen in den Ressourcen als
/// Help_{Thema}_{n}_Title und Help_{Thema}_{n}_Body; freigegeben von Tom am 03.11.2026
/// (docs/specs/82-blueprint-foundation/erklaerspalte-texte.md).
/// </summary>
public static class HelpTopics
{
    public const string Start = "start";
    public const string Plans = "plans";
    public const string Month = "month";
    public const string Bookings = "bookings";
    public const string Review = "review";
    public const string Household = "household";
    public const string HouseholdPlan = "householdplan";
    public const string Grants = "grants";
    public const string Commitments = "commitments";
    public const string Accounts = "accounts";
    public const string Savings = "savings";
    public const string UserAccount = "useraccount";
    public const string Admin = "admin";

    /// <summary>Wie viele Abschnitte ein Thema hat.</summary>
    public static IReadOnlyDictionary<string, int> Count { get; } = new Dictionary<string, int>
    {
        [Start] = 2, [Plans] = 3, [Month] = 6, [Bookings] = 3, [Review] = 2, [Household] = 4, [HouseholdPlan] = 4,
        [Grants] = 4, [Commitments] = 4, [Accounts] = 4, [Savings] = 3, [UserAccount] = 2, [Admin] = 2,
    };

    /// <summary>Das Thema einer Adresse ohne Rechnername; null, wenn die Seite keine Erklärungen hat (Mehr, Lizenzen, Rechtliches).</summary>
    public static string? ForPath(string path)
    {
        var parts = path.Split('?', '#')[0].Trim('/').Split('/');
        return parts[0] switch
        {
            "" => Start,
            "plan" => parts.Length > 1 ? Month : Plans,
            "haushaltsplan" => HouseholdPlan,
            "haushalt" => parts.Length > 1 && parts[1] == "freigaben" ? Grants : parts.Length > 1 && parts[1] == "richtwerte" ? HouseholdPlan : Household,
            "vertraege" => Commitments,
            "konten" => Accounts,
            "sparen" => Savings,
            "konto" => parts.Length > 1 ? null : UserAccount,
            "admin" => Admin,
            _ => null,
        };
    }
}
