using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal sealed partial class BlackFlowSpireWishUi(RunState state, NMapScreen screen) : Node
{
    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || BlackFlowSpireRuntime.IsTraveling || !screen.IsOpen || !screen.IsVisibleInTree()
            || NModalContainer.Instance is not { OpenModal: null } container) return;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (BlackFlowSpireWishRules.IsPending(map, progress))
            container.Add(new BlackFlowSpireWishScreen(state, screen, map, progress), showBackstop: false);
    }

    internal static void Dismiss(NMapScreen screen)
    {
        if (NModalContainer.Instance is { OpenModal: BlackFlowSpireWishScreen wish } container
            && ReferenceEquals(wish.Screen, screen)) container.Clear();
    }
}

internal sealed partial class BlackFlowSpireWishScreen(RunState state, NMapScreen screen, BlackFlowSpireMap map,
    BlackFlowSpireExplorationProgress progress) : Control, IScreenContext
{
    private BlackFlowSpireEventView _view = null!;
    private BlackFlowSpireWishProgress? _shown;
    private int[] _actions = [];
    private bool _submitting;
    private bool _focused;

    internal NMapScreen Screen => screen;
    public NetScreenType ScreenType => NetScreenType.Room;
    public Control? DefaultFocusedControl => _submitting ? null : _actions.Length == 0 ? _view?.LeaveButton : _view?.GetChoice(0);

    public override void _Ready()
    {
        FitViewport();
        MouseFilter = MouseFilterEnum.Stop;
        _view = new BlackFlowSpireEventView(this, index => Submit(_actions[index]), () => Submit(BlackFlowSpireWishRules.Leave));
        if (BlackFlowSpireTeam.IsHost) AddChild(new BlackFlowSpireTeamMapStatus(state));
        BlackFlowSpireLocalization.RefreshWithLocale(this, Refresh);
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || !GodotObject.IsInstanceValid(screen) || !screen.IsOpen || !BlackFlowSpireWishRules.IsPending(map, progress))
        {
            BlackFlowSpireWishUi.Dismiss(screen);
            return;
        }
        FitViewport();
        var busy = _submitting || BlackFlowSpireRuntime.IsTraveling;
        _view.SetBusy(busy);
        if (!busy && _shown != progress.Wish) Refresh();
        if (!_focused && DefaultFocusedControl is Button { Disabled: false } focus)
        {
            focus.GrabFocus();
            _focused = true;
        }
    }

    private void FitViewport()
    {
        var area = GetViewportRect();
        var inverse = (GetParent() as Control)?.GetGlobalTransformWithCanvas().AffineInverse() ?? Transform2D.Identity;
        Position = inverse * area.Position;
        Size = inverse * area.End - Position;
    }

    private void Refresh()
    {
        _shown = progress.Wish;
        _focused = false;
        _view.ResetSelection();
        var background = BlackFlowSpireEventView.LoadTexture("pic_rogue_6_25");
        var engine = BlackFlowSpireGenerationService.GetEngine(state);
        var claimed = _shown?.ClaimedId;
        var offers = claimed == null ? BlackFlowSpireWishRules.Offers(map, engine, _shown?.Refreshed == true) : [];
        if (offers.Length == 0)
        {
            _actions = [];
            var result = claimed != null && BlackFlowSpireCollectibleCatalog.Find(claimed) is { } item
                ? Text("result").Replace("{item}", item.Title, StringComparison.Ordinal) : Text("empty");
            _view.SetStory(background, Text("title"), result);
            _view.ShowResult(Text("leave"));
            return;
        }

        _view.SetStory(background, Text("title"), Text(_shown?.Refreshed == true ? "refreshed" : "intro"));
        var options = offers.Select((item, index) => new BlackFlowSpireEventOption(Text($"barrel.{index}"),
            $"{item.Title}\n{item.Description}", ResourceLoader.Load<Texture2D>(item.IconPath))).ToList();
        var actions = offers.Select(item => item.Number).ToList();
        if (_shown == null)
        {
            var price = BlackFlowSpireWishRules.RefreshPrice * BlackFlowSpirePartService.GoldPerOriginium;
            var affordable = BlackFlowSpireTeam.Owner(state)?.Gold >= price;
            actions.Add(BlackFlowSpireWishRules.Refresh);
            options.Add(new(Text("refresh"), Text("refresh.description"), BlackFlowSpireEventView.LoadTexture("rogue_6_gold"),
                affordable && BlackFlowSpireWishRules.CanRefresh(map, progress, engine)));
        }
        _actions = actions.ToArray();
        _view.SetOptions(options);
    }

    private void Submit(int choice)
    {
        if (_submitting || BlackFlowSpireRuntime.IsTraveling || !BlackFlowSpireTeam.CanTravel(state)) return;
        _submitting = true;
        _view.SetBusy(true);
        Callable.From(() => TaskHelper.RunSafely(SubmitAsync(choice))).CallDeferred();
    }

    private async Task SubmitAsync(int choice)
    {
        try { await BlackFlowSpireRuntime.ResolveWish(choice); }
        finally { _submitting = false; }
    }

    private static string Text(string key) => BlackFlowSpireText.Get("UI.WISH." + key);
}
