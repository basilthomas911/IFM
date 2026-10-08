using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Position;

/// <summary>Seals the explicit ended value date; retries must validate committed position state.</summary>
[MessagePackObject]
public sealed record EndOfDayFuturesPositionCommand : FuturesPositionCommand, ICommandRetryIdentity
{
    public const string Verb = "EndOfDayFuturesPosition";
    /// <summary>Gets the UTC boundary of the ended session.</summary>
    [Key(4)] public DateTime EffectiveAtUtc { get; init; }
    /// <summary>Gets the explicit ended exchange value date.</summary>
    [Key(5)] public DateOnly ValueDate { get; init; }

    /// <summary>Retains the complete business identity so a retry cannot change the position, date or close boundary.</summary>
    ICommand ICommandRetryIdentity.ForRetryIdentity() => this;
}
