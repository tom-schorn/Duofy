namespace Duofy.Domain;

/// <summary>
/// Ein Haushalt: die Menschen, die zusammen planen. Eine Person ist in keinem, bis sie einen gründet
/// (<see cref="Found"/>) oder einer Einladung folgt, und höchstens in einem. Nur der Gründer verwaltet ihn.
///
/// Ein Haushalt besitzt nichts. Konten, Verträge, Pläne und Buchungen gehören immer einer Person;
/// Beitreten, Austreten und Entfernen ändern an ihnen nichts.
/// </summary>
public class Household
{
    /// <summary>Die eindeutige Kennung; sie entsteht schon beim Gründen in <see cref="Found"/>.</summary>
    public Guid Id { get; set; }

    /// <summary>Der Name, 1 bis 100 Zeichen, zum Beispiel "Haushalt von Anna".</summary>
    public required string Name { get; set; }

    /// <summary>Die Zielquote des Haushalts für Grundbedarf in ganzen Prozent; jede Person behält ihre eigenen.</summary>
    public int QuotaNeeds { get; set; } = Quotas.Default.Needs;

    /// <summary>Die Zielquote des Haushalts für Wünsche in ganzen Prozent.</summary>
    public int QuotaWants { get; set; } = Quotas.Default.Wants;

    /// <summary>Die Zielquote des Haushalts für Sparen in ganzen Prozent.</summary>
    public int QuotaSavings { get; set; } = Quotas.Default.Savings;

    /// <summary>Die drei Zielquoten zusammen; liest und schreibt die drei Felder darüber.</summary>
    public Quotas Quotas
    {
        get => new(QuotaNeeds, QuotaWants, QuotaSavings);
        set => (QuotaNeeds, QuotaWants, QuotaSavings) = (value.Needs, value.Wants, value.Savings);
    }

    /// <summary>Wann der Haushalt gegründet wurde.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Die Mitglieder; immer mindestens eines, und genau eines davon ist der Gründer.</summary>
    public List<HouseholdMember> Members { get; set; } = [];

    /// <summary>Der Name, wenn beim Gründen keiner angegeben ist: "Haushalt von Vorname", auf 100 Zeichen gekürzt.</summary>
    public static string DefaultName(string firstName)
    {
        var name = $"Haushalt von {firstName}";
        return name.Length > HouseholdRules.NameMaxLength ? name[..HouseholdRules.NameMaxLength] : name;
    }

    /// <summary>
    /// Gründet einen Haushalt mit der Person als Gründer und einzigem Mitglied. Ohne Namen heißt er nach
    /// <see cref="DefaultName"/>; einen angegebenen Namen prüft der Aufrufer vorher.
    /// </summary>
    public static Household Found(Guid userId, string firstName, string? name, DateTimeOffset now)
    {
        var household = new Household { Id = Guid.NewGuid(), Name = name ?? DefaultName(firstName), CreatedAt = now };
        household.Members.Add(new HouseholdMember
        {
            HouseholdId = household.Id,
            UserId = userId,
            Role = HouseholdRole.Founder,
            JoinedAt = now,
        });
        return household;
    }

    /// <summary>Die Mitgliedschaft der Person oder null, wenn sie nicht zu diesem Haushalt gehört.</summary>
    public HouseholdMember? MemberOf(Guid userId) => Members.FirstOrDefault(m => m.UserId == userId);

    /// <summary>True, wenn die Person den Haushalt verwaltet.</summary>
    public bool IsFounder(Guid userId) => MemberOf(userId)?.Role == HouseholdRole.Founder;

    /// <summary>
    /// Wer Gründer wird, wenn <paramref name="leavingId"/> das Konto löscht: das Mitglied, das am frühesten
    /// dazukam. Null, wenn sonst niemand im Haushalt ist.
    /// </summary>
    public HouseholdMember? NextFounder(Guid leavingId) =>
        Members.Where(m => m.UserId != leavingId).OrderBy(m => m.JoinedAt).ThenBy(m => m.UserId).FirstOrDefault();
}

/// <summary>Eine Person in einem Haushalt. Jede Person hat höchstens eine Mitgliedschaft.</summary>
public class HouseholdMember
{
    /// <summary>Der Haushalt.</summary>
    public Guid HouseholdId { get; set; }

    /// <summary>Die Person.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gründer oder Mitglied; eine andere Rolle gibt es nicht.</summary>
    public HouseholdRole Role { get; set; }

    /// <summary>Wann die Person dazukam. Löscht der Gründer sein Konto, wird das früheste Mitglied Gründer.</summary>
    public DateTimeOffset JoinedAt { get; set; }
}

/// <summary>Die Rolle in einem Haushalt.</summary>
public enum HouseholdRole
{
    /// <summary>Hat den Haushalt gegründet; benennt um, setzt Quoten, lädt ein und entfernt. Genau einer je Haushalt.</summary>
    Founder,

    /// <summary>Sieht den Haushalt und kann austreten; danach ist die Person in keinem.</summary>
    Member,
}

/// <summary>Die festen Grenzen des Haushalts.</summary>
public static class HouseholdRules
{
    /// <summary>Die größte Länge eines Haushaltsnamens.</summary>
    public const int NameMaxLength = 100;

    /// <summary>Höchstens so viele offene Einladungen hat ein Haushalt zugleich.</summary>
    public const int MaxOpenInvitations = 10;

    /// <summary>So lange gilt eine Einladung.</summary>
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(14);
}
