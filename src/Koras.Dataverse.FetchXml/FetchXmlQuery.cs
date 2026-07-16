using System.Globalization;
using System.Xml.Linq;

namespace Koras.Dataverse.FetchXml;

/// <summary>
/// An immutable, validated FetchXML query. Create instances with <see cref="FetchXml.For(string)"/>
/// (fluent builder) or <see cref="FromXml(string)"/> (existing FetchXML text).
/// </summary>
/// <remarks>Instances are immutable and safe to share across threads.</remarks>
public sealed class FetchXmlQuery
{
    internal FetchXmlQuery(string tableName, string xml)
    {
        TableName = tableName;
        Xml = xml;
    }

    /// <summary>The logical name of the table the query targets.</summary>
    public string TableName { get; }

    /// <summary>The FetchXML document text.</summary>
    public string Xml { get; }

    /// <summary>
    /// Wraps existing FetchXML text. The text is parsed to verify it is well-formed and rooted in a
    /// <c>fetch</c> element; the target table is read from the <c>entity</c> element.
    /// </summary>
    /// <param name="xml">A complete FetchXML document.</param>
    /// <exception cref="ArgumentException">The text is not well-formed FetchXML.</exception>
    public static FetchXmlQuery FromXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException("FetchXML text must be provided.", nameof(xml));
        }

        XElement fetch;
        try
        {
            fetch = XElement.Parse(xml);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new ArgumentException("The provided text is not well-formed XML.", nameof(xml), exception);
        }

        if (fetch.Name.LocalName != "fetch")
        {
            throw new ArgumentException("FetchXML must be rooted in a <fetch> element.", nameof(xml));
        }

        XElement? entity = fetch.Element("entity");
        string? tableName = entity?.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(tableName))
        {
            throw new ArgumentException("FetchXML must contain an <entity name=\"...\"> element.", nameof(xml));
        }

        return new FetchXmlQuery(tableName!, fetch.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>
    /// Returns a copy of this query positioned on the given page. Used for paging through results;
    /// existing <c>page</c>, <c>count</c>, and <c>paging-cookie</c> attributes are replaced.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page (1–5000).</param>
    /// <param name="pagingCookie">Paging cookie from the previous page, or null for the first page.</param>
    public FetchXmlQuery WithPage(int page, int pageSize, string? pagingCookie = null)
    {
        if (page <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page numbers are 1-based.");
        }

        if (pageSize <= 0 || pageSize > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be between 1 and 5000.");
        }

        XElement fetch = XElement.Parse(Xml);
        fetch.SetAttributeValue("top", null);
        fetch.SetAttributeValue("page", page.ToString(CultureInfo.InvariantCulture));
        fetch.SetAttributeValue("count", pageSize.ToString(CultureInfo.InvariantCulture));
        fetch.SetAttributeValue("paging-cookie", string.IsNullOrEmpty(pagingCookie) ? null : pagingCookie);
        return new FetchXmlQuery(TableName, fetch.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>Returns the FetchXML text.</summary>
    public override string ToString() => Xml;
}
