namespace TomasAI.IFM.Shared.Application;

/// <summary>
/// Provides a collection of URI paths for querying option pricer endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// option pricer API. These endpoints are used to retrieve option pricer devices, spread distributions, and job status.
/// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public static class OptionPricerQueryUriPath
{
    public const string GetOptionPricerDevices = "/api/optionpricer/devices";
    public const string GetSpreadDistribution = "/api/optionpricer/spreaddistribution";
    public const string IsSpreadDistributionJobInProgress = "/api/optionpricer/spreaddistribution/jobinprogress";
}

/// <summary>
/// Provides a collection of URI paths for querying system administrator endpoints.
/// </summary>
