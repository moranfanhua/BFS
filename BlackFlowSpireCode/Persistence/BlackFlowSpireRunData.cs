using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Persistence;

public sealed partial class BlackFlowSpireRunData
{
    internal const int CurrentSchemaVersion = 2;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public bool Enabled { get; set; }
    public ulong? TeamOwnerId { get; set; }
    public long TeamCommandId { get; set; }
    public int TargetLife { get; set; } = BlackFlowSpireCombatHealth.InitialTargetLife;
    public int MaxTargetLife { get; set; } = BlackFlowSpireCombatHealth.InitialTargetLife;
    public int TargetShield { get; set; }
    public int CommandExperience { get; set; }
    public BlackFlowSpireDamageMultipliers DamageMultipliers { get; set; } = new();
    public Dictionary<ulong, decimal> PlayerLifeMultipliers { get; set; } = [];
    public Dictionary<int, BlackFlowSpireMap> Maps { get; set; } = [];
    public Dictionary<int, BlackFlowSpireExplorationProgress> Progress { get; set; } = [];
    public BlackFlowSpireEngineInventory Engine { get; set; } = new();
    public BlackFlowSpireFloorTransition? PendingFloorTransition { get; set; }

    internal BlackFlowSpireRunData ActiveFloor(int act)
    {
        var copy = (BlackFlowSpireRunData)MemberwiseClone();
        copy.Maps = new() { [act] = Maps[act] };
        copy.Progress = new() { [act] = Progress[act] };
        return copy;
    }

    internal void KeepOtherFloors(BlackFlowSpireRunData previous, int act)
    {
        foreach (var (index, map) in previous.Maps)
            if (index != act) Maps[index] = map;
        foreach (var (index, progress) in previous.Progress)
            if (index != act) Progress[index] = progress;
    }
}
