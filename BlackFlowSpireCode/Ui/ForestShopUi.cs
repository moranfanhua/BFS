using System.Reflection;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Shop;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal static class ForestShopUi
{
    private const string ControllerName = "BlackFlowSpireShop";
    public static ForestShopController? Find(NMerchantRoom room) => room.GetNodeOrNull<ForestShopController>(ControllerName);

    public static void Attach(NMerchantRoom room)
    {
        ForestNaturalService.TraderRefreshHandler ??= RefreshFromNaturalSale;
        if (Find(room) is { } existing)
        {
            existing.Show();
            return;
        }
        // Native merchants build every player's inventory in slot order. Do
        // the same for extra relics because their rolls also affect the shared bag.
        if (ForestTeam.IsOnline)
            foreach (var inventory in room.Room.Inventories) ForestShopInventory.Prepare(inventory);
        // Preserve the initialized native relic slots and purchase effects. The
        // themed inventory replaces card/potion stock with its own offers below.
        foreach (var child in room.GetChildren().OfType<CanvasItem>()) child.Hide();
        room.AddChild(new ForestShopController(room) { Name = ControllerName });
        ActiveScreenContext.Instance.Update();
    }

    private static Task<bool> RefreshFromNaturalSale(RunState state) =>
        ReferenceEquals(ForestRuntime.State, state) && NMerchantRoom.Instance is { } room && Find(room) is { } shop
            ? shop.RefreshFromNaturalSale() : Task.FromResult(false);
}

internal partial class ForestShopController(NMerchantRoom room) : Control
{
    private static readonly MethodInfo SelectNativeSlot = AccessTools.Method(typeof(NMerchantSlot), "OnSelected");
    private readonly List<GoodsBinding> _goods = [];
    private Control _view = null!;
    private MerchantInventory _inventory = null!;
    private ForestShopInventory _stock = null!;
    private Button _leave = null!;
    private Button _refresh = null!;
    private bool _busy;
    private bool _confirmLeave;
    private bool _disposed;
    private bool _sharedStockDirty;
    private Texture2D? _removalArtwork;
    private string _dialogueKey = "UI.SHOP.DIALOGUE.BUY_SELECT.NONE.00";

    public Control DefaultFocus => _selling ? _saleGoods.FirstOrDefault().Button ?? _leave
        : _stock.IsSecret ? _view.GetNode<Button>("Canvas/GoodsViewport/Padding/Grid/Garden")
        : _goods.FirstOrDefault(goods => !goods.Button.Disabled)?.Button ?? _leave;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _inventory = room.Room.GetLocalInventory();
        _stock = ForestShopInventory.Prepare(_inventory);
        _view = ResourceLoader.Load<PackedScene>(_stock.IsSecret
            ? "res://BlackFlowSpire/Scenes/Shop/ForestSecretShopScreen.tscn"
            : "res://BlackFlowSpire/Scenes/Shop/ForestShopScreen.tscn").Instantiate<Control>();
        AddChild(_view);
        _leave = _view.GetNode<Button>("Canvas/Leave");
        _leave.Pressed += RequestLeave;
        _refresh = _view.GetNode<Button>("Canvas/Refresh");
        if (ForestTeam.IsGuest)
        {
            _refresh.TooltipText = ForestText.Get("UI.TEAM.shop");
            _view.GetNode<Button>("Canvas/BuySell/SellButton").TooltipText = _refresh.TooltipText;
        }
        _refresh.Pressed += () => TaskHelper.RunSafely(RefreshGoods(true));
        _view.GetNode<Button>("Canvas/BuySell/SellButton").Pressed += () => SetSelling(true);
        _view.GetNode<Button>("Canvas/BuySell/BuyButton").Pressed += () => SetSelling(false);
        _view.GetNode<Button>("Canvas/BatchSell").Pressed += OpenBatchSale;

