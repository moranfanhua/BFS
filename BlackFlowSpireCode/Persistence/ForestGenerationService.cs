using System.Globalization;
using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Integration;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace BlackFlowSpire.Persistence;

public sealed class ForestRunData
{
    public int SchemaVersion { get; set; } = 1;
    public bool Enabled { get; set; }
    public ulong? TeamOwnerId { get; set; }
    public int TargetLife { get; set; } = ForestCombatHealth.InitialTargetLife;
    public int MaxTargetLife { get; set; } = ForestCombatHealth.InitialTargetLife;
    public int TargetShield { get; set; }
    public ForestDamageMultipliers DamageMultipliers { get; set; } = new();
    public Dictionary<ulong, decimal> PlayerLifeMultipliers { get; set; } = [];
    public Dictionary<int, ForestMap> Maps { get; set; } = [];
    public Dictionary<int, ForestExplorationProgress> Progress { get; set; } = [];
    public ForestEngineInventory Engine { get; set; } = new();
    public ForestFloorTransition? PendingFloorTransition { get; set; }
}

public static class ForestGenerationService
{
    private static RunSavedData<ForestRunData>? _slot;

    internal static void Register()
    {
        _slot ??= RitsuLibFramework.GetRunSavedDataStore(Entry.ModId)
            .Register<ForestRunData>("forest_maps_v1");
    }

    public static bool IsEnabled(RunState? state) => state != null && _slot != null
        && _slot.TryGet(state, out var data) && data.Enabled;

    public static void Enable(RunState state) => _slot!.Modify(state, data => data.Enabled = true);
    internal static ulong? GetTeamOwnerId(RunState state) => _slot!.Get(state).TeamOwnerId;

    public static ForestDamageMultipliers GetDamageMultipliers(RunState state) => _slot!.Get(state).DamageMultipliers;

    public static void SetDamageMultipliers(RunState state, ForestDamageMultipliers multipliers, bool replicated = false)
    {
        if (!IsEnabled(state) || (!replicated && ForestTeam.IsGuest)) return;
        // Team snapshots apply between battles. A change during an online battle
        // must come from a deterministic effect executed on all peers.
        if (!replicated && ForestTeam.IsOnline && CombatManager.Instance.IsInProgress)
            throw new InvalidOperationException("Changes to damage multipliers during an online battle must be replicated.");
        RestoreDamageMultipliers(state, multipliers);
        if (!replicated) ForestTeam.Publish(state);
    }

