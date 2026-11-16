using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Localization;
using STS2RitsuLib.Utils;

namespace BlackFlowSpire.Ui;

internal static class BlackFlowSpireLocalization
{
    private static readonly Dictionary<string, string> TableIds = BlackFlowSpireText.CatalogNames
        .ToDictionary(name => name, name => I18NLocTableBridge.GetTableId(Entry.ModId, name));
    private static IReadOnlyList<I18N> _catalogs = [];
    private static LocManager? _subscribedManager;

    public static LocString CreateLocString(string key)
    {
        var fullKey = BlackFlowSpireText.FullKey(key);
        return new LocString(TableIds[BlackFlowSpireText.CatalogNameFor(fullKey)], fullKey);
    }

    public static void AttachLocaleManager()
    {
        var manager = LocManager.Instance;
        if (manager == null || ReferenceEquals(manager, _subscribedManager)) return;
        _subscribedManager?.UnsubscribeToLocaleChange(BlackFlowSpireText.NotifyChanged);
        _subscribedManager = null;
        foreach (var catalog in _catalogs) catalog.Dispose();

        // I18N subscribes here, so its table snapshots update before UI callbacks run.
        _catalogs = RegisterCatalogs();
        manager.SubscribeToLocaleChange(BlackFlowSpireText.NotifyChanged);
        _subscribedManager = manager;
        BlackFlowSpireText.Configure((key, values) =>
        {
            var text = CreateLocString(key);
            foreach (var pair in values) text.AddObj(pair.Key, pair.Value);
            return text.GetFormattedText();
        });
    }

    private static IReadOnlyList<I18N> RegisterCatalogs()
    {
        List<I18N> catalogs = [];
        try
        {
            foreach (var (name, tableId) in TableIds)
            {
                var catalog = new I18N(tableId, resourceFolders: [BlackFlowSpireText.ResourcePrefix + name],
                    resourceAssembly: typeof(BlackFlowSpireLocalization).Assembly);
                catalogs.Add(catalog);
                if (!I18NLocTableBridge.TryRegister(Entry.ModId, catalog, name))
                    throw new InvalidOperationException("Localization table is already registered: " + tableId);
            }
            return catalogs;
        }
        catch
        {
            foreach (var catalog in catalogs) catalog.Dispose();
            throw;
        }
    }

    public static void BindStaticText(Control root)
    {
        foreach (var control in Descendants(root))
        {
            if (control.HasMeta("loc_key"))
            {
                var text = BlackFlowSpireText.Get(control.GetMeta("loc_key").AsString());
                if (control is Label label) label.Text = text;
                else if (control is Button button) button.Text = text;
            }
        }
    }

    public static void RefreshWithLocale(Node owner, Action refresh)
    {
        const string boundKey = "BlackFlowSpire_locale_refresh_bound";
        if (owner.HasMeta(boundKey)) return;
        owner.SetMeta(boundKey, true);
        BlackFlowSpireText.Changed += refresh;
        Action onExit = null!;
        onExit = () =>
        {
            BlackFlowSpireText.Changed -= refresh;
            owner.TreeExiting -= onExit;
            owner.RemoveMeta(boundKey);
        };
        owner.TreeExiting += onExit;
    }

    private static IEnumerable<Control> Descendants(Node node)
    {
        if (node is Control control) yield return control;
        foreach (var child in node.GetChildren())
        foreach (var item in Descendants(child)) yield return item;
    }
}
