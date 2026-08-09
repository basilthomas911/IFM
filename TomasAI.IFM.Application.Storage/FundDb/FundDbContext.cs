using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Application.Storage.FundDb;

/// <summary>
/// Represents the database context for managing fund-related data and operations.
/// </summary>
/// <remarks>The <see cref="FundDbContext"/> class provides methods for performing CRUD operations on funds, fund
/// orders, fund transactions, and related entities. It also includes functionality for retrieving financial data such
/// as balances, profit/loss, and daily balances. This class is designed to interact with a database using the provided
/// connection settings and factory.</remarks>
/// <param name="connectionSettings">The database connection settings used to configure the context.</param>
/// <param name="dbFactory">The factory for creating database connections and executing commands.</param>
/// <param name="sequenceIdGenerator">The generator for creating unique sequence IDs for database entities.</param>
/// <param name="logger">The logger used for logging database operations and errors.</param>
public class FundDbContext : ObjectDataRepository<FundDbContext>, IFundDbContext
{
    readonly IDbContextFactory _dbFactory;
    readonly ISequenceIdGenerator _sequenceIdGenerator;
    public const string FundDbConnection = "FundDbConnection";
    static readonly int[] AmountSigns = [-1, 1];
    const int MaxConcurrentIdentityReads = 8;
    const int MaxReservationRotationAttempts = 8;
    const int ApplicationWriteBatchSize = 256;
    internal Func<Task>? FundOrderCanonicalMutationSubmittingForTestingAsync { get; set; }
    static readonly FundTransactionType[] OrderAmountTransactionTypes =
    [
        FundTransactionType.RealizedTradePnlAdjustment,
        FundTransactionType.UnrealizedTradePnl,
        FundTransactionType.UnrealizedTradePnlAdjustment,
        FundTransactionType.TradeCommission,
        FundTransactionType.OpeningTradeAdjustment,
        FundTransactionType.RealizedTradePnl,
        FundTransactionType.TradeCommissionAdjustment
    ];

    /*
    // Parameterless constructor for unit testing only
    public FundDbContext()
        : base(null, null) 
    {
    }
    */