        if (_stock.IsSecret)
            _view.GetNode<Button>("Canvas/GoodsViewport/Padding/Grid/Garden").Pressed += OpenGarden;
        BindGoods();
        _inventory.Player.GoldChanged += Refresh;
        ForestLocalization.RefreshWithLocale(this, Refresh);
        Speak(ForestMerchantDialogueGroup.Browse);
        Refresh();
        if (_stock.IsSecret && _inventory.Player.RunState is RunState state && ForestSecretShop.GetStock(state).PendingHarvest.Count > 0)
            Callable.From(() => OpenGarden()).CallDeferred();
    }

    private void BindGoods()
    {
        ClearSaleGoods();
        ClearGoods();
        var grid = _view.GetNode<GridContainer>("Canvas/GoodsViewport/Padding/Grid");
        var tileScene = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Shop/ForestShopGoodsTile.tscn");
        var slots = room.Inventory.GetAllSlots().ToArray();
        _removalArtwork = slots.FirstOrDefault(slot => slot.Entry is MerchantCardRemovalEntry)
            ?.GetNodeOrNull<Sprite2D>("%Visual")?.Texture;
        var nativeRelics = slots.Where(slot => slot.Entry is MerchantRelicEntry).ToDictionary(slot => slot.Entry);
        IEnumerable<MerchantEntry> entries = _stock.IsSecret ? _stock.SecretParts
            : _stock.Offers.Cast<MerchantEntry>().Concat(_inventory.RelicEntries);
        if (_stock.Part != null) entries = entries.Prepend(_stock.Part);
        foreach (var entry in entries)
        {
            nativeRelics.TryGetValue(entry, out var slot);
            var button = tileScene.Instantiate<Button>();
            grid.AddChild(button);
            var goods = new GoodsBinding(entry, slot, button);
            _goods.Add(goods);
            button.Pressed += () => TaskHelper.RunSafely(Buy(goods));
            button.MouseEntered += () => ShowTip(goods);
            button.MouseExited += () => NHoverTipSet.Remove(button);
            button.FocusEntered += () => ShowTip(goods);
            button.FocusExited += () => NHoverTipSet.Remove(button);
            button.GuiInput += input =>
            {
                if (!_busy && goods.Entry.IsStocked && input is InputEventMouseButton
                    { Pressed: true, ButtonIndex: MouseButton.Right })
                {
                    NHoverTipSet.Remove(button);
                    if (entry is MerchantRelicEntry { Model: { } relic })
                        NGame.Instance?.GetInspectRelicScreen().Open(
                            _inventory.RelicEntries.Select(item => item.Model).OfType<RelicModel>().ToArray(), relic);
                    button.AcceptEvent();
                }
            };
            entry.EntryUpdated += Refresh;
            entry.PurchaseCompleted += OnPurchaseCompleted;
            entry.PurchaseFailed += OnPurchaseFailed;
        }
    }

    private void Refresh()
    {
        if (_disposed || !IsInsideTree()) return;
        if (_sharedStockDirty && !_busy)
        {
            _sharedStockDirty = false;
            _stock.ReadSharedStock();
            BindCurrentGoods();
        }
        ForestLocalization.BindStaticText(_view);
        RefreshDialogue();
        _view.GetNode<Control>("Canvas/Leave/Image").Visible = !_confirmLeave;
        _view.GetNode<Control>("Canvas/Leave/Confirm").Visible = _confirmLeave;
        _leave.Disabled = _busy;
        _refresh.Disabled = _busy || ForestTeam.IsGuest || !_stock.CanRefresh || _inventory.Player.Gold < _stock.RefreshGold;
        _refresh.GetNode<Label>("Price/Content/Cost").Text = ForestText.Get("UI.COST.amount", ("Amount", _stock.RefreshGold));
        _refresh.Modulate = _refresh.Disabled ? new Color(0.65f, 0.65f, 0.65f) : Colors.White;
        RefreshSellingControls();
        if (_stock.IsSecret && _inventory.Player.RunState is RunState state)
        {
            var garden = _view.GetNode<Button>("Canvas/GoodsViewport/Padding/Grid/Garden");
            garden.Disabled = _busy || ForestTeam.IsGuest;
            garden.GetNode<Label>("Count").Text = ForestSecretShop.SeedCount(state).ToString();
            garden.Visible = !_selling;
        }
        foreach (var goods in _goods)
        {
            var entry = goods.Entry;
            var sale = false;
            switch (entry)
            {
                case ForestShopPartEntry part:
                    goods.Title = part.Title;
                    goods.Artwork = ResourceLoader.Load<Texture2D>(part.ArtworkPath);
                    break;
                case ForestShopOfferEntry offer:
                    goods.Title = offer.Title;
                    goods.Artwork = offer.Kind switch
                    {
                        ForestShopOfferKind.RemoveCard => _removalArtwork,
                        ForestShopOfferKind.UpgradeCard => ModelDb.Relic<Whetstone>().Icon,
                        ForestShopOfferKind.PotionPack => ModelDb.Relic<PotionBelt>().Icon,
                        _ => ResourceLoader.Load<Texture2D>(ImageHelper.GetImagePath("ui/reward_screen/reward_icon_card.png"))
                    };
                    break;
                case MerchantRelicEntry relic:
                    if (relic.Model is { } relicModel)
                    {
                        goods.Title = relicModel.Title.GetFormattedText();
                        goods.Artwork = relicModel.Icon;
                    }
                    break;
            }
            goods.Button.Disabled = _busy || !entry.IsStocked || (ForestTeam.IsGuest && entry is not MerchantRelicEntry);
            goods.Button.GetNode<TextureRect>("Artwork").Texture = goods.Artwork;
            goods.Button.Call("bind_item", goods.Title, entry.IsStocked ? entry.Cost : 0, entry.IsStocked,
                entry.EnoughGold, sale);
            if (entry is ForestShopPartEntry partEntry)
                goods.Button.Call("bind_part", partEntry.Part.Processed.HasValue ? "processed" : partEntry.Part.Natural.HasValue ? "natural" : "concept",
                    partEntry.Part.Processed is { } processed ? Map.Parts.ForestProcessedParts.Get(processed).Uses : 0,
                    partEntry.Part.Processed is { } maximum ? Map.Parts.ForestProcessedParts.Get(maximum).Uses : 0, false);
        }
        UpdateNavigation();
    }

    internal void RefreshSharedStock()
    {
        if (!IsNodeReady() || _disposed) return;
        _sharedStockDirty = true;
        Refresh();
    }

    internal async Task<bool> RefreshGoods(bool chargeGold)
    {
        if (_busy || _disposed || ForestTeam.IsGuest || !_stock.CanRefresh) return false;
        var price = chargeGold ? _stock.RefreshGold : 0;
        if (_inventory.Player.Gold < price) return false;
        _busy = true;
        _confirmLeave = false;
        foreach (var goods in _goods) NHoverTipSet.Remove(goods.Button);
        Refresh();
        try
        {
            if (price > 0)
            {
                await PlayerCmd.LoseGold(price, _inventory.Player, GoldLossType.Spent);
                RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(price);
            }
            _stock.RefreshStock(chargeGold);
            BindCurrentGoods();
            if (chargeGold) Speak(ForestMerchantDialogueGroup.Browse);
            await (_stock.Part ?? _stock.SecretParts[0]).Save();
            return true;
        }
        catch (Exception error)
        {
            _dialogueKey = "UI.SHOP.dialogue.failed";
            Entry.Logger.Error($"[Forest] Shop refresh failed: {error}");
            return false;
        }
        finally
        {
            _busy = false;
            if (!_disposed && GodotObject.IsInstanceValid(this))
            {
                Refresh();
                ActiveScreenContext.Instance.Update();
            }
        }
    }

    private async Task Buy(GoodsBinding goods)
    {
        if (_busy || !goods.Entry.IsStocked || (ForestTeam.IsGuest && goods.Entry is not MerchantRelicEntry)) return;
        _busy = true;
        _confirmLeave = false;
        foreach (var item in _goods) NHoverTipSet.Remove(item.Button);
        Refresh();
        try
        {
            if (ForestTeam.IsHost && goods.Entry is ForestShopOfferEntry sharedOffer && _inventory.Player.RunState is RunState state)
            {
                if (await ForestTeam.BuyOffer(state, sharedOffer.Kind)) OnPurchaseCompleted(PurchaseStatus.Success, sharedOffer);
                else if (_inventory.Player.Gold < sharedOffer.Cost) OnPurchaseFailed(PurchaseStatus.FailureGold);
                RefreshSharedStock();
                return;
            }
            // Native purchase VFX originate at the displayed tile, including restocked items.
            if (goods.NativeSlot is { } nativeSlot)
            {
                nativeSlot.GlobalPosition = goods.Button.GetGlobalRect().GetCenter();
                AccessTools.Method(nativeSlot.GetType(), "UpdateVisual").Invoke(nativeSlot, null);
                await (Task)SelectNativeSlot.Invoke(nativeSlot, null)!;
            }
            else
            {
                var relic = (goods.Entry as MerchantRelicEntry)?.Model;
                var purchased = await goods.Entry.OnTryPurchaseWrapper(_inventory);
                if (purchased && relic != null)
                    NRun.Instance?.GlobalUi.RelicInventory.AnimateRelic(relic, goods.Button.GetGlobalRect().GetCenter());
                if (purchased && goods.Entry is ForestShopPartEntry part) await part.Save();
                if (purchased && goods.Entry is ForestShopOfferEntry offer) await offer.Save();
            }
        }
        catch (Exception error)
        {
            _dialogueKey = "UI.SHOP.dialogue.failed";
            Entry.Logger.Error($"[Forest] Shop purchase UI failed: {error}");
        }
        finally
        {
            _busy = false;
            if (!_disposed && GodotObject.IsInstanceValid(this))
            {
                Refresh();
                ActiveScreenContext.Instance.Update();
            }
        }
    }

    private void OnPurchaseCompleted(PurchaseStatus status, MerchantEntry entry)
    {
        Speak(ForestMerchantDialogueGroup.Purchase);
        Refresh();
    }

    private void OnPurchaseFailed(PurchaseStatus status)
    {
        _dialogueKey = status == PurchaseStatus.FailureGold ? "UI.SHOP.dialogue.no_gold" : "UI.SHOP.dialogue.failed";
        Refresh();
    }

    private void ShowTip(GoodsBinding goods)
    {
        if (_busy || !goods.Entry.IsStocked) return;
        BrowseGoods(goods);
        // Mouse entry and keyboard focus may both fire for the same button.
        NHoverTipSet.Remove(goods.Button);
        NHoverTipSet? tip = goods.Entry switch
        {
            ForestShopPartEntry part => NHoverTipSet.CreateAndShow(goods.Button,
                new HoverTip(ForestLocalization.CreateLocString(part.NameKey), part.Description)),
            ForestShopOfferEntry offer => NHoverTipSet.CreateAndShow(goods.Button,
                new HoverTip(ForestLocalization.CreateLocString(offer.TextKey + ".title"),
                    ForestLocalization.CreateLocString(offer.TextKey + ".description"))),
            MerchantRelicEntry { Model: { } relic } => NHoverTipSet.CreateAndShow(goods.Button, relic.HoverTips),
            _ => null
        };
        tip?.SetAlignment(goods.Button, HoverTip.GetHoverTipAlignment(goods.Button));
    }

    private void UpdateNavigation()
    {
        var available = _goods.Where(goods => !goods.Button.Disabled).ToArray();
        foreach (var goods in available)
        {
            var index = _goods.IndexOf(goods) + 1; // Investment occupies the first grid cell.
            foreach (var (dx, dy, side) in new[] { (-1, 0, Side.Left), (1, 0, Side.Right), (0, -1, Side.Top), (0, 1, Side.Bottom) })
            {
                var candidate = available.Where(other => other != goods).Select(other =>
                    (Other: other, Index: _goods.IndexOf(other) + 1))
                    .Where(other => dx != 0 ? other.Index / 4 == index / 4 && Math.Sign(other.Index % 4 - index % 4) == dx
                        : other.Index % 4 == index % 4 && Math.Sign(other.Index / 4 - index / 4) == dy)
                    .OrderBy(other => Math.Abs(other.Index - index)).Select(other => other.Other).FirstOrDefault();
                var fallback = side == Side.Bottom ? _leave : side == Side.Left && !_refresh.Disabled ? _refresh : goods.Button;
                var path = (candidate?.Button ?? fallback).GetPath();
                switch (side)
                {
                    case Side.Left: goods.Button.FocusNeighborLeft = path; break;
                    case Side.Right: goods.Button.FocusNeighborRight = path; break;
                    case Side.Top: goods.Button.FocusNeighborTop = path; break;
                    case Side.Bottom: goods.Button.FocusNeighborBottom = path; break;
                }
            }
        }
        if (available.Length > 0)
        {
            _refresh.FocusNeighborRight = available[0].Button.GetPath();
            _refresh.FocusNeighborBottom = available[0].Button.GetPath();
            _leave.FocusNeighborLeft = available[^1].Button.GetPath();
            _leave.FocusNeighborTop = available[^1].Button.GetPath();
        }
        if (_stock.IsSecret)
        {
            var garden = _view.GetNode<Button>("Canvas/GoodsViewport/Padding/Grid/Garden");
            garden.FocusNeighborRight = (_goods.FirstOrDefault()?.Button ?? _leave).GetPath();
            garden.FocusNeighborBottom = (_goods.ElementAtOrDefault(3)?.Button ?? _leave).GetPath();
            garden.FocusNeighborLeft = _refresh.GetPath();
            if (_goods.Count > 0) _goods[0].Button.FocusNeighborLeft = garden.GetPath();
            if (_goods.Count > 3) _goods[3].Button.FocusNeighborTop = garden.GetPath();
        }
    }

    private void RequestLeave()
    {
        if (_busy) return;
        if (_confirmLeave) Leave();
        else
        {
            _confirmLeave = true;
            Refresh();
        }
    }

    private void Leave()
    {
        if (_busy) return;
        foreach (var goods in _goods) NHoverTipSet.Remove(goods.Button);
        NMapScreen.Instance?.Open(false);
    }

    private void OpenGarden()
    {
        if (_busy || _disposed || ForestTeam.IsGuest || !_stock.IsSecret || _inventory.Player.RunState is not RunState state
            || MegaCrit.Sts2.Core.Nodes.Screens.Overlays.NOverlayStack.Instance is not { } overlays) return;
        foreach (var goods in _goods) NHoverTipSet.Remove(goods.Button);
        Speak(ForestMerchantDialogueGroup.Browse);
        _busy = true;
        Refresh();
        SetCultivationVisible(true);
        overlays.Push(new ForestCultivationScreen(state, () =>
        {
            if (_disposed || !GodotObject.IsInstanceValid(this)) return;
            _busy = false;
            SetCultivationVisible(false);
            Refresh();
            ActiveScreenContext.Instance.Update();
        }) { Name = "ForestCultivation" });
    }

    private void SetCultivationVisible(bool cultivating)
    {
        foreach (var path in new[] { "Canvas/GoodsViewport", "Canvas/BuySell", "Canvas/Refresh", "Canvas/Leave" })
            _view.GetNode<Control>(path).Visible = !cultivating;
    }

    public override void _ExitTree()
    {
        _disposed = true;
        _inventory.Player.GoldChanged -= Refresh;
        ClearGoods(false);
        ClearSaleGoods(false);
    }

    private void ClearGoods(bool removeNodes = true)
    {
        foreach (var goods in _goods)
        {
            goods.Entry.EntryUpdated -= Refresh;
            goods.Entry.PurchaseCompleted -= OnPurchaseCompleted;
            goods.Entry.PurchaseFailed -= OnPurchaseFailed;
            if (GodotObject.IsInstanceValid(goods.Button))
            {
                NHoverTipSet.Remove(goods.Button);
                if (removeNodes)
                {
                    goods.Button.GetParent()?.RemoveChild(goods.Button);
                    goods.Button.QueueFree();
                }
            }
        }
        _goods.Clear();
    }

    private sealed class GoodsBinding(MerchantEntry entry, NMerchantSlot? nativeSlot, Button button)
    {
        public NMerchantSlot? NativeSlot { get; } = nativeSlot;
        public MerchantEntry Entry { get; } = entry;
        public Button Button { get; } = button;
        public string Title { get; set; } = "";
        public Texture2D? Artwork { get; set; }
    }
}
