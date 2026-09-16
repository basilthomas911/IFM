using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;

/// <summary>Owns query calculations and projection reads for the Intrinsic Time strategy workflow.</summary>
internal static class IntrinsicTimeStrategyWorkflowQueryModel
{
    internal static async ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, GetIntrinsicTimeStrategyWorkflowByIdQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb
            .GetIntrinsicTimeStrategyWorkflowAsync(query.WorkflowId, cancellationToken).ConfigureAwait(false);
        RequireRevision(result?.WorkflowRevision, query.MinimumWorkflowRevision, query.WorkflowId.ToString());
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<IntrinsicTimeStrategyWorkflowReadModel>(result!)).ConfigureAwait(false);
    }

    internal static async ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, GetActiveIntrinsicTimeStrategyWorkflowQuery query, CancellationToken cancellationToken)
    {
        ActiveIntrinsicTimeStrategyWorkflowReadModel? result;
        if (!services.ProjectionCache.TryGet(query.WorkflowEntityId, out result))
        {
            result = await services.DbFactory.TradeDb
                .GetActiveIntrinsicTimeStrategyWorkflowAsync(query.WorkflowEntityId, cancellationToken)
                .ConfigureAwait(false);
            if (result is not null)
                services.ProjectionCache.Set(result);
        }
        RequireRevision(result?.WorkflowRevision, query.MinimumWorkflowRevision, query.WorkflowEntityId);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<ActiveIntrinsicTimeStrategyWorkflowReadModel>(result!)).ConfigureAwait(false);
    }

    internal static async ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(
            query.WorkflowEntityId, query.BeforeUtc, RequirePageSize(query.PageSize), cancellationToken)
            .ConfigureAwait(false);
        await ReplyArray(context, query, query.Subject.Verb, result).ConfigureAwait(false);
    }

    internal static async ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, GetIntrinsicTimeStrategyWorkflowStageStateQuery query, CancellationToken cancellationToken)
    {
        var projection = await services.DbFactory.TradeDb
            .GetIntrinsicTimeStrategyWorkflowAsync(query.WorkflowId, cancellationToken).ConfigureAwait(false);
        RequireRevision(projection?.WorkflowRevision, query.MinimumWorkflowRevision, query.WorkflowId.ToString());
        if (projection is null)
            throw new KeyNotFoundException($"Workflow {query.WorkflowId} was not found.");
        var state = MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(projection.StatePayload);
        var result = query.Stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => state.RegimeDiscovery,
            StrategyWorkflowStage.MarketCondition => state.MarketCondition,
            StrategyWorkflowStage.TradeSelection => state.TradeSelection,
            StrategyWorkflowStage.OrderComposition => state.OrderComposition,
            StrategyWorkflowStage.RiskManagement => state.RiskManagement,
            _ => throw new ArgumentOutOfRangeException(nameof(query.Stage), query.Stage, "A concrete stage is required.")
        };
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<StrategyWorkflowStageState>(result)).ConfigureAwait(false);
    }

    internal static async ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, GetIntrinsicTimeStrategyWorkflowTimelineQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowTimelineAsync(
            query.WorkflowId, query.AfterEventId, RequirePageSize(query.PageSize), cancellationToken)
            .ConfigureAwait(false);
        await ReplyArray(context, query, query.Subject.Verb, result).ConfigureAwait(false);
    }

    internal static async ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, GetRecentIntrinsicTimeStrategyWorkflowsQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
            query.WorkflowEntityId, query.BeforeUtc, RequirePageSize(query.PageSize), cancellationToken)
            .ConfigureAwait(false);
        await ReplyArray(context, query, query.Subject.Verb, result).ConfigureAwait(false);
    }

    internal static async ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, GetCompletedIntrinsicTimeStrategyWorkflowsQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
            StrategyWorkflowStatus.Completed, query.StartDate, query.EndDate,
            RequirePageSize(query.PageSize), cancellationToken).ConfigureAwait(false);
        await ReplyArray(context, query, query.Subject.Verb, result).ConfigureAwait(false);
    }

    internal static async ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, GetStoppedIntrinsicTimeStrategyWorkflowsQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
            StrategyWorkflowStatus.Stopped, query.StartDate, query.EndDate,
            RequirePageSize(query.PageSize), cancellationToken).ConfigureAwait(false);
        await ReplyArray(context, query, query.Subject.Verb, result).ConfigureAwait(false);
    }

    internal static async ValueTask ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, GetIntrinsicTimeStrategyWorkflowObservationQuery query, CancellationToken cancellationToken)
    {
        var result = await ObserveAsync(services, query, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<IntrinsicTimeStrategyWorkflowObservationReadModel>(result)).ConfigureAwait(false);
    }


    static async ValueTask ReplyArray<T>(
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        IQuery query,
        string verb,
        ICollection<T> values)
        where T : class
        => await context.ReplyAsync(query.Subject.ThreadId, verb,
            new ServiceResult<T[]>(values.ToArray())).ConfigureAwait(false);

    static int RequirePageSize(int pageSize)
    {
        if (pageSize is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be between 1 and 1000.");
        return pageSize;
    }

    static async ValueTask<IntrinsicTimeStrategyWorkflowObservationReadModel> ObserveAsync(IIntrinsicTimeStrategyWorkflowQueryContext services,
        GetIntrinsicTimeStrategyWorkflowObservationQuery query,
        CancellationToken cancellationToken)
    {
        var entityText = query.WorkflowEntity.Format();
        var now = services.TimeProvider.GetUtcNow().UtcDateTime;
        var load = new ExecuteIntrinsicTimeStrategyWorkflowCommand
        {
            Subject = new ActorSubject(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor,
                ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb, entityText),
            EntityId = query.WorkflowEntity
        };

        IntrinsicTimeStrategyWorkflowView? view;
        try
        {
            view = (await services.StateRepository.LoadStateAsync(load, cancellationToken)
                .ConfigureAwait(false)).CurrentView;
        }
        catch (LegacyWorkflowStreamException exception)
        {
            services.Logger.LogError(exception,
                "Workflow observation is migration-blocked for {WorkflowEntityId} {StreamId}",
                entityText, exception.StreamId);
            return MigrationBlocked(entityText, now, exception.Message);
        }

        if (view is null)
            return new IntrinsicTimeStrategyWorkflowObservationReadModel
            {
                WorkflowEntityId = entityText,
                OperationalStatus = IntrinsicTimeStrategyWorkflowOperationalStatus.NotStarted,
                ObservedAtUtc = now
            };

        var regime = await services.DbFactory.TradeDb
            .GetRegimeDiscoveryAsync(view.WorkflowId, cancellationToken).ConfigureAwait(false);
        var marketCondition = await services.DbFactory.TradeDb
            .GetMarketConditionAsync(view.WorkflowId, cancellationToken).ConfigureAwait(false);
        var result = CreateObservation(entityText, view, regime, now, marketCondition);
        var projected = await services.DbFactory.TradeDb
            .GetMarketConditionAssessmentAsync(view.WorkflowId,cancellationToken).ConfigureAwait(false);
        if (projected is not null)
        {
            var assessment = Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentContracts.ReadResult(projected.Result);
            var accepted = view.MarketCondition.SourceEventId == projected.Id && view.MarketCondition.Result?.PayloadSha256 == projected.Result.PayloadSha256;
            result = result with { MarketAssessment = assessment, WorkflowAcceptedMarketAssessment = accepted, MarketAssessmentOrphanSuspected = !accepted,
                MarketAssessmentExpired = assessment.Assessment.ValidUntilUtc is { } until && until <= now,
                IsOperationalIssue = result.IsOperationalIssue || !accepted, Diagnostic = !accepted ? "MarketAssessmentProjectionNotAccepted" : result.Diagnostic };
        }

        if (result.OperationalStatus == IntrinsicTimeStrategyWorkflowOperationalStatus.ExpiredNotClosed)
            services.Logger.LogWarning(
                "Workflow is expired but not closed for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                entityText, view.WorkflowId, view.WorkflowRevision);
        if (result.NotificationLossSuspected)
            services.Logger.LogWarning(
                "Regime terminal notification was not accepted by workflow {WorkflowEntityId} {WorkflowId} source {SourceEventId}",
                entityText, view.WorkflowId, regime!.SourceEventId);
        if (result.MarketConditionNotificationLossSuspected)
            services.Logger.LogWarning(
                "Market Condition terminal notification was not accepted by workflow {WorkflowEntityId} {WorkflowId} source {SourceEventId}",
                entityText, view.WorkflowId, marketCondition!.SourceEventId);

        return result;
    }

    internal static IntrinsicTimeStrategyWorkflowObservationReadModel CreateObservation(
        string entityText,
        IntrinsicTimeStrategyWorkflowView view,
        RegimeDiscoveryReadModel? regime,
        DateTime now,
        MarketConditionReadModel? marketCondition = null)
    {
        var accepted = regime is not null &&
                       regime.WorkflowId == view.WorkflowId &&
                       regime.InputWorkflowRevision == view.RegimeDiscovery.InputWorkflowRevision &&
                       regime.SourceEventId == view.RegimeDiscovery.SourceEventId;
        var expired = view.Status == WorkflowStrategyMachineStatus.Started && now >= view.ExpiresAtUtc;
        var marketConditionAccepted = marketCondition is not null &&
                                      marketCondition.WorkflowId == view.WorkflowId &&
                                      marketCondition.InputWorkflowRevision == view.MarketCondition.InputWorkflowRevision &&
                                      marketCondition.SourceEventId == view.MarketCondition.SourceEventId;
        var regimeNotificationLoss = expired && regime is not null && !accepted;
        var marketConditionNotificationLoss = marketCondition is not null && !marketConditionAccepted;
        var notificationLoss = regimeNotificationLoss || marketConditionNotificationLoss;
        var operationalStatus = Classify(view.Status, expired);
        return new IntrinsicTimeStrategyWorkflowObservationReadModel
        {
            WorkflowEntityId = entityText,
            WorkflowId = view.WorkflowId,
            CorrelationId = view.CorrelationId,
            MachineStatus = view.Status,
            CurrentStage = view.CurrentStage,
            WorkflowRevision = view.WorkflowRevision,
            StartedAtUtc = view.StartedAtUtc,
            ExpiresAtUtc = view.ExpiresAtUtc,
            TerminalAtUtc = view.TerminalAtUtc,
            StopReasonCode = view.StopReasonCode,
            OperationalStatus = operationalStatus,
            IsOperationalIssue = expired || notificationLoss ||
                                 operationalStatus is IntrinsicTimeStrategyWorkflowOperationalStatus.Failed or
                                     IntrinsicTimeStrategyWorkflowOperationalStatus.TimedOut,
            RegimeTerminal = regime,
            WorkflowAcceptedRegimeTerminal = accepted,
            NotificationLossSuspected = notificationLoss,
            MarketConditionTerminal = marketCondition,
            WorkflowAcceptedMarketConditionTerminal = marketConditionAccepted,
            MarketConditionNotificationLossSuspected = marketConditionNotificationLoss,
            ObservedAtUtc = now,
            Diagnostic = marketConditionNotificationLoss ? "MarketConditionTerminalNotAccepted" :
                regimeNotificationLoss ? "RegimeTerminalNotAccepted" :
                expired ? "WorkflowExpiredNotClosed" : string.Empty
        };
    }

    internal static IntrinsicTimeStrategyWorkflowOperationalStatus Classify(
        WorkflowStrategyMachineStatus status,
        bool expired)
        => status switch
        {
            WorkflowStrategyMachineStatus.Started when expired =>
                IntrinsicTimeStrategyWorkflowOperationalStatus.ExpiredNotClosed,
            WorkflowStrategyMachineStatus.Started => IntrinsicTimeStrategyWorkflowOperationalStatus.Running,
            WorkflowStrategyMachineStatus.Failed => IntrinsicTimeStrategyWorkflowOperationalStatus.Failed,
            WorkflowStrategyMachineStatus.TimedOut => IntrinsicTimeStrategyWorkflowOperationalStatus.TimedOut,
            WorkflowStrategyMachineStatus.Completed => IntrinsicTimeStrategyWorkflowOperationalStatus.Completed,
            WorkflowStrategyMachineStatus.Cancelled => IntrinsicTimeStrategyWorkflowOperationalStatus.Cancelled,
            _ => IntrinsicTimeStrategyWorkflowOperationalStatus.NotStarted
        };

    internal static IntrinsicTimeStrategyWorkflowObservationReadModel MigrationBlocked(
        string entityText,
        DateTime now,
        string diagnostic)
        => new()
        {
            WorkflowEntityId = entityText,
            OperationalStatus = IntrinsicTimeStrategyWorkflowOperationalStatus.MigrationBlocked,
            IsOperationalIssue = true,
            ObservedAtUtc = now,
            Diagnostic = diagnostic
        };

    static void RequireRevision(long? actualRevision, long minimumRevision, string identity)
    {
        if (minimumRevision > 0 && (!actualRevision.HasValue || actualRevision.Value < minimumRevision))
            throw new ProjectionSnapshotNotReadyException(identity, minimumRevision, actualRevision);
    }

}
