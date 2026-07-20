using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve external economic calendars from an external data source.
/// </summary>
/// <remarks>
/// This parameter object is used as a query identifier for retrieving economic calendar data from external sources.
/// Since this query does not require additional filtering parameters, the QueryParams property provides a simple
/// identification string.
/// </remarks>
[MessagePackObject(false)]
public record GetExternalEconomicCalendarsParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetExternalEconomicCalendarsParameter()
    {
        QueryParams = "GetExternalEconomicCalendars";
    }

    public string Format()
        => "ExternalEconomicCalendars";
}
