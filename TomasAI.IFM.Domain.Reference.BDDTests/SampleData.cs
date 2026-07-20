using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.BDDTests;

/// <summary>
/// Provides sample data for BDD tests related to reference actor domain entities.
/// </summary>
public static class SampleData
{
    static readonly DateTime _economicEventDate = new DateTime(2025, 02, 15, 14, 30, 0);
    static readonly DateTime _economicCalendarCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);
    static readonly DateTime _lookupTypeCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);

    /// <summary>
    /// Sample economic calendar view model representing a Non-Farm Payrolls event.
    /// </summary>
    public static readonly EconomicCalendarReadModel EconomicCalendar = new EconomicCalendarReadModel(
        eventDate: _economicEventDate,
        countryCode: "US",
        eventName: "Non-Farm Payrolls",
        actual: "250K",
        forecast: "240K",
        prior: "230K",
        createdOn: _economicCalendarCreatedOn,
        createdBy: "admin");

    /// <summary>
    /// Alternate sample economic calendar view model representing a GDP event.
    /// </summary>
    public static readonly EconomicCalendarReadModel EconomicCalendarAlternate = new EconomicCalendarReadModel(
        eventDate: new DateTime(2025, 03, 20, 10, 0, 0),
        countryCode: "US",
        eventName: "GDP Growth Rate",
        actual: "2.5%",
        forecast: "2.3%",
        prior: "2.1%",
        createdOn: _economicCalendarCreatedOn,
        createdBy: "admin");

    /// <summary>
    /// Sample economic calendar view model representing a European event.
    /// </summary>
    public static readonly EconomicCalendarReadModel EconomicCalendarEU = new EconomicCalendarReadModel(
        eventDate: new DateTime(2025, 04, 10, 9, 0, 0),
        countryCode: "EU",
        eventName: "ECB Interest Rate Decision",
        actual: "4.0%",
        forecast: "4.0%",
        prior: "3.75%",
        createdOn: _economicCalendarCreatedOn,
        createdBy: "admin");

    /// <summary>
    /// Sample lookup type view model representing a Currency type.
    /// </summary>
    public static readonly LookupTypeReadModel LookupType = new LookupTypeReadModel(
        lookupTypeName: "Currency",
        shortCode: "USD",
        orderId: 1,
        description: "United States Dollar",
        createdOn: _lookupTypeCreatedOn,
        createdBy: "admin");

    /// <summary>
    /// Alternate sample lookup type view model representing a different Currency type.
    /// </summary>
    public static readonly LookupTypeReadModel LookupTypeAlternate = new LookupTypeReadModel(
        lookupTypeName: "Currency",
        shortCode: "EUR",
        orderId: 2,
        description: "Euro",
        createdOn: _lookupTypeCreatedOn,
        createdBy: "admin");

    /// <summary>
    /// Sample lookup type view model representing an Asset Class type.
    /// </summary>
    public static readonly LookupTypeReadModel LookupTypeAssetClass = new LookupTypeReadModel(
        lookupTypeName: "AssetClass",
        shortCode: "EQ",
        orderId: 1,
        description: "Equity",
        createdOn: _lookupTypeCreatedOn,
        createdBy: "admin");
}
