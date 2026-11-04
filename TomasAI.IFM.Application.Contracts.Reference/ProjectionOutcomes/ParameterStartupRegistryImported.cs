using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;

[MessagePackObject]
public sealed partial record ParameterStartupRegistryImportedCompleteEvent : ReferenceProjectionComplete<ParameterStartupEntityId>;

[MessagePackObject]
public sealed partial record ParameterStartupRegistryImportedFailEvent : ReferenceProjectionFail<ParameterStartupEntityId>;
