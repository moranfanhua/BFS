using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpireTeamEventUi(RunState state, NMapScreen screen, BlackFlowSpireMap map,
    BlackFlowSpireExplorationProgress progress, Func<int?> selected, Action<int?> followNode) : Node
{
    private double _nextUpdate;
    private int? _shownNode;
    private BlackFlowSpireObservedEvent? _mirror;

    public override void _Ready() => screen.Closed += OnClosed;

    public override void _Process(double delta)
    {
        _nextUpdate -= delta;
        if (_nextUpdate > 0) return;
        _nextUpdate = 0.10;
        if (!screen.IsOpen || !screen.IsVisibleInTree() || !BlackFlowSpireRuntime.CanContinueRun
            || !ReferenceEquals(state, BlackFlowSpireRuntime.State) || NModalContainer.Instance is not { } container) return;
        if (BlackFlowSpireRuntime.IsTraveling)
        {
            CloseMirror();
            return;
        }
        if (BlackFlowSpireTeam.IsOperator(state))
        {
            CloseMirror();
            if (container.OpenModal is Node modal && !modal.IsNodeReady()) return;
            var view = ReadHostPage(container.OpenModal);
            BlackFlowSpireTeam.ShareEventView(state, map.PositionNodeId, progress.StepsTaken, view);
            return;
        }
        var remote = BlackFlowSpireTeam.ReadEventView(state, map.PositionNodeId, progress.StepsTaken);
        if (_mirror != null && (!ReferenceEquals(container.OpenModal, _mirror) || _mirror.Page != remote.Page)) CloseMirror();
        if (remote.Page == ForestEventPage.Map)
        {
            if (container.OpenModal == null && _shownNode != remote.SelectedNode)
            {
                _shownNode = remote.SelectedNode;
                followNode(_shownNode);
            }
            return;
        }
        if (_mirror == null)
        {
            if (container.OpenModal != null || CreatePage(remote.Page) is not { } content) return;
            _mirror = new BlackFlowSpireObservedEvent(state, remote.Page, content);
            container.Add(_mirror, showBackstop: remote.Page is ForestEventPage.Passage or ForestEventPage.HuntIntel);
        }
        if (_mirror.IsNodeReady()) _mirror.Follow(remote);
    }

    private ForestEventView ReadHostPage(IScreenContext? modal)
    {
        var page = modal switch
        {
            BlackFlowSpireRestScreen => ForestEventPage.Rest,
            BlackFlowSpireWishScreen => ForestEventPage.Wish,
            BlackFlowSpireSacrificeScreen => ForestEventPage.Sacrifice,
            BlackFlowSpireFloorExitScreen => ForestEventPage.FloorExit,
            BlackFlowSpirePassageNotice => ForestEventPage.Passage,
            BlackFlowSpireHuntNotice => ForestEventPage.HuntNotice,
            BlackFlowSpireEnemyIntelModal { IsHunt: true } => ForestEventPage.HuntIntel,
            null when progress.HuntPhase == BlackFlowSpireHuntPhase.AwaitingBattle => ForestEventPage.HuntDetails,
            BlackFlowSpireExpeditionScreen { IsChoice: true } when progress.ExpeditionScene != BlackFlowSpireExpeditionScene.Intro
                => ForestEventPage.ExpeditionResult,
            _ => ForestEventPage.Map
        };
        if (page == ForestEventPage.Map) return modal == null ? ForestEventView.Map with { SelectedNode = selected() } : ForestEventView.Map;
        if (modal is BlackFlowSpireEnemyIntelModal intel) return new(page, intel.SelectedIndex, Scroll: intel.Scroll);
        var choices = (modal as Control)?.GetNodeOrNull<Control>("EventChoice");
        var view = choices != null ? new ForestEventView(page, choices.Call("inspected_choice").AsInt32(),
            TextRevealed: choices.Call("text_revealed").AsBool()) : new ForestEventView(page);
        return modal is BlackFlowSpireSacrificeScreen sacrifice ? sacrifice.ReadPicker(view) : view;
    }

    private Control? CreatePage(ForestEventPage page) => page switch
    {
        ForestEventPage.Rest when BlackFlowSpireRestRules.IsPending(map, progress) =>
            new BlackFlowSpireRestScreen(state, screen, progress, BlackFlowSpireRestRules.Options(map, map.PositionNodeId)),
        ForestEventPage.Wish when BlackFlowSpireWishRules.IsPending(map, progress) => new BlackFlowSpireWishScreen(state, screen, map, progress),
        ForestEventPage.Sacrifice when BlackFlowSpireSacrificeRules.IsPending(map, progress) => new BlackFlowSpireSacrificeScreen(state, screen, map, progress),
        ForestEventPage.FloorExit when BlackFlowSpireFloorExitRules.IsPending(map, progress) => new BlackFlowSpireFloorExitScreen(state, screen),
        ForestEventPage.Passage when BlackFlowSpirePassageRules.IsPending(map, progress) => new BlackFlowSpirePassageNotice(state, screen),
        ForestEventPage.HuntNotice when progress.HuntPhase == BlackFlowSpireHuntPhase.NoticePending => new BlackFlowSpireHuntNotice(state, screen),
        ForestEventPage.HuntDetails when progress.HuntPhase == BlackFlowSpireHuntPhase.AwaitingBattle => new BlackFlowSpireHuntLayer(state, screen),
        ForestEventPage.HuntIntel when progress.HuntPhase == BlackFlowSpireHuntPhase.AwaitingBattle =>
            new BlackFlowSpireEnemyIntelModal(BlackFlowSpireResidentEncounterService.CreateHunt(map), hunt: true),
        ForestEventPage.ExpeditionResult when BlackFlowSpireExpeditionRules.IsPending(map, progress)
            && progress.ExpeditionScene != BlackFlowSpireExpeditionScene.Intro =>
            new BlackFlowSpireExpeditionScreen(state, screen, map, progress, null, () => { }),
        _ => null
    };

    private void CloseMirror()
    {
        if (_mirror == null) return;
        if (NModalContainer.Instance is { } container && ReferenceEquals(container.OpenModal, _mirror)) container.Clear();
        _mirror = null;
    }

    private void OnClosed()
    {
        CloseMirror();
        if (BlackFlowSpireTeam.IsOperator(state) && ReferenceEquals(state, BlackFlowSpireRuntime.State))
            BlackFlowSpireTeam.ShareEventView(state, map.PositionNodeId, progress.StepsTaken, ForestEventView.Map);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(screen)) screen.Closed -= OnClosed;
        CloseMirror();
    }
}

