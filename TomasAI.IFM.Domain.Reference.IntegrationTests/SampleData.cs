using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

/// <summary>
/// Provides sample data for integration tests related to reference actor domain entities.
/// </summary>
public static class SampleData
{
    static readonly DateTime _economicCalendarCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);
    static readonly DateTime _lookupTypeCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);

    /// <summary>
    /// Sample lookup type view model representing a Currency type.
    /// </summary>
    public static readonly LookupTypeReadModel LookupType1 = new LookupTypeReadModel(
        lookupTypeName: "Currency",
        shortCode: "USD",
        orderId: 1,
        description: "United States Dollar",
        createdOn: _lookupTypeCreatedOn,
        createdBy: "IntegrationTest");

    /// <summary>
    /// Sample lookup type view model representing a Currency type (EUR).
    /// </summary>
    public static readonly LookupTypeReadModel LookupType2 = new LookupTypeReadModel(
        lookupTypeName: "Currency",
        shortCode: "EUR",
        orderId: 2,
        description: "Euro",
        createdOn: _lookupTypeCreatedOn,
        createdBy: "IntegrationTest");

    /// <summary>
    /// Sample lookup type view model representing a Trade Status type.
    /// </summary>
    public static readonly LookupTypeReadModel LookupType3 = new LookupTypeReadModel(
        lookupTypeName: "TradeStatus",
        shortCode: "OPEN",
        orderId: 1,
        description: "Trade is open",
        createdOn: _lookupTypeCreatedOn,
        createdBy: "IntegrationTest");

    /// <summary>
    /// Sample lookup type view model representing a Trade Status type (Closed).
    /// </summary>
    public static readonly LookupTypeReadModel LookupType4 = new LookupTypeReadModel(
        lookupTypeName: "TradeStatus",
        shortCode: "CLOSED",
        orderId: 2,
        description: "Trade is closed",
        createdOn: _lookupTypeCreatedOn,
        createdBy: "IntegrationTest");

    /// <summary>
    /// Sample economic calendar view model representing a Non-Farm Payrolls event.
    /// </summary>
    public static readonly EconomicCalendarReadModel EconomicCalendar1 = new EconomicCalendarReadModel(
        eventDate: new DateTime(2025, 02, 15, 14, 30, 0),
        countryCode: "US",
        eventName: "Non-Farm Payrolls",
        actual: "250K",
        forecast: "240K",
        prior: "230K",
        createdOn: _economicCalendarCreatedOn,
        createdBy: "IntegrationTest");

    /// <summary>
    /// Sample economic calendar view model representing a GDP Growth Rate event.
    /// </summary>
    public static readonly EconomicCalendarReadModel EconomicCalendar2 = new EconomicCalendarReadModel(
        eventDate: new DateTime(2025, 03, 20, 10, 0, 0),
        countryCode: "US",
        eventName: "GDP Growth Rate",
        actual: "2.5%",
        forecast: "2.3%",
        prior: "2.1%",
        createdOn: _economicCalendarCreatedOn,
        createdBy: "IntegrationTest");

    /// <summary>
    /// Sample economic calendar view model representing an ECB Interest Rate Decision event.
    /// </summary>
    public static readonly EconomicCalendarReadModel EconomicCalendar3 = new EconomicCalendarReadModel(
        eventDate: new DateTime(2025, 04, 10, 9, 0, 0),
        countryCode: "EU",
        eventName: "ECB Interest Rate Decision",
        actual: "4.0%",
        forecast: "4.0%",
        prior: "3.75%",
        createdOn: _economicCalendarCreatedOn,
        createdBy: "IntegrationTest");

    /// <summary>
    /// Sample economic calendar view model representing a CPI data release event.
    /// </summary>
    public static readonly EconomicCalendarReadModel EconomicCalendar4 = new EconomicCalendarReadModel(
        eventDate: new DateTime(2025, 05, 12, 8, 30, 0),
        countryCode: "US",
        eventName: "Consumer Price Index",
        actual: "3.2%",
        forecast: "3.1%",
        prior: "3.0%",
        createdOn: _economicCalendarCreatedOn,
        createdBy: "IntegrationTest");

    /// <summary>
    /// Sample economic calendar view model representing a UK Retail Sales event.
    /// </summary>
    public static readonly EconomicCalendarReadModel EconomicCalendar5 = new EconomicCalendarReadModel(
        eventDate: new DateTime(2025, 06, 18, 7, 0, 0),
        countryCode: "GB",
        eventName: "Retail Sales",
        actual: "1.5%",
        forecast: "1.2%",
        prior: "0.8%",
        createdOn: _economicCalendarCreatedOn,
        createdBy: "IntegrationTest");

    /// <summary>
    /// Array of all sample economic calendars for bulk import testing.
    /// </summary>
    public static readonly EconomicCalendarReadModel[] EconomicCalendars =
    [
        EconomicCalendar1,
        EconomicCalendar2,
        EconomicCalendar3,
        EconomicCalendar4,
        EconomicCalendar5
    ];
}
