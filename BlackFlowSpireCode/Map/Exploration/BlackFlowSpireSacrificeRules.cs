using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

internal enum BlackFlowSpireSacrificeKind { Collectible, Natural, Processed, Concept }
internal enum BlackFlowSpireSacrificeAction { Decline, Claim, RestoreCivilization, Leave }
public enum BlackFlowSpireSacrificeScene { Intro, FirstResult, Continue, SecondResult, Declined, Stopped, Finished, Civilization, CivilizationResult }

public sealed record BlackFlowSpireSacrificeProgress(BlackFlowSpireSacrificeScene Scene,
    BlackFlowSpireSacrificeReward? Reward = null, bool PartRoute = false);

public sealed record BlackFlowSpireSacrificeReward(string? CollectibleId = null,
    BlackFlowSpireNaturalPartKind? Natural = null, BlackFlowSpireProcessedPartKind? Processed = null,
    BlackFlowSpireConceptPartKind? Concept = null);

internal sealed record BlackFlowSpireSacrificeOffer(int Argument, BlackFlowSpireSacrificeKind Kind, int SortId,
    string IconId, BlackFlowSpireSacrificeReward Reward, int? Appraisal = null, int? Uses = null,
    bool Loaded = false, BlackFlowSpireCollectible? Collectible = null);

internal static class BlackFlowSpireSacrificeRules
{
    public const int CivilizationCollectible = 238;
    private static readonly BlackFlowSpireNaturalPartKind[] RareNaturals =
    [
        BlackFlowSpireNaturalPartKind.BoardVine, BlackFlowSpireNaturalPartKind.HomesickFruit,
        BlackFlowSpireNaturalPartKind.BrilliantTruffle, BlackFlowSpireNaturalPartKind.CageController,
        BlackFlowSpireNaturalPartKind.TravelFluff
    ];

    public static bool IsChoice(BlackFlowSpireMap map, BlackFlowSpireNode node) =>
        node.Content == BlackFlowSpireNodeContent.Sacrifice && !node.Completed
        && BlackFlowSpireResidentRules.Occupant(map, node.Id) == null;

    public static bool IsPending(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress) =>
        progress.PendingNodeId == map.PositionNodeId && IsChoice(map, map.GetNode(map.PositionNodeId));

    public static bool IsEnding(BlackFlowSpireSacrificeScene scene) =>
        scene is BlackFlowSpireSacrificeScene.Declined or BlackFlowSpireSacrificeScene.Stopped
            or BlackFlowSpireSacrificeScene.Finished or BlackFlowSpireSacrificeScene.CivilizationResult;

    public static bool CanOffer(BlackFlowSpireSacrificeProgress? progress, bool parts) =>
        progress == null || progress.Scene == BlackFlowSpireSacrificeScene.Continue && progress.PartRoute == parts;

    public static bool CanRestoreCivilization(BlackFlowSpireMap map, BlackFlowSpireSacrificeProgress? progress,
        BlackFlowSpireEngineInventory engine) =>
        map.ActIndex >= 2 && progress is { Scene: BlackFlowSpireSacrificeScene.Continue, PartRoute: true }
        && !engine.Collectibles.Has(CivilizationCollectible) && engine.NaturalParts.Count >= 2;

