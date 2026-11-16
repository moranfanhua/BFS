using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Exploration;
using static BlackFlowSpire.Map.Presentation.BlackFlowSpireNodeVisuals;

namespace BlackFlowSpire.Map.Presentation;

public sealed record BlackFlowSpireNodeView(string IconKey, string Type, string Title, bool IsMasked);

public static class BlackFlowSpireNodePresentation
{
    public static BlackFlowSpireNodeView Get(BlackFlowSpireNode node, bool revealAll = false, string? bossTitle = null,
        BlackFlowSpireMap? map = null)
    {
        if (map != null && BlackFlowSpireResidentRules.Occupant(map, node.Id) != null)
            return new(BATTLE_SAVAGE_BUOY, TypeForIcon(BATTLE_SAVAGE_BUOY),
                Get(node, revealAll, bossTitle).Title, false);
        BlackFlowSpireNodeView View(string key, bool masked = false)
        {
            var title = !masked && node.Details?.TitleKey is { } titleKey
                ? ResolveMapText(titleKey)
                : key == BATTLE_BOSS && bossTitle != null ? bossTitle
                : BlackFlowSpireText.Get("MAP." + key + ".title");
            return new(key, TypeForIcon(key), title, masked);
        }
        // Boss visibility does not depend on the saved Revealed flag.
        if (node.Kind == BlackFlowSpireNodeKind.Boss)
            return View(BATTLE_BOSS);
        if (node.Kind == BlackFlowSpireNodeKind.Exit)
            return View(FINAL);
        if (node.Kind == BlackFlowSpireNodeKind.Empty)
            return View(EMPTY);
        var visualId = node.Details?.VisualId ?? node.Content switch
        {
            BlackFlowSpireNodeContent.NormalCombat => BATTLE_NORMAL,
            BlackFlowSpireNodeContent.EliteCombat => BATTLE_ELITE,
            BlackFlowSpireNodeContent.Event => INCIDENT,
            BlackFlowSpireNodeContent.Reward => WISH,
            BlackFlowSpireNodeContent.Shop => BATTLE_SHOP,
            BlackFlowSpireNodeContent.Door => DOOR,
            BlackFlowSpireNodeContent.BattleSavage => BATTLE_SAVAGE,
            BlackFlowSpireNodeContent.Light => LIGHT,
            BlackFlowSpireNodeContent.Evacuate => EVACUATE,
            _ => throw new ArgumentOutOfRangeException(nameof(node))
        };
        if (!revealAll && !node.Revealed && !BlackFlowSpireNodeVisuals.Get(visualId).AlwaysVisible)
            return node.Kind switch
            {
                BlackFlowSpireNodeKind.Combat => View(HIDE_BATTLE, true),
                BlackFlowSpireNodeKind.Event => View(HIDE_INVISIBLE, true),
                _ => throw new ArgumentOutOfRangeException(nameof(node))
            };
        return View(visualId);
    }

    // Map captions describe the visible icon category, while detail titles identify an
    // actual stage. Combat categories must never become placeholder stage names.
    public static string CaptionForIcon(string key) => key == EMPTY ? "" : BlackFlowSpireNodeVisuals.Get(key).UsesStageTitle
        ? TypeForIcon(key) : BlackFlowSpireText.Get("MAP." + key + ".title");

    public static string TypeForIcon(string key) => TypeForEntry(BlackFlowSpireNodeVisuals.Get(key).Id);
    public static string TypeForEntry(string entry) => BlackFlowSpireText.Get("MAP." + entry + ".type");

    public static string DescriptionForNode(BlackFlowSpireNode node, BlackFlowSpireNodeView view) =>
        view.IconKey == BATTLE_SAVAGE_BUOY ? DescriptionForIcon(BATTLE_SAVAGE_BUOY)
        : !view.IsMasked && node.Details?.DescriptionKey is { } descriptionKey
            ? ResolveMapText(descriptionKey) : DescriptionForIcon(view.IconKey);

    private static string ResolveMapText(string key) => string.IsNullOrWhiteSpace(key) ? "" : BlackFlowSpireText.Get(key);

    public static string DescriptionForIcon(string key) => BlackFlowSpireText.Get("MAP." + BlackFlowSpireNodeVisuals.Get(key).Id + ".description");
}
