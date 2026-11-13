using System.Text.Json.Serialization;
using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Presentation;

namespace BlackFlowSpire.Map.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ForestNodeKind>))]
public enum ForestNodeKind { Empty, Combat, Event, Boss, Exit }

[JsonConverter(typeof(JsonStringEnumConverter<ForestNodeContent>))]
public enum ForestNodeContent { Empty, NormalCombat, EliteCombat, Event, Reward, Shop, Boss, Door, BattleSavage, Light, Final, Evacuate }

// Edges are saved as IDs rather than object references, including when the graph has cycles.
public sealed class ForestNode
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public ForestNodeKind Kind { get; set; }
    public ForestNodeContent Content { get; set; }
    public List<int> Neighbors { get; set; } = [];
    public bool Revealed { get; set; }
    public bool Completed { get; set; }
    // A completed encounter's clearing differs from an original, even previously visited, empty node.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CreatedByCompletion { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool VantageActivated { get; set; }
    private bool _repeatable;

    // The black pool and regular traders remain available after departure.
    // Secret traders are consumed unless explicitly marked repeatable.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Repeatable
    {
        get => Content == ForestNodeContent.Final
            || Content == ForestNodeContent.Shop && Details?.VisualId != ForestNodeVisuals.SCRAP_SHOP || _repeatable;
        set => _repeatable = value;
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CanPassThrough { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PairedNodeId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ForestCombatSetup? CombatSetup { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ForestNodeDetailsSource? Details { get; set; }
}

// Map content supplies localization keys, keeping actual stage text out of UI code
// and save files independent of the language selected when the map was generated.
public sealed class ForestNodeDetailsSource
{
    // Optional original node ID for specialized/future presentations. Room behavior
    // still comes from Content; registering artwork never enables new gameplay.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VisualId { get; set; }
    public string? TitleKey { get; set; }
    public string? DescriptionKey { get; set; }
}

public enum ForestSpecialEncounter { None, Resident, Hunt }

public sealed class ForestCombatSetup
{
    public string EncounterId { get; set; } = "";
    public ulong Seed { get; set; }
    public List<ForestEnemySlot> Enemies { get; set; } = [];
    public ForestSpecialEncounter Special { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ForestStageSnapshot? Stage { get; set; }
}

public sealed record ForestEnemySlot(string MonsterId, string? Slot);

// Occupation is an overlay: Kind, Content and Details on the underlying node remain intact.
public sealed class ForestRoamingResident
{
    public int Id { get; set; }
    public int StrongholdNodeId { get; set; }
    public int NodeId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ForestCombatSetup? CombatSetup { get; set; }
}

public sealed class ForestMap
{
    public int SchemaVersion { get; set; } = 1;
    public int GeneratorVersion { get; set; } = 1;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StageCatalogVersion { get; set; }
    public string Seed { get; set; } = "";
    public int ActIndex { get; set; }
    public ForestGenerationOptions Options { get; set; } = new();
    public int StartNodeId { get; set; }
    // Native ActMap calls its terminal point BossMapPoint even on a noncombat exit floor.
    public int BossNodeId { get; set; }
    // Missing in older snapshots: treat the center starting node as the current position.
    public int? CurrentNodeId { get; set; }
    public List<ForestNode> Nodes { get; set; } = [];
    public List<ForestRoamingResident> RoamingResidents { get; set; } = [];
    public int ResidentMovementCount { get; set; }

    [JsonIgnore]
    public int PositionNodeId => CurrentNodeId ?? StartNodeId;

    public ForestNode GetNode(int id) => Nodes.First(n => n.Id == id);

    public IReadOnlyList<int> FindShortestPath(int from, int to)
    {
        var byId = Nodes.ToDictionary(n => n.Id);
        if (!byId.ContainsKey(from) || !byId.ContainsKey(to))
            return [];
        var previous = new Dictionary<int, int> { [from] = from };
        var pending = new Queue<int>();
        pending.Enqueue(from);
        while (pending.TryDequeue(out var id))
        {
            if (id == to)
            {
                var path = new List<int> { to };
                while (path[^1] != from)
                    path.Add(previous[path[^1]]);
                path.Reverse();
                return path;
            }
            foreach (var neighbor in byId[id].Neighbors)
            {
                if (byId.ContainsKey(neighbor) && previous.TryAdd(neighbor, id))
                    pending.Enqueue(neighbor);
            }
        }
        return [];
    }
}
