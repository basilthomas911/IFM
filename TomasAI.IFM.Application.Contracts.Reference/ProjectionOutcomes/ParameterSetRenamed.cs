using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;

[MessagePackObject]
public sealed partial record ParameterSetRenamedCompleteEvent : ReferenceProjectionComplete<ParameterSetEntityId>;

[MessagePackObject]
public sealed partial record ParameterSetRenamedFailEvent : ReferenceProjectionFail<ParameterSetEntityId>;
