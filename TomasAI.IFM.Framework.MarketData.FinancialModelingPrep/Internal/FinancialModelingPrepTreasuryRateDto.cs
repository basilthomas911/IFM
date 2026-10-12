using System.Text.Json;
using System.Text.Json.Serialization;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "System.Text.Json instantiates this provider response DTO.")]
internal sealed class FinancialModelingPrepTreasuryRateDto
{
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("month1")]
    public decimal? Month1 { get; init; }

    [JsonPropertyName("month2")]
    public decimal? Month2 { get; init; }

    [JsonPropertyName("month3")]
    public decimal? Month3 { get; init; }

    [JsonPropertyName("month6")]
    public decimal? Month6 { get; init; }

    [JsonPropertyName("year1")]
    public decimal? Year1 { get; init; }

    [JsonPropertyName("year2")]
    public decimal? Year2 { get; init; }

    [JsonPropertyName("year3")]
    public decimal? Year3 { get; init; }

    [JsonPropertyName("year5")]
    public decimal? Year5 { get; init; }

    [JsonPropertyName("year7")]
    public decimal? Year7 { get; init; }

    [JsonPropertyName("year10")]
    public decimal? Year10 { get; init; }

    [JsonPropertyName("year20")]
    public decimal? Year20 { get; init; }

    [JsonPropertyName("year30")]
    public decimal? Year30 { get; init; }
}
