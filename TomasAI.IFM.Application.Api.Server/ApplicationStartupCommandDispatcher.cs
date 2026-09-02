using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Posts one typed StartApplication command after host and actor bootstrap are healthy.</summary>
public sealed class ApplicationStartupCommandDispatcher(
    IHostApplicationLifetime lifetime,
    IApplicationBootstrapReadiness bootstrapReadiness,
    IFuturesMarketSessionAuthority marketSessionAuthority,
    IApplicationCommandApi commandApi,
    IStatusConsoleWriter statusConsoleWriter,
    ApplicationStartupOptions options,
    TimeProvider timeProvider,
    ILogger<ApplicationStartupCommandDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await DispatchAfterBootstrapAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Application startup dispatch was cancelled by API process shutdown.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Application startup dispatch failed before command acceptance.");
            await ReportAsync(
                $"Application startup dispatch failed before command acceptance: {Bound(exception.Message)}",
                10013).ConfigureAwait(false);
        }
    }

    async Task DispatchAfterBootstrapAsync(CancellationToken stoppingToken)
    {
        if (!options.AutoStartAfterBootstrap)
        {
            logger.LogInformation("Automatic Application startup dispatch is disabled.");
            return;
        }

        if (!await HostedServiceLifecycle.WaitForSignalAsync(
                lifetime.ApplicationStarted, stoppingToken).ConfigureAwait(false))
        {
            logger.LogInformation("Application startup dispatch stopped before API bootstrap completed.");
            return;
        }
        var started = timeProvider.GetTimestamp();
        while (timeProvider.GetElapsedTime(started) < options.BootstrapTimeout)
        {
            stoppingToken.ThrowIfCancellationRequested();
            if (await bootstrapReadiness.IsHealthyAsync(stoppingToken).ConfigureAwait(false))
            {
                var valueDate = marketSessionAuthority.Current.OperationalValueDate;
                var result = await commandApi.StartApplicationAsync(valueDate).ConfigureAwait(false);
                var message = result.Success
                    ? $"StartApplication command accepted after bootstrap. ValueDate={valueDate:yyyy-MM-dd}; CommandId={result.Value}."
                    : $"StartApplication command rejected after bootstrap ({result.ErrorCode}): {result.ErrorMessage}";
                await ReportAsync(message, result.Success ? null : result.ErrorCode).ConfigureAwait(false);
                return;
            }
            if (!await HostedServiceLifecycle.DelayAsync(
                    TimeSpan.FromMilliseconds(250), timeProvider, stoppingToken)
                .ConfigureAwait(false))
            {
                logger.LogInformation("Application startup dispatch stopped during bootstrap qualification.");
                return;
            }
        }

        await ReportAsync(
            $"Application bootstrap did not become healthy within {options.BootstrapTimeout}; StartApplication was not submitted.",
            10012).ConfigureAwait(false);
    }

    async Task ReportAsync(string message, int? errorCode)
    {
        if (errorCode.HasValue)
            logger.LogError("{ApplicationStartupMessage}", message);
        else
            logger.LogInformation("{ApplicationStartupMessage}", message);
        try
        {
            if (errorCode.HasValue)
                await statusConsoleWriter.WriteConsoleAsync(LogSourceType.System, errorCode.Value, message)
                    .ConfigureAwait(false);
            else
                await statusConsoleWriter.WriteConsoleAsync(LogSourceType.System, message)
                    .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to publish bootstrap dispatch status to the System Console.");
        }
    }

    static string Bound(string? value)
    {
        const int maximumLength = 512;
        var text = string.IsNullOrWhiteSpace(value) ? "No detail supplied." : value.Trim();
        return text.Length <= maximumLength ? text : text[..maximumLength];
    }
}
