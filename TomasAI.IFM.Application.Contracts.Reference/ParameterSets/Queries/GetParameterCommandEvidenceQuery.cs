using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject]
public sealed record GetParameterCommandEvidenceQuery : IQuery<ParameterCommandEvidenceReadModel>
{
    public const string Actor = "ParameterPreparationQuery";
    public const string Verb = "GetParameterCommandEvidence";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public ParameterCommandPreparation Preparation { get; init; } = new();
    [IgnoreMember] public int ErrorCode { get; init; } = 33101;
    [IgnoreMember] public string? QueryParams { get; init; }
}

[MessagePackObject]
public sealed record ParameterCommandEvidenceReadModel(
    [property: Key(0)] Guid OperationId,
    [property: Key(1)] ParameterCommandEvidence Evidence);
