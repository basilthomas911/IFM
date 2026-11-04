using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;

[MessagePackObject]
public sealed partial record ParameterSetCreatedCompleteEvent : ReferenceProjectionComplete<ParameterSetEntityId>;

[MessagePackObject]
public sealed partial record ParameterSetCreatedFailEvent : ReferenceProjectionFail<ParameterSetEntityId>;
