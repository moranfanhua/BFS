using System.Text.Json.Serialization;

namespace BlackFlowSpire.Map.Stages;

[JsonConverter(typeof(JsonStringEnumConverter<ForestBattleVariant>))]
public enum ForestBattleVariant { Normal, Emergency, Boss }
