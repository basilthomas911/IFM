using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Actor.Event.Actor;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.Application.Actor.Event;

/// <summary>Application-startup event-family behavior.</summary>
public static class ApplicationStartup
{
    static readonly Guid ProcessBootId = Guid.NewGuid();

    public static async ValueTask ExecuteAsync(
        this ApplicationStartupEvent @event,
        IApplicationEventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        var existing = context.StartupStatusStore.Current;
        if (existing.ValueDate == @event.EntityId.ValueDate
            && existing.State is ApplicationLifecycleState.Running
                or ApplicationLifecycleState.Degraded
                or ApplicationLifecycleState.ScheduledStopped)
        {
            await ReportAsync(
                context.StatusConsoleWriter,
                context.Logger,
                $"Application startup already reconciled for {@event.EntityId.ValueDate:yyyy-MM-dd}; no duplicate side effects were executed.")
                .ConfigureAwait(false);
            await SendTerminalAsync(@event, context, existing.State, existing.Summary)
                .ConfigureAwait(false);
            return;
        }

        var correlationId = @event.Id == Guid.Empty ? Guid.NewGuid() : @event.Id;
        var workflow = new ApplicationStartupContext(
            @event.EntityId.ValueDate,
            ProcessBootId,
            @event.CommandId,
            correlationId);
        var workflowStarted = context.TimeProvider.GetUtcNow().UtcDateTime;
        var results = new List<ApplicationStartupActivityResult>(7);
        context.StartupStatusStore.Set(new()
        {
            State = ApplicationLifecycleState.Starting,
            ValueDate = workflow.ValueDate,
            ProcessBootId = workflow.ProcessBootId,
            CommandId = workflow.CommandId,
            CorrelationId = workflow.CorrelationId,
            StartedAtUtc = workflowStarted,
            Summary = "Application startup activities are executing sequentially."
        });
        await ReportAsync(
            context.StatusConsoleWriter,
            context.Logger,
            $"Application startup began. ValueDate={workflow.ValueDate:yyyy-MM-dd}; CommandId={workflow.CommandId}; CorrelationId={workflow.CorrelationId}.")
            .ConfigureAwait(false);

        foreach (var activity in ApplicationStartupPlan.Activities)
        {
            results.Add(await ExecuteActivityAsync(
                activity.Activity,
                activity.Required,
                activity.Dependencies,
                workflow,
                context,
                ResolveActivity(context.StartupActivities, activity.Activity),
                results,
                cancellationToken));
        }

        var state = ApplicationStartupPlan.Aggregate(results);
        var summary = CreateSummary(state, results);
        var completed = context.TimeProvider.GetUtcNow().UtcDateTime;
        context.StartupStatusStore.Set(new()
        {
            State = state,
            ValueDate = workflow.ValueDate,
            ProcessBootId = workflow.ProcessBootId,
            CommandId = workflow.CommandId,
            CorrelationId = workflow.CorrelationId,
            StartedAtUtc = workflowStarted,
            CompletedAtUtc = completed,
            Activities = [.. results],
            Summary = summary
        });
        await ReportAsync(context.StatusConsoleWriter, context.Logger, summary).ConfigureAwait(false);
        await SendTerminalAsync(@event, context, state, summary).ConfigureAwait(false);
    }

