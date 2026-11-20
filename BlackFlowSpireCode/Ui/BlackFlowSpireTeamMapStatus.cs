using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpireTeamMapStatus(RunState state, bool observing = false) : Label
{
    private double _refreshIn;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        OffsetLeft = -300;
        OffsetRight = 300;
        AnchorTop = AnchorBottom = 0.085f;
        OffsetTop = 0;
        OffsetBottom = 28;
        HorizontalAlignment = HorizontalAlignment.Center;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeFontOverride("font", BlackFlowSpireFonts.Body);
        AddThemeFontSizeOverride("font_size", 22);
        AddThemeConstantOverride("outline_size", 5);
        AddThemeColorOverride("font_outline_color", Colors.Black);
        if (observing)
        {
            void ShowObserverLabel() => Text = BlackFlowSpireText.Get("UI.TEAM.observing");
            ShowObserverLabel();
            BlackFlowSpireLocalization.RefreshWithLocale(this, ShowObserverLabel);
            SetProcess(false);
        }
    }

    public override void _Process(double delta)
    {
        _refreshIn -= delta;
        if (_refreshIn > 0 || !BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)) return;
        _refreshIn = 0.75;
        if (BlackFlowSpireTeam.IsGuest)
        {
            Visible = NModalContainer.Instance?.OpenModal is not BlackFlowSpireObservedEvent;
            Text = BlackFlowSpireText.Get("UI.TEAM.follow");
            if (!BlackFlowSpireRuntime.IsTraveling && !CombatManager.Instance.IsInProgress) BlackFlowSpireTeam.ReportReady(state, true);
        }
        else
        {
            BlackFlowSpireTeam.ResumeExitIfReady(state);
            BlackFlowSpireTeam.ConfirmExpeditionIfReady(state);
            Text = BlackFlowSpireTeam.CanTravel(state) ? "" : BlackFlowSpireText.Get("UI.TEAM.wait");
        }
    }
}
