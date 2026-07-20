using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve an option quote id.
/// </summary>
[MessagePackObject(false)]
public record GetOptionQuoteIdParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetOptionQuoteIdParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => "quoteid";
}
