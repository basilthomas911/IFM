using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Receipt-based committed history projection, including events whose transactions commit after a newer sequence.</summary>
public sealed class RiskHistoryJournal(IPostgresEventTransaction transactions)
{
    public const string HistoryProjection = "risk-history-v1";
    public const string FundOutcomeProjection = "risk-fund-outcomes-v1";


    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<long> LoadCursorAsync(CancellationToken token = default, string projectionName = HistoryProjection)
        => transactions.ExecuteAsync(async (db, ct) =>
        {
            await (db.ExecuteAsync(Schema.EventSourceSchemaSql.CreateRiskHistoryJournal, [], ct))
            .ConfigureAwait(false);
            var value = await (db.ScalarAsync(EventSourceDbSql.RiskHistoryJournal_LoadCursorAsync_1, [projectionName], ct))
            .ConfigureAwait(false);
            var cursor = value is null ? 0 : Convert.ToInt64(value,System.Globalization.CultureInfo.InvariantCulture);
            // Compatible migration: the old cursor proves these events were previously projected.
            await (db.ExecuteAsync(EventSourceDbSql.RiskHistoryJournal_LoadCursorAsync_2, [projectionName, cursor], ct))
            .ConfigureAwait(false);
            return cursor;
        }, token);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task SaveCursorAsync(long after, CancellationToken token = default, string projectionName = HistoryProjection)
        => transactions.ExecuteAsync(async (db, ct) =>
        {
            await (db.ExecuteAsync(EventSourceDbSql.RiskHistoryJournal_SaveCursorAsync_1, [after, projectionName], ct))
            .ConfigureAwait(false);
            return true;
        }, token);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<IReadOnlyList<WorkflowStrategyStateUpdatedEvent>> PageAsync(long after, CancellationToken token = default,
        string projectionName = HistoryProjection)
        => transactions.ExecuteAsync(async (db, ct) =>
        {
            var rows = await (db.QueryAsync(EventSourceDbSql.RiskHistoryJournal_PageAsync_1, [projectionName], Read, ct))
            .ConfigureAwait(false);
            return (IReadOnlyList<WorkflowStrategyStateUpdatedEvent>)rows.Select(x => (WorkflowStrategyStateUpdatedEvent)x.ToDomainEvent()).ToArray();
        }, token);

    public Task AcknowledgeAsync(string projectionName, long eventId, CancellationToken token = default)
        => transactions.ExecuteAsync(async (db, ct) =>
        {
            await (db.ExecuteAsync(EventSourceDbSql.RiskHistoryJournal_AcknowledgeAsync_1, [projectionName, eventId], ct))
            .ConfigureAwait(false);
            return true;
        }, token);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The parameter is captured by the transaction callback from the enclosing public method.")]
    public Task QuarantineConflictAsync(long eventId, RiskHistoryProjectionResult result, CancellationToken token = default)
        => transactions.ExecuteAsync(async (db, ct) =>
        {
            if (result.Disposition != RiskHistoryProjectionDisposition.Conflict || result.WorkflowId == Guid.Empty || result.InvocationId == Guid.Empty)
                throw new ArgumentException("A complete Risk history conflict result is required.", nameof(result));
            await (db.ExecuteAsync(EventSourceDbSql.RiskHistoryJournal_QuarantineConflictAsync_1, [HistoryProjection, eventId, result.WorkflowId, result.InvocationId, result.Revision, result.StoredHash!, result.IncomingHash!], ct))
            .ConfigureAwait(false);
            await (db.ExecuteAsync(
                EventSourceDbSql.RiskHistoryJournal_AcknowledgeAsync_1,
                [HistoryProjection, eventId], ct))
            .ConfigureAwait(false);
            return true;
        }, token);

    public Task<int> ProjectCompletionPageAsync(CancellationToken token = default)
        => transactions.ExecuteAsync(async (db, ct) =>
        {
            const string projection = "risk-completion-v1";
            var rows = await db.QueryAsync(EventSourceDbSql.RiskHistoryJournal_ProjectCompletionPageAsync_1, [nameof(RiskManagementFunctionCompletedEvent), projection], Read, ct)
            .ConfigureAwait(false);
            foreach (var row in rows)
            {
                var completed = (RiskManagementFunctionCompletedEvent)row.ToDomainEvent();
                if (completed.CommandId != row.CommandId)
                    throw new InvalidDataException("Risk completion does not match its committed command identity.");
                await RiskCompletionProjection.WriteAsync(db, completed, ct)
            .ConfigureAwait(false);
                await db.ExecuteAsync(
                    EventSourceDbSql.RiskHistoryJournal_AcknowledgeAsync_1,
                    [projection, completed.EventId], ct)
            .ConfigureAwait(false);
            }

            return rows.Count;
        }, token);

    static EventLogReadModel Read(System.Data.Common.DbDataReader r) => new(r.GetInt64(0), r.GetString(1), r.GetString(2),
        r.GetInt64(3), r.GetFieldValue<byte[]>(4), r.GetGuid(5), r.GetString(6), r.GetInt64(7));
}
