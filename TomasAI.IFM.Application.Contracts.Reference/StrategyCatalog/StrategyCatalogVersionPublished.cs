using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed partial record StrategyCatalogVersionPublishedCompleteEvent : ReferenceProjectionComplete<ActorEntityId>;

[MessagePackObject]
public sealed partial record StrategyCatalogVersionPublishedFailEvent : ReferenceProjectionFail<ActorEntityId>;
