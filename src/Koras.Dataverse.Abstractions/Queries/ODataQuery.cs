using System.Globalization;
using System.Text;

namespace Koras.Dataverse.Queries;

/// <summary>
/// A fluent OData query against a Dataverse table. Configure with the fluent methods, then pass
/// to <c>IDataverseClient.QueryAsync</c>/<c>QueryAllAsync</c>. Addressed by table <b>logical
/// name</b>; the client resolves the entity set name.
/// </summary>
/// <remarks>This type is not thread-safe while being configured.</remarks>
public sealed class ODataQuery
{
    private readonly List<string> _select = new();
    private readonly List<string> _orderBy = new();
    private readonly List<string> _expand = new();
    private ODataFilterBuilder? _filter;
    private int? _top;
    private bool _count;

    private ODataQuery(string tableName) => TableName = DataverseNames.ValidTableName(tableName, nameof(tableName));

    /// <summary>The table logical name the query targets.</summary>
    public string TableName { get; }

    /// <summary>The preferred page size (sent as <c>odata.maxpagesize</c>), when set.</summary>
    public int? PreferredPageSize { get; private set; }

    /// <summary>Starts a query for the given table logical name (for example <c>account</c>).</summary>
    public static ODataQuery For(string tableName) => new(tableName);

    /// <summary>Adds columns to <c>$select</c>. Always select explicitly in production code.</summary>
    public ODataQuery Select(params string[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        foreach (string column in columns)
        {
            _select.Add(DataverseNames.ValidColumnName(column, nameof(columns)));
        }

        return this;
    }

    /// <summary>Adds filter conditions (AND-combined with previously added conditions).</summary>
    public ODataQuery Where(Action<ODataFilterBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _filter ??= new ODataFilterBuilder();
        configure(_filter);
        return this;
    }

    /// <summary>
    /// Sets raw <c>$filter</c> text verbatim. The SDK performs no encoding or validation on this
    /// text — never interpolate untrusted input; prefer <see cref="Where"/>.
    /// </summary>
    public ODataQuery WhereRaw(string filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filter);
        _filter ??= new ODataFilterBuilder();
        _filter.Raw(filter);
        return this;
    }

    /// <summary>Adds an ascending sort.</summary>
    public ODataQuery OrderBy(string column)
    {
        _orderBy.Add(DataverseNames.ValidColumnName(column, nameof(column)));
        return this;
    }

    /// <summary>Adds a descending sort.</summary>
    public ODataQuery OrderByDescending(string column)
    {
        _orderBy.Add(DataverseNames.ValidColumnName(column, nameof(column)) + " desc");
        return this;
    }

    /// <summary>Limits the number of rows returned (<c>$top</c>).</summary>
    public ODataQuery Top(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);
        _top = count;
        return this;
    }

    /// <summary>Expands a navigation property (<c>$expand</c>), optionally selecting its columns.</summary>
    /// <param name="navigationProperty">The navigation property name, for example <c>primarycontactid</c>.</param>
    /// <param name="columns">Columns of the related row to select.</param>
    public ODataQuery Expand(string navigationProperty, params string[] columns)
    {
        DataverseNames.ValidColumnName(navigationProperty, nameof(navigationProperty));
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length == 0)
        {
            _expand.Add(navigationProperty);
        }
        else
        {
            foreach (string column in columns)
            {
                DataverseNames.ValidColumnName(column, nameof(columns));
            }

            _expand.Add($"{navigationProperty}($select={string.Join(",", columns)})");
        }

        return this;
    }

    /// <summary>Requests the total row count (<c>$count=true</c>); read it from <c>DataverseQueryResult.TotalCount</c>.</summary>
    public ODataQuery IncludeCount()
    {
        _count = true;
        return this;
    }

    /// <summary>Sets the preferred server page size (1–5000) for this query.</summary>
    public ODataQuery PageSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, 5000);
        PreferredPageSize = size;
        return this;
    }

    /// <summary>
    /// Renders the OData system query options (for example
    /// <c>$select=name&amp;$filter=statecode%20eq%200</c>). Values are percent-encoded; the result
    /// is ready to append to an entity-set URL after a <c>?</c>. Returns an empty string when no
    /// options are set.
    /// </summary>
    public string ToQueryString()
    {
        var parts = new List<string>(5);
        if (_select.Count > 0)
        {
            parts.Add("$select=" + Uri.EscapeDataString(string.Join(",", _select)));
        }

        if (_filter is not null && !_filter.IsEmpty)
        {
            parts.Add("$filter=" + Uri.EscapeDataString(_filter.Build()));
        }

        if (_orderBy.Count > 0)
        {
            parts.Add("$orderby=" + Uri.EscapeDataString(string.Join(",", _orderBy)));
        }

        if (_expand.Count > 0)
        {
            parts.Add("$expand=" + Uri.EscapeDataString(string.Join(",", _expand)));
        }

        if (_top.HasValue)
        {
            parts.Add("$top=" + _top.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (_count)
        {
            parts.Add("$count=true");
        }

        return string.Join("&", parts);
    }

    /// <summary>Returns the query-string form of the query.</summary>
    public override string ToString()
    {
        string options = ToQueryString();
        return options.Length == 0 ? TableName : TableName + "?" + options;
    }
}
