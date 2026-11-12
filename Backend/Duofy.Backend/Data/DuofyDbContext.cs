using Duofy.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Duofy.Data;

/// <summary>
/// Die Verbindung zur Datenbank. Über diese Klasse wird alles gelesen und
/// gespeichert. Sie entspricht der "Session" in SQLAlchemy.
///
/// Die Basisklasse <see cref="IdentityDbContext{TUser, TRole, TKey}"/> bringt die
/// Tabellen für die Anmeldung mit: Benutzer, Rollen und ihre Verknüpfungen. Die
/// drei Angaben in den spitzen Klammern sind unser Benutzer, die Rolle aus dem
/// Baustein und der Typ der Kennungen.
/// </summary>
public class DuofyDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public DuofyDbContext(DbContextOptions<DuofyDbContext> options, IOwnerContext owners) : base(options)
    {
        Owners = owners;
    }

    /// <summary>Wessen Daten diese Verbindung sehen und schreiben darf (Spec 56).</summary>
    public IOwnerContext Owners { get; }

    // Die Filter lesen diese drei Werte bei jeder Abfrage neu; EF Core setzt sie als Parameter ein.
    private bool OwnerFilterOff => Owners.IsBypassed;

    private bool SignedIn => Owners.CurrentUserId is not null;

    private Guid[] VisibleOwnerIds => [.. Owners.VisibleOwners];

    private Guid[] CategoryOwnerIds => [.. Owners.CategoryOwners];

    /// <summary>Die Prüfung beim Speichern gilt für jede Verbindung, auch für die, die ein Test selbst baut.</summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(OwnerSaveCheck.Instance);

    /// <summary>
    /// Die Tabelle der Verträge. Achtung: Sie enthält die Verträge aller Benutzer.
    /// Wer hier liest, schränkt selbst auf den gemeinten Besitzer ein, also mit
    /// <c>.Where(c => c.OwnerId == ...)</c>; der Besitzer-Filter darunter zeigt ohnehin
    /// nur sichtbare Besitzer (Spec 56).
    /// </summary>
    public DbSet<Commitment> Commitments => Set<Commitment>();

    /// <summary>
    /// Die Tabelle der Sparziele (Issue 30). Sie enthält die Ziele aller Benutzer; wer hier liest, schränkt selbst auf
    /// den gemeinten Besitzer ein, der Besitzer-Filter darunter zeigt ohnehin nur sichtbare Besitzer (Spec 56). Den Stand
    /// eines Ziels gibt es hier nicht; er wird aus den zugeordneten Buchungszeilen ausgerechnet.
    /// </summary>
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();

    /// <summary>
    /// Die Tabelle der Schulden und Kredite (Issue 31). Sie enthält die Einträge aller Benutzer; wer hier liest, schränkt selbst
    /// auf den gemeinten Besitzer ein. Das Gezahlte gibt es hier nicht; es wird aus den zugeordneten Ausgaben ausgerechnet.
    /// </summary>
    public DbSet<Debt> Debts => Set<Debt>();

    /// <summary>
    /// Die Tabelle der Kategorien. Sie enthält die eingebauten (ohne Besitzer, von der
    /// Migration angelegt) und die eigenen Kategorien aller Benutzer. Wer hier liest,
    /// zeigt nur eingebaute und die eigenen des gemeinten Besitzers; der Besitzer-Filter
    /// darunter zeigt ohnehin nur sichtbare Besitzer (Spec 56).
    /// </summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>
    /// Die Tabelle der Konten aller Benutzer. Wer hier liest, schränkt selbst auf den
    /// gemeinten Besitzer ein, also mit <c>.Where(a => a.OwnerId == ...)</c>; der
    /// Besitzer-Filter darunter zeigt ohnehin nur sichtbare Besitzer (Spec 56).
    /// </summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>
    /// Die Tabelle der Buchungen aller Benutzer, mit ihren Zeilen in "booking_lines". Wer hier
    /// liest, schränkt selbst auf den gemeinten Besitzer ein; der Besitzer-Filter darunter zeigt
    /// ohnehin nur sichtbare Besitzer (Spec 56).
    /// </summary>
    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>
    /// Die Tabelle der Monatspläne aller Benutzer, mit ihren Posten in "plan_positions". Wer hier
    /// liest, schränkt selbst auf den gemeinten Besitzer ein; der Besitzer-Filter darunter zeigt
    /// ohnehin nur sichtbare Besitzer (Spec 56).
    /// </summary>
    public DbSet<Plan> Plans => Set<Plan>();

    /// <summary>
    /// Die Tabelle der Posten aller Pläne. Ein Posten hat keinen eigenen Besitzer; er gehört der
    /// Person seines Plans, wer hier liest, schränkt über den Plan ein.
    /// </summary>
    public DbSet<PlanPosition> PlanPositions => Set<PlanPosition>();

    /// <summary>
    /// Die Tabelle der Haushalte, mit ihren Mitgliedern in "household_members". Ein Haushalt besitzt
    /// nichts; wer hier liest, zeigt nur den Haushalt, in dem der angemeldete Benutzer Mitglied ist.
    /// </summary>
    public DbSet<Household> Households => Set<Household>();

    /// <summary>Die Mitgliedschaften aller Haushalte; jeder Benutzer hat höchstens eine.</summary>
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();

    /// <summary>Die Einladungen aller Haushalte. Nur der Gründer des Haushalts sieht sie und ihre Tokens.</summary>
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();

    /// <summary>
    /// Die Freigaben aller Personen (Spec 68). Wer hier liest, fragt nie selbst, sondern über die zentrale
    /// Prüfung der API: Eine Zeile zählt nur, solange beide Personen im selben Haushalt sind.
    /// </summary>
    public DbSet<Grant> Grants => Set<Grant>();

    /// <summary>
    /// Die Einladungen zur Registrierung auf diesem Server (Spec 70). Sie gehören niemandem im Sinne des
    /// Besitzer-Filters; nur Administratoren sehen sie und ihre Tokens.
    /// </summary>
    public DbSet<RegistrationInvitation> RegistrationInvitations => Set<RegistrationInvitation>();

    /// <summary>
    /// Die Links der Administratoren zum Zurücksetzen eines Passworts (Spec 53, R4). Sie gehören niemandem im Sinne
    /// des Besitzer-Filters; gespeichert ist nur der Hash des Tokens.
    /// </summary>
    public DbSet<PasswordResetLink> PasswordResetLinks => Set<PasswordResetLink>();

    /// <summary>Der Name der Schatten-Spalte, die den Plan eines Postens nennt.</summary>
    public const string PositionPlan = "PlanId";

    /// <summary>Der Name der Schatten-Spalte, die die Reihenfolge der Zeilen einer Buchung festhält (0 bis 19).</summary>
    public const string LinePosition = "Position";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Die Basisklasse beschreibt hier ihre eigenen Tabellen für die Anmeldung.
        // Ohne diesen Aufruf kennt EF Core sie nicht und meldet beim Erzeugen einer
        // Migration einen fehlenden Primärschlüssel.
        base.OnModelCreating(modelBuilder);

        // Der Baustein nennt seine Tabellen von Haus aus "AspNetUsers",
        // "AspNetRoles" und so weiter. Hier bekommen sie Namen in derselben
        // Schreibweise wie unsere eigenen Tabellen.
        modelBuilder.Entity<AppUser>().ToTable("users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        modelBuilder.Entity<AppUser>()
            .Property(u => u.FirstName)
            .HasMaxLength(100);

        modelBuilder.Entity<AppUser>()
            .Property(u => u.LastName)
            .HasMaxLength(100);

        // Die Quoten bekommen keinen Standardwert in der Datenbank: EF Core würde eine Quote von 0
        // beim Anlegen sonst für "nicht gesetzt" halten. Bestehende Benutzer bekommen die
        // 50/30/20-Regel von Hand in der Migration AddMonthlyPlan.
        modelBuilder.Entity<AppUser>().ToTable(table =>
            table.HasCheckConstraint("ck_users_quotas", "quota_needs + quota_wants + quota_savings = 100"));

        ConfigureCommitments(modelBuilder);
        ConfigureSavingsGoals(modelBuilder);
        ConfigureDebts(modelBuilder);
        ConfigureCategories(modelBuilder);
        ConfigureAccounts(modelBuilder);
        ConfigureBookings(modelBuilder);
        ConfigurePlans(modelBuilder);
        ConfigureHouseholds(modelBuilder);
        ConfigureGrants(modelBuilder);
        ConfigureRegistrationInvitations(modelBuilder);
        ConfigurePasswordResetLinks(modelBuilder);
        ConfigureOwnerFilters(modelBuilder);
    }

    /// <summary>
    /// Das Netz unter der zentralen Prüfung der API (Spec 56, R1, R2): Jede Abfrage auf Daten mit Besitzer zeigt nur
    /// Zeilen der sichtbaren Besitzer, ohne dass die Abfrage es sagt. Die eigenen Bedingungen der Endpunkte bleiben
    /// und schränken weiter ein. Posten und Buchungszeilen haben keinen eigenen Besitzer und folgen ihrem Plan oder
    /// ihrer Buchung. Eingebaute Kategorien ohne Besitzer sieht jeder, der angemeldet ist. Ohne Anmeldung ist die
    /// Menge leer, also bleibt jede dieser Abfragen leer. In einem Bereich aus <see cref="IOwnerContext.Bypass"/>
    /// ist der Filter aus.
    /// </summary>
    private void ConfigureOwnerFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>().HasQueryFilter(a => OwnerFilterOff || VisibleOwnerIds.Contains(a.OwnerId));
        modelBuilder.Entity<Booking>().HasQueryFilter(b => OwnerFilterOff || VisibleOwnerIds.Contains(b.OwnerId));
        modelBuilder.Entity<Commitment>().HasQueryFilter(c => OwnerFilterOff || VisibleOwnerIds.Contains(c.OwnerId));
        modelBuilder.Entity<SavingsGoal>().HasQueryFilter(g => OwnerFilterOff || VisibleOwnerIds.Contains(g.OwnerId));
        modelBuilder.Entity<Debt>().HasQueryFilter(d => OwnerFilterOff || VisibleOwnerIds.Contains(d.OwnerId));
        modelBuilder.Entity<Plan>().HasQueryFilter(p => OwnerFilterOff || VisibleOwnerIds.Contains(p.OwnerId));
        modelBuilder.Entity<Category>().HasQueryFilter(c =>
            OwnerFilterOff || (c.OwnerId == null ? SignedIn : (VisibleOwnerIds.Contains(c.OwnerId.Value) || CategoryOwnerIds.Contains(c.OwnerId.Value))));

        modelBuilder.Entity<BookingLine>().HasQueryFilter(l =>
            OwnerFilterOff || Bookings.Any(b => b.Id == EF.Property<Guid>(l, "BookingId") && VisibleOwnerIds.Contains(b.OwnerId)));
        modelBuilder.Entity<PlanPosition>().HasQueryFilter(p =>
            OwnerFilterOff || Plans.Any(plan => plan.Id == EF.Property<Guid>(p, PositionPlan) && VisibleOwnerIds.Contains(plan.OwnerId)));
    }

    /// <summary>
    /// Die Tabelle "password_reset_links". Ein Link verschwindet mit der Person, deren Passwort er setzt, und mit dem
    /// Administrator, der ihn angelegt hat; er gilt ohnehin nur 24 Stunden.
    /// </summary>
    private static void ConfigurePasswordResetLinks(ModelBuilder modelBuilder)
    {
        var link = modelBuilder.Entity<PasswordResetLink>();
        link.ToTable("password_reset_links");
        link.Property(l => l.Id).ValueGeneratedNever();
        link.Property(l => l.TokenHash).HasMaxLength(64);
        link.HasIndex(l => l.TokenHash).IsUnique();
        link.HasIndex(l => l.UserId);
        link.HasIndex(l => l.CreatedById);
        link.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        link.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(l => l.CreatedById)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// Die Tabelle "registration_invitations". Eine Einladung verschwindet mit dem Administrator, der sie angelegt
    /// hat; wer sie benutzt hat, bleibt als Kennung stehen, bis das Konto gelöscht wird.
    /// </summary>
    private static void ConfigureRegistrationInvitations(ModelBuilder modelBuilder)
    {
        var invitation = modelBuilder.Entity<RegistrationInvitation>();
        invitation.ToTable("registration_invitations");
        invitation.Property(i => i.Id).ValueGeneratedNever();
        invitation.Property(i => i.Token).HasMaxLength(64);
        invitation.HasIndex(i => i.Token).IsUnique();
        invitation.Property(i => i.Email).HasMaxLength(RegistrationInvitation.EmailMaxLength);
        invitation.HasIndex(i => i.CreatedById);
        invitation.HasIndex(i => i.UsedById);
        invitation.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(i => i.CreatedById)
            .OnDelete(DeleteBehavior.Cascade);
        invitation.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(i => i.UsedById)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>
    /// Die Tabelle "grants": Schlüssel (granter, grantee, area), nie an sich selbst, nur die Stufen ab
    /// "view" (keine Zeile heißt "none"; nur für Kategorien steht auch ein ausdrückliches "none" in einer Zeile, Spec 140). Die Zeilen verschwinden mit jeder der beiden Personen.
    /// </summary>
    private static void ConfigureGrants(ModelBuilder modelBuilder)
    {
        var grant = modelBuilder.Entity<Grant>();
        grant.ToTable("grants", table =>
        {
            table.HasCheckConstraint("ck_grants_not_self", "granter_id <> grantee_id");
            table.HasCheckConstraint("ck_grants_area", "area IN ('plan', 'book', 'accounts', 'commitments', 'savings', 'categories')");
            table.HasCheckConstraint("ck_grants_level", "level IN ('view', 'create', 'edit', 'delete') OR (level = 'none' AND area = 'categories')");
        });
        grant.HasKey(g => new { g.GranterId, g.GranteeId, g.Area });
        grant.HasIndex(g => g.GranteeId);
        grant.Property(g => g.Area)
            .HasMaxLength(20)
            .HasConversion(area => area.ToString().ToLowerInvariant(), key => Enum.Parse<Area>(key, ignoreCase: true));
        grant.Property(g => g.Level)
            .HasMaxLength(20)
            .HasConversion(level => level.ToString().ToLowerInvariant(), key => Enum.Parse<AccessLevel>(key, ignoreCase: true));
        grant.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(g => g.GranterId)
            .OnDelete(DeleteBehavior.Cascade);
        grant.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(g => g.GranteeId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// Die Tabellen "households", "household_members" und "household_invitations". Jeder Benutzer hat
    /// höchstens eine Mitgliedschaft (eindeutiger Index); sie verschwindet mit ihm. Mitglieder und
    /// Einladungen verschwinden mit ihrem Haushalt, Einladungen auch mit der Person, die sie angelegt hat.
    /// Wer eine Einladung benutzt hat, bleibt als Kennung stehen, bis das Konto gelöscht wird.
    /// </summary>
    private static void ConfigureHouseholds(ModelBuilder modelBuilder)
    {
        var household = modelBuilder.Entity<Household>();
        household.ToTable("households", table =>
            table.HasCheckConstraint("ck_households_quotas", "quota_needs + quota_wants + quota_savings = 100"));
        household.Ignore(h => h.Quotas);
        household.Property(h => h.Id).ValueGeneratedNever();
        household.Property(h => h.Name).HasMaxLength(HouseholdRules.NameMaxLength);
        household.HasMany(h => h.Members)
            .WithOne()
            .HasForeignKey(m => m.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        var member = modelBuilder.Entity<HouseholdMember>();
        member.ToTable("household_members");
        member.HasKey(m => new { m.HouseholdId, m.UserId });
        member.HasIndex(m => m.UserId).IsUnique();
        member.Property(m => m.Role)
            .HasMaxLength(20)
            .HasConversion(role => role.ToString().ToLowerInvariant(), key => Enum.Parse<HouseholdRole>(key, ignoreCase: true));
        member.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var invitation = modelBuilder.Entity<HouseholdInvitation>();
        invitation.ToTable("household_invitations");
        invitation.Property(i => i.Id).ValueGeneratedNever();
        invitation.Property(i => i.Token).HasMaxLength(64);
        invitation.HasIndex(i => i.Token).IsUnique();
        invitation.HasIndex(i => i.HouseholdId);
        invitation.HasIndex(i => i.CreatedById);
        invitation.HasIndex(i => i.UsedById);
        invitation.Property(i => i.Status)
            .HasMaxLength(20)
            .HasConversion(status => status.ToString().ToLowerInvariant(), key => Enum.Parse<InvitationStatus>(key, ignoreCase: true));
        invitation.HasOne<Household>()
            .WithMany()
            .HasForeignKey(i => i.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
        invitation.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(i => i.CreatedById)
            .OnDelete(DeleteBehavior.Cascade);
        invitation.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(i => i.UsedById)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>
    /// Die Tabellen "plans" und "plan_positions". Je Person und Monat gibt es höchstens einen Plan
    /// (eindeutiger Index); Pläne und ihre Posten werden mit der Person gelöscht.
    ///
    /// Ein Posten mit Buchungszeilen lässt sich nicht löschen: Der Fremdschlüssel der Zeilen ist
    /// NO ACTION. Er prüft am Ende der Anweisung, so dass das Löschen einer Person, bei dem Posten
    /// und Zeilen zusammen verschwinden, trotzdem durchgeht. Verschwindet eine Buchung, vergessen
    /// Posten und Plan sie von selbst (SET NULL bei Haken-Buchung und Übertrag).
    /// </summary>
    private static void ConfigurePlans(ModelBuilder modelBuilder)
    {
        var plan = modelBuilder.Entity<Plan>();
        plan.ToTable("plans", table =>
        {
            table.HasCheckConstraint("ck_plans_month", "month BETWEEN 1 AND 12");
            table.HasCheckConstraint("ck_plans_quotas", "quota_needs + quota_wants + quota_savings = 100");
        });
        plan.Ignore(p => p.Quotas);
        plan.Ignore(p => p.HasCarryOver);
        plan.HasIndex(p => new { p.OwnerId, p.Year, p.Month }).IsUnique();

        plan.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        plan.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(p => p.CarryOverIncomeBookingId)
            .OnDelete(DeleteBehavior.SetNull);
        plan.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(p => p.CarryOverExpenseBookingId)
            .OnDelete(DeleteBehavior.SetNull);

        plan.HasMany(p => p.Positions)
            .WithOne()
            .HasForeignKey(PositionPlan)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        var position = modelBuilder.Entity<PlanPosition>();
        position.ToTable("plan_positions", table =>
        {
            table.HasCheckConstraint("ck_plan_positions_amount_positive", "amount_planned > 0");
            table.HasCheckConstraint("ck_plan_positions_due_day", "due_day BETWEEN 1 AND 31");
        });
        position.Ignore(p => p.IsDone);

        // Plan und Posten bekommen ihre Kennung schon in der Domäne (Plan.CreateFrom). Ohne diesen
        // Hinweis hielte EF Core einen neuen Posten, der an einen geladenen Plan gehängt wird, für
        // einen vorhandenen und wollte ihn ändern statt anlegen.
        plan.Property(p => p.Id).ValueGeneratedNever();
        position.Property(p => p.Id).ValueGeneratedNever();

        // Wie bei den Verträgen: breiter als die 100 Zeichen der API, weil der Name aus dem Vertrag kommt.
        position.Property(p => p.Label).HasMaxLength(200);
        position.Property(p => p.AmountPlanned).HasPrecision(12, 2);
        position.Property(p => p.Type).HasConversion<string>();
        position.Property(p => p.Pot)
            .HasMaxLength(20)
            .HasConversion(pot => pot.ToString().ToLowerInvariant(), key => Enum.Parse<Pot>(key, ignoreCase: true));
        position.Property(p => p.PaymentMethod)
            .HasMaxLength(20)
            .HasConversion(method => PaymentMethodKeys.ToKey(method), key => PaymentMethodKeys.Parse(key));

        position.HasIndex(p => p.CommitmentId);
        position.HasIndex(p => p.CategoryId);
        position.HasIndex(p => p.AccountId);
        position.HasIndex(p => p.CounterAccountId);
        position.HasIndex(p => p.TickBookingId);
        position.HasIndex(p => p.MovedToPositionId);
        position.HasIndex(p => p.MovedFromPositionId);
        position.Ignore(p => p.IsMoved);

        // Verschieben (Spec 136): Kopie und Original verweisen aufeinander; fällt eine Seite weg, bleibt die andere ohne Verweis.
        position.HasOne<PlanPosition>()
            .WithMany()
            .HasForeignKey(p => p.MovedToPositionId)
            .OnDelete(DeleteBehavior.SetNull);
        position.HasOne<PlanPosition>()
            .WithMany()
            .HasForeignKey(p => p.MovedFromPositionId)
            .OnDelete(DeleteBehavior.SetNull);

        position.HasOne<Commitment>()
            .WithMany()
            .HasForeignKey(p => p.CommitmentId)
            .OnDelete(DeleteBehavior.NoAction);
        position.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
        position.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.NoAction);
        position.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.CounterAccountId)
            .OnDelete(DeleteBehavior.NoAction);
        position.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(p => p.TickBookingId)
            .OnDelete(DeleteBehavior.SetNull);

        var line = modelBuilder.Entity<BookingLine>();
        line.HasIndex(l => l.PositionId);
        line.HasOne<PlanPosition>()
            .WithMany()
            .HasForeignKey(l => l.PositionId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    /// <summary>
    /// Die Tabelle "commitments". Die Art steht wie bisher als Name des Enums in der Tabelle
    /// ("Contract"); Topf und Zahlungsart stehen klein geschrieben, so wie die API sie schickt
    /// ("needs", "direct_debit").
    ///
    /// Kategorien und Konten werden nie unter einem Vertrag gelöscht: Die Fremdschlüssel dorthin
    /// sind NO ACTION und prüfen erst am Ende der Transaktion (DEFERRABLE, von Hand in der
    /// Migration AddCommitmentDetails), genau wie bei den Buchungen.
    /// </summary>
    private static void ConfigureCommitments(ModelBuilder modelBuilder)
    {
        var commitment = modelBuilder.Entity<Commitment>();

        commitment.ToTable("commitments", table =>
        {
            table.HasCheckConstraint("ck_commitments_two_different_accounts", "counter_account_id IS NULL OR account_id IS NULL OR counter_account_id <> account_id");
        });

        commitment.Property(c => c.Amount).HasPrecision(12, 2);
        commitment.Property(c => c.TargetAmount).HasPrecision(12, 2);

        // Die Spalte ist breiter als die 100 Zeichen, die die API erlaubt; so bleiben ältere,
        // längere Bezeichnungen aus der Lernzeit erhalten.
        commitment.Property(c => c.Name).HasMaxLength(200);

        commitment.Property(c => c.Type).HasConversion<string>();

        commitment.Property(c => c.Pot)
            .HasMaxLength(20)
            .HasConversion(pot => pot.ToString().ToLowerInvariant(), key => Enum.Parse<Pot>(key, ignoreCase: true));

        commitment.Property(c => c.PaymentMethod)
            .HasMaxLength(20)
            .HasConversion(method => PaymentMethodKeys.ToKey(method), key => PaymentMethodKeys.Parse(key));

        commitment.HasIndex(c => c.CategoryId);
        commitment.HasIndex(c => c.AccountId);
        commitment.HasIndex(c => c.CounterAccountId);

        // Jeder Vertrag gehört einem Benutzer. Die Datenbank prüft, dass es den
        // Benutzer wirklich gibt, und löscht seine Verträge mit, wenn er gelöscht wird.
        commitment.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        commitment.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
        commitment.HasOne<Account>()
            .WithMany()
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.NoAction);
        commitment.HasOne<Account>()
            .WithMany()
            .HasForeignKey(c => c.CounterAccountId)
            .OnDelete(DeleteBehavior.NoAction);
        commitment.HasIndex(c => c.SavingsGoalId);
        commitment.HasOne<SavingsGoal>()
            .WithMany()
            .HasForeignKey(c => c.SavingsGoalId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    /// <summary>
    /// Die Tabelle "debts" (Issue 31). Jeder Eintrag gehört einer Person und verschwindet mit ihr. Buchungszeilen und Verträge
    /// zeigen mit NO ACTION auf einen Eintrag: Einer mit Zuordnungen lässt sich nicht löschen.
    /// </summary>
    private static void ConfigureDebts(ModelBuilder modelBuilder)
    {
        var debt = modelBuilder.Entity<Debt>();
        debt.ToTable("debts", table =>
        {
            table.HasCheckConstraint("ck_debts_amount_positive", "original_amount > 0");
            table.HasCheckConstraint("ck_debts_kind", "kind IN ('claim', 'loan')");
        });
        debt.Property(d => d.Kind).HasMaxLength(10)
            .HasConversion(kind => kind.ToString().ToLowerInvariant(), key => Enum.Parse<DebtKind>(key, ignoreCase: true));
        debt.Property(d => d.Name).HasMaxLength(100);
        debt.Property(d => d.Creditor).HasMaxLength(100);
        debt.Property(d => d.Note).HasMaxLength(200);
        debt.Property(d => d.OriginalAmount).HasPrecision(12, 2);
        debt.HasIndex(d => d.OwnerId);
        debt.HasOne<AppUser>().WithMany().HasForeignKey(d => d.OwnerId).OnDelete(DeleteBehavior.Cascade);

        var line = modelBuilder.Entity<BookingLine>();
        line.HasIndex(l => l.DebtId);
        line.HasOne<Debt>().WithMany().HasForeignKey(l => l.DebtId).OnDelete(DeleteBehavior.NoAction);

        var commitment = modelBuilder.Entity<Commitment>();
        commitment.HasIndex(c => c.DebtId);
        commitment.HasOne<Debt>().WithMany().HasForeignKey(c => c.DebtId).OnDelete(DeleteBehavior.NoAction);
    }

    /// <summary>
    /// Die Tabelle "savings_goals" (Issue 30). Jedes Ziel gehört einer Person und verschwindet mit ihr. Das Konto darf nie
    /// unter einem Ziel verschwinden (NO ACTION). Buchungszeilen und Verträge zeigen mit NO ACTION auf ein Ziel: Ein Ziel
    /// mit Zuordnungen lässt die API nicht löschen.
    /// </summary>
    private static void ConfigureSavingsGoals(ModelBuilder modelBuilder)
    {
        var goal = modelBuilder.Entity<SavingsGoal>();
        goal.ToTable("savings_goals", table =>
        {
            table.HasCheckConstraint("ck_savings_goals_target_positive", "target_amount IS NULL OR target_amount > 0");
            table.HasCheckConstraint("ck_savings_goals_start_not_negative", "start_amount >= 0");
        });
        goal.Property(g => g.Name).HasMaxLength(100);
        goal.Property(g => g.TargetAmount).HasPrecision(12, 2);
        goal.Property(g => g.StartAmount).HasPrecision(12, 2).HasDefaultValue(0m);
        goal.HasIndex(g => g.OwnerId);
        goal.HasIndex(g => g.AccountId);
        goal.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(g => g.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        goal.HasOne<Account>()
            .WithMany()
            .HasForeignKey(g => g.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        var line = modelBuilder.Entity<BookingLine>();
        line.HasIndex(l => l.SavingsGoalId);
        line.HasOne<SavingsGoal>()
            .WithMany()
            .HasForeignKey(l => l.SavingsGoalId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    /// <summary>
    /// Die Tabellen "bookings" und "booking_lines". Der Betrag steht positiv in der Tabelle; die
    /// Richtung ergibt sich aus der Umbuchung oder dem Topf der Zeilen. Die Art (Ausgabe,
    /// Einnahme, Umbuchung) ist deshalb keine eigene Spalte.
    ///
    /// Konten und Kategorien werden nie unter einer Buchung gelöscht: Die Fremdschlüssel dorthin
    /// sind NO ACTION und prüfen erst am Ende der Transaktion (DEFERRABLE, von Hand in der
    /// Migration AddBookings). Wird eine Person gelöscht, verschwinden ihre Konten, Kategorien und
    /// Buchungen in einem Rutsch, und die Prüfung am Ende lässt das zu.
    /// </summary>
    private static void ConfigureBookings(ModelBuilder modelBuilder)
    {
        var booking = modelBuilder.Entity<Booking>();

        booking.ToTable("bookings", table =>
        {
            table.HasCheckConstraint("ck_bookings_amount_positive", "amount > 0");
            table.HasCheckConstraint("ck_bookings_two_different_accounts", "counter_account_id IS NULL OR counter_account_id <> account_id");
        });

        booking.Property(b => b.PaymentDate).HasColumnName("date");
        booking.Property(b => b.Amount).HasPrecision(12, 2);
        booking.Property(b => b.Note).HasMaxLength(200);

        // Nur bei einer Umbuchung: der Zieltopf (Spec 72, R9); null bei allen anderen und bei älteren Umbuchungen.
        booking.Property(b => b.TargetPot)
            .HasColumnName("target_pot")
            .HasMaxLength(20)
            .HasConversion(
                pot => pot == null ? null : pot.Value.ToString().ToLowerInvariant(),
                key => key == null ? null : Enum.Parse<Pot>(key, true));

        // Die Bezeichnung stammt aus der Zeit vor den Kategorien und wird nicht gespeichert.
        booking.Ignore(b => b.Name);

        booking.HasIndex(b => new { b.OwnerId, b.PaymentDate });
        booking.HasIndex(b => new { b.OwnerId, b.PlanYear, b.PlanMonth });
        booking.HasIndex(b => b.AccountId);
        booking.HasIndex(b => b.CounterAccountId);

        booking.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(b => b.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        booking.HasOne<Account>()
            .WithMany()
            .HasForeignKey(b => b.AccountId)
            .OnDelete(DeleteBehavior.NoAction);
        booking.HasOne<Account>()
            .WithMany()
            .HasForeignKey(b => b.CounterAccountId)
            .OnDelete(DeleteBehavior.NoAction);

        // Die Zeilen gehören zur Buchung und werden mit ihr gelöscht.
        booking.HasMany(b => b.Lines)
            .WithOne()
            .HasForeignKey("BookingId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        var line = modelBuilder.Entity<BookingLine>();
        line.ToTable("booking_lines", table =>
        {
            table.HasCheckConstraint("ck_booking_lines_amount_positive", "amount > 0");
            table.HasCheckConstraint("ck_booking_lines_position", "position BETWEEN 0 AND 19");
        });
        line.Property(l => l.Amount).HasPrecision(12, 2);
        line.Property(l => l.Pot)
            .HasMaxLength(20)
            .HasConversion(pot => pot.ToString().ToLowerInvariant(), key => Enum.Parse<Pot>(key, ignoreCase: true));
        line.Property<int>(LinePosition);
        line.HasIndex(l => l.CategoryId);
        line.HasOne<Category>()
            .WithMany()
            .HasForeignKey(l => l.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    /// <summary>
    /// Die Tabelle "accounts". Die Art steht als kleingeschriebener Text in der Tabelle
    /// ("checking"), so wie sie auch die API schickt. Den Kontostand gibt es hier nicht;
    /// er wird aus den Buchungen ausgerechnet.
    /// </summary>
    private static void ConfigureAccounts(ModelBuilder modelBuilder)
    {
        var account = modelBuilder.Entity<Account>();

        account.ToTable("accounts", table =>
        {
            // Das Standardkonto ist immer ein aktives Konto; wer ein Konto auflöst, nimmt ihm die Markierung.
            table.HasCheckConstraint("ck_accounts_default_is_active", "NOT is_default OR active");
        });

        account.Property(a => a.Name).HasMaxLength(100);
        account.Property(a => a.OpeningBalance).HasPrecision(12, 2);
        account.Property(a => a.Iban).HasMaxLength(Iban.MaxLength);
        account.Property(a => a.Icon).HasMaxLength(20);

        account.Property(a => a.Type)
            .HasMaxLength(20)
            .HasConversion(type => type.ToString().ToLowerInvariant(), key => Enum.Parse<AccountType>(key, ignoreCase: true));

        // Die Liste der Buchungen füllt die API für die Rechnung des Stands; sie ist keine Spalte.
        account.Ignore(a => a.Bookings);

        // Jede Abfrage sucht die Konten einer Person.
        account.HasIndex(a => a.OwnerId, "ix_accounts_owner_id")
            .HasDatabaseName("ix_accounts_owner_id");

        // Höchstens ein Standardkonto je Person: ein eindeutiger Index nur über die Zeilen mit is_default.
        account.HasIndex(a => a.OwnerId, "ux_accounts_one_default_per_owner")
            .IsUnique()
            .HasFilter("is_default")
            .HasDatabaseName("ux_accounts_one_default_per_owner");

        // Die Konten einer Person werden mit ihr gelöscht.
        account.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(a => a.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// Die Tabelle "categories". Gruppe und Topf stehen als kleingeschriebener Text
    /// in der Tabelle ("household", "needs"), so wie sie auch die API schickt.
    /// </summary>
    private static void ConfigureCategories(ModelBuilder modelBuilder)
    {
        var category = modelBuilder.Entity<Category>();

        category.ToTable("categories", table =>
        {
            // Entweder eingebaut (Schlüssel, kein Besitzer, kein eigener Name) oder
            // eigen (Besitzer und Name, kein Schlüssel). Etwas dazwischen gibt es nicht.
            table.HasCheckConstraint(
                "ck_categories_built_in_or_own",
                "(owner_id IS NULL AND key IS NOT NULL AND name IS NULL) OR (owner_id IS NOT NULL AND key IS NULL AND name IS NOT NULL)");
        });

        category.Property(c => c.Key).HasMaxLength(60);
        category.Property(c => c.Name).HasMaxLength(CategoryNameMaxLength);

        category.Property(c => c.Group)
            .HasMaxLength(20)
            .HasConversion(group => CategoryGroups.ToKey(group), key => Enum.Parse<CategoryGroup>(key, ignoreCase: true));

        category.Property(c => c.SuggestedPot)
            .HasMaxLength(20)
            .HasConversion(pot => pot.ToString().ToLowerInvariant(), key => Enum.Parse<Pot>(key, ignoreCase: true));

        // Der Schlüssel einer eingebauten Kategorie ist eindeutig; eigene haben keinen.
        category.HasIndex(c => c.Key).IsUnique();

        // Die eigenen Kategorien einer Person werden mit ihr gelöscht.
        category.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Die eingebauten Kategorien kommen aus dem Katalog der Domäne und werden von
        // der Migration angelegt. Neue Einträge im Katalog brauchen eine neue Migration.
        category.HasData(CategoryCatalog.All.Select(built => new
        {
            built.Id,
            built.Key,
            built.Group,
            built.SuggestedPot,
        }));
    }

    private const int CategoryNameMaxLength = 60;
}
