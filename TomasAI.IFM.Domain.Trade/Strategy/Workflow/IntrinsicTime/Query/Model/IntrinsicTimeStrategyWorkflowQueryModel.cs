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
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;
/// <summary>Owns query calculations and projection reads for the Intrinsic Time strategy workflow.</summary>
internal static class IntrinsicTimeStrategyWorkflowQueryModel
{
    internal static async ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowReadModel>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetIntrinsicTimeStrategyWorkflowByIdQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(query.WorkflowId, cancellationToken).ConfigureAwait(false);
        RequireRevision(result?.WorkflowRevision, query.MinimumWorkflowRevision, query.WorkflowId.ToString());
        return new ServiceResult<IntrinsicTimeStrategyWorkflowReadModel>(result!);
    }

    internal static async ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowReadModel[]>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetIntrinsicTimeStrategyWorkflowsByIdsQuery query, CancellationToken cancellationToken)
    {
        if (query.WorkflowIds is null || query.MinimumRevisions is null || query.WorkflowIds.Length == 0 || query.WorkflowIds.Length > 100 || query.WorkflowIds.Length != query.MinimumRevisions.Length)
            throw new ArgumentException("A batch requires 1-100 workflow IDs and one minimum revision per ID.",nameof(query));
        var results = new IntrinsicTimeStrategyWorkflowReadModel[query.WorkflowIds.Length];
        await Parallel.ForEachAsync(Enumerable.Range(0, results.Length), new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken }, async (index, token) =>
        {
            var id = query.WorkflowIds[index];
            var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(id, token).ConfigureAwait(false);
            RequireRevision(result?.WorkflowRevision, query.MinimumRevisions[index], id.ToString());
            results[index] = result ?? throw new KeyNotFoundException($"Workflow {id} was not found.");
        }).ConfigureAwait(false);
        return BoundWorkflowBatch(results);
    }

    internal static ServiceResult<IntrinsicTimeStrategyWorkflowReadModel[]> BoundWorkflowBatch(
        IntrinsicTimeStrategyWorkflowReadModel[] results)
    {
        const int replyBudget = 1000 * 1024;
        var reply = new ServiceResult<IntrinsicTimeStrategyWorkflowReadModel[]>(results);
        if (MessagePackSerializer.Serialize(reply, TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Options).Length <= replyBudget)
            return reply;
        var message = results.Length > 1
            ? "Workflow detail response is too large. Retry with fewer workflow IDs."
            : "This workflow detail exceeds the transport limit. Request an individual workflow stage.";
        return new ServiceFailed<IntrinsicTimeStrategyWorkflowReadModel[]>(413, message);
    }

    internal static async ValueTask<ServiceResult<ActiveIntrinsicTimeStrategyWorkflowReadModel>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetActiveIntrinsicTimeStrategyWorkflowQuery query, CancellationToken cancellationToken)
    {
        ActiveIntrinsicTimeStrategyWorkflowReadModel? result;
        if (!services.ProjectionCache.TryGet(query.WorkflowEntityId, out result))
        {
            result = await services.DbFactory.TradeDb.GetActiveIntrinsicTimeStrategyWorkflowAsync(query.WorkflowEntityId, cancellationToken).ConfigureAwait(false);
            if (result is not null)
                services.ProjectionCache.Set(result);
        }

        RequireRevision(result?.WorkflowRevision, query.MinimumWorkflowRevision, query.WorkflowEntityId);
        return new ServiceResult<ActiveIntrinsicTimeStrategyWorkflowReadModel>(result!);
    }

    internal static async ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowStartAttemptReadModel[]>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(query.WorkflowEntityId, query.BeforeUtc, RequirePageSize(query.PageSize), cancellationToken).ConfigureAwait(false);
        return new ServiceResult<IntrinsicTimeStrategyWorkflowStartAttemptReadModel[]>(result.ToArray());
    }

    internal static async ValueTask<ServiceResult<StrategyWorkflowStageState>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetIntrinsicTimeStrategyWorkflowStageStateQuery query, CancellationToken cancellationToken)
    {
        var projection = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(query.WorkflowId, cancellationToken).ConfigureAwait(false);
        RequireRevision(projection?.WorkflowRevision, query.MinimumWorkflowRevision, query.WorkflowId.ToString());
        if (projection is null)
            throw new KeyNotFoundException($"Workflow {query.WorkflowId} was not found.");
        var state = ProjectedWorkflowView.Decode(projection, cancellationToken);
        var result = query.Stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => state.RegimeDiscovery,
            StrategyWorkflowStage.MarketCondition => state.MarketCondition,
            StrategyWorkflowStage.TradeSelection => state.TradeSelection,
            StrategyWorkflowStage.OrderComposition => state.OrderComposition,
            StrategyWorkflowStage.RiskManagement => state.RiskManagement,
            _ => throw new ArgumentOutOfRangeException(nameof(query),query.Stage,"A concrete stage is required.")};
        return new ServiceResult<StrategyWorkflowStageState>(result);
    }

    internal static async ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowTimelineReadModel[]>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetIntrinsicTimeStrategyWorkflowTimelineQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowTimelineAsync(query.WorkflowId, query.AfterEventId, RequirePageSize(query.PageSize), cancellationToken).ConfigureAwait(false);
        return new ServiceResult<IntrinsicTimeStrategyWorkflowTimelineReadModel[]>(result.ToArray());
    }

    internal static async ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetRecentIntrinsicTimeStrategyWorkflowsQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(query.WorkflowEntityId, query.BeforeUtc, RequirePageSize(query.PageSize), cancellationToken).ConfigureAwait(false);
        return new ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>(result.ToArray());
    }

    internal static async ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryPageReadModel>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetIntrinsicTimeStrategyWorkflowHistoryPageQuery query, CancellationToken cancellationToken)
    {
        RequireHistoryPage(query.Symbol, query.TimePeriod, query.FromUtc, query.ToUtc, query.PageNumber);
        var ordered = await ReadHistoryAsync(services, query.Symbol, query.TimePeriod, query.FromUtc, query.ToUtc, cancellationToken).ConfigureAwait(false);
        var pageSize = RequirePageSize(query.PageSize);
        var offset = checked((query.PageNumber - 1) * pageSize);
        var page = new IntrinsicTimeStrategyWorkflowHistoryPageReadModel(ordered.Skip(offset).Take(pageSize).ToArray(), query.PageNumber, pageSize, ordered.Length);
        return new ServiceResult<IntrinsicTimeStrategyWorkflowHistoryPageReadModel>(page);
    }

    internal static async ValueTask<ServiceResult<IntrinsicTimeStrategyUpdatesGridPage>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetIntrinsicTimeStrategyUpdatesGridQuery query, CancellationToken cancellationToken)
    {
        RequireHistoryPage(query.Symbol, query.TimePeriod, query.FromUtc, query.ToUtc, query.PageNumber);
        var ordered = await ReadHistoryAsync(services, query.Symbol, query.TimePeriod, query.FromUtc, query.ToUtc, cancellationToken).ConfigureAwait(false);
        var pageSize = RequirePageSize(query.PageSize);
        var offset = checked((query.PageNumber - 1) * pageSize);
        var page = ordered.Skip(offset).Take(pageSize).ToArray();
        var rows = new IntrinsicTimeStrategyUpdatesGridRow[page.Length];
        await Parallel.ForEachAsync(Enumerable.Range(0, page.Length), new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken }, async (index, token) =>
        {
            var history = page[index];
            var projection = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(history.WorkflowId, token).ConfigureAwait(false);
            RequireRevision(projection?.WorkflowRevision, history.WorkflowRevision, history.WorkflowId.ToString());
            var view = TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(projection!.StatePayload);
            rows[index] = IntrinsicTimeStrategyUpdatesGridRow.FromView(view);
        }).ConfigureAwait(false);
        return new ServiceResult<IntrinsicTimeStrategyUpdatesGridPage>(new IntrinsicTimeStrategyUpdatesGridPage(rows, query.PageNumber, pageSize, ordered.Length));
    }

    static async Task<IntrinsicTimeStrategyWorkflowHistoryReadModel[]> ReadHistoryAsync(
        IIntrinsicTimeStrategyWorkflowQueryContext services, string requestedSymbol, TimeFrameType timePeriod,
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var symbol = requestedSymbol.Trim().ToUpperInvariant();
        var frameStarts = ResolveHistoryBucketStarts(fromUtc, toUtc, timePeriod);
        var history = new List<IntrinsicTimeStrategyWorkflowHistoryReadModel>();
        foreach (var status in Enum.GetValues<StrategyWorkflowStatus>().Where(static status => status != StrategyWorkflowStatus.None))
        {
            for (var date = DateOnly.FromDateTime(fromUtc); date <= DateOnly.FromDateTime(toUtc); date = date.AddDays(1))
            {
                var candidates = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowsByStatusAsync(status, date, date, 1000, cancellationToken).ConfigureAwait(false);
                history.AddRange(candidates.Where(item => item.StartedAtUtc >= fromUtc && item.StartedAtUtc <= toUtc && MatchesHistoryEntity(item.WorkflowEntityId, symbol, frameStarts, timePeriod)));
            }
        }

        return history.GroupBy(static item => item.WorkflowId).Select(static group => group.OrderByDescending(item => item.WorkflowRevision).First()).OrderByDescending(static item => item.StartedAtUtc).ThenByDescending(static item => item.WorkflowId.Value).ToArray();
    }

    internal static async ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetCompletedIntrinsicTimeStrategyWorkflowsQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowsByStatusAsync(StrategyWorkflowStatus.Completed, query.StartDate, query.EndDate, RequirePageSize(query.PageSize), cancellationToken).ConfigureAwait(false);
        return new ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>(result.ToArray());
    }

    internal static async ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetStoppedIntrinsicTimeStrategyWorkflowsQuery query, CancellationToken cancellationToken)
    {
        var result = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowsByStatusAsync(StrategyWorkflowStatus.Stopped, query.StartDate, query.EndDate, RequirePageSize(query.PageSize), cancellationToken).ConfigureAwait(false);
        return new ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>(result.ToArray());
    }

    internal static async ValueTask<ServiceResult<IntrinsicTimeStrategyWorkflowObservationReadModel>> ExecuteAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetIntrinsicTimeStrategyWorkflowObservationQuery query, CancellationToken cancellationToken)
    {
        var result = await ObserveAsync(services, query, cancellationToken).ConfigureAwait(false);
        return new ServiceResult<IntrinsicTimeStrategyWorkflowObservationReadModel>(result);
    }

    static void RequireHistoryPage(string symbol, TimeFrameType timePeriod, DateTime fromUtc, DateTime toUtc, int pageNumber)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Symbol is required.", nameof(symbol));
        if (timePeriod is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))
            throw new ArgumentOutOfRangeException(nameof(symbol));
        if (fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Workflow history bounds must be UTC.",nameof(symbol));
        if (toUtc < fromUtc)
            throw new ArgumentOutOfRangeException(nameof(symbol));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(symbol));
    }

    internal static bool MatchesHistoryEntity(string workflowEntityId, string symbol, IReadOnlyCollection<DateOnly> frameStarts, TimeFrameType timePeriod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(frameStarts);
        var prefix = $"{IntrinsicTimeStrategyWorkflowDefinition.Id}.";
        if (!workflowEntityId.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var timePeriodSeparator = workflowEntityId.LastIndexOf('.');
        if (timePeriodSeparator <= prefix.Length || !Enum.TryParse<TimeFrameType>(workflowEntityId[(timePeriodSeparator + 1)..], out var entityTimePeriod) || entityTimePeriod != timePeriod)
            return false;
        var valueDateSeparator = workflowEntityId.LastIndexOf('.', timePeriodSeparator - 1);
        if (valueDateSeparator <= prefix.Length || !DateOnly.TryParseExact(workflowEntityId.AsSpan(valueDateSeparator + 1, timePeriodSeparator - valueDateSeparator - 1), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var frameStart) || !frameStarts.Contains(frameStart))
            return false;
        var contractId = workflowEntityId[prefix.Length..valueDateSeparator];
        return IsContractForSymbol(contractId, symbol);
    }

    static bool IsContractForSymbol(string contractId, string symbol)
    {
        if (!contractId.StartsWith(symbol, StringComparison.Ordinal))
            return false;
        var suffix = contractId.AsSpan(symbol.Length);
        if (suffix.Length == 8 && DateOnly.TryParseExact(suffix, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return true;
        return suffix.Length is >= 2 and <= 5 && "FGHJKMNQUVXZ".Contains(suffix[0], StringComparison.Ordinal) && suffix[1..].IndexOfAnyExceptInRange('0', '9') < 0;
    }

    /// <summary>Includes trading-date buckets on either side of UTC dates; exact UTC timestamps still limit results.</summary>
    /// <remarks>Evening futures sessions use the next trading date before UTC midnight. Weekly and monthly bucket rollovers follow that date too.</remarks>
    internal static IReadOnlyList<DateOnly> ResolveHistoryBucketStarts(DateTime fromUtc, DateTime toUtc, TimeFrameType timePeriod)
    {
        var first = DateOnly.FromDateTime(fromUtc);
        var last = DateOnly.FromDateTime(toUtc);
        return ResolveCalendarBucketStarts(first == DateOnly.MinValue ? first : first.AddDays(-1), last == DateOnly.MaxValue ? last : last.AddDays(1), timePeriod);
    }

    internal static IReadOnlyList<DateOnly> ResolveCalendarBucketStarts(DateOnly startValueDate, DateOnly endValueDate, TimeFrameType timePeriod)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(endValueDate, startValueDate);
        var first = ResolveCalendarBucketStart(startValueDate, timePeriod);
        var last = ResolveCalendarBucketStart(endValueDate, timePeriod);
        List<DateOnly> result = [];
        for (var current = first; current <= last; current = timePeriod switch
        {
            TimeFrameType.Daily => current.AddDays(1),
            TimeFrameType.Weekly => current.AddDays(7),
            TimeFrameType.Monthly => current.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(timePeriod))})
            result.Add(current);
        return result;
    }

    static DateOnly ResolveCalendarBucketStart(DateOnly valueDate, TimeFrameType timePeriod) => timePeriod switch
    {
        TimeFrameType.Daily => valueDate,
        TimeFrameType.Weekly => valueDate.AddDays(-(((int)valueDate.DayOfWeek + 6) % 7)),
        TimeFrameType.Monthly => new DateOnly(valueDate.Year, valueDate.Month, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(timePeriod), timePeriod, "Only Daily, Weekly, and Monthly workflow history is supported.")};
    static int RequirePageSize(int pageSize)
    {
        if (pageSize is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be between 1 and 1000.");
        return pageSize;
    }

    static async ValueTask<IntrinsicTimeStrategyWorkflowObservationReadModel> ObserveAsync(IIntrinsicTimeStrategyWorkflowQueryContext services, GetIntrinsicTimeStrategyWorkflowObservationQuery query, CancellationToken cancellationToken)
    {
        var entityText = query.WorkflowEntity.Format();
        var now = services.TimeProvider.GetUtcNow().UtcDateTime;
        IntrinsicTimeStrategyWorkflowView? view;
        try
        {
            view = await ProjectedWorkflowView.ReadCurrentAsync(services.DbFactory.TradeDb.DbReader, entityText, cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException exception)
        {
            return MigrationBlocked(entityText, now, exception.Message);
        }

        if (view is null)
            return new IntrinsicTimeStrategyWorkflowObservationReadModel
            {
                WorkflowEntityId = entityText,
                OperationalStatus = IntrinsicTimeStrategyWorkflowOperationalStatus.NotStarted,
                ObservedAtUtc = now
            };
        var regime = await services.DbFactory.TradeDb.GetRegimeDiscoveryAsync(view.WorkflowId, cancellationToken).ConfigureAwait(false);
        var marketCondition = await services.DbFactory.TradeDb.GetMarketConditionAsync(view.WorkflowId, cancellationToken).ConfigureAwait(false);
        var result = CreateObservation(entityText, view, regime, now, marketCondition);
        var projected = await services.DbFactory.TradeDb.GetMarketConditionAssessmentAsync(view.WorkflowId, cancellationToken).ConfigureAwait(false);
        if (projected is not null)
        {
            var assessment = Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentContracts.ReadResult(projected.Result);
            var accepted = view.MarketCondition.SourceEventId == projected.Id && string.Equals(view.MarketCondition.Result?.PayloadSha256, projected.Result.PayloadSha256, StringComparison.Ordinal);
            result = result with
            {
                MarketAssessment = assessment,
                WorkflowAcceptedMarketAssessment = accepted,
                MarketAssessmentOrphanSuspected = !accepted,
                MarketAssessmentExpired = assessment.Assessment.ValidUntilUtc is { } until && until <= now,
                IsOperationalIssue = result.IsOperationalIssue || !accepted,
                Diagnostic = !accepted ? "MarketAssessmentProjectionNotAccepted" : result.Diagnostic
            };
        }

        if (result.OperationalStatus == IntrinsicTimeStrategyWorkflowOperationalStatus.ExpiredNotClosed && services.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning))
            global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write38(services.Logger, nameof(IntrinsicTimeStrategyWorkflowQueryModel), nameof(ObserveAsync), entityText, view.WorkflowId, view.WorkflowRevision);

        if (result.NotificationLossSuspected && services.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning))
            global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write55(services.Logger, nameof(IntrinsicTimeStrategyWorkflowQueryModel), nameof(ObserveAsync), entityText, view.WorkflowId, regime!.SourceEventId);

        if (result.MarketConditionNotificationLossSuspected && services.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning))
            global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write56(services.Logger, nameof(IntrinsicTimeStrategyWorkflowQueryModel), nameof(ObserveAsync), entityText, view.WorkflowId, marketCondition!.SourceEventId);

        return result;
    }

    internal static IntrinsicTimeStrategyWorkflowObservationReadModel CreateObservation(string entityText, IntrinsicTimeStrategyWorkflowView view, RegimeDiscoveryReadModel? regime, DateTime now, MarketConditionReadModel? marketCondition = null)
    {
        var accepted = regime is not null && regime.WorkflowId == view.WorkflowId && regime.InputWorkflowRevision == view.RegimeDiscovery.InputWorkflowRevision && regime.SourceEventId == view.RegimeDiscovery.SourceEventId;
        var expired = view.Status == WorkflowStrategyMachineStatus.Started && now >= view.ExpiresAtUtc;
        var marketConditionAccepted = marketCondition is not null && marketCondition.WorkflowId == view.WorkflowId && marketCondition.InputWorkflowRevision == view.MarketCondition.InputWorkflowRevision && marketCondition.SourceEventId == view.MarketCondition.SourceEventId;
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
            IsOperationalIssue = expired || notificationLoss || operationalStatus is IntrinsicTimeStrategyWorkflowOperationalStatus.Failed or IntrinsicTimeStrategyWorkflowOperationalStatus.TimedOut,
            RegimeTerminal = regime,
            WorkflowAcceptedRegimeTerminal = accepted,
            NotificationLossSuspected = notificationLoss,
            MarketConditionTerminal = marketCondition,
            WorkflowAcceptedMarketConditionTerminal = marketConditionAccepted,
            MarketConditionNotificationLossSuspected = marketConditionNotificationLoss,
            ObservedAtUtc = now,
            Diagnostic = ((marketConditionNotificationLoss) switch
            {
                true => "MarketConditionTerminalNotAccepted",
                _ => ((regimeNotificationLoss) switch
                {
                    true => "RegimeTerminalNotAccepted",
                    _ => expired ? "WorkflowExpiredNotClosed" : string.Empty
                })})
        };
    }

    internal static IntrinsicTimeStrategyWorkflowOperationalStatus Classify(WorkflowStrategyMachineStatus status, bool expired) => status switch
    {
        WorkflowStrategyMachineStatus.Started when expired => IntrinsicTimeStrategyWorkflowOperationalStatus.ExpiredNotClosed,
        WorkflowStrategyMachineStatus.Started => IntrinsicTimeStrategyWorkflowOperationalStatus.Running,
        WorkflowStrategyMachineStatus.Failed => IntrinsicTimeStrategyWorkflowOperationalStatus.Failed,
        WorkflowStrategyMachineStatus.TimedOut => IntrinsicTimeStrategyWorkflowOperationalStatus.TimedOut,
        WorkflowStrategyMachineStatus.Completed => IntrinsicTimeStrategyWorkflowOperationalStatus.Completed,
        WorkflowStrategyMachineStatus.Cancelled => IntrinsicTimeStrategyWorkflowOperationalStatus.Cancelled,
        _ => IntrinsicTimeStrategyWorkflowOperationalStatus.NotStarted
    };
    internal static IntrinsicTimeStrategyWorkflowObservationReadModel MigrationBlocked(string entityText, DateTime now, string diagnostic) => new()
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
