using System.Reflection.Emit;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(Hook), "ModifyDamageInternal")]
internal static class ForestDamageMultiplierPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
    {
        var instructions = source.ToList();
        var hasFlag = AccessTools.Method(typeof(Enum), nameof(Enum.HasFlag));
        var capCheck = new CodeMatcher(instructions).MatchStartForward(
            new CodeMatch(instruction => instruction.IsLdarg(8)),
            new CodeMatch(OpCodes.Box, typeof(ModifyDamageHookType)),
            new CodeMatch(instruction => instruction.LoadsConstant((long)ModifyDamageHookType.Cap)),
            new CodeMatch(OpCodes.Box, typeof(ModifyDamageHookType)),
            new CodeMatch(instruction => instruction.Calls(hasFlag)));
        if (!capCheck.IsValid || instructions.Count < 2
            || !instructions[0].IsLdarg(4) || instructions[1].opcode != OpCodes.Stloc_0)
        {
            Entry.Logger.Warn("[Forest] Could not locate the damage multiplier phase. Using native damage calculation.");
            return instructions;
        }

        // Local 0 is the accumulated damage. Apply this multiplier after the native
        // modifiers, then let the native cap phase and block process that result.
        var loadRunState = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(capCheck.Instruction);
        return capCheck.Insert(
            loadRunState,
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Ldarg_3),
            new CodeInstruction(OpCodes.Ldloc_0),
            CodeInstruction.LoadArgument(8),
            CodeInstruction.Call(typeof(ForestDamageMultiplierPatch), nameof(Apply)),
            new CodeInstruction(OpCodes.Stloc_0)).InstructionEnumeration();
    }

    private static decimal Apply(IRunState runState, ICombatState? combatState, Creature? dealer,
        decimal damage, ModifyDamageHookType phases)
    {
        if ((phases & ModifyDamageHookType.Multiplicative) == 0 || combatState == null || dealer == null
            || runState is not RunState run || !ForestGenerationService.IsEnabled(run)) return damage;
        var collectibles = ForestCollectibleService.GetInventory(run);
        var allyAttack = ForestCollectibleRules.AllyAttack(collectibles);
        if (dealer.IsPlayer) allyAttack *= ForestCombatHealthRuntime.PlayerAttack(combatState);
        if (dealer.Side == CombatSide.Player && dealer.Block > 0 && collectibles.Has(121)) allyAttack *= 1.4m;
        return ForestGenerationService.GetDamageMultipliers(run).Apply(dealer.Side, damage,
            ForestCollectibleRules.AllyDefense(collectibles), ForestCollectibleRules.EnemyDefense(collectibles), allyAttack);
    }
}