    public static IReadOnlyList<BlackFlowSpireSacrificeOffer> Offers(BlackFlowSpireMap map,
        BlackFlowSpireSacrificeProgress? progress, BlackFlowSpireEngineInventory engine, bool parts)
    {
        if (!CanOffer(progress, parts)) return [];
        var random = RandomFor(map, progress);
        if (!parts)
        {
            var reward = BlackFlowSpireCollectibleRules.Draw(random, engine.Collectibles.OwnedIds, rare: false);
            if (reward == null) return [];
            // Assignment relics and story relics have canSacrifice=false in the source item table.
            return engine.Collectibles.OwnedIds.Select(BlackFlowSpireCollectibleCatalog.Find)
                .OfType<BlackFlowSpireCollectible>()
                .Where(item => item.Kind is "RELIC" or "UPGRADE" && item.Number is not (>= 174 and <= 188 or >= 233 and <= 242))
                .OrderBy(item => item.Number)
                .Select(item => new BlackFlowSpireSacrificeOffer(item.Number, BlackFlowSpireSacrificeKind.Collectible,
                    item.Number, item.IconId, new(CollectibleId: reward.Id), Collectible: item)).ToArray();
        }

        return engine.NaturalParts.Select(part =>
            {
                var pool = (part.Kind == BlackFlowSpireNaturalPartKind.ThinBurialGrass
                    ? RareNaturals.AsEnumerable()
                    : BlackFlowSpireNaturalParts.All.Select(item => item.Kind)).Where(kind => kind != part.Kind).ToArray();
                return new BlackFlowSpireSacrificeOffer(-1 - part.Id, BlackFlowSpireSacrificeKind.Natural, (int)part.Kind,
                    BlackFlowSpireShopParts.IconId(part.Kind), new(Natural: pool[random.Next(pool.Length)]), part.Appraisal);
            })
            .Concat(engine.Parts.Select(part =>
            {
                var pool = BlackFlowSpireProcessedParts.All.Where(item => item.Kind != part.Kind).ToArray();
                return new BlackFlowSpireSacrificeOffer(-1 - part.Id, BlackFlowSpireSacrificeKind.Processed, (int)part.Kind,
                    BlackFlowSpireShopParts.IconId(part.Kind), new(Processed: pool[random.Next(pool.Length)].Kind),
                    Uses: part.RemainingUses, Loaded: engine.LoadedPartId == part.Id);
            }))
            .Concat(engine.ConceptParts.Select(part =>
            {
                var pool = BlackFlowSpireConceptParts.All.Where(item => item.Kind != part.Kind).ToArray();
                return new BlackFlowSpireSacrificeOffer(-1 - part.Id, BlackFlowSpireSacrificeKind.Concept, (int)part.Kind,
                    BlackFlowSpireShopParts.IconId(part.Kind), new(Concept: pool[random.Next(pool.Length)].Kind),
                    Uses: part.RemainingActivations);
            }))
            // The source item sort IDs put natural parts at 100, processed parts at 200 and concepts at 300.
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.SortId).ThenByDescending(item => item.Argument).ToArray();
    }

    public static bool TryExchange(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress,
        BlackFlowSpireEngineInventory engine, int argument)
    {
        if (!IsPending(map, progress) || argument == 0) return false;
        var offer = Offers(map, progress.Sacrifice, engine, parts: argument < 0)
            .FirstOrDefault(item => item.Argument == argument);
        if (offer == null) return false;
        switch (offer.Kind)
        {
            case BlackFlowSpireSacrificeKind.Collectible:
                if (!engine.Collectibles.OwnedIds.Remove(offer.Collectible!.Id)) return false;
                break;
            case BlackFlowSpireSacrificeKind.Natural: engine.DiscardNatural(-1 - argument); break;
            case BlackFlowSpireSacrificeKind.Processed: engine.Discard(-1 - argument); break;
            case BlackFlowSpireSacrificeKind.Concept: engine.DiscardConcept(-1 - argument); break;
            default: return false;
        }
        progress.Sacrifice = new(progress.Sacrifice == null ? BlackFlowSpireSacrificeScene.FirstResult
            : BlackFlowSpireSacrificeScene.SecondResult, offer.Reward, argument < 0);
        return true;
    }

    public static bool TryResolve(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress,
        BlackFlowSpireEngineInventory engine, BlackFlowSpireSacrificeAction action,
        Func<BlackFlowSpireSacrificeReward, bool> claim)
    {
        if (!IsPending(map, progress)) return false;
        var current = progress.Sacrifice ?? new(BlackFlowSpireSacrificeScene.Intro);
        switch (action)
        {
            case BlackFlowSpireSacrificeAction.Decline when current.Scene is BlackFlowSpireSacrificeScene.Intro or BlackFlowSpireSacrificeScene.Continue:
                progress.Sacrifice = current with { Scene = current.Scene == BlackFlowSpireSacrificeScene.Intro
                    ? BlackFlowSpireSacrificeScene.Declined : BlackFlowSpireSacrificeScene.Stopped };
                return true;
            case BlackFlowSpireSacrificeAction.Claim when current.Reward is { } reward:
                if (!claim(reward)) return false;
                progress.Sacrifice = current with
                {
                    Reward = null,
                    Scene = current.Scene switch
                    {
                        BlackFlowSpireSacrificeScene.FirstResult => BlackFlowSpireSacrificeScene.Continue,
                        BlackFlowSpireSacrificeScene.Civilization => BlackFlowSpireSacrificeScene.CivilizationResult,
                        _ => BlackFlowSpireSacrificeScene.Finished
                    }
                };
                return true;
            case BlackFlowSpireSacrificeAction.RestoreCivilization when CanRestoreCivilization(map, current, engine):
                var candidates = engine.NaturalParts.Select(part => part.Id).Order().ToList();
                RandomFor(map, current).Shuffle(candidates);
                engine.DiscardNatural(candidates[0]);
                engine.DiscardNatural(candidates[1]);
                progress.Sacrifice = new(BlackFlowSpireSacrificeScene.Civilization,
                    new(CollectibleId: "rogue_6_relic_final_6"), PartRoute: true);
                return true;
            case BlackFlowSpireSacrificeAction.Leave when IsEnding(current.Scene):
                map.GetNode(map.PositionNodeId).Completed = true;
                progress.PendingNodeId = null;
                progress.Sacrifice = null;
                return true;
            default:
                return false;
        }
    }

    internal static void Populate(BlackFlowSpireMap map)
    {
        var random = new StableRandom(map.Seed, map.ActIndex, "sacrifice/v1");
        if (random.Next(100) >= map.Options.SacrificeChancePercent) return;
        var candidates = map.Nodes.Where(node => node.Content == BlackFlowSpireNodeContent.Event).ToArray();
        if (candidates.Length > 0) candidates[random.Next(candidates.Length)].Content = BlackFlowSpireNodeContent.Sacrifice;
    }

    private static StableRandom RandomFor(BlackFlowSpireMap map, BlackFlowSpireSacrificeProgress? progress) =>
        new(map.Seed, map.ActIndex, $"sacrifice-reward/v1/{map.PositionNodeId}/{progress?.Scene ?? BlackFlowSpireSacrificeScene.Intro}");
}
