using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the most recent yield curve rate.
/// </summary>
/// <remarks>
/// Use this type when requesting the latest yield curve rate snapshot.
/// This record is typically used as a data transfer object in service or actor-based APIs.
/// Since this query retrieves the most recent rate, no specific parameters are required.
/// </remarks>
[MessagePackObject(false)]
public record GetLastYieldCurveRateParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    [SerializationConstructor]
    public GetLastYieldCurveRateParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => "last";
}