// Keep the event's authored button visuals, while the outer modal owns all guest input.
internal partial class BlackFlowSpireObservedEvent(RunState state, ForestEventPage page, Control content) : Control, IScreenContext
{
    internal ForestEventPage Page => page;
    public NetScreenType ScreenType => NetScreenType.Room;
    public Control? DefaultFocusedControl => null;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
        content.MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Disabled;
        content.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled;
        AddChild(content);
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // The host drives page changes. Child controls still run their own text and artwork animations.
        content.SetProcess(false);
        content.SetProcessUnhandledInput(false);
        // Native popups also bind keyboard shortcuts and automatically close on release.
        foreach (var popup in content.GetChildren().OfType<NVerticalPopup>())
        {
            popup.DisconnectHotkeys();
            popup.DisconnectSignals();
            // NPopupYesNoButton registers its shortcuts on the next idle pass.
            Callable.From(() => { if (GodotObject.IsInstanceValid(popup)) popup.DisconnectHotkeys(); }).CallDeferred();
        }
        AddChild(new BlackFlowSpireTeamMapStatus(state, observing: true) { ZIndex = 1 });
    }

    internal void Follow(ForestEventView view)
    {
        content.GetNodeOrNull<Control>("EventChoice")?.Call("follow_choice", view.Option, view.TextRevealed);
        if (content is BlackFlowSpireSacrificeScreen sacrifice) sacrifice.FollowPicker(view);
        if (content is BlackFlowSpireEnemyIntelModal intel) intel.Follow(view.Option, view.Scroll);
    }
}
