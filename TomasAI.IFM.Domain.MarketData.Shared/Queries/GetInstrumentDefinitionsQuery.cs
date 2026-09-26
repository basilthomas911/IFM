using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed class GetInstrumentDefinitionsQuery : IQuery<InstrumentDefinitionPage>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetInstrumentDefinitionsQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="request">The Request field.</param>
    [SerializationConstructor]
    public GetInstrumentDefinitionsQuery(ActorSubject subject, IActorEntityId entityId, InstrumentDefinitionPageRequest request)
    {
        Subject = subject;
        EntityId = entityId;
        Request = request;
    }
    [IgnoreMember] public const string Actor = "FuturesContractQuery";
    [IgnoreMember] public const string Verb = "GetInstrumentDefinitions";
    [IgnoreMember] public const int ErrorId = 1063;
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = ActorEntityId.Default;
    [Key(2)] public InstrumentDefinitionPageRequest Request { get; set; } = new();
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => Request.QueryParams;
}
