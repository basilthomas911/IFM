using System.Text.Json;
using System.Text.Json.Serialization;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "System.Text.Json instantiates this provider response DTO.")]
internal sealed class FinancialModelingPrepEconomicCalendarDto
{
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("event")]
    public string? Event { get; init; }

    [JsonPropertyName("actual")]
    public JsonElement Actual { get; init; }

    [JsonPropertyName("estimate")]
    public JsonElement Estimate { get; init; }

    [JsonPropertyName("previous")]
    public JsonElement Previous { get; init; }

    [JsonPropertyName("impact")]
    public JsonElement Impact { get; init; }

    [JsonPropertyName("unit")]
    public JsonElement Unit { get; init; }

    [JsonPropertyName("change")]
    public JsonElement Change { get; init; }

    [JsonPropertyName("changePercentage")]
    public JsonElement ChangePercentage { get; init; }
}
