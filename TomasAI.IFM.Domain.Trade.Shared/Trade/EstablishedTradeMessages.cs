using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade;

[MessagePackObject]
[Union(0, typeof(Futures.CreateFuturesTradeCommand))]
[Union(1, typeof(Futures.AmendFuturesTradeEvidenceCommand))]
[Union(2, typeof(Futures.BeginCloseFuturesTradeCommand))]
[Union(3, typeof(Futures.CloseFuturesTradeCommand))]
[Union(4, typeof(Futures.Option.CreateOptionTradeCommand))]
[Union(5, typeof(Futures.Option.AmendOptionTradeEvidenceCommand))]
[Union(6, typeof(Futures.Option.BeginCloseOptionTradeCommand))]
[Union(7, typeof(Futures.Option.CloseOptionTradeCommand))]
public abstract record EstablishedTradeCommand : ICommand<TradeEntityId>
{
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public TradeEntityId EntityId { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public abstract BoundedContextName RouteTo { get; }
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Subject.Name}Actor";
    [IgnoreMember] public int ErrorCode => 25103;
}

[MessagePackObject]
[Union(0, typeof(Futures.CreateFuturesTradeCommand))]
[Union(1, typeof(Futures.Option.CreateOptionTradeCommand))]
public abstract record CreateEstablishedTradeCommand : EstablishedTradeCommand
{
    [Key(4)] public EstablishedTradeDefinition Trade { get; init; } = new();
}

[MessagePackObject]
[Union(0, typeof(Futures.AmendFuturesTradeEvidenceCommand))]
[Union(1, typeof(Futures.Option.AmendOptionTradeEvidenceCommand))]
public abstract record AmendEstablishedTradeEvidenceCommand : EstablishedTradeCommand
{
    [Key(4)] public Guid AmendmentId { get; init; }
    [Key(5)] public decimal CommissionDelta { get; init; }
}
