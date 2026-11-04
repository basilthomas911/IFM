using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;

[MessagePackObject]
public sealed partial record ParameterAssignmentAssignedCompleteEvent : ReferenceProjectionComplete<ParameterAssignmentEntityId>;

[MessagePackObject]
public sealed partial record ParameterAssignmentAssignedFailEvent : ReferenceProjectionFail<ParameterAssignmentEntityId>;
