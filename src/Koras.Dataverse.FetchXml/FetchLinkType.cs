namespace Koras.Dataverse.FetchXml;

/// <summary>Join type for a FetchXML <c>link-entity</c>.</summary>
public enum FetchLinkType
{
    /// <summary>Inner join: parent rows without a matching linked row are excluded.</summary>
    Inner,

    /// <summary>Left outer join: parent rows are returned even when no linked row matches.</summary>
    Outer,
}
