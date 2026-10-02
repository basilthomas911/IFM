using MessagePack;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.Queries;

/// <summary>Requests the latest atomically stored Supervisor actor-health summary.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetSupervisorHealthSummaryQuery : IQuery<SupervisorHealthSummaryReadModel>
{
    public const string Actor = "SupervisorQuery";
    public const string QueryVerb = "GetHealthSummary";

    /// <summary>Creates an empty query for serialization.</summary>
    public GetSupervisorHealthSummaryQuery()
    {
    }

    /// <summary>Rehydrates the permanent query schema.</summary>
    [SerializationConstructor]
    public GetSupervisorHealthSummaryQuery(ActorSubject subject, ActorEntityId entityId)
    {
        Subject = subject;
        EntityId = entityId;
    }

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;
    [IgnoreMember] public int ErrorCode => 9700;
    [IgnoreMember] public string? QueryParams => null;
    [IgnoreMember] public string Verb => QueryVerb;
}
