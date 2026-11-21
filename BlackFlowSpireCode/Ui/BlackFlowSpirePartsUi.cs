using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace BlackFlowSpire.Ui;

internal static class BlackFlowSpirePartsUi
{
    public static void OpenBox(RunState state, bool inspection = false)
    {
        if (!ReferenceEquals(BlackFlowSpireRuntime.State, state) || !BlackFlowSpireRuntime.CanContinueRun
            || BlackFlowSpireRuntime.IsTraveling || CombatManager.Instance.IsInProgress
            || NModalContainer.Instance is not { OpenModal: null } container
            || (NOverlayStack.Instance is { ScreenCount: > 0 } && NMapScreen.Instance is not { IsOpen: true }
                && !BlackFlowSpireBattleSettlementScreen.LootIsOpen)) return;
        // Native overlay stacks are hidden while the map is open. A modal stays
        // above the map and also protects the underlying room from input.
        var mapInspection = NMapScreen.Instance is { IsOpen: true } screen
            ? BlackFlowSpireMapUi.IsInspection(screen) : state.CurrentRoom is not MapRoom;
        container.Add(new BlackFlowSpirePartsSelectionScreen(state, inspection || mapInspection) { Name = "BlackFlowSpirePartsSelection" }, showBackstop: false);
    }

    internal static void DismissBox()
    {
        if (NModalContainer.Instance is { OpenModal: BlackFlowSpirePartsSelectionScreen } container) container.Clear();
    }

    internal static void RefreshSharedBox()
    {
        if (NModalContainer.Instance?.OpenModal is BlackFlowSpirePartsSelectionScreen screen) screen.RefreshShared();
    }

    public static string ConceptTitle(BlackFlowSpireConceptPartKind kind) => BlackFlowSpireText.Get(BlackFlowSpireText.PartKey(BlackFlowSpireShopParts.IconId(kind)) + ".name");
    public static string ConceptDescription(BlackFlowSpireConceptPartKind kind)
    {
        var part = BlackFlowSpireConceptParts.Get(kind);
        return BlackFlowSpireText.Get(BlackFlowSpireText.PartKey(BlackFlowSpireShopParts.IconId(kind)) + ".desc", ("Speed", part.AttackSpeed),
            ("VictoryRelics", part.VictoryRelics), ("Relics", part.Relics), ("RareRelics", part.RareRelics),
            ("Activations", part.Activations.GetValueOrDefault()), ("Shield", part.Shield));
    }
    public static string NaturalTitle(BlackFlowSpireNaturalPartKind kind) => BlackFlowSpireText.Get(BlackFlowSpireText.PartKey(BlackFlowSpireShopParts.IconId(kind)) + ".name");
    public static string NaturalDescription(BlackFlowSpireNaturalPartKind kind) => BlackFlowSpireText.Get(BlackFlowSpireText.PartKey(BlackFlowSpireShopParts.IconId(kind)) + ".desc");

    public static void OpenSale(RunState state)
    {
        if (BlackFlowSpireRuntime.IsTraveling || !BlackFlowSpireTeam.CanOperate || NModalContainer.Instance?.OpenModal != null) return;
        if (ReferenceEquals(BlackFlowSpireRuntime.State, state) && MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom.Instance is { } room)
            BlackFlowSpireShopUi.Find(room)?.SetSelling(true);
    }
    public static string Title(BlackFlowSpireProcessedPartKind kind) => BlackFlowSpireText.Get(BlackFlowSpireText.PartKey(BlackFlowSpireShopParts.IconId(kind)) + ".name");
    public static string Description(BlackFlowSpireProcessedPartKind kind)
    {
        var definition = BlackFlowSpireProcessedParts.Get(kind);
        return BlackFlowSpireText.Get(BlackFlowSpireText.PartKey(BlackFlowSpireShopParts.IconId(kind)) + ".desc",
            ("Total", definition.Uses), ("Hope", definition.HopeGain));
    }

    public static void Attach(Control root, RunState state, NMapScreen screen, bool inspection,
        Action clearSelection, Action<BlackFlowSpireProcessedPart?> preview)
    {
        var engine = BlackFlowSpireGenerationService.GetEngine(state);
        var widget = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/BlackFlowSpireActionPoints.tscn").Instantiate<Control>();
        widget.Name = "MovementMode";
        root.AddChild(widget);
        BlackFlowSpireLocalization.BindStaticText(widget);
        widget.GetNode<Label>("%ActionPointsValue").Text = BlackFlowSpireGenerationService.GetProgress(state).ActionPointsRemaining.ToString();
        if (engine.LoadedPart is { } loaded)
        {
            widget.Call("bind_movement", BlackFlowSpireShopParts.IconId(loaded.Kind), loaded.RemainingUses,
                BlackFlowSpireProcessedParts.Get(loaded.Kind).Uses);
        }
        else widget.Call("bind_movement", "", 0, 0);
        var canOpen = !inspection && BlackFlowSpireTeam.CanOperate;
        widget.GetNode<Control>("Change").Visible = canOpen;
        root.Call("bind_movement_layout", widget);
        if (!canOpen) return;
        var open = new Button { MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        BlackFlowSpireMapAssets.ClearButtonStyle(open);
        widget.AddChild(open);
        open.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        open.TooltipText = BlackFlowSpireText.Get("UI.MOVEMENT.title");
        open.Pressed += () =>
        {
            if (NModalContainer.Instance is not { OpenModal: null } container
                || !BlackFlowSpireMovementModeScreen.CanOpen(state, screen)) return;
            clearSelection();
            container.Add(new BlackFlowSpireMovementModeScreen(state, screen, widget, preview) { Name = "BlackFlowSpireMovementMode" }, false);
        };
    }
}
