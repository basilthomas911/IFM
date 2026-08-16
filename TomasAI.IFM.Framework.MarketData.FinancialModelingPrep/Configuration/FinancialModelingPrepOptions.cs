namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

/// <summary>
/// Non-secret configuration for the Financial Modeling Prep adapter.
/// </summary>
public sealed class FinancialModelingPrepOptions
{
    public const string DefaultApiKeyEnvironmentVariable = "FMP_API_KEY";

    public Uri BaseAddress { get; set; } = new("https://financialmodelingprep.com/stable/");

    public string TreasuryRatesEndpoint { get; set; } = "treasury-rates";

    public string EconomicCalendarEndpoint { get; set; } = "economic-calendar";

    public string ApiKeyEnvironmentVariable { get; set; } = DefaultApiKeyEnvironmentVariable;

    public int LatestTreasuryLookbackDays { get; set; } = 14;

    public int MaximumProviderWindowDays { get; set; } = 90;

    public int MaximumRequestRangeDays { get; set; } = 3_660;

    public int MaximumResponseBytes { get; set; } = 4 * 1024 * 1024;

    public int MaximumNormalizedRows { get; set; } = 10_000;

    public int MaximumConcurrentRequests { get; set; } = 2;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);

    public TimeSpan TotalOperationTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public int MaximumRetryAttempts { get; set; } = 2;

    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    internal void Validate(bool requireApiKey)
    {
        if (BaseAddress is null || !BaseAddress.IsAbsoluteUri || BaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            throw new FinancialModelingPrepConfigurationException("The FMP base address must be an absolute HTTPS URI.");
        }

        ValidateEndpoint(TreasuryRatesEndpoint, nameof(TreasuryRatesEndpoint));
        ValidateEndpoint(EconomicCalendarEndpoint, nameof(EconomicCalendarEndpoint));

        if (string.IsNullOrWhiteSpace(ApiKeyEnvironmentVariable))
        {
            throw new FinancialModelingPrepConfigurationException("The FMP API-key environment variable name is required.");
        }

        RequirePositive(LatestTreasuryLookbackDays, nameof(LatestTreasuryLookbackDays));
        RequirePositive(MaximumProviderWindowDays, nameof(MaximumProviderWindowDays));
        RequirePositive(MaximumRequestRangeDays, nameof(MaximumRequestRangeDays));
        RequirePositive(MaximumResponseBytes, nameof(MaximumResponseBytes));
        RequirePositive(MaximumNormalizedRows, nameof(MaximumNormalizedRows));
        RequirePositive(MaximumConcurrentRequests, nameof(MaximumConcurrentRequests));
        RequirePositive(CircuitBreakerFailureThreshold, nameof(CircuitBreakerFailureThreshold));

        if (LatestTreasuryLookbackDays > MaximumRequestRangeDays)
        {
            throw new FinancialModelingPrepConfigurationException(
                $"{nameof(LatestTreasuryLookbackDays)} cannot exceed {nameof(MaximumRequestRangeDays)}.");
        }

        if (RequestTimeout <= TimeSpan.Zero || TotalOperationTimeout <= TimeSpan.Zero)
        {
            throw new FinancialModelingPrepConfigurationException("FMP request and operation timeouts must be positive.");
        }

        if (MaximumRetryAttempts is < 0 or > 10)
        {
            throw new FinancialModelingPrepConfigurationException($"{nameof(MaximumRetryAttempts)} must be between 0 and 10.");
        }

        if (InitialRetryDelay < TimeSpan.Zero || MaximumRetryDelay < InitialRetryDelay)
        {
            throw new FinancialModelingPrepConfigurationException("FMP retry delays are invalid.");
        }

        if (CircuitBreakerBreakDuration <= TimeSpan.Zero)
        {
            throw new FinancialModelingPrepConfigurationException($"{nameof(CircuitBreakerBreakDuration)} must be positive.");
        }

        if (requireApiKey && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable)))
        {
            throw new FinancialModelingPrepConfigurationException(
                $"FMP is enabled but environment variable '{ApiKeyEnvironmentVariable}' is not set.");
        }
    }

    internal string GetApiKey()
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new FinancialModelingPrepConfigurationException(
                $"FMP API credentials are unavailable. Set environment variable '{ApiKeyEnvironmentVariable}'.");
        }

        return apiKey;
    }

    private static void ValidateEndpoint(string endpoint, string name)
    {
        if (string.IsNullOrWhiteSpace(endpoint)
            || Uri.IsWellFormedUriString(endpoint, UriKind.Absolute)
            || endpoint.Contains('?')
            || endpoint.Contains('#'))
        {
            throw new FinancialModelingPrepConfigurationException($"{name} must be a non-empty relative path without a query or fragment.");
        }
    }

    private static void RequirePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new FinancialModelingPrepConfigurationException($"{name} must be positive.");
        }
    }
}
