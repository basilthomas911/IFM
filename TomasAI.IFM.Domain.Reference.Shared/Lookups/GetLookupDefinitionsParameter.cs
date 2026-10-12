using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Lookups;

public sealed record GetLookupDefinitionsParameter(string GroupName) : IQueryParameter
{
    public string? QueryParams => null;
}
