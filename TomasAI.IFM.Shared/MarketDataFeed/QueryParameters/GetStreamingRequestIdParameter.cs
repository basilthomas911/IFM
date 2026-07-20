using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve a streaming request id.
/// </summary>
[MessagePackObject(false)]
public record GetStreamingRequestIdParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetStreamingRequestIdParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => "requestid";
}
