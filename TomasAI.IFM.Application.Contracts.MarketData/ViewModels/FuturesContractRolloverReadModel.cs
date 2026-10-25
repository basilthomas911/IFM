using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Identifies the contract currently assigned to one futures symbol and the date
/// on which that assignment must next be reconsidered.
/// </summary>
[MessagePackObject]
public sealed record FuturesContractRolloverReadModel
{
    [Key(0)] public required string Symbol { get; init; }
    [Key(1)] public string? ContractId { get; init; }
    [Key(2)] public DateOnly? NextRolloverDate { get; init; }
    [Key(3)] public DateTime? UpdatedOn { get; init; }
    [Key(4)] public string? UpdatedBy { get; init; }
    [Key(5)] public required DateTime CreatedOn { get; init; }
    [Key(6)] public required string CreatedBy { get; init; }
}
