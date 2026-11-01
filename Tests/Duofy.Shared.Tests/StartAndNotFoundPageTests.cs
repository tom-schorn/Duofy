using System.Reflection;
using Bunit;
using Duofy.Shared.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Tests;

public class StartAndNotFoundPageTests : PageTestContext
{
    [Fact]
    public void Start_greets_the_person_by_first_name()
    {
        var cut = Render<Start>();

        cut.WaitForAssertion(() => Assert.Contains("Hallo, Anna!", cut.Markup));
    }

    [Fact]
    public void Start_is_the_root_page_and_needs_a_sign_in()
    {
        Assert.Equal("/", typeof(Start).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.NotNull(typeof(Start).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void Not_found_says_so_in_german_and_links_to_start()
    {
        var cut = Render<NotFound>();

        Assert.Contains("Seite nicht gefunden", cut.Markup);
        Assert.Equal("/", cut.Find("a").GetAttribute("href"));
    }

    // Radzen-Umstellung R13
    [Fact]
    public void Not_found_is_built_with_radzen_and_its_link_looks_like_a_button()
    {
        var cut = Render<NotFound>();

        Assert.Equal("Seite nicht gefunden", cut.Find("h1").TextContent.Trim());
        Assert.Contains("rz-button", cut.Find("a[href='/']").ClassList);
    }

    [Fact]
    public void Start_has_its_main_heading()
    {
        var cut = Render<Start>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("h1")));
    }
}