    internal static void RestoreDamageMultipliers(RunState state, ForestDamageMultipliers multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);
        _slot!.Modify(state, data => data.DamageMultipliers = multipliers);
    }

    public static decimal GetPlayerLifeMultiplier(RunState state, ulong playerId) =>
        _slot!.Get(state).PlayerLifeMultipliers.GetValueOrDefault(playerId, 1m);

    public static void SetPlayerLifeMultiplier(RunState state, ulong playerId, decimal multiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(multiplier);
        if (!IsEnabled(state) || ForestTeam.IsGuest) return;
        _slot!.Modify(state, data =>
        {
            if (multiplier == 1m) data.PlayerLifeMultipliers.Remove(playerId);
            else data.PlayerLifeMultipliers[playerId] = multiplier;
        });
        ForestTeam.Publish(state);
    }

    internal static Dictionary<ulong, decimal> CopyPlayerLifeMultipliers(RunState state) =>
        new(_slot!.Get(state).PlayerLifeMultipliers);

    internal static void RestorePlayerLifeMultipliers(RunState state, Dictionary<ulong, decimal>? multipliers) =>
        _slot!.Modify(state, data => data.PlayerLifeMultipliers = multipliers ?? []);

    internal static (int Current, int Maximum, int Shield) GetTargetLife(RunState state)
    {
        var data = _slot!.Get(state);
        return (data.TargetLife, data.MaxTargetLife, data.TargetShield);
    }

    internal static void SetTargetLife(RunState state, int life, int maximum, int shield) =>
        _slot!.Modify(state, data =>
        {
            data.MaxTargetLife = Math.Max(1, maximum);
            data.TargetLife = Math.Clamp(life, 0, data.MaxTargetLife);
            data.TargetShield = Math.Max(0, shield);
        });

    public static void GrantTargetLife(RunState state, int maxLifeGain = 0, int lifeRestored = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLifeGain);
        ArgumentOutOfRangeException.ThrowIfNegative(lifeRestored);
        if (!IsEnabled(state) || ForestTeam.IsGuest) return;

        _slot!.Modify(state, data =>
        {
            data.MaxTargetLife = checked(data.MaxTargetLife + maxLifeGain);
            data.TargetLife = (int)Math.Min(data.MaxTargetLife, (long)data.TargetLife + lifeRestored);
        });
        ForestTeam.Publish(state);
    }

    // A replicated movement reward already runs once on every peer before combat starts.
    public static void GrantTargetShield(RunState state, int amount, bool replicated = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount == 0 || !IsEnabled(state) || (!replicated && ForestTeam.IsGuest)) return;
        _slot!.Modify(state, data => data.TargetShield = checked(data.TargetShield + amount));
        if (!replicated) ForestTeam.Publish(state);
    }

    internal static int SettleCombatHealth(RunState state, ForestCombatHealth health, long teamMaxHp) =>
        _slot!.Modify(state, data =>
        {
            var loveGrassCount = data.Engine.NaturalParts.Count(part => part.Kind == ForestNaturalPartKind.LoveGrass);
            var (lifeLost, shieldUsed) = health.GetLosses(teamMaxHp, data.TargetLife, data.TargetShield, loveGrassCount);
            ForestNaturalRules.CombatFinished(data.Engine, targetLifeLost: lifeLost);
            data.TargetLife -= lifeLost;
            data.TargetShield -= shieldUsed;
        }).TargetLife;

    internal static bool IsTeamLobbyEnabled(StartRunLobby lobby) => _slot!.Lobby.TryGet(lobby, out var data) && data.Enabled;

    internal static void SetTeamLobby(StartRunLobby lobby, bool enabled)
    {
        if (enabled) _slot!.Lobby.Set(lobby, new ForestRunData { Enabled = true, TeamOwnerId = lobby.NetService.NetId });
        else _slot!.Lobby.Remove(lobby);
    }

    internal static void SetSharedEngine(RunState state, ForestEngineInventory engine) =>
        _slot!.Modify(state, data => data.Engine = engine);

    public static ForestEngineInventory GetEngine(RunState state) => _slot!.Get(state).Engine;

    public static ForestFloorTransition? GetFloorTransition(RunState state) => _slot!.Get(state).PendingFloorTransition;
    public static void SetFloorTransition(RunState state, ForestFloorTransition? transition) =>
        _slot!.Modify(state, data => data.PendingFloorTransition = transition);

    internal static void ApplyFloorTransition(RunState state)
    {
        if (GetFloorTransition(state) is not { } transition || state.CurrentActIndex != transition.SourceActIndex + 1) return;
        var map = GetOrGenerate(state);
        var progress = GetProgress(state);
        progress.ActionPointsRemaining = checked(map.Options.InitialActionPoints + transition.ActionPointBonus);
        // Add after EnterFloor has expired old temporary parts, so a newly won
        // spring or XiaoBajie survives for use on the destination floor.
        ModifyEngine(state, engine => engine.Add(transition.Reward));
        MarkChanged(state, map, progress);
        SetFloorTransition(state, null);
    }

    public static void ModifyEngine(RunState state, Action<ForestEngineInventory> update) =>
        _slot!.Modify(state, data => update(data.Engine));

    public static ForestExplorationProgress GetProgress(RunState state)
    {
        var map = GetOrGenerate(state);
        var data = _slot!.Get(state);
        if (!data.Progress.TryGetValue(state.CurrentActIndex, out var progress))
        {
            progress = ForestTravelRules.CreateProgress(map);
            _slot.Modify(state, saved => saved.Progress[state.CurrentActIndex] = progress);
        }
        return progress;
    }

    public static void MarkChanged(RunState state, ForestMap map, ForestExplorationProgress progress)
    {
        _slot!.Modify(state, data =>
        {
            data.Maps[state.CurrentActIndex] = map;
            data.Progress[state.CurrentActIndex] = progress;
        });
    }

    public static ForestMap GetOrGenerate(RunState runState, ForestGenerationOptions? options = null)
    {
        var slot = _slot ?? throw new InvalidOperationException("BlackFlowSpire has not initialized its run-data slot.");
        if (slot.TryGet(runState, out var saved))
        {
            if (saved.SchemaVersion != 1)
                throw new InvalidDataException("Unsupported BlackFlowSpire run-data version.");
            if (saved.Maps.TryGetValue(runState.CurrentActIndex, out var existing))
            {
                ForestMapValidator.Validate(existing);
                saved.Progress.TryGetValue(runState.CurrentActIndex, out var progress);
                var populated = ForestEncounterService.Populate(runState, existing, progress);
                if (ForestFog.RevealNearby(existing, GetEngine(runState)).Count > 0 || populated)
                    slot.Modify(runState, data => data.Maps[runState.CurrentActIndex] = existing);
                return existing;
            }
        }
        var seed = string.IsNullOrWhiteSpace(runState.Rng.StringSeed)
            ? runState.Rng.Seed.ToString(CultureInfo.InvariantCulture)
            : runState.Rng.StringSeed;
        var map = ForestMapGenerator.Generate(seed, runState.CurrentActIndex, options);
        ForestNaturalRules.NodesRevealed(GetEngine(runState), map.Nodes.Count(node => node.Revealed && node.Kind != ForestNodeKind.Empty));
        // Opt in only newly generated maps; existing maps keep their frozen
        // encounters, while still receiving stage text when a matching key exists.
        if (ForestStageCatalog.ForRegion(runState.Act.Id.Entry).Count > 0
            && ForestStageCatalog.FindBossByEncounter(runState.Act.BossEncounter.Id.ToString()) is { IsBoss: true })
            map.StageCatalogVersion = 1;
        ForestEncounterService.Populate(runState, map, null);
        slot.Modify(runState, data => data.Maps.Add(runState.CurrentActIndex, map));
        return map;
    }

    public static IReadOnlyList<int> UpdatePosition(RunState runState, int targetNodeId)
    {
        var map = GetOrGenerate(runState);
        var newlyRevealed = ForestFog.MoveToAdjacent(map, targetNodeId);
        // Mark the RitsuLib slot dirty even if this move only changes position.
        _slot!.Modify(runState, data => data.Maps[runState.CurrentActIndex] = map);
        return newlyRevealed;
    }
}
