using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Strategy.StrategyCatalog;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>All cache population is outside workflow decisions. Each horizon prepares independently.</summary>
public sealed partial class WorkflowInputPreparationService(IntrinsicTimeStrategyWorkflowOptions options,
    IConfigurationDbContext configuration, IStrategyDeploymentReader deployments,
    WorkflowSelectionConfigurationCache selection, WorkflowPreparedConfigurationCache prepared,
    MarketConditionEventRiskAdapter calendar, WorkflowCompositionInputCache composition, ILogger<WorkflowInputPreparationService> logger,
    TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterRuntimeSnapshot parameterRuntime,
    TimeProvider? timeProvider = null) : BackgroundService
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly System.Collections.Concurrent.ConcurrentDictionary<TimeFrameType, (string Failure, DateTime LastLogged)> failures = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await Task.WhenAll(new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly }
            .Select(horizon => RunHorizonAsync(horizon, stoppingToken))).ConfigureAwait(false);

    async Task RunHorizonAsync(TimeFrameType horizon, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        do
        {
            if (options.Enabled) await PrepareAsync(horizon, stoppingToken).ConfigureAwait(false);
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    async Task PrepareAsync(TimeFrameType horizon, CancellationToken token)
    {
        try
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var selectionRuntime = parameterRuntime.Resolve(IntrinsicTimeStrategyWorkflowDefinition.Id, horizon);
            // An unassigned generic scope retains the authored legacy profile, prepared here rather than in decisions.
            // Explicitly disabled or missing assigned versions must never fall back.
            if (!selectionRuntime.IsConfigured && !prepared.TryAcquireRegime(horizon, now.AddSeconds(5), out _))
            {
                var regimeGeneration = ReadCacheInvalidation.Configuration;
                var regime = await configuration.GetEffectiveRegimeDiscoveryAsync(now, horizon, token).ConfigureAwait(false);
                if (regime is not null) prepared.PublishRegime(horizon, regime, regimeGeneration, now);
            }
            var profile = options.MarketConditionAssessmentProfileId;
            if (!prepared.TryAcquire(profile, horizon, now.AddSeconds(5), out var assessment))
            {
                var generation = ReadCacheInvalidation.Configuration;
                var resolved = await configuration.GetEffectiveMarketConditionAssessmentAsync(now, profile, "ES", horizon).ConfigureAwait(false);
                if (resolved is not null)
                {
                    if (options.LocalDecisionRuntimeEnabled)
                    {
                        var active = selectionRuntime.Applied;
                        var pinned = resolved.ParameterSet.HorizonProfile;
                        prepared.TryAcquireRegime(horizon, now, out var legacy);
                        var profileId = active?.Version.Reference.SetId ?? (!selectionRuntime.IsConfigured ? legacy?.ParameterSet.ParameterSetId : null);
                        var version = active?.Version.Reference.Version ?? (!selectionRuntime.IsConfigured ? legacy?.ParameterSet.Version : null);
                        if (profileId is null || pinned.RegimeProfileId != profileId || pinned.RegimeProfileVersion != version)
                            throw new InvalidOperationException("MC.CONFIG.REGIME_PROFILE_NOT_PREPARED");
                    }
                    prepared.Publish(profile, horizon, resolved, generation, now);
                }
                prepared.TryAcquire(profile, horizon, now, out assessment);
            }
            if (assessment is not null)
            {
                var p = assessment.Parameters;
                await calendar.WarmAsync(new MarketConditionEventRiskConfiguration
                { HighImpactBeforeMinutes = p.HighImpactBeforeMinutes, HighImpactAfterMinutes = p.HighImpactAfterMinutes,
                    RateDecisionBeforeMinutes = p.RateDecisionBeforeMinutes, RateDecisionAfterMinutes = p.RateDecisionAfterMinutes }, now, token).ConfigureAwait(false);
            }

            var assignments = options.Assignments.Where(value => value.Timeframe == horizon).ToArray();
            foreach (var assignment in assignments.Cast<IntrinsicTimeFundAssignment?>().DefaultIfEmpty())
            {
                token.ThrowIfCancellationRequested();
                // This is a configuration template, not an invented market observation or workflow admission.
                var signal = new FuturesItiSignalEntityId(assignment?.InstrumentRoot ?? "ES", DateOnly.FromDateTime(now), horizon);
                var view = new IntrinsicTimeStrategyWorkflowView
                {
                    EntityId = IntrinsicTimeStrategyWorkflowEntityId.Create(signal) with { Assignment = assignment },
                    WorkflowId = StrategyWorkflowId.New(clock), CorrelationId = Guid.NewGuid(), StartedAtUtc = now,
                    ExpiresAtUtc = now.AddDays(1), TriggerEvent = new() { EntityId = signal, CreatedOn = now }
                };
                if (!selection.IsPrepared(configuration, options, view))
                    await selection.ResolveAsync(configuration, deployments, options, view, token).ConfigureAwait(false);
                if (selection.PreparedTemplate(configuration, options, view) is { } template)
                    await composition.PrepareAsync(template, token).ConfigureAwait(false);
            }
            failures.TryRemove(horizon, out _);
        }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var reason = error.GetType().Name + ":" + error.Message;
            if (!failures.TryGetValue(horizon, out var previous) || previous.Failure != reason || now - previous.LastLogged >= TimeSpan.FromSeconds(30))
            {
                failures[horizon] = (reason, now);
                PreparationFailed(logger, horizon, error);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Strategy input preparation unavailable for {Horizon}.")]
    static partial void PreparationFailed(ILogger logger, TimeFrameType horizon, Exception error);
}