    static async ValueTask<ApplicationStartupActivityResult> ExecuteActivityAsync(
        ApplicationStartupActivity activity,
        bool required,
        IReadOnlyCollection<ApplicationStartupActivity> dependencies,
        ApplicationStartupContext workflow,
        IApplicationEventContext context,
        Func<ApplicationStartupContext, CancellationToken, ValueTask<ApplicationStartupActivityOutcome>> execute,
        IReadOnlyCollection<ApplicationStartupActivityResult> previous,
        CancellationToken cancellationToken)
    {
        var started = context.TimeProvider.GetUtcNow().UtcDateTime;
        var blockedBy = dependencies.FirstOrDefault(dependency => previous.Any(result =>
            result.Activity == dependency && !result.IsSatisfied));
        if (blockedBy != default)
        {
            var skipped = Result(
                activity,
                ApplicationStartupActivityOutcome.SkippedDependency,
                required,
                started,
                context.TimeProvider.GetUtcNow().UtcDateTime,
                0,
                $"Dependency {blockedBy} was not satisfied.");
            await ReportResultAsync(context, workflow, skipped).ConfigureAwait(false);
            return skipped;
        }

        try
        {
            var outcome = await execute(workflow, cancellationToken).ConfigureAwait(false);
            var result = Result(
                activity,
                outcome,
                required,
                started,
                context.TimeProvider.GetUtcNow().UtcDateTime,
                0,
                outcome.ToString());
            await ReportResultAsync(context, workflow, result).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            const int activityFailureCode = 10011;
            context.Logger.LogError(
                exception,
                "Application startup activity {Activity} failed. ValueDate={ValueDate}; CommandId={CommandId}; CorrelationId={CorrelationId}",
                activity,
                workflow.ValueDate,
                workflow.CommandId,
                workflow.CorrelationId);
            var failure = Result(
                activity,
                ApplicationStartupActivityOutcome.Failed,
                required,
                started,
                context.TimeProvider.GetUtcNow().UtcDateTime,
                activityFailureCode,
                Bound(exception.Message));
            await ReportErrorAsync(context.StatusConsoleWriter, context.Logger, failure)
                .ConfigureAwait(false);
            return failure;
        }
    }

    static ApplicationStartupActivityResult Result(
        ApplicationStartupActivity activity,
        ApplicationStartupActivityOutcome outcome,
        bool required,
        DateTime started,
        DateTime completed,
        int errorCode,
        string reason) => new()
        {
            Activity = activity,
            Outcome = outcome,
            Required = required,
            StartedAtUtc = started,
            CompletedAtUtc = completed,
            ErrorCode = errorCode,
            Reason = Bound(reason)
        };

    static Func<ApplicationStartupContext, CancellationToken, ValueTask<ApplicationStartupActivityOutcome>>
        ResolveActivity(IApplicationStartupActivities activities, ApplicationStartupActivity activity) => activity switch
        {
            ApplicationStartupActivity.ResolveAuthority => activities.ResolveAuthorityAsync,
            ApplicationStartupActivity.ReconcileReferenceData => activities.ReconcileReferenceDataAsync,
            ApplicationStartupActivity.ReconcileCurrentContracts => activities.ReconcileCurrentContractsAsync,
            ApplicationStartupActivity.StartMarketData => activities.StartMarketDataAsync,
            ApplicationStartupActivity.WarmHistoricalAnalytics => activities.WarmHistoricalAnalyticsAsync,
            ApplicationStartupActivity.StartRealtimeAnalytics => activities.StartRealtimeAnalyticsAsync,
            ApplicationStartupActivity.QualifyOperationalState => activities.QualifyOperationalStateAsync,
            _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unknown startup activity.")
        };

    static string CreateSummary(
        ApplicationLifecycleState state,
        IReadOnlyCollection<ApplicationStartupActivityResult> results) =>
        $"Application startup {state}. Activities={results.Count}; "
        + $"Satisfied={results.Count(result => result.IsSatisfied)}; "
        + $"Failed={results.Count(result => result.Outcome == ApplicationStartupActivityOutcome.Failed)}; "
        + $"Skipped={results.Count(result => result.Outcome == ApplicationStartupActivityOutcome.SkippedDependency)}.";

