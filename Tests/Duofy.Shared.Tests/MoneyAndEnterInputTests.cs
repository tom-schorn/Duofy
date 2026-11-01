using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Duofy.Schemas;
using Duofy.Shared.Pages;
using Microsoft.AspNetCore.Components.Web;

namespace Duofy.Shared.Tests;

/// <summary>
/// Review von Issue 41: Ein Betrag darf nicht von der Sprache des Rechners abhängen. Im Container ohne gesetzte Sprache wurde
/// aus 12,34 sonst 1234,00. Und Enter im Passwortfeld schickt, was gerade dort steht, nicht den Wert vom letzten Verlassen.
/// </summary>
public partial class MoneyAndEnterInputTests : PageTestContext
{
    private static T UnderInvariantCulture<T>(Func<T> action)
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void The_opening_balance_stays_12_34_without_a_system_language()
    {
        var value = UnderInvariantCulture(() =>
        {
            var cut = Render<AccountDialog>();
            Field(cut, "account-opening-balance").Change("12,34");
            return Field(cut, "account-opening-balance").GetAttribute("value");
        });

        Assert.Equal("12,34", value);
    }

    [Fact]
    public void The_contract_amount_stays_12_34_without_a_system_language()
    {
        var value = UnderInvariantCulture(() =>
        {
            var cut = Render<CommitmentDialog>(p => p.Add(x => x.Today, new DateOnly(2026, 10, 21)));
            Field(cut, "commitment-amount").Change("12,34");
            return Field(cut, "commitment-amount").GetAttribute("value");
        });

        Assert.Equal("12,34", value);
    }

    // Jedes Zahlenfeld mit Nachkommastellen nennt seine Sprache selbst, auch künftige.
    [Fact]
    public void Every_decimal_field_names_its_culture()
    {
        var findings = new List<string>();
        foreach (var file in Directory.EnumerateFiles(RepositoryFiles.Path("Shared", "Duofy.Shared"), "*.razor", SearchOption.AllDirectories)
                     .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")))
        {
            foreach (Match field in DecimalField().Matches(File.ReadAllText(file)))
            {
                if (!field.Value.Contains("Culture=", StringComparison.Ordinal))
                {
                    findings.Add($"{Path.GetFileName(file)}: {field.Value[..Math.Min(70, field.Value.Length)]}");
                }
            }
        }

        Assert.True(findings.Count == 0, string.Join("\n", findings));
    }

    [Fact]
    public void Enter_in_the_login_password_sends_what_was_just_typed()
    {
        var cut = Render<Login>();
        Field(cut, "login-email").Input("anna@example.org");
        Field(cut, "login-password").Input("ein-langes-passwort");

        Field(cut, "login-password").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Equal(new LoginRequest("anna@example.org", "ein-langes-passwort"), Assert.Single(Auth.LoggedIn)));
    }

    [Fact]
    public void Enter_in_the_delete_password_sends_what_was_just_typed()
    {
        var cut = Render<DeleteAccount>();
        Field(cut, "delete-password").Input("ein-langes-passwort");

        Field(cut, "delete-password").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Equal(new DeleteMeRequest("ein-langes-passwort"), Assert.Single(Users.Deleted)));
    }

    [GeneratedRegex(@"<RadzenNumeric\s+TValue=""decimal\??"".*?/>", RegexOptions.Singleline)]
    private static partial Regex DecimalField();
}
