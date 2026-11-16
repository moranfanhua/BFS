using System.Text.Json.Serialization;

namespace BlackFlowSpire.Map.Stages;

[JsonConverter(typeof(JsonStringEnumConverter<BlackFlowSpireBattleVariant>))]
public enum BlackFlowSpireBattleVariant { Normal, Emergency, Boss }