    static async ValueTask ReportResultAsync(
        IApplicationEventContext context,
        ApplicationStartupContext workflow,
        ApplicationStartupActivityResult result)
    {
        context.Logger.LogInformation(
            "Application startup activity {Activity} returned {Outcome}. Required={Required}; ValueDate={ValueDate}; CommandId={CommandId}; CorrelationId={CorrelationId}; Reason={Reason}",
            result.Activity,
            result.Outcome,
            result.Required,
            workflow.ValueDate,
            workflow.CommandId,
            workflow.CorrelationId,
            result.Reason);
        await ReportAsync(
            context.StatusConsoleWriter,
            context.Logger,
            $"Application startup: {result.Activity} => {result.Outcome}. {result.Reason}")
            .ConfigureAwait(false);
    }

    static async ValueTask ReportAsync(
        IStatusConsoleWriter writer,
        ILogger logger,
        string message)
    {
        try
        {
            await writer.WriteConsoleAsync(LogSourceType.System, message).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to publish Application lifecycle status to the System Console.");
        }
    }

    static async ValueTask ReportErrorAsync(
        IStatusConsoleWriter writer,
        ILogger logger,
        ApplicationStartupActivityResult result)
    {
        try
        {
            await writer.WriteConsoleAsync(
                LogSourceType.System,
                result.ErrorCode,
                $"Application startup: {result.Activity} => {result.Outcome}. {result.Reason}",
                nameof(ApplicationStartupActivity),
                result.Activity.ToString()).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to publish Application lifecycle failure to the System Console.");
        }
    }

    static async ValueTask SendTerminalAsync(
        ApplicationStartupEvent @event,
        IApplicationEventContext context,
        ApplicationLifecycleState state,
        string summary)
    {
        if (state is ApplicationLifecycleState.Running or ApplicationLifecycleState.ScheduledStopped)
        {
            var completed = (ApplicationStartupCompleteEvent)@event
                .ToCompleteEvent<ApplicationStartupCompleteEvent, ApplicationEntityId>();
            await context.SendAsync<ApplicationStartupCompleteEvent, ApplicationEntityId>(completed)
                .ConfigureAwait(false);
            return;
        }

        if (state == ApplicationLifecycleState.Degraded)
        {
            var degraded = new ApplicationStartupDegradedEvent
            {
                Subject = new ActorSubject(
                    ActorType.Event,
                    ApplicationStartupDegradedEvent.Actor,
                    ApplicationStartupDegradedEvent.Verb,
                    @event.EntityId.Format()),
                EntityId = @event.EntityId,
                Id = @event.Id,
                EventId = @event.EventId,
                CommandId = @event.CommandId,
                AggregateId = @event.AggregateId,
                EventSource = nameof(ApplicationEventActor),
                ReceivedOn = @event.ReceivedOn,
                CreatedOn = context.TimeProvider.GetUtcNow().UtcDateTime,
                CreatedBy = @event.CreatedBy,
                Reason = summary
            };
            await context.SendAsync<ApplicationStartupDegradedEvent, ApplicationEntityId>(degraded)
                .ConfigureAwait(false);
            return;
        }

        var failed = new ApplicationStartupFailEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                ApplicationStartupFailEvent.Actor,
                ApplicationStartupFailEvent.Verb,
                @event.EntityId.Format()),
            EntityId = @event.EntityId,
            Id = @event.Id,
            ErrorDate = context.TimeProvider.GetUtcNow().UtcDateTime,
            EventId = @event.EventId,
            CommandId = @event.CommandId,
            EventSource = nameof(ApplicationEventActor),
            ErrorMessage = summary,
            ErrorType = ErrorType.System,
            ErrorCode = ApplicationStartupEvent.ErrorCode,
            ReceivedOn = @event.ReceivedOn,
            AggregateId = @event.AggregateId
        };
        await context.SendAsync<ApplicationStartupFailEvent, ApplicationEntityId>(failed)
            .ConfigureAwait(false);
    }

    static string Bound(string? value)
    {
        const int maximumLength = 512;
        var text = string.IsNullOrWhiteSpace(value) ? "No detail supplied." : value.Trim();
        return text.Length <= maximumLength ? text : text[..maximumLength];
    }
}
