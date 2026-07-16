namespace Koras.Dataverse.Queries;

/// <summary>Validation for names embedded in OData query fragments.</summary>
internal static class DataverseNames
{
    /// <summary>
    /// Validates a column or navigation-property name: letters, digits, and underscores only
    /// (lookup value columns such as <c>_ownerid_value</c> start with an underscore).
    /// </summary>
    public static string ValidColumnName(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        foreach (char c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                throw new ArgumentException(
                    $"'{value}' is not a valid Dataverse column or navigation property name.",
                    paramName);
            }
        }

        return value;
    }

    /// <summary>Validates a table logical name: lowercase letters, digits, and underscores.</summary>
    public static string ValidTableName(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        foreach (char c in value)
        {
            if (!(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_'))
            {
                throw new ArgumentException(
                    $"'{value}' is not a valid Dataverse table logical name (lowercase letters, digits, underscores).",
                    paramName);
            }
        }

        return value;
    }
}
