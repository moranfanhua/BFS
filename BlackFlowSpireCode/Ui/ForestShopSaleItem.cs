using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal sealed record ForestShopSaleItem(int Id, string Title, string Description, string IconId,
    string Category, int Gold, int Uses = 0, int MaxUses = 0, bool Loaded = false, int? Activations = null)
{
    public int Appraisal => Gold / ForestPartService.GoldPerOriginium;

    public string BatchDescription => MaxUses > 0
        ? ForestText.Get("UI.SHOP.batch_movement_description", ("Description", Description), ("Count", Uses))
        : Activations.HasValue ? ForestText.Get("UI.SHOP.batch_activation_description", ("Description", Description), ("Count", Activations.Value))
        : Description;
    public static IReadOnlyList<ForestShopSaleItem> Get(RunState state)
    {
        var engine = ForestGenerationService.GetEngine(state);
        return engine.Parts.Select(part => new ForestShopSaleItem(part.Id, ForestPartsUi.Title(part.Kind),
                ForestPartsUi.Description(part.Kind), ForestShopParts.IconId(part.Kind), "processed",
                ForestPartService.FixedSaleGold, part.RemainingUses, ForestProcessedParts.Get(part.Kind).Uses, engine.LoadedPartId == part.Id))
            .Concat(engine.NaturalParts.Select(part => new ForestShopSaleItem(part.Id, ForestPartsUi.NaturalTitle(part.Kind),
                ForestPartsUi.NaturalDescription(part.Kind), ForestShopParts.IconId(part.Kind), "natural", ForestNaturalService.SaleGold(part))))
            .Concat(engine.ConceptParts.Select(part => new ForestShopSaleItem(part.Id, ForestPartsUi.ConceptTitle(part.Kind),
                ForestPartsUi.ConceptDescription(part.Kind), ForestShopParts.IconId(part.Kind), "concept", ForestPartService.FixedSaleGold,
                Activations: part.RemainingActivations)))
            .OrderBy(part => part.Id).ToArray();
    }

    public void Bind(Button button)
    {
        ForestLocalization.BindStaticText(button);
        button.GetNode<TextureRect>("Artwork").Texture = ResourceLoader.Load<Texture2D>($"res://BlackFlowSpire/Images/Parts/{IconId}.png");
        button.Call("bind_item", Title, Gold, true, true, false, true);
        button.Call("bind_part", Category, Uses, MaxUses, Loaded);
    }
}
