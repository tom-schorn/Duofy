using System.Threading.RateLimiting;
using Duofy.Api;
using Duofy.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Eine Anmeldung hält sieben Tage. Jede Erneuerung gibt ein neues Refresh-Token aus,
// das wieder sieben Tage gilt: Wer die App oder Web öfter als alle sieben Tage benutzt,
// bleibt angemeldet (Spec 97).
var RefreshTokenLifetime = TimeSpan.FromDays(7);

// Die Verbindung zur Datenbank. Die Verbindungszeichenfolge kommt aus der
// Umgebungsvariable ConnectionStrings__Duofy oder aus dotnet user-secrets.
// Sie wird erst beim ersten Zugriff gelesen, damit Tests sie vorher setzen können.
builder.Services.AddDbContext<DuofyDbContext>((services, options) => options
    .UseNpgsql(services.GetRequiredService<IConfiguration>().GetConnectionString("Duofy"))
    .UseSnakeCaseNamingConvention());

// --- Anmeldung -------------------------------------------------------------
// Web und App melden sich mit Tokens an, nicht mit einem Cookie. Identity bringt
// dafür das Verfahren "BearerToken" mit: Die Tokens sind mit den Schlüsseln des
// Servers verschlüsselt, ein verändertes Token wird abgelehnt.
builder.Services
    .AddAuthentication(IdentityConstants.BearerScheme)
    .AddBearerToken(IdentityConstants.BearerScheme, options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
        options.RefreshTokenExpiration = RefreshTokenLifetime;
    });
builder.Services.AddAuthorization();
// Die Schlüssel der Tokens überleben einen Neustart, wenn Duofy:KeysDirectory gesetzt ist (Spec 79, siehe KeyRing).
builder.Services.AddKeyRing();

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        // Der Benutzername ist die E-Mail-Adresse; sie darf jedes Zeichen enthalten,
        // das eine Adresse enthalten kann.
        options.User.AllowedUserNameCharacters = "";

        // Mindestens 10 Zeichen und sonst keine Vorschriften, wie es das NIST heute rät.
        options.Password.RequiredLength = Duofy.Schemas.AccountRules.PasswordMinLength;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        // Nach fünf falschen Passwörtern ist das Konto fünf Minuten gesperrt.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

        // Die bestätigte Adresse verlangt die Anmeldung selbst, und nur auf der gehosteten Version (Spec 53):
        // Identity würde sonst schon vor der Prüfung des Passworts verraten, dass es das Konto gibt.
        options.SignIn.RequireConfirmedAccount = false;

        // Bestätigen einer Adresse (Registrierung, Wechsel) mit 24 Stunden, "Passwort vergessen" mit einer.
        options.Tokens.EmailConfirmationTokenProvider = ConfirmationTokenProvider.ProviderName;
        options.Tokens.ChangeEmailTokenProvider = ConfirmationTokenProvider.ProviderName;
    })
    .AddEntityFrameworkStores<DuofyDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddTokenProvider<ConfirmationTokenProvider>(ConfirmationTokenProvider.ProviderName);
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = ConfirmationTokenProvider.ResetLifetime);

