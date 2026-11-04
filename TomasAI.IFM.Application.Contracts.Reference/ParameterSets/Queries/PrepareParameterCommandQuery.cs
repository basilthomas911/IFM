using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Requests coherent, explicit owner evidence before dispatching a parameter mutation.</summary>
[MessagePackObject]
public sealed record PrepareParameterCommandQuery : IQuery<ParameterCommandPreparation>
{
    public PrepareParameterCommandQuery() { }

    [SerializationConstructor]
    public PrepareParameterCommandQuery(ActorSubject subject, IActorEntityId entityId, ParameterCommandPreparation preparation)
    {
        Subject = subject;
        EntityId = entityId;
        Preparation = preparation;
    }

    public const string Actor = "ParameterPreparationQuery";
    public const string Verb = "PrepareParameterCommand";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public ParameterCommandPreparation Preparation { get; init; } = new();
    [IgnoreMember] public int ErrorCode { get; init; } = 33101;
    [IgnoreMember] public string? QueryParams { get; init; }
}
