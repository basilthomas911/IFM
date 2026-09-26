using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Subscriptions;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

/// <summary>Provides PostgreSQL persistence for current Databento contracts and ordered watchdog history.</summary>
/// <param name="settings">The named database connection settings.</param>
/// <param name="factory">The factory used to access related database contexts.</param>
/// <param name="sequenceIds">The sequence service used to allocate watchdog identifiers.</param>
/// <param name="logger">The database-provider logger.</param>
public sealed class MarketDataServiceDbContext(
    IDbConnectionSettings settings,
    IDbContextFactory factory,
    ISequenceIdDbContext sequenceIds,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<MarketDataServiceDbContext>(settings[MarketDataServiceDbConnection], logger),
      IMarketDataServiceDbContext
{
    internal readonly ISequenceIdDbContext _sequenceIds = sequenceIds;
    internal readonly IDurableSubscriptionIntentStore _durableIntent =
        new MarketDataServiceDurableSubscriptionStore(settings, logger);

    internal MarketDataServiceDbContext(
        IDbConnectionSettings settings,
        IDbContextFactory factory,
        ISequenceIdDbContext sequenceIds,
        ILogger<DbProvider> logger,
        TimeProvider? timeProvider,
        Action<DurableStoreWriteStage>? writeObserver)
        : this(settings, factory, sequenceIds, logger)
        => _durableIntent = new MarketDataServiceDurableSubscriptionStore(settings, logger, timeProvider, writeObserver);

    /// <summary>Gets the Market Data Service database connection name.</summary>
    public const string MarketDataServiceDbConnection = "MarketDataServiceDbConnection";

    /// <summary>Gets the concrete Market Data Service database context.</summary>
    public override MarketDataServiceDbContext Database => this;

    /// <summary>Gets the Market Data Service database read capability.</summary>
    public IMarketDataServiceDbReadContext DbReader => this;

    /// <summary>Gets the Market Data Service database write capability.</summary>
    public IMarketDataServiceDbWriteContext DbWriter => this;

    /// <summary>Gets the assignment for a Databento contract role.</summary>
    /// <param name="role">The contract role.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The matching assignment, or <see langword="null"/> when none exists.</returns>
    public Task<FuturesRolloverContractAssignment?> GetAssignmentAsync(
        DatabentoContractRole role,
        CancellationToken cancellationToken = default)
        => Database
            .Use("MarketDataService.GetAssignment", MarketDataServiceDbSql.GetAssignment)
            .SetParameters(new RoleParameter(role))
            .ExecuteSingleAsync<FuturesRolloverContractAssignment?>(MapToAssignment, cancellationToken);

    /// <summary>Gets all current Databento contract assignments.</summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The ordered current assignments.</returns>
    public async Task<IReadOnlyList<FuturesRolloverContractAssignment>> ListAssignmentsAsync(
        CancellationToken cancellationToken = default)
        => [.. await Database
            .Use("MarketDataService.ListAssignments", MarketDataServiceDbSql.ListAssignments)
            .ExecuteQueryAsync(MapToAssignment, cancellationToken)
            .ConfigureAwait(false)];

    /// <summary>Inserts or updates a Databento contract assignment.</summary>
    /// <param name="assignment">The assignment to persist.</param>
    /// <param name="expectedRowVersion">The optimistic-concurrency row version.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The persisted assignment.</returns>
    public async Task<FuturesRolloverContractAssignment> UpsertAssignmentAsync(
        FuturesRolloverContractAssignment assignment,
        long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        var sql = expectedRowVersion == 0
            ? MarketDataServiceDbSql.InsertAssignment
            : MarketDataServiceDbSql.UpdateAssignment;
        var saved = await Database
            .Use("MarketDataService.UpsertAssignment", sql)
            .SetParameters(new AssignmentParameter(assignment, expectedRowVersion))
            .ExecuteSingleAsync<FuturesRolloverContractAssignment?>(MapToAssignment, cancellationToken)
            .ConfigureAwait(false);
        return saved ?? throw new InvalidOperationException("The assignment was concurrently changed or already exists.");
    }

    /// <summary>Deletes a Databento contract assignment.</summary>
    /// <param name="role">The contract role to delete.</param>
    /// <param name="expectedRowVersion">The optimistic-concurrency row version.</param>
    /// <param name="deletedBy">The identity requesting the deletion.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    public async Task DeleteAssignmentAsync(
        DatabentoContractRole role,
        long expectedRowVersion,
        string deletedBy,
        CancellationToken cancellationToken = default)
    {
        var count = await Database
            .Use("MarketDataService.DeleteAssignment", MarketDataServiceDbSql.DeleteAssignment)
            .SetParameters(new DeleteParameter(role, expectedRowVersion))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        if (count.Sum() != 1)
            throw new InvalidOperationException("The assignment was concurrently changed or does not exist.");
    }

    /// <summary>Atomically replaces the front- and second-month VX assignments.</summary>
    /// <param name="front">The front-month assignment.</param>
    /// <param name="second">The second-month assignment.</param>
    /// <param name="expectedFrontVersion">The expected front-month row version.</param>
    /// <param name="expectedSecondVersion">The expected second-month row version.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The persisted front- and second-month assignments.</returns>
    public async Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReplaceVxAssignmentsAsync(
        FuturesRolloverContractAssignment front,
        FuturesRolloverContractAssignment second,
        long expectedFrontVersion,
        long expectedSecondVersion,
        CancellationToken cancellationToken = default)
    {
        var transaction = BeginTransaction();
        try
        {
            if (expectedFrontVersion != 0 || expectedSecondVersion != 0)
            {
                var deleteParameters = new VxPairDeleteParameter(
                    front.ContractRole,
                    expectedFrontVersion,
                    second.ContractRole,
                    expectedSecondVersion);
                var deleted = await Database
                    .Use("MarketDataService.DeleteVxAssignments", MarketDataServiceDbSql.DeleteVxPair)
                    .SetParameters(deleteParameters)
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (deleted.Sum() != 2)
                    throw new InvalidOperationException("A VX assignment was concurrently changed; neither role was committed.");
            }

            var insertParameters = new VxPairParameter(front, expectedFrontVersion, second, expectedSecondVersion);
            var inserted = await Database
                .Use("MarketDataService.InsertVxAssignments", MarketDataServiceDbSql.InsertVxPair)
                .SetParameters(insertParameters)
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            if (inserted.Sum() != 2)
                throw new InvalidOperationException("The complete VX assignment pair was not stored; neither role was committed.");
            transaction?.Commit();
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }

        var savedFront = await GetAssignmentAsync(DatabentoContractRole.VxFrontMonth, cancellationToken)
            .ConfigureAwait(false);
        var savedSecond = await GetAssignmentAsync(DatabentoContractRole.VxSecondMonth, cancellationToken)
            .ConfigureAwait(false);
        return [savedFront!, savedSecond!];
    }

    /// <summary>Appends a watchdog observation idempotently.</summary>
    /// <param name="observation">The observation to append.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The persisted observation.</returns>
    public async Task<DatabentoWatchdogObservation> AppendObservationAsync(
        DatabentoWatchdogObservation observation,
        CancellationToken cancellationToken = default)
    {
        var existing = await this.GetObservationByIdentityAsync(observation.ObservationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
            return existing;
        var id = await _sequenceIds
            .GetNextSequenceIdAsync(SequenceName.MarketDataService_WatchdogStatusLogId, cancellationToken)
            .ConfigureAwait(false);
        var saved = observation with { WatchdogStatusLogId = id, RowVersion = 1 };
        _ = await Database
            .Use("MarketDataService.InsertObservation", MarketDataServiceDbSql.InsertObservation)
            .SetParameters(new ObservationParameter(saved))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        return await this.GetObservationByIdentityAsync(observation.ObservationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The watchdog observation was not persisted.");
    }

    /// <summary>Gets a watchdog observation by its storage identifier.</summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The matching observation, or <see langword="null"/> when none exists.</returns>
    public Task<DatabentoWatchdogObservation?> GetObservationAsync(
        long id,
        CancellationToken cancellationToken = default)
        => Database
            .Use("MarketDataService.GetObservation", MarketDataServiceDbSql.GetObservation)
            .SetParameters(new IdParameter(id))
            .ExecuteSingleAsync<DatabentoWatchdogObservation?>(MapToObservation, cancellationToken);

    /// <summary>Updates a watchdog observation.</summary>
    /// <param name="observation">The observation to update.</param>
    /// <param name="expectedRowVersion">The optimistic-concurrency row version.</param>
    /// <param name="changedBy">The identity applying the update.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The persisted observation.</returns>
    public async Task<DatabentoWatchdogObservation> UpdateObservationAsync(
        DatabentoWatchdogObservation observation,
        long expectedRowVersion,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var parameters = new ObservationUpdateParameter(observation, expectedRowVersion, changedBy);
        var saved = await Database
            .Use("MarketDataService.UpdateObservation", MarketDataServiceDbSql.UpdateObservation)
            .SetParameters(parameters)
            .ExecuteSingleAsync<DatabentoWatchdogObservation?>(MapToObservation, cancellationToken)
            .ConfigureAwait(false);
        return saved ?? throw new InvalidOperationException("The watchdog observation was concurrently changed or does not exist.");
    }

    /// <summary>Deletes a watchdog observation.</summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="expectedRowVersion">The optimistic-concurrency row version.</param>
    /// <param name="deletedBy">The identity requesting the deletion.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    public async Task DeleteObservationAsync(
        long id,
        long expectedRowVersion,
        string deletedBy,
        CancellationToken cancellationToken = default)
    {
        var count = await Database
            .Use("MarketDataService.DeleteObservation", MarketDataServiceDbSql.DeleteObservation)
            .SetParameters(new ObservationDeleteParameter(id, expectedRowVersion))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        if (count.Sum() != 1)
            throw new InvalidOperationException("The watchdog observation was concurrently changed or does not exist.");
    }

    /// <summary>Gets watchdog observations matching the supplied filters.</summary>
    /// <param name="valueDate">The optional value-date filter.</param>
    /// <param name="status">The optional major-status filter.</param>
    /// <param name="pageSize">The maximum number of observations to return.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The matching observations.</returns>
    public async Task<IReadOnlyList<DatabentoWatchdogObservation>> ListObservationsAsync(
        DateOnly? valueDate = null,
        DatabentoMajorStatus? status = null,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => [.. await Database
            .Use("MarketDataService.ListObservations", MarketDataServiceDbSql.ListObservations)
            .SetParameters(new ObservationListParameter(valueDate, status, pageSize))
            .ExecuteQueryAsync(MapToObservation, cancellationToken)
            .ConfigureAwait(false)];

    /// <summary>Persists a dataset incident transition idempotently.</summary>
    /// <param name="transition">The incident transition to persist.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The transition with its current row version.</returns>
    public async Task<DatasetIncidentTransition> PersistDatasetIncidentAsync(
        DatasetIncidentTransition transition,
        CancellationToken cancellationToken = default)
    {
        var version = await Database
            .Use("MarketDataService.PersistDatasetIncident", MarketDataServiceDbSql.PersistDatasetIncident)
            .SetParameters(new IncidentParameter(transition))
            .ExecuteScalarAsync(static row => row.GetLong(0), cancellationToken)
            .ConfigureAwait(false);
        return transition with { RowVersion = version == 0 ? transition.RowVersion : version };
    }

    /// <summary>Gets all currently open dataset incidents.</summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The open dataset incident transitions.</returns>
    public async Task<IReadOnlyList<DatasetIncidentTransition>> ListOpenDatasetIncidentsAsync(
        CancellationToken cancellationToken = default)
        => [.. await Database
            .Use("MarketDataService.ListOpenDatasetIncidents", MarketDataServiceDbSql.ListOpenDatasetIncidents)
            .ExecuteQueryAsync(MapToIncident, cancellationToken)
            .ConfigureAwait(false)];

    /// <summary>Gets an immutable composition route plan by content identity.</summary>
    /// <param name="planId">The content-addressed plan identity.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The matching plan, or <see langword="null"/> when none exists.</returns>
    public Task<CompositionRoutePlan?> ReadAsync(string planId, CancellationToken cancellationToken)
        => Database
            .Use("MarketDataService.GetCompositionRoutePlan", MarketDataServiceDbSql.GetCompositionRoutePlan)
            .SetParameters(new CompositionRoutePlanIdParameter(planId))
            .ExecuteSingleAsync<CompositionRoutePlan?>(MapToCompositionRoutePlan, cancellationToken);

    /// <summary>Persists an immutable composition route plan idempotently.</summary>
    /// <param name="plan">The content-addressed plan to persist.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous persistence operation.</returns>
    public async Task SaveAsync(CompositionRoutePlan plan, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(plan, new JsonSerializerOptions { MaxDepth = 32 });
        _ = await Database
            .Use("MarketDataService.InsertCompositionRoutePlan", MarketDataServiceDbSql.InsertCompositionRoutePlan)
            .SetParameters(new CompositionRoutePlanParameter(plan, payload))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        var saved = await ReadAsync(plan.PlanId, cancellationToken)
            .ConfigureAwait(false);
        if (saved is null || saved.PlanId != plan.PlanId)
            throw new InvalidDataException("Route plan commit could not be verified.");
    }

    /// <summary>Gets the durable subscription snapshot for a scope and dataset.</summary>
    /// <param name="scope">The durable ownership scope.</param>
    /// <param name="dataset">The provider dataset.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The current durable subscription snapshot.</returns>
    public Task<DurableSubscriptionSnapshot> ReadAsync(
        string scope,
        string dataset,
        CancellationToken cancellationToken = default)
        => _durableIntent.ReadAsync(scope, dataset, cancellationToken);

    /// <summary>Gets a previously persisted durable-intent operation result.</summary>
    /// <param name="scope">The durable ownership scope.</param>
    /// <param name="dataset">The provider dataset.</param>
    /// <param name="operationId">The idempotent operation identity.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The operation result, or <see langword="null"/> when it is unknown.</returns>
    public Task<DurableIntentResult?> FindOperationAsync(
        string scope,
        string dataset,
        Guid operationId,
        CancellationToken cancellationToken = default)
        => _durableIntent.FindOperationAsync(scope, dataset, operationId, cancellationToken);

    /// <summary>Applies a durable subscription authority mutation.</summary>
    /// <param name="mutation">The authority mutation to apply atomically.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The durable mutation result.</returns>
    public Task<DurableIntentResult> ApplyAsync(
        DurableAuthorityMutation mutation,
        CancellationToken cancellationToken = default)
        => _durableIntent.ApplyAsync(mutation, cancellationToken);

    /// <summary>Gets pending durable subscription outbox items.</summary>
    /// <param name="scope">The durable ownership scope.</param>
    /// <param name="dataset">The provider dataset.</param>
    /// <param name="pageSize">The maximum number of items to return.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The pending outbox items.</returns>
    public Task<IReadOnlyList<DurableSubscriptionOutboxItem>> ReadPendingOutboxAsync(
        string scope,
        string dataset,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
        => _durableIntent.ReadPendingOutboxAsync(scope, dataset, pageSize, cancellationToken);

    /// <summary>Acknowledges delivery of a durable subscription outbox item.</summary>
    /// <param name="scope">The durable ownership scope.</param>
    /// <param name="dataset">The provider dataset.</param>
    /// <param name="transitionId">The delivered transition identity.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> when the item was acknowledged; otherwise, <see langword="false"/>.</returns>
    public Task<bool> AcknowledgeOutboxAsync(
        string scope,
        string dataset,
        Guid transitionId,
        CancellationToken cancellationToken = default)
        => _durableIntent.AcknowledgeOutboxAsync(scope, dataset, transitionId, cancellationToken);

    internal static FuturesRolloverContractAssignment MapToAssignment(IObjectDataRecord row) => new()
    {
        ContractRole = Enum.Parse<DatabentoContractRole>(row.GetString(0)),
        RootSymbol = row.GetString(1),
        ContractId = row.GetString(2),
        Description = row.GetString(3),
        LocalSymbol = row.GetString(4),
        SecurityType = row.GetString(5),
        Currency = row.GetString(6),
        Exchange = row.GetString(7),
        Multiplier = row.GetString(8),
        LastTradeDate = row.GetDateOnly(9),
        NextRolloverDate = row.GetDateOnly(10),
        SourceContractHash = row.GetString(11),
        RowVersion = row.GetLong(12),
        CreatedOnUtc = row.GetDateTime(13).AsUtc(),
        CreatedBy = row.GetString(14),
        UpdatedOnUtc = row.GetDateTime(15).AsUtc(),
        UpdatedBy = row.GetString(16)
    };

    internal static DatabentoWatchdogObservation MapToObservation(IObjectDataRecord row) => new()
    {
        WatchdogStatusLogId = row.GetLong(0),
        ObservationId = row.GetGuid(1),
        CorrelationId = row.GetGuid(2),
        ValueDate = row.GetDateOnly(3),
        ObservedOnUtc = row.GetDateTime(4).AsUtc(),
        OperationReason = Enum.Parse<DatabentoOperationReason>(row.GetString(5)),
        MajorStatus = Enum.Parse<DatabentoMajorStatus>(row.GetString(6)),
        DisplayHealth = Enum.Parse<DatabentoDisplayHealth>(row.GetString(7)),
        CoreContractsReady = row.GetBool(8),
        RecoveryAttempt = row.GetInt(9),
        NativeBackend = row.GetString(10),
        NativeAbiVersion = row.GetInt(11),
        NativeGeneration = row.GetGuid(12),
        FailureStage = row.GetString(13),
        FailureDetail = row.GetString(14),
        FeedStatusDetails = JsonSerializer.Deserialize<DatabentoFeedWatchdogStatus[]>(row.GetString(15)) ?? [],
        RowVersion = row.GetLong(16)
    };

    internal static DatasetIncidentTransition MapToIncident(IObjectDataRecord row) => new(
        row.GetGuid(0),
        row.GetGuid(1),
        JsonSerializer.Deserialize<DatasetIncidentSnapshot>(row.GetString(2))
            ?? throw new InvalidDataException("Persisted dataset incident snapshot is invalid."),
        row.GetLong(3));

    internal static CompositionRoutePlan MapToCompositionRoutePlan(IObjectDataRecord row)
    {
        var result = JsonSerializer.Deserialize<CompositionRoutePlan>(
            row.GetString(0),
            new JsonSerializerOptions { MaxDepth = 32 })
            ?? throw new InvalidDataException("Persisted route plan is empty.");
        result.Validate();
        return result;
    }
}
