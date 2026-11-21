using System.Globalization;
using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Integration;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace BlackFlowSpire.Persistence;

public static partial class BlackFlowSpireGenerationService
{
    private static RunSavedData<BlackFlowSpireRunData>? _slot;

    internal static void Register()
    {
        _slot ??= RitsuLibFramework.GetRunSavedDataStore(Entry.ModId)
            .Register<BlackFlowSpireRunData>(Entry.ModId + "_maps_v1");
    }

    public static bool IsEnabled(RunState? state) => state != null && _slot != null
        && _slot.TryGet(state, out var data) && data.Enabled;

    public static void Enable(RunState state) => _slot!.Modify(state, data => data.Enabled = true);
    internal static BlackFlowSpireRunData TeamData(RunState state) => _slot!.Get(state);
    internal static void SaveTeamChange(RunState state) => _slot!.Set(state, _slot.Get(state));
    internal static ulong? GetTeamOwnerId(RunState state) => _slot!.Get(state).TeamOwnerId;
    internal static long GetCommandId(RunState state) => _slot!.Get(state).TeamCommandId;
    internal static void SetCommandId(RunState state, long id) => _slot!.Modify(state, data => data.TeamCommandId = id);

    internal static BlackFlowSpireRunData CaptureFloor(RunState state)
    {
        GetProgress(state);
        return _slot!.Get(state).ActiveFloor(state.CurrentActIndex);
    }

    internal static void ApplyFloor(RunState state, BlackFlowSpireRunData authoritative)
    {
        var act = state.CurrentActIndex;
        if (authoritative.SchemaVersion != BlackFlowSpireRunData.CurrentSchemaVersion || !authoritative.Enabled
            || !authoritative.Maps.TryGetValue(act, out var map) || map.ActIndex != act || !authoritative.Progress.ContainsKey(act))
            throw new InvalidDataException("Invalid authoritative forest floor.");
        BlackFlowSpireMapValidator.Validate(map);
        var saved = _slot!.Get(state);
        authoritative.KeepOtherFloors(saved, act);
        _slot.Set(state, authoritative);
        if (state.Map is BlackFlowSpireActMap adapter && adapter.Rebind(map)) return;
        state.Map = new BlackFlowSpireActMap(map);
    }

    public static int GetHope(RunState state) => GetHopeStatus(state).Balance;

    private static ulong LocalPlayerId(RunState state) => (LocalContext.GetMe(state) ?? state.Players.Single()).NetId;

    internal static BlackFlowSpireHope GetHopeStatus(RunState state) => GetHopeStatus(state, LocalPlayerId(state));

    internal static BlackFlowSpireHope GetHopeStatus(RunState state, ulong playerId) => _slot!.Get(state).GetHope(playerId);

    internal static int GetCommandExperience(RunState state) => _slot!.Get(state).CommandExperience;

    // Gains raise both values. Spending reduces only the balance.
    public static bool TryChangeHope(RunState state, int delta, bool replicated = false) =>
        IsEnabled(state) && TryChangeHope(state, LocalPlayerId(state), delta, replicated);

    internal static bool TryChangeHope(RunState state, ulong playerId, int delta, bool replicated = false)
    {
        if (!IsEnabled(state) || (!replicated && BlackFlowSpireTeam.IsGuest)) return false;
        if (delta == 0) return true;
        var changed = false;
        _slot!.Modify(state, data => changed = data.TryChangeHope(playerId, delta));
        if (changed && !replicated) BlackFlowSpireTeam.Publish(state);
        return changed;
    }

    internal static void RefundHope(RunState state, ulong playerId, int amount)
    {
        if (amount == 0 || BlackFlowSpireTeam.IsGuest) return;
        _slot!.Modify(state, data => data.RefundHope(playerId, amount));
        BlackFlowSpireTeam.Publish(state);
    }

    internal static (int Experience, int Hope) GrantCommandExperience(RunState state, int amount)
    {
        if (amount <= 0) return default;
        (int Experience, int Hope) reward = default;
        _slot!.Modify(state, data =>
        {
            var previous = Math.Clamp(data.CommandExperience, 0, BlackFlowSpireCommandLevel.MaximumExperience);
            var next = previous + Math.Min(amount, BlackFlowSpireCommandLevel.MaximumExperience - previous);
            if (previous == next) return;
            var earned = BlackFlowSpireCommandLevel.FromExperience(next).HopeEarned
                - BlackFlowSpireCommandLevel.FromExperience(previous).HopeEarned;
            GrantTeamHope(state, data, earned);
            data.CommandExperience = next;
            reward = (next - previous, earned);
        });
        return reward;
    }

