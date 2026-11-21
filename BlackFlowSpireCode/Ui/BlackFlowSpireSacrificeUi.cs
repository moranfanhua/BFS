using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
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

internal partial class BlackFlowSpireSacrificeUi(RunState state, NMapScreen screen) : Node
{
    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || BlackFlowSpireRuntime.IsTraveling || !screen.IsOpen || !screen.IsVisibleInTree()
            || NModalContainer.Instance is not { OpenModal: null } container) return;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (BlackFlowSpireSacrificeRules.IsPending(map, progress))
            container.Add(new BlackFlowSpireSacrificeScreen(state, screen, map, progress), showBackstop: false);
    }

    public static void Dismiss(NMapScreen screen)
    {
        if (NModalContainer.Instance is { OpenModal: BlackFlowSpireSacrificeScreen sacrifice } container
            && ReferenceEquals(sacrifice.Screen, screen)) container.Clear();
    }
}

internal partial class BlackFlowSpireSacrificeScreen(RunState state, NMapScreen screen, BlackFlowSpireMap map,
    BlackFlowSpireExplorationProgress progress) : Control, IScreenContext
{
    private readonly List<(BlackFlowSpireEventOption Option, Action Action)> _choices = [];
    private BlackFlowSpireEventView _eventView = null!;
    private BlackFlowSpireSacrificePicker? _picker;
    private BlackFlowSpireSacrificeProgress? _shown;
    private bool _submitting;
    private bool _focused;
    private bool Busy => _submitting || BlackFlowSpireRuntime.IsTraveling;

    public NMapScreen Screen => screen;
    public NetScreenType ScreenType => NetScreenType.Room;
    public Control? DefaultFocusedControl => Busy ? null : _picker is { } picker ? picker.DefaultFocus
        : BlackFlowSpireSacrificeRules.IsEnding(Scene) ? _eventView?.LeaveButton
        : Enumerable.Range(0, _choices.Count).Select(index => _eventView?.GetChoice(index)).FirstOrDefault(button => button is { Disabled: false });
    private BlackFlowSpireSacrificeScene Scene => progress.Sacrifice?.Scene ?? BlackFlowSpireSacrificeScene.Intro;
    private BlackFlowSpireEngineInventory Engine => BlackFlowSpireGenerationService.GetEngine(state);

    internal ForestEventView ReadPicker(ForestEventView view) => _picker is { } picker
        ? view with { Parts = picker.Parts, Item = picker.SelectedItem, ConfirmLoaded = picker.ConfirmingLoaded, Scroll = picker.Scroll }
        : view;

    internal void FollowPicker(ForestEventView view)
    {
        if (_picker != null && _picker.Parts != view.Parts) ClosePicker();
        if (view.Parts is not { } parts) return;
        if (_picker == null) OpenPicker(parts);
        _picker?.Follow(view.Item, view.ConfirmLoaded, view.Scroll);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        FitViewport();
        MouseFilter = MouseFilterEnum.Stop;
        _eventView = new BlackFlowSpireEventView(this, index => _choices[index].Action(), () => Resolve(BlackFlowSpireSacrificeAction.Leave));
        if (BlackFlowSpireTeam.IsOnline && BlackFlowSpireTeam.CanOperate) AddChild(new BlackFlowSpireTeamMapStatus(state) { ZIndex = 1 });
        BlackFlowSpireLocalization.RefreshWithLocale(this, Refresh);
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || !GodotObject.IsInstanceValid(screen) || !screen.IsOpen
            || !BlackFlowSpireSacrificeRules.IsPending(map, progress))
        {
            BlackFlowSpireSacrificeUi.Dismiss(screen);
            return;
        }
        FitViewport();
        _eventView.SetBusy(Busy || _picker != null);
        _picker?.SetBusy(Busy);
        if (!Busy && _shown != progress.Sacrifice)
        {
            ClosePicker();
            _eventView.ResetSelection();
            Refresh();
            _focused = false;
        }
        if (!_focused && DefaultFocusedControl is Button { Disabled: false } button)
        {
            _focused = true;
            button.GrabFocus();
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_picker == null || Busy || !inputEvent.IsActionPressed("ui_cancel")) return;
        if (!_picker.CancelJudge()) ClosePicker();
        GetViewport().SetInputAsHandled();
    }

    private void FitViewport()
    {
        var viewport = GetViewportRect();
        var inverse = (GetParent() as Control)?.GetGlobalTransformWithCanvas().AffineInverse() ?? Transform2D.Identity;
        Position = inverse * viewport.Position;
        Size = inverse * viewport.End - Position;
    }

    private void Refresh()
    {
        var current = progress.Sacrifice;
        _shown = current;
        _choices.Clear();
        var description = Scene switch
        {
            BlackFlowSpireSacrificeScene.FirstResult => current!.PartRoute ? "story.first_part" : "story.first_relic",
            BlackFlowSpireSacrificeScene.SecondResult => current!.PartRoute ? "story.second_part" : "story.second_relic",
            BlackFlowSpireSacrificeScene.Continue => "story.continue",
            BlackFlowSpireSacrificeScene.Declined => "story.declined",
            BlackFlowSpireSacrificeScene.Stopped => "story.stopped",
            BlackFlowSpireSacrificeScene.Finished => "story.finished",
            BlackFlowSpireSacrificeScene.Civilization => "story.civilization",
            BlackFlowSpireSacrificeScene.CivilizationResult => "story.civilization_result",
            _ => "story.intro"
        };
        _eventView.SetStory(BlackFlowSpireSacrificePicker.Asset("pic_rogue_6_24"), Text("title"), Text(description));
        if (BlackFlowSpireSacrificeRules.IsEnding(Scene))
        {
            _eventView.ShowResult(Text("leave.title"));
            return;
        }

        if (current?.Reward is { } reward)
        {
            var civilization = Scene == BlackFlowSpireSacrificeScene.Civilization;
            var key = civilization ? "claim_civilization" : current.PartRoute ? "claim_part" : "claim_relic";
            var item = RewardPresentation(reward);
            _choices.Add((new(Text(key + ".title"), Text(key + ".description", ("Item", item.Name)), item.Icon),
                () => Resolve(BlackFlowSpireSacrificeAction.Claim)));
        }
        else
        {
            var engine = Engine;
            var first = Scene == BlackFlowSpireSacrificeScene.Intro;
            if (first || !current!.PartRoute) AddExchangeOption(engine, parts: false, first);
            if (first || current!.PartRoute) AddExchangeOption(engine, parts: true, first);
            if (map.ActIndex >= 2 && current is { PartRoute: true } && !engine.Collectibles.Has(BlackFlowSpireSacrificeRules.CivilizationCollectible))
                AddOption("civilization", "unknown", () => Resolve(BlackFlowSpireSacrificeAction.RestoreCivilization),
                    BlackFlowSpireSacrificeRules.CanRestoreCivilization(map, current, engine));
            AddOption(first ? "leave" : "stop", "leave", () => Resolve(BlackFlowSpireSacrificeAction.Decline));
        }
        _eventView.SetOptions(_choices.Select(choice => choice.Option).ToArray());
        _picker?.Refresh();
    }

    private void AddExchangeOption(BlackFlowSpireEngineInventory engine, bool parts, bool first)
    {
        var options = BlackFlowSpireSacrificeRules.Offers(map, progress.Sacrifice, engine, parts);
        AddOption(first ? parts ? "offer_part" : "offer_relic" : "offer_again",
            parts ? "sacrifice_scrap" : "sacrifice", () => OpenPicker(parts), options.Count > 0);
    }

    private void AddOption(string key, string icon, Action action, bool enabled = true) =>
        _choices.Add((new(Text(key + ".title"), Text(key + ".description"),
            icon.StartsWith("sacrifice", StringComparison.Ordinal) ? BlackFlowSpireSacrificePicker.Asset(icon)
                : BlackFlowSpireEventView.LoadTexture(icon), enabled), action));

    private void OpenPicker(bool parts)
    {
        if (Busy || _picker != null) return;
        var offers = BlackFlowSpireSacrificeRules.Offers(map, progress.Sacrifice, Engine, parts);
        if (offers.Count == 0) return;
        _eventView.Visible = false;
        _picker = new(this, offers, parts, ClosePicker, argument => Submit(() => BlackFlowSpireRuntime.ExchangeSacrificeItem(argument)));
        _focused = false;
        ActiveScreenContext.Instance.Update();
    }

    private void ClosePicker()
    {
        if (_picker == null) return;
        _picker.Close();
        _picker = null;
        _eventView.Visible = true;
        _focused = false;
        ActiveScreenContext.Instance.Update();
    }

    private void Resolve(BlackFlowSpireSacrificeAction action) => Submit(() => BlackFlowSpireRuntime.ResolveSacrifice(action));

    private void Submit(Func<Task> submit)
    {
        if (Busy || !BlackFlowSpireTeam.CanTravel(state)) return;
        _submitting = true;
        _eventView.SetBusy(true);
        _picker?.SetBusy(true);
        Callable.From(() => { _ = TaskHelper.RunSafely(SubmitAsync(submit)); }).CallDeferred();
    }

    private async Task SubmitAsync(Func<Task> submit)
    {
        try { await submit(); }
        finally { _submitting = false; }
    }

    private static (string Name, Texture2D Icon) RewardPresentation(BlackFlowSpireSacrificeReward reward)
    {
        if (reward.Natural is { } natural) return (BlackFlowSpirePartsUi.NaturalTitle(natural), BlackFlowSpireSacrificePicker.PartArtwork(BlackFlowSpireShopParts.IconId(natural)));
        if (reward.Processed is { } processed) return (BlackFlowSpirePartsUi.Title(processed), BlackFlowSpireSacrificePicker.PartArtwork(BlackFlowSpireShopParts.IconId(processed)));
        if (reward.Concept is { } concept) return (BlackFlowSpirePartsUi.ConceptTitle(concept), BlackFlowSpireSacrificePicker.PartArtwork(BlackFlowSpireShopParts.IconId(concept)));
        var collectible = BlackFlowSpireCollectibleCatalog.Find(reward.CollectibleId!)!;
        return (collectible.Title, ResourceLoader.Load<Texture2D>(collectible.IconPath));
    }

    internal static string Text(string key, params (string Name, object Value)[] arguments) =>
        BlackFlowSpireText.Get("UI.SACRIFICE." + key, arguments);
}
