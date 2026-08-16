using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

namespace TomasAI.IFM.Application.Api.Server;

public sealed class FmpImportScheduleOptions
{
    public bool Enabled { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);
    public int LookbackDays { get; set; } = 7;
    public int ForwardDays { get; set; } = 7;
    public string[]? CountryCodes { get; set; }

    public FmpImportScheduleOptions Validate()
    {
        if (Interval < TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(Interval));
        if (LookbackDays < 0)
            throw new ArgumentOutOfRangeException(nameof(LookbackDays));
        if (ForwardDays < 0)
            throw new ArgumentOutOfRangeException(nameof(ForwardDays));
        return this;
    }
}

public sealed class FmpMarketDataImportHostedService(
    IFmpMarketDataImportCoordinator coordinator,
    FmpImportScheduleOptions options,
    TimeProvider timeProvider,
    ILogger<FmpMarketDataImportHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(options.Interval, timeProvider, stoppingToken).ConfigureAwait(false);
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            try
            {
                var result = await coordinator.ImportAsync(
                    new FmpMarketDataImportRequest(
                        today.AddDays(-options.LookbackDays),
                        today.AddDays(options.ForwardDays),
                        CountryCodes: options.CountryCodes),
                    stoppingToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Scheduled FMP import submitted {SubmittedCommands} commands; {RejectedSubmissions} submissions were rejected. Terminal import outcomes are recorded by their correlated events.",
                    result.SubmittedCommands,
                    result.RejectedSubmissions);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled FMP market-data import failed.");
            }
        }
    }
}
