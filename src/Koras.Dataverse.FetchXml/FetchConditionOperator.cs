namespace Koras.Dataverse.FetchXml;

/// <summary>FetchXML condition operators supported by the builder.</summary>
public enum FetchConditionOperator
{
    /// <summary>Equal (<c>eq</c>).</summary>
    Equal,

    /// <summary>Not equal (<c>ne</c>).</summary>
    NotEqual,

    /// <summary>Greater than (<c>gt</c>).</summary>
    GreaterThan,

    /// <summary>Greater than or equal (<c>ge</c>).</summary>
    GreaterEqual,

    /// <summary>Less than (<c>lt</c>).</summary>
    LessThan,

    /// <summary>Less than or equal (<c>le</c>).</summary>
    LessEqual,

    /// <summary>Wildcard match (<c>like</c>). Use <c>%</c> as the wildcard.</summary>
    Like,

    /// <summary>Negated wildcard match (<c>not-like</c>).</summary>
    NotLike,

    /// <summary>Value is in a set (<c>in</c>).</summary>
    In,

    /// <summary>Value is not in a set (<c>not-in</c>).</summary>
    NotIn,

    /// <summary>Column has no value (<c>null</c>).</summary>
    Null,

    /// <summary>Column has a value (<c>not-null</c>).</summary>
    NotNull,

    /// <summary>Date equals the given day (<c>on</c>).</summary>
    On,

    /// <summary>Date is on or after the given day (<c>on-or-after</c>).</summary>
    OnOrAfter,

    /// <summary>Date is on or before the given day (<c>on-or-before</c>).</summary>
    OnOrBefore,

    /// <summary>String starts with the given text (<c>begins-with</c>).</summary>
    BeginsWith,

    /// <summary>String ends with the given text (<c>ends-with</c>).</summary>
    EndsWith,
}
