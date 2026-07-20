using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the current value date.
/// </summary>
[MessagePackObject(false)]
public record GetValueDateParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetValueDateParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => "valueDate";
}
