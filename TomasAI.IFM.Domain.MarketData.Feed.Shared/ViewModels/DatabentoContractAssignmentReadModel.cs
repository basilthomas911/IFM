using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

[MessagePackObject(false)]
public sealed record DatabentoContractAssignmentReadModel
{
    [Key(0)] public string Role { get; init; } = string.Empty;
    [Key(1)] public string RootSymbol { get; init; } = string.Empty;
    [Key(2)] public string ContractId { get; init; } = string.Empty;
    [Key(3)] public DateOnly LastTradeDate { get; init; }
    [Key(4)] public DateOnly NextRolloverDate { get; init; }
    [Key(5)] public long RowVersion { get; init; }
    [Key(6)] public DateTime UpdatedOnUtc { get; init; }
}
