namespace TomasAI.IFM.Shared.Application;

/// <summary>
/// Provides a collection of URI paths for querying reference data endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// reference API. These endpoints are used to retrieve lookup types, economic calendars, seed IDs, and related reference data.
/// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public static class ReferenceQueryUriPath
{
    public const string GetDefaultFuturesContractDefinitions = "/api/reference/defaultfuturescontractdefinitions";
    public const string GetLookupTypes = "/api/reference/lookuptypes";
    public const string GetLookupType = "/api/reference/lookuptype";
    public const string GetLookupTypeNames = "/api/reference/lookuptype/names";
    public const string GetLookupTypeShortCodes = "/api/reference/lookuptype/shortcodes";
    public const string LookupTypeShortCodeExists = "/api/reference/lookuptype/shortcodeexists";
    public const string GetMarketDataDefinitionTypes = "/api/reference/marketdatadefinitiontypes";
    public const string GetReferenceDataDefinitionTypes = "/api/reference/referencedatadefinitiontypes";
    public const string GetSystemAdminFunctionTypes = "/api/reference/systemadminfunctiontypes";
    public const string GetNextSeedId = "/api/reference/nextseedid";
    public const string GetCurrentSeedId = "/api/reference/currentseedid";
    public const string GetFuturesOptionStrikePriceDefinitions = "/api/reference/futuresoptionstrikepricedefinitions";
    public const string GetMDIForwardLossRatios = "/api/reference/mdiforwardlossratios";
}

/// <summary>
/// Provides a collection of URI paths for querying system administrator endpoints.
/// </summary>
