using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;

public static class GetEconomicCalendarCountryCodes
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask<EconomicCalendarCountryCodeReadModel[]> GetEconomicCalendarCountryCodesAsync(
        this GetEconomicCalendarCountryCodesQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
        => [.. await (cancellationToken.CanBeCanceled
            ? dbFactory.ReferenceDb.GetEconomicCalendarCountryCodesAsync(cancellationToken)
            : dbFactory.ReferenceDb.GetEconomicCalendarCountryCodesAsync())];
}
