using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Position;

public static class FuturesPositionActorNames
{
    public const string Command = "FuturesTradePositionCommand";
    public const string Query = "FuturesTradePositionQuery";
}

[MessagePackObject]
[Union(0, typeof(OpenFuturesPositionCommand))]
[Union(1, typeof(UpdateFuturesPositionMarketPriceCommand))]
[Union(2, typeof(EndOfDayFuturesPositionCommand))]
[Union(3, typeof(CloseFuturesPositionCommand))]
[Union(4, typeof(CorrectFuturesPositionBasisCommand))]
[Union(5, typeof(SnapshotFuturesPositionCommand))]
public abstract record FuturesPositionCommand : ICommand<StrategyPositionId>
{
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public StrategyPositionId EntityId { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.FuturesTradePositionBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{FuturesPositionActorNames.Command}Actor";
    [IgnoreMember] public int ErrorCode => 25105;
}

[MessagePackObject]
public sealed record OpenFuturesPositionCommand : FuturesPositionCommand
{
    public const string Verb = "OpenFuturesPosition";
    [Key(4)] public EstablishedTradeDefinition Trade { get; init; } = new();
    [Key(5)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject]
public sealed record UpdateFuturesPositionMarketPriceCommand : FuturesPositionCommand
{
    public const string Verb = "UpdateFuturesPositionMarketPrice";
    [Key(4)] public Guid TradeLegId { get; init; }
    [Key(5)] public decimal Price { get; init; }
    [Key(6)] public long SourceSequence { get; init; }
    [Key(7)] public long RouteGeneration { get; init; }
    [Key(8)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject]
public sealed record EndOfDayFuturesPositionCommand : FuturesPositionCommand
{
    public const string Verb = "EndOfDayFuturesPosition";
    [Key(4)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject]
public sealed record CloseFuturesPositionCommand : FuturesPositionCommand
{
    public const string Verb = "CloseFuturesPosition";
    [Key(4)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject]
public sealed record CorrectFuturesPositionBasisCommand : FuturesPositionCommand
{
    public const string Verb = "CorrectFuturesPositionBasis";
    [Key(4)] public Guid TradeLegId { get; init; }
    [Key(5)] public decimal Price { get; init; }
    [Key(6)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject]
public sealed record SnapshotFuturesPositionCommand : FuturesPositionCommand
{
    public const string Verb = "SnapshotFuturesPosition";
}

[MessagePackObject]
public sealed record FuturesPositionChangedEvent : PositionChangedEvent;
