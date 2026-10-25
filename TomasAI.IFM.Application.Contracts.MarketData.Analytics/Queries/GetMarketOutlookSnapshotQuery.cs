using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetMarketOutlookSnapshotQuery : IQuery<MarketOutlookReadModel>
{
    [IgnoreMember] public const string Actor = "MarketOutlookSnapshotQuery";
    [IgnoreMember] public const string Verb = "GetMarketOutlookSnapshot";
    [IgnoreMember] public const int ErrorId = 1019;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = new GetMarketOutlookSnapshotParameter();
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
    [Key(2)] public string ContractId { get; init; } = string.Empty;
    [Key(3)] public DateOnly ValueDate { get; init; }

    public GetMarketOutlookSnapshotQuery() { }

    public GetMarketOutlookSnapshotQuery(
        string contractId,
        DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        EntityId = new GetMarketOutlookSnapshotParameter(ContractId, valueDate);
    }

    [SerializationConstructor]
    public GetMarketOutlookSnapshotQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        string contractId,
        DateOnly valueDate)
        : this(contractId, valueDate) => Subject = subject;
}
