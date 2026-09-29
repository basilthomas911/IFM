using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Domain.MarketData.Shared;

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
    IValueDateProvider valueDateProvider,
    ILogger<FmpMarketDataImportHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!options.Enabled)
                return;

            using var timer = new PeriodicTimer(options.Interval, timeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                var today = valueDateProvider.ValueDate;
                try
                {
                    var result = await coordinator.ImportAsync(
                        new FmpMarketDataImportRequest(
                            today.AddDays(-options.LookbackDays),
                            today.AddDays(options.ForwardDays),
                            IncludeTreasury: false,
                            CountryCodes: options.CountryCodes),
                        stoppingToken).ConfigureAwait(false);
                    logger.LogInformation(
                        "Scheduled FMP import submitted {SubmittedCommands} commands; {RejectedSubmissions} submissions were rejected. Terminal import outcomes are recorded by their correlated events.",
                        result.SubmittedCommands,
                        result.RejectedSubmissions);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Scheduled FMP market-data import failed; scheduling continues.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Scheduled FMP market-data import stopped during API shutdown.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Scheduled FMP market-data import worker failed unexpectedly; the API host will remain running.");
        }
    }
}
