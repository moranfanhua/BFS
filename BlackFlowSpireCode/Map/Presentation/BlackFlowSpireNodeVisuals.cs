namespace BlackFlowSpire.Map.Presentation;

// Original node IDs are shared by localization, the map, the legend and details.
// A visual definition does not register gameplay or add the node to generation.
public sealed record BlackFlowSpireNodeVisual(string Id, string? Icon, string Theme,
    bool HasEnemyIntel = false, bool UsesStageTitle = false, bool AlwaysVisible = false,
    string? FocusIcon = null, string? HintImage = null)
{
    // Native walking cores use the visible node decoration. The two hidden
    // decorations explicitly fall back to grey, despite their colored icons.
    public string WalkCoreTexture => Id is BlackFlowSpireNodeVisuals.HIDE_BATTLE or BlackFlowSpireNodeVisuals.HIDE_INVISIBLE ? "grey" : Theme switch
    {
        "cyan" => "blue",
        "grey" or "green" or "final_green" or "purple" or "red" or "yellow" => Theme,
        _ => "grey"
    };
}

public static class BlackFlowSpireNodeVisuals
{
    public const string EMPTY = "EMPTY";
    public const string HIDE_BATTLE = "HIDE_BATTLE";
    public const string HIDE_INVISIBLE = "HIDE_INVISIBLE";
    public const string BATTLE_NORMAL = "BATTLE_NORMAL";
    public const string BATTLE_ELITE = "BATTLE_ELITE";
    public const string BATTLE_BOSS = "BATTLE_BOSS";
    public const string INCIDENT = "INCIDENT";
    public const string WISH = "WISH";
    public const string BATTLE_SHOP = "BATTLE_SHOP";
    public const string SACRIFICE = "SACRIFICE";
    public const string EXPEDITION = "EXPEDITION";
    public const string DUEL = "DUEL";
    public const string STORY = "STORY";
    public const string DOOR = "DOOR";
    public const string LIGHT = "LIGHT";
    public const string FINAL = "FINAL";
    public const string EVACUATE = "EVACUATE";
    public const string SCRAP_SHOP = "SCRAP_SHOP";
    public const string PORTAL = "PORTAL";
    public const string REST = "REST";
    public const string BATTLE_SAVAGE = "BATTLE_SAVAGE";
    public const string BATTLE_SAVAGE_BUOY = "BATTLE_SAVAGE_BUOY";
    public const string EMPLOY = "EMPLOY";

    public static IReadOnlyList<BlackFlowSpireNodeVisual> All { get; } = Array.AsReadOnly<BlackFlowSpireNodeVisual>(
    [
        new(EMPTY, "icon_empty", "grey", AlwaysVisible: true, FocusIcon: "icon_empty_node_focus_view"),
        new(HIDE_BATTLE, "icon_hide_battle", "purple", HasEnemyIntel: true),
        new(HIDE_INVISIBLE, "icon_hide_invisible", "cyan"),
        new(BATTLE_NORMAL, "icon_battle_normal", "purple", HasEnemyIntel: true, UsesStageTitle: true),
        new(BATTLE_ELITE, "icon_battle_elite", "red", HasEnemyIntel: true, UsesStageTitle: true),
        // Boss icons continue to use the native Slay the Spire renderer.
        new(BATTLE_BOSS, null, "red", HasEnemyIntel: true, UsesStageTitle: true, AlwaysVisible: true),
        new(INCIDENT, "icon_incident", "cyan"),
        new(WISH, "icon_wish", "cyan"),
        new(BATTLE_SHOP, "icon_shop", "green", HintImage: "bg_hint_battle_shop"),
        new(SACRIFICE, "icon_sacrifice", "cyan"),
        new(EXPEDITION, "icon_expedition", "cyan"),
        new(DUEL, "icon_duel", "cyan"),
        new(STORY, "icon_story", "cyan"),
        new(DOOR, "icon_door", "yellow", AlwaysVisible: true),
        new(LIGHT, "icon_light", "yellow", AlwaysVisible: true, HintImage: "bg_hint_light"),
        new(FINAL, "icon_final", "final_green", AlwaysVisible: true, HintImage: "bg_hint_final"),
        new(EVACUATE, "icon_evacuate", "yellow", HintImage: "bg_hint_evacuate"),
        new(SCRAP_SHOP, "icon_scrap_shop", "yellow", HintImage: "bg_hint_scrap_shop"),
        new(BATTLE_SAVAGE, "icon_battle_savage", "red", HasEnemyIntel: true, HintImage: "bg_hint_battle_savage"),
        new(BATTLE_SAVAGE_BUOY, "icon_savage_buoy_focus_view_node", "purple", HasEnemyIntel: true,
            AlwaysVisible: true, FocusIcon: "icon_savage_buoy_focus_view_node", HintImage: "bg_hint_battle_savage_buoy"),
        new(REST, "icon_rest", "cyan"),
        // Reserved nodes: title and artwork only; type/description are deliberately blank.
        new(PORTAL, "icon_portal", "yellow", HintImage: "bg_hint_portal"),
        new(EMPLOY, "icon_employ", "yellow")
    ]);

    private static readonly IReadOnlyDictionary<string, BlackFlowSpireNodeVisual> ById = All.ToDictionary(item => item.Id);

    public static BlackFlowSpireNodeVisual Get(string id) => ById.TryGetValue(id, out var visual)
        ? visual : throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown node visual ID.");
}
