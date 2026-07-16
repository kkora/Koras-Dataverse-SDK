namespace Koras.Dataverse.FetchXml;

/// <summary>
/// Entry point for building FetchXML queries fluently.
/// </summary>
/// <example>
/// <code>
/// var query = FetchXml.For("account")
///     .Attributes("name", "revenue")
///     .Where(f => f.Eq("statecode", 0))
///     .OrderBy("name")
///     .Top(50)
///     .Build();
/// </code>
/// </example>
public static class FetchXml
{
    /// <summary>Starts building a FetchXML query for the given table (entity logical name).</summary>
    /// <param name="tableName">The logical name of the table, for example <c>account</c>.</param>
    /// <returns>A new <see cref="FetchXmlBuilder"/>.</returns>
    /// <exception cref="ArgumentException">The table name is null, empty, or not a valid logical name.</exception>
    public static FetchXmlBuilder For(string tableName) => new FetchXmlBuilder(tableName);
}