    private static void GrantTeamHope(RunState state, BlackFlowSpireRunData data, int amount) =>
        data.GrantHopeToPlayers(state.Players.Select(player => player.NetId), amount);

    public static BlackFlowSpireDamageMultipliers GetDamageMultipliers(RunState state) => _slot!.Get(state).DamageMultipliers;

    public static void SetDamageMultipliers(RunState state, BlackFlowSpireDamageMultipliers multipliers, bool replicated = false)
    {
        if (!IsEnabled(state) || (!replicated && BlackFlowSpireTeam.IsGuest)) return;
        // Team snapshots apply between battles. A change during an online battle
        // must come from a deterministic effect executed on all peers.
        if (!replicated && BlackFlowSpireTeam.IsOnline && CombatManager.Instance.IsInProgress)
            throw new InvalidOperationException("Changes to damage multipliers during an online battle must be replicated.");
        RestoreDamageMultipliers(state, multipliers);
        if (!replicated) BlackFlowSpireTeam.Publish(state);
    }

    internal static void RestoreDamageMultipliers(RunState state, BlackFlowSpireDamageMultipliers multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);
        _slot!.Modify(state, data => data.DamageMultipliers = multipliers);
    }

    public static decimal GetPlayerLifeMultiplier(RunState state, ulong playerId) =>
        _slot!.Get(state).PlayerLifeMultipliers.GetValueOrDefault(playerId, 1m);

    public static void SetPlayerLifeMultiplier(RunState state, ulong playerId, decimal multiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(multiplier);
        if (!IsEnabled(state) || BlackFlowSpireTeam.IsGuest) return;
        _slot!.Modify(state, data =>
        {
            if (multiplier == 1m) data.PlayerLifeMultipliers.Remove(playerId);
            else data.PlayerLifeMultipliers[playerId] = multiplier;
        });
        BlackFlowSpireTeam.Publish(state);
    }

    internal static (int Current, int Maximum, int Shield) GetTargetLife(RunState state)
    {
        var data = _slot!.Get(state);
        return (data.TargetLife, data.MaxTargetLife, data.TargetShield);
    }

    public static void GrantTargetLife(RunState state, int maxLifeGain = 0, int lifeRestored = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLifeGain);
        ArgumentOutOfRangeException.ThrowIfNegative(lifeRestored);
        if (!IsEnabled(state) || BlackFlowSpireTeam.IsGuest) return;

        _slot!.Modify(state, data =>
        {
            data.MaxTargetLife = checked(data.MaxTargetLife + maxLifeGain);
            data.TargetLife = (int)Math.Min(data.MaxTargetLife, (long)data.TargetLife + lifeRestored);
        });
        BlackFlowSpireTeam.Publish(state);
    }

    // A replicated movement reward already runs once on every peer before combat starts.
    public static void GrantTargetShield(RunState state, int amount, bool replicated = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount == 0 || !IsEnabled(state) || (!replicated && BlackFlowSpireTeam.IsGuest)) return;
        _slot!.Modify(state, data => data.TargetShield = checked(data.TargetShield + amount));
        if (!replicated) BlackFlowSpireTeam.Publish(state);
    }

    internal static int SettleCombatHealth(RunState state, BlackFlowSpireCombatHealth health, long teamMaxHp) =>
        _slot!.Modify(state, data =>
        {
            var loveGrassCount = data.Engine.NaturalParts.Count(part => part.Kind == BlackFlowSpireNaturalPartKind.LoveGrass);
            var (lifeLost, shieldUsed) = health.GetLosses(teamMaxHp, data.TargetLife, data.TargetShield, loveGrassCount);
            BlackFlowSpireNaturalRules.CombatFinished(data.Engine, targetLifeLost: lifeLost);
            data.TargetLife -= lifeLost;
            data.TargetShield -= shieldUsed;
        }).TargetLife;

    internal static void RecordSpecialVictory(RunState state, BlackFlowSpireSpecialEncounter kind)
    {
        _slot!.Modify(state, data =>
        {
            if (kind == BlackFlowSpireSpecialEncounter.Hunt
                && (!data.Progress.TryGetValue(state.CurrentActIndex, out var progress) || !BlackFlowSpireHuntRules.WinBattle(progress))) return;
            var collectibles = data.Engine.Collectibles;
            collectibles.SpecialVictoryAttackSpeed = checked(collectibles.SpecialVictoryAttackSpeed
                + BlackFlowSpireCollectibleRules.SpecialVictoryAttackSpeed(collectibles, kind));
        });
    }

    internal static void GrantCollectible(RunState state, BlackFlowSpireMap map, BlackFlowSpireCollectible item)
        => _slot!.Modify(state, data => GrantCollectible(state, data, map, item));

    private static void GrantCollectible(RunState state, BlackFlowSpireRunData data, BlackFlowSpireMap map, BlackFlowSpireCollectible item)
    {
        var collectibles = data.Engine.Collectibles;
        if (collectibles.Has(item.Number)) return;
        var reward = BlackFlowSpireCollectibleRules.OnAcquired(item, data.Engine, map);
        var maxLife = checked(data.MaxTargetLife + reward.MaxLife);
        var shield = checked(data.TargetShield + reward.Shield);
        GrantTeamHope(state, data, reward.Hope);
        collectibles.OwnedIds.Add(item.Id);
        data.MaxTargetLife = maxLife;
        data.TargetLife = (int)Math.Min(maxLife, (long)data.TargetLife + reward.Life);
        data.TargetShield = shield;
        collectibles.PendingGold += reward.Originium * BlackFlowSpirePartService.GoldPerOriginium;
        BlackFlowSpireCollectibleRules.RevealMap(map, data.Engine);
    }

    internal static bool TryResolveSacrifice(RunState state, BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress,
        int argument, bool exchange)
    {
        var changed = false;
        _slot!.Modify(state, data =>
        {
            changed = exchange
                ? BlackFlowSpireSacrificeRules.TryExchange(map, progress, data.Engine, argument)
                : BlackFlowSpireSacrificeRules.TryResolve(map, progress, data.Engine, (BlackFlowSpireSacrificeAction)argument, reward =>
                {
                    if (reward.CollectibleId is { } id && BlackFlowSpireCollectibleCatalog.Find(id) is { } collectible)
                        GrantCollectible(state, data, map, collectible);
                    else if (reward.Natural is { } natural) data.Engine.AddNatural(natural);
                    else if (reward.Processed is { } processed) data.Engine.Add(processed);
                    else if (reward.Concept is { } concept) data.Engine.AddConcept(concept);
                    else return false;
                    return true;
                });
            if (!changed) return;
            data.Maps[state.CurrentActIndex] = map;
            data.Progress[state.CurrentActIndex] = progress;
        });
        return changed;
    }

    internal static bool IsTeamLobbyEnabled(StartRunLobby lobby) => _slot!.Lobby.TryGet(lobby, out var data) && data.Enabled;

    internal static void SetTeamLobby(StartRunLobby lobby, bool enabled)
    {
        if (enabled) _slot!.Lobby.Set(lobby, new BlackFlowSpireRunData
        {
            Enabled = true, TeamOwnerId = lobby.NetService.NetId
        });
        else _slot!.Lobby.Remove(lobby);
    }

    public static BlackFlowSpireEngineInventory GetEngine(RunState state) => _slot!.Get(state).Engine;

    public static BlackFlowSpireFloorTransition? GetFloorTransition(RunState state) => _slot!.Get(state).PendingFloorTransition;
    public static void SetFloorTransition(RunState state, BlackFlowSpireFloorTransition? transition) =>
        _slot!.Modify(state, data => data.PendingFloorTransition = transition);

    internal static void ApplyFloorTransition(RunState state, BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress)
    {
        if (GetFloorTransition(state) is not { } transition || map.ActIndex != transition.SourceActIndex + 1) return;
        var actionPoints = checked(map.Options.InitialActionPoints + transition.ActionPointBonus);
        _slot!.Modify(state, data =>
        {
            if (data.PendingFloorTransition != transition) return;
            GrantTeamHope(state, data, transition.Hope);
            progress.ActionPointsRemaining = actionPoints;
            // EnterFloor has already expired the previous floor's temporary parts.
            data.Engine.Add(transition.Reward);
            data.PendingFloorTransition = null;
        });
    }

    internal static bool TryChooseRest(RunState state, BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress,
        BlackFlowSpireRestChoice choice)
    {
        if (!BlackFlowSpireRestRules.IsPending(map, progress) || progress.RestChoice.HasValue
            || !BlackFlowSpireRestRules.Options(map, map.PositionNodeId).Contains(choice)) return false;

        // Store the reward and its result scene in the same run-data update.
        _slot!.Modify(state, data =>
        {
            switch (choice)
            {
                case BlackFlowSpireRestChoice.MaximumLife:
                    data.MaxTargetLife = checked(data.MaxTargetLife + 3);
                    break;
                case BlackFlowSpireRestChoice.Hope:
                    GrantTeamHope(state, data, 3);
                    break;
                case BlackFlowSpireRestChoice.ActionPoints:
                    progress.ActionPointsRemaining = checked(progress.ActionPointsRemaining + 2);
                    break;
                case BlackFlowSpireRestChoice.PartsCapacity:
                    data.Engine.Capacity = checked(data.Engine.Capacity + 1);
                    break;
            }
            progress.RestChoice = choice;
        });
        return true;
    }

    internal static BlackFlowSpirePartMovementReward? TakeMovementReward(RunState state, BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress)
    {
        if (progress.PendingPartReward is not { } reward) return null;
        if (reward.ActIndex != map.ActIndex || reward.NodeId != map.PositionNodeId)
            throw new InvalidOperationException("The part reward does not match the movement destination.");
        _slot!.Modify(state, data =>
        {
            GrantTeamHope(state, data, reward.Hope);
            progress.PendingPartReward = null;
        });
        return reward;
    }

    public static void ModifyEngine(RunState state, Action<BlackFlowSpireEngineInventory> update) =>
        _slot!.Modify(state, data => update(data.Engine));

    public static BlackFlowSpireExplorationProgress GetProgress(RunState state)
    {
        var map = GetOrGenerate(state);
        var data = _slot!.Get(state);
        if (!data.Progress.TryGetValue(state.CurrentActIndex, out var progress))
        {
            progress = BlackFlowSpireTravelRules.CreateProgress(map);
            _slot.Modify(state, saved => saved.Progress[state.CurrentActIndex] = progress);
        }
        return progress;
    }

    public static void MarkChanged(RunState state, BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress)
    {
        _slot!.Modify(state, data =>
        {
            data.Maps[state.CurrentActIndex] = map;
            data.Progress[state.CurrentActIndex] = progress;
        });
    }

    public static BlackFlowSpireMap GetOrGenerate(RunState runState, BlackFlowSpireGenerationOptions? options = null)
    {
        var slot = _slot ?? throw new InvalidOperationException("BlackFlowSpire has not initialized its run-data slot.");
        if (slot.TryGet(runState, out var saved))
        {
            if (saved.SchemaVersion != BlackFlowSpireRunData.CurrentSchemaVersion)
                throw new InvalidDataException("Unsupported BlackFlowSpire run-data version.");
            if (saved.Maps.TryGetValue(runState.CurrentActIndex, out var existing))
            {
                BlackFlowSpireMapValidator.Validate(existing);
                if (BlackFlowSpireTeam.IsGuest) return existing;
                var revealed = BlackFlowSpireFog.RevealNearby(existing, saved.Engine).Count > 0;
                revealed |= BlackFlowSpireCollectibleRules.RevealMap(existing, saved.Engine);
                if (revealed)
                    slot.Modify(runState, data => data.Maps[runState.CurrentActIndex] = existing);
                return existing;
            }
        }
        var seed = string.IsNullOrWhiteSpace(runState.Rng.StringSeed)
            ? runState.Rng.Seed.ToString(CultureInfo.InvariantCulture)
            : runState.Rng.StringSeed;
        var engine = GetEngine(runState);
        if (engine.Collectibles.Has(32))
        {
            options ??= BlackFlowSpireFloorPlan.GenerationOptions(runState.CurrentActIndex);
            options = options with { InitialActionPoints = checked(options.InitialActionPoints + 1) };
        }
        var map = BlackFlowSpireMapGenerator.Generate(seed, runState.CurrentActIndex, options);
        BlackFlowSpireEncounterService.Populate(runState, map);
        BlackFlowSpireNaturalRules.NodesRevealed(engine, map.Nodes.Count(node => node.Revealed && node.Kind != BlackFlowSpireNodeKind.Empty));
        BlackFlowSpireCollectibleRules.RevealMap(map, engine);
        slot.Modify(runState, data => data.Maps.Add(runState.CurrentActIndex, map));
        return map;
    }
}
