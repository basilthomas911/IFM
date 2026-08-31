using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

/// <summary>Records one asynchronous analytics component in a Market Outlook aggregate.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ObserveMarketOutlookComponentCommand : ICommand<MarketOutlookEntityId>
{
    /// <summary>Gets the target command actor mailbox name.</summary>
    public const string Actor = "MarketOutlookSnapshotCommand";

    /// <summary>Gets the command verb.</summary>
    public const string Verb = "ObserveComponent";

    /// <summary>Gets the command error identifier.</summary>
    public const int ErrorId = 20101;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.MarketOutlookSnapshotBoundedContext;

    /// <summary>Gets the stable identity of the source realtime or completed event.</summary>
    [Key(6)] public Guid SourceEventId { get; init; }

    /// <summary>Gets the durable source sequence when one is available.</summary>
    [Key(7)] public long SourceEventSequence { get; init; }

    /// <summary>Gets the source event timestamp used for deterministic ordering.</summary>
    [Key(8)] public DateTime SourceEventTimestamp { get; init; }

    /// <summary>Gets the source event contract name.</summary>
    [Key(9)] public string SourceEventName { get; init; } = string.Empty;

    /// <summary>Gets an optional RSI component.</summary>
    [Key(10)] public FuturesRsiSignalReadModel? FuturesRsiSignal { get; init; }

    /// <summary>Gets an optional TDI component.</summary>
    [Key(11)] public FuturesTdiSignalReadModel? FuturesTdiSignal { get; init; }

    /// <summary>Gets an optional ITI component.</summary>
    [Key(12)] public FuturesItiSignalV2ReadModel? FuturesItiSignal { get; init; }

    /// <summary>Gets an optional VX futures price component.</summary>
    [Key(13)] public decimal VixFuturesPrice { get; init; }

    /// <summary>Gets an optional Daily EMA component.</summary>
    [Key(14)] public FuturesEmaSignalReadModel? FuturesEmaSignal { get; init; }

    /// <summary>Gets an optional Daily Bollinger component.</summary>
    [Key(15)] public FuturesBbSignalReadModel? FuturesBbSignal { get; init; }

    /// <summary>Gets the CLR command name.</summary>
    [IgnoreMember] public string CommandName => nameof(ObserveMarketOutlookComponentCommand);

    /// <summary>Gets the event stream identifier derived from the command subject.</summary>
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";

    /// <summary>Gets the source used for committed domain-event metadata.</summary>
    [IgnoreMember] public string EventSource => $"{Actor}Actor";

    /// <summary>Gets the command origination time.</summary>
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;

    /// <summary>Gets the current operating-system user identity.</summary>
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Gets the number of component payloads supplied by this command.</summary>
    [IgnoreMember]
    public int ComponentCount => (FuturesRsiSignal is null ? 0 : 1)
        + (FuturesTdiSignal is null ? 0 : 1)
        + (FuturesItiSignal is null ? 0 : 1)
        + (VixFuturesPrice > 0 ? 1 : 0)
        + (FuturesEmaSignal is null ? 0 : 1)
        + (FuturesBbSignal is null ? 0 : 1);

    /// <summary>Initializes an empty command for MessagePack deserialization.</summary>
    public ObserveMarketOutlookComponentCommand() { }

    /// <summary>Initializes a component observation command.</summary>
    public ObserveMarketOutlookComponentCommand(
        MarketOutlookEntityId entityId,
        Guid sourceEventId,
        long sourceEventSequence,
        DateTime sourceEventTimestamp,
        string sourceEventName,
        FuturesRsiSignalReadModel? futuresRsiSignal = null,
        FuturesTdiSignalReadModel? futuresTdiSignal = null,
        FuturesItiSignalV2ReadModel? futuresItiSignal = null,
        decimal vixFuturesPrice = 0,
        FuturesEmaSignalReadModel? futuresEmaSignal = null,
        FuturesBbSignalReadModel? futuresBbSignal = null)
    {
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        SourceEventId = sourceEventId;
        SourceEventSequence = sourceEventSequence;
        SourceEventTimestamp = sourceEventTimestamp;
        SourceEventName = sourceEventName ?? string.Empty;
        FuturesRsiSignal = futuresRsiSignal;
        FuturesTdiSignal = futuresTdiSignal;
        FuturesItiSignal = futuresItiSignal;
        VixFuturesPrice = vixFuturesPrice;
        FuturesEmaSignal = futuresEmaSignal;
        FuturesBbSignal = futuresBbSignal;
    }
}

