using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Commands;

[MessagePackObject]
public sealed record InsertFuturesTickTradeDataCommand : ICommand<TickDataEntityId>
{
    public const string Actor = "TickAggregationCommand";
    public const string Verb = "InsertFuturesTickTradeData";
    public const int ErrorId = 5701;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public TickDataEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesTickDataBoundedContext;
    [Key(6)] public ushort SchemaVersion { get; init; } = 1;
    [Key(7)] public TickDataId TickDataId { get; init; }
    [Key(8)] public AssetTypeId AssetTypeId { get; init; }
    [Key(9)] public string Dataset { get; init; } = string.Empty;
    [Key(10)] public DateOnly DefinitionDate { get; init; }
    [Key(11)] public ushort PublisherId { get; init; }
    [Key(12)] public uint InstrumentId { get; init; }
    [Key(13)] public FuturesTickTradeData TradeData { get; init; }
    [IgnoreMember] public string CommandName => nameof(InsertFuturesTickTradeDataCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
}

[MessagePackObject]
public sealed record InsertFuturesTickQuoteDataCommand : ICommand<TickDataEntityId>
{
    public const string Actor = "TickAggregationCommand";
    public const string Verb = "InsertFuturesTickQuoteData";
    public const int ErrorId = 5702;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public TickDataEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesTickDataBoundedContext;
    [Key(6)] public ushort SchemaVersion { get; init; } = 1;
    [Key(7)] public TickDataId TickDataId { get; init; }
    [Key(8)] public AssetTypeId AssetTypeId { get; init; }
    [Key(9)] public string Dataset { get; init; } = string.Empty;
    [Key(10)] public DateOnly DefinitionDate { get; init; }
    [Key(11)] public ushort PublisherId { get; init; }
    [Key(12)] public uint InstrumentId { get; init; }
    [Key(13)] public QuoteEmissionReason EmissionReason { get; init; }
    [Key(14)] public ushort QuoteCount { get; init; }
    [Key(15)] public FuturesTickQuoteDataSegment QuoteData { get; init; }
    [IgnoreMember] public string CommandName => nameof(InsertFuturesTickQuoteDataCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
}
