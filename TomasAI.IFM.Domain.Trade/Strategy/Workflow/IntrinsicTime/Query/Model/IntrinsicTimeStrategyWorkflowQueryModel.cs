using MessagePack;
using Microsoft.Extensions.Logging;
using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
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

    internal static async ValueTask ExecuteAsync(
        IIntrinsicTimeStrategyWorkflowQueryContext services,
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        GetIntrinsicTimeStrategyWorkflowsByIdsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.WorkflowIds is null || query.MinimumRevisions is null
            || query.WorkflowIds.Length == 0 || query.WorkflowIds.Length > 100
            || query.WorkflowIds.Length != query.MinimumRevisions.Length)
            throw new ArgumentException("A batch requires 1-100 workflow IDs and one minimum revision per ID.");

        var results = new IntrinsicTimeStrategyWorkflowReadModel[query.WorkflowIds.Length];
        await Parallel.ForEachAsync(Enumerable.Range(0, results.Length),
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
            async (index, token) =>
            {
                var id = query.WorkflowIds[index];
                var result = await services.DbFactory.TradeDb
                    .GetIntrinsicTimeStrategyWorkflowAsync(id, token).ConfigureAwait(false);
                RequireRevision(result?.WorkflowRevision, query.MinimumRevisions[index], id.ToString());
                results[index] = result ?? throw new KeyNotFoundException($"Workflow {id} was not found.");
            }).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<IntrinsicTimeStrategyWorkflowReadModel[]>(results)).ConfigureAwait(false);
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

    internal static async ValueTask ExecuteAsync(
        IIntrinsicTimeStrategyWorkflowQueryContext services,
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        GetIntrinsicTimeStrategyWorkflowHistoryPageQuery query,
        CancellationToken cancellationToken)
    {
        RequireHistoryPage(query);
        var symbol = query.Symbol.Trim().ToUpperInvariant();
        var frameStarts = ResolveCalendarBucketStarts(
            DateOnly.FromDateTime(query.FromUtc),
            DateOnly.FromDateTime(query.ToUtc),
            query.TimePeriod);
        var history = new List<IntrinsicTimeStrategyWorkflowHistoryReadModel>();
        foreach (var status in Enum.GetValues<StrategyWorkflowStatus>().Where(static status => status != StrategyWorkflowStatus.None))
        {
            for (var date = DateOnly.FromDateTime(query.FromUtc); date <= DateOnly.FromDateTime(query.ToUtc); date = date.AddDays(1))
            {
                var candidates = await services.DbFactory.TradeDb
                    .GetIntrinsicTimeStrategyWorkflowsByStatusAsync(status, date, date, 1000, cancellationToken)
                    .ConfigureAwait(false);
                history.AddRange(candidates.Where(item =>
                    item.StartedAtUtc >= query.FromUtc &&
                    item.StartedAtUtc <= query.ToUtc &&
                    MatchesHistoryEntity(item.WorkflowEntityId, symbol, frameStarts, query.TimePeriod)));
            }
        }
        var ordered = history
            .GroupBy(static item => item.WorkflowId)
            .Select(static group => group.OrderByDescending(item => item.WorkflowRevision).First())
            .OrderByDescending(static item => item.StartedAtUtc)
            .ThenByDescending(static item => item.WorkflowId.Value)
            .ToArray();
        var pageSize = RequirePageSize(query.PageSize);
        var offset = checked((query.PageNumber - 1) * pageSize);
        var page = new IntrinsicTimeStrategyWorkflowHistoryPageReadModel(
            ordered.Skip(offset).Take(pageSize).ToArray(),
            query.PageNumber, pageSize, ordered.Length);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<IntrinsicTimeStrategyWorkflowHistoryPageReadModel>(page)).ConfigureAwait(false);
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


    static void RequireHistoryPage(GetIntrinsicTimeStrategyWorkflowHistoryPageQuery query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Symbol);
        if (query.TimePeriod is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))
            throw new ArgumentOutOfRangeException(nameof(query.TimePeriod));
        if (query.FromUtc.Kind != DateTimeKind.Utc || query.ToUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Workflow history bounds must be UTC.");
        if (query.ToUtc < query.FromUtc)
            throw new ArgumentOutOfRangeException(nameof(query.ToUtc));
        if (query.PageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(query.PageNumber));
        RequirePageSize(query.PageSize);
    }

    internal static bool MatchesHistoryEntity(
        string workflowEntityId,
        string symbol,
        IReadOnlyCollection<DateOnly> frameStarts,
        TimeFrameType timePeriod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(frameStarts);

        var prefix = $"{IntrinsicTimeStrategyWorkflowDefinition.Id}.";
        if (!workflowEntityId.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var timePeriodSeparator = workflowEntityId.LastIndexOf('.');
        if (timePeriodSeparator <= prefix.Length ||
            !Enum.TryParse<TimeFrameType>(workflowEntityId[(timePeriodSeparator + 1)..], out var entityTimePeriod) ||
            entityTimePeriod != timePeriod)
            return false;

        var valueDateSeparator = workflowEntityId.LastIndexOf('.', timePeriodSeparator - 1);
        if (valueDateSeparator <= prefix.Length ||
            !DateOnly.TryParseExact(
                workflowEntityId.AsSpan(valueDateSeparator + 1, timePeriodSeparator - valueDateSeparator - 1),
                "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var frameStart) ||
            !frameStarts.Contains(frameStart))
            return false;

        var contractId = workflowEntityId[prefix.Length..valueDateSeparator];
        return IsContractForSymbol(contractId, symbol);
    }

    static bool IsContractForSymbol(string contractId, string symbol)
    {
        if (!contractId.StartsWith(symbol, StringComparison.Ordinal))
            return false;

        var suffix = contractId.AsSpan(symbol.Length);
        if (suffix.Length == 8 &&
            DateOnly.TryParseExact(suffix, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return true;

        return suffix.Length is >= 2 and <= 5 &&
               "FGHJKMNQUVXZ".Contains(suffix[0]) &&
               suffix[1..].IndexOfAnyExceptInRange('0', '9') < 0;
    }

    internal static IReadOnlyList<DateOnly> ResolveCalendarBucketStarts(
        DateOnly startValueDate,
        DateOnly endValueDate,
        TimeFrameType timePeriod)
    {
        if (endValueDate < startValueDate)
            throw new ArgumentOutOfRangeException(nameof(endValueDate));

        var first = ResolveCalendarBucketStart(startValueDate, timePeriod);
        var last = ResolveCalendarBucketStart(endValueDate, timePeriod);
        List<DateOnly> result = [];
        for (var current = first; current <= last; current = timePeriod switch
             {
                 TimeFrameType.Daily => current.AddDays(1),
                 TimeFrameType.Weekly => current.AddDays(7),
                 TimeFrameType.Monthly => current.AddMonths(1),
                 _ => throw new ArgumentOutOfRangeException(nameof(timePeriod))
             })
            result.Add(current);
        return result;
    }

    static DateOnly ResolveCalendarBucketStart(DateOnly valueDate, TimeFrameType timePeriod)
        => timePeriod switch
        {
            TimeFrameType.Daily => valueDate,
            TimeFrameType.Weekly => valueDate.AddDays(-(((int)valueDate.DayOfWeek + 6) % 7)),
            TimeFrameType.Monthly => new DateOnly(valueDate.Year, valueDate.Month, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(timePeriod), timePeriod,
                "Only Daily, Weekly, and Monthly workflow history is supported.")
        };

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
