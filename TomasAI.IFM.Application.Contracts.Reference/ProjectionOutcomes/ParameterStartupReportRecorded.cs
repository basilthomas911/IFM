using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;

[MessagePackObject]
public sealed partial record ParameterStartupReportRecordedCompleteEvent : ReferenceProjectionComplete<ParameterStartupEntityId>;

[MessagePackObject]
public sealed partial record ParameterStartupReportRecordedFailEvent : ReferenceProjectionFail<ParameterStartupEntityId>;
