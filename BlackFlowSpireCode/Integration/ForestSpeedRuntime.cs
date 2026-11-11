using System.Runtime.CompilerServices;
using BlackFlowSpire.Combat;
using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

public static class ForestSpeedRuntime
{
    private sealed class BattleClock
    {
        public ForestActionTimeline Timeline { get; } = new();
        public HashSet<Creature> PreparedIntents { get; } = [];
        public Queue<Func<Task>> EnemyActions { get; } = [];
    }

    private static readonly ConditionalWeakTable<CombatState, BattleClock> Clocks = new();
    public static bool IsEnabled(CombatState state) => ForestSpeedIntentPatch.Available
        && state.RunState is RunState run && ForestGenerationService.IsEnabled(run);
    public static ForestActionTimeline GetTimeline(CombatState state) => GetClock(state).Timeline;
    private static BattleClock GetClock(CombatState state) => Clocks.GetValue(state, static _ => new BattleClock());

    public static void SetSpeed(CombatState state, CombatSide side, decimal speed)
    {
        if (!IsEnabled(state)) throw new InvalidOperationException("The forest speed system is not active for this combat.");
        GetTimeline(state).SetSpeed(ToTimelineSide(side), speed);
    }

    public static void AddSpeed(CombatState state, CombatSide side, decimal amount)
    {
        var clock = GetTimeline(state);
        SetSpeed(state, side, (side == CombatSide.Player ? clock.PlayerSpeed : clock.EnemySpeed) + amount);
    }

    internal static void BeginCombat(CombatState state, decimal playerSpeedBonus)
    {
        if (!IsEnabled(state)) return;
        var clock = GetClock(state);
        if (clock.Timeline.Started) return;
        clock.Timeline.SetSpeed(ForestActionSide.Player, clock.Timeline.PlayerSpeed + playerSpeedBonus);
        state.CurrentSide = ToCombatSide(clock.Timeline.Start());
        if (state.CurrentSide == CombatSide.Enemy)
        {
            // Vanilla initializes its counters for a player-first battle. An
            // enemy-first schedule must still call the first player turn "1".
            state.RoundNumber = 0;
            foreach (var player in state.Players)
                AccessTools.PropertySetter(player.PlayerCombatState!.GetType(), "TurnNumber").Invoke(player.PlayerCombatState, [0]);
        }
    }

    public static CombatSide PreviewNextSide(CombatState state)
    {
        if (!IsEnabled(state)) throw new InvalidOperationException("The forest speed system is not active for this combat.");
        // Query the same native extra-turn rule used when the turn actually
        // ends. Previewing must not advance the clock or consume that effect.
        if (state.CurrentSide == CombatSide.Player && state.Players.Any(player => Hook.ShouldTakeExtraTurn(state, player)))
            return CombatSide.Player;
        return ToCombatSide(GetTimeline(state).Preview(1)[0]);
    }

    internal static CombatSide NextSide(CombatState state, bool extraPlayerTurn)
    {
        var clock = GetClock(state);
        if (state.CurrentSide == CombatSide.Enemy) clock.PreparedIntents.Clear();
        return ToCombatSide(clock.Timeline.CompleteTurn(extraPlayerTurn));
    }

    internal static void BeforeTurnStart(CombatState state)
    {
        if (!IsEnabled(state) || !CombatManager.Instance.IsInProgress) return;
        var clock = GetClock(state);
        if (!clock.Timeline.Started) return;
        if (state.CurrentSide == CombatSide.Enemy)
            foreach (var enemy in state.Enemies)
                if (clock.PreparedIntents.Add(enemy)) enemy.PrepareForNextTurn(state.PlayerCreatures);
    }

    // Only the native player-start preparation call is redirected here; monster
    // scripts remain free to deliberately change their intent during a turn.
    internal static void PrepareEnemyIntentForPlayerTurn(Creature enemy, IEnumerable<Creature> targets, bool rollNewMove)
    {
        if (enemy.CombatState is not CombatState state || !IsEnabled(state))
        {
            enemy.PrepareForNextTurn(targets, rollNewMove);
            return;
        }
        enemy.PrepareForNextTurn(targets, GetClock(state).PreparedIntents.Add(enemy) && rollNewMove);
    }

    internal static void RememberSpawnedEnemyIntent(CombatState state, Creature enemy)
    {
        // Native AfterCreatureAdded already rolls the intent of enemies spawned
        // during a player turn. Do not reroll it at the next scheduled turn.
        if (IsEnabled(state) && CombatManager.Instance.IsInProgress && state.CurrentSide == CombatSide.Player
            && enemy.IsEnemy && GetTimeline(state).Started) GetClock(state).PreparedIntents.Add(enemy);
    }

    internal static Func<Task>? DeferEnemyAction(CombatState state, Func<Task>? action)
    {
        if (!IsEnabled(state)) return action;
        var actions = GetClock(state).EnemyActions;
        if (action != null) actions.Enqueue(action);
        if (state.CurrentSide == CombatSide.Player || actions.Count == 0) return null;
        var pending = actions.ToArray();
        actions.Clear();
        return async () => { foreach (var callback in pending) await callback(); };
    }

    internal static ForestActionSide ToTimelineSide(CombatSide side) => side == CombatSide.Player ? ForestActionSide.Player : ForestActionSide.Enemy;
    internal static CombatSide ToCombatSide(ForestActionSide side) => side == ForestActionSide.Player ? CombatSide.Player : CombatSide.Enemy;
}
