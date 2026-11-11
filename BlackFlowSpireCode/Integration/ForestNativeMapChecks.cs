using System.Text.Json;
using BlackFlowSpire.Map.Models;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace BlackFlowSpire.Integration;

public static class ForestNativeMapChecks
{
    // Run inside the game, where the game DLL's Sentry/Godot module initializer is available.
    public static void VerifyRoundTrip(ForestMap snapshot)
    {
        var native = new ForestActMap(snapshot);
        var savedJson = JsonSerializer.Serialize(SerializableActMap.FromActMap(native));
        var restored = new SavedActMap(JsonSerializer.Deserialize<SerializableActMap>(savedJson)
            ?? throw new InvalidDataException("Native map snapshot is empty."));
        if (restored.GetAllMapPoints().Count() != snapshot.Nodes.Count - 2)
            throw new InvalidDataException("Native map grid lost or duplicated nodes.");
        foreach (var node in snapshot.Nodes)
        {
            var point = restored.GetPoint(new MapCoord(node.X, node.Y))
                ?? throw new InvalidDataException("Native map round-trip lost a node.");
            var actualIds = point.Children.Select(c => c.coord.row * snapshot.Options.Width + c.coord.col).Order();
            if (!node.Neighbors.Order().SequenceEqual(actualIds))
                throw new InvalidDataException("Native map round-trip changed an edge.");
            foreach (var child in point.Children)
                if (!ReferenceEquals(child, restored.GetPoint(child.coord)))
                    throw new InvalidDataException("Native map edge references an orphaned point.");
        }
    }
}
