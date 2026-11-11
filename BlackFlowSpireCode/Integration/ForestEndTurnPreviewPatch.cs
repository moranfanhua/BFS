using BlackFlowSpire.Map.Presentation;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.CreateAndShow),
    new[] { typeof(Control), typeof(IHoverTip), typeof(HoverTipAlignment) })]
internal static class ForestEndTurnPreviewPatch
{
    private static readonly AccessTools.FieldRef<NEndTurnButton, CombatState?> CombatState =
        AccessTools.FieldRefAccess<NEndTurnButton, CombatState?>("_combatState");

    private static void Prefix(Control owner, ref IHoverTip hoverTip)
    {
        if (owner is not NEndTurnButton button || hoverTip is not HoverTip original) return;
        var state = CombatState(button);
        if (state == null || !ForestSpeedRuntime.IsEnabled(state) || !ForestSpeedRuntime.GetTimeline(state).Started)
            return;
        if (NInputManager.Instance is not { } input) return;

        var next = ForestSpeedRuntime.PreviewNextSide(state);
        var side = ForestText.Get(next == CombatSide.Player ? "UI.SPEED.player" : "UI.SPEED.enemy");
        // HoverTip is a value type: its native constructor may be emitted as
        // call .ctor rather than newobj. Replace the already-created tooltip
        // through this API instead of depending on compiler-specific IL.
        var title = new LocString("static_hover_tips", "END_TURN.title");
        title.Add("Hotkey", input.GetCurrentHotkey(MegaInput.endTurn).ToString());
        hoverTip = new HoverTip(title, ForestText.Get("UI.SPEED.end_turn_description", ("Side", side)), original.Icon)
        {
            Id = original.Id
        };
    }
}
