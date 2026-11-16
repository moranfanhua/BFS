using System.Text.Json;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Serialization;

public static class BlackFlowSpireMapJson
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // A developer/save snapshot includes actual content. The presentation API controls player-facing information.
    public static string Serialize(BlackFlowSpireMap map)
    {
        BlackFlowSpireMapValidator.Validate(map);
        return JsonSerializer.Serialize(map, JsonOptions);
    }

    public static BlackFlowSpireMap Deserialize(string json)
    {
        var map = JsonSerializer.Deserialize<BlackFlowSpireMap>(json, JsonOptions)
            ?? throw new InvalidDataException("Empty forest map snapshot.");
        BlackFlowSpireMapValidator.Validate(map);
        return map;
    }
}
