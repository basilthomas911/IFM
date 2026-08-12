using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.BDDTests;

/// <summary>
/// Provides sample data for BDD tests related to reference actor domain entities.
/// </summary>
public static class SampleData
{
    static readonly DateTime _lookupTypeCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);

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