    // Parameterized constructor
    public FundDbContext(
        IDbConnectionSettings connectionSettings,
        IDbContextFactory dbFactory,
        ISequenceIdGenerator sequenceIdGenerator,
        ILogger<DbProvider> logger)
        : base(connectionSettings[FundDbConnection], logger)
    {
        _dbFactory = IsArgumentNull.Set(dbFactory);
        _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);
    }

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override FundDbContext Database => this;

    /// <summary>
    /// object mapping properties
    /// </summary>
    static FundReadModel MapToFund<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            fundId: e.GetInt(0),
            name: e.GetString(1),
            description: e.GetString(2),
            balance: e.GetDecimal(3),
            isProduction: e.GetBool(4),
            createdOn: e.GetDateTime(5).ToUniversalTime(),
            createdBy: e.GetString(6)
        );

    static FundOrderReadModel MapToFundOrder<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            fundId: e.GetInt(0),
            orderId: e.GetInt(1),
            orderDate: e.GetDateTime(2),
            orderStatus: e.GetEnum<Domain.Fund.Shared.OrderStatus>(3),
            baseContractId: e.GetString(4),
            tradeDate: e.GetDateOnly(5),
            maturityDate: e.GetDateOnly(6),
            reference: e.GetString(7),
            createdOn: e.GetDateTime(8),
            createdBy: e.GetString(9),
            updatedOn: e.GetDateTime(10),
            updatedBy: e.GetString(11)
        );

    static FundOrderTradeReadModel MapToFundOrderTrade<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            fundId: e.GetInt(0),
            orderId: e.GetInt(1),
            tradeId: e.GetInt(2),
            tradeType: e.GetEnum<TradeType>(3),
            tradeDate: e.GetDateOnly(4),
            maturityDate: e.GetDateOnly(5),
            tradeState: e.GetEnum<TradeState>(6),
            tradeAction: e.GetEnum<TradeAction>(7),
            reference: e.GetString(8),
            primaryTrade: e.GetBool(9),
            baseContractSymbol: e.GetString(10),
            createdOn: e.GetDateTime(11),
            createdBy: e.GetString(12),
            updatedOn: e.GetDateTime(13),
            updatedBy: e.GetString(14)
        );

    static FundTransactionReadModel MapToFundTransaction<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            transactionId: e.GetLong(0),
            transactionDate: e.GetDateTime(1),
            transactionType: e.GetEnum<FundTransactionType>(2),
            fundId: e.GetInt(3),
            orderId: e.GetInt(4),
            tradeId: e.GetInt(5),
            tradeType: e.GetEnum<TradeType>(6),
            valueDate: e.GetDateOnly(7),
            tradeStatus: e.GetEnum<TradeStatus>(8),
            description: e.GetString(9),
            amount: e.GetDecimal(10),
            balance: e.GetDecimal(11)
        );

    static FundTransactionAmountProjection MapToFundTransactionAmount<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            FundId: e.GetInt(0),
            ValueDate: e.GetDateOnly(1),
            OrderId: e.GetInt(2),
            TradeId: e.GetInt(3),
            TradeType: e.GetEnum<TradeType>(4),
            TransactionDate: e.GetDateTime(5),
            TransactionId: e.GetLong(6),
            Amount: e.GetDecimal(7));

    static decimal MapToFundBalance<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDecimal(0);

    static DateOnly MapToValueDate<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDateOnly(0);

    static Guid MapToGuid<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetGuid(0);

    static bool MapToBoolean<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetBool(0);

    static FundTransactionIdentityRow MapToFundTransactionIdentity<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetLong(0));

    static FundTransactionProjectionMutationJournalEntry MapToFundTransactionProjectionMutationJournalEntry<TDataRecord>(
        TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(e.GetInt(0), e.GetDateOnly(1), e.GetGuid(2), e.GetDateTime(3));

    static FundTransactionWriteMutationJournalEntry MapToFundTransactionWriteMutationJournalEntry<TDataRecord>(
        TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(e.GetInt(0), e.GetGuid(1), e.GetDateTime(2));

    static FundTransactionProjectionKey MapToFundTransactionProjectionKey<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            e.GetInt(0),
            e.GetDateOnly(1),
            e.GetInt(2),
            e.GetInt(3),
            e.GetString(4),
            e.GetString(5),
            FundTransactionProjection.NormalizeTransactionDate(e.GetDateTime(6)),
            e.GetLong(7));

    static FundTransactionProjectionState MapToFundTransactionProjectionState<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            e.GetGuid(0),
            e.GetBool(1),
            e.GetLong(2),
            e.GetString(3),
            e.GetDateTime(4));

    static FundTransactionProjectionKey GetProjectionKey(FundTransactionReadModel transaction)
        => new(
            transaction.FundId,
            transaction.ValueDate,
            transaction.OrderId,
            transaction.TradeId,
            transaction.TradeType.ToStringFast(),
            transaction.TransactionType.ToStringFast(),
            FundTransactionProjection.NormalizeTransactionDate(transaction.TransactionDate),
            transaction.TransactionId);

    static int MapToFundId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetInt(0);

    static FundOrderProjectionRow MapToFundOrderProjectionRow<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(
            e.GetInt(0),
            e.GetInt(1),
            e.IsNull(2) ? null : e.GetGuid(2));

    static FundOrderReservation MapToFundOrderReservation<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(
            e.GetInt(0),
            e.IsNull(1) ? null : e.GetGuid(1));

    static FundOrderWriteOwnership MapToFundOrderWriteOwnership<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetInt(0), e.GetGuid(1), e.GetDateTime(2));

    static async Task<FundOrderReservation?> ReadFundOrderReservationAsync(
        IObjectRepository db,
        int orderId)
        => await db.Use(FundDbCql.GetFundOrderReservationV3)
            .SetParameters(new GetFundOrderReservationV3(orderId))
            .ExecuteSingleAsync<FundOrderReservation?>(
                static row => MapToFundOrderReservation(row));

    static async Task<IReadOnlyList<FundOrderWriteOwnership>> BeginFundOrderWritesAsync(
        IObjectRepository db,
        IEnumerable<int> orderIds)
    {
        var ownerships = new List<FundOrderWriteOwnership>();
        foreach (var orderId in orderIds.Distinct().Order())
        {
            var ownership = new FundOrderWriteOwnership(
                orderId,
                Guid.NewGuid(),
                DateTime.UtcNow);
            try
            {
                var applied = await db.Use(FundDbCql.ClaimFundOrderWriteOwnershipV3)
                    .SetParameters(new ClaimFundOrderWriteOwnershipV3(
                        ownership.OrderId,
                        ownership.OperationId,
                        ownership.StartedOn))
                    .ExecuteSingleAsync(MapToBoolean!);
                if (applied != true)
                {
                    throw new StorageException(
                        $"Fund order {orderId} is already being modified; retry the write.");
                }
                ownerships.Add(ownership);
            }
            catch
            {
                // A timed-out claim may still have applied. Exact cleanup includes
                // the attempted scope and preserves the initiating exception.
                await TryReleaseFundOrderWritesAsync(db, [.. ownerships, ownership])
                    .ConfigureAwait(false);
                throw;
            }
        }
        return ownerships;
    }

    static async Task ReleaseFundOrderWritesAsync(
        IObjectRepository db,
        IEnumerable<FundOrderWriteOwnership> ownerships)
    {
        foreach (var ownership in ownerships.Reverse())
        {
            _ = await db.Use(FundDbCql.ReleaseFundOrderWriteOwnershipV3)
                .SetParameters(new ReleaseFundOrderWriteOwnershipV3(
                    ownership.OrderId,
                    ownership.OperationId))
                .ExecuteSingleAsync(MapToBoolean!);
        }
    }

    static async Task TryReleaseFundOrderWritesAsync(
        IObjectRepository db,
        IEnumerable<FundOrderWriteOwnership> ownerships)
    {
        try
        {
            await ReleaseFundOrderWritesAsync(db, ownerships).ConfigureAwait(false);
        }
        catch
        {
            // An unresolved exact row remains fail-closed for explicit stale recovery.
        }
    }

    static async Task RecoverVerifiedInactiveFundOrderWritesAsync(
        IObjectRepository db,
        DateTime staleOperationCutoffUtc,
        CancellationToken cancellationToken)
    {
        await foreach (var ownership in db.Use(FundDbCql.GetFundOrderWriteOwnershipsV3All)
            .ExecuteStreamAsync(MapToFundOrderWriteOwnership, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ProjectionMutationSafety.AsUtc(ownership.StartedOn) > staleOperationCutoffUtc)
                continue;
            _ = await db.Use(FundDbCql.ReleaseFundOrderWriteOwnershipV3)
                .SetParameters(new ReleaseFundOrderWriteOwnershipV3(
                    ownership.OrderId,
                    ownership.OperationId))
                .ExecuteSingleAsync(MapToBoolean!);
        }
    }

    async Task<List<FundTransactionReadModel>> ReadFundTransactionTimelineAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var ranges = FundTransactionProjection.GetMonthRanges(startDate, endDate);
        return await FundTransactionProjection.ReadBoundedAsync<FundTransactionMonthRange, FundTransactionReadModel>(
            ranges,
            async range =>
            {
                var generation = await GetFundTransactionProjectionReadGenerationAsync(
                    fundId,
                    range.MonthBucket,
                    cancellationToken).ConfigureAwait(false);
                if (generation is null)
                    return await ReadBaseFundTransactionRangeAsync(fundId, range.StartDate, range.EndDate, cancellationToken).ConfigureAwait(false);

                var projectedRows = await _dbFactory.FundDb
                    .Use(FundDbCql.GetFundTransactionTimelineV3)
                    .SetParameters(new GetFundTransactionTimelineV3(fundId, range.MonthBucket, range.StartDate, range.EndDate))
                    .ExecuteQueryAsync(MapToFundTransaction!, cancellationToken).ConfigureAwait(false);

                return await IsFundTransactionProjectionReadGenerationValidAsync(
                    fundId,
                    range.MonthBucket,
                    generation.Value,
                    cancellationToken).ConfigureAwait(false)
                    ? projectedRows
                    : await ReadBaseFundTransactionRangeAsync(fundId, range.StartDate, range.EndDate, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    async Task<FundTransactionProjectionState?> GetFundTransactionProjectionStateAsync(
        int fundId,
        DateOnly monthBucket,
        CancellationToken cancellationToken = default)
    {
        var states = await _dbFactory.FundDb
            .Use(FundDbCql.GetFundTransactionProjectionStateV3)
            .SetParameters(new GetFundTransactionProjectionStateV3(fundId, monthBucket))
            .ExecuteQueryAsync(MapToFundTransactionProjectionState, cancellationToken).ConfigureAwait(false);
        return states.Count == 0 ? null : states.First();
    }

    async Task<ICollection<Guid>> GetFundTransactionProjectionMutationsAsync(
        int fundId,
        DateOnly monthBucket,
        CancellationToken cancellationToken = default)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundTransactionProjectionMutationsV3)
            .SetParameters(new GetFundTransactionProjectionMutationsV3(fundId, monthBucket))
            .ExecuteQueryAsync(MapToGuid, cancellationToken).ConfigureAwait(false);

    async Task<Guid?> GetFundTransactionProjectionReadGenerationAsync(
        int fundId,
        DateOnly monthBucket,
        CancellationToken cancellationToken = default)
    {
        var state = await GetFundTransactionProjectionStateAsync(fundId, monthBucket, cancellationToken).ConfigureAwait(false);
        if (state is null || !state.Value.IsComplete)
            return null;

        return (await GetFundTransactionProjectionMutationsAsync(fundId, monthBucket, cancellationToken).ConfigureAwait(false)).Count == 0
            ? state.Value.Generation
            : null;
    }

    async Task<bool> IsFundTransactionProjectionReadGenerationValidAsync(
        int fundId,
        DateOnly monthBucket,
        Guid generation,
        CancellationToken cancellationToken = default)
    {
        var state = await GetFundTransactionProjectionStateAsync(fundId, monthBucket, cancellationToken).ConfigureAwait(false);
        return state is { IsComplete: true } &&
            state.Value.Generation == generation &&
            (await GetFundTransactionProjectionMutationsAsync(fundId, monthBucket, cancellationToken).ConfigureAwait(false)).Count == 0;
    }

    Task<ICollection<FundTransactionReadModel>> ReadBaseFundTransactionRangeAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
        => _dbFactory.FundDb
            .Use(FundDbCql.GetFundTransactions)
            .SetParameters(new GetFundTransactions(fundId, startDate, endDate))
            .ExecuteQueryAsync(MapToFundTransaction!, cancellationToken);

    async Task<List<FundTransactionAmountProjection>> ReadFundTransactionAmountsAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate,
        IEnumerable<FundTransactionType> transactionTypes,
        IEnumerable<int> amountSigns,
        CancellationToken cancellationToken = default)
    {
        var typeNames = transactionTypes.Select(transactionType => transactionType.ToStringFast()).Distinct().ToArray();
        var signs = amountSigns.Distinct().ToArray();
        var results = new List<FundTransactionAmountProjection>();

        foreach (var range in FundTransactionProjection.GetMonthRanges(startDate, endDate))
        {
            var generation = await GetFundTransactionProjectionReadGenerationAsync(
                fundId,
                range.MonthBucket,
                cancellationToken).ConfigureAwait(false);
            if (generation is null)
            {
                await AddBaseRowsAsync(range).ConfigureAwait(false);
                continue;
            }

            var partitions = typeNames
                .SelectMany(transactionType => signs.Select(amountSign =>
                    new FundTransactionAmountPartition(range, transactionType, amountSign)))
                .ToArray();
            var partitionResults = await FundTransactionProjection.ReadBoundedPartitionsAsync<FundTransactionAmountPartition, FundTransactionAmountProjection>(
                partitions,
                partition => _dbFactory.FundDb
                    .Use(FundDbCql.GetFundTransactionAmountsV3)
                    .SetParameters(new GetFundTransactionAmountsV3(
                        fundId,
                        partition.Range.MonthBucket,
                        partition.TransactionType,
                        partition.AmountSign,
                        partition.Range.StartDate,
                        partition.Range.EndDate))
                    .ExecuteQueryAsync(MapToFundTransactionAmount, cancellationToken)).ConfigureAwait(false);

            if (await IsFundTransactionProjectionReadGenerationValidAsync(
                    fundId,
                    range.MonthBucket,
                    generation.Value,
                    cancellationToken).ConfigureAwait(false))
            {
                foreach (var partitionResult in partitionResults)
                    results.AddRange(partitionResult.Rows);
            }
            else
            {
                await AddBaseRowsAsync(range).ConfigureAwait(false);
            }
        }

        return results;

        async Task AddBaseRowsAsync(FundTransactionMonthRange range)
        {
            var baseRows = await ReadBaseFundTransactionRangeAsync(
                fundId,
                range.StartDate,
                range.EndDate,
                cancellationToken).ConfigureAwait(false);
            results.AddRange(baseRows
                .Where(transaction =>
                    typeNames.Contains(transaction.TransactionType.ToStringFast(), StringComparer.Ordinal) &&
                    signs.Contains(FundTransactionProjection.GetAmountSign(transaction.Amount)))
                .Select(transaction => new FundTransactionAmountProjection(
                    transaction.FundId,
                    transaction.ValueDate,
                    transaction.OrderId,
                    transaction.TradeId,
                    transaction.TradeType,
                    transaction.TransactionDate,
                    transaction.TransactionId,
                    transaction.Amount)));
        }
    }

    async Task<ICollection<FundOrderAmountReadModel>> GetFundOrderAmountsAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate,
        int amountSign,
        CancellationToken cancellationToken = default)
    {
        var transactions = await ReadFundTransactionAmountsAsync(
            fundId,
            startDate,
            endDate,
            OrderAmountTransactionTypes,
            [amountSign],
            cancellationToken).ConfigureAwait(false);

        return transactions
            .GroupBy(transaction => new { transaction.FundId, transaction.ValueDate, transaction.OrderId })
            .Select(group => new FundOrderAmountReadModel(
                group.Key.FundId,
                group.Key.ValueDate,
                group.Key.OrderId,
                group.Sum(transaction => transaction.Amount)))
            .OrderByDescending(amount => amount.ValueDate)
            .ThenBy(amount => amount.OrderId)
            .ToArray();
    }

    async Task<decimal> ReadFundStatusBalanceAsync(
        int fundId,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        bool ascending,
        CancellationToken cancellationToken = default)
    {
        var tradeStatusName = tradeStatus.ToStringFast();
        var monthBucket = FundTransactionProjection.GetMonthBucket(valueDate);
        var generation = await GetFundTransactionProjectionReadGenerationAsync(fundId, monthBucket, cancellationToken).ConfigureAwait(false);
        if (generation is null)
            return await ReadBaseFundStatusBalanceAsync(fundId, valueDate, tradeStatus, ascending, cancellationToken).ConfigureAwait(false);

        ICollection<decimal> projectedBalances;
        if (ascending)
        {
            projectedBalances = await _dbFactory.FundDb
                .Use(FundDbCql.GetOpeningFundBalanceV3)
                .SetParameters(new GetOpeningFundBalanceV3(fundId, monthBucket, valueDate, tradeStatusName))
                .ExecuteQueryAsync(MapToFundBalance!, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            projectedBalances = await _dbFactory.FundDb
                .Use(FundDbCql.GetClosingFundBalanceV3)
                .SetParameters(new GetClosingFundBalanceV3(fundId, monthBucket, valueDate, tradeStatusName))
                .ExecuteQueryAsync(MapToFundBalance!, cancellationToken).ConfigureAwait(false);
        }

        return await IsFundTransactionProjectionReadGenerationValidAsync(
            fundId,
            monthBucket,
            generation.Value,
            cancellationToken).ConfigureAwait(false)
            ? projectedBalances.FirstOrDefault()
            : await ReadBaseFundStatusBalanceAsync(fundId, valueDate, tradeStatus, ascending, cancellationToken).ConfigureAwait(false);
    }

    async Task<decimal> ReadBaseFundStatusBalanceAsync(
        int fundId,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        bool ascending,
        CancellationToken cancellationToken = default)
    {
        var baseRows = await ReadBaseFundTransactionRangeAsync(fundId, valueDate, valueDate, cancellationToken).ConfigureAwait(false);
        var matchingRows = baseRows.Where(transaction => transaction.TradeStatus == tradeStatus);
        var selected = ascending
            ? matchingRows.MinBy(transaction => (transaction.TransactionDate, transaction.TransactionId))
            : matchingRows.MaxBy(transaction => (transaction.TransactionDate, transaction.TransactionId));
        return selected?.Balance ?? 0m;
    }

    async Task<int> WriteFundTransactionBatchAsync(
        IEnumerable<FundTransactionReadModel> transactions,
        CancellationToken cancellationToken = default)
    {
        var transactionList = transactions as IReadOnlyList<FundTransactionReadModel> ?? transactions.ToArray();
        if (transactionList.Count == 0)
            return 0;
        FundTransactionProjection.ValidateLogicalDuplicates(transactionList);

        var db = _dbFactory.FundDb;
        var scopes = CreateFundTransactionMutationScopes(
            transactionList.Select(transaction => (
                transaction.FundId,
                FundTransactionProjection.GetMonthBucket(transaction.ValueDate))));
        var succeeded = false;
        var mutationsStarted = false;
        var targetMutationSubmissionStarted = false;
        try
        {
            // Publish the distributed write marker before identity lookup/allocation. This makes
            // identity reservations visible to overlapping backfills and delete/write recovery.
            await BeginFundTransactionProjectionMutationsAsync(scopes, cancellationToken).ConfigureAwait(false);
            mutationsStarted = true;
            var writes = await ResolveFundTransactionWritesAsync(
                transactionList,
                () => targetMutationSubmissionStarted = true).ConfigureAwait(false);
            var existingProjectionDeletes = new List<object>();
            foreach (var existing in writes
                .Select(write => write.ExistingTransaction)
                .Where(static transaction => transaction is not null)
                .DistinctBy(static transaction => transaction!.Id))
            {
                QueueFundTransactionProjectionDeleteCommands(db, existingProjectionDeletes, existing!);
            }
            if (existingProjectionDeletes.Count > 0)
            {
                targetMutationSubmissionStarted = true;
                await db.ExecuteQueuedCommandsAsync(existingProjectionDeletes).ConfigureAwait(false);
            }

            targetMutationSubmissionStarted = true;
            await db.Use(FundDbCql.InsertFundTransaction)
                .SetParameters(writes.Select(CreateFundTransactionInsert))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            await WriteFundTransactionProjectionBatchAsync(writes, cancellationToken).ConfigureAwait(false);
            succeeded = true;
            return writes.Count;
        }
        finally
        {
            if (mutationsStarted &&
                (succeeded || ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                    targetMutationSubmissionStarted)))
            {
                await FinishFundTransactionProjectionMutationsAsync(scopes, succeeded).ConfigureAwait(false);
            }
        }
    }

    async Task<List<FundTransactionWrite>> ResolveFundTransactionWritesAsync(
        IEnumerable<FundTransactionReadModel> transactions,
        Action onIdentityReservationSubmitting)
    {
        var transactionList = transactions as IReadOnlyList<FundTransactionReadModel> ?? transactions.ToArray();
        var representatives = new Dictionary<FundTransactionLogicalKey, FundTransactionReadModel>();
        foreach (var transaction in transactionList)
            representatives.TryAdd(FundTransactionLogicalKey.From(transaction), transaction);

        var resolvedIds = new Dictionary<FundTransactionLogicalKey, long>();
        var canonicalRows = new Dictionary<FundTransactionLogicalKey, ICollection<FundTransactionReadModel>>();
        foreach (var batch in representatives.Chunk(MaxConcurrentIdentityReads))
        {
            var identityReads = await Task.WhenAll(batch.Select(async entry =>
            {
                var db = _dbFactory.FundDb;
                var existingTask = db
                    .Use(FundDbCql.GetFundTransaction)
                    .SetParameters(new GetFundTransaction(
                        entry.Value.FundId,
                        entry.Value.ValueDate,
                        entry.Value.OrderId,
                        entry.Value.TradeId,
                        entry.Value.TradeType.ToStringFast(),
                        entry.Value.TransactionType.ToStringFast(),
                        entry.Key.TransactionDate))
                    .ExecuteQueryAsync(MapToFundTransaction!);
                var identityTask = db
                    .Use(FundDbCql.GetFundTransactionIdentityV4)
                    .SetParameters(CreateFundTransactionIdentityGet(entry.Key))
                    .ExecuteSingleAsync(MapToFundTransactionIdentity!);
                await Task.WhenAll(existingTask, identityTask).ConfigureAwait(false);
                return (
                    entry.Key,
                    ExistingRows: await existingTask.ConfigureAwait(false),
                    ReservedTransactionId: await identityTask.ConfigureAwait(false));
            })).ConfigureAwait(false);

            foreach (var identityRead in identityReads)
            {
                canonicalRows.Add(identityRead.Key, identityRead.ExistingRows);
                if (identityRead.ReservedTransactionId is { } reservedIdentity)
                    resolvedIds.Add(identityRead.Key, reservedIdentity.TransactionId);
            }
        }

        var reservationCandidates = new List<(FundTransactionLogicalKey Key, long TransactionId)>();
        foreach (var entry in representatives)
        {
            if (resolvedIds.ContainsKey(entry.Key))
                continue;

            var existingTransaction = canonicalRows[entry.Key].MinBy(row => row.TransactionId);
            var candidateTransactionId = existingTransaction?.TransactionId ??
                (entry.Value.TransactionId > 0
                    ? entry.Value.TransactionId
                    : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FundTransaction_TransactionId)
                        .ConfigureAwait(false));
            reservationCandidates.Add((entry.Key, candidateTransactionId));
        }

        foreach (var batch in reservationCandidates.Chunk(MaxConcurrentIdentityReads))
        {
            var reservations = await Task.WhenAll(batch.Select(async candidate =>
            {
                // Identity reservation is a canonical mutation and can apply before
                // a timed-out response reaches this process.
                onIdentityReservationSubmitting();
                var applied = await TryReserveFundTransactionIdentityAsync(
                    candidate.Key,
                    candidate.TransactionId).ConfigureAwait(false);
                if (applied)
                    return candidate;

                var winningTransactionId = await GetFundTransactionIdentityAsync(candidate.Key).ConfigureAwait(false);
                if (winningTransactionId is null)
                {
                    throw new StorageException(
                        $"FundDb could not read the winning distributed identity reservation for fund {candidate.Key.FundId}.");
                }

                return (Key: candidate.Key, TransactionId: winningTransactionId.Value);
            })).ConfigureAwait(false);

            foreach (var reservation in reservations)
                resolvedIds.Add(reservation.Key, reservation.TransactionId);
        }

        return transactionList
            .Select(transaction =>
            {
                var key = FundTransactionLogicalKey.From(transaction);
                var transactionId = resolvedIds[key];
                var existingTransaction = canonicalRows[key]
                    .FirstOrDefault(row => row.TransactionId == transactionId) ??
                    canonicalRows[key].MinBy(row => row.TransactionId);
                return new FundTransactionWrite(transaction, transactionId, existingTransaction);
            })
            .ToList();
    }

    static GetFundTransactionIdentityV4 CreateFundTransactionIdentityGet(FundTransactionLogicalKey key)
        => new(
            key.FundId,
            key.ValueDate,
            key.OrderId,
            key.TradeId,
            key.TradeType.ToStringFast(),
            key.TransactionType.ToStringFast(),
            key.TransactionDate);

    static ReserveFundTransactionIdentityV4 CreateFundTransactionIdentityReservation(
        FundTransactionLogicalKey key,
        long transactionId)
        => new(
            key.FundId,
            key.ValueDate,
            key.OrderId,
            key.TradeId,
            key.TradeType.ToStringFast(),
            key.TransactionType.ToStringFast(),
            key.TransactionDate,
            transactionId);

    async Task<long?> GetFundTransactionIdentityAsync(FundTransactionLogicalKey key)
        => (await _dbFactory.FundDb
            .Use(FundDbCql.GetFundTransactionIdentityV4)
            .SetParameters(CreateFundTransactionIdentityGet(key))
            .ExecuteSingleAsync(MapToFundTransactionIdentity!))?.TransactionId;

    async Task<bool> TryReserveFundTransactionIdentityAsync(
        FundTransactionLogicalKey key,
        long transactionId)
        => await _dbFactory.FundDb
            .Use(FundDbCql.ReserveFundTransactionIdentityV4)
            .SetParameters(CreateFundTransactionIdentityReservation(key, transactionId))
            .ExecuteScalarAsync(MapToBoolean!);

    static FundTransactionMutationScope[] CreateFundTransactionMutationScopes(
        IEnumerable<(int FundId, DateOnly MonthBucket)> months)
        => months
            .Distinct()
            .GroupBy(month => month.FundId)
            .OrderBy(group => group.Key)
            .Select(group => new FundTransactionMutationScope(
                group.Key,
                group.Select(month => month.MonthBucket)))
            .ToArray();

    async Task BeginFundTransactionProjectionMutationsAsync(
        IReadOnlyCollection<FundTransactionMutationScope> scopes,
        CancellationToken cancellationToken = default)
    {
        if (scopes.Count == 0)
            return;

        var db = _dbFactory.FundDb;
        var startedOn = DateTime.UtcNow;
        try
        {
            await db.Use(FundDbCql.InsertFundTransactionWriteMutationV3)
                .SetParameters(scopes.Select(scope => new InsertFundTransactionWriteMutationV3(
                    scope.FundId,
                    scope.MutationId,
                    startedOn)))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            await db.Use(FundDbCql.InsertFundTransactionProjectionMutationV3)
                .SetParameters(scopes
                    .SelectMany(scope => scope.Mutations)
                    .Select(mutation => new InsertFundTransactionProjectionMutationV3(
                        mutation.FundId,
                        mutation.MonthBucket,
                        mutation.MutationId,
                        startedOn)))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

            foreach (var scope in scopes)
            {
                scope.OwnershipClaimAttempted = true;
                scope.OwnsWriteOwnership = await TryClaimFundTransactionWriteOwnershipAsync(
                    scope,
                    cancellationToken).ConfigureAwait(false);
                var activeWrites = await GetFundTransactionWriteMutationsAsync(scope.FundId).ConfigureAwait(false);
                var ownsExclusiveWriteEpoch = scope.OwnsWriteOwnership &&
                    ProjectionMutationSafety.HasExclusiveMarker(activeWrites, scope.MutationId);
                if (!ownsExclusiveWriteEpoch)
                {
                    // Poison the current owner, including a newly claimed owner that can still see
                    // an older contender after an ownership handoff.
                    await FlagFundTransactionWriteOwnershipConflictAsync(scope.FundId).ConfigureAwait(false);
                }
                if (!scope.OwnsWriteOwnership)
                {
                    await InvalidateFundTransactionProjectionMonthsAsync(scope.Mutations, CancellationToken.None)
                        .ConfigureAwait(false);
                    continue;
                }

                foreach (var mutation in scope.Mutations)
                {
                    var activeMutations = await GetFundTransactionProjectionMutationsAsync(
                        mutation.FundId,
                        mutation.MonthBucket).ConfigureAwait(false);
                    var state = await GetFundTransactionProjectionStateAsync(
                        mutation.FundId,
                        mutation.MonthBucket).ConfigureAwait(false);
                    if (ProjectionMutationSafety.HasExclusiveMarker(activeMutations, mutation.MutationId) &&
                        state is { IsComplete: true })
                    {
                        scope.SetReadyGeneration(mutation.MonthBucket, state.Value.Generation);
                    }
                    else
                    {
                        await InvalidateFundTransactionProjectionMonthsAsync(
                            [mutation],
                            CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
        }
        catch
        {
            try
            {
                await InvalidateFundTransactionProjectionMonthsAsync(
                    scopes.SelectMany(scope => scope.Mutations).ToArray(),
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Ownership resolution is independent of state invalidation.
            }

            var removableScopes = new List<FundTransactionMutationScope>(scopes.Count);
            foreach (var scope in scopes)
            {
                var ownershipResolved = !scope.OwnershipClaimAttempted ||
                    await TryConfirmFundTransactionWriteOwnershipReleasedOrAbsentAsync(scope)
                        .ConfigureAwait(false);
                if (ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                    targetMutationSubmissionStarted: false,
                    ownershipReleaseOrAbsenceConfirmed: ownershipResolved))
                {
                    removableScopes.Add(scope);
                }
            }
            if (removableScopes.Count != 0)
            {
                try
                {
                    await EndFundTransactionProjectionMutationsAsync(
                        removableScopes,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Retain journals when their cleanup is ambiguous.
                }
            }
            throw;
        }
    }

    async Task InvalidateFundTransactionProjectionMonthsAsync(
        IReadOnlyCollection<FundTransactionProjectionMutation> mutations,
        CancellationToken cancellationToken = default)
    {
        if (mutations.Count == 0)
            return;

        await _dbFactory.FundDb
            .Use(FundDbCql.MarkFundTransactionProjectionIncompleteV3)
            .SetParameters(mutations.Select(mutation => new MarkFundTransactionProjectionIncompleteV3(
                mutation.MutationId,
                mutation.FundId,
                mutation.MonthBucket)))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    Task<ICollection<Guid>> GetFundTransactionWriteMutationsAsync(int fundId)
        => _dbFactory.FundDb
            .Use(FundDbCql.GetFundTransactionWriteMutationsV3)
            .SetParameters(new GetFundTransactionWriteMutationsV3(fundId))
            .ExecuteQueryAsync(MapToGuid);

    async Task<bool> TryClaimFundTransactionWriteOwnershipAsync(
        FundTransactionMutationScope scope,
        CancellationToken cancellationToken = default)
        => await _dbFactory.FundDb
            .Use(FundDbCql.ClaimFundTransactionWriteOwnershipV3)
            .SetParameters(new ClaimFundTransactionWriteOwnershipV3(
                scope.FundId,
                scope.MutationId,
                DateTime.UtcNow))
            .ExecuteScalarAsync(MapToBoolean!);

    async Task FlagFundTransactionWriteOwnershipConflictAsync(int fundId)
        => _ = await _dbFactory.FundDb
            .Use(FundDbCql.FlagFundTransactionWriteOwnershipConflictV3)
            .SetParameters(new FlagFundTransactionWriteOwnershipConflictV3(fundId))
            .ExecuteScalarAsync(MapToBoolean!);

    async Task<bool> ReleaseFundTransactionWriteOwnershipIfSafeAsync(FundTransactionMutationScope scope)
    {
        var released = await _dbFactory.FundDb
            .Use(FundDbCql.ReleaseFundTransactionWriteOwnershipIfSafeV3)
            .SetParameters(new ReleaseFundTransactionWriteOwnershipV3(scope.FundId, scope.MutationId))
            .ExecuteScalarAsync(MapToBoolean!);
        if (released)
            scope.OwnsWriteOwnership = false;
        return released;
    }

    async Task ReleaseFundTransactionWriteOwnershipAsync(FundTransactionMutationScope scope)
    {
        // Always issue the exact conditional release. A claim LWT can apply and then
        // time out before OwnsWriteOwnership is assigned locally.
        _ = await _dbFactory.FundDb
            .Use(FundDbCql.ReleaseFundTransactionWriteOwnershipV3)
            .SetParameters(new ReleaseFundTransactionWriteOwnershipV3(scope.FundId, scope.MutationId))
            .ExecuteScalarAsync(MapToBoolean!);
        scope.OwnsWriteOwnership = false;
    }

    async Task<bool> TryConfirmFundTransactionWriteOwnershipReleasedOrAbsentAsync(
        FundTransactionMutationScope scope)
    {
        try
        {
            await ReleaseFundTransactionWriteOwnershipAsync(scope).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    async Task<bool> AreFundTransactionReadyGenerationsValidAsync(FundTransactionMutationScope scope)
    {
        foreach (var readyGeneration in scope.ReadyGenerations)
        {
            var state = await GetFundTransactionProjectionStateAsync(
                scope.FundId,
                readyGeneration.Key).ConfigureAwait(false);
            var activeMutations = await GetFundTransactionProjectionMutationsAsync(
                scope.FundId,
                readyGeneration.Key).ConfigureAwait(false);
            if (state is not { IsComplete: true } ||
                state.Value.Generation != readyGeneration.Value ||
                !ProjectionMutationSafety.HasExclusiveMarker(activeMutations, scope.MutationId))
            {
                return false;
            }
        }
        return true;
    }

    async Task FinishFundTransactionProjectionMutationsAsync(
        IReadOnlyCollection<FundTransactionMutationScope> scopes,
        bool succeeded)
    {
        Exception? firstError = null;
        foreach (var scope in scopes)
        {
            try
            {
                await FinishFundTransactionMutationScopeAsync(scope, succeeded).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                firstError ??= exception;
            }
        }

        if (firstError is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
    }

    async Task FinishFundTransactionMutationScopeAsync(
        FundTransactionMutationScope scope,
        bool succeeded)
    {
        if (!succeeded)
        {
            scope.ClearReadyGenerations();
            await InvalidateFundTransactionProjectionMonthsAsync(
                scope.Mutations,
                CancellationToken.None).ConfigureAwait(false);
        }

        var activeWrites = await GetFundTransactionWriteMutationsAsync(scope.FundId).ConfigureAwait(false);
        if (!ProjectionMutationSafety.HasExclusiveMarker(activeWrites, scope.MutationId))
        {
            scope.ClearReadyGenerations();
            await InvalidateFundTransactionProjectionMonthsAsync(
                scope.Mutations,
                CancellationToken.None).ConfigureAwait(false);
            await ReleaseFundTransactionWriteOwnershipAsync(scope).ConfigureAwait(false);
            await EndFundTransactionProjectionMutationsAsync([scope], CancellationToken.None).ConfigureAwait(false);
            return;
        }

        var generationsMatch = succeeded &&
            await AreFundTransactionReadyGenerationsValidAsync(scope).ConfigureAwait(false);
        if (!generationsMatch)
        {
            scope.ClearReadyGenerations();
            await InvalidateFundTransactionProjectionMonthsAsync(
                scope.Mutations,
                CancellationToken.None).ConfigureAwait(false);
        }

        if (scope.OwnsWriteOwnership)
        {
            await RecomputeFundBalanceAsync(scope.FundId, CancellationToken.None).ConfigureAwait(false);
            var releasedWithoutConflict =
                await ReleaseFundTransactionWriteOwnershipIfSafeAsync(scope).ConfigureAwait(false);
            var keepReady = ProjectionMutationSafety.CanPublishReady(
                succeeded,
                ownsWriteEpoch: true,
                wasReadyOrExactlyReconciled: scope.ReadyGenerations.Count > 0,
                markerIsExclusive: true,
                generationStillMatches: generationsMatch,
                ownershipReleasedWithoutConflict: releasedWithoutConflict);
            if (releasedWithoutConflict)
            {
                if (!keepReady && scope.ReadyGenerations.Count > 0)
                {
                    scope.ClearReadyGenerations();
                    await InvalidateFundTransactionProjectionMonthsAsync(
                        scope.Mutations,
                        CancellationToken.None).ConfigureAwait(false);
                }
                await EndFundTransactionProjectionMutationsAsync([scope], CancellationToken.None).ConfigureAwait(false);
                return;
            }

            scope.ClearReadyGenerations();
            await InvalidateFundTransactionProjectionMonthsAsync(
                scope.Mutations,
                CancellationToken.None).ConfigureAwait(false);
            await ReleaseFundTransactionWriteOwnershipAsync(scope).ConfigureAwait(false);
        }

        // This method removes journals on its confirmed paths. If it throws, retain
        // them so explicit stale recovery can run after all writers have drained.
        await ReconcileFundBalanceAfterOverlapAsync(scope).ConfigureAwait(false);
    }

    async Task ReconcileFundBalanceAfterOverlapAsync(FundTransactionMutationScope scope)
    {
        const int maxAttempts = 64;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            scope.OwnershipClaimAttempted = true;
            try
            {
                scope.OwnsWriteOwnership = await TryClaimFundTransactionWriteOwnershipAsync(scope).ConfigureAwait(false);
                if (!scope.OwnsWriteOwnership)
                {
                    await FlagFundTransactionWriteOwnershipConflictAsync(scope.FundId).ConfigureAwait(false);
                    var activeOwnerWrites = await GetFundTransactionWriteMutationsAsync(scope.FundId).ConfigureAwait(false);
                    if (!ProjectionMutationSafety.HasExclusiveMarker(activeOwnerWrites, scope.MutationId))
                    {
                        await EndFundTransactionProjectionMutationsAsync([scope], CancellationToken.None).ConfigureAwait(false);
                        return;
                    }
                    await Task.Yield();
                    continue;
                }

                var activeWrites = await GetFundTransactionWriteMutationsAsync(scope.FundId).ConfigureAwait(false);
                if (!ProjectionMutationSafety.HasExclusiveMarker(activeWrites, scope.MutationId))
                {
                    await FlagFundTransactionWriteOwnershipConflictAsync(scope.FundId).ConfigureAwait(false);
                    await ReleaseFundTransactionWriteOwnershipAsync(scope).ConfigureAwait(false);
                    await EndFundTransactionProjectionMutationsAsync([scope], CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                await RecomputeFundBalanceAsync(scope.FundId, CancellationToken.None).ConfigureAwait(false);
                if (await ReleaseFundTransactionWriteOwnershipIfSafeAsync(scope).ConfigureAwait(false))
                {
                    await EndFundTransactionProjectionMutationsAsync([scope], CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                await ReleaseFundTransactionWriteOwnershipAsync(scope).ConfigureAwait(false);
                activeWrites = await GetFundTransactionWriteMutationsAsync(scope.FundId).ConfigureAwait(false);
                if (!ProjectionMutationSafety.HasExclusiveMarker(activeWrites, scope.MutationId))
                {
                    await EndFundTransactionProjectionMutationsAsync([scope], CancellationToken.None).ConfigureAwait(false);
                    return;
                }
                await Task.Yield();
            }
            catch
            {
                // A claim or release LWT can apply before timing out. Best-effort the
                // exact release, but never remove journals on this exceptional path.
                _ = await TryConfirmFundTransactionWriteOwnershipReleasedOrAbsentAsync(scope)
                    .ConfigureAwait(false);
                throw;
            }
        }

        throw new StorageException(
            $"FundDb could not publish a stable balance for fund {scope.FundId} after {maxAttempts} ownership attempts.");
    }

    async Task EndFundTransactionProjectionMutationsAsync(
        IReadOnlyCollection<FundTransactionMutationScope> scopes,
        CancellationToken cancellationToken = default)
    {
        if (scopes.Count == 0)
            return;

        var db = _dbFactory.FundDb;
        await db
            .Use(FundDbCql.DeleteFundTransactionProjectionMutationV3)
            .SetParameters(scopes
                .SelectMany(scope => scope.Mutations)
                .Select(mutation => new DeleteFundTransactionProjectionMutationV3(
                    mutation.FundId,
                    mutation.MonthBucket,
                    mutation.MutationId)))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        await db
            .Use(FundDbCql.DeleteFundTransactionWriteMutationV3)
            .SetParameters(scopes.Select(scope => new DeleteFundTransactionWriteMutationV3(
                scope.FundId,
                scope.MutationId)))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task RecomputeFundBalanceAsync(int fundId, CancellationToken cancellationToken = default)
    {
        var lastValueDate = await _dbFactory.FundDb
            .Use(FundDbCql.GetLastFundTransactionValueDate)
            .SetParameters(new GetLastFundTransactionValueDate(fundId, DateOnly.MaxValue))
            .ExecuteScalarAsync(MapToValueDate!).ConfigureAwait(false);
        var balance = 0m;
        if (lastValueDate != DateOnly.MinValue)
        {
            var transactions = await ReadBaseFundTransactionRangeAsync(fundId, lastValueDate, lastValueDate).ConfigureAwait(false);
            balance = transactions.MaxBy(transaction => transaction.TransactionId)?.Balance ?? 0m;
        }

        await _dbFactory.FundDb
            .Use(FundDbCql.UpdateFundBalance)
            .SetParameters(new UpdateFundBalance(fundId, balance))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task WriteFundTransactionProjectionBatchAsync(
        IReadOnlyList<FundTransactionWrite> writes,
        CancellationToken cancellationToken = default)
    {
        if (writes.Count == 0)
            return;

        var db = _dbFactory.FundDb;
        await db.Use(FundDbCql.InsertFundTransactionTimelineV3)
            .SetParameters(writes.Select(CreateFundTransactionTimelineInsert))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        await db.Use(FundDbCql.InsertFundBalanceByStatusDayV3)
            .SetParameters(writes.Select(CreateFundStatusBalanceInsert))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        await db.Use(FundDbCql.InsertFundTransactionAmountV3)
            .SetParameters(writes.Select(CreateFundTransactionAmountInsert))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    static InsertFundTransaction CreateFundTransactionInsert(FundTransactionWrite write)
    {
        var transaction = write.Transaction;
        return new InsertFundTransaction(
            write.TransactionId,
            FundTransactionProjection.NormalizeTransactionDate(transaction.TransactionDate),
            transaction.TransactionType.ToStringFast(),
            transaction.FundId,
            transaction.OrderId,
            transaction.TradeId,
            transaction.TradeType.ToStringFast(),
            transaction.ValueDate,
            transaction.TradeStatus.ToStringFast(),
            transaction.Description,
            transaction.Amount,
            transaction.Balance);
    }

    static InsertFundTransactionTimelineV3 CreateFundTransactionTimelineInsert(FundTransactionWrite write)
    {
        var transaction = write.Transaction;
        return new InsertFundTransactionTimelineV3(
            transaction.FundId,
            FundTransactionProjection.GetMonthBucket(transaction.ValueDate),
            transaction.ValueDate,
            FundTransactionProjection.NormalizeTransactionDate(transaction.TransactionDate),
            write.TransactionId,
            transaction.TransactionType.ToStringFast(),
            transaction.OrderId,
            transaction.TradeId,
            transaction.TradeType.ToStringFast(),
            transaction.TradeStatus.ToStringFast(),
            transaction.Description,
            transaction.Amount,
            transaction.Balance);
    }

    static InsertFundBalanceByStatusDayV3 CreateFundStatusBalanceInsert(FundTransactionWrite write)
    {
        var transaction = write.Transaction;
        return new InsertFundBalanceByStatusDayV3(
            transaction.FundId,
            FundTransactionProjection.GetMonthBucket(transaction.ValueDate),
            transaction.ValueDate,
            transaction.TradeStatus.ToStringFast(),
            FundTransactionProjection.NormalizeTransactionDate(transaction.TransactionDate),
            write.TransactionId,
            transaction.TransactionType.ToStringFast(),
            transaction.OrderId,
            transaction.TradeId,
            transaction.TradeType.ToStringFast(),
            transaction.Balance);
    }

    static InsertFundTransactionAmountV3 CreateFundTransactionAmountInsert(FundTransactionWrite write)
    {
        var transaction = write.Transaction;
        return new InsertFundTransactionAmountV3(
            transaction.FundId,
            FundTransactionProjection.GetMonthBucket(transaction.ValueDate),
            transaction.TransactionType.ToStringFast(),
            FundTransactionProjection.GetAmountSign(transaction.Amount),
            transaction.ValueDate,
            FundTransactionProjection.NormalizeTransactionDate(transaction.TransactionDate),
            write.TransactionId,
            transaction.OrderId,
            transaction.TradeId,
            transaction.TradeType.ToStringFast(),
            transaction.Amount);
    }

    static void QueueFundTransactionProjectionDeleteCommands(
        IObjectRepository db,
        List<object> queuedCommands,
        FundTransactionReadModel transaction)
    {
        var monthBucket = FundTransactionProjection.GetMonthBucket(transaction.ValueDate);
        var transactionType = transaction.TransactionType.ToStringFast();
        var transactionDate = FundTransactionProjection.NormalizeTransactionDate(transaction.TransactionDate);

        queuedCommands.Add(db.Use(FundDbCql.DeleteFundTransactionTimelineV3)
            .SetParameters(new DeleteFundTransactionTimelineV3(
                transaction.FundId,
                monthBucket,
                transaction.ValueDate,
                transaction.OrderId,
                transaction.TradeId,
                transaction.TradeType.ToStringFast(),
                transactionType,
                transactionDate,
                transaction.TransactionId))
            .QueueCommand());
        queuedCommands.Add(db.Use(FundDbCql.DeleteFundBalanceByStatusDayV3)
            .SetParameters(new DeleteFundBalanceByStatusDayV3(
                transaction.FundId,
                monthBucket,
                transaction.ValueDate,
                transaction.TradeStatus.ToStringFast(),
                transactionDate,
                transaction.TransactionId,
                transaction.OrderId,
                transaction.TradeId,
                transaction.TradeType.ToStringFast(),
                transactionType))
            .QueueCommand());
        queuedCommands.Add(db.Use(FundDbCql.DeleteFundTransactionAmountV3)
            .SetParameters(new DeleteFundTransactionAmountV3(
                transaction.FundId,
                monthBucket,
                transactionType,
                FundTransactionProjection.GetAmountSign(transaction.Amount),
                transaction.ValueDate,
                transactionDate,
                transaction.TransactionId,
                transaction.OrderId,
                transaction.TradeId,
                transaction.TradeType.ToStringFast()))
            .QueueCommand());
    }

    /// <summary>
    /// delete fund, fund order and fund order trades by tradeId
    /// </summary>
    /// <param name="fundId"></param>
    /// <returns></returns>
    public async Task DeleteFundAsync(int fundId)
        => await _dbFactory.FundDb
                .Use(FundDbCql.DeleteFund)
                .SetParameters(new DeleteFund(fundId))
                .ExecuteCommandAsync();

    /// <summary>
    /// delete fund order
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task DeleteFundOrderAsync(int fundId, int orderId)
    {
        var db = _dbFactory.FundDb;
        var ownerships = await BeginFundOrderWritesAsync(db, [orderId]).ConfigureAwait(false);
        var targetMutationSubmissionStarted = false;
        try
        {
            var canonical = await db.Use(FundDbCql.GetFundOrder)
                .SetParameters(new GetFundOrder(fundId, orderId))
                .ExecuteSingleAsync(MapToFundOrder!);
            if (canonical is null)
            {
                var historicalReservation = await ReadFundOrderReservationAsync(db, orderId)
                    .ConfigureAwait(false);
                if (historicalReservation is { FundId: var historicalFundId }
                    && historicalFundId != fundId)
                {
                    throw new StorageException(
                        $"Fund order {orderId} is permanently assigned to fund {historicalFundId}.");
                }

                await ReleaseFundOrderWritesAsync(db, ownerships).ConfigureAwait(false);
                return;
            }

            // The reservation is permanent historical ownership. Delete only the
            // canonical row; never make the order ID reusable.
            _ = await ReserveFundOrderIdAsync(db, orderId, fundId).ConfigureAwait(false);
            targetMutationSubmissionStarted = true;
            await db.Use(FundDbCql.DeleteFundOrder)
                .SetParameters(new DeleteFundOrder(fundId, orderId))
                .ExecuteCommandAsync().ConfigureAwait(false);
            await ReleaseFundOrderWritesAsync(db, ownerships).ConfigureAwait(false);
        }
        catch
        {
            if (!targetMutationSubmissionStarted)
                await TryReleaseFundOrderWritesAsync(db, ownerships).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// delete fund order trade
    /// </summary>
    /// <param name="fundId">fund order trade id</param>
    /// <param name="orderId">fund order id</param>
    /// <param name="tradeId">fund order trade id</param>
    /// <returns></returns>
    public async Task DeleteFundOrderTradeAsync(int fundId, int orderId, int tradeId)
        => await _dbFactory.FundDb.Use(FundDbCql.DeleteFundOrderTrade)
               .SetParameters(new DeleteFundOrderTrade(fundId, orderId, tradeId))
               .ExecuteCommandAsync();

    /// <summary>
    /// delete fund trancaction
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="valueDate"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="transactionType"></param>
    /// <param name="transactionDate"></param>
    /// <returns></returns>
    public async Task DeleteFundTransactionAsync(int fundId, DateOnly valueDate, int orderId, int tradeId, 
        TradeType tradeType, FundTransactionType transactionType, DateTime transactionDate)
    {
        var db = _dbFactory.FundDb;
        var tradeTypeName = tradeType.ToStringFast();
        var transactionTypeName = transactionType.ToStringFast();
        var normalizedTransactionDate = FundTransactionProjection.NormalizeTransactionDate(transactionDate);
        var monthBucket = FundTransactionProjection.GetMonthBucket(valueDate);
        var scopes = CreateFundTransactionMutationScopes([(fundId, monthBucket)]);
        var succeeded = false;
        var mutationsStarted = false;
        var targetMutationSubmissionStarted = false;
        try
        {
            await BeginFundTransactionProjectionMutationsAsync(scopes).ConfigureAwait(false);
            mutationsStarted = true;
            var matchingTransactions = await db
                .Use(FundDbCql.GetFundTransaction)
                .SetParameters(new GetFundTransaction(
                    fundId,
                    valueDate,
                    orderId,
                    tradeId,
                    tradeTypeName,
                    transactionTypeName,
                    normalizedTransactionDate))
                .ExecuteQueryAsync(MapToFundTransaction!).ConfigureAwait(false);

            var projectedTransactions = await db
                .Use(FundDbCql.GetFundTransactionTimelineV3)
                .SetParameters(new GetFundTransactionTimelineV3(fundId, monthBucket, valueDate, valueDate))
                .ExecuteQueryAsync(MapToFundTransaction!).ConfigureAwait(false);

            var transactionsToDelete = matchingTransactions
                .Concat(projectedTransactions.Where(transaction =>
                    transaction.OrderId == orderId &&
                    transaction.TradeId == tradeId &&
                    transaction.TradeType == tradeType &&
                    transaction.TransactionType == transactionType &&
                    FundTransactionProjection.NormalizeTransactionDate(transaction.TransactionDate) == normalizedTransactionDate))
                .DistinctBy(transaction => transaction.TransactionId)
                .ToArray();

            var queuedCommands = new List<object>(transactionsToDelete.Length * 3 + 1);
            foreach (var transaction in transactionsToDelete)
                QueueFundTransactionProjectionDeleteCommands(db, queuedCommands, transaction);

            queuedCommands.Add(db.Use(FundDbCql.DeleteFundTransaction)
                .SetParameters(new DeleteFundTransaction(
                    fundId,
                    valueDate,
                    orderId,
                    tradeId,
                    tradeTypeName,
                    transactionTypeName,
                    normalizedTransactionDate))
                .QueueCommand());

            targetMutationSubmissionStarted = true;
            await db.ExecuteQueuedCommandsAsync(queuedCommands).ConfigureAwait(false);
            succeeded = true;
        }
        finally
        {
            if (mutationsStarted &&
                (succeeded || ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                    targetMutationSubmissionStarted)))
            {
                await FinishFundTransactionProjectionMutationsAsync(scopes, succeeded).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// return single fund by id
    /// </summary>
    /// <param name="fundId"></param>
    /// <returns></returns>
    public async Task<FundReadModel?> GetFundAsync(int fundId)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundByFundId)
            .SetParameters(new GetFundByFundId(fundId))
            .ExecuteSingleAsync(MapToFund);

    /// <summary>
    /// return all funds
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FundReadModel>> GetFundsAsync()
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFunds)
            .ExecuteQueryAsync(MapToFund!);

    public async Task<ICollection<FundReadModel>> GetFundsAsync(CancellationToken cancellationToken)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFunds)
            .ExecuteQueryAsync(MapToFund!, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// return fund order by fund id and order id
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>  
    /// <returns></returns>
    public async Task<FundOrderReadModel?> GetFundOrderAsync(int fundId, int orderId)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundOrder)
            .SetParameters(new GetFundOrder(fundId, orderId))
            .ExecuteSingleAsync(MapToFundOrder);

    /// <summary>
    /// return all fund orders
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FundOrderReadModel>> GetFundOrdersAsync()
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundOrders)
            .ExecuteQueryAsync(MapToFundOrder);

    public async Task<ICollection<FundOrderReadModel>> GetFundOrdersAsync(CancellationToken cancellationToken)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundOrders)
            .ExecuteQueryAsync(MapToFundOrder, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// return all fund order trades 
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FundOrderTradeReadModel>> GetFundOrderTradesAsync()
        => await _dbFactory.FundDb
                .Use(FundDbCql.GetFundOrderTrades)
                .ExecuteQueryAsync(MapToFundOrderTrade);

    public async Task<ICollection<FundOrderTradeReadModel>> GetFundOrderTradesAsync(CancellationToken cancellationToken)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundOrderTrades)
            .ExecuteQueryAsync(MapToFundOrderTrade, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// return fund order trade by fund id, order id and trade id
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    /// 
    public async Task<FundOrderTradeReadModel?> GetFundOrderTradeAsync(int fundId, int orderId, int tradeId)
    => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundOrderTrade)
            .SetParameters(new GetFundOrderTrade(fundId, orderId, tradeId))
            .ExecuteSingleAsync(MapToFundOrderTrade!);

    /// <summary>
    /// return fund transaction 
    /// <param name="fundId"></param>
    /// <param name="valueDate"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="transactionType"></param>
    /// <param name="transactionDate"></param>
    /// <returns></returns>
    public async Task<FundTransactionReadModel?> GetFundTransactionAsync(int fundId, DateOnly valueDate, int orderId, int tradeId, TradeType tradeType,
        FundTransactionType transactionType, DateTime transactionDate)
        => await _dbFactory.FundDb
                .Use(FundDbCql.GetFundTransaction)
                .SetParameters(new GetFundTransaction(
                    fundId,
                    valueDate,
                    orderId,
                    tradeId,
                    tradeType.ToStringFast(),
                    transactionType.ToStringFast(),
                    FundTransactionProjection.NormalizeTransactionDate(transactionDate)))
                .ExecuteSingleAsync(MapToFundTransaction!);

    /// <summary>
    /// return list of fund transactions for selected fund by date range
    /// </summary>
    /// <param name="fundId">selected fund</param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FundTransactionReadModel>> GetFundTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate)
        => (await ReadFundTransactionTimelineAsync(fundId, startDate, endDate).ConfigureAwait(false))
            .OrderByDescending(transaction => transaction.ValueDate)
            .ThenByDescending(transaction => transaction.TransactionDate)
            .ThenByDescending(transaction => transaction.TransactionId)
            .ToArray();

    public async Task<ICollection<FundTransactionReadModel>> GetFundTransactionsAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
        => (await ReadFundTransactionTimelineAsync(fundId, startDate, endDate, cancellationToken).ConfigureAwait(false))
            .OrderByDescending(transaction => transaction.ValueDate)
            .ThenByDescending(transaction => transaction.TransactionDate)
            .ThenByDescending(transaction => transaction.TransactionId)
            .ToArray();

    /// <summary>
    /// return all  fund transactions
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FundTransactionReadModel>> GetFundTransactionsAsync()
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundTransactionsAll)
            .ExecuteQueryAsync(MapToFundTransaction!);

    /// <summary>
    /// return fund pnl for selected fund by date range
    /// </summary>
    /// <param name="fundId">selected fund</param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FundPnlReadModel>> GetFundPnlAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var transactions = await ReadFundTransactionAmountsAsync(
            fundId,
            startDate,
            endDate,
            [FundTransactionType.RealizedTradePnl],
            AmountSigns).ConfigureAwait(false);

        return transactions
            .GroupBy(transaction => new
            {
                transaction.FundId,
                transaction.ValueDate,
                transaction.OrderId,
                transaction.TradeId,
                transaction.TradeType
            })
            .Select(group => new FundPnlReadModel(
                group.Key.FundId,
                group.Key.ValueDate,
                group.Key.OrderId,
                group.Key.TradeId,
                group.Key.TradeType,
                group.Sum(transaction => transaction.Amount)))
            .OrderByDescending(pnl => pnl.ValueDate)
            .ThenBy(pnl => pnl.OrderId)
            .ThenBy(pnl => pnl.TradeId)
            .ToArray();
    }

    /// <summary>
    /// return fund balance
    /// </param>
    /// <returns></returns>
    public async Task<decimal> GetFundBalanceAsync(int fundId)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundBalance)
            .SetParameters(new GetFundBalance(fundId))
            .ExecuteScalarAsync(MapToFundBalance!);

    public async Task<decimal> GetFundBalanceAsync(int fundId, CancellationToken cancellationToken)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundBalance)
            .SetParameters(new GetFundBalance(fundId))
            .ExecuteScalarAsync(MapToFundBalance!, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// return fund trade commission
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<decimal> GetFundTradeCommissionAsync(int fundId, DateOnly startDate, DateOnly endDate)
        => (await ReadFundTransactionAmountsAsync(
                fundId,
                startDate,
                endDate,
                [FundTransactionType.TradeCommission],
                AmountSigns).ConfigureAwait(false))
            .Sum(transaction => transaction.Amount);

    public async Task<decimal> GetFundTradeCommissionAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
        => (await ReadFundTransactionAmountsAsync(
                fundId,
                startDate,
                endDate,
                [FundTransactionType.TradeCommission],
                AmountSigns,
                cancellationToken).ConfigureAwait(false))
            .Sum(transaction => transaction.Amount);

    /// <summary>
    /// return starting fund balance
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="startDate"></param>
    /// <returns></returns>
    public async Task<decimal> GetFundStartingBalanceAsync(int fundId, DateOnly startDate)
    {
        // ValueDate is the financial chronology. TransactionId is only a deterministic
        // tie-breaker inside the first eligible day because imported/backfilled rows may
        // carry explicit IDs that are not chronological across value dates.
        var firstValueDate = await _dbFactory.FundDb
            .Use(FundDbCql.GetFirstFundTransactionValueDate)
            .SetParameters(new GetFirstFundTransactionValueDate(fundId, startDate))
            .ExecuteScalarAsync(MapToValueDate!).ConfigureAwait(false);

        if (firstValueDate == DateOnly.MinValue)
            return 0m;

        var transactions = await ReadFundTransactionTimelineAsync(fundId, firstValueDate, firstValueDate).ConfigureAwait(false);

        return transactions.Count == 0
            ? 0m
            : transactions.MinBy(transaction => transaction.TransactionId)!.Balance;
    }

    public async Task<decimal> GetFundStartingBalanceAsync(
        int fundId,
        DateOnly startDate,
        CancellationToken cancellationToken)
    {
        var firstValueDate = await _dbFactory.FundDb
            .Use(FundDbCql.GetFirstFundTransactionValueDate)
            .SetParameters(new GetFirstFundTransactionValueDate(fundId, startDate))
            .ExecuteScalarAsync(MapToValueDate!, cancellationToken).ConfigureAwait(false);

        if (firstValueDate == DateOnly.MinValue)
            return 0m;

        var transactions = await ReadFundTransactionTimelineAsync(
            fundId,
            firstValueDate,
            firstValueDate,
            cancellationToken).ConfigureAwait(false);

        return transactions.Count == 0
            ? 0m
            : transactions.MinBy(transaction => transaction.TransactionId)!.Balance;
    }

    /// <summary>
    /// return ending fund balance
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<decimal> GetFundEndingBalanceAsync(int fundId, DateOnly endDate)
    {
        // Keep the boundary symmetric with the starting balance: choose the last eligible
        // financial day first, then its greatest transaction ID.
        var lastValueDate = await _dbFactory.FundDb
            .Use(FundDbCql.GetLastFundTransactionValueDate)
            .SetParameters(new GetLastFundTransactionValueDate(fundId, endDate))
            .ExecuteScalarAsync(MapToValueDate!).ConfigureAwait(false);

        if (lastValueDate == DateOnly.MinValue)
            return 0m;

        var transactions = await ReadFundTransactionTimelineAsync(fundId, lastValueDate, lastValueDate).ConfigureAwait(false);

        return transactions.Count == 0
            ? 0m
            : transactions.MaxBy(transaction => transaction.TransactionId)!.Balance;
    }

    public async Task<decimal> GetFundEndingBalanceAsync(
        int fundId,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var lastValueDate = await _dbFactory.FundDb
            .Use(FundDbCql.GetLastFundTransactionValueDate)
            .SetParameters(new GetLastFundTransactionValueDate(fundId, endDate))
            .ExecuteScalarAsync(MapToValueDate!, cancellationToken).ConfigureAwait(false);

        if (lastValueDate == DateOnly.MinValue)
            return 0m;

        var transactions = await ReadFundTransactionTimelineAsync(
            fundId,
            lastValueDate,
            lastValueDate,
            cancellationToken).ConfigureAwait(false);

        return transactions.Count == 0
            ? 0m
            : transactions.MaxBy(transaction => transaction.TransactionId)!.Balance;
    }

    /// <summary>
    /// return opening fund balance
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<decimal> GetOpeningFundBalanceAsync(int fundId, DateOnly valueDate)
        => await ReadFundStatusBalanceAsync(fundId, valueDate, TradeStatus.Open, ascending: true).ConfigureAwait(false);

    public async Task<decimal> GetOpeningFundBalanceAsync(int fundId, DateOnly valueDate, CancellationToken cancellationToken)
        => await ReadFundStatusBalanceAsync(
            fundId,
            valueDate,
            TradeStatus.Open,
            ascending: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// return closing fund balance
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<decimal> GetClosingFundBalanceAsync(int fundId, DateOnly valueDate)
        => await ReadFundStatusBalanceAsync(fundId, valueDate, TradeStatus.Close, ascending: false).ConfigureAwait(false);

    public async Task<decimal> GetClosingFundBalanceAsync(int fundId, DateOnly valueDate, CancellationToken cancellationToken)
        => await ReadFundStatusBalanceAsync(
            fundId,
            valueDate,
            TradeStatus.Close,
            ascending: false,
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// return fund orders with loss amounts
    /// </summary>
    /// <param name="fundId">selected fund</param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<ICollection<FundOrderAmountReadModel>> GetFundLossOrdersAsync(int fundId, DateOnly startDate, DateOnly endDate)
        => await GetFundOrderAmountsAsync(fundId, startDate, endDate, -1).ConfigureAwait(false);

    public async Task<ICollection<FundOrderAmountReadModel>> GetFundLossOrdersAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
        => await GetFundOrderAmountsAsync(fundId, startDate, endDate, -1, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// return fund orders with profit amounts
    /// </summary>
    /// <param name="fundId">selected fund</param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<ICollection<FundOrderAmountReadModel>> GetFundProfitOrdersAsync(int fundId, DateOnly startDate, DateOnly endDate)
        => await GetFundOrderAmountsAsync(fundId, startDate, endDate, 1).ConfigureAwait(false);

    public async Task<ICollection<FundOrderAmountReadModel>> GetFundProfitOrdersAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
        => await GetFundOrderAmountsAsync(fundId, startDate, endDate, 1, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// return fund daily balances
    /// </summary>
    /// <param name="fundId">selected fund</param>
    /// <param name="startDate">
    /// <paramref name="endDate"/>
    public async Task<ICollection<FundDailyBalanceReadModel>> GetFundDailyBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate)
        => (await ReadFundTransactionTimelineAsync(fundId, startDate, endDate).ConfigureAwait(false))
            .GroupBy(transaction => transaction.ValueDate)
            .Select(group => new FundDailyBalanceReadModel(fundId, group.Key, group.Max(transaction => transaction.Balance)))
            .OrderByDescending(balance => balance.ValueDate)
            .ToArray();

    public async Task<ICollection<FundDailyBalanceReadModel>> GetFundDailyBalancesAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
        => (await ReadFundTransactionTimelineAsync(fundId, startDate, endDate, cancellationToken).ConfigureAwait(false))
            .GroupBy(transaction => transaction.ValueDate)
            .Select(group => new FundDailyBalanceReadModel(fundId, group.Key, group.Max(transaction => transaction.Balance)))
            .OrderByDescending(balance => balance.ValueDate)
            .ToArray();

    /// <summary>
    /// return fund drawdown balances
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<FundDrawdownBalancesReadModel> GetFundDrawdownBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var startingBalance = await GetFundStartingBalanceAsync(fundId, startDate);
        var endingBalance = await GetFundEndingBalanceAsync(fundId, endDate);
        return new FundDrawdownBalancesReadModel(fundId, startingBalance, endingBalance);
    }

    /// <summary>
    /// return fund id from order id
    /// </summary>
    /// <param name="orderId"></param>
    public async Task<int> GetFundIdFromOrderIdAsync(int orderId)
    {
        var db = _dbFactory.FundDb;
        var projectedFundId = await db.Use(FundDbCql.GetFundIdFromOrderId)
            .SetParameters(new GetFundIdFromOrderId(orderId))
            .ExecuteScalarAsync(MapToFundId!);
        if (projectedFundId != 0)
        {
            var canonical = await db.Use(FundDbCql.GetFundOrder)
                .SetParameters(new GetFundOrder(projectedFundId, orderId))
                .ExecuteSingleAsync(MapToFundOrder!);
            if (canonical is not null)
                return projectedFundId;
        }

        // Migration-safe fallback: a full table stream is legal CQL and retains only the
        // requested key. It is used only until the additive projection has been backfilled.
        var matchedFundId = 0;
        await foreach (var order in db.Use(FundDbCql.GetFundOrders)
            .ExecuteStreamAsync(MapToFundOrder!))
        {
            if (order.OrderId != orderId)
                continue;
            if (matchedFundId != 0 && matchedFundId != order.FundId)
            {
                throw new StorageException(
                    $"Fund order {orderId} is assigned to canonical funds {matchedFundId} and {order.FundId}.");
            }
            matchedFundId = order.FundId;
        }
        return matchedFundId;
    }

    public async Task<int> GetFundIdFromOrderIdAsync(int orderId, CancellationToken cancellationToken)
    {
        var db = _dbFactory.FundDb;
        var projectedFundId = await db.Use(FundDbCql.GetFundIdFromOrderId)
            .SetParameters(new GetFundIdFromOrderId(orderId))
            .ExecuteScalarAsync(MapToFundId!, cancellationToken).ConfigureAwait(false);
        if (projectedFundId != 0)
        {
            var canonical = await db.Use(FundDbCql.GetFundOrder)
                .SetParameters(new GetFundOrder(projectedFundId, orderId))
                .ExecuteSingleAsync(MapToFundOrder!, cancellationToken).ConfigureAwait(false);
            if (canonical is not null)
                return projectedFundId;
        }

        var matchedFundId = 0;
        await foreach (var order in db.Use(FundDbCql.GetFundOrders)
            .ExecuteStreamAsync(MapToFundOrder!, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (order.OrderId != orderId)
                continue;
            if (matchedFundId != 0 && matchedFundId != order.FundId)
            {
                throw new StorageException(
                    $"Fund order {orderId} is assigned to canonical funds {matchedFundId} and {order.FundId}.");
            }
            matchedFundId = order.FundId;
        }
        return matchedFundId;
    }

    /// <summary>
    /// insert fund order
    /// </summary>
    /// <param name="e">investment fund</param>
    /// <returns></returns>
    public async Task InsertFundAsync(FundReadModel e)
        => await _dbFactory.FundDb
            .Use(FundDbCql.InsertFund)
            .SetParameters(new InsertFund(e.FundId, e.Name, e.Description, e.Balance, e.IsProduction, e.CreatedOn, e.CreatedBy))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of funds into the database asynchronously.
    /// </summary>
    /// <remarks>This method uses the underlying database connection to execute the insertion command. Ensure
    /// that the provided <paramref name="funds"/> collection is not null or empty, and that each <see
    /// cref="FundReadModel"/> contains valid data to avoid potential errors during execution.</remarks>
    /// <param name="funds">A collection of <see cref="FundReadModel"/> objects representing the funds to be inserted. Each fund must have
    /// valid values for its properties, such as <c>FundId</c>, <c>Name</c>, and <c>Balance>.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the funds have been successfully
    /// inserted.</returns>
    public async Task InsertFundsAsync(ICollection<FundReadModel> funds)
        => await _dbFactory.FundDb
            .Use(FundDbCql.InsertFund)
            .SetParameters(funds.Select(e => new InsertFund(e.FundId, e.Name, e.Description, e.Balance, e.IsProduction, e.CreatedOn, e.CreatedBy)))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of funds into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of funds and inserts them into the database. 
    /// The operation is performed asynchronously, and the method returns the total count of rows inserted.</remarks>
    /// <param name="funds">A collection of <see cref="FundReadModel"/> objects representing the funds to be inserted. Each fund must have
    /// valid properties such as <c>FundId</c>, <c>Name</c>, and <c>Balance</c>.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the number of rows
    /// successfully inserted into the database.</returns>
    public async Task<long> InsertFundsAsync(IEnumerable<FundReadModel> funds)
    {
        var rowCount = 0l;
        await _dbFactory.FundDb
            .Use(FundDbCql.InsertFund)
            .SetParameters(GetFunds().Select(e => new InsertFund(e.FundId, e.Name, e.Description, e.Balance, e.IsProduction, e.CreatedOn, e.CreatedBy)))
            .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<FundReadModel> GetFunds()
        {
            rowCount = 0l;
            foreach (var e in funds)
            {
                rowCount++;
                yield return e;
            }
        }

    }

    /// <summary>
    /// insert fund order
    /// </summary>
    /// <param name="e">fund order</param>
    /// <returns></returns>
    public async Task InsertFundOrderAsync(FundOrderReadModel e)
        => await InsertFundOrderBatchAsync([e]).ConfigureAwait(false);

    /// <summary>
    /// insert fund orders
    /// </summary>
    /// <param name="fundOrders">fund order</param>
    /// <returns></returns>
    public async Task InsertFundOrdersAsync(ICollection<FundOrderReadModel> fundOrders)
        => await InsertFundOrderBatchAsync(fundOrders as IReadOnlyList<FundOrderReadModel> ?? fundOrders.ToArray())
            .ConfigureAwait(false);

    /// <summary>
    /// insert fund orders
    /// </summary>
    /// <param name="fundOrders">fund order</param>
    /// <returns></returns>
    public async Task<long> InsertFundOrdersAsync(IEnumerable<FundOrderReadModel> fundOrders)
    {
        ArgumentNullException.ThrowIfNull(fundOrders);
        long rowCount = 0;
        foreach (var batch in fundOrders.Chunk(ApplicationWriteBatchSize))
        {
            await InsertFundOrderBatchAsync(batch).ConfigureAwait(false);
            rowCount += batch.Length;
        }
        return rowCount;
    }

    async Task InsertFundOrderBatchAsync(IReadOnlyList<FundOrderReadModel> fundOrders)
    {
        if (fundOrders.Count == 0)
            return;

        var distinctOrders = new Dictionary<int, FundOrderReadModel>();
        foreach (var order in fundOrders)
        {
            if (distinctOrders.TryGetValue(order.OrderId, out var existing))
            {
                if (existing.FundId != order.FundId)
                {
                    throw new ArgumentException(
                        $"Fund order {order.OrderId} cannot be assigned to funds {existing.FundId} and {order.FundId} in one write.",
                        nameof(fundOrders));
                }
                if (!CreateFundOrderInsert(existing).Equals(CreateFundOrderInsert(order)))
                {
                    throw new ArgumentException(
                        $"Fund order {order.OrderId} has conflicting payloads in one write.",
                        nameof(fundOrders));
                }
                continue;
            }
            distinctOrders.Add(order.OrderId, order);
        }

        var db = _dbFactory.FundDb;
        var ownerships = await BeginFundOrderWritesAsync(db, distinctOrders.Keys).ConfigureAwait(false);
        var targetMutationSubmissionStarted = false;
        try
        {
            foreach (var order in distinctOrders.Values)
                await ReserveFundOrderIdAsync(db, order.OrderId, order.FundId).ConfigureAwait(false);

            if (FundOrderCanonicalMutationSubmittingForTestingAsync is { } mutationSubmitting)
                await mutationSubmitting().ConfigureAwait(false);

            targetMutationSubmissionStarted = true;
            await db.Use(FundDbCql.InsertFundOrder)
                .SetParameters(distinctOrders.Values.Select(CreateFundOrderInsert))
                .ExecuteCommandAsync().ConfigureAwait(false);
            await ReleaseFundOrderWritesAsync(db, ownerships).ConfigureAwait(false);
        }
        catch
        {
            if (!targetMutationSubmissionStarted)
                await TryReleaseFundOrderWritesAsync(db, ownerships).ConfigureAwait(false);
            throw;
        }
    }

    static InsertFundOrder CreateFundOrderInsert(FundOrderReadModel order)
        => new(
            order.FundId,
            order.OrderId,
            order.OrderDate,
            order.OrderStatus.ToStringFast(),
            order.BaseContractId,
            order.TradeDate,
            order.MaturityDate,
            order.Reference ?? string.Empty,
            order.CreatedOn,
            order.CreatedBy,
            order.UpdatedOn,
            order.UpdatedBy);

    static async Task<Guid> ReserveFundOrderIdAsync(IObjectRepository db, int orderId, int fundId)
    {
        var insertedReservationToken = Guid.NewGuid();
        await db.Use(FundDbCql.InsertFundOrderByOrderIdV3)
            .SetParameters(new InsertFundOrderByOrderIdV3(
                orderId,
                fundId,
                insertedReservationToken))
            .ExecuteCommandAsync();

        for (var attempt = 0; attempt < MaxReservationRotationAttempts; attempt++)
        {
            var reservation = await ReadFundOrderReservationAsync(db, orderId).ConfigureAwait(false)
                ?? throw new StorageException(
                    $"Fund order {orderId} could not establish its uniqueness reservation.");
            if (reservation.FundId != fundId)
            {
                throw new StorageException(
                    $"Fund order {orderId} is already assigned to fund {reservation.FundId}.");
            }
            if (reservation.ReservationToken is not { } currentReservationToken)
            {
                throw new StorageException(
                    $"Fund order {orderId} has a legacy tokenless reservation. " +
                    "Repair it only while order writers are drained.");
            }
            if (currentReservationToken == insertedReservationToken)
                return insertedReservationToken;

            var replacementReservationToken = Guid.NewGuid();
            var rotated = await db.Use(FundDbCql.RotateFundOrderByOrderIdV3Reservation)
                .SetParameters(new RotateFundOrderByOrderIdV3Reservation(
                    replacementReservationToken,
                    orderId,
                    fundId,
                    currentReservationToken))
                .ExecuteSingleAsync(MapToBoolean!);
            if (rotated == true)
                return replacementReservationToken;
        }

        throw new StorageException(
            $"Fund order {orderId} uniqueness reservation changed too frequently; retry the write.");
    }

    /// <summary>
    /// Idempotently rebuilds the order-id lookup projection. Reads validate a projected hit
    /// against the canonical partition and stream-fallback on a miss, so this backfill can be
    /// replayed online without exposing stale mappings.
    /// </summary>
    public async Task<FundOrderProjectionBackfillResult> BackfillFundOrderByOrderIdProjectionAsync(
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null)
    {
        ProjectionMutationSafety.ValidateStaleOperationCutoffUtc(
            staleOperationCutoffUtc,
            nameof(staleOperationCutoffUtc));
        var db = _dbFactory.FundDb;
        if (staleOperationCutoffUtc is { } verifiedInactiveCutoffUtc)
        {
            await RecoverVerifiedInactiveFundOrderWritesAsync(
                db,
                verifiedInactiveCutoffUtc,
                cancellationToken).ConfigureAwait(false);
        }
        long sourceRows = 0;
        long missingRows = 0;
        long conflictingRows = 0;
        await foreach (var order in db.Use(FundDbCql.GetFundOrders)
            .ExecuteStreamAsync(MapToFundOrder!, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            sourceRows++;

            // The projection is a permanent identity registry, not a mirror of the
            // current canonical rows. Insert-if-absent, then point-check the exact
            // owner so memory remains bounded regardless of table size.
            await db.Use(FundDbCql.InsertFundOrderByOrderIdV3)
                .SetParameters(new InsertFundOrderByOrderIdV3(
                    order.OrderId,
                    order.FundId,
                    Guid.NewGuid()))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

            var reservation = await ReadFundOrderReservationAsync(db, order.OrderId)
                .ConfigureAwait(false);
            if (reservation is null)
                missingRows++;
            else if (reservation.Value.FundId != order.FundId)
                conflictingRows++;
        }

        long projectedRows = 0;
        long tokenlessRows = 0;
        await foreach (var projection in db.Use(FundDbCql.GetFundOrderByOrderIdKeysV3All)
            .ExecuteStreamAsync(MapToFundOrderProjectionRow, cancellationToken))
        {
            projectedRows++;
            if (!projection.ReservationToken.HasValue)
                tokenlessRows++;
        }

        return new FundOrderProjectionBackfillResult(
            sourceRows,
            projectedRows,
            missingRows,
            conflictingRows,
            tokenlessRows);
    }

    /// <summary>
    /// insert fund order trade
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFundOrderTradeAsync(FundOrderTradeReadModel e)
            => await _dbFactory.FundDb
                    .Use(FundDbCql.InsertFundOrderTrade)
                   .SetParameters(new InsertFundOrderTrade(e.FundId, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.MaturityDate, e.TradeState.ToStringFast(), e.TradeAction.ToStringFast(), e.Reference ?? string.Empty, e.PrimaryTrade, e.BaseContractSymbol, e.CreatedOn, e.CreatedBy))
                   .ExecuteCommandAsync();

    /// <summary>
    /// insert fund order trades
    /// </summary>
    /// <param name="fundOrderTrades"></param>
    /// <returns></returns>
    public async Task InsertFundOrderTradesAsync(ICollection<FundOrderTradeReadModel> fundOrderTrades)
            => await _dbFactory.FundDb
                    .Use(FundDbCql.InsertFundOrderTrade)
                   .SetParameters(fundOrderTrades.Select(e => new InsertFundOrderTrade(e.FundId, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.MaturityDate, e.TradeState.ToStringFast(), e.TradeAction.ToStringFast(), e.Reference ?? string.Empty, e.PrimaryTrade, e.BaseContractSymbol, e.CreatedOn, e.CreatedBy)))
                   .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of fund order trade records into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of fund order trades and inserts them into the
    /// database using the configured database factory and query. Ensure that the <paramref name="fundOrderTrades"/>
    /// collection is not null and contains valid data for each trade, as invalid or incomplete data may result in an
    /// error.</remarks>
    /// <param name="fundOrderTrades">A collection of <see cref="FundOrderTradeReadModel"/> objects representing the fund order trades to be inserted.
    /// Each object must contain valid data for the associated fund, order, and trade details.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when all fund order trade records have
    /// been successfully inserted into the database.</returns>
    public async Task<long> InsertFundOrderTradesAsync(IEnumerable<FundOrderTradeReadModel> fundOrderTrades)
    {
        var rowCount = 0l;
        await _dbFactory.FundDb
           .Use(FundDbCql.InsertFundOrderTrade)
           .SetParameters(GetFundOrderTrades().Select(e => new InsertFundOrderTrade(e.FundId, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.MaturityDate, e.TradeState.ToStringFast(), e.TradeAction.ToStringFast(), e.Reference ?? string.Empty, e.PrimaryTrade, e.BaseContractSymbol, e.CreatedOn, e.CreatedBy)))
           .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<FundOrderTradeReadModel> GetFundOrderTrades()
        {
            rowCount = 0l;
            foreach (var e in fundOrderTrades)
            {
                rowCount++;
                yield return e;
            }
        }
    }


    /// <summary>
    /// insert fund transction
    /// </summary>
    /// <param name="e">fund transaction</param>
    /// <returns></returns>
    public async Task InsertFundTransactionAsync(FundTransactionReadModel e)
        => _ = await WriteFundTransactionBatchAsync([e]).ConfigureAwait(false);

    /// <summary>
    /// insert fund transctions
    /// </summary>
    /// <param name="fundTransactions">fund transaction</param>
    /// <returns></returns>
    public async Task InsertFundTransactionsAsync(ICollection<FundTransactionReadModel> fundTransactions)
        => _ = await WriteFundTransactionBatchAsync(fundTransactions).ConfigureAwait(false);

    /// <summary>
    /// Inserts a collection of fund transactions into the database and updates the corresponding fund balances.
    /// </summary>
    /// <remarks>This method generates unique transaction IDs, writes the canonical rows first, then writes the
    /// idempotent query projections and the final balance for each fund. Multi-row statements use the storage
    /// provider's bounded bulk scheduler. A failed projection phase can be safely replayed by the projection backfill.</remarks>
    /// <param name="fundTransactions">A collection of <see cref="FundTransactionReadModel"/> objects representing the fund transactions to be
    /// inserted. Each transaction must include details such as transaction date, type, fund ID, and balance.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// database commands queued and executed.</returns>
    public async Task<long> InsertFundTransactionsAsync(IEnumerable<FundTransactionReadModel> fundTransactions)
    {
        ArgumentNullException.ThrowIfNull(fundTransactions);
        long rowCount = 0;
        foreach (var batch in fundTransactions.Chunk(ApplicationWriteBatchSize))
            rowCount += await WriteFundTransactionBatchAsync(batch).ConfigureAwait(false);
        return rowCount;
    }

    async Task RecoverVerifiedInactiveFundTransactionMutationsAsync(
        int fundId,
        DateTime staleOperationCutoffUtc,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.FundDb;
        var writeJournal = await db.Use(FundDbCql.GetFundTransactionWriteMutationJournalV3)
            .SetParameters(new GetFundTransactionWriteMutationsV3(fundId))
            .ExecuteQueryAsync(MapToFundTransactionWriteMutationJournalEntry).ConfigureAwait(false);
        var staleWrites = writeJournal
            .Where(entry => ProjectionMutationSafety.AsUtc(entry.StartedOn) <= staleOperationCutoffUtc)
            .ToArray();

        var staleProjectionMutations = new List<FundTransactionProjectionMutationJournalEntry>();
        await foreach (var entry in db.Use(FundDbCql.GetFundTransactionProjectionMutationJournalV3All)
            .ExecuteStreamAsync(MapToFundTransactionProjectionMutationJournalEntry, cancellationToken))
        {
            if (entry.FundId == fundId &&
                ProjectionMutationSafety.AsUtc(entry.StartedOn) <= staleOperationCutoffUtc)
            {
                staleProjectionMutations.Add(entry);
            }
        }

        if (staleWrites.Length == 0 && staleProjectionMutations.Count == 0)
            return;

        var affectedMonths = staleProjectionMutations
            .Select(static entry => entry.MonthBucket)
            .Distinct()
            .Select(monthBucket => new FundTransactionProjectionMutation(
                fundId,
                monthBucket,
                Guid.NewGuid()))
            .ToArray();
        await InvalidateFundTransactionProjectionMonthsAsync(
            affectedMonths,
            cancellationToken).ConfigureAwait(false);

        var staleMutationIds = staleWrites
            .Select(static entry => entry.MutationId)
            .Concat(staleProjectionMutations.Select(static entry => entry.MutationId))
            .Distinct()
            .ToArray();
        foreach (var mutationId in staleMutationIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = await db.Use(FundDbCql.ReleaseFundTransactionWriteOwnershipV3)
                .SetParameters(new ReleaseFundTransactionWriteOwnershipV3(fundId, mutationId))
                .ExecuteScalarAsync(MapToBoolean!);
        }

        if (staleProjectionMutations.Count != 0)
        {
            await db.Use(FundDbCql.DeleteFundTransactionProjectionMutationV3)
                .SetParameters(staleProjectionMutations.Select(static entry =>
                    new DeleteFundTransactionProjectionMutationV3(
                        entry.FundId,
                        entry.MonthBucket,
                        entry.MutationId)))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        }
        if (staleWrites.Length != 0)
        {
            await db.Use(FundDbCql.DeleteFundTransactionWriteMutationV3)
                .SetParameters(staleWrites.Select(static entry =>
                    new DeleteFundTransactionWriteMutationV3(
                        entry.FundId,
                        entry.MutationId)))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Idempotently rebuilds complete monthly projection partitions from the canonical transaction table.
    /// Every month touched by the requested range is rebuilt in full so stale keys are removed safely. A month
    /// becomes readable only after its row counts and key fingerprints reconcile and its generation is unchanged.
    /// Cancellation, failure, or a concurrent mutation leaves that month incomplete and on canonical fallback.
    /// </summary>
    public async Task<FundTransactionProjectionBackfillResult> BackfillFundTransactionProjectionsAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate,
        int batchSize = 500,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be greater than zero.");
        ProjectionMutationSafety.ValidateStaleOperationCutoffUtc(
            staleOperationCutoffUtc,
            nameof(staleOperationCutoffUtc));
        if (endDate < startDate)
            return default;

        if (staleOperationCutoffUtc is { } verifiedInactiveCutoffUtc)
        {
            await RecoverVerifiedInactiveFundTransactionMutationsAsync(
                fundId,
                verifiedInactiveCutoffUtc,
                cancellationToken).ConfigureAwait(false);
        }

        var db = _dbFactory.FundDb;
        var monthRanges = FundTransactionProjection.GetMonthRanges(startDate, endDate)
            .Select(range => new FundTransactionMonthRange(
                range.MonthBucket,
                range.MonthBucket,
                new DateOnly(
                    range.MonthBucket.Year,
                    range.MonthBucket.Month,
                    DateTime.DaysInMonth(range.MonthBucket.Year, range.MonthBucket.Month))))
            .ToArray();
        var scopes = CreateFundTransactionMutationScopes(
            monthRanges.Select(range => (fundId, range.MonthBucket)));
        var scope = scopes.Single();
        var mutations = scope.Mutations;
        var mutationByMonth = mutations.ToDictionary(mutation => mutation.MonthBucket);
        var reconciliations = new List<FundTransactionMonthReconciliation>(monthRanges.Length);
        var buffer = new List<FundTransactionWrite>(batchSize);
        var sourceFingerprint = new FundTransactionKeyFingerprint();
        var timelineFingerprint = new FundTransactionKeyFingerprint();
        var statusBalanceFingerprint = new FundTransactionKeyFingerprint();
        var transactionAmountFingerprint = new FundTransactionKeyFingerprint();
        var transactionsRead = 0L;
        var transactionsProjected = 0L;
        var logicalTransactionKeys = 0L;
        var identityRows = 0L;
        var missingIdentityRows = 0L;
        var conflictingIdentityRows = 0L;
        var duplicateCanonicalRows = 0L;
        var batchesExecuted = 0;
        var cleanupCompleted = false;
        var mutationsStarted = false;
        var targetMutationSubmissionStarted = false;
        try
        {
            await BeginFundTransactionProjectionMutationsAsync(scopes, cancellationToken).ConfigureAwait(false);
            mutationsStarted = true;
            scope.ClearReadyGenerations();
            await InvalidateFundTransactionProjectionMonthsAsync(mutations, cancellationToken).ConfigureAwait(false);
            foreach (var range in monthRanges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var mutation = mutationByMonth[range.MonthBucket];
                var partition = new FundTransactionProjectionPartition(fundId, range.MonthBucket);
                targetMutationSubmissionStarted = true;
                await db.Use(FundDbCql.DeleteFundTransactionTimelinePartitionV3)
                    .SetParameters(partition)
                    .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                await db.Use(FundDbCql.DeleteFundBalanceByStatusMonthPartitionV3)
                    .SetParameters(partition)
                    .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                await db.Use(FundDbCql.DeleteFundTransactionAmountPartitionV3)
                    .SetParameters(partition)
                    .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

                var monthSourceFingerprint = new FundTransactionKeyFingerprint();
                var identityExpectations = new Dictionary<FundTransactionLogicalKey, FundTransactionIdentityExpectation>();
                var stream = db
                    .Use(FundDbCql.GetFundTransactions)
                    .SetParameters(new GetFundTransactions(fundId, range.StartDate, range.EndDate))
                    .ExecuteStreamAsync(MapToFundTransaction!, cancellationToken);

                await foreach (var transaction in stream.ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var key = GetProjectionKey(transaction);
                    monthSourceFingerprint.Add(key);
                    sourceFingerprint.Add(key);
                    transactionsRead++;
                    var logicalKey = FundTransactionLogicalKey.From(transaction);
                    if (identityExpectations.TryGetValue(logicalKey, out var identityExpectation))
                    {
                        identityExpectations[logicalKey] = identityExpectation.Add(transaction.TransactionId);
                    }
                    else
                    {
                        identityExpectations.Add(
                            logicalKey,
                            new FundTransactionIdentityExpectation(transaction.TransactionId, CanonicalRows: 1));
                    }
                    buffer.Add(new FundTransactionWrite(transaction, transaction.TransactionId));
                    if (buffer.Count >= batchSize)
                        await FlushAsync().ConfigureAwait(false);
                }

                await FlushAsync().ConfigureAwait(false);
                // Group the complete month before reserving so the minimum canonical ID wins
                // regardless of Scylla page/scan order. IF NOT EXISTS never overwrites an
                // identity selected by a concurrent post-deploy writer.
                var identityReconciliation = await ReconcileFundTransactionIdentitiesAsync(
                    identityExpectations,
                    cancellationToken).ConfigureAwait(false);
                logicalTransactionKeys += identityReconciliation.LogicalTransactionKeys;
                identityRows += identityReconciliation.IdentityRows;
                missingIdentityRows += identityReconciliation.MissingIdentityRows;
                conflictingIdentityRows += identityReconciliation.ConflictingIdentityRows;
                duplicateCanonicalRows += identityReconciliation.DuplicateCanonicalRows;
                var monthTimelineFingerprint = await ReadFundTransactionProjectionFingerprintAsync(
                    FundDbCql.GetFundTransactionTimelineKeysV3,
                    partition,
                    cancellationToken).ConfigureAwait(false);
                var monthStatusBalanceFingerprint = await ReadFundTransactionProjectionFingerprintAsync(
                    FundDbCql.GetFundBalanceByStatusMonthKeysV3,
                    partition,
                    cancellationToken).ConfigureAwait(false);
                var monthTransactionAmountFingerprint = await ReadFundTransactionProjectionFingerprintAsync(
                    FundDbCql.GetFundTransactionAmountKeysV3,
                    partition,
                    cancellationToken).ConfigureAwait(false);
                timelineFingerprint.Merge(monthTimelineFingerprint);
                statusBalanceFingerprint.Merge(monthStatusBalanceFingerprint);
                transactionAmountFingerprint.Merge(monthTransactionAmountFingerprint);
                reconciliations.Add(new FundTransactionMonthReconciliation(
                    mutation,
                    monthSourceFingerprint.Count,
                    monthSourceFingerprint.Value,
                    monthTimelineFingerprint.Count,
                    monthTimelineFingerprint.Value,
                    monthStatusBalanceFingerprint.Count,
                    monthStatusBalanceFingerprint.Value,
                    monthTransactionAmountFingerprint.Count,
                    monthTransactionAmountFingerprint.Value,
                    identityReconciliation.LogicalTransactionKeys,
                    identityReconciliation.IdentityRows,
                    identityReconciliation.MissingIdentityRows,
                    identityReconciliation.ConflictingIdentityRows,
                    identityReconciliation.DuplicateCanonicalRows));

                async Task FlushAsync()
                {
                    if (buffer.Count == 0)
                        return;

                    cancellationToken.ThrowIfCancellationRequested();
                    await WriteFundTransactionProjectionBatchAsync(buffer, cancellationToken).ConfigureAwait(false);
                    transactionsProjected += buffer.Count;
                    batchesExecuted++;
                    buffer.Clear();
                }
            }

            var completionCandidates = new List<FundTransactionMonthReconciliation>(reconciliations.Count);
            foreach (var reconciliation in reconciliations.Where(reconciliation => reconciliation.IsReconciled))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var mutation = reconciliation.Mutation;
                var activeMutations = await GetFundTransactionProjectionMutationsAsync(
                    mutation.FundId,
                    mutation.MonthBucket).ConfigureAwait(false);
                if (activeMutations.Count != 1 || !activeMutations.Contains(mutation.MutationId))
                    continue;

                await db.Use(FundDbCql.MarkFundTransactionProjectionCompleteV3)
                    .SetParameters(new MarkFundTransactionProjectionCompleteV3(
                        reconciliation.SourceCount,
                        reconciliation.SourceFingerprint,
                        DateTime.UtcNow,
                        mutation.FundId,
                        mutation.MonthBucket,
                        mutation.MutationId))
                    .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

                var state = await GetFundTransactionProjectionStateAsync(
                    mutation.FundId,
                    mutation.MonthBucket).ConfigureAwait(false);
                if (state is { IsComplete: true } &&
                    state.Value.Generation == mutation.MutationId &&
                    state.Value.SourceCount == reconciliation.SourceCount &&
                    state.Value.SourceFingerprint == reconciliation.SourceFingerprint)
                {
                    scope.SetReadyGeneration(mutation.MonthBucket, mutation.MutationId);
                    completionCandidates.Add(reconciliation);
                }
            }

            await FinishFundTransactionProjectionMutationsAsync(scopes, succeeded: true).ConfigureAwait(false);
            cleanupCompleted = true;

            var completedMonths = 0;
            foreach (var reconciliation in completionCandidates)
            {
                if (await IsFundTransactionProjectionReadGenerationValidAsync(
                    reconciliation.Mutation.FundId,
                    reconciliation.Mutation.MonthBucket,
                    reconciliation.Mutation.MutationId).ConfigureAwait(false))
                {
                    completedMonths++;
                }
            }

            return new FundTransactionProjectionBackfillResult(
                transactionsRead,
                transactionsProjected,
                batchesExecuted,
                timelineFingerprint.Count,
                statusBalanceFingerprint.Count,
                transactionAmountFingerprint.Count,
                sourceFingerprint.Value,
                timelineFingerprint.Value,
                statusBalanceFingerprint.Value,
                transactionAmountFingerprint.Value,
                completedMonths,
                monthRanges.Length,
                logicalTransactionKeys,
                identityRows,
                missingIdentityRows,
                conflictingIdentityRows,
                duplicateCanonicalRows);
        }
        finally
        {
            if (mutationsStarted && !cleanupCompleted &&
                ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                    targetMutationSubmissionStarted))
            {
                await FinishFundTransactionProjectionMutationsAsync(scopes, succeeded: false).ConfigureAwait(false);
            }
        }
    }

    async Task<FundTransactionIdentityReconciliation> ReconcileFundTransactionIdentitiesAsync(
        IReadOnlyDictionary<FundTransactionLogicalKey, FundTransactionIdentityExpectation> expectations,
        CancellationToken cancellationToken)
    {
        var identityRows = 0L;
        var missingIdentityRows = 0L;
        var conflictingIdentityRows = 0L;
        var duplicateCanonicalRows = expectations.Values.Sum(expectation => expectation.DuplicateCanonicalRows);

        foreach (var batch in expectations.Chunk(MaxConcurrentIdentityReads))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reconciled = await Task.WhenAll(batch.Select(async entry =>
            {
                var applied = await TryReserveFundTransactionIdentityAsync(
                    entry.Key,
                    entry.Value.TransactionId).ConfigureAwait(false);
                var actualTransactionId = applied
                    ? entry.Value.TransactionId
                    : await GetFundTransactionIdentityAsync(entry.Key).ConfigureAwait(false);
                return (ExpectedTransactionId: entry.Value.TransactionId, ActualTransactionId: actualTransactionId);
            })).ConfigureAwait(false);

            foreach (var identity in reconciled)
            {
                if (identity.ActualTransactionId is null)
                {
                    missingIdentityRows++;
                    continue;
                }

                identityRows++;
                if (identity.ActualTransactionId.Value != identity.ExpectedTransactionId)
                    conflictingIdentityRows++;
            }
        }

        return new FundTransactionIdentityReconciliation(
            expectations.Count,
            identityRows,
            missingIdentityRows,
            conflictingIdentityRows,
            duplicateCanonicalRows);
    }

    async Task<FundTransactionKeyFingerprint> ReadFundTransactionProjectionFingerprintAsync(
        string cql,
        FundTransactionProjectionPartition partition,
        CancellationToken cancellationToken)
    {
        var fingerprint = new FundTransactionKeyFingerprint();
        var stream = _dbFactory.FundDb
            .Use(cql)
            .SetParameters(partition)
            .ExecuteStreamAsync(MapToFundTransactionProjectionKey, cancellationToken);
        await foreach (var key in stream.ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            fingerprint.Add(key);
        }

        return fingerprint;
    }

    /// <summary>
    /// update fund order trade state
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>  
    /// <param name="tradeState"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    public async Task UpdateFundOrderTradeStateAsync(int fundId, int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy)
        => await _dbFactory.FundDb
                .Use(FundDbCql.UpdateFundOrderTradeState)
               .SetParameters(new UpdateFundOrderTradeState(fundId, orderId, tradeId, tradeState.ToStringFast(), updatedOn, updatedBy))
               .ExecuteCommandAsync();

    /// <summary>
    /// update fund order status
    /// </summary>
    /// <param name="e"></param>
    /// <param name="orderStatus"></param>
    /// <returns></returns>
    public async Task UpdateFundOrderStatusAsync(int fundId, int orderId, Domain.Fund.Shared.OrderStatus orderStatus)
    {
        var db = _dbFactory.FundDb;
        var ownerships = await BeginFundOrderWritesAsync(db, [orderId]).ConfigureAwait(false);
        var targetMutationSubmissionStarted = false;
        try
        {
            var canonical = await db.Use(FundDbCql.GetFundOrder)
                .SetParameters(new GetFundOrder(fundId, orderId))
                .ExecuteSingleAsync(MapToFundOrder!);
            if (canonical is null)
            {
                throw new StorageException(
                    $"Fund order {orderId} does not exist in fund {fundId}; status was not updated.");
            }

            _ = await ReserveFundOrderIdAsync(db, orderId, fundId).ConfigureAwait(false);
            targetMutationSubmissionStarted = true;
            await db.Use(FundDbCql.UpdateFundOrderStatus)
                .SetParameters(new UpdateFundOrderStatus(fundId, orderId, orderStatus.ToStringFast()))
                .ExecuteCommandAsync().ConfigureAwait(false);
            await ReleaseFundOrderWritesAsync(db, ownerships).ConfigureAwait(false);
        }
        catch
        {
            if (!targetMutationSubmissionStarted)
                await TryReleaseFundOrderWritesAsync(db, ownerships).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// backup fund database
    /// </summary>
    /// <param name="backupType"></param>
    /// <param name="commandTimeout"></param>
    /// <param name="onInfoMessage"></param>
    /// <returns></returns>
    public async Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage)
            => throw new NotImplementedException();
}
