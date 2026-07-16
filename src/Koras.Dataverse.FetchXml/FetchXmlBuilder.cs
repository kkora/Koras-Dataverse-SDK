using System.Globalization;
using System.Xml.Linq;
using Koras.Dataverse.FetchXml.Internal;

namespace Koras.Dataverse.FetchXml;

/// <summary>
/// Fluent builder for FetchXML queries. Create instances with <see cref="FetchXml.For(string)"/>.
/// </summary>
/// <remarks>This type is not thread-safe. Build queries on a single thread and share the
/// resulting immutable <see cref="FetchXmlQuery"/> freely.</remarks>
public sealed class FetchXmlBuilder
{
    private readonly string _tableName;
    private readonly List<string> _attributes = new List<string>();
    private readonly List<XElement> _orders = new List<XElement>();
    private readonly List<XElement> _links = new List<XElement>();
    private FetchFilterBuilder? _filter;
    private bool _allAttributes;
    private bool _distinct;
    private int? _top;
    private int? _page;
    private int? _pageSize;
    private string? _pagingCookie;

    internal FetchXmlBuilder(string tableName) =>
        _tableName = FetchNames.ValidLogicalName(tableName, nameof(tableName));

    /// <summary>Adds specific columns to the result. Prefer explicit columns over <see cref="AllAttributes"/>.</summary>
    public FetchXmlBuilder Attributes(params string[] columns)
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

    /// <summary>Includes all columns. Costs bandwidth and API allowance; prefer explicit columns.</summary>
    public FetchXmlBuilder AllAttributes()
    {
        _allAttributes = true;
        return this;
    }

    /// <summary>Returns only distinct rows.</summary>
    public FetchXmlBuilder Distinct(bool distinct = true)
    {
        _distinct = distinct;
        return this;
    }

    /// <summary>Limits the total number of rows returned. Cannot be combined with <see cref="Page"/>.</summary>
    public FetchXmlBuilder Top(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Top must be a positive number.");
        }

        _top = count;
        return this;
    }

    /// <summary>Requests a specific result page. Cannot be combined with <see cref="Top"/>.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page (Dataverse allows up to 5000).</param>
    /// <param name="pagingCookie">The paging cookie returned with the previous page, if any.</param>
    public FetchXmlBuilder Page(int page, int pageSize, string? pagingCookie = null)
    {
        if (page <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page numbers are 1-based.");
        }

        if (pageSize <= 0 || pageSize > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be between 1 and 5000.");
        }

        _page = page;
        _pageSize = pageSize;
        _pagingCookie = pagingCookie;
        return this;
    }

    /// <summary>Adds the query filter. Conditions are combined with <c>and</c>; use nested groups for <c>or</c>.</summary>
    public FetchXmlBuilder Where(Action<FetchFilterBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        _filter ??= new FetchFilterBuilder(isOr: false);
        configure(_filter);
        return this;
    }

    /// <summary>Adds an ascending sort.</summary>
    public FetchXmlBuilder OrderBy(string column) => AddOrder(column, descending: false);

    /// <summary>Adds a descending sort.</summary>
    public FetchXmlBuilder OrderByDescending(string column) => AddOrder(column, descending: true);

    /// <summary>Joins a related table.</summary>
    /// <param name="tableName">Logical name of the table to join.</param>
    /// <param name="from">Column on <paramref name="tableName"/> used for the join.</param>
    /// <param name="to">Column on the parent table used for the join.</param>
    /// <param name="configure">Optional configuration (alias, columns, filters, nested links).</param>
    /// <param name="linkType">Join type; inner by default.</param>
    public FetchXmlBuilder Link(
        string tableName,
        string from,
        string to,
        Action<FetchLinkEntityBuilder>? configure = null,
        FetchLinkType linkType = FetchLinkType.Inner)
    {
        var link = new FetchLinkEntityBuilder(tableName, from, to, linkType);
        configure?.Invoke(link);
        _links.Add(link.ToElement());
        return this;
    }

    /// <summary>Builds the immutable <see cref="FetchXmlQuery"/>.</summary>
    /// <exception cref="InvalidOperationException">Both <see cref="Top"/> and <see cref="Page"/> were specified.</exception>
    public FetchXmlQuery Build()
    {
        if (_top.HasValue && _page.HasValue)
        {
            throw new InvalidOperationException("Top and Page cannot be combined in a FetchXML query.");
        }

        var fetch = new XElement("fetch");
        if (_distinct)
        {
            fetch.Add(new XAttribute("distinct", "true"));
        }

        if (_top.HasValue)
        {
            fetch.Add(new XAttribute("top", _top.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (_page.HasValue)
        {
            fetch.Add(new XAttribute("page", _page.Value.ToString(CultureInfo.InvariantCulture)));
            fetch.Add(new XAttribute("count", _pageSize!.Value.ToString(CultureInfo.InvariantCulture)));
            if (!string.IsNullOrEmpty(_pagingCookie))
            {
                fetch.Add(new XAttribute("paging-cookie", _pagingCookie!));
            }
        }

        var entity = new XElement("entity", new XAttribute("name", _tableName));

        if (_allAttributes)
        {
            entity.Add(new XElement("all-attributes"));
        }
        else
        {
            foreach (string attribute in _attributes)
            {
                entity.Add(new XElement("attribute", new XAttribute("name", attribute)));
            }
        }

        if (_filter != null && !_filter.IsEmpty)
        {
            entity.Add(_filter.ToElement());
        }

        foreach (XElement order in _orders)
        {
            entity.Add(order);
        }

        foreach (XElement link in _links)
        {
            entity.Add(link);
        }

        fetch.Add(entity);
        return new FetchXmlQuery(_tableName, fetch.ToString(SaveOptions.DisableFormatting));
    }

    private FetchXmlBuilder AddOrder(string column, bool descending)
    {
        FetchNames.ValidLogicalName(column, nameof(column));
        var order = new XElement("order", new XAttribute("attribute", column));
        if (descending)
        {
            order.Add(new XAttribute("descending", "true"));
        }

        _orders.Add(order);
        return this;
    }
}
