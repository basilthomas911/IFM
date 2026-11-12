using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

/// <summary>An order and one of its execution attempts, or an order with no execution yet.</summary>
[MessagePackObject]
public sealed record FundOrderFillsRow(
    [property: Key(0)] int OrderId,
    [property: Key(1)] int? TradeId,
    [property: Key(2)] Guid ExecutionAttemptId,
    [property: Key(3)] string Status,
    [property: Key(4)] IReadOnlyList<ExecutionFillEvidence> Fills,
    [property: Key(5)] DateTime? OrderDateTime = null,
    [property: Key(6)] int OrderQuantity = 0,
    [property: Key(7)] int FilledQuantity = 0,
    [property: Key(8)] DateTime? LastFillDateTime = null,
    [property: Key(9)] string? RejectionDetail = null,
    [property: Key(10)] IReadOnlyList<TradeOrderComponentDefinition>? Components = null)
{
    public const int PageSize = 8;
}
