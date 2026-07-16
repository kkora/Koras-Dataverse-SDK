namespace Koras.Dataverse.FetchXml.Internal;

/// <summary>Validation helpers for Dataverse logical names used inside FetchXML.</summary>
internal static class FetchNames
{
    /// <summary>
    /// Validates that a value is a plausible Dataverse logical name (lowercase letters, digits,
    /// underscores). Enforcing this at build time makes it impossible to inject markup through
    /// table, column, or alias names.
    /// </summary>
    public static string ValidLogicalName(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("A logical name must be provided.", paramName);
        }

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
            if (!valid)
            {
                throw new ArgumentException(
                    $"'{value}' is not a valid Dataverse logical name. Logical names contain only lowercase letters, digits, and underscores.",
                    paramName);
            }
        }

        return value;
    }
}
