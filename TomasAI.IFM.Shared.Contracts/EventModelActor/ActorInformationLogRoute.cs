using System.Collections.Frozen;

namespace TomasAI.IFM.Shared.EventModelActor;

readonly record struct ActorInformationLogRoute(ActorType ActorType, string Name, string Verb);
