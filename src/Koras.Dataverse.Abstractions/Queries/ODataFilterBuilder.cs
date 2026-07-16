using System.Globalization;
using System.Text;

namespace Koras.Dataverse.Queries;

/// <summary>
/// Builds an OData <c>$filter</c> expression with strict literal encoding: strings are quoted and
/// escaped, GUIDs/numbers/dates are rendered in their unquoted OData forms, and column names are
/// validated — user input can never change the shape of the expression.
/// </summary>
/// <remarks>This type is not thread-safe. Build queries on a single thread.</remarks>
public sealed class ODataFilterBuilder
{
    private readonly StringBuilder _expression = new();
    private readonly string _combinator;

    internal ODataFilterBuilder(bool isOr = false) => _combinator = isOr ? " or " : " and ";

    /// <summary>Adds <c>column eq value</c>.</summary>
    public ODataFilterBuilder Eq(string column, object? value) => Comparison(column, "eq", value);

    /// <summary>Adds <c>column ne value</c>.</summary>
    public ODataFilterBuilder Ne(string column, object? value) => Comparison(column, "ne", value);

    /// <summary>Adds <c>column gt value</c>.</summary>
    public ODataFilterBuilder Gt(string column, object value) => Comparison(column, "gt", value);

    /// <summary>Adds <c>column ge value</c>.</summary>
    public ODataFilterBuilder Ge(string column, object value) => Comparison(column, "ge", value);

    /// <summary>Adds <c>column lt value</c>.</summary>
    public ODataFilterBuilder Lt(string column, object value) => Comparison(column, "lt", value);

    /// <summary>Adds <c>column le value</c>.</summary>
    public ODataFilterBuilder Le(string column, object value) => Comparison(column, "le", value);

    /// <summary>Adds <c>contains(column,'text')</c>.</summary>
    public ODataFilterBuilder Contains(string column, string text) => Function("contains", column, text);

    /// <summary>Adds <c>startswith(column,'text')</c>.</summary>
    public ODataFilterBuilder StartsWith(string column, string text) => Function("startswith", column, text);

    /// <summary>Adds <c>endswith(column,'text')</c>.</summary>
    public ODataFilterBuilder EndsWith(string column, string text) => Function("endswith", column, text);

    /// <summary>Adds <c>column eq null</c>.</summary>
    public ODataFilterBuilder IsNull(string column) => Comparison(column, "eq", null);

    /// <summary>Adds <c>column ne null</c>.</summary>
    public ODataFilterBuilder IsNotNull(string column) => Comparison(column, "ne", null);

    /// <summary>Adds a membership test expanded to an OR of equality comparisons.</summary>
    public ODataFilterBuilder In(string column, params object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
        {
            throw new ArgumentException("Provide at least one value.", nameof(values));
        }

        DataverseNames.ValidColumnName(column, nameof(column));
        var parts = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            parts[i] = $"{column} eq {Literal(values[i])}";
        }

        return Append(values.Length == 1 ? parts[0] : "(" + string.Join(" or ", parts) + ")");
    }

    /// <summary>Adds a nested group combined with <c>and</c>.</summary>
    public ODataFilterBuilder And(Action<ODataFilterBuilder> configure) => Group(isOr: false, configure);

    /// <summary>Adds a nested group combined with <c>or</c>.</summary>
    public ODataFilterBuilder Or(Action<ODataFilterBuilder> configure) => Group(isOr: true, configure);

    /// <summary>Adds a negated nested group: <c>not (…)</c>.</summary>
    public ODataFilterBuilder Not(Action<ODataFilterBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var nested = new ODataFilterBuilder();
        configure(nested);
        if (nested._expression.Length == 0)
        {
            throw new ArgumentException("The negated group is empty.", nameof(configure));
        }

        return Append($"not ({nested._expression})");
    }

    /// <summary>
    /// Adds raw OData filter text verbatim. The SDK performs no encoding or validation on this
    /// fragment — never interpolate untrusted input into it; prefer the typed methods.
    /// </summary>
    public ODataFilterBuilder Raw(string filterFragment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterFragment);
        return Append("(" + filterFragment + ")");
    }

    /// <summary>Renders a single value as an OData literal (quoted/escaped as needed).</summary>
    public static string Literal(object? value)
    {
        return value switch
        {
            null => "null",
            string s => "'" + s.Replace("'", "''", StringComparison.Ordinal) + "'",
            bool b => b ? "true" : "false",
            Guid g => g.ToString("D"),
            DateTimeOffset dto => dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EntityReference r => r.Id.ToString("D"),
            Enum e => Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => throw new ArgumentException($"Values of type '{value.GetType().Name}' cannot be used in an OData filter.", nameof(value)),
        };
    }

    internal string Build() => _expression.ToString();

    internal bool IsEmpty => _expression.Length == 0;

    private ODataFilterBuilder Comparison(string column, string op, object? value)
    {
        DataverseNames.ValidColumnName(column, nameof(column));
        return Append($"{column} {op} {Literal(value)}");
    }

    private ODataFilterBuilder Function(string function, string column, string text)
    {
        DataverseNames.ValidColumnName(column, nameof(column));
        ArgumentNullException.ThrowIfNull(text);
        return Append($"{function}({column},{Literal(text)})");
    }

    private ODataFilterBuilder Group(bool isOr, Action<ODataFilterBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var nested = new ODataFilterBuilder(isOr);
        configure(nested);
        if (nested._expression.Length == 0)
        {
            throw new ArgumentException("The nested group is empty.", nameof(configure));
        }

        return Append("(" + nested._expression + ")");
    }

    private ODataFilterBuilder Append(string term)
    {
        if (_expression.Length > 0)
        {
            _expression.Append(_combinator);
        }

        _expression.Append(term);
        return this;
    }
}
