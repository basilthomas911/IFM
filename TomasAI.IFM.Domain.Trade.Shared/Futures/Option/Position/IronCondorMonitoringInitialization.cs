using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

/// <summary>Captures opening execution prices, accepted capital and ledger cash for one monitoring initialization.</summary>
[MessagePackObject]
public sealed record IronCondorMonitoringInitialization
{
    [Key(0)] public EstablishedTradeDefinition IronCondorTrade { get; init; } = new();
    [Key(1)] public decimal FundAvailableCash { get; init; }
    [Key(2)] public long FundFinancialRevision { get; init; }
    [Key(3)] public DateTime FundCashAsOfUtc { get; init; }
    [Key(4)] public decimal RequiredCapital { get; init; }
    [Key(5)] public int TradeOrderRevision { get; init; }
    [Key(6)] public DateOnly ValueDate { get; init; }
    [Key(7)] public DateTime InitializedAtUtc { get; init; }
}
