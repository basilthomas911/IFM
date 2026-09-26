using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetParameterStartupRunsQuery:IQuery<ParameterStartupRun[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetParameterStartupRunsQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="limit">The Limit field.</param>
    /// <param name="afterCreatedAtUtc">The AfterCreatedAtUtc field.</param>
    /// <param name="afterRunId">The AfterRunId field.</param>
    [SerializationConstructor]
    public GetParameterStartupRunsQuery(ActorSubject subject, IActorEntityId entityId, int limit, DateTime? afterCreatedAtUtc, Guid? afterRunId)
    {
        Subject = subject;
        EntityId = entityId;
        Limit = limit;
        AfterCreatedAtUtc = afterCreatedAtUtc;
        AfterRunId = afterRunId;
    }
 public const string Actor="ParameterSetQuery";public const string Verb="GetParameterStartupRuns";
 [Key(0)]public ActorSubject Subject{get;init;}
 [Key(1)]public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)]public int Limit{get;init;}=20;
 [Key(3)]public DateTime? AfterCreatedAtUtc{get;init;}
 [Key(4)]public Guid? AfterRunId{get;init;}
 [IgnoreMember]public int ErrorCode{get;init;}=33101;
 [IgnoreMember]public string? QueryParams{get;init;}
}
