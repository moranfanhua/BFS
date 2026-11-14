using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace BlackFlowSpire.Ui;

internal static class ForestPartsUi
{
    public static void OpenBox(RunState state, bool inspection = false)
    {
        if (!ReferenceEquals(ForestRuntime.State, state) || !ForestRuntime.CanContinueRun
            || ForestRuntime.IsTraveling || CombatManager.Instance.IsInProgress
            || NModalContainer.Instance is not { OpenModal: null } container
            || (NOverlayStack.Instance is { ScreenCount: > 0 } && NMapScreen.Instance is not { IsOpen: true }
                && !ForestBattleSettlementScreen.LootIsOpen)) return;
        // Native overlay stacks are hidden while the map is open. A modal stays
        // above the map and also protects the underlying room from input.
        var mapInspection = NMapScreen.Instance is { IsOpen: true } screen
            ? ForestMapUi.IsInspection(screen) : state.CurrentRoom is not MapRoom;
        container.Add(new ForestPartsSelectionScreen(state, inspection || mapInspection) { Name = "ForestPartsSelection" }, showBackstop: false);
    }

    internal static void DismissBox()
    {
        if (NModalContainer.Instance is { OpenModal: ForestPartsSelectionScreen } container) container.Clear();
    }

    internal static void RefreshSharedBox()
    {
        if (NModalContainer.Instance?.OpenModal is ForestPartsSelectionScreen screen) screen.RefreshShared();
    }

    public static string ConceptTitle(ForestConceptPartKind kind) => ForestText.Get(ForestText.PartKey(ForestShopParts.IconId(kind)) + ".name");
    public static string ConceptDescription(ForestConceptPartKind kind)
    {
        var part = ForestConceptParts.Get(kind);
        return ForestText.Get(ForestText.PartKey(ForestShopParts.IconId(kind)) + ".desc", ("Speed", part.AttackSpeed),
            ("VictoryRelics", part.VictoryRelics), ("Relics", part.Relics), ("RareRelics", part.RareRelics),
            ("Activations", part.Activations.GetValueOrDefault()), ("Shield", part.Shield));
    }
    public static string NaturalTitle(ForestNaturalPartKind kind) => ForestText.Get(ForestText.PartKey(ForestShopParts.IconId(kind)) + ".name");
    public static string NaturalDescription(ForestNaturalPartKind kind) => ForestText.Get(ForestText.PartKey(ForestShopParts.IconId(kind)) + ".desc");

    public static void OpenSale(RunState state)
    {
        if (ForestRuntime.IsTraveling || !ForestTeam.CanManage(state) || NModalContainer.Instance?.OpenModal != null) return;
        if (ReferenceEquals(ForestRuntime.State, state) && MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom.Instance is { } room)
            ForestShopUi.Find(room)?.SetSelling(true);
    }
    public static string Title(ForestProcessedPartKind kind) => ForestText.Get(ForestText.PartKey(ForestShopParts.IconId(kind)) + ".name");
    public static string Description(ForestProcessedPartKind kind)
    {
        var definition = ForestProcessedParts.Get(kind);
        return ForestText.Get(ForestText.PartKey(ForestShopParts.IconId(kind)) + ".desc",
            ("Total", definition.Uses), ("Hope", definition.HopeGain));
    }

    public static void Attach(Control root, RunState state, NMapScreen screen, bool inspection,
        Action clearSelection, Action<ForestProcessedPart?> preview)
    {
        var engine = ForestGenerationService.GetEngine(state);
        var widget = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/ForestActionPoints.tscn").Instantiate<Control>();
        widget.Name = "MovementMode";
        root.AddChild(widget);
        ForestLocalization.BindStaticText(widget);
        widget.GetNode<Label>("%ActionPointsValue").Text = ForestGenerationService.GetProgress(state).ActionPointsRemaining.ToString();
        if (engine.LoadedPart is { } loaded)
        {
            widget.Call("bind_movement", ForestShopParts.IconId(loaded.Kind), loaded.RemainingUses,
                ForestProcessedParts.Get(loaded.Kind).Uses);
        }
        else widget.Call("bind_movement", "", 0, 0);
        var canOpen = !inspection && !ForestTeam.IsGuest;
        widget.GetNode<Control>("Change").Visible = canOpen;
        var debugInfo = NRun.Instance?.GlobalUi.DebugInfo;
        root.Call("bind_movement_layout", widget, debugInfo == null ? default(Variant) : (Variant)debugInfo);
        if (!canOpen) return;
        var open = new Button { MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        ForestMapAssets.ClearButtonStyle(open);
        widget.AddChild(open);
        open.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        open.TooltipText = ForestText.Get("UI.MOVEMENT.title");
        open.Pressed += () =>
        {
            if (NModalContainer.Instance is not { OpenModal: null } container
                || !ForestMovementModeScreen.CanOpen(state, screen)) return;
            clearSelection();
            container.Add(new ForestMovementModeScreen(state, screen, widget, preview) { Name = "ForestMovementMode" }, false);
        };
    }
}
