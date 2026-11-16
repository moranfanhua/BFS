using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal sealed record BlackFlowSpireShopSaleItem(int Id, string Title, string Description, string IconId,
    string Category, int Gold, int Uses = 0, int MaxUses = 0, bool Loaded = false, int? Activations = null)
{
    public int Appraisal => Gold / BlackFlowSpirePartService.GoldPerOriginium;

    public string BatchDescription => MaxUses > 0
        ? BlackFlowSpireText.Get("UI.SHOP.batch_movement_description", ("Description", Description), ("Count", Uses))
        : Activations.HasValue ? BlackFlowSpireText.Get("UI.SHOP.batch_activation_description", ("Description", Description), ("Count", Activations.Value))
        : Description;
    public static IReadOnlyList<BlackFlowSpireShopSaleItem> Get(RunState state)
    {
        var engine = BlackFlowSpireGenerationService.GetEngine(state);
        return engine.Parts.Select(part => new BlackFlowSpireShopSaleItem(part.Id, BlackFlowSpirePartsUi.Title(part.Kind),
                BlackFlowSpirePartsUi.Description(part.Kind), BlackFlowSpireShopParts.IconId(part.Kind), "processed",
                BlackFlowSpirePartService.FixedSaleGold, part.RemainingUses, BlackFlowSpireProcessedParts.Get(part.Kind).Uses, engine.LoadedPartId == part.Id))
            .Concat(engine.NaturalParts.Select(part => new BlackFlowSpireShopSaleItem(part.Id, BlackFlowSpirePartsUi.NaturalTitle(part.Kind),
                BlackFlowSpirePartsUi.NaturalDescription(part.Kind), BlackFlowSpireShopParts.IconId(part.Kind), "natural", BlackFlowSpireNaturalService.SaleGold(part))))
            .Concat(engine.ConceptParts.Select(part => new BlackFlowSpireShopSaleItem(part.Id, BlackFlowSpirePartsUi.ConceptTitle(part.Kind),
                BlackFlowSpirePartsUi.ConceptDescription(part.Kind), BlackFlowSpireShopParts.IconId(part.Kind), "concept", BlackFlowSpirePartService.FixedSaleGold,
                Activations: part.RemainingActivations)))
            .OrderBy(part => part.Id).ToArray();
    }

    public void Bind(Button button)
    {
        BlackFlowSpireLocalization.BindStaticText(button);
        button.GetNode<TextureRect>("Artwork").Texture = ResourceLoader.Load<Texture2D>($"res://BlackFlowSpire/Images/Parts/{IconId}.png");
        button.Call("bind_item", Title, Gold, true, true, false, true);
        button.Call("bind_part", Category, Uses, MaxUses, Loaded);
    }
}
