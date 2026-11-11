using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Commands;

/// <summary>Applies an authoritative rollover set prepared against one committed catalog revision.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ReplaceFuturesRolloverSetCommand : ICommand<FuturesContractId>
{
    public const string Verb = "ReplaceRolloverSet";
    public const int ErrorId = 6012;
    public const int CatalogChanged = 409;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesContractId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [Key(6)] public long ExpectedCatalogRevision { get; init; }
    [Key(7)] public FuturesContractRolloverReadModel Rollover { get; init; } = null!;
    [Key(8)] public FuturesContractV3ReadModel[] ChangedContracts { get; init; } = [];

    [IgnoreMember] public string CommandName => nameof(ReplaceFuturesRolloverSetCommand);
    [IgnoreMember] public string StreamId => SecuritiesCatalogStreams.Futures;
    [IgnoreMember] public string EventSource => "FuturesContractCommandActor";
    [IgnoreMember] public DateTime OriginatedOn => MessageOrigins.Get(this)?.OriginatedAtUtc.UtcDateTime ?? default;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}