/// <summary>Publishes the current Market Outlook aggregate at the EOD boundary.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PublishMarketOutlookSnapshotCommand : ICommand<MarketOutlookEntityId>
{
    /// <summary>Gets the target command actor mailbox name.</summary>
    public const string Actor = ObserveMarketOutlookComponentCommand.Actor;

    /// <summary>Gets the command verb.</summary>
    public const string Verb = "PublishSnapshot";

    /// <summary>Gets the command error identifier.</summary>
    public const int ErrorId = 20102;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.MarketOutlookSnapshotBoundedContext;

    /// <summary>Gets the stable identity of the source EOD event.</summary>
    [Key(6)] public Guid SourceEventId { get; init; }

    /// <summary>Gets the durable source sequence when one is available.</summary>
    [Key(7)] public long SourceEventSequence { get; init; }

    /// <summary>Gets the source event timestamp used for deterministic ordering.</summary>
    [Key(8)] public DateTime SourceEventTimestamp { get; init; }

    /// <summary>Gets the EOD data that establishes the publication boundary.</summary>
    [Key(9)] public FuturesEodDataV2ReadModel FuturesEodData { get; init; } = new();

    /// <summary>Gets an optional reconciled RSI input loaded at the EOD boundary.</summary>
    [Key(10)] public FuturesRsiSignalReadModel? FuturesRsiSignal { get; init; }

    /// <summary>Gets an optional reconciled TDI input loaded at the EOD boundary.</summary>
    [Key(11)] public FuturesTdiSignalReadModel? FuturesTdiSignal { get; init; }

    /// <summary>Gets optional reconciled ITI inputs loaded at the EOD boundary.</summary>
    [Key(12)] public FuturesItiSignalDataReadModel? FuturesItiSignalData { get; init; }

    /// <summary>Gets an optional reconciled VX futures price.</summary>
    [Key(13)] public decimal VixFuturesPrice { get; init; }

    /// <summary>Gets the latest completed Daily EMA family available at the publication boundary.</summary>
    [Key(14)] public FuturesEmaSignalReadModel? FuturesEmaSignal { get; init; }

    /// <summary>Gets the latest completed Daily Bollinger family available at the publication boundary.</summary>
    [Key(15)] public FuturesBbSignalReadModel? FuturesBbSignal { get; init; }

    /// <summary>Gets the CLR command name.</summary>
    [IgnoreMember] public string CommandName => nameof(PublishMarketOutlookSnapshotCommand);

    /// <summary>Gets the event stream identifier derived from the command subject.</summary>
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";

    /// <summary>Gets the source used for committed domain-event metadata.</summary>
    [IgnoreMember] public string EventSource => $"{Actor}Actor";

    /// <summary>Gets the command origination time.</summary>
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;

    /// <summary>Gets the current operating-system user identity.</summary>
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Initializes an empty command for MessagePack deserialization.</summary>
    public PublishMarketOutlookSnapshotCommand() { }

    /// <summary>Initializes an EOD snapshot publication command.</summary>
    public PublishMarketOutlookSnapshotCommand(
        MarketOutlookEntityId entityId,
        Guid sourceEventId,
        long sourceEventSequence,
        DateTime sourceEventTimestamp,
        FuturesEodDataV2ReadModel futuresEodData,
        FuturesRsiSignalReadModel? futuresRsiSignal = null,
        FuturesTdiSignalReadModel? futuresTdiSignal = null,
        FuturesItiSignalDataReadModel? futuresItiSignalData = null,
        decimal vixFuturesPrice = 0,
        FuturesEmaSignalReadModel? futuresEmaSignal = null,
        FuturesBbSignalReadModel? futuresBbSignal = null)
    {
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        SourceEventId = sourceEventId;
        SourceEventSequence = sourceEventSequence;
        SourceEventTimestamp = sourceEventTimestamp;
        FuturesEodData = futuresEodData ?? throw new ArgumentNullException(nameof(futuresEodData));
        FuturesRsiSignal = futuresRsiSignal;
        FuturesTdiSignal = futuresTdiSignal;
        FuturesItiSignalData = futuresItiSignalData;
        VixFuturesPrice = vixFuturesPrice;
        FuturesEmaSignal = futuresEmaSignal;
        FuturesBbSignal = futuresBbSignal;
    }
}
