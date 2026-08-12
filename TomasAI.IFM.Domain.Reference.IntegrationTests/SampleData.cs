using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

/// <summary>
/// Provides sample data for integration tests related to reference actor domain entities.
/// </summary>
public static class SampleData
{
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

}
