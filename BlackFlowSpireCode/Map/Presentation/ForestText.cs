using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlackFlowSpire.Map.Presentation;

// Game code supplies the native LocString resolver. Offline tools use the
// same JSON catalogs embedded by their project, without loading Godot.
public static class ForestText
{
    public const string Prefix = "BLACK_FLOW_SPIRE.";
    internal const string ResourcePrefix = "BlackFlowSpire.Localization.";
    private const string EnglishSuffix = ".eng.json";
    public static IReadOnlyList<string> CatalogNames { get; } = Array.AsReadOnly(typeof(ForestText).Assembly
        .GetManifestResourceNames()
        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(EnglishSuffix, StringComparison.Ordinal))
        .Select(name => name[ResourcePrefix.Length..^EnglishSuffix.Length])
        .Order(StringComparer.Ordinal)
        .ToArray());
    private static readonly Dictionary<string, (string Table, string Text)> EnglishCatalog = LoadEnglishCatalog();
    private static Func<string, IReadOnlyDictionary<string, object>, string> _resolve = CatalogResolver("eng");
    public static event Action? Changed;

    internal static string PartKey(string id) => "UI.PARTS." + id;
    internal static string FullKey(string key) => key.StartsWith(Prefix, StringComparison.Ordinal) ? key : Prefix + key;
    internal static string CatalogNameFor(string fullKey) => EnglishCatalog[fullKey].Table;

    public static string Get(string key, params (string Name, object Value)[] variables)
    {
        return _resolve(FullKey(key), variables.ToDictionary(pair => pair.Name, pair => pair.Value));
    }

    public static void Configure(Func<string, IReadOnlyDictionary<string, object>, string> resolve)
    {
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        NotifyChanged();
    }

    public static void UseCatalog(string language) => Configure(CatalogResolver(language));
    public static void NotifyChanged() => Changed?.Invoke();

    private static Dictionary<string, string> LoadCatalog(string table, string language)
    {
        using var stream = typeof(ForestText).Assembly.GetManifestResourceStream($"{ResourcePrefix}{table}.{language}.json");
        return stream == null ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    }

    private static Dictionary<string, (string Table, string Text)> LoadEnglishCatalog()
    {
        Dictionary<string, (string Table, string Text)> entries = [];
        foreach (var table in CatalogNames)
            foreach (var (key, text) in LoadCatalog(table, "eng"))
                entries.Add(key, (table, text));

        if (entries.Count == 0) throw new InvalidDataException("The English localization resources are missing.");
        return entries;
    }

    private static Func<string, IReadOnlyDictionary<string, object>, string> CatalogResolver(string language)
    {
        var catalog = EnglishCatalog.ToDictionary(pair => pair.Key, pair => pair.Value.Text);
        if (language != "eng")
        {
            foreach (var table in CatalogNames)
                foreach (var (key, text) in LoadCatalog(table, language))
                    catalog[key] = text;
        }

        return (key, values) =>
        {
            if (!catalog.TryGetValue(key, out var template)) throw new KeyNotFoundException("Missing localization key: " + key);
            return Regex.Replace(template, @"\{([A-Za-z][A-Za-z0-9_]*)\}", match =>
                values.TryGetValue(match.Groups[1].Value, out var value)
                    ? Convert.ToString(value, CultureInfo.CurrentCulture) ?? ""
                    : throw new FormatException("Missing localization argument: " + match.Value));
        };
    }
}
