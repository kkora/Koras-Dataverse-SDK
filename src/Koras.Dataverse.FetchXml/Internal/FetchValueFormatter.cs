using System.Globalization;

namespace Koras.Dataverse.FetchXml.Internal;

/// <summary>Formats condition values as FetchXML value text using invariant culture.</summary>
internal static class FetchValueFormatter
{
    public static string Format(object value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "Use IsNull/IsNotNull for null comparisons.");
        }

        switch (value)
        {
            case string s:
                return s;
            case bool b:
                return b ? "1" : "0";
            case Guid g:
                return g.ToString("D");
            case DateTime dt:
                return dt.Kind == DateTimeKind.Utc
                    ? dt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                    : dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            case DateTimeOffset dto:
                return dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            case Enum e:
                return Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            case IFormattable f:
                return f.ToString(null, CultureInfo.InvariantCulture);
            default:
                return value.ToString() ?? string.Empty;
        }
    }
}
