namespace Koras.Dataverse;

/// <summary>One page of query results.</summary>
public sealed class DataverseQueryResult
{
    private readonly bool? _moreRecords;

    /// <summary>Creates a result page.</summary>
    /// <param name="entities">The rows in this page.</param>
    /// <param name="nextLink">The OData next-page link, when provided.</param>
    /// <param name="pagingCookie">The FetchXML paging cookie, when provided.</param>
    /// <param name="totalCount">The total row count, when requested.</param>
    /// <param name="moreRecords">Explicit more-records flag (FetchXML); inferred from the paging fields when null.</param>
    public DataverseQueryResult(IReadOnlyList<Entity> entities, string? nextLink = null, string? pagingCookie = null, long? totalCount = null, bool? moreRecords = null)
    {
        Entities = entities ?? throw new ArgumentNullException(nameof(entities));
        NextLink = nextLink;
        PagingCookie = pagingCookie;
        TotalCount = totalCount;
        _moreRecords = moreRecords;
    }

    /// <summary>The rows in this page.</summary>
    public IReadOnlyList<Entity> Entities { get; }

    /// <summary>The OData next-page link, when more rows exist (OData queries).</summary>
    public string? NextLink { get; }

    /// <summary>The paging cookie for the next page, when more rows exist (FetchXML queries).</summary>
    public string? PagingCookie { get; }

    /// <summary>The total row count, when the query requested a count.</summary>
    public long? TotalCount { get; }

    /// <summary>Whether another page is available.</summary>
    public bool MoreRecords => _moreRecords ?? (NextLink is not null || PagingCookie is not null);
}
