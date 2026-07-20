using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the normal curve table.
/// </summary>
[MessagePackObject(false)]
public record GetNormalCurveTableParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetNormalCurveTableParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => "normalcurvetable";
}
