using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve economic calendar country codes.
/// </summary>
/// <remarks>
/// This parameter object is used as a query identifier for retrieving the list of country codes
/// used in the economic calendar system. Since this query does not require additional filtering
/// parameters, the QueryParams property provides a simple identification string.
/// </remarks>
[MessagePackObject(false)]
public record GetEconomicCalendarCountryCodesParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetEconomicCalendarCountryCodesParameter()
    {
        QueryParams = "GetEconomicCalendarCountryCodes";
    }

    public string Format()
        => "EconomicCalendarCountryCodes";
}
