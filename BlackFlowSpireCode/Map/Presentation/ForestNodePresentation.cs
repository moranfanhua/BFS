using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Exploration;
using static BlackFlowSpire.Map.Presentation.ForestNodeVisuals;

namespace BlackFlowSpire.Map.Presentation;

public sealed record ForestNodeView(string IconKey, string Type, string Title, bool IsMasked);

public static class ForestNodePresentation
{
    public static ForestNodeView Get(ForestNode node, bool revealAll = false, string? bossTitle = null,
        ForestMap? map = null)
    {
        if (map != null && ForestResidentRules.Occupant(map, node.Id) != null)
            return new(BATTLE_SAVAGE_BUOY, TypeForIcon(BATTLE_SAVAGE_BUOY),
                Get(node, revealAll, bossTitle).Title, false);
        ForestNodeView View(string key, bool masked = false)
        {
            var title = !masked && node.Details?.TitleKey is { } titleKey
                ? ResolveMapText(titleKey)
                : key == BATTLE_BOSS && bossTitle != null ? bossTitle
                : ForestText.Get("MAP." + key + ".title");
            return new(key, TypeForIcon(key), title, masked);
        }
        // Boss visibility does not depend on the saved Revealed flag.
        if (node.Kind == ForestNodeKind.Boss)
            return View(BATTLE_BOSS);
        if (node.Kind == ForestNodeKind.Exit)
            return View(FINAL);
        if (node.Kind == ForestNodeKind.Empty)
            return View(EMPTY);
        var visualId = node.Details?.VisualId ?? node.Content switch
        {
            ForestNodeContent.NormalCombat => BATTLE_NORMAL,
            ForestNodeContent.EliteCombat => BATTLE_ELITE,
            ForestNodeContent.Event => INCIDENT,
            ForestNodeContent.Reward => WISH,
            ForestNodeContent.Shop => BATTLE_SHOP,
            ForestNodeContent.Door => DOOR,
            ForestNodeContent.BattleSavage => BATTLE_SAVAGE,
            ForestNodeContent.Light => LIGHT,
            ForestNodeContent.Evacuate => EVACUATE,
            _ => throw new ArgumentOutOfRangeException(nameof(node))
        };
        if (!revealAll && !node.Revealed && !ForestNodeVisuals.Get(visualId).AlwaysVisible)
            return node.Kind switch
            {
                ForestNodeKind.Combat => View(HIDE_BATTLE, true),
                ForestNodeKind.Event => View(HIDE_INVISIBLE, true),
                _ => throw new ArgumentOutOfRangeException(nameof(node))
            };
        return View(visualId);
    }

    // Map captions describe the visible icon category, while detail titles identify an
    // actual stage. Combat categories must never become placeholder stage names.
    public static string CaptionForIcon(string key) => key == EMPTY ? "" : ForestNodeVisuals.Get(key).UsesStageTitle
        ? TypeForIcon(key) : ForestText.Get("MAP." + key + ".title");

    public static string TypeForIcon(string key) => TypeForEntry(ForestNodeVisuals.Get(key).Id);
    public static string TypeForEntry(string entry) => ForestText.Get("MAP." + entry + ".type");

    public static string DescriptionForNode(ForestNode node, ForestNodeView view) =>
        view.IconKey == BATTLE_SAVAGE_BUOY ? DescriptionForIcon(BATTLE_SAVAGE_BUOY)
        : !view.IsMasked && node.Details?.DescriptionKey is { } descriptionKey
            ? ResolveMapText(descriptionKey) : DescriptionForIcon(view.IconKey);

    private static string ResolveMapText(string key) => string.IsNullOrWhiteSpace(key) ? "" : ForestText.Get(key);

    public static string DescriptionForIcon(string key) => ForestText.Get("MAP." + ForestNodeVisuals.Get(key).Id + ".description");
}
