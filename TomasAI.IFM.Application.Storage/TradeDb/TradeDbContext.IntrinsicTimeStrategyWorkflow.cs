using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public partial class TradeDbContext
{
    /// <inheritdoc />
    public Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(
        StrategyWorkflowId workflowId)
        => GetIntrinsicTimeStrategyWorkflowAsync(workflowId, CancellationToken.None);

    /// <inheritdoc />
    public Task<IntrinsicTimeStrategyWorkflowReadModel?> GetIntrinsicTimeStrategyWorkflowAsync(
        StrategyWorkflowId workflowId,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflow)}", TradeDbCql.GetIntrinsicTimeStrategyWorkflow)
            .SetParameters(new GetIntrinsicTimeStrategyWorkflow(workflowId.Value))
            .ExecuteSingleAsync(MapWorkflow, cancellationToken);

    /// <inheritdoc />
    public Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> GetActiveIntrinsicTimeStrategyWorkflowAsync(
        string workflowEntityId)
        => GetActiveIntrinsicTimeStrategyWorkflowAsync(workflowEntityId, CancellationToken.None);

    /// <inheritdoc />
    public Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> GetActiveIntrinsicTimeStrategyWorkflowAsync(
        string workflowEntityId,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetActiveIntrinsicTimeStrategyWorkflow)}", TradeDbCql.GetActiveIntrinsicTimeStrategyWorkflow)
            .SetParameters(new GetActiveIntrinsicTimeStrategyWorkflow(workflowEntityId))
            .ExecuteSingleAsync(MapActiveWorkflow, cancellationToken);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>> GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(
        string workflowEntityId,
        DateTime beforeUtc,
        int pageSize)
        => GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(
            workflowEntityId,
            beforeUtc,
            pageSize,
            CancellationToken.None);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowStartAttemptReadModel>> GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(
        string workflowEntityId,
        DateTime beforeUtc,
        int pageSize,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowStartAttempts)}", TradeDbCql.GetIntrinsicTimeStrategyWorkflowStartAttempts)
            .SetParameters(new GetIntrinsicTimeStrategyWorkflowStartAttempts(workflowEntityId, beforeUtc, RequirePageSize(pageSize)))
            .ExecuteQueryAsync(MapStartAttempt, cancellationToken);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowTimelineReadModel>> GetIntrinsicTimeStrategyWorkflowTimelineAsync(
        StrategyWorkflowId workflowId,
        long afterEventId,
        int pageSize)
        => GetIntrinsicTimeStrategyWorkflowTimelineAsync(
            workflowId,
            afterEventId,
            pageSize,
            CancellationToken.None);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowTimelineReadModel>> GetIntrinsicTimeStrategyWorkflowTimelineAsync(
        StrategyWorkflowId workflowId,
        long afterEventId,
        int pageSize,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowTimeline)}", TradeDbCql.GetIntrinsicTimeStrategyWorkflowTimeline)
            .SetParameters(new GetIntrinsicTimeStrategyWorkflowTimeline(workflowId.Value, afterEventId, RequirePageSize(pageSize)))
            .ExecuteQueryAsync(MapTimelineEvent, cancellationToken);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
        string workflowEntityId,
        DateTime beforeUtc,
        int pageSize)
        => GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
            workflowEntityId,
            beforeUtc,
            pageSize,
            CancellationToken.None);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
        string workflowEntityId,
        DateTime beforeUtc,
        int pageSize,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByEntity)}", TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByEntity)
            .SetParameters(new GetIntrinsicTimeStrategyWorkflowsByEntity(workflowEntityId, beforeUtc, RequirePageSize(pageSize)))
            .ExecuteQueryAsync(MapWorkflowHistory, cancellationToken);

    /// <inheritdoc />
    public Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
        StrategyWorkflowStatus status,
        DateOnly startDate,
        DateOnly endDate,
        int pageSize)
        => GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
            status,
            startDate,
            endDate,
            pageSize,
            CancellationToken.None);

    /// <inheritdoc />
    public async Task<ICollection<IntrinsicTimeStrategyWorkflowHistoryReadModel>> GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
        StrategyWorkflowStatus status,
        DateOnly startDate,
        DateOnly endDate,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (endDate < startDate)
            throw new ArgumentOutOfRangeException(nameof(endDate), endDate, "End date must not precede start date.");

        var maximumCount = RequirePageSize(pageSize);
        var results = new List<IntrinsicTimeStrategyWorkflowHistoryReadModel>(maximumCount);
        for (var date = endDate; results.Count < maximumCount; date = date.AddDays(-1))
        {
            var remaining = maximumCount - results.Count;
            var dayResults = await _dbFactory.TradeDb
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByStatusDay)}", TradeDbCql.GetIntrinsicTimeStrategyWorkflowsByStatusDay)
                .SetParameters(new GetIntrinsicTimeStrategyWorkflowsByStatusDay(status.ToString(), date, remaining))
                .ExecuteQueryAsync(MapWorkflowHistory, cancellationToken)
                .ConfigureAwait(false);
            results.AddRange(dayResults);
            if (date == startDate)
                break;
        }

        return results;
    }

    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowAsync(IntrinsicTimeStrategyWorkflowReadModel workflow)
        => UpsertIntrinsicTimeStrategyWorkflowAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public async Task UpsertIntrinsicTimeStrategyWorkflowAsync(
        IntrinsicTimeStrategyWorkflowReadModel workflow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflow)}", TradeDbCql.UpsertIntrinsicTimeStrategyWorkflow)
            .SetParameters(new UpsertIntrinsicTimeStrategyWorkflow(
                workflow.WorkflowId.Value,
                workflow.WorkflowEntityId,
                workflow.WorkflowDefinitionId,
                workflow.WorkflowDefinitionVersion,
                workflow.ContractId,
                workflow.TimeFrameStartValueDate,
                workflow.TimePeriod.ToString(),
                workflow.TriggerEventId,
                workflow.CorrelationId,
                workflow.Status.ToString(),
                workflow.Outcome.ToString(),
                workflow.CurrentStage.ToString(),
                workflow.WorkflowRevision,
                workflow.LastEventId,
                workflow.StateSchemaVersion,
                workflow.StatePayload.ToArray(),
                workflow.StopReasonCode,
                workflow.StartedAtUtc,
                workflow.TerminalAtUtc,
                workflow.UpdatedAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow)
        => UpsertActiveIntrinsicTimeStrategyWorkflowAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public async Task UpsertActiveIntrinsicTimeStrategyWorkflowAsync(
        ActiveIntrinsicTimeStrategyWorkflowReadModel workflow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertActiveIntrinsicTimeStrategyWorkflow)}", TradeDbCql.UpsertActiveIntrinsicTimeStrategyWorkflow)
            .SetParameters(new UpsertActiveIntrinsicTimeStrategyWorkflow(
                workflow.WorkflowEntityId,
                workflow.WorkflowId.Value,
                workflow.ContractId,
                workflow.TimeFrameStartValueDate,
                workflow.TimePeriod.ToString(),
                workflow.CurrentStage.ToString(),
                workflow.WorkflowRevision,
                workflow.LastEventId,
                workflow.StateSchemaVersion,
                workflow.StatePayload.ToArray(),
                workflow.StartedAtUtc,
                workflow.UpdatedAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DeleteActiveIntrinsicTimeStrategyWorkflowAsync(string workflowEntityId)
        => DeleteActiveIntrinsicTimeStrategyWorkflowAsync(workflowEntityId, CancellationToken.None);

    /// <inheritdoc />
    public async Task DeleteActiveIntrinsicTimeStrategyWorkflowAsync(
        string workflowEntityId,
        CancellationToken cancellationToken)
    {
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteActiveIntrinsicTimeStrategyWorkflow)}", TradeDbCql.DeleteActiveIntrinsicTimeStrategyWorkflow)
            .SetParameters(new DeleteActiveIntrinsicTimeStrategyWorkflow(workflowEntityId))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(
        IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt)
        => InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(attempt, CancellationToken.None);

    /// <inheritdoc />
    public async Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(
        IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertIntrinsicTimeStrategyWorkflowStartAttempt)}", TradeDbCql.InsertIntrinsicTimeStrategyWorkflowStartAttempt)
            .SetParameters(new InsertIntrinsicTimeStrategyWorkflowStartAttempt(
                attempt.WorkflowEntityId,
                attempt.RequestedAtUtc,
                attempt.RequestedWorkflowId.Value,
                attempt.Decision.ToString(),
                attempt.ActiveWorkflowId?.Value,
                attempt.StartCommandId,
                attempt.TriggerEventId,
                attempt.ActiveStage.ToString(),
                attempt.ReasonCode,
                attempt.SourceEventId))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(
        IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent)
        => InsertIntrinsicTimeStrategyWorkflowTimelineAsync(timelineEvent, CancellationToken.None);

    /// <inheritdoc />
    public async Task InsertIntrinsicTimeStrategyWorkflowTimelineAsync(
        IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timelineEvent);
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertIntrinsicTimeStrategyWorkflowTimeline)}", TradeDbCql.InsertIntrinsicTimeStrategyWorkflowTimeline)
            .SetParameters(new InsertIntrinsicTimeStrategyWorkflowTimeline(
                timelineEvent.WorkflowId.Value,
                timelineEvent.EventId,
                timelineEvent.WorkflowEntityId,
                timelineEvent.WorkflowRevision,
                timelineEvent.Stage.ToString(),
                timelineEvent.EventName,
                timelineEvent.EventSchemaVersion,
                timelineEvent.EventPayload.ToArray(),
                timelineEvent.OccurredAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow)
        => UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public async Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByEntity)}", TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByEntity)
            .SetParameters(ToByEntityParameters(workflow))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow)
        => UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(workflow, CancellationToken.None);

    /// <inheritdoc />
    public async Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByStatusDay)}", TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByStatusDay)
            .SetParameters(new UpsertIntrinsicTimeStrategyWorkflowByStatusDay(
                workflow.Status.ToString(),
                DateOnly.FromDateTime(workflow.StartedAtUtc),
                workflow.StartedAtUtc,
                workflow.WorkflowId.Value,
                workflow.WorkflowEntityId,
                workflow.Outcome.ToString(),
                workflow.CurrentStage.ToString(),
                workflow.WorkflowRevision,
                workflow.TerminalAtUtc,
                workflow.StopReasonCode))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    static int RequirePageSize(int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        return pageSize;
    }

    static UpsertIntrinsicTimeStrategyWorkflowByEntity ToByEntityParameters(
        IntrinsicTimeStrategyWorkflowHistoryReadModel workflow)
        => new(
            workflow.WorkflowEntityId,
            workflow.StartedAtUtc,
            workflow.WorkflowId.Value,
            workflow.Status.ToString(),
            workflow.Outcome.ToString(),
            workflow.CurrentStage.ToString(),
            workflow.WorkflowRevision,
            workflow.TerminalAtUtc,
            workflow.StopReasonCode);

    static IntrinsicTimeStrategyWorkflowReadModel MapWorkflow(IObjectDataRecord row)
        => new(
            new StrategyWorkflowId(row.GetGuid(0)),
            row.GetString(1),
            row.GetString(2),
            row.GetInt(3),
            row.GetString(4),
            row.GetDateOnly(5),
            row.GetEnum<TimeFrameType>(6),
            row.GetGuid(7),
            row.GetGuid(8),
            row.GetEnum<StrategyWorkflowStatus>(9),
            row.GetEnum<StrategyWorkflowOutcome>(10),
            row.GetEnum<StrategyWorkflowStage>(11),
            row.GetLong(12),
            row.GetLong(13),
            row.GetInt(14),
            row.GetBytes(15),
            row.GetString(16),
            row.GetDateTime(17),
            row.IsNull(18) ? null : row.GetDateTime(18),
            row.GetDateTime(19));

    static ActiveIntrinsicTimeStrategyWorkflowReadModel MapActiveWorkflow(IObjectDataRecord row)
        => new(
            row.GetString(0),
            new StrategyWorkflowId(row.GetGuid(1)),
            row.GetString(2),
            row.GetDateOnly(3),
            row.GetEnum<TimeFrameType>(4),
            row.GetEnum<StrategyWorkflowStage>(5),
            row.GetLong(6),
            row.GetLong(7),
            row.GetInt(8),
            row.GetBytes(9),
            row.GetDateTime(10),
            row.GetDateTime(11));

    static IntrinsicTimeStrategyWorkflowStartAttemptReadModel MapStartAttempt(IObjectDataRecord row)
        => new(
            row.GetString(0),
            row.GetDateTime(1),
            new StrategyWorkflowId(row.GetGuid(2)),
            row.GetEnum<StrategyWorkflowStartDecision>(3),
            row.IsNull(4) ? null : new StrategyWorkflowId(row.GetGuid(4)),
            row.GetGuid(5),
            row.GetGuid(6),
            row.GetEnum<StrategyWorkflowStage>(7),
            row.GetString(8),
            row.GetLong(9));

    static IntrinsicTimeStrategyWorkflowTimelineReadModel MapTimelineEvent(IObjectDataRecord row)
        => new(
            new StrategyWorkflowId(row.GetGuid(0)),
            row.GetLong(1),
            row.GetString(2),
            row.GetLong(3),
            row.GetEnum<StrategyWorkflowStage>(4),
            row.GetString(5),
            row.GetInt(6),
            row.GetBytes(7),
            row.GetDateTime(8));

    static IntrinsicTimeStrategyWorkflowHistoryReadModel MapWorkflowHistory(IObjectDataRecord row)
        => new(
            row.GetString(0),
            row.GetDateTime(1),
            new StrategyWorkflowId(row.GetGuid(2)),
            row.GetEnum<StrategyWorkflowStatus>(3),
            row.GetEnum<StrategyWorkflowOutcome>(4),
            row.GetEnum<StrategyWorkflowStage>(5),
            row.GetLong(6),
            row.IsNull(7) ? null : row.GetDateTime(7),
            row.GetString(8));
}
