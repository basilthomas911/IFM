using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Posts one typed StartApplication command after host and actor bootstrap are healthy.</summary>
public sealed class ApplicationStartupCommandDispatcher(
    IHostApplicationLifetime lifetime,
    IActorRuntimeStartupSignal actorRuntimeStartup,
    IApplicationBootstrapReadiness bootstrapReadiness,
    IFuturesMarketSessionAuthority marketSessionAuthority,
    IApplicationCommandApi commandApi,
    IApplicationStartupStatusStore startupStatusStore,
    IApplicationStartupHandoffStatusStore handoffStatusStore,
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
            logger.LogInformation("{Component}.{Method} "+"Application startup dispatch was cancelled by API process shutdown.",nameof(ApplicationStartupCommandDispatcher),nameof(ExecuteAsync));
        }
        catch (Exception exception)
        {
            logger.LogError(exception,"{Component}.{Method} "+"Application startup dispatch failed before command acceptance.",nameof(ApplicationStartupCommandDispatcher),nameof(ExecuteAsync));
            await ReportAsync(
                $"Application startup dispatch failed before command acceptance: {Bound(exception.Message)}",
                10013).ConfigureAwait(false);
        }
    }

    async Task DispatchAfterBootstrapAsync(CancellationToken stoppingToken)
    {
        if (!options.AutoStartAfterBootstrap)
        {
            logger.LogInformation("{Component}.{Method} "+"Automatic Application startup dispatch is disabled.",nameof(ApplicationStartupCommandDispatcher),nameof(DispatchAfterBootstrapAsync));
            return;
        }

        if (!await HostedServiceLifecycle.WaitForSignalAsync(
                lifetime.ApplicationStarted, stoppingToken).ConfigureAwait(false))
        {
            logger.LogInformation("{Component}.{Method} "+"Application startup dispatch stopped before API bootstrap completed.",nameof(ApplicationStartupCommandDispatcher),nameof(DispatchAfterBootstrapAsync));
            return;
        }
        await actorRuntimeStartup.WaitAsync(stoppingToken).ConfigureAwait(false);
        if (!await bootstrapReadiness.IsHealthyAsync(stoppingToken).ConfigureAwait(false))
            throw new InvalidOperationException(
                "Actor runtime completed but required bootstrap health checks are not healthy.");

        var valueDate = marketSessionAuthority.Current.OperationalValueDate;
        await DispatchAndObserveAsync(valueDate, stoppingToken).ConfigureAwait(false);
    }

    async Task DispatchAndObserveAsync(DateOnly valueDate, CancellationToken stoppingToken)
    {
        var acceptedCommands = new Dictionary<Guid, DateTime>();
        for (var attempt = 1; attempt <= options.HandoffMaximumAttempts; attempt++)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                var observationStartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                var result = await commandApi.StartApplicationAsync(valueDate).ConfigureAwait(false);
                if (!result.Success || result.Value == Guid.Empty)
                {
                    var message = result.Success
                        ? "StartApplication command was accepted without a command identity."
                        : $"Application startup command was rejected: {result.ErrorMessage} (error code {result.ErrorCode}).";
                    SetHandoff(new()
                    {
                        State = ApplicationStartupHandoffState.CommandRejected,
                        ValueDate = valueDate,
                        AttemptCount = attempt,
                        LastError = message,
                        Summary = message
                    });
                    await ReportAsync(message, result.Success ? 10016 : result.ErrorCode).ConfigureAwait(false);
                    return;
                }

                var acceptedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                acceptedCommands[result.Value] = observationStartedAtUtc;
                SetHandoff(new()
                {
                    State = ApplicationStartupHandoffState.CommandAccepted,
                    ValueDate = valueDate,
                    CommandId = result.Value,
                    AcceptedAtUtc = acceptedAtUtc,
                    ObservationDeadlineUtc = acceptedAtUtc + options.HandoffObservationTimeout,
                    AttemptCount = attempt,
                    Summary = "Application startup command was accepted; waiting for lifecycle observation."
                });
                await ReportAsync(
                    $"StartApplication command accepted after bootstrap. ValueDate={valueDate:yyyy-MM-dd}; CommandId={result.Value}; Attempt={attempt}.",
                    null).ConfigureAwait(false);
                logger.LogInformation(
                    "{Component}.{Method} "+"Application startup handoff accepted. ValueDate={ValueDate}; CommandId={CommandId}; Attempt={Attempt}.",nameof(ApplicationStartupCommandDispatcher),nameof(DispatchAndObserveAsync),                    valueDate,                    result.Value,                    attempt);

                if (await WaitForLifecycleObservationAsync(
                        valueDate, acceptedCommands, stoppingToken).ConfigureAwait(false) is { } observed)
                {
                    var observedAcceptedAtUtc = acceptedCommands.TryGetValue(observed.CommandId, out var acceptedAt)
                        ? acceptedAt
                        : observed.StartedAtUtc;
                    SetHandoff(new()
                    {
                        State = ApplicationStartupHandoffState.LifecycleObserved,
                        ValueDate = valueDate,
                        CommandId = observed.CommandId,
                        AcceptedAtUtc = observedAcceptedAtUtc,
                        ObservationDeadlineUtc = observedAcceptedAtUtc + options.HandoffObservationTimeout,
                        ObservedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                        AttemptCount = attempt,
                        Summary = $"Application lifecycle observed command {observed.CommandId}."
                    });
                    logger.LogInformation(
                        "{Component}.{Method} "+"Application startup lifecycle observed. ValueDate={ValueDate}; CommandId={CommandId}; Attempt={Attempt}; LifecycleState={LifecycleState}.",nameof(ApplicationStartupCommandDispatcher),nameof(DispatchAndObserveAsync),                        valueDate,                        observed.CommandId,                        attempt,                        observed.State);
                    return;
                }

                var timeoutMessage =
                    $"Application startup was accepted but its lifecycle event was not observed within {options.HandoffObservationTimeout}. "
                    + $"ValueDate={valueDate:yyyy-MM-dd}; CommandId={result.Value}; Attempt={attempt}.";
                SetHandoff(new()
                {
                    State = ApplicationStartupHandoffState.TimedOut,
                    ValueDate = valueDate,
                    CommandId = result.Value,
                    AcceptedAtUtc = acceptedAtUtc,
                    ObservationDeadlineUtc = acceptedAtUtc + options.HandoffObservationTimeout,
                    AttemptCount = attempt,
                    LastError = timeoutMessage,
                    Summary = timeoutMessage
                });
                logger.LogError(
                    "{Component}.{Method} "+"Application startup lifecycle was not observed. ValueDate={ValueDate}; CommandId={CommandId}; Attempt={Attempt}; ObservationTimeout={ObservationTimeout}.",nameof(ApplicationStartupCommandDispatcher),nameof(DispatchAndObserveAsync),                    valueDate,                    result.Value,                    attempt,                    options.HandoffObservationTimeout);
                await ReportAsync(timeoutMessage, 10014).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var message =
                    $"Application startup handoff attempt {attempt} failed before lifecycle observation: {Bound(exception.Message)}";
                SetHandoff(new()
                {
                    State = ApplicationStartupHandoffState.Failed,
                    ValueDate = valueDate,
                    AttemptCount = attempt,
                    LastError = message,
                    Summary = message
                });
                await ReportAsync(message, 10013).ConfigureAwait(false);
            }

            if (attempt < options.HandoffMaximumAttempts
                && !await HostedServiceLifecycle.DelayAsync(
                    options.HandoffRetryDelay, timeProvider, stoppingToken).ConfigureAwait(false))
                return;
        }
    }

    void SetHandoff(ApplicationStartupHandoffStatus status)
    {
        handoffStatusStore.Set(status);
        ApplicationStartupHandoffMetrics.Record(status);
    }

    async Task<ApplicationStartupStatus?> WaitForLifecycleObservationAsync(
        DateOnly valueDate,
        IReadOnlyDictionary<Guid, DateTime> acceptedCommands,
        CancellationToken stoppingToken)
    {
        var deadline = timeProvider.GetUtcNow() + options.HandoffObservationTimeout;
        var earliestAcceptedAtUtc = acceptedCommands.Values.Min();
        var status = startupStatusStore.Current;
        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();
            if (status.State != ApplicationLifecycleState.Bootstrapped
                && status.ValueDate == valueDate
                // A command durably accepted before a process failure can still be delivered after
                // this handoff begins. Its process-local lifecycle is valid if it started after the
                // current handoff; status that predates this handoff remains ineligible.
                && (acceptedCommands.ContainsKey(status.CommandId)
                    || status.StartedAtUtc >= earliestAcceptedAtUtc))
                return status;

            var remaining = deadline - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
                return null;
            try
            {
                status = await startupStatusStore.WaitForChangeAsync(status, stoppingToken)
                    .AsTask()
                    .WaitAsync(remaining, timeProvider, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return null;
            }
        }
    }

    async Task ReportAsync(string message, int? errorCode)
    {
        if (errorCode.HasValue)
            logger.LogError("{Component}.{Method} "+"{ApplicationStartupMessage}",nameof(ApplicationStartupCommandDispatcher),nameof(ReportAsync),message);
        else
            logger.LogInformation("{Component}.{Method} "+"{ApplicationStartupMessage}",nameof(ApplicationStartupCommandDispatcher),nameof(ReportAsync),message);
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
            logger.LogWarning(exception,"{Component}.{Method} "+"Unable to publish bootstrap dispatch status to the System Console.",nameof(ApplicationStartupCommandDispatcher),nameof(ReportAsync));
        }
    }

    static string Bound(string? value)
    {
        const int maximumLength = 512;
        var text = string.IsNullOrWhiteSpace(value) ? "No detail supplied." : value.Trim();
        return text.Length <= maximumLength ? text : text[..maximumLength];
    }
}
