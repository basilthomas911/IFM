using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Prepares the next futures rollover assignment during the exchange's closed
/// 17:00-18:00 Eastern interval. Failures are operational results: they are
/// logged and retried without faulting the API host.
/// </summary>
public sealed class FuturesRolloverPreparationHostedService(
    FuturesContractRolloverStartupCheck rolloverCheck,
    IFuturesExchangeBusinessCalendar calendar,
    IStatusConsoleWriter statusConsoleWriter,
    TimeProvider timeProvider,
    ILogger<FuturesRolloverPreparationHostedService> logger) : BackgroundService
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    DateOnly? _completedTarget;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Futures rollover preparation failed; the previous coherent assignment remains active and preparation will retry.");
                await ReportAsync(
                    $"Futures rollover preparation failed and will retry: {Bound(exception.Message)}",
                    errorCode: 10031).ConfigureAwait(false);
            }

            if (!await HostedServiceLifecycle.DelayAsync(
                    PollInterval, timeProvider, stoppingToken).ConfigureAwait(false))
                return;
        }
    }

    internal async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        if (!FuturesRolloverPreparationPolicy.TryResolveTargetValueDate(
                timeProvider.GetUtcNow(), calendar, out var target)
            || _completedTarget == target)
            return;

        await rolloverCheck.ExecuteAsync(target, cancellationToken).ConfigureAwait(false);
        _completedTarget = target;
        logger.LogInformation(
            "Futures rollover preparation qualified for effective value date {ValueDate}.",
            target);
        await ReportAsync(
            $"Futures rollover preparation qualified for effective value date {target:yyyy-MM-dd}.",
            errorCode: null).ConfigureAwait(false);
    }

    async Task ReportAsync(string message, int? errorCode)
    {
        try
        {
            if (errorCode.HasValue)
                await statusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.System, errorCode.Value, message).ConfigureAwait(false);
            else
                await statusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.System, message).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Unable to publish futures rollover preparation status to the System Console.");
        }
    }

    static string Bound(string value) => value.Length <= 512 ? value : value[..512];
}
