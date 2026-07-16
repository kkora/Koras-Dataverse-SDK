using System.Xml.Linq;
using Koras.Dataverse.FetchXml.Internal;

namespace Koras.Dataverse.FetchXml;

/// <summary>
/// Builds a FetchXML <c>filter</c> element. Conditions added directly to the builder are combined
/// with the builder's own filter type (<c>and</c> by default); nested groups are created with
/// <see cref="And(Action{FetchFilterBuilder})"/> and <see cref="Or(Action{FetchFilterBuilder})"/>.
/// All values are XML-encoded, and all column names are validated, so user input can never break
/// out of the query structure.
/// </summary>
/// <remarks>This type is not thread-safe. Build queries on a single thread.</remarks>
public sealed class FetchFilterBuilder
{
    private readonly string _filterType;
    private readonly List<XElement> _children = new List<XElement>();

    internal FetchFilterBuilder(bool isOr) => _filterType = isOr ? "or" : "and";

    /// <summary>Adds a condition with an explicit operator. Prefer the named helpers where available.</summary>
    /// <param name="column">Column logical name.</param>
    /// <param name="conditionOperator">The FetchXML operator.</param>
    /// <param name="values">Condition values; none for null-checks, several for set operators.</param>
    public FetchFilterBuilder Condition(string column, FetchConditionOperator conditionOperator, params object[] values)
    {
        FetchNames.ValidLogicalName(column, nameof(column));

        var element = new XElement("condition",
            new XAttribute("attribute", column),
            new XAttribute("operator", OperatorText(conditionOperator)));

        if (values is null || values.Length == 0)
        {
            if (RequiresValue(conditionOperator))
            {
                throw new ArgumentException($"Operator '{conditionOperator}' requires at least one value.", nameof(values));
            }
        }
        else if (conditionOperator == FetchConditionOperator.In || conditionOperator == FetchConditionOperator.NotIn)
        {
            foreach (object value in values)
            {
                element.Add(new XElement("value", FetchValueFormatter.Format(value)));
            }
        }
        else if (values.Length == 1)
        {
            element.Add(new XAttribute("value", FetchValueFormatter.Format(values[0])));
        }
        else
        {
            throw new ArgumentException($"Operator '{conditionOperator}' accepts a single value.", nameof(values));
        }

        _children.Add(element);
        return this;
    }

    /// <summary>Adds an equality condition (<c>eq</c>).</summary>
    public FetchFilterBuilder Eq(string column, object value) => Condition(column, FetchConditionOperator.Equal, value);

    /// <summary>Adds a not-equal condition (<c>ne</c>).</summary>
    public FetchFilterBuilder Ne(string column, object value) => Condition(column, FetchConditionOperator.NotEqual, value);

    /// <summary>Adds a greater-than condition (<c>gt</c>).</summary>
    public FetchFilterBuilder Gt(string column, object value) => Condition(column, FetchConditionOperator.GreaterThan, value);

    /// <summary>Adds a greater-or-equal condition (<c>ge</c>).</summary>
    public FetchFilterBuilder Ge(string column, object value) => Condition(column, FetchConditionOperator.GreaterEqual, value);

    /// <summary>Adds a less-than condition (<c>lt</c>).</summary>
    public FetchFilterBuilder Lt(string column, object value) => Condition(column, FetchConditionOperator.LessThan, value);

    /// <summary>Adds a less-or-equal condition (<c>le</c>).</summary>
    public FetchFilterBuilder Le(string column, object value) => Condition(column, FetchConditionOperator.LessEqual, value);

    /// <summary>Adds a wildcard match condition (<c>like</c>). Use <c>%</c> as the wildcard.</summary>
    public FetchFilterBuilder Like(string column, string pattern) => Condition(column, FetchConditionOperator.Like, pattern);

    /// <summary>Adds a negated wildcard match condition (<c>not-like</c>).</summary>
    public FetchFilterBuilder NotLike(string column, string pattern) => Condition(column, FetchConditionOperator.NotLike, pattern);

    /// <summary>Adds a begins-with condition.</summary>
    public FetchFilterBuilder BeginsWith(string column, string text) => Condition(column, FetchConditionOperator.BeginsWith, text);

    /// <summary>Adds an ends-with condition.</summary>
    public FetchFilterBuilder EndsWith(string column, string text) => Condition(column, FetchConditionOperator.EndsWith, text);

    /// <summary>Adds an <c>in</c> condition matching any of the given values.</summary>
    public FetchFilterBuilder In(string column, params object[] values) => Condition(column, FetchConditionOperator.In, values);

    /// <summary>Adds a <c>not-in</c> condition.</summary>
    public FetchFilterBuilder NotIn(string column, params object[] values) => Condition(column, FetchConditionOperator.NotIn, values);

    /// <summary>Adds a null-check condition (<c>null</c>).</summary>
    public FetchFilterBuilder IsNull(string column) => Condition(column, FetchConditionOperator.Null);

    /// <summary>Adds a not-null condition (<c>not-null</c>).</summary>
    public FetchFilterBuilder IsNotNull(string column) => Condition(column, FetchConditionOperator.NotNull);

    /// <summary>Adds a nested <c>and</c> group.</summary>
    public FetchFilterBuilder And(Action<FetchFilterBuilder> configure) => Group(false, configure);

    /// <summary>Adds a nested <c>or</c> group.</summary>
    public FetchFilterBuilder Or(Action<FetchFilterBuilder> configure) => Group(true, configure);

    private FetchFilterBuilder Group(bool isOr, Action<FetchFilterBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var nested = new FetchFilterBuilder(isOr);
        configure(nested);
        _children.Add(nested.ToElement());
        return this;
    }

    internal XElement ToElement()
    {
        var filter = new XElement("filter", new XAttribute("type", _filterType));
        foreach (XElement child in _children)
        {
            filter.Add(child);
        }

        return filter;
    }

    internal bool IsEmpty => _children.Count == 0;

    private static bool RequiresValue(FetchConditionOperator op) =>
        op != FetchConditionOperator.Null && op != FetchConditionOperator.NotNull;

    private static string OperatorText(FetchConditionOperator op)
    {
        switch (op)
        {
            case FetchConditionOperator.Equal: return "eq";
            case FetchConditionOperator.NotEqual: return "ne";
            case FetchConditionOperator.GreaterThan: return "gt";
            case FetchConditionOperator.GreaterEqual: return "ge";
            case FetchConditionOperator.LessThan: return "lt";
            case FetchConditionOperator.LessEqual: return "le";
            case FetchConditionOperator.Like: return "like";
            case FetchConditionOperator.NotLike: return "not-like";
            case FetchConditionOperator.In: return "in";
            case FetchConditionOperator.NotIn: return "not-in";
            case FetchConditionOperator.Null: return "null";
            case FetchConditionOperator.NotNull: return "not-null";
            case FetchConditionOperator.On: return "on";
            case FetchConditionOperator.OnOrAfter: return "on-or-after";
            case FetchConditionOperator.OnOrBefore: return "on-or-before";
            case FetchConditionOperator.BeginsWith: return "begins-with";
            case FetchConditionOperator.EndsWith: return "ends-with";
            default:
                throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown FetchXML operator.");
        }
    }
}
