using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal partial class ForestTeamMapStatus(RunState state) : Label
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
        AddThemeFontOverride("font", ForestFonts.Body);
        AddThemeFontSizeOverride("font_size", 22);
        AddThemeConstantOverride("outline_size", 5);
        AddThemeColorOverride("font_outline_color", Colors.Black);
    }

    public override void _Process(double delta)
    {
        _refreshIn -= delta;
        if (_refreshIn > 0 || !ForestRuntime.CanContinueRun || !ReferenceEquals(ForestRuntime.State, state)) return;
        _refreshIn = 0.75;
        if (ForestTeam.IsGuest)
        {
            Text = ForestText.Get("UI.TEAM.follow");
            if (!ForestRuntime.IsTraveling && !CombatManager.Instance.IsInProgress) ForestTeam.ReportReady(state, true);
        }
        else Text = ForestTeam.CanTravel(state) ? "" : ForestText.Get("UI.TEAM.wait");
    }
}