// --- Bremse für die Anmeldung ----------------------------------------------
// Höchstens N Anfragen pro Minute und Absenderadresse an /api/v1/auth/*, danach 429.
// Hinter dem Tunnel ist die Absenderadresse die aus CF-Connecting-IP (Spec 79, siehe ForwardedClients).
builder.Services.AddForwardedClients();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, context =>
    {
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        // Ausgenommen sind die Adressen in RateLimiting:ExemptAddresses, gedacht für den Web-Container: Er ruft die
        // API für alle Web-Nutzer, die sich sonst ein Budget teilten. Vor ihm bremst Cloudflare je Person (Spec 79).
        if (ForwardedClients.ConnectionIsFrom(context, configuration["RateLimiting:ExemptAddresses"]))
        {
            return RateLimitPartition.GetNoLimiter("exempt");
        }
        var permitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
        var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICategoryLookup, DbCategoryLookup>();
builder.Services.AddScoped<IAccountLookup, DbAccountLookup>();
builder.Services.AddScoped<IPositionLookup, DbPositionLookup>();
builder.Services.AddScoped<PlanStore>();
builder.Services.AddScoped<HouseholdStore>();
builder.Services.AddScoped<HouseholdPlanStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAccess, DbAccess>();
builder.Services.AddScoped<OwnerContext>();
builder.Services.AddScoped<IOwnerContext>(services => services.GetRequiredService<OwnerContext>());
builder.Services.AddSingleton<PublicLinks>();
builder.Services.AddSingleton<LegalDocuments>();
builder.Services.AddSingleton<InstanceSettings>();
builder.Services.AddScoped<InstanceAdmins>();
builder.Services.AddSingleton<HostedSettings>();
builder.Services.AddSingleton<IMailSender, SmtpMailSender>();
builder.Services.AddScoped<Mailer>();
builder.Services.AddSingleton<MailQueue>();
builder.Services.AddHostedService<MailDispatcher>();
builder.Services.AddSingleton<MailThrottle>();
// Erinnerungen an fällige Posten (Spec 84): nur gehostet und mit Duofy:Reminders:Enabled. FCM (Android) und WNS (Windows)
// sind angebunden, sobald ihre Zugangsdaten gesetzt sind (Dienstkonto-Datei, Client-Kennung und Geheimnis aus der Umgebung);
// ohne sie schickt UnconfiguredPush nichts und sagt es im Log, damit ein Server ohne Zugang nicht still bleibt.
builder.Services.AddSingleton<ReminderSwitch>();
builder.Services.AddSingleton<UnconfiguredPush>();
if (!string.IsNullOrWhiteSpace(builder.Configuration[PushSettings.FcmServiceAccountFile]))
{
    builder.Services.AddSingleton<IFcmClient, FcmClient>();
}
else
{
    builder.Services.AddSingleton<IFcmClient>(services => services.GetRequiredService<UnconfiguredPush>());
}
if (!string.IsNullOrWhiteSpace(builder.Configuration[PushSettings.WnsClientId]))
{
    builder.Services.AddHttpClient<IWnsClient, WnsClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
}
else
{
    builder.Services.AddSingleton<IWnsClient>(services => services.GetRequiredService<UnconfiguredPush>());
}
builder.Services.AddSingleton<PushSender>();
builder.Services.AddScoped<ReminderRunner>();
builder.Services.AddHostedService<ReminderJob>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddProblemDetails();

var app = builder.Build();

// Eine gehostete Version ohne vollständige Mail-Einstellungen startet nicht (Spec 53, R1).
var hosted = app.Services.GetRequiredService<HostedSettings>();
hosted.StopIfIncomplete();

// Neue Migrationen werden beim Start eingespielt.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DuofyDbContext>();
    using (db.Owners.Bypass("Migrationen beim Start"))
    {
        db.Database.Migrate();
    }
    scope.ServiceProvider.GetRequiredService<InstanceSettings>().WarnIfNoAdministratorCanArise(db);
}

// Zuerst: Alles danach (Bremse, Log) sieht die Adresse der Person statt der des Tunnels.
app.UseForwardedClients();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseAuthentication();
app.UseOwnerFilter();
app.UseAuthorization();
app.UseRateLimiter();

var v1 = app.MapGroup("/api/v1");
v1.MapHealthChecks("/health");
v1.MapInstanceEndpoints();
v1.MapLegalEndpoints();
v1.MapAuthEndpoints();
v1.MapUserEndpoints();
v1.MapCategoryEndpoints();
v1.MapAccountEndpoints();
v1.MapBookingEndpoints();
v1.MapCommitmentEndpoints();
v1.MapSavingsGoalEndpoints();
v1.MapDebtEndpoints();
v1.MapPlanEndpoints();
v1.MapHouseholdEndpoints();
v1.MapHouseholdPlanEndpoints();
v1.MapReminderEndpoints();
v1.MapInvitationEndpoints();
v1.MapAdminEndpoints();
// Die Mail-Wege gibt es nur auf der gehosteten Version, den Link des Administrators nur ohne sie (Spec 53).
if (hosted.Hosted)
{
    v1.MapMailEndpoints();
}
else
{
    v1.MapResetLinkEndpoints();
}

app.Run();

/// <summary>Macht die Klasse für die Tests sichtbar (WebApplicationFactory).</summary>
public partial class Program;
