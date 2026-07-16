using System.Collections.Concurrent;

namespace Koras.Dataverse.Serialization;

/// <summary>
/// Resolves Web API entity set names from table logical names using Dataverse's standard
/// pluralization rules, with caller-supplied overrides for customized set names.
/// </summary>
internal sealed class EntitySetNameResolver
{
    private readonly IDictionary<string, string> _overrides;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    public EntitySetNameResolver(IDictionary<string, string> overrides) => _overrides = overrides;

    public string Resolve(string tableLogicalName)
    {
        if (_overrides.TryGetValue(tableLogicalName, out string? overridden))
        {
            return overridden;
        }

        return _cache.GetOrAdd(tableLogicalName, static name => Pluralize(name));
    }

    /// <summary>Applies the pluralization rules Dataverse uses when generating entity set names.</summary>
    internal static string Pluralize(string name)
    {
        if (name.EndsWith('s') ||
            name.EndsWith('x') ||
            name.EndsWith('z') ||
            name.EndsWith("ch", StringComparison.Ordinal) ||
            name.EndsWith("sh", StringComparison.Ordinal))
        {
            return name + "es";
        }

        if (name.Length >= 2 && name.EndsWith('y') && !IsVowel(name[^2]))
        {
            return name[..^1] + "ies";
        }

        return name + "s";
    }

    private static bool IsVowel(char c) => c is 'a' or 'e' or 'i' or 'o' or 'u';
}
