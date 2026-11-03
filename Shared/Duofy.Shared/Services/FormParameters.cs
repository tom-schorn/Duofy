using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace Duofy.Shared.Services;

/// <summary>
/// Die Parameter eines Dialogs, beim Übersetzen geprüft: Der Dialogdienst nimmt ein Dictionary mit Namen als Text,
/// hier kommen Name und Typ aus der Eigenschaft des Dialogs. Ein Tippfehler oder ein falscher Typ fällt
/// also nicht erst zur Laufzeit auf.
/// <code>new FormParameters&lt;ServerDialog&gt;().Add(d =&gt; d.Title, "Server")</code>
/// </summary>
public sealed class FormParameters<TDialog> where TDialog : ComponentBase
{
    private readonly Dictionary<string, object?> values = [];

    public FormParameters<TDialog> Add<TValue>(Expression<Func<TDialog, TValue>> property, TValue value)
    {
        if (property.Body is not MemberExpression { Member: var member })
        {
            throw new ArgumentException("Der Ausdruck muss eine Eigenschaft des Dialogs nennen, zum Beispiel d => d.Title.", nameof(property));
        }

        values[member.Name] = value;
        return this;
    }

    /// <summary>Das Dictionary, das <c>DialogService.OpenAsync</c> erwartet.</summary>
    public Dictionary<string, object?> ToDictionary() => new(values);
}
