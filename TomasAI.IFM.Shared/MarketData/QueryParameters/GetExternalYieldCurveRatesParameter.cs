using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve external yield curve rates.
/// </summary>
[MessagePackObject(false)]
public record GetExternalYieldCurveRatesParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetExternalYieldCurveRatesParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => ActorEntityId.Default.Format();
}
