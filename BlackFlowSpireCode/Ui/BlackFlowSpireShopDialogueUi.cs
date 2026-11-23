using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Shop;
using Godot;
using MegaCrit.Sts2.Core.Entities.Merchant;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpireShopController
{
    private readonly BlackFlowSpireMerchantDialogue _dialogue = new();
    private ulong _dialogueProtectedUntil;
    private object? _dialogueSubject;

    private void Speak(BlackFlowSpireMerchantDialogueGroup group, object? subject = null)
    {
        if (_disposed) return;
        var now = Time.GetTicksMsec();
        // Hover and focus can both fire for one tile. Browsing never interrupts
        // a purchase/sale response or rapidly replaces a line as the mouse moves.
        if (subject != null && (_busy || ReferenceEquals(subject, _dialogueSubject) || now < _dialogueProtectedUntil)) return;
        _dialogueSubject = subject;
        _dialogueProtectedUntil = now + (subject == null ? 2400UL : 1800UL);
        _dialogueKey = _dialogue.Next(group, _stock.IsSecret);
        RefreshDialogue();
    }

    private void RefreshDialogue()
    {
        _view.GetNode<Label>("Canvas/Dialogue/Text").Text = BlackFlowSpireText.Get(_dialogueKey);
        _view.Call("layout_dialogue");
    }

    private void BrowseGoods(GoodsBinding goods)
    {
        var group = goods.Entry switch
        {
            BlackFlowSpireShopPartEntry => BlackFlowSpireMerchantDialogueGroup.Scrap,
            BlackFlowSpireShopCollectibleEntry => BlackFlowSpireMerchantDialogueGroup.Relic,
            BlackFlowSpireShopOfferEntry { Kind: BlackFlowSpireShopOfferKind.PotionPack } => BlackFlowSpireMerchantDialogueGroup.ActiveTool,
            // Card packs and services are not recruitment tickets.
            _ => BlackFlowSpireMerchantDialogueGroup.Browse
        };
        Speak(group, goods);
    }

    private void BrowseSalePart(Button button, BlackFlowSpireShopSaleItem part)
    {
        if (_busy || _disposed) return;
        Speak(BlackFlowSpireMerchantDialogueGroup.SaleSelection, button);
        ShowSaleTip(button, part);
    }
}
