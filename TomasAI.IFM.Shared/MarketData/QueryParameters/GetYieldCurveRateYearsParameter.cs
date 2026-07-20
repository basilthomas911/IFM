using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve yield curve rate years.
/// </summary>
/// <remarks>This record is typically used as a data transfer object in service or actor-based APIs.
/// Since this query retrieves all years, no specific parameters are required.</remarks>
[MessagePackObject(false)]
public record GetYieldCurveRateYearsParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    [SerializationConstructor]
    public GetYieldCurveRateYearsParameter()
    {
        QueryParams = string.Empty;
    }

    /// <summary>
    /// Formats the parameter as a string identifier.
    /// </summary>
    /// <returns>A formatted string representation.</returns>
    public string Format()
        => "YieldCurveRateYears";
}
