using System.Xml.Linq;
using Koras.Dataverse.FetchXml.Internal;

namespace Koras.Dataverse.FetchXml;

/// <summary>
/// Builds a FetchXML <c>link-entity</c> element (a join to a related table).
/// </summary>
/// <remarks>This type is not thread-safe. Build queries on a single thread.</remarks>
public sealed class FetchLinkEntityBuilder
{
    private readonly string _tableName;
    private readonly string _fromColumn;
    private readonly string _toColumn;
    private readonly FetchLinkType _linkType;
    private readonly List<string> _attributes = new List<string>();
    private readonly List<XElement> _children = new List<XElement>();
    private string? _alias;
    private bool _allAttributes;

    internal FetchLinkEntityBuilder(string tableName, string fromColumn, string toColumn, FetchLinkType linkType)
    {
        _tableName = FetchNames.ValidLogicalName(tableName, nameof(tableName));
        _fromColumn = FetchNames.ValidLogicalName(fromColumn, nameof(fromColumn));
        _toColumn = FetchNames.ValidLogicalName(toColumn, nameof(toColumn));
        _linkType = linkType;
    }

    /// <summary>Sets the alias used to prefix this link's columns in results.</summary>
    public FetchLinkEntityBuilder Alias(string alias)
    {
        _alias = FetchNames.ValidLogicalName(alias, nameof(alias));
        return this;
    }

    /// <summary>Adds specific columns of the linked table to the result.</summary>
    public FetchLinkEntityBuilder Attributes(params string[] columns)
    {
        if (columns is null)
        {
            throw new ArgumentNullException(nameof(columns));
        }

        foreach (string column in columns)
        {
            _attributes.Add(FetchNames.ValidLogicalName(column, nameof(columns)));
        }

        return this;
    }

    /// <summary>Includes all columns of the linked table. Prefer explicit columns in production queries.</summary>
    public FetchLinkEntityBuilder AllAttributes()
    {
        _allAttributes = true;
        return this;
    }

    /// <summary>Adds a filter on the linked table.</summary>
    public FetchLinkEntityBuilder Where(Action<FetchFilterBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var filter = new FetchFilterBuilder(isOr: false);
        configure(filter);
        if (!filter.IsEmpty)
        {
            _children.Add(filter.ToElement());
        }

        return this;
    }

    /// <summary>Adds a nested link from this linked table to a further table.</summary>
    /// <param name="tableName">Logical name of the table to join.</param>
    /// <param name="from">Column on <paramref name="tableName"/> used for the join.</param>
    /// <param name="to">Column on this linked table used for the join.</param>
    /// <param name="configure">Optional configuration of the nested link.</param>
    /// <param name="linkType">Join type; inner by default.</param>
    public FetchLinkEntityBuilder Link(
        string tableName,
        string from,
        string to,
        Action<FetchLinkEntityBuilder>? configure = null,
        FetchLinkType linkType = FetchLinkType.Inner)
    {
        var link = new FetchLinkEntityBuilder(tableName, from, to, linkType);
        configure?.Invoke(link);
        _children.Add(link.ToElement());
        return this;
    }

    internal XElement ToElement()
    {
        var element = new XElement("link-entity",
            new XAttribute("name", _tableName),
            new XAttribute("from", _fromColumn),
            new XAttribute("to", _toColumn),
            new XAttribute("link-type", _linkType == FetchLinkType.Outer ? "outer" : "inner"));

        if (_alias != null)
        {
            element.Add(new XAttribute("alias", _alias));
        }

        if (_allAttributes)
        {
            element.Add(new XElement("all-attributes"));
        }
        else
        {
            foreach (string attribute in _attributes)
            {
                element.Add(new XElement("attribute", new XAttribute("name", attribute)));
            }
        }

        foreach (XElement child in _children)
        {
            element.Add(child);
        }

        return element;
    }
}
