using System.Reflection;
using System.Xml.Linq;
using Bunit;
using Duofy.Shared.Pages;
using Duofy.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

/// <summary>Die Lizenzen der Bausteine anderer Leute (Spec 67: QRCoder 1.8.0, MIT, liefert seinen Hinweis mit).</summary>
public class LicensesPageTests : PageTestContext
{
    [Fact]
    public void Mehr_leads_to_the_licenses()
    {
        var cut = Render<Mehr>();

        Assert.Equal("/lizenzen", cut.Find("[data-testid='more-licenses']").GetAttribute("href"));
        Assert.Equal("/lizenzen", typeof(Licenses).GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Fact]
    public void The_page_shows_the_mit_notice_of_qrcoder()
    {
        var cut = Render<Licenses>();

        var qrcoder = cut.Find("[data-testid='license-QRCoder']").TextContent;
        Assert.Contains("QRCoder 1.8.0", qrcoder);
        Assert.Contains("MIT", qrcoder);
        Assert.Contains("Copyright (c) 2013-2025 Raffael Herrmann", qrcoder);
        Assert.Contains("THE SOFTWARE IS PROVIDED \"AS IS\"", qrcoder);
    }

    // Spec 53: Die gehostete Version verschickt Mails mit MailKit, das MimeKit mitbringt; beide MIT.
    [Theory]
    [InlineData("MailKit")]
    [InlineData("MimeKit")]
    public void The_page_shows_the_mit_notice_of_the_mail_packages(string name)
    {
        var cut = Render<Licenses>();

        var notice = cut.Find($"[data-testid='license-{name}']").TextContent;
        Assert.Contains($"{name} 4.18.1", notice);
        Assert.Contains("MIT", notice);
        Assert.Contains("-2026 .NET Foundation and Contributors", notice);
        Assert.Contains("Copyright (C) 20", notice);
        Assert.Contains("THE SOFTWARE IS PROVIDED \"AS IS\"", notice);
    }

    [Fact]
    public void The_listed_mailkit_version_is_the_one_the_api_pins()
    {
        var project = XDocument.Load(Path.Combine(RepositoryFiles.Root, "Backend", "Duofy.Backend", "Duofy.Backend.csproj"));
        var reference = project.Descendants("PackageReference").Single(e => (string?)e.Attribute("Include") == "MailKit");

        Assert.Equal($"[{Assert.Single(ThirdPartyLicenses.All, l => l.Name == "MailKit").Version}]", (string?)reference.Attribute("Version"));
    }

    // Die Liste nennt dieselbe Version, die das Projekt einbindet; sonst stünde ein falscher Hinweis da.
    [Fact]
    public void The_listed_version_is_the_referenced_one()
    {
        var project = XDocument.Load(Path.Combine(RepositoryFiles.Root, "Shared", "Duofy.Shared", "Duofy.Shared.csproj"));
        var reference = project.Descendants("PackageReference").Single(e => (string?)e.Attribute("Include") == "QRCoder");

        var listed = Assert.Single(ThirdPartyLicenses.All, l => l.Name == "QRCoder");

        Assert.Equal((string?)reference.Attribute("Version"), listed.Version);
    }

    // Radzen-Umstellung R2: Radzen, Inter und die Material Symbols stehen mit ihren Hinweisen auf der Seite.
    [Theory]
    [InlineData("Radzen.Blazor", "12.1.8", "MIT", "Copyright (c) 2018-2026 Radzen Ltd")]
    [InlineData("Inter", "5.3.0", "SIL Open Font License 1.1", "The Inter Project Authors")]
    [InlineData("Material Symbols", "Outlined", "Apache License 2.0", "Copyright 2025 Google LLC")]
    public void The_page_shows_the_notices_of_radzen_inter_and_material_symbols(string name, string version, string license, string copyright)
    {
        var cut = Render<Licenses>();

        var notice = cut.Find($"[data-testid='license-{name}']").TextContent;
        Assert.Contains(name, notice);
        Assert.Contains(version, notice);
        Assert.Contains(license, notice);
        Assert.Contains(copyright, notice);
    }

    [Fact]
    public void The_page_is_built_with_radzen_only()
    {
        var cut = Render<Licenses>();

        Assert.Contains("rz-", cut.Markup);
    }

    [Fact]
    public void Radzen_is_pinned_to_the_version_the_plan_names()
    {
        var project = XDocument.Load(Path.Combine(RepositoryFiles.Root, "Shared", "Duofy.Shared", "Duofy.Shared.csproj"));
        var reference = project.Descendants("PackageReference").Single(e => (string?)e.Attribute("Include") == "Radzen.Blazor");

        Assert.Equal("12.1.8", (string?)reference.Attribute("Version"));
        Assert.Equal("12.1.8", Assert.Single(ThirdPartyLicenses.All, l => l.Name == "Radzen.Blazor").Version);
    }

    [Fact]
    public void The_ui_registration_adds_the_radzen_services()
    {
        Assert.NotNull(Services.GetService(typeof(Radzen.DialogService)));
        Assert.NotNull(Services.GetService(typeof(Radzen.NotificationService)));
    }
}
