using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;

[MessagePackObject]
public sealed partial record ParameterAssignmentDisabledCompleteEvent : ReferenceProjectionComplete<ParameterAssignmentEntityId>;

[MessagePackObject]
public sealed partial record ParameterAssignmentDisabledFailEvent : ReferenceProjectionFail<ParameterAssignmentEntityId>;
