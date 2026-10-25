using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetFuturesOptionContractsPageQuery : IQuery<FuturesOptionContractPageReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesOptionContractQuery";
    [IgnoreMember] public const string Verb = "GetFuturesOptionContractsPage";
    [IgnoreMember] public const int ErrorId = 1033;
    [Key(0)] public ActorSubject Subject { get; init; } = default!;
    [Key(1)] public IActorEntityId EntityId { get; init; } = default!;
    [Key(2)] public GetFuturesOptionContractsPageParameter Request { get; init; } = new();
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string QueryParams { get; init; } = string.Empty;

    public GetFuturesOptionContractsPageQuery() { }
    public GetFuturesOptionContractsPageQuery(GetFuturesOptionContractsPageParameter request)
        => (Request, EntityId) = (request, request);
    [SerializationConstructor]
    public GetFuturesOptionContractsPageQuery(ActorSubject subject, IActorEntityId entityId, GetFuturesOptionContractsPageParameter request)
        => (Subject, EntityId, Request) = (subject, request, request);
}
