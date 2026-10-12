using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoBulkWatchdogSnapshot
{
    public required bool Complete { get; init; }
    public required string NativeBackend { get; init; }
    public required int NativeAbiVersion { get; init; }
    public required Guid NativeGeneration { get; init; }
    public required DateTime ObservedOnUtc { get; init; }
    public required IReadOnlyList<DatabentoFeedWatchdogStatus> Feeds { get; init; }
    public string FailureDetail { get; init; } = string.Empty;
}
