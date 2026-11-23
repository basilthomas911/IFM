using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

/// <summary>Seals the explicit ended value date; retries must validate committed position state.</summary>
[MessagePackObject]
public sealed record EndOfDayVerticalSpreadPositionCommand : TimedPositionCommand, IRecoverableCommandAudit, IStoredQuotePositionEndOfDayCommand
{
    public const string Verb = "EndOfDayVerticalSpreadPosition";
    /// <inheritdoc />
    [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesVerticalSpreadTradePositionBoundedContext;
    /// <summary>Gets the explicit ended exchange value date.</summary>
    [Key(5)] public DateOnly ValueDate { get; init; }
    [Key(6)] public StrategyPositionSnapshot? HistoricalSnapshot { get; init; }
    [Key(7)] public long HistoricalSourceEventId { get; init; }
    [Key(8)] public StoredPositionClosingQuote[] HistoricalClosingQuotes { get; init; } = [];
    [Key(9)] public StrategyPositionSnapshot? PreviousEndOfDay { get; init; }

    /// <summary>Retains the complete business identity so a retry cannot change the position, date or close boundary.</summary>
    ICommand ICommandRetryIdentity.ForRetryIdentity() => this;
}
