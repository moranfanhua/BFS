using System.Text.Json;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Serialization;

public static class ForestMapJson
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // A developer/save snapshot includes actual content. The presentation API controls player-facing information.
    public static string Serialize(ForestMap map)
    {
        ForestMapValidator.Validate(map);
        return JsonSerializer.Serialize(map, JsonOptions);
    }

    public static ForestMap Deserialize(string json)
    {
        var map = JsonSerializer.Deserialize<ForestMap>(json, JsonOptions)
            ?? throw new InvalidDataException("Empty forest map snapshot.");
        ForestMapValidator.Validate(map);
        return map;
    }
}
