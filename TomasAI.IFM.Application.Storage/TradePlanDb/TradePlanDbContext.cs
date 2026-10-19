using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>
/// Provides read and write persistence operations for Trade Plan projections.
/// </summary>
/// <param name="connectionSettings">The named database connection settings.</param>
/// <param name="logger">The database-provider logger.</param>
public sealed class TradePlanDbContext(
    IDbConnectionSettings connectionSettings,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<TradePlanDbContext>(
        connectionSettings[TradePlanDbConnection],
        logger),
      ITradePlanDbContext
{
    /// <inheritdoc />
    public Task<StrategyTradePlanHistoryPage> GetOffsetHistoryAsync(StrategyPositionId positionId, TradeStrategyKind strategy,
        DateOnly valueDate, int offset, int pageSize, long? maximumRevision = null, CancellationToken cancellationToken = default)
        => this.ReadOffsetHistoryAsync(positionId, strategy, valueDate, offset, pageSize, maximumRevision, cancellationToken);

    internal readonly ILogger<DbProvider> tradePlanLogger = logger;

    /// <inheritdoc />
    public Task ProjectMaterialAsync(IEnlistedPostgresTransaction transaction, StrategyTradePlanSnapshot plan, CancellationToken cancellationToken = default)
        => this.ProjectMaterialAsyncCore(transaction, plan, cancellationToken);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Disposable monitoring snapshots are logged and dropped on any persistence failure, as required; the next market update calculates a new snapshot.")]
    public Task ProjectMaterialAsync(
        StrategyTradePlanSnapshot plan,
        CancellationToken cancellationToken = default)
        => this.ProjectMaterialAsyncCoreBinding(plan, cancellationToken);
    /// <inheritdoc />
    public Task ProjectExitWorkflowAsync(IEnlistedPostgresTransaction transaction, ExitPositionWorkflowProjection workflow, CancellationToken cancellationToken = default)
        => this.ProjectExitWorkflowAsyncCore(transaction, workflow, cancellationToken);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task ProjectExitWorkflowAsync(
        ExitPositionWorkflowProjection workflow,
        CancellationToken cancellationToken = default)
        => this.ProjectExitWorkflowAsyncCoreBinding(workflow, cancellationToken);


    /// <summary>Gets the Trade Plan database connection-setting name.</summary>
    public const string TradePlanDbConnection = "TradePlanDbConnection";

    /// <summary>Gets the concrete Trade Plan database context.</summary>
    public override TradePlanDbContext Database => this;

    /// <summary>Gets the Trade Plan read capability.</summary>
    public ITradePlanDbReadContext DbReader => this;

    /// <summary>Gets the Trade Plan write capability.</summary>
    public ITradePlanDbWriteContext DbWriter => this;

    internal static (T Item, string[] Keys) MapToPageItem<T>(Npgsql.NpgsqlDataReader reader, int count) =>
        (MessagePackBinarySerializer.Shared.Deserialize<T>(reader.GetFieldValue<byte[]>(0)),
         Enumerable.Range(1, count).Select(i => reader.GetValue(i) switch
         {
             DateTime date => date.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
             IFormattable value => value.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
             var value => value.ToString()!
         }).ToArray());

    /// <inheritdoc />
    public Task<StrategyTradePlanSnapshot?> GetCurrentAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategy,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        return this.GetCurrentAsyncCore(positionId, strategy, valueDate, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueryPage<StrategyTradePlanSnapshot>> GetHistoryAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategy,
        DateOnly valueDate,
        int pageSize,
        byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        return this.GetHistoryAsyncCore(positionId, strategy, valueDate, pageSize, pagingState, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueryPage<StrategyTradePlanSnapshot>> GetActivityAsync(
        DateOnly valueDate,
        int pageSize,
        byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        return this.GetActivityAsyncCore(valueDate, pageSize, pagingState, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ExitPositionWorkflowProjection?> GetCurrentExitWorkflowAsync(
        StrategyPositionId positionId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        return this.GetCurrentExitWorkflowAsyncCore(positionId, valueDate, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueryPage<ExitPositionWorkflowProjection>> GetExitWorkflowTimelineAsync(
        StrategyPositionId positionId,
        DateOnly valueDate,
        int pageSize,
        byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        return this.GetExitWorkflowTimelineAsyncCore(positionId, valueDate, pageSize, pagingState, cancellationToken);
    }
    /// <inheritdoc />
    public Task<IReadOnlyList<ExitPositionWorkflowProjection>> ReadExitWorkflowBaselineAsync(IEnlistedPostgresTransaction transaction, CancellationToken cancellationToken = default)
        => this.ReadExitWorkflowBaselineCoreAsync(transaction, cancellationToken);

    internal static ExitPositionWorkflowProjection MapToExitWorkflow(Npgsql.NpgsqlDataReader reader) =>
        MessagePackBinarySerializer.Shared.Deserialize<ExitPositionWorkflowProjection>(reader.GetFieldValue<byte[]>(0));
}
