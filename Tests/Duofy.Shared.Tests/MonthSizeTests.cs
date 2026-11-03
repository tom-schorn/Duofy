namespace Duofy.Shared.Tests;

/// <summary>Größen im Monat nach Toms Durchsicht vom 03.11.2026: Kennzahl-Kästen am Handy, Tageskurve.</summary>
public class MonthSizeTests
{
    private static string Css => File.ReadAllText(RepositoryFiles.Path("Shared", "Duofy.Shared", "wwwroot", "duofy.css")).Replace("\r\n", "\n");

    // Tom, 03.11.2026: Am Handy ist ein Kennzahl-Kasten nur so hoch wie sein Inhalt.
    [Fact]
    public void On_the_phone_each_metric_box_is_only_as_high_as_its_content()
    {
        var css = Css;
        var phone = css[css.IndexOf("    .metrics {\n        display: flex;", StringComparison.Ordinal)..];
        phone = phone[..phone.IndexOf("\n}\n", StringComparison.Ordinal)];
        Assert.Contains(".metrics { align-items: flex-start; }", phone);
        Assert.Contains(".metrics .metric { height: auto;", phone);
    }

    // Tom, 03.11.2026: Sankey weg; nichts im Stylesheet erinnert noch daran.
    [Fact]
    public void The_stylesheet_has_no_flow_chart_rules()
    {
        Assert.DoesNotContain(".sankey-", Css);
        Assert.DoesNotContain(".flow-", Css);
    }
}
