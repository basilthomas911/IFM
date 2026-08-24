using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;

/// <summary>Creates concrete UI services with substitutes for dependencies irrelevant to a test.</summary>
public static class UiServiceFactory
{
    /// <summary>Creates a Reference service around the supplied typed API and event-consumer boundaries.</summary>
    public static IReferenceDataService CreateReference(
        IReferenceQueryApi? queryApi = null,
        IReferenceCommandApi? commandApi = null,
        ILookupTypeUIEventConsumer? eventConsumer = null)
        => new ReferenceDataService(
            commandApi ?? Substitute.For<IReferenceCommandApi>(),
            queryApi ?? Substitute.For<IReferenceQueryApi>(),
            eventConsumer ?? Substitute.For<ILookupTypeUIEventConsumer>());

    /// <summary>Creates an Economic Calendar service around the supplied typed API and event-consumer boundaries.</summary>
    public static IEconomicCalendarService CreateEconomicCalendar(
        IMarketDataQueryApi? queryApi = null,
        IMarketDataCommandApi? commandApi = null,
        IEconomicCalendarUIEventConsumer? eventConsumer = null)
        => new EconomicCalendarService(
            commandApi ?? Substitute.For<IMarketDataCommandApi>(),
            queryApi ?? Substitute.For<IMarketDataQueryApi>(),
            eventConsumer ?? Substitute.For<IEconomicCalendarUIEventConsumer>());
}
