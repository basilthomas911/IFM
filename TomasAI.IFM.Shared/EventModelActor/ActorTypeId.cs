namespace TomasAI.IFM.Shared.EventModelActor;

public record struct ActorTypeId(ActorType ActorType, string Name, string Verb)
{
    public override string ToString() => $"{ActorType}.{Name}.{Verb}";
}