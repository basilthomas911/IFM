using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject]
public readonly record struct ParameterStartupEntityId([property: Key(0)] Guid Id) : IActorEntityId
{
    public static ParameterStartupEntityId Registry { get; } = new(new Guid("fedabf28-dcfb-4e01-8e5f-20c96a4e603c"));
    public string Format() => Id.ToString("N");
}

