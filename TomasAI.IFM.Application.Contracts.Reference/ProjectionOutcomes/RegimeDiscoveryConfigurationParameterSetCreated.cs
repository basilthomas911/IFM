using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;

namespace TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;

[MessagePackObject]
public sealed partial record RegimeDiscoveryConfigurationParameterSetCreatedCompleteEvent : ReferenceProjectionComplete<RegimeDiscoveryParameterSetEntityId>;

[MessagePackObject]
public sealed partial record RegimeDiscoveryConfigurationParameterSetCreatedFailEvent : ReferenceProjectionFail<RegimeDiscoveryParameterSetEntityId>;
