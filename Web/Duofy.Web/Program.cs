using System.Globalization;
using Duofy.Shared.Blocks;
using Duofy.Shared.Services;
using Duofy.Web.Components;
using Duofy.Web.Services;
using Duofy.Web.Session;

// Das Image setzt keine Sprache. Ohne feste Standardsprache hingen Zahlen und Daten vom Rechner ab (Review von Issue 41).
var german = new CultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = german;
CultureInfo.DefaultThreadCurrentUICulture = german;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add device-specific services used by the Duofy.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// --- Name und Logo ---------------------------------------------------------
// Duofy:AppName (Umgebungsvariable Duofy__AppName) nennt die Marke ausdrücklich; ohne sie gilt Duofy:Hosted, derselbe
// Wert wie bei der API: gehostet heißt Plagevo, sonst Duofy. Vor AddDuofyUi, das nur eine Voreinstellung anmeldet.
builder.Services.AddSingleton(AppBrand.FromSettings(
    builder.Configuration["Duofy:AppName"],
    builder.Configuration.GetValue("Duofy:Hosted", false)));

// --- Gemeinsame Oberfläche -------------------------------------------------
// Radzen, Texte und Hell/Dunkel aus Duofy.Shared.
builder.Services.AddDuofyUi();
// Nur im Entwicklungsbetrieb ist die Testseite der Bausteine erreichbar (Spec 6).
builder.Services.AddSingleton<IDevelopmentMode>(new DevelopmentMode(builder.Environment.IsDevelopment()));
builder.Services.AddScoped<IShareService, ClipboardShareService>();
// ---------------------------------------------------------------------------

// --- Duofy-API -------------------------------------------------------------
// Die Web-Variante hat keine Datenbank; alles geht über die API. Die Adresse
// kommt aus appsettings.json oder der Umgebungsvariable Duofy__ApiBaseAddress.
var apiBaseAddress = builder.Configuration["Duofy:ApiBaseAddress"];
if (!Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out var apiUri))
{
    throw new InvalidOperationException(
        $"Duofy:ApiBaseAddress ist keine gültige Adresse: '{apiBaseAddress}'. Siehe .env.example.");
}
builder.Services.AddDuofyApiClient(apiUri);

// Die Schlüssel für Formulare und den Zustand der Seiten überleben mit Duofy:KeysDirectory einen Neubau (siehe WebKeyRing).
builder.Services.AddWebKeyRing();

// --- Anmeldung -------------------------------------------------------------
// Die Tokens der API liegen in der Sitzung auf diesem Server, der Browser hält
// nur ein HttpOnly-Cookie mit der Kennung der Sitzung (siehe Session/).
builder.Services.AddWebSession();
builder.Services.AddCascadingAuthenticationState();
// ---------------------------------------------------------------------------

// --- Hinter dem Cloudflare Tunnel ------------------------------------------
// Adresse der Person und https aus den Köpfen des Tunnels, nur aus vertrauten Netzen (Spec 79).
builder.Services.AddForwardedClients();
// Für den HEALTHCHECK des Containers: /health antwortet ohne Anmeldung.
builder.Services.AddHealthChecks();
// ---------------------------------------------------------------------------

var app = builder.Build();

// Zuerst: Alles danach (HTTPS-Umleitung, Cookies, Log) sieht die Person und https statt des Tunnels.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseWebSessionCookie();
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Duofy.Shared._Imports).Assembly);

app.Run();

/// <summary>Macht die Klasse für die Tests sichtbar (WebApplicationFactory).</summary>
public partial class Program;
