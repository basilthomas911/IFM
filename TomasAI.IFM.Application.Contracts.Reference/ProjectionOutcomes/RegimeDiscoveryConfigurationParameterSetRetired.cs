using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;

namespace TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;

[MessagePackObject]
public sealed partial record RegimeDiscoveryConfigurationParameterSetRetiredCompleteEvent : ReferenceProjectionComplete<RegimeDiscoveryParameterSetEntityId>;

[MessagePackObject]
public sealed partial record RegimeDiscoveryConfigurationParameterSetRetiredFailEvent : ReferenceProjectionFail<RegimeDiscoveryParameterSetEntityId>;
