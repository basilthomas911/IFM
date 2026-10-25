using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record CapacityExecutionAcceptance
{
    [Key(0)] public Guid ExecutionId { get; init; }
    [Key(1)] public long ExecutionRevision { get; init; }
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int FundId { get; init; }
    [Key(4)] public int OrderId { get; init; }
    [Key(5)] public Guid ReservationId { get; init; }
    [Key(6)] public string SizedOrderHash { get; init; } = string.Empty;
    [Key(7)] public string RequirementsHash { get; init; } = string.Empty;
    [Key(8)] public string Environment { get; init; } = string.Empty;
    [Key(9)] public DateTime ValidUntilUtc { get; init; }
    [Key(10)] public string ExecutionOrderHash { get; init; } = "";
}
