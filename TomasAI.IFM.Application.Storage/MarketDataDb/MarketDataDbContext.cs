using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using MathNet.Numerics.Distributions;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend;
using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>
/// market data database constructor
/// </summary>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
/// <param name="logger"></param>
public partial class MarketDataDbContext(
    IDbConnectionSettings connectionSettings,
    IDbContextFactory dbFactory,
    IBlackboardService blackboardService,
    ISequenceIdGenerator sequenceIdGenerator,
   ILogger<DbProvider> logger)
    : ObjectDataRepository<MarketDataDbContext>(connectionSettings[MarketDataDbConnection], logger), IMarketDataDbContext
{
    public const string MarketDataDbConnection = "MarketDataDbConnection";
    const string FuturesTickByTimeProjection = "futures_tick_data_by_time";
    const string FuturesEodProjection = "futures_eod_data_by_month";
    const string VixFuturesContractIndexProjection = "vix_futures_contract_index";
    const int ProjectionWriteBatchSize = 256;
    const int TickAtomicBatchRowCount = 24;
    const int VixContractBucketCount = 32;
    const int ProjectionGuardScopeCount = 32;
    const string ProjectionGuardScopePrefix = "$guard:";
    const int ProjectionReadConcurrency = 8;
    const int ProjectionScopeStateReadBatchSize = 32;
    internal const int YieldCurveMaximumRangeDays = 3_660;
    internal const int YieldCurveMaximumRows = 5_000;
    internal const int YieldCurveMaximumYears = 200;
    const int YieldCurveLookupId = 1;
    readonly static Dictionary<TradingDaysKey, int> _tradingDaysMap = [];
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);
    readonly ISequenceIdGenerator _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);
    static NormalCurveTableReadModel? _normalCurveTable;

    // Deterministic integration-test seams for the two sides of the online-backfill
    // fence. They remain null in production and do not expose migration state publicly.
    internal Func<Func<Task>, Task>? TickProjectionGuardRegistrationForTestingAsync { get; set; }
    internal Func<Task>? TickProjectionGuardRegisteredForTestingAsync { get; set; }
    internal Func<Func<Task>, Task>? MaintainedProjectionScopeActivationForTestingAsync { get; set; }
    internal Func<Task>? MaintainedProjectionMutationSubmittingForTestingAsync { get; set; }
    internal Func<Task>? FuturesEodProjectionMonthSubmittingForTestingAsync { get; set; }
    internal Func<Func<Task>, Task>? ProjectionBackfillGlobalActivationForTestingAsync { get; set; }
    internal Func<Func<Task>, Task>? ProjectionBackfillScopeActivationForTestingAsync { get; set; }
    internal Func<Task>? ProjectionBackfillTargetMutationSubmittingForTestingAsync { get; set; }
    internal Func<Task>? ProjectionBackfillReconciledForTestingAsync { get; set; }

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override MarketDataDbContext Database => this;

    // InsertFuturesItiSignalAsync
    public IMarketDataDbReadContext DbReader => this;
    public IMarketDataDbWriteContext DbWriter => this;

    static int MapToYearMonth<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetInt(0);

    static string MapToString<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetString(0);

    static Guid MapToGuid<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetGuid(0);

    static bool MapToBoolean<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetBool(0);

    static void ValidateImportPolicy(ImportDuplicatePolicy duplicatePolicy, Guid commandId)
    {
        if (!Enum.IsDefined(duplicatePolicy))
            throw new ArgumentOutOfRangeException(nameof(duplicatePolicy));
        if (duplicatePolicy == ImportDuplicatePolicy.Reject && commandId == Guid.Empty)
            throw new ArgumentException("Reject imports require a durable command identity.", nameof(commandId));
    }

    static MarketDataImportOwnership MapToMarketDataImportOwnership<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord
        => new(row.GetGuid(0), row.GetBool(1));

    async Task EnsureImportOwnershipAsync(
        string dataset,
        string logicalKey,
        Guid commandId,
        bool logicalRowAlreadyExists)
    {
        var db = _dbFactory.MarketDataDb;
        var applied = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.ClaimMarketDataImportOwnership)}", MarketDataDbCql.ClaimMarketDataImportOwnership)
            .SetParameters(new ClaimMarketDataImportOwnership(
                dataset, logicalKey, commandId, !logicalRowAlreadyExists, DateTime.UtcNow))
            .ExecuteScalarAsync(MapToBoolean!);
        if (applied && !logicalRowAlreadyExists)
            return;

        var owner = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataImportOwnership)}", MarketDataDbCql.GetMarketDataImportOwnership)
            .SetParameters(new GetMarketDataImportOwnership(dataset, logicalKey))
            .ExecuteSingleAsync(MapToMarketDataImportOwnership!);
        if (owner is { CommandId: var ownerCommandId, MayWrite: true }
            && ownerCommandId == commandId)
            return;

        throw new MarketDataImportDuplicateException(
            $"A {dataset} row with logical key '{logicalKey}' is already owned by another import command.");
    }

    static MarketDataProjectionMutationData MapToProjectionMutation<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetGuid(0), e.GetDateTime(1));

    static MarketDataProjectionScopeMutationData MapToProjectionScopeMutation<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetString(0), e.GetString(1), e.GetGuid(2), e.GetDateTime(3));

    static VixFuturesContractIndexData MapToVixFuturesContractIndex<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetInt(0), e.GetString(1));

    static MarketDataProjectionStateData MapToProjectionState<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(e.GetString(0), e.GetGuid(1), e.GetBool(2));

    static MarketDataProjectionScopeStateData MapToProjectionScopeState<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => new(
            e.GetString(0),
            e.GetString(1),
            e.GetGuid(2),
            e.GetBool(3),
            e.GetBool(4),
            e.IsCollectionEmpty(5));

    static string MapToFuturesTickProjectionScope<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => GetFuturesTickScopeKey(e.GetString(0), e.GetDateOnly(1));

    static string MapToFuturesEodProjectionSourceScope<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => GetFuturesEodScopeKey(e.GetDateOnly(0));

    static string MapToFuturesEodProjectionTargetScope<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord
        => GetFuturesEodScopeKey(e.GetInt(0));

    static int ToYearMonth(DateOnly valueDate)
        => checked(valueDate.Year * 100 + valueDate.Month);

    static DateOnly GetMonthStart(int yearMonth)
        => new(yearMonth / 100, yearMonth % 100, 1);

    static DateOnly GetMonthEnd(int yearMonth)
    {
        var monthStart = GetMonthStart(yearMonth);
        if (monthStart.Year == DateOnly.MaxValue.Year && monthStart.Month == DateOnly.MaxValue.Month)
            return DateOnly.MaxValue;
        return monthStart.AddMonths(1).AddDays(-1);
    }

    static IEnumerable<int> GetYearMonths(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
            throw new ArgumentOutOfRangeException(nameof(startDate), startDate, "Start date must be on or before end date.");

        for (var month = new DateOnly(startDate.Year, startDate.Month, 1); month <= endDate;)
        {
            yield return ToYearMonth(month);
            if (month.Year == DateOnly.MaxValue.Year && month.Month == DateOnly.MaxValue.Month)
                yield break;
            month = month.AddMonths(1);
        }
    }

    static ulong GetFuturesTickIdentity(FuturesTickDataV2ReadModel row)
    {
        var hash = MarketDataProjectionHash.Start();
        hash = MarketDataProjectionHash.Add(hash, row.ContractId);
        hash = MarketDataProjectionHash.Add(hash, row.ValueDate);
        hash = MarketDataProjectionHash.Add(hash, row.TickId);
        hash = MarketDataProjectionHash.Add(hash, row.TickTime);
        hash = MarketDataProjectionHash.Add(hash, row.Price);
        return MarketDataProjectionHash.Add(hash, row.Size);
    }

    static ulong GetFuturesEodIdentity(FuturesEodDataV2ReadModel row)
    {
        var hash = MarketDataProjectionHash.Start();
        hash = MarketDataProjectionHash.Add(hash, row.ContractId);
        hash = MarketDataProjectionHash.Add(hash, row.ValueDate);
        hash = MarketDataProjectionHash.Add(hash, row.Symbol);
        hash = MarketDataProjectionHash.Add(hash, row.OpenPrice);
        hash = MarketDataProjectionHash.Add(hash, row.HighPrice);
        hash = MarketDataProjectionHash.Add(hash, row.LowPrice);
        hash = MarketDataProjectionHash.Add(hash, row.ClosePrice);
        hash = MarketDataProjectionHash.Add(hash, row.Volume);
        hash = MarketDataProjectionHash.Add(hash, row.DailyPercentChange);
        hash = MarketDataProjectionHash.Add(hash, row.DailyStdDev);
        hash = MarketDataProjectionHash.Add(hash, row.DailyStdDevAmount);
        hash = MarketDataProjectionHash.Add(hash, row.UpperBand);
        hash = MarketDataProjectionHash.Add(hash, row.Mean);
        hash = MarketDataProjectionHash.Add(hash, row.LowerBand);
        hash = MarketDataProjectionHash.Add(hash, (int)row.MarketDirection);
        hash = MarketDataProjectionHash.Add(hash, (int)row.MarketVolatility);
        hash = MarketDataProjectionHash.Add(hash, (int)row.PriceDirection);
        hash = MarketDataProjectionHash.Add(hash, (int)row.PriceVolatility);
        hash = MarketDataProjectionHash.Add(hash, row.MarketDirectionIndicator);
        hash = MarketDataProjectionHash.Add(hash, row.WindowSize);
        hash = MarketDataProjectionHash.Add(hash, row.FiftyDMA);
        return MarketDataProjectionHash.Add(hash, row.TwoHundredDMA);
    }

    internal static void EnsureDistinctFuturesTickWrites(
        ICollection<FuturesTickDataV2ReadModel> rows)
    {
        var canonicalKeys = new HashSet<(string ContractId, DateOnly ValueDate, long TickId)>(rows.Count);
        foreach (var row in rows)
        {
            if (!canonicalKeys.Add((row.ContractId, row.ValueDate, row.TickId)))
            {
                throw new ArgumentException(
                    $"The futures-tick write contains duplicate canonical key " +
                    $"('{row.ContractId}', '{row.ValueDate:yyyy-MM-dd}', {row.TickId}).",
                    nameof(rows));
            }
        }
    }

    internal static void EnsureDistinctFuturesEodWrites(
        ICollection<FuturesEodDataV2ReadModel> rows)
    {
        var canonicalKeys = new HashSet<(string ContractId, DateOnly ValueDate, string Symbol)>(rows.Count);
        foreach (var row in rows)
        {
            if (!canonicalKeys.Add((row.ContractId, row.ValueDate, row.Symbol)))
            {
                throw new ArgumentException(
                    $"The futures-EOD write contains duplicate canonical key " +
                    $"('{row.ContractId}', '{row.ValueDate:yyyy-MM-dd}', '{row.Symbol}').",
                    nameof(rows));
            }
        }
    }

    static ulong GetVixContractIdentity(string contractId)
        => GetVixContractIdentity(GetVixContractBucket(contractId), contractId);

    static ulong GetVixContractIdentity(int bucket, string contractId)
    {
        var hash = MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), bucket);
        return MarketDataProjectionHash.Add(hash, contractId);
    }

    static int GetVixContractBucket(string contractId)
        => (int)(MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), contractId) % VixContractBucketCount);

    static string GetFuturesTickScopeKey(string contractId, DateOnly valueDate)
        => string.Concat(
            contractId.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ":",
            contractId,
            ":",
            valueDate.DayNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

    static string GetFuturesEodScopeKey(DateOnly valueDate)
        => GetFuturesEodScopeKey(ToYearMonth(valueDate));

    static string GetFuturesEodScopeKey(int yearMonth)
        => yearMonth.ToString(System.Globalization.CultureInfo.InvariantCulture);

    static string GetVixContractIndexScopeKey(string contractId)
        => GetVixContractIndexScopeKey(GetVixContractBucket(contractId));

    static string GetVixContractIndexScopeKey(int bucket)
        => bucket.ToString(System.Globalization.CultureInfo.InvariantCulture);

    static string GetProjectionGuardScopeKey(string scopeKey)
        => string.Concat(
            ProjectionGuardScopePrefix,
            (MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), scopeKey) %
                ProjectionGuardScopeCount)
            .ToString(System.Globalization.CultureInfo.InvariantCulture));

    static bool IsProjectionGuardScopeKey(string scopeKey)
        => scopeKey.StartsWith(ProjectionGuardScopePrefix, StringComparison.Ordinal);

    static string[] GetProjectionGuardScopeKeys()
        => Enumerable.Range(0, ProjectionGuardScopeCount)
            .Select(bucket => string.Concat(
                ProjectionGuardScopePrefix,
                bucket.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();

    static string[] AddProjectionGuardScopes(IEnumerable<string> scopeKeys)
    {
        var dataScopes = scopeKeys.Distinct(StringComparer.Ordinal).ToArray();
        return dataScopes
            .Concat(dataScopes
                .Where(static scope => !IsProjectionGuardScopeKey(scope))
                .Select(GetProjectionGuardScopeKey))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    async Task<MarketDataProjectionStateData?> GetProjectionStateAsync(string projectionName)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionState)}", MarketDataDbCql.GetMarketDataProjectionState)
            .SetParameters(new GetMarketDataProjectionState(projectionName))
            .ExecuteSingleAsync<MarketDataProjectionStateData?>(
                static row => MapToProjectionState(row));

    async Task<bool> HasProjectionMutationAsync(string projectionName)
        => (await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMutation)}", MarketDataDbCql.GetMarketDataProjectionMutation)
            .SetParameters(new GetMarketDataProjectionMutation(projectionName))
            .ExecuteQueryAsync(MapToGuid)).Count != 0;

    async Task<Guid?> GetProjectionReadGenerationAsync(string projectionName)
    {
        var state = await GetProjectionStateAsync(projectionName);
        if (state is null || !state.Value.IsReady || await HasProjectionMutationAsync(projectionName))
            return null;
        return state.Value.Generation;
    }

    async Task<bool> IsProjectionReadGenerationValidAsync(string projectionName, Guid generation)
    {
        var state = await GetProjectionStateAsync(projectionName);
        return state is { IsReady: true } &&
            state.Value.Generation == generation &&
            !await HasProjectionMutationAsync(projectionName);
    }

    async Task<Dictionary<string, MarketDataProjectionScopeStateData?>> GetProjectionScopeStatesAsync(
        string projectionName,
        IReadOnlyList<string> scopeKeys)
    {
        var states = new Dictionary<string, MarketDataProjectionScopeStateData?>(
            scopeKeys.Count,
            StringComparer.Ordinal);
        for (var offset = 0; offset < scopeKeys.Count; offset += ProjectionScopeStateReadBatchSize)
        {
            var count = Math.Min(ProjectionScopeStateReadBatchSize, scopeKeys.Count - offset);
            var keys = scopeKeys.Skip(offset).Take(count).ToArray();
            foreach (var key in keys)
                states.Add(key, null);
            var values = await _dbFactory.MarketDataDb
                .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)}", MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)
                .SetParameters(new GetMarketDataProjectionScopeStatesV3(projectionName, keys))
                .ExecuteQueryAsync(MapToProjectionScopeState);
            foreach (var value in values)
                states[value.ScopeKey] = value;
        }
        return states;
    }

    async Task<MarketDataProjectionScopeReadStamp?> GetProjectionScopeReadStampAsync(
        string projectionName,
        IEnumerable<string> scopeKeys)
    {
        var scopes = AddProjectionGuardScopes(scopeKeys);
        var globalGeneration = await GetProjectionReadGenerationAsync(projectionName);
        if (globalGeneration is null)
            return null;

        var states = await GetProjectionScopeStatesAsync(projectionName, scopes);
        var generations = new MarketDataProjectionScopeGeneration[scopes.Length];
        for (var index = 0; index < scopes.Length; index++)
        {
            var state = states[scopes[index]];
            if (state is null)
            {
                // A globally ready projection plus a stable ready guard is a valid
                // negative cache entry. Any writer that creates this data scope also
                // changes its guard, and validation below rejects an appeared scope.
                if (IsProjectionGuardScopeKey(scopes[index]))
                    return null;
                generations[index] = new(scopes[index], Guid.Empty, IsMissing: true);
                continue;
            }
            if (!state.Value.CanRead)
                return null;
            generations[index] = new(scopes[index], state.Value.Generation, IsMissing: false);
        }

        return new(projectionName, globalGeneration.Value, generations);
    }

    async Task<bool> IsProjectionScopeReadStampValidAsync(MarketDataProjectionScopeReadStamp stamp)
    {
        if (!await IsProjectionReadGenerationValidAsync(
            stamp.ProjectionName,
            stamp.GlobalGeneration))
        {
            return false;
        }

        var states = await GetProjectionScopeStatesAsync(
            stamp.ProjectionName,
            stamp.Scopes.Select(static scope => scope.ScopeKey).ToArray());
        foreach (var scope in stamp.Scopes)
        {
            var state = states[scope.ScopeKey];
            if (scope.IsMissing)
            {
                if (state is not null)
                    return false;
                continue;
            }
            if (state is null || !state.Value.CanRead || state.Value.Generation != scope.Generation)
                return false;
        }
        return true;
    }

    async Task ExecuteMaintainedProjectionMutationAsync(
        string projectionName,
        IEnumerable<string> scopeKeys,
        Func<Task> mutation)
    {
        var scopes = AddProjectionGuardScopes(scopeKeys);
        if (scopes.Length == 0)
        {
            await mutation();
            return;
        }

        var globalGeneration = await GetProjectionReadGenerationAsync(projectionName);
        var initialStates = await GetProjectionScopeStatesAsync(projectionName, scopes);
        var restorableScopes = globalGeneration is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : scopes.Where(scope => initialStates[scope] is null || initialStates[scope]!.Value.CanRead)
                .ToHashSet(StringComparer.Ordinal);
        var mutationId = Guid.NewGuid();
        var activeOperations = new HashSet<Guid> { mutationId };
        var db = _dbFactory.MarketDataDb;
        var scopeActivationAcknowledged = false;
        var mutationSubmissionStarted = false;

        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
            .SetParameters(scopes.Select(scope => new InsertMarketDataProjectionScopeMutationV3(
                projectionName,
                scope,
                mutationId,
                DateTime.UtcNow)))
            .ExecuteCommandAsync();

        try
        {
            async Task ActivateScopesAsync()
                => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)
                    .SetParameters(scopes.Select(scope => new BeginMarketDataProjectionScopeOperationV3(
                        projectionName,
                        scope,
                        mutationId,
                        activeOperations)))
                    .ExecuteCommandAsync();

            if (MaintainedProjectionScopeActivationForTestingAsync is { } scopeActivation)
                await scopeActivation(ActivateScopesAsync);
            else
                await ActivateScopesAsync();
            scopeActivationAcknowledged = true;

            mutationSubmissionStarted = true;
            if (MaintainedProjectionMutationSubmittingForTestingAsync is { } mutationSubmitting)
                await mutationSubmitting();
            await mutation();

            var globalStillValid = globalGeneration.HasValue &&
                await IsProjectionReadGenerationValidAsync(projectionName, globalGeneration.Value);
            var scopesToEnd = new List<string>();
            foreach (var scopeBatch in scopes.Chunk(ProjectionReadConcurrency))
            {
                var completions = scopeBatch.Select(async scope =>
                {
                    if (!globalStillValid || !restorableScopes.Contains(scope))
                        return (Scope: scope, Completed: false);
                    var completed = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.CompleteMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.CompleteMarketDataProjectionScopeOperationV3)
                        .SetParameters(new CompleteMarketDataProjectionScopeOperationV3(
                            projectionName,
                            scope,
                            mutationId,
                            activeOperations,
                            DateTime.UtcNow,
                            activeOperations))
                        .ExecuteSingleAsync(MapToBoolean) == true;
                    return (Scope: scope, Completed: completed);
                }).ToArray();
                foreach (var completion in await Task.WhenAll(completions))
                {
                    if (!completion.Completed)
                        scopesToEnd.Add(completion.Scope);
                }
            }

            await EndProjectionScopeOperationsAsync(
                db,
                projectionName,
                scopesToEnd,
                activeOperations);

            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                .SetParameters(scopes.Select(scope => new DeleteMarketDataProjectionScopeMutationV3(
                    projectionName,
                    scope,
                    mutationId)))
                .ExecuteCommandAsync();
        }
        catch
        {
            if (!scopeActivationAcknowledged || mutationSubmissionStarted)
            {
                // A Begin or target mutation may have reached Scylla even when its
                // response is a timeout. Keep the original nonfailed journals and
                // active data/guard IDs so only an explicit cutoff after writers are
                // drained can recover without racing delayed server-side application.
                throw;
            }

            try
            {
                // A definitively acknowledged Begin can be classified without issuing
                // a racing End. The active ID remains paired with its failed journal
                // for exact removal by the next repair.
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)
                    .SetParameters(scopes.Select(scope => new FailMarketDataProjectionScopeMutationV3(
                        projectionName,
                        scope,
                        mutationId,
                        DateTime.UnixEpoch)))
                    .ExecuteCommandAsync();
            }
            catch
            {
                // An unclassified in-flight marker is never cleared automatically.
            }

            throw;
        }
    }

    static Task<long[]> EndProjectionScopeOperationsAsync(
        IObjectRepository db,
        string projectionName,
        IEnumerable<string> scopeKeys,
        HashSet<Guid> activeOperations,
        CancellationToken cancellationToken = default)
    {
        var scopes = scopeKeys as ICollection<string> ?? scopeKeys.ToArray();
        return scopes.Count == 0
            ? Task.FromResult(Array.Empty<long>())
            : db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.EndMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.EndMarketDataProjectionScopeOperationV3)
                .SetParameters(scopes.Select(scope => new EndMarketDataProjectionScopeOperationV3(
                    projectionName,
                    scope,
                    Guid.NewGuid(),
                    activeOperations)))
                .ExecuteCommandAsync(cancellationToken);
    }

    async Task ExecuteAtomicTickWriteAsync(
        string scopeKey,
        IReadOnlyCollection<InsertFuturesTickData> canonicalRows,
        IReadOnlyCollection<InsertFuturesTickDataByTime> projectionRows)
    {
        if (canonicalRows.Count == 0)
            return;
        if (canonicalRows.Count != projectionRows.Count || canonicalRows.Count > TickAtomicBatchRowCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(canonicalRows),
                canonicalRows.Count,
                $"Atomic tick writes require matching collections of at most {TickAtomicBatchRowCount} rows.");
        }

        await ExecuteGuardedAtomicTickMutationAsync(scopeKey, db =>
        [
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTickData)}", MarketDataDbCql.InsertFuturesTickData)
                .SetParameters(canonicalRows)
                .QueueCommand(),
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTickDataByTime)}", MarketDataDbCql.InsertFuturesTickDataByTime)
                .SetParameters(projectionRows)
                .QueueCommand(),
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.MarkMarketDataProjectionScopeAtomicWriteV3)}", MarketDataDbCql.MarkMarketDataProjectionScopeAtomicWriteV3)
                .SetParameters(new MarkMarketDataProjectionScopeAtomicWriteV3(
                    FuturesTickByTimeProjection,
                    scopeKey,
                    Guid.NewGuid()))
                .QueueCommand()
        ]);
    }

    async Task ExecuteGuardedAtomicTickMutationAsync(
        string scopeKey,
        Func<IObjectRepository, List<object>> createDataCommands)
    {
        var db = _dbFactory.MarketDataDb;
        var guardScopeKey = GetProjectionGuardScopeKey(scopeKey);
        var operationId = Guid.NewGuid();
        var activeOperations = new HashSet<Guid> { operationId };

        // This registration is deliberately a separate request before the data batch.
        // Set additions commute with a backfill claim, so an already-in-flight tick
        // cannot be hidden by scalar last-write-wins timestamps on the guard row.
        List<object> registrationCommands =
        [
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
                .SetParameters(new InsertMarketDataProjectionScopeMutationV3(
                    FuturesTickByTimeProjection,
                    guardScopeKey,
                    operationId,
                    DateTime.UtcNow))
                .QueueCommand(),
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.RegisterMarketDataProjectionGuardOperationV3)}", MarketDataDbCql.RegisterMarketDataProjectionGuardOperationV3)
                .SetParameters(new RegisterMarketDataProjectionGuardOperationV3(
                    FuturesTickByTimeProjection,
                    guardScopeKey,
                    activeOperations))
                .QueueCommand()
        ];
        try
        {
            async Task RegisterGuardAsync()
                => await db.ExecuteQueuedCommandsAsync(registrationCommands, useTransaction: true);

            if (TickProjectionGuardRegistrationForTestingAsync is { } registration)
                await registration(RegisterGuardAsync);
            else
                await RegisterGuardAsync();
        }
        catch
        {
            // A timed-out logged registration batch may still be replayed server-side.
            // Preserve its original journal timestamp so automatic recovery cannot
            // remove the marker before a delayed guard activation is applied.
            await TryClassifyTickGuardOperationFailureAsync(
                db,
                guardScopeKey,
                operationId,
                TickProjectionGuardFailureStage.RegistrationResponseUnknown);
            throw;
        }

        if (TickProjectionGuardRegisteredForTestingAsync is { } guardRegistered)
        {
            try
            {
                await guardRegistered();
            }
            catch
            {
                // No data request has started, so this operation is safe for automatic
                // recovery even if its registration response was delayed.
                await TryClassifyTickGuardOperationFailureAsync(
                    db,
                    guardScopeKey,
                    operationId,
                    TickProjectionGuardFailureStage.RegisteredBeforeDataSubmission);
                throw;
            }
        }

        try
        {
            await db.ExecuteQueuedCommandsAsync(createDataCommands(db), useTransaction: true);
        }
        catch
        {
            // A logged data batch timeout is ambiguous: batchlog replay can still apply
            // canonical data after this catch. Keep the original nonfailed journal row
            // and active guard ID. Only an explicit cutoff after writers are drained may
            // reclaim it; automatic failed-operation recovery would reopen a race.
            await TryClassifyTickGuardOperationFailureAsync(
                db,
                guardScopeKey,
                operationId,
                TickProjectionGuardFailureStage.DataBatchResponseUnknown);
            throw;
        }

        bool guardCompleted;
        try
        {
            guardCompleted = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.CompleteMarketDataProjectionGuardOperationV3)}", MarketDataDbCql.CompleteMarketDataProjectionGuardOperationV3)
                .SetParameters(new CompleteMarketDataProjectionGuardOperationV3(
                    FuturesTickByTimeProjection,
                    guardScopeKey,
                    Guid.NewGuid(),
                    activeOperations,
                    DateTime.UtcNow,
                    activeOperations))
                .ExecuteSingleAsync(MapToBoolean) == true;
        }
        catch
        {
            await TryClassifyTickGuardOperationFailureAsync(
                db,
                guardScopeKey,
                operationId,
                TickProjectionGuardFailureStage.AfterDataAcknowledged);
            throw;
        }

        if (guardCompleted)
        {
            try
            {
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                    .SetParameters(new DeleteMarketDataProjectionScopeMutationV3(
                        FuturesTickByTimeProjection,
                        guardScopeKey,
                        operationId))
                    .ExecuteCommandAsync();
            }
            catch
            {
                // The data and guard are committed. Retain a recoverable failed marker
                // instead of making the caller retry a successful tick write.
                await TryClassifyTickGuardOperationFailureAsync(
                    db,
                    guardScopeKey,
                    operationId,
                    TickProjectionGuardFailureStage.AfterDataAcknowledged);
            }
            return;
        }

        MarketDataProjectionScopeStateData? guardState;
        try
        {
            var states = await GetProjectionScopeStatesAsync(
                FuturesTickByTimeProjection,
                new[] { guardScopeKey });
            guardState = states[guardScopeKey];
        }
        catch
        {
            await TryClassifyTickGuardOperationFailureAsync(
                db,
                guardScopeKey,
                operationId,
                TickProjectionGuardFailureStage.AfterDataAcknowledged);
            return;
        }

        if (guardState is { Blocked: true })
        {
            // Backfill owns the guard. Leaving this ID active makes its conditional
            // release fail; the failed marker lets the next repair reclaim it exactly.
            await TryClassifyTickGuardOperationFailureAsync(
                db,
                guardScopeKey,
                operationId,
                TickProjectionGuardFailureStage.AfterDataAcknowledged);
            return;
        }

        // Another ordinary tick sharing this guard may have prevented the singleton
        // LWT. Its own data-scope generation still protects readers, so remove only
        // this operation and marker. Calls from one bulk write are serialized per guard
        // to keep this uncommon cross-process path off the normal fast path.
        try
        {
            List<object> cleanupCommands =
            [
                db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.RemoveMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.RemoveMarketDataProjectionScopeOperationV3)
                    .SetParameters(new RemoveMarketDataProjectionScopeOperationV3(
                        FuturesTickByTimeProjection,
                        guardScopeKey,
                        operationId))
                    .QueueCommand(),
                db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                    .SetParameters(new DeleteMarketDataProjectionScopeMutationV3(
                        FuturesTickByTimeProjection,
                        guardScopeKey,
                        operationId))
                    .QueueCommand()
            ];
            await db.ExecuteQueuedCommandsAsync(cleanupCommands, useTransaction: true);
        }
        catch
        {
            await TryClassifyTickGuardOperationFailureAsync(
                db,
                guardScopeKey,
                operationId,
                TickProjectionGuardFailureStage.AfterDataAcknowledged);
        }
    }

    internal static bool IsTickGuardFailureAutomaticallyRecoverable(
        TickProjectionGuardFailureStage stage)
        => stage is TickProjectionGuardFailureStage.RegisteredBeforeDataSubmission or
            TickProjectionGuardFailureStage.AfterDataAcknowledged;

    static Task TryClassifyTickGuardOperationFailureAsync(
        IObjectRepository db,
        string guardScopeKey,
        Guid operationId,
        TickProjectionGuardFailureStage stage)
        => IsTickGuardFailureAutomaticallyRecoverable(stage)
            ? TryFailTickGuardOperationAsync(db, guardScopeKey, operationId)
            : Task.CompletedTask;

    static async Task TryFailTickGuardOperationAsync(
        IObjectRepository db,
        string guardScopeKey,
        Guid operationId)
    {
        try
        {
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
                .SetParameters(new InsertMarketDataProjectionScopeMutationV3(
                    FuturesTickByTimeProjection,
                    guardScopeKey,
                    operationId,
                    DateTime.UnixEpoch))
                .ExecuteCommandAsync();
        }
        catch
        {
            // Never remove ambiguous recovery evidence after another storage failure.
        }
    }

    static Task<long[]> EndProjectionOperationAsync(
        IObjectRepository db,
        string projectionName,
        HashSet<Guid> activeOperations,
        CancellationToken cancellationToken = default)
        => db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.EndMarketDataProjectionOperation)}", MarketDataDbCql.EndMarketDataProjectionOperation)
            .SetParameters(new EndMarketDataProjectionOperation(
                projectionName,
                Guid.NewGuid(),
                activeOperations))
            .ExecuteCommandAsync(cancellationToken);

    public async Task<MarketDataProjectionReadiness> GetQueryProjectionReadinessAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(
            await GetProjectionScopeReadStampAsync(
                FuturesTickByTimeProjection,
                GetProjectionGuardScopeKeys()) is not null,
            await GetProjectionScopeReadStampAsync(
                FuturesEodProjection,
                GetProjectionGuardScopeKeys()) is not null,
            await GetProjectionScopeReadStampAsync(
                VixFuturesContractIndexProjection,
                GetProjectionGuardScopeKeys()) is not null,
            await GetProjectionScopeReadStampAsync(
                FuturesItiSignalQueryProjection,
                GetProjectionGuardScopeKeys()) is not null);
    }

    static FuturesDataId MapToFuturesDataId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1)
        );

    static FuturesTickDataV2ReadModel MapToFuturesTickData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            tickId: e.GetLong(2),
            tickTime: e.GetTimeOnly(3),
            price: e.GetDecimal(4),
            size: e.GetInt(5)
        );

    static FuturesTickDataId MapToFuturesTickDataId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            ContractId: e.GetString(0),
            ValueDate: e.GetDateOnly(1),
            TickId: e.GetLong(2)
        );

    static FuturesOptionTickDataV2ReadModel MapToFuturesOptionTickData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            tickId: e.GetLong(2),
            tickTime: e.GetTimeOnly(3),
            optionPrice: e.GetDouble(4),
            bidPrice: e.GetDouble(5),
            askPrice: e.GetDouble(6),
            bidSize: e.GetInt(7),
            askSize: e.GetInt(8),
            impliedVolatility: e.GetDouble(9),
            underlyingPrice: e.GetDouble(10),
            delta: e.GetDouble(11),
            gamma: e.GetDouble(12),
            vega: e.GetDouble(13),
            theta: e.GetDouble(14),
            rho: e.GetDouble(15)
        );

    static FuturesOptionTickDataV2ReadModel MapToFuturesOptionTickPriceData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
    => new(
        contractId: e.GetString(0),
        valueDate: e.GetDateOnly(1),
        tickId: e.GetLong(2),
        tickTime: e.GetTimeOnly(3),
        optionPrice: e.GetDouble(4),
        bidPrice: e.GetDouble(5),
        askPrice: e.GetDouble(6),
        bidSize: e.GetInt(7),
        askSize: e.GetInt(8),
        impliedVolatility: e.GetDouble(9),
        underlyingPrice: e.GetDouble(10),
        delta: e.GetDouble(11),
        gamma: e.GetDouble(12),
        vega: e.GetDouble(13),
        theta: e.GetDouble(14),
        rho: e.GetDouble(15)
    );

    static FuturesOptionTickDataId MapToFuturesOptionTickDataId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            ContractId: e.GetString(0),
            ValueDate: e.GetDateOnly(1),
            TickId: e.GetLong(2)
        );

    static FuturesBarDataReadModel MapToFuturesBarData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            symbol: e.GetString(1),
            valueDate: e.GetDateOnly(2),
            barDate: e.GetDateTime(3),
            barRateType: e.GetEnum<BarRateType>(4),
            barValue: e.GetDecimal(5),
            upTrendTrigger: e.GetDouble(6),
            downTrendTrigger: e.GetDouble(7)
        );

    static long MapToFuturesBarDataCount<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);

    static FuturesClosingPriceReadModel MapToFuturesClosingPrice<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            closingPrice: e.GetDecimal(2),
            createdOn: e.GetDateTime(3),
            createdBy: e.GetString(4)
        );

    static FuturesEodDataV2ReadModel MapToFuturesEodData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            symbol: e.GetString(2),
            openPrice: e.GetDecimal(3),
            highPrice: e.GetDecimal(4),
            lowPrice: e.GetDecimal(5),
            closePrice: e.GetDecimal(6),
            volume: e.GetLong(7),
            dailyPercentChange: e.GetDouble(8),
            dailyStdDev: e.GetDouble(9),
            dailyStdDevAmount: e.GetDouble(10),
            upperBand: e.GetDouble(11),
            mean: e.GetDouble(12),
            lowerBand: e.GetDouble(13),
            marketDirection: e.GetEnum<MarketDirectionType>(14),
            marketVolatility: e.GetEnum<MarketVolatilityType>(15),
            priceDirection: e.GetEnum<PriceDirectionType>(16),
            priceVolatility: e.GetEnum<PriceVolatilityType>(17),
            marketDirectionIndicator: e.GetDouble(18),
            windowSize: e.GetInt(19),
            fiftyDMA: e.IsNull(20) ? 0m : e.GetDecimal(20),
            twoHundredDMA: e.IsNull(21) ? 0m : e.GetDecimal(21)
        );

    static FuturesIntraDayDataReadModel MapToFuturesIntraDayData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            sequenceId: e.GetLong(2),
            symbol: e.GetString(3),
            openPrice: e.GetDecimal(4),
            highPrice: e.GetDecimal(5),
            lowPrice: e.GetDecimal(6),
            closePrice: e.GetDecimal(7),
            volume: e.GetLong(8),
            dailyPercentChange: e.GetDouble(9),
            dailyStdDev: e.GetDouble(10),
            dailyStdDevAmount: e.GetDouble(11),
            upperBand: e.GetDouble(12),
            mean: e.GetDouble(13),
            lowerBand: e.GetDouble(14),
            marketDirection: e.GetEnum<MarketDirectionType>(15),
            marketVolatility: e.GetEnum<MarketVolatilityType>(16),
            priceDirection: e.GetEnum<PriceDirectionType>(17),
            priceVolatility: e.GetEnum<PriceVolatilityType>(18),
            marketDirectionIndicator: e.GetInt(19),
            windowSize: e.GetInt(20)
         );

    static FuturesEodClosingPriceReadModel MapToFuturesEodClosingPrice<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            closingPrice: e.GetDecimal(2)
        );

    static FuturesTickHLVDataReadModel MapToFuturesTickHLVData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            ContractId: e.GetString(0),
            ValueDate: e.GetDateOnly(1),
            HighPrice: e.GetDecimal(2),
            LowPrice: e.GetDecimal(3),
            Volume: e.GetLong(4)
        );

    static FuturesItiSignalV2ReadModel MapToFuturesItiSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TimeFrameType>(2),
            sequenceId: e.GetLong(3),
            intrinsicTime: e.GetDateTime(4),
            intrinsicTimeGroupId: e.GetInt(5),
            intrinsicTimeLength: e.GetDouble(6),
            intrinsicPrice: e.GetDouble(7),
            intrinsicTimeTrend: e.GetEnum<IntrinsicTimeTrendType>(8),
            intrinsicTimeMode: e.GetEnum<IntrinsicTimeModeType>(9),
            trendPrice: e.GetDouble(10),
            trendExtreme: e.GetDouble(11),
            trendReversal: e.GetDouble(12),
            trendDelta: e.GetDouble(13),
            targetDelta: e.GetDouble(14),
            lambda: e.GetDouble(15),
            tradingDays: e.GetInt(16),
            threshold: e.GetDouble(17),
            upTrendTrigger: e.GetDouble(18),
            downTrendTrigger: e.GetDouble(19),
            tradeState: e.GetEnum<IntrinsicTimeTradeState>(20),
            bandLevel: e.GetDouble(21),
            reversalLevel: e.GetDouble(22)
        );

    static FuturesItiSignalMDIV2ReadModel MapToFuturesItiSignalMDI<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            intrinsicTime: e.GetDateTime(2),
            trendType: e.GetEnum<IntrinsicTimeTrendType>(3),
            mdi: e.GetDouble(4)
        );

    static DateOnly MapToMaxValueDate<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDateOnly(0);

    static int MapToMaxIntrinsicTimeGroupId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetInt(0);

    static FuturesItiTrendDeltaDataReadModel MapToFuturesItiTrendDeltaData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timestamp: e.GetDateTime(2),
            sequenceId: e.GetLong(3),
            trendDelta: e.GetFloat(4),
            trendDirection: e.GetFloat(5),
            trendDirectionMode: e.GetInt(6),
            futuresPrice: e.GetFloat(7),
            trendExtreme: e.GetFloat(8),
            futuresRsi: e.GetFloat(9)
        );

    static FuturesItiTrendClassDataReadModel MapToFuturesItiTrendClassData(IObjectDataRecord e)
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timestamp: e.GetDateTime(2),
            sequenceId: e.GetLong(3),
            trendClass: e.GetFloat(4),
            trendDirection: e.GetFloat(5),
            trendDirectionMode: e.GetInt(6),
            trendDelta: e.GetFloat(7),
            futuresRsi: e.GetFloat(8)
        );

    static FuturesItiSignalV2ReadModel MapToFuturesItiTimeFrameState<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TimeFrameType>(2),
            sequenceId: e.GetLong(3),
            intrinsicTime: e.GetDateTime(4),
            intrinsicTimeGroupId: e.GetInt(5),
            intrinsicTimeLength: e.GetDouble(6),
            intrinsicPrice: e.GetDouble(7),
            intrinsicTimeTrend: e.GetEnum<IntrinsicTimeTrendType>(8),
            intrinsicTimeMode: e.GetEnum<IntrinsicTimeModeType>(9),
            trendPrice: e.GetDouble(10),
            trendExtreme: e.GetDouble(11),
            trendReversal: e.GetDouble(12),
            trendDelta: e.GetDouble(13),
            targetDelta: e.GetDouble(14),
            lambda: e.GetDouble(15),
            tradingDays: e.GetInt(16),
            threshold: e.GetDouble(17),
            upTrendTrigger: e.GetDouble(18),
            downTrendTrigger: e.GetDouble(19),
            tradeState: e.GetEnum<IntrinsicTimeTradeState>(20),
            timeFrameStartValueDate: e.GetDateOnly(21),
            bandAnchorPrice: e.GetDouble(22),
            bandPercentage: e.GetDouble(23),
            bandSize: e.GetDouble(24),
            bandLevel: e.GetDouble(25),
            reversalLevel: e.GetDouble(26));

    static FuturesItiTrendDeltaModelReadModel MapToFuturesItiTrendDeltaModel<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            startDate: e.GetDateOnly(2),
            endDate: e.GetDateOnly(3),
            count: e.GetInt(4),
            maximum: e.GetDouble(5),
            mean: e.GetDouble(6),
            median: e.GetDouble(7),
            minimum: e.GetDouble(8),
            skewness: e.GetDouble(9),
            stdDev: e.GetDouble(10),
            variance: e.GetDouble(11),
            meanAbsoluteError: e.GetDouble(12),
            meanSquaredError: e.GetDouble(13),
            rootMeanSquaredError: e.GetDouble(14),
            lossFunction: e.GetDouble(15),
            rSquared: e.GetDouble(16),
            modelData: e.GetBytes(17)
        );

    static FuturesItiTrendClassModelReadModel MapToFuturesItiTrendClassModel<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            startDate: e.GetDateOnly(2),
            endDate: e.GetDateOnly(3),
            count: e.GetInt(4),
            maximum: e.GetDouble(5),
            mean: e.GetDouble(6),
            median: e.GetDouble(7),
            minimum: e.GetDouble(8),
            skewness: e.GetDouble(9),
            stdDev: e.GetDouble(10),
            variance: e.GetDouble(11),
            accuracy: e.GetDouble(12),
            areaUnderPrecisionRecallCurve: e.GetDouble(13),
            areaUnderRocCurve: e.GetDouble(14),
            entropy: e.GetDouble(15),
            f1Score: e.GetDouble(16),
            modelData: e.GetBytes(17)
        );

    static double MapToRsi<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDouble(0);

    static long MapToMaxSequenceId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);

    static FuturesTdiSignalReadModel MapToFuturesTdiSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new()
        {
            ContractId = e.GetString(0),
            ValueDate = e.GetDateOnly(1),
            TimePeriod = e.GetEnum<TimeFrameType>(2),
            Timestamp = e.GetTimeOnly(3),
            SchemaVersion = e.GetInt(4),
            ConfigurationId = e.GetString(5),
            RsiPeriod = e.GetInt(6),
            PriceLinePeriod = e.GetInt(7),
            SignalLinePeriod = e.GetInt(8),
            MarketBasePeriod = e.GetInt(9),
            VolatilityBandPeriod = e.GetInt(10),
            VolatilityBandDeviation = e.GetDouble(11),
            Price = e.GetDecimal(12),
            Rsi = e.GetDouble(13),
            PriceLine = e.GetDouble(14),
            SignalLine = e.GetDouble(15),
            MarketBaseLine = e.GetDouble(16),
            UpperVolatilityBand = e.GetDouble(17),
            LowerVolatilityBand = e.GetDouble(18),
            BandWidth = e.GetDouble(19),
            PriceSignalDivergence = e.GetDouble(20),
            Cross = e.GetEnum<FuturesTdiCrossType>(21),
            MarketState = e.GetEnum<FuturesTdiMarketStateType>(22),
            TDI = e.GetEnum<FuturesTrendDirectionType>(23),
            TDIStrength = e.GetEnum<FuturesTrendDirectionStrengthType>(24),
            SourceSequence = e.GetLong(25),
            SourceEventTimestamp = e.GetDateTime(26)
        };

    static FuturesMacdSignalReadModel MapToFuturesMacdSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TimeFrameType>(2),
            signalEmaPeriod: e.GetInt(3),
            fastEmaPeriod: e.GetInt(4),
            slowEmaPeriod: e.GetInt(5),
            timestamp: e.GetTimeOnly(6),
            futuresPrice: e.GetDecimal(7),
            fastEma: e.GetDouble(8),
            slowEma: e.GetDouble(9),
            macdLine: e.GetDouble(10),
            signalLine: e.GetDouble(11),
            histogram: e.GetDouble(12),
            macd: e.GetEnum<FuturesTrendDirectionType>(13),
            macdStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(14)
        );

    static FuturesAtrSignalReadModel MapToFuturesAtrSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
    {
        var signal = new FuturesAtrSignalReadModel(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TimeFrameType>(2),
            periodLength:e.GetInt(3),
            timestamp: e.GetTimeOnly(4),
            futuresPrice: e.GetDecimal(5),
            atrValue: e.GetDouble(6),
            trueRange: e.GetDouble(7),
            atr: e.GetEnum<FuturesTrendDirectionType>(8),
            atrStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(9)
        )
        {
            PreviousAtrValue = e.IsNull(10) ? null : e.GetDouble(10),
            AtrBaseline = e.IsNull(11) ? null : e.GetDouble(11),
            AtrRatio = e.IsNull(12) ? null : e.GetDouble(12),
            IsWarm = !e.IsNull(13) && e.GetBool(13)
        };
        if (e.IsNull(14))
            return signal;

        var marketDataAsOf = new DateTimeOffset(
            DateTime.SpecifyKind(e.GetDateTime(16), DateTimeKind.Utc));
        return signal with
        {
            Metadata = new MarketAnalyticsSignalMetadata
            {
                SignalKey = new(
                    MarketSeriesIdentity.ForContract(signal.ContractId),
                    MarketAnalyticsSignalKind.Atr,
                    signal.TimePeriod,
                    e.GetString(14)),
                ContractId = signal.ContractId,
                ValueDate = signal.ValueDate,
                ObservationId = new FuturesTradeSessionBarId(e.GetGuid(15)),
                MarketDataAsOfUtc = marketDataAsOf,
                CalculatedAtUtc = marketDataAsOf,
                SourceSequence = e.GetLong(17),
                CalculationVersion = e.GetString(18),
                CalculationMethod = e.GetEnum<MarketSignalCalculationMethod>(19),
                SchemaVersion = checked((ushort)e.GetInt(20)),
                IsValid = e.GetBool(21)
            }
        };
    }

    static FuturesAdxSignalReadModel MapToFuturesAdxSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
    {
        var signal = new FuturesAdxSignalReadModel(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TimeFrameType>(2),
            periodLength: e.GetInt(3),
            timestamp: e.GetTimeOnly(4),
            futuresPrice: e.GetDecimal(5),
            plusDI: e.GetDouble(6),
            minusDI: e.GetDouble(7),
            adxValue: e.GetDouble(8),
            adx: e.GetEnum<FuturesTrendDirectionType>(9),
            adxStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(10)
        );
        if (e.IsNull(11))
            return signal;

        var marketDataAsOf = new DateTimeOffset(
            DateTime.SpecifyKind(e.GetDateTime(13), DateTimeKind.Utc));
        return signal with
        {
            Metadata = new MarketAnalyticsSignalMetadata
            {
                SignalKey = new(
                    MarketSeriesIdentity.ForContract(signal.ContractId),
                    MarketAnalyticsSignalKind.Adx,
                    signal.TimePeriod,
                    e.GetString(11)),
                ContractId = signal.ContractId,
                ValueDate = signal.ValueDate,
                ObservationId = new FuturesTradeSessionBarId(e.GetGuid(12)),
                MarketDataAsOfUtc = marketDataAsOf,
                CalculatedAtUtc = marketDataAsOf,
                SourceSequence = e.GetLong(14),
                CalculationVersion = e.GetString(15),
                CalculationMethod = e.GetEnum<MarketSignalCalculationMethod>(16),
                SchemaVersion = checked((ushort)e.GetInt(17)),
                IsValid = e.GetBool(18)
            }
        };
    }

    static FuturesTradeSignalV2ReadModel MapToFuturesTradeSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TimeFrameType>(2),
            sequenceId: e.GetLong(3),
            timestamp: e.GetTimeOnly(4),
            mean: e.GetDouble(5),
            stdDev: e.GetDouble(6),
            futuresPrice: e.GetDouble(7),
            priceChangePercent: e.GetDouble(8),
            fundRiskPercent: e.GetDouble(9),
            rsi: e.GetDouble(10),
            rsiSlope: e.GetDouble(11),
            trendType: e.GetEnum<FuturesTrendType>(12),
            trendStrength: e.GetEnum<FuturesTrendStrengthType>(13),
            tradeSignal: e.GetEnum<TradeSignalType>(14),
            tdi: e.GetEnum<FuturesTrendDirectionType>(15),
            tdiStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(16),
            mdi: e.GetDouble(17),
            mdiTrend: e.GetEnum<FuturesMDITrendType>(18),
            mdiUpTrendLimit: e.GetDouble(19),
            mdiDownTrendLimit: e.GetDouble(20),
            upTrendingTrigger: e.GetDouble(21),
            downTrendingTrigger: e.GetDouble(22),
            entryTrigger: e.GetDouble(23),
            exitTrigger: e.GetDouble(24),
            trendDelta: e.GetDouble(25),
            trendExtreme: e.GetDouble(26),
            trendReversal: e.GetDouble(27),
            fiftyDMA: e.GetDecimal(28),
            twoHundredDMA: e.GetDecimal(29),
            tradeExecuteState: e.GetEnum<TradeExecuteState>(30)
        );

    static RateOfReturnReadModel MapToRateOfReturn<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            symbol: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            rateOfReturn: e.GetDouble(2)
        );

    static VixFuturesEodDataReadModel MapToVixFuturesEodData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            openPrice: e.GetDecimal(2),
            highPrice: e.GetDecimal(3),
            lowPrice: e.GetDecimal(4),
            closePrice: e.GetDecimal(5),
            volume: e.GetLong(6)
        );

    static InsertFuturesEodData CreateFuturesEodDataParameters(FuturesEodDataV2ReadModel e, decimal openPrice)
        => new(
            contractId: e.ContractId,
            valueDate: e.ValueDate,
            symbol: e.Symbol,
            openPrice,
            highPrice: e.HighPrice,
            lowPrice: e.LowPrice,
            closePrice: e.ClosePrice,
            volume: e.Volume,
            dailyPercentChange: e.DailyPercentChange,
            dailyStdDev: e.DailyStdDev,
            dailyStdDevAmount: e.DailyStdDevAmount,
            upperBand: e.UpperBand,
            mean: e.Mean,
            lowerBand: e.LowerBand,
            marketDirection: e.MarketDirection.ToStringFast(),
            marketVolatility: e.MarketVolatility.ToStringFast(),
            priceDirection: e.PriceDirection.ToStringFast(),
            priceVolatility: e.PriceVolatility.ToStringFast(),
            marketDirectionIndicator: e.MarketDirectionIndicator,
            windowSize: e.WindowSize,
            fiftyDMA: e.FiftyDMA,
            twoHundredDMA: e.TwoHundredDMA);

    static InsertFuturesEodDataByMonth CreateFuturesEodDataByMonthParameters(FuturesEodDataV2ReadModel e, decimal openPrice)
        => new(
            yearMonth: ToYearMonth(e.ValueDate),
            contractId: e.ContractId,
            valueDate: e.ValueDate,
            symbol: e.Symbol,
            openPrice,
            highPrice: e.HighPrice,
            lowPrice: e.LowPrice,
            closePrice: e.ClosePrice,
            volume: e.Volume,
            dailyPercentChange: e.DailyPercentChange,
            dailyStdDev: e.DailyStdDev,
            dailyStdDevAmount: e.DailyStdDevAmount,
            upperBand: e.UpperBand,
            mean: e.Mean,
            lowerBand: e.LowerBand,
            marketDirection: e.MarketDirection.ToStringFast(),
            marketVolatility: e.MarketVolatility.ToStringFast(),
            priceDirection: e.PriceDirection.ToStringFast(),
            priceVolatility: e.PriceVolatility.ToStringFast(),
            marketDirectionIndicator: e.MarketDirectionIndicator,
            windowSize: e.WindowSize,
            fiftyDMA: e.FiftyDMA,
            twoHundredDMA: e.TwoHundredDMA);

    async Task UpsertFuturesEodProjectionAsync(FuturesEodDataV2ReadModel e, decimal openPrice)
    {
        var db = _dbFactory.MarketDataDb;
        var yearMonth = ToYearMonth(e.ValueDate);
        List<object> commands =
        [
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataByMonth)}", MarketDataDbCql.InsertFuturesEodDataByMonth)
                .SetParameters(CreateFuturesEodDataByMonthParameters(e, openPrice))
                .QueueCommand(),
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                .SetParameters(new InsertMarketDataProjectionMonth(FuturesEodProjection, yearMonth))
                .QueueCommand()
        ];
        await db.ExecuteQueuedCommandsAsync(commands);
    }

    async Task UpsertVixFuturesContractIndexAsync(string contractId)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertVixFuturesContractIndex)}", MarketDataDbCql.InsertVixFuturesContractIndex)
            .SetParameters(new InsertVixFuturesContractIndex(
                GetVixContractBucket(contractId),
                contractId))
            .ExecuteCommandAsync();

    async Task InsertFuturesEodBatchAsync(ICollection<FuturesEodDataV2ReadModel> batch)
    {
        if (batch.Count == 0)
            return;

        EnsureDistinctFuturesEodWrites(batch);
        await ExecuteMaintainedProjectionMutationAsync(
            FuturesEodProjection,
            batch.Select(static e => GetFuturesEodScopeKey(e.ValueDate)),
            async () =>
        {
            var db = _dbFactory.MarketDataDb;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodData)}", MarketDataDbCql.InsertFuturesEodData)
                .SetParameters(batch.Select(e => CreateFuturesEodDataParameters(e, e.OpenPrice)))
                .ExecuteCommandAsync();
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataByMonth)}", MarketDataDbCql.InsertFuturesEodDataByMonth)
                .SetParameters(batch.Select(e => CreateFuturesEodDataByMonthParameters(e, e.OpenPrice)))
                .ExecuteCommandAsync();

            var projectionMonths = batch.Select(e => ToYearMonth(e.ValueDate)).Distinct().ToArray();
            if (FuturesEodProjectionMonthSubmittingForTestingAsync is { } projectionMonthSubmitting)
                await projectionMonthSubmitting();
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                .SetParameters(projectionMonths.Select(yearMonth =>
                    new InsertMarketDataProjectionMonth(FuturesEodProjection, yearMonth)))
                .ExecuteCommandAsync();
        });
    }

    async Task<FuturesEodDataV2ReadModel?> ReadLegacyCurrentFuturesEodDataAsync(DateOnly valueDate)
    {
        FuturesEodDataV2ReadModel? latest = null;
        await foreach (var row in _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataAll)}", MarketDataDbCql.GetFuturesEodDataAll)
            .ExecuteStreamAsync(MapToFuturesEodData!))
        {
            if (row.ValueDate > valueDate)
                continue;
            if (latest is null ||
                row.ValueDate > latest.ValueDate ||
                row.ValueDate == latest.ValueDate && string.CompareOrdinal(row.ContractId, latest.ContractId) < 0 ||
                row.ValueDate == latest.ValueDate && row.ContractId == latest.ContractId &&
                    string.CompareOrdinal(row.Symbol, latest.Symbol) < 0)
            {
                latest = row;
            }
        }

        return latest;
    }

    async Task<ICollection<FuturesEodDataV2ReadModel>> ReadLegacyFuturesEodDataByMonthsAsync(
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlySet<int> yearMonths)
    {
        List<FuturesEodDataV2ReadModel> results = [];
        await foreach (var row in _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataAll)}", MarketDataDbCql.GetFuturesEodDataAll)
            .ExecuteStreamAsync(MapToFuturesEodData!))
        {
            if (row.ValueDate >= startDate &&
                row.ValueDate <= endDate &&
                yearMonths.Contains(ToYearMonth(row.ValueDate)))
                results.Add(row);
        }

        return results;
    }

    async Task<ICollection<VixFuturesEodDataReadModel>> ReadLegacyVixFuturesEodDataByValueDateAsync(
        DateOnly valueDate)
    {
        List<VixFuturesEodDataReadModel> results = [];
        await foreach (var row in _dbFactory.MarketDataDb.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodDataAll)}", MarketDataDbCql.GetVixFuturesEodDataAll)
            .ExecuteStreamAsync(MapToVixFuturesEodData))
        {
            if (row.ValueDate <= valueDate)
                results.Add(row);
        }

        return [.. results
            .OrderByDescending(static row => row.ValueDate)
            .ThenBy(static row => row.ContractId, StringComparer.Ordinal)];
    }

    async Task<ICollection<VixFuturesEodDataReadModel>> ReadIndexedVixFuturesEodDataByValueDateAsync(
        DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        HashSet<string> contractIds = new(StringComparer.Ordinal);
        for (var firstBucket = 0; firstBucket < VixContractBucketCount; firstBucket += ProjectionReadConcurrency)
        {
            var bucketCount = Math.Min(ProjectionReadConcurrency, VixContractBucketCount - firstBucket);
            var bucketReads = Enumerable.Range(firstBucket, bucketCount)
                .Select(async bucket => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesContractIds)}", MarketDataDbCql.GetVixFuturesContractIds)
                    .SetParameters(new GetVixFuturesContractIds(bucket))
                    .ExecuteQueryAsync(MapToString))
                .ToArray();
            foreach (var bucket in await Task.WhenAll(bucketReads))
                contractIds.UnionWith(bucket);
        }

        List<VixFuturesEodDataReadModel> results = [];
        var orderedContractIds = contractIds.Order(StringComparer.Ordinal).ToArray();
        for (var offset = 0; offset < orderedContractIds.Length; offset += ProjectionReadConcurrency)
        {
            var count = Math.Min(ProjectionReadConcurrency, orderedContractIds.Length - offset);
            var contractReads = orderedContractIds.AsSpan(offset, count).ToArray()
                .Select(async contractId => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodDataThroughDate)}", MarketDataDbCql.GetVixFuturesEodDataThroughDate)
                    .SetParameters(new GetVixFuturesEodDataThroughDate(contractId, valueDate))
                    .ExecuteQueryAsync(MapToVixFuturesEodData))
                .ToArray();
            foreach (var rows in await Task.WhenAll(contractReads))
                results.AddRange(rows);
        }

        return [.. results
            .OrderByDescending(static row => row.ValueDate)
            .ThenBy(static row => row.ContractId, StringComparer.Ordinal)];
    }

    static long MapToMinTickId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);

    static decimal MapToPrice<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDecimal(0);

    static YieldCurveRateReadModel MapToYieldCurveRate<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            valueDate: e.GetDateOnly(0),
            oneMonth: e.GetDouble(1),
            twoMonth: e.GetDouble(2),
            threeMonth: e.GetDouble(3),
            sixMonth: e.GetDouble(4),
            oneYear: e.GetDouble(5),
            twoYear: e.GetDouble(6),
            threeYear: e.GetDouble(7),
            fiveYear: e.GetDouble(8),
            sevenYear: e.GetDouble(9),
            tenYear: e.GetDouble(10),
            twentyYear: e.GetDouble(11),
            thirtyYear: e.GetDouble(12)
        );

    static MarketHolidayReadModel MapToMarketHoliday<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            currencyType: e.GetEnum<CurrencyType>(0),
            holidayDate: e.GetDateOnly(1),
            description: e.GetString(2)
        );

    static FuturesItiSignalAverageInfoDataModel MapToFuturesItiSignalAverageInfo<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            predictedDelta: e.GetDouble(2),
            futuresRSI: e.GetDouble(3)
        );

    static double MapToAveragePredictedDelta<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDouble(0);

    static NormalCurveDataReadModel MapToNormalCurveData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
    => new(
        stdDevIndex: e.GetInt(0),
        percent: e.GetDouble(1)
    );

    static FuturesTradeSignalId MapToFuturesTradeSignalId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TimeFrameType>(2),
            sequenceId: e.GetLong(3)
        );

    static FuturesRsiSignalReadModel MapToFuturesRsiSignal<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            valueDate: e.GetDateOnly(1),
            timePeriod: e.GetEnum<TimeFrameType>(2),
            periodLength: e.GetInt(3),
            timestamp: e.GetTimeOnly(4),
            price: e.GetDecimal(5),
            priceChange: e.GetDecimal(6),
            priceGain: e.GetDecimal(7),
            priceLoss: e.GetDecimal(8),
            averagePriceGain: e.GetDecimal(9),
            averagePriceLoss: e.GetDecimal(10),
            rs: e.GetDouble(11),
            rsi: e.GetDouble(12),
            rsiAverage: e.GetDouble(13),
            rsiSlope: e.GetDouble(14),
            sourceSequence: e.GetLong(15),
            sourceEventTimestamp: e.GetDateTime(16)
        );

    static FuturesContractV2ReadModel MapToFuturesContract(IObjectDataRecord o)
            => new(
               o.GetString(0),
               o.GetString(1),
               o.GetString(2),
               o.GetString(3),
               o.GetString(4),
               o.GetString(5),
               o.GetString(6),
               o.GetString(7),
               o.GetDateOnly(8),
               o.GetBool(9));

    static TradeLiveFeedReadModel MapToTradeLiveFeed<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            tradeLiveFeedState: e.GetEnum<TradeLiveFeedStateType>(2)
        );

    /// <summary>
    /// Deletes a futures bar data record from the database.
    /// </summary>
    /// <param name="e">The identifier of the futures bar data to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteFuturesBarDataAsync(FuturesBarDataId e)
        => await _dbFactory.MarketDataDb
                .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesBarData)}", MarketDataDbCql.DeleteFuturesBarData)
                .SetParameters(new DeleteFuturesBarData(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate))
                .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously deletes the closing price entry for the specified futures contract on the given date.
    /// </summary>
    /// <remarks>If no closing price exists for the specified contract and date, no action is taken. Ensure
    /// that the contract identifier and date correspond to an existing entry to avoid unnecessary operations.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract for which to delete the closing price.</param>
    /// <param name="valueDate">The date of the closing price to delete. Must be a valid date.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesClosingPriceAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
                .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesClosingPrice)}", MarketDataDbCql.DeleteFuturesClosingPrice)
                .SetParameters(new DeleteFuturesClosingPrice(contractId, valueDate))
                .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures EOD data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task DeleteFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        await ExecuteMaintainedProjectionMutationAsync(
            FuturesEodProjection,
            new[] { GetFuturesEodScopeKey(valueDate) },
            async () =>
        {
            var db = _dbFactory.MarketDataDb;
            List<object> commands =
            [
                db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesEodData)}", MarketDataDbCql.DeleteFuturesEodData)
                    .SetParameters(new DeleteFuturesEodData(contractId, valueDate))
                    .QueueCommand(),
                db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesEodDataByMonth)}", MarketDataDbCql.DeleteFuturesEodDataByMonth)
                    .SetParameters(new DeleteFuturesEodDataByMonth(ToYearMonth(valueDate), valueDate, contractId))
                    .QueueCommand()
            ];
            await db.ExecuteQueuedCommandsAsync(commands);
        });
    }

    /// <summary>
    /// Asynchronously deletes all futures tick data for the specified contract and value date from the market data
    /// database.
    /// </summary>
    /// <remarks>Ensure that the specified contract identifier and value date are valid before calling this
    /// method. This operation is irreversible and will remove all tick data for the given contract and date.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract whose tick data is to be deleted. Cannot be null or empty.</param>
    /// <param name="valueDate">The date for which the futures tick data should be deleted.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesTickDataAsync(string contractId, DateOnly valueDate)
    {
        var scopeKey = GetFuturesTickScopeKey(contractId, valueDate);
        await ExecuteGuardedAtomicTickMutationAsync(scopeKey, db =>
        [
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesTickData)}", MarketDataDbCql.DeleteFuturesTickData)
                .SetParameters(new DeleteFuturesTickData(contractId, valueDate))
                .QueueCommand(),
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesTickDataByTime)}", MarketDataDbCql.DeleteFuturesTickDataByTime)
                .SetParameters(new DeleteFuturesTickDataByTime(contractId, valueDate))
                .QueueCommand(),
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.MarkMarketDataProjectionScopeAtomicWriteV3)}", MarketDataDbCql.MarkMarketDataProjectionScopeAtomicWriteV3)
                .SetParameters(new MarkMarketDataProjectionScopeAtomicWriteV3(
                    FuturesTickByTimeProjection,
                    scopeKey,
                    Guid.NewGuid()))
                .QueueCommand()
        ]);
    }

    /// <summary>
    /// Deletes a trade live feed record from the database.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteTradeLiveFeedAsync(int orderId, int tradeId)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteTradeLiveFeed)}", MarketDataDbCql.DeleteTradeLiveFeed)
            .SetParameters(new DeleteTradeLiveFeed(orderId, tradeId))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures tick data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task DeleteVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        await ExecuteMaintainedProjectionMutationAsync(
            VixFuturesContractIndexProjection,
            new[] { GetVixContractIndexScopeKey(contractId) },
            async () =>
        {
            var db = _dbFactory.MarketDataDb;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteVixFuturesEodData)}", MarketDataDbCql.DeleteVixFuturesEodData)
                .SetParameters(new DeleteVixFuturesEodData(contractId, valueDate))
                .ExecuteCommandAsync();

            var remaining = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastVixFuturesEodData)}", MarketDataDbCql.GetLastVixFuturesEodData)
                .SetParameters(new GetLastVixFuturesEodData(contractId, DateOnly.MaxValue))
                .ExecuteSingleAsync(MapToVixFuturesEodData);
            if (remaining is null)
            {
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteVixFuturesContractIndex)}", MarketDataDbCql.DeleteVixFuturesContractIndex)
                    .SetParameters(new DeleteVixFuturesContractIndex(
                        GetVixContractBucket(contractId),
                        contractId))
                    .ExecuteCommandAsync();
            }
        });
    }

    /// <summary>
    /// Deletes yield curve rate data
    /// </summary>
    /// <param name="valueDate">The value date to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteYieldCurveRateAsync(DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        await db.ExecuteQueuedCommandsAsync([
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteYieldCurveRate)}", MarketDataDbCql.DeleteYieldCurveRate)
                .SetParameters(new DeleteYieldCurveRate(valueDate))
                .QueueCommand(),
            db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteYieldCurveRateByDate)}", MarketDataDbCql.DeleteYieldCurveRateByDate)
                .SetParameters(new DeleteYieldCurveRate(valueDate))
                .QueueCommand()
        ]);
    }

    /// <summary>
    /// Deletes futures ITI signal data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="timePeriod"></param>
    /// <returns></returns>
    public async Task DeleteFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod)
    {
        var existing = await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignals)}", MarketDataDbCql.GetFuturesItiSignals)
            .SetParameters(new GetFuturesItiSignals(contractId, valueDate, timePeriod.ToStringFast()))
            .ExecuteQueryAsync(MapToFuturesItiSignal!);
        var scopes = new[]
            {
                GetFuturesItiDayScopeKey(contractId, valueDate),
                GetFuturesItiMonthScopeKey(contractId, ToYearMonth(valueDate))
            }
            .Concat(existing.Select(row => GetFuturesItiTimelineScopeKey(
                row.ContractId,
                row.IntrinsicTimeTrend.ToStringFast(),
                row.IntrinsicTimeMode.ToStringFast(),
                ToYearMonth(row.ValueDate))));
        await ExecuteMaintainedProjectionMutationAsync(
            FuturesItiSignalQueryProjection,
            scopes,
            async () =>
            {
                var db = _dbFactory.MarketDataDb;
                List<object> commands =
                [
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiSignal)}", MarketDataDbCql.DeleteFuturesItiSignal)
                        .SetParameters(new DeleteFuturesItiSignal(
                            contractId, valueDate, timePeriod.ToStringFast()))
                        .QueueCommand(),
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiTimeFrameState)}", MarketDataDbCql.DeleteFuturesItiTimeFrameState)
                        .SetParameters(new GetFuturesItiTimeFrameState(
                            contractId,
                            timePeriod.ToStringFast(),
                            GetFuturesItiCalendarBucketStart(valueDate, timePeriod)))
                        .QueueCommand()
                ];
                foreach (var row in existing)
                {
                    var rowTrend = row.IntrinsicTimeTrend.ToStringFast();
                    var rowMode = row.IntrinsicTimeMode.ToStringFast();
                    var rowTimePeriod = row.TimePeriod.ToStringFast();
                    var yearMonth = ToYearMonth(row.ValueDate);
                    commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiSignalByContractDay)}", MarketDataDbCql.DeleteFuturesItiSignalByContractDay)
                        .SetParameters(new DeleteFuturesItiSignalByContractDay(
                            row.ContractId,
                            row.ValueDate,
                            rowMode,
                            row.SequenceId,
                            rowTimePeriod,
                            rowTrend,
                            row.IntrinsicTimeGroupId))
                        .QueueCommand());
                    commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiSignalByContractMonth)}", MarketDataDbCql.DeleteFuturesItiSignalByContractMonth)
                        .SetParameters(new DeleteFuturesItiSignalByContractMonth(
                            row.ContractId,
                            yearMonth,
                            row.ValueDate,
                            row.SequenceId,
                            rowTimePeriod,
                            rowMode,
                            rowTrend,
                            row.IntrinsicTimeGroupId))
                        .QueueCommand());
                    commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiSignalByTrendModeMonth)}", MarketDataDbCql.DeleteFuturesItiSignalByTrendModeMonth)
                        .SetParameters(new DeleteFuturesItiSignalByTrendModeMonth(
                            row.ContractId,
                            rowTrend,
                            rowMode,
                            yearMonth,
                            row.ValueDate,
                            row.SequenceId,
                            rowTimePeriod,
                            row.IntrinsicTimeGroupId))
                        .QueueCommand());
                }
                await db.ExecuteQueuedCommandsAsync(commands);
            });
    }

    /// <summary>
    /// Deletes futures option tick data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesOptionTickData)}", MarketDataDbCql.DeleteFuturesOptionTickData)
            .SetParameters(new DeleteFuturesOptionTickData(contractId, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously deletes tick price data for a specified futures option contract on a given date.
    /// </summary>
    /// <remarks>This method removes all tick price data associated with the specified contract and date from
    /// the database. Ensure that the contract identifier and date are valid before calling this method.</remarks>
    /// <param name="contractId">The unique identifier of the futures option contract whose tick price data is to be deleted. Cannot be null or
    /// empty.</param>
    /// <param name="valueDate">The date for which the tick price data should be deleted.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    public async Task DeleteFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesOptionTickPriceData)}", MarketDataDbCql.DeleteFuturesOptionTickPriceData)
            .SetParameters(new DeleteFuturesOptionTickPriceData(contractId, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes a market holiday record from the database.
    /// </summary>
    /// <param name="e">The market holiday to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteMarketHolidayAsync(MarketHolidayReadModel e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketHoliday)}", MarketDataDbCql.DeleteMarketHoliday)
            .SetParameters(new DeleteMarketHoliday(currencyType: e.CurrencyType.ToStringFast(), holidayDate: e.HolidayDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes market holiday records from the database for a given currency type within a specified date range.
    /// </summary>
    /// <param name="currencyType">The currency type.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteMarketHolidaysAsync(CurrencyType currencyType)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketHolidays)}", MarketDataDbCql.DeleteMarketHolidays)
            .SetParameters(new DeleteMarketHolidays(currencyType: currencyType.ToStringFast()))
            .ExecuteCommandAsync();

    public async Task DeleteRateOfReturnAsync(string symbol, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteRateOfReturn)}", MarketDataDbCql.DeleteRateOfReturn)
            .SetParameters(new DeleteRateOfReturn(symbol, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Gets the futures closing price for a given FuturesClosingPriceId.
    /// </summary>
    /// <param name="e">The identifier of the futures closing price to retrieve.</param>
    /// <returns>The <see cref="FuturesClosingPriceReadModel"/>.</returns>
    public async Task<FuturesClosingPriceReadModel?> GetFuturesClosingPriceAsync(FuturesDataId e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesClosingPrice)}", MarketDataDbCql.GetFuturesClosingPrice)
            .SetParameters(new GetFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesClosingPrice!);

    /// <summary>
    /// Gets yesterday's futures closing price for a given FuturesClosingPriceId.
    /// </summary>
    /// <param name="id">The identifier of the futures closing price to retrieve.</param>
    /// <returns>The <see cref="FuturesClosingPriceReadModel"/>.</returns>
    public async Task<FuturesClosingPriceReadModel?> GetYesterdaysFuturesClosingPriceAsync(FuturesDataId e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYesterdaysFuturesClosingPrice)}", MarketDataDbCql.GetYesterdaysFuturesClosingPrice)
            .SetParameters(new GetYesterdaysFuturesClosingPrice(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync<FuturesClosingPriceReadModel>(MapToFuturesClosingPrice!);

    /// <summary>
    /// get futures tick data   
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataV2ReadModel?> GetFuturesTickDataAsync(FuturesTickDataId e)
           => await _dbFactory.MarketDataDb
               .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickData)}", MarketDataDbCql.GetFuturesTickData)
               .SetParameters(new GetFuturesTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId))
               .ExecuteSingleAsync(MapToFuturesTickData!);

    /// <summary>
    /// get last futures option tick data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate)
            => await _dbFactory.MarketDataDb
               .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTickData)}", MarketDataDbCql.GetLastFuturesTickData)
               .SetParameters(new GetLastFuturesTickData(contractId, valueDate))
               .ExecuteSingleAsync(MapToFuturesTickData!);

    /// <summary>
	/// get last futures option tick data
	/// </summary>
	/// <param name="contractId"></param>
	/// <param name="tickDate"></param>
	/// <returns></returns>
	public async Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataByTickDateAsync(string contractId, DateTime tickDate)
    {
        var db = _dbFactory.MarketDataDb;
        var valueDate = DateOnly.FromDateTime(tickDate);
        var tickTime = TimeOnly.FromDateTime(tickDate);
        var stamp = await GetProjectionScopeReadStampAsync(
            FuturesTickByTimeProjection,
            new[] { GetFuturesTickScopeKey(contractId, valueDate) });
        if (stamp is not null)
        {
            var projected = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTickDataByTickTime)}", MarketDataDbCql.GetLastFuturesTickDataByTickTime)
                .SetParameters(new GetLastFuturesTickDataByTickTime(contractId, valueDate, tickTime))
                .ExecuteSingleAsync(MapToFuturesTickData!);
            if (await IsProjectionScopeReadStampValidAsync(stamp.Value))
                return projected;
        }

        return (await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickDataByDate)}", MarketDataDbCql.GetFuturesTickDataByDate)
                .SetParameters(new GetLastFuturesTickData(contractId, valueDate))
                .ExecuteQueryAsync(MapToFuturesTickData!))
            .Where(e => e.TickTime == tickTime)
            .OrderByDescending(e => e.TickId)
            .FirstOrDefault();
    }

    /// <summary>
    /// get last futures option tick data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
            => await _dbFactory.MarketDataDb
               .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesOptionTickData)}", MarketDataDbCql.GetLastFuturesOptionTickData)
               .SetParameters(new GetLastFuturesOptionTickData(contractId, valueDate))
               .ExecuteSingleAsync(MapToFuturesOptionTickData!);

    /// <summary>
    /// Asynchronously retrieves the most recent tick price data for a specified futures option contract on a given
    /// date.
    /// </summary>
    /// <remarks>This method queries the market data database for the latest available tick price information
    /// for the given contract and date. Ensure that the contract identifier and date are valid to avoid
    /// exceptions.</remarks>
    /// <param name="contractId">The unique identifier of the futures option contract for which to retrieve tick price data. Cannot be null or
    /// empty.</param>
    /// <param name="valueDate">The date for which to retrieve the tick price data. Must be a valid date.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the last tick price data for the
    /// specified contract and date, or null if no data is found.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
           .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesOptionTickPriceData)}", MarketDataDbCql.GetLastFuturesOptionTickPriceData)
           .SetParameters(new GetLastFuturesOptionTickPriceData(contractId, valueDate))
           .ExecuteSingleAsync(MapToFuturesOptionTickPriceData!);
    /// <summary>
    /// get futures tick data id    
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesTickDataId?> GetLastFuturesTickDataIdAsync(string contractId, DateOnly valueDate)
          => await _dbFactory.MarketDataDb
              .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTickData)}", MarketDataDbCql.GetLastFuturesTickData)
              .SetParameters(new GetLastFuturesTickData(contractId, valueDate))
              .ExecuteSingleAsync(MapToFuturesTickDataId);

    /// <summary>
    /// Gets the futures bar data for a given contractId, symbol, valueDate, startDate, and endDate.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="valueDate">The value date.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>A collection of <see cref="FuturesBarDataReadModel"/>.</returns>
    public async Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
    {
        var futuresBarData = await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesBarData)}", MarketDataDbCql.GetFuturesBarData)
            .SetParameters(new GetFuturesBarData(
                contractId,
                symbol,
                valueDate,
                startDate,
                endDate
            ))
            .ExecuteQueryAsync(MapToFuturesBarData!);
        return [.. futuresBarData.OrderBy(e => e.BarDate)];
    }

    /// <summary>
    /// gets all futures bar data.
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync()
    {
        var futuresBarData = await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesBarDataAll)}", MarketDataDbCql.GetFuturesBarDataAll)
            .ExecuteQueryAsync(MapToFuturesBarData!);
        return futuresBarData;
    }

    /// <summary>
    /// gets the last futures bar data for a given contractId, symbol, and valueDate.
    /// </summary>
    /// <returns></returns>
    public async Task<FuturesBarDataReadModel> GetLastFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var lastFuturesBarData = await db
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesBarData)}", MarketDataDbCql.GetLastFuturesBarData)
            .SetParameters(new GetLastFuturesBarData(contractId, symbol, valueDate))
            .ExecuteSingleAsync(MapToFuturesBarData!);
        return lastFuturesBarData!;
    }


    /// <summary>
    /// Gets the count of futures bar data for a given FuturesBarDataId.
    /// </summary>
    /// <param name="e">The futures bar data identifier.</param>
    /// <returns>The count of futures bar data.</returns>
    public async Task<int> GetFuturesBarDataCountAsync(FuturesBarDataId e)
        => Convert.ToInt32(await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesBarDataCount)}", MarketDataDbCql.GetFuturesBarDataCount)
            .SetParameters(new GetFuturesBarDataCount(contractId: e.ContractId, symbol: e.Symbol, valueDate: e.ValueDate))
            .ExecuteScalarAsync(MapToFuturesBarDataCount!));

    /// <summary>
    /// Gets a collection of futures ITI signals for a given entity ID.
    /// </summary>
    /// <param name="e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref="FuturesItiSignalV2ReadModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(FuturesItiSignalEntityId e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignals)}", MarketDataDbCql.GetFuturesItiSignals)
            .SetParameters(new GetFuturesItiSignals(contractId: e.ContractId, valueDate: e.ValueDate, timePeriod: e.TimePeriod.ToStringFast()))
            .ExecuteQueryAsync(MapToFuturesItiSignal!);

    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref="FuturesItiSignalV2ReadModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        var contractIds = (await dbSec.GetFuturesContractsBySymbolAsync(symbol))
            .Select(static contract => contract.ContractId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return await ReadFuturesItiSignalsByDateRangeAsync(contractIds, startDate, endDate);
    }

    /// <summary>
    /// Gets futures ITI signals for one concrete futures contract and date range.
    /// This avoids the securities-symbol lookup used by the cross-contract query.
    /// </summary>
    public Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsForContractAsync(
        string contractId,
        DateOnly startDate,
        DateOnly endDate) =>
        ReadFuturesItiSignalsByDateRangeAsync([contractId], startDate, endDate);

    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        var contractIds = (await dbSec.GetFuturesContractsBySymbolAsync(symbol))
            .Select(static contract => contract.ContractId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var modes = GetIntrinsicTimeModes().ToHashSet(StringComparer.Ordinal);
        var futuresItiSignals = await ReadFuturesItiSignalsByDateRangeAsync(
            contractIds, startDate, endDate);
        return [.. futuresItiSignals
            .Where(signal => modes.Contains(signal.IntrinsicTimeMode.ToStringFast()))
            .OrderBy(static signal => signal.ValueDate)
            .ThenBy(static signal => signal.SequenceId)];
    }

    /// <summary>
    /// Gets a collection of futures ITI signals for a given symbol and date range.
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        var contractIds = (await dbSec.GetFuturesContractsBySymbolAsync(symbol))
            .Select(static contract => contract.ContractId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var modes = GetIntrinsicTimeModes().ToHashSet(StringComparer.Ordinal);
        var futuresItiSignals = await ReadFuturesItiSignalsByDateRangeAsync(
            contractIds, startDate, endDate);
        return [.. futuresItiSignals
            .Where(signal => modes.Contains(signal.IntrinsicTimeMode.ToStringFast()))
            .OrderBy(static signal => signal.ValueDate)
            .ThenBy(static signal => signal.SequenceId)];
    }

    /// <summary>
    /// Inserts a new futures bar data record into the database.
    /// </summary>
    /// <param name="e">The futures bar data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesBarDataAsync(FuturesBarDataReadModel e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesBarData)}", MarketDataDbCql.InsertFuturesBarData)
            .SetParameters(new InsertFuturesBarData(
                contractId: e.ContractId,
                symbol: e.Symbol,
                valueDate: e.ValueDate,
                barDate: e.BarDate,
                barRateType: e.BarRateType.ToStringFast(),
                barValue: e.BarValue,
                upTrendTrigger: e.UpTrendTrigger,
                downTrendTrigger: e.DownTrendTrigger
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a new futures bar data record into the database.
    /// </summary>
    /// <param name="futuresBarData">The futures bar data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesBarDataAsync(ICollection<FuturesBarDataReadModel> futuresBarData)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesBarData)}", MarketDataDbCql.InsertFuturesBarData)
            .SetParameters(futuresBarData.Select(e => new InsertFuturesBarData(
                contractId: e.ContractId,
                symbol: e.Symbol,
                valueDate: e.ValueDate,
                barDate: e.BarDate,
                barRateType: e.BarRateType.ToStringFast(),
                barValue: e.BarValue,
                upTrendTrigger: e.UpTrendTrigger,
                downTrendTrigger: e.DownTrendTrigger
            )))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of futures bar data into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided futures bar data and inserts it into the database using a
    /// batch operation.  The <paramref name="futuresBarData"/> collection is enumerated to count the rows and prepare
    /// the data for insertion.</remarks>
    /// <param name="futuresBarData">A collection of <see cref="FuturesBarDataReadModel"/> objects representing the futures bar data to be inserted.
    /// Each object must contain valid values for contract ID, symbol, value date, bar date, bar rate type, bar value, 
    /// up trend trigger, and down trend trigger.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number  of
    /// rows processed during the insertion.</returns>
    public async Task<long> InsertFuturesBarDataAsync(IEnumerable<FuturesBarDataReadModel> futuresBarData)
    {
        long rowCount = 0;
        await _dbFactory.MarketDataDb
        .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesBarData)}", MarketDataDbCql.InsertFuturesBarData)
        .SetParameters(GetFuturesBarData().Select(e => new InsertFuturesBarData(
            contractId: e.ContractId,
            symbol: e.Symbol,
            valueDate: e.ValueDate,
            barDate: e.BarDate,
            barRateType: e.BarRateType.ToStringFast(),
            barValue: e.BarValue,
            upTrendTrigger: e.UpTrendTrigger,
            downTrendTrigger: e.DownTrendTrigger
        )))
        .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<FuturesBarDataReadModel> GetFuturesBarData()
        {
            foreach (var barData in futuresBarData)
            {
                rowCount++;
                yield return barData;
            }
        }
    }


    /// <summary>
    /// Inserts a collection of futures bar data records into the database. 
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesClosingPriceAsync(FuturesClosingPriceReadModel e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesClosingPrice)}", MarketDataDbCql.InsertFuturesClosingPrice)
            .SetParameters(new InsertFuturesClosingPrice(
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                closingPrice: e.ClosingPrice,
                createdOn: e.CreatedOn,
                createdBy: e.CreatedBy
            ))
            .ExecuteCommandAsync();



    /// <summary>
    /// 
    /// </summary>
    /// <param name="tickData"></param>
    /// <returns></returns>
    public async Task InsertFuturesTickDataAsync(FuturesTickDataV2ReadModel e)
    {
        var tickId = e.TickId > 0
            ? e.TickId
            : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTickData_TickId);
        await ExecuteAtomicTickWriteAsync(
            GetFuturesTickScopeKey(e.ContractId, e.ValueDate),
            new[]
            {
                new InsertFuturesTickData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    tickId,
                    tickTime: e.TickTime,
                    price: e.Price,
                    size: e.Size)
            },
            new[]
            {
                new InsertFuturesTickDataByTime(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    tickTime: e.TickTime,
                    tickId,
                    price: e.Price,
                    size: e.Size)
            });
    }

    /// <summary>
    /// insert futures tick data collection
    /// </summary>
    /// <param name="tickData"></param>
    /// <returns></returns>
    public async Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataV2ReadModel> tickData)
    {
        if (tickData.Count == 0)
            return;

        EnsureDistinctFuturesTickWrites(tickData);
        var batchesByGuard = tickData
            .GroupBy(static e => (e.ContractId, e.ValueDate))
            .SelectMany(group => group.Chunk(TickAtomicBatchRowCount)
                .Select(chunk => (group.Key.ContractId, group.Key.ValueDate, Rows: chunk)))
            .GroupBy(batch => GetProjectionGuardScopeKey(
                GetFuturesTickScopeKey(batch.ContractId, batch.ValueDate)));
        foreach (var guardGroupBatch in batchesByGuard.Chunk(ProjectionReadConcurrency))
        {
            await Task.WhenAll(guardGroupBatch.Select(async guardBatches =>
            {
                foreach (var batch in guardBatches)
                {
                    await ExecuteAtomicTickWriteAsync(
                        GetFuturesTickScopeKey(batch.ContractId, batch.ValueDate),
                        batch.Rows.Select(e => new InsertFuturesTickData(
                            contractId: e.ContractId,
                            valueDate: e.ValueDate,
                            tickId: e.TickId,
                            tickTime: e.TickTime,
                            price: e.Price,
                            size: e.Size)).ToArray(),
                        batch.Rows.Select(e => new InsertFuturesTickDataByTime(
                            contractId: e.ContractId,
                            valueDate: e.ValueDate,
                            tickTime: e.TickTime,
                            tickId: e.TickId,
                            price: e.Price,
                            size: e.Size)).ToArray());
                }
            }));
        }
    }

    /// <summary>
    /// Inserts a new futures ITI signal record into the database.
    /// </summary>
    /// <param name="e">The futures ITI signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesItiSignalAsync(FuturesItiSignalV2ReadModel e)
    {
        var trend = e.IntrinsicTimeTrend.ToStringFast();
        var mode = e.IntrinsicTimeMode.ToStringFast();
        await ExecuteMaintainedProjectionMutationAsync(
            FuturesItiSignalQueryProjection,
            GetFuturesItiProjectionScopeKeys(e.ContractId, e.ValueDate, trend, mode),
            async () =>
            {
                var sequenceId = e.SequenceId > 0
                    ? e.SequenceId
                    : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesItiSignal_SequenceId);
                var db = _dbFactory.MarketDataDb;
                var canonicalParameters = CreateFuturesItiSignalParameters(e, sequenceId);
                var monthParameters = CreateFuturesItiSignalMonthParameters(e, sequenceId);
                List<object> commands =
                [
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalIndex)}", MarketDataDbCql.InsertFuturesItiSignalIndex)
                        .SetParameters(new InsertFuturesItiSignalIndex(e.ValueDate, e.ContractId))
                        .QueueCommand(),
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignal)}", MarketDataDbCql.InsertFuturesItiSignal)
                        .SetParameters(canonicalParameters)
                        .QueueCommand(),
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByContractDay)}", MarketDataDbCql.InsertFuturesItiSignalByContractDay)
                        .SetParameters(canonicalParameters)
                        .QueueCommand(),
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByContractMonth)}", MarketDataDbCql.InsertFuturesItiSignalByContractMonth)
                        .SetParameters(monthParameters)
                        .QueueCommand(),
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByTrendModeMonth)}", MarketDataDbCql.InsertFuturesItiSignalByTrendModeMonth)
                        .SetParameters(monthParameters)
                        .QueueCommand(),
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpsertFuturesItiTimeFrameState)}", MarketDataDbCql.UpsertFuturesItiTimeFrameState)
                        .SetParameters(CreateFuturesItiTimeFrameStateParameters(e, sequenceId))
                        .QueueCommand(),
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                        .SetParameters(new InsertMarketDataProjectionMonth(
                            FuturesItiSignalQueryProjection,
                            ToYearMonth(e.ValueDate)))
                        .QueueCommand()
                ];
                await db.ExecuteQueuedCommandsAsync(commands);
            });
    }

    public async Task<FuturesItiSignalV2ReadModel?> GetFuturesItiTimeFrameStateAsync(
        string contractId,
        TimeFrameType timePeriod,
        DateOnly calendarBucketStart,
        CancellationToken cancellationToken = default)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTimeFrameState)}", MarketDataDbCql.GetFuturesItiTimeFrameState)
            .SetParameters(new GetFuturesItiTimeFrameState(
                contractId,
                timePeriod.ToStringFast(),
                calendarBucketStart))
            .ExecuteSingleAsync(MapToFuturesItiTimeFrameState!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Inserts a Futures RSI Signal asynchronously into the database.
    /// </summary>
    /// <param name="futuresRsiSignal">The Futures RSI Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesRsiSignalAsync(FuturesRsiSignalReadModel e)
    {
        var parameters = new InsertFuturesRsiSignal(
                e.ContractId,
                e.ValueDate,
                e.TimePeriod.ToStringFast(),
                e.PeriodLength,
                e.Timestamp,
                e.Price,
                e.PriceChange,
                e.PriceGain,
                e.PriceLoss,
                e.AveragePriceGain,
                e.AveragePriceLoss,
                e.RS,
                e.RSI,
                e.RSIAverage,
                e.RSISlope,
                e.SourceSequence,
                e.SourceEventTimestamp,
                e.Metadata?.CalculationConfigurationId,
                e.Metadata?.ObservationId.Value,
                e.Metadata?.MarketDataAsOfUtc.UtcDateTime,
                e.Metadata?.CalculationVersion,
                e.Metadata?.CalculationMethod.ToString(),
                e.Metadata is { } rsiMetadata ? rsiMetadata.SchemaVersion : null,
                e.Metadata?.IsValid);
        await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesRsiSignal)}", MarketDataDbCql.InsertFuturesRsiSignal)
            .SetParameters(parameters)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// Inserts a single yield curve rate record into the database.
    /// </summary>
    /// <param name="e">The YieldCurveRateReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task InsertYieldCurveRateAsync(YieldCurveRateReadModel e)
        => InsertYieldCurveRatesAsync([e]);

    /// <summary>
    /// Inserts a collection of yield curve rate records into the database.
    /// </summary>
    /// <param name="e">The collection of YieldCurveRateReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertYieldCurveRatesAsync(YieldCurveRateReadModel[] e)
        => await InsertYieldCurveRatesAsync(e, ImportDuplicatePolicy.Overwrite, Guid.Empty);

    public async Task InsertYieldCurveRatesAsync(
        YieldCurveRateReadModel[] e,
        ImportDuplicatePolicy duplicatePolicy,
        Guid commandId)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Length == 0)
            return;

        ValidateImportPolicy(duplicatePolicy, commandId);

        var db = _dbFactory.MarketDataDb;
        var commands = new List<object>(e.Length * 3);
        var years = new HashSet<int>();
        foreach (var row in e)
        {
            if (duplicatePolicy == ImportDuplicatePolicy.Reject)
            {
                await EnsureImportOwnershipAsync(
                    "treasury-curve",
                    row.ValueDate.ToString("yyyy-MM-dd"),
                    commandId,
                    await GetYieldCurveRateAsync(row.ValueDate).ConfigureAwait(false) is not null)
                    .ConfigureAwait(false);
            }
            var parameters = new InsertYieldCurveRate(
                id: YieldCurveLookupId,
                valueDate: row.ValueDate,
                oneMonth: row.OneMonth,
                twoMonth: row.TwoMonth,
                threeMonth: row.ThreeMonth,
                sixMonth: row.SixMonth,
                oneYear: row.OneYear,
                twoYear: row.TwoYear,
                threeYear: row.ThreeYear,
                fiveYear: row.FiveYear,
                sevenYear: row.SevenYear,
                tenYear: row.TenYear,
                twentyYear: row.TwentyYear,
                thirtyYear: row.ThirtyYear);
            commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertYieldCurveRate)}", MarketDataDbCql.InsertYieldCurveRate)
                .SetParameters(parameters)
                .QueueCommand());
            commands.Add(db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertYieldCurveRateByDate)}", MarketDataDbCql.InsertYieldCurveRateByDate)
                .SetParameters(parameters)
                .QueueCommand());
            years.Add(row.ValueDate.Year);
        }
        commands.AddRange(years.Select(rateYear => db
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertYieldCurveRateYear)}", MarketDataDbCql.InsertYieldCurveRateYear)
            .SetParameters(new InsertYieldCurveRateYear(YieldCurveLookupId, rateYear))
            .QueueCommand()));
        await db.ExecuteQueuedCommandsAsync(commands);
    }

    /// <summary>
    /// Gets the last FuturesOptionTickDataId for a given contractId and valueDate.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The last <see cref="FuturesOptionTickDataId"/>.</returns>
    public async Task<FuturesOptionTickDataId?> GetLastFuturesOptionTickDataIdAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesOptionTickDataId)}", MarketDataDbCql.GetLastFuturesOptionTickDataId)
            .SetParameters(new GetLastFuturesOptionTickDataId(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesOptionTickDataId!);

    /// <summary>
    /// Gets the FuturesOptionTickDataV2ReadModel for a given FuturesOptionTickDataId.
    /// </summary>
    /// <param name="e">The futures option tick data identifier.</param>
    /// <returns>The <see cref="FuturesOptionTickDataV2ReadModel"/>.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickDataAsync(FuturesOptionTickDataId e)
         => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesOptionTickData)}", MarketDataDbCql.GetFuturesOptionTickData)
            .SetParameters(new GetFuturesOptionTickData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId))
            .ExecuteSingleAsync(MapToFuturesOptionTickData!);

    /// <summary>
    /// Asynchronously retrieves the tick price data for a specified futures option.
    /// </summary>
    /// <remarks>This method queries the market data database for the latest tick price information associated
    /// with the provided identifier. Ensure that the identifier is valid to avoid unexpected results.</remarks>
    /// <param name="e">An identifier that specifies the futures option tick data to retrieve. This includes the contract ID, value
    /// date, and tick ID. Must represent a valid futures option tick.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the tick price data for the
    /// specified futures option, or null if no matching data is found.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickPriceDataAsync(FuturesOptionTickDataId e)
      => await _dbFactory.MarketDataDb
         .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesOptionTickPriceData)}", MarketDataDbCql.GetFuturesOptionTickPriceData)
         .SetParameters(new GetFuturesOptionTickPriceData(contractId: e.ContractId, valueDate: e.ValueDate, tickId: e.TickId))
         .ExecuteSingleAsync(MapToFuturesOptionTickPriceData!);

    /// <summary>
    /// Inserts a single FuturesOptionTickData into the database.
    /// </summary>
    /// <param name="e">The futures option tick data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataV2ReadModel e)
    {
        var tickId = e.TickId > 0
            ? e.TickId
            : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesOptionTickData_TickId);
        await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesOptionTickData)}", MarketDataDbCql.InsertFuturesOptionTickData)
            .SetParameters(new InsertFuturesOptionTickData(
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                tickId,
                tickTime: e.TickTime,
                optionPrice: e.OptionPrice,
                bidPrice: e.BidPrice,
                askPrice: e.AskPrice,
                bidSize: e.BidSize,
                askSize: e.AskSize,
                impliedVolatility: e.ImpliedVolatility,
                underlyingPrice: e.UnderlyingPrice,
                delta: e.Delta,
                gamma: e.Gamma,
                vega: e.Vega,
                theta: e.Theta,
                rho: e.Rho
            ))
            .ExecuteCommandAsync();
    }

    public async Task InsertFuturesOptionTickPriceDataAsync(FuturesOptionTickDataV2ReadModel e)
    {
        var tickId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesOptionTickPriceData_TickId);
        await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesOptionTickPriceData)}", MarketDataDbCql.InsertFuturesOptionTickPriceData)
            .SetParameters(new InsertFuturesOptionTickData(
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                tickId,
                tickTime: e.TickTime,
                optionPrice: e.OptionPrice,
                bidPrice: e.BidPrice,
                askPrice: e.AskPrice,
                bidSize: e.BidSize,
                askSize: e.AskSize,
                impliedVolatility: e.ImpliedVolatility,
                underlyingPrice: e.UnderlyingPrice,
                delta: e.Delta,
                gamma: e.Gamma,
                vega: e.Vega,
                theta: e.Theta,
                rho: e.Rho
            ))
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// Inserts a collection of FuturesOptionTickDataV2ReadModel into the database.
    /// </summary>
    /// <param name="tickData">The collection of futures option tick data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataV2ReadModel> tickData)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesOptionTickData)}", MarketDataDbCql.InsertFuturesOptionTickData)
            .SetParameters(tickData.Select(e => new InsertFuturesOptionTickData(
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                tickId: e.TickId,
                tickTime: e.TickTime,
                optionPrice: e.OptionPrice,
                bidPrice: e.BidPrice,
                askPrice: e.AskPrice,
                bidSize: e.BidSize,
                askSize: e.AskSize,
                impliedVolatility: e.ImpliedVolatility,
                underlyingPrice: e.UnderlyingPrice,
                delta: e.Delta,
                gamma: e.Gamma,
                vega: e.Vega,
                theta: e.Theta,
                rho: e.Rho
            )))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a trade live feed record into the database.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertTradeLiveFeed)}", MarketDataDbCql.InsertTradeLiveFeed)
            .SetParameters(new InsertTradeLiveFeed(orderId: e.OrderId, tradeId: e.TradeId, tradeLiveFeedState: e.TradeLiveFeedState.ToStringFast()))
            .ExecuteCommandAsync();

    /// <summary>
    /// Gets the FuturesDataId for a given contractId and valueDate.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesDataId.</returns>
    public async Task<FuturesDataId?> GetFuturesDataId(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesDataId)}", MarketDataDbCql.GetFuturesDataId)
            .SetParameters(new GetFuturesDataId(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesDataId); // Map the result to FuturesDataId

    /// <summary>
    /// Gets the FuturesTickHLVDataReadModel for a given FuturesDataId.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<FuturesTickHLVDataReadModel?> GetFuturesTickHLVDataAsync(FuturesDataId e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickHLVData)}", MarketDataDbCql.GetFuturesTickHLVData)
            .SetParameters(new GetFuturesTickHLVData(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesTickHLVData!);

    /// <summary>
    /// Gets the FuturesTickHLVDataReadModel for a given VixFuturesEodDataEntityId.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<FuturesTickHLVDataReadModel?> GetVixFuturesTickHLVDataAsync(VixFuturesEodDataEntityId e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickHLVData)}", MarketDataDbCql.GetFuturesTickHLVData)
            .SetParameters(new GetFuturesTickHLVData(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesTickHLVData!);

    /// <summary>
    /// Gets the FuturesEodDataV2ReadModel for a given contractId and valueDate.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodDataV2ReadModel.</returns>
    public async Task<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var futuresEodData = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodData)}", MarketDataDbCql.GetFuturesEodData)
           .SetParameters(new GetFuturesEodData(contractId, valueDate))
           .ExecuteSingleAsync(MapToFuturesEodData!);
        futuresEodData ??= await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYesterdaysFuturesEodData)}", MarketDataDbCql.GetYesterdaysFuturesEodData)
            .SetParameters(new GetYesterdaysFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesEodData!);
        return futuresEodData;
    }

    /// <summary>
    /// Asynchronously retrieves intra-day market data for a specified futures contract on a given date.
    /// </summary>
    /// <remarks>This method performs an asynchronous database query to fetch the requested data. Ensure that
    /// the provided contract identifier and date are valid to avoid exceptions.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract for which to retrieve intra-day data. This parameter cannot be
    /// null or empty.</param>
    /// <param name="valueDate">The date for which the intra-day market data is requested.</param>
    /// <returns>A collection of <see cref="FuturesIntraDayDataReadModel"/> objects representing the intra-day market data for
    /// the specified contract and date.</returns>
    public async Task<ICollection<FuturesIntraDayDataReadModel>> GetFuturesIntraDayDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesIntraDayData)}", MarketDataDbCql.GetFuturesIntraDayData)
            .SetParameters(new GetFuturesIntraDayData(contractId, valueDate))
            .ExecuteQueryAsync(MapToFuturesIntraDayData!);

    /// <summary>
    /// Asynchronously retrieves the most recent end-of-day futures data.
    /// </summary>
    /// <remarks>This method queries the market data database to obtain the latest available futures data at
    /// the end of the trading day.</remarks>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see
    /// cref="FuturesEodDataV2ReadModel"/> representing the latest end-of-day futures data, or <see langword="null"/> if
    /// no data is available.</returns>
    public async Task<FuturesEodDataV2ReadModel?> GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesEodData)}", MarketDataDbCql.GetLastFuturesEodData)
            .SetParameters(new GetLastFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesEodData!);

    /// <summary>
    /// Asynchronously retrieves a collection of end-of-day futures data.
    /// </summary>
    /// <remarks>This method queries the market data database to obtain all available end-of-day futures
    /// data.</remarks>
    /// <returns>A task representing the asynchronous operation. The task result contains a collection of  <see
    /// cref="FuturesEodDataV2ReadModel"/> objects representing the end-of-day futures data.</returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync()
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataAll)}", MarketDataDbCql.GetFuturesEodDataAll)
            .ExecuteQueryAsync(MapToFuturesEodData!);

    /// <summary>
    /// Gets a collection of FuturesEodDataV2ReadModel for a given contractId and date range.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataByDateRangeAsync(string contractId, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataByDateRange)}", MarketDataDbCql.GetFuturesEodDataByDateRange)
            .SetParameters(new GetFuturesEodDataByDateRange(contractId, startDate, endDate))
            .ExecuteQueryAsync(MapToFuturesEodData!);

    /// <summary>
    /// Gets the current FuturesEodDataV2ReadModel for a given FuturesDataId.
    /// </summary>
    /// <param name="e">The FuturesDataId containing the contractId and valueDate.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodDataV2ReadModel.</returns>
    public async Task<FuturesEodDataV2ReadModel?> GetCurrentFuturesEodDataAsync(DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var targetYearMonth = ToYearMonth(valueDate);
        var projectionMonths = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMonths)}", MarketDataDbCql.GetMarketDataProjectionMonths)
            .SetParameters(new GetMarketDataProjectionMonths(FuturesEodProjection, targetYearMonth))
            .ExecuteQueryAsync(MapToYearMonth);
        var orderedProjectionMonths = projectionMonths.ToArray();
        var stamp = await GetProjectionScopeReadStampAsync(
            FuturesEodProjection,
            orderedProjectionMonths
                .Select(GetFuturesEodScopeKey)
                .Concat(GetProjectionGuardScopeKeys()));
        if (stamp is null)
            return await ReadLegacyCurrentFuturesEodDataAsync(valueDate);

        FuturesEodDataV2ReadModel? result = null;
        foreach (var yearMonth in orderedProjectionMonths)
        {
            var monthCutoff = yearMonth == targetYearMonth ? valueDate : GetMonthEnd(yearMonth);
            result = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetCurrentFuturesEodDataByMonth)}", MarketDataDbCql.GetCurrentFuturesEodDataByMonth)
                .SetParameters(new GetCurrentFuturesEodDataByMonth(yearMonth, monthCutoff))
                .ExecuteSingleAsync(MapToFuturesEodData!);
            if (result is not null)
                break;
        }

        var validatedProjectionMonths = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMonths)}", MarketDataDbCql.GetMarketDataProjectionMonths)
            .SetParameters(new GetMarketDataProjectionMonths(FuturesEodProjection, targetYearMonth))
            .ExecuteQueryAsync(MapToYearMonth);
        if (orderedProjectionMonths.SequenceEqual(validatedProjectionMonths) &&
            await IsProjectionScopeReadStampValidAsync(stamp.Value))
        {
            return result;
        }

        return await ReadLegacyCurrentFuturesEodDataAsync(valueDate);
    }

    /// <summary>
    /// Gets a collection of FuturesEodDataV2ReadModel for a given date range.
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesEodDataV2ReadModel>> GetCurrentFuturesEodDataByDateRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        var db = _dbFactory.MarketDataDb;
        var yearMonths = GetYearMonths(startDate, endDate).ToHashSet();
        var stamp = await GetProjectionScopeReadStampAsync(
            FuturesEodProjection,
            yearMonths.Select(GetFuturesEodScopeKey));
        if (stamp is null)
            return await ReadLegacyFuturesEodDataByMonthsAsync(startDate, endDate, yearMonths);

        List<FuturesEodDataV2ReadModel> results = [];
        foreach (var yearMonth in yearMonths)
        {
            var monthStart = GetMonthStart(yearMonth);
            var monthEnd = GetMonthEnd(yearMonth);
            var rangeStart = startDate > monthStart ? startDate : monthStart;
            var rangeEnd = endDate < monthEnd ? endDate : monthEnd;
            var monthValues = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetCurrentFuturesEodDataByDateRange)}", MarketDataDbCql.GetCurrentFuturesEodDataByDateRange)
                .SetParameters(new GetCurrentFuturesEodDataByDateRange(yearMonth, rangeStart, rangeEnd))
                .ExecuteQueryAsync(MapToFuturesEodData!);
            results.AddRange(monthValues);
        }

        if (!await IsProjectionScopeReadStampValidAsync(stamp.Value))
            return await ReadLegacyFuturesEodDataByMonthsAsync(startDate, endDate, yearMonths);

        return [.. results.OrderByDescending(e => e.ValueDate).ThenBy(e => e.ContractId)];
    }

    /// <summary>
    /// Gets the FuturesEodMovingAverageReadModel for a given symbol and date range.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>A task representing the asynchronous operation, containing the FuturesEodMovingAverageReadModel.</returns>
    public async Task<FuturesEodMovingAverageReadModel?> GetFuturesEodMovingAverageAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        var values = (await GetCurrentFuturesEodDataByDateRangeAsync(
                DateOnly.FromDateTime(startDate),
                DateOnly.FromDateTime(endDate)))
            .Where(e => string.Equals(e.Symbol, symbol, StringComparison.Ordinal))
            .Select(e => e.ClosePrice)
            .ToArray();
        return values.Length == 0
            ? null
            : new FuturesEodMovingAverageReadModel(symbol, (double)values.Average());
    }

    /// <summary>
    /// Gets a collection of FuturesEodClosingPriceReadModel for a given symbol and date range, limited by maxDays.
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="maxDays">The maximum number of days to retrieve.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of FuturesEodClosingPriceReadModel.</returns>
    public async Task<ICollection<FuturesEodClosingPriceReadModel>> GetFuturesEodClosingPricesAsync(string contractId, string symbol, DateOnly startDate, DateOnly endDate, int maxDays)
    {
        if (maxDays <= 0)
            return [];

        var closingPrices = new List<FuturesEodClosingPriceReadModel>(Math.Min(maxDays, 256));
        var rows = _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodClosingPrices)}", MarketDataDbCql.GetFuturesEodClosingPrices)
            .SetParameters(new GetFuturesEodClosingPrices(
                contractId,
                startDate,
                endDate))
            .ExecuteStreamAsync(MapToFuturesEodClosingPrice);
        await foreach (var row in rows.ConfigureAwait(false))
        {
            if (!string.Equals(row.Symbol, symbol, StringComparison.Ordinal))
                continue;

            closingPrices.Add(row);
            if (closingPrices.Count == maxDays)
                break;
        }

        return closingPrices;
    }

    /// <summary>
    /// return futures iti trend delta data by date range
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<ICollection<FuturesItiTrendDeltaDataReadModel>> GetFuturesItiTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendDeltaData)}", MarketDataDbCql.GetFuturesItiTrendDeltaData)
            .SetParameters(new GetFuturesItiTrendDeltaData(
                symbol,
                startDate,
                endDate
            ))
            .ExecuteQueryAsync(MapToFuturesItiTrendDeltaData);

    /// <summary>
    /// return futures iti trend class data by date range
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesItiTrendClassDataReadModel>> GetFuturesItiTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendClassData)}", MarketDataDbCql.GetFuturesItiTrendClassData)
            .SetParameters(new GetFuturesItiTrendClassData(
                symbol,
                startDate,
                endDate
            ))
            .ExecuteQueryAsync(MapToFuturesItiTrendClassData);

    /// <summary>
    /// return futures iti trend delta model
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    public async Task<FuturesItiTrendDeltaModelReadModel> GetFuturesItiTrendDeltaModelAsync(string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var maxValueDate = await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendDeltaModelMaxValueDate)}", MarketDataDbCql.GetFuturesItiTrendDeltaModelMaxValueDate)
            .SetParameters(new GetFuturesItiTrendDeltaModelMaxValueDate(
                symbol,
                valueDate
            ))
            .ExecuteScalarAsync(MapToMaxValueDate);

        return await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendDeltaModel)}", MarketDataDbCql.GetFuturesItiTrendDeltaModel)
            .SetParameters(new GetFuturesItiTrendDeltaModel(
                symbol,
                valueDate: maxValueDate
            ))
            .ExecuteSingleAsync(MapToFuturesItiTrendDeltaModel!);
    }

    /// <summary>
    /// return futures iti trend class model
    /// </summary>
    /// <param name="symbol"></param>   
    /// <param name="valueDate"></param>
    public async Task<FuturesItiTrendClassModelReadModel> GetFuturesItiTrendClassModelAsync(string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var maxValueDate = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendClassModelMaxValueDate)}", MarketDataDbCql.GetFuturesItiTrendClassModelMaxValueDate)
            .SetParameters(new GetFuturesItiTrendClassModelMaxValueDate(symbol, valueDate))
            .ExecuteScalarAsync(MapToMaxValueDate!);

        return await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiTrendClassModel)}", MarketDataDbCql.GetFuturesItiTrendClassModel)
            .SetParameters(new GetFuturesItiTrendClassModel(symbol, valueDate: maxValueDate))
            .ExecuteSingleAsync(MapToFuturesItiTrendClassModel!);
    }


    /// <summary>
    /// Inserts a new record into the futures_eod_data_index table if it does not already exist.
    /// </summary>
    /// <param name="e">The FuturesEodDataIndexReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesEodDataIndexAsync(FuturesEodDataIndexReadModel e)
    => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataIndex)}", MarketDataDbCql.InsertFuturesEodDataIndex)
            .SetParameters(new InsertFuturesEodDataIndex(valueDate: e.ValueDate, contractId: e.ContractId))
            .ExecuteCommandAsync();

    /// <summary>
    /// insert futures iti trend delta model
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesItiTrendDeltaModelAsync(FuturesItiTrendDeltaModelReadModel e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiTrendDeltaModel)}", MarketDataDbCql.InsertFuturesItiTrendDeltaModel)
            .SetParameters(new InsertFuturesItiTrendDeltaModel(
                symbol: e.Symbol,
                valueDate: e.ValueDate,
                startDate: e.StartDate,
                endDate: e.EndDate,
                count: e.Count,
                maximum: e.Maximum,
                mean: e.Mean,
                median: e.Median,
                minimum: e.Minimum,
                skewness: e.Skewness,
                stdDev: e.StdDev,
                variance: e.Variance,
                meanAbsoluteError: e.MeanAbsoluteError,
                meanSquaredError: e.MeanSquaredError,
                rootMeanSquaredError: e.RootMeanSquaredError,
                lossFunction: e.LossFunction,
                rSquared: e.RSquared,
                modelData: e.ModelData
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// insert funtures iti trend class model
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFuturesItiTrendClassModelAsync(FuturesItiTrendClassModelReadModel e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiTrendClassModel)}", MarketDataDbCql.InsertFuturesItiTrendClassModel)
            .SetParameters(new InsertFuturesItiTrendClassModel(
                symbol: e.Symbol,
                valueDate: e.ValueDate,
                startDate: e.StartDate,
                endDate: e.EndDate,
                count: e.Count,
                maximum: e.Maximum,
                mean: e.Mean,
                median: e.Median,
                minimum: e.Minimum,
                skewness: e.Skewness,
                stdDev: e.StdDev,
                variance: e.Variance,
                accuracy: e.Accuracy,
                areaUnderPrecisionRecallCurve: e.AreaUnderPrecisionRecallCurve,
                areaUnderRocCurve: e.AreaUnderRocCurve,
                entropy: e.Entropy,
                f1Score: e.F1Score,
                modelData: e.ModelData
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a Futures TDI Signal asynchronously into the database.
    /// </summary>
    /// <param name="futuresTdiSignal">The Futures TDI Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesTdiSignalAsync(FuturesTdiSignalReadModel futuresTdiSignal)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTdiSignal)}", MarketDataDbCql.InsertFuturesTdiSignal)
            .SetParameters(new InsertFuturesTdiSignal(
                contractId: futuresTdiSignal.ContractId,
                timePeriod: futuresTdiSignal.TimePeriod.ToStringFast(),
                configurationId: futuresTdiSignal.ConfigurationId,
                valueDate: futuresTdiSignal.ValueDate,
                timestamp: futuresTdiSignal.Timestamp,
                schemaVersion: futuresTdiSignal.SchemaVersion,
                rsiPeriod: futuresTdiSignal.RsiPeriod,
                priceLinePeriod: futuresTdiSignal.PriceLinePeriod,
                signalLinePeriod: futuresTdiSignal.SignalLinePeriod,
                marketBasePeriod: futuresTdiSignal.MarketBasePeriod,
                volatilityBandPeriod: futuresTdiSignal.VolatilityBandPeriod,
                volatilityBandDeviation: futuresTdiSignal.VolatilityBandDeviation,
                price: futuresTdiSignal.Price,
                rsi: futuresTdiSignal.Rsi,
                priceLine: futuresTdiSignal.PriceLine,
                signalLine: futuresTdiSignal.SignalLine,
                marketBaseLine: futuresTdiSignal.MarketBaseLine,
                upperVolatilityBand: futuresTdiSignal.UpperVolatilityBand,
                lowerVolatilityBand: futuresTdiSignal.LowerVolatilityBand,
                bandWidth: futuresTdiSignal.BandWidth,
                priceSignalDivergence: futuresTdiSignal.PriceSignalDivergence,
                crossType: futuresTdiSignal.Cross.ToString(),
                marketState: futuresTdiSignal.MarketState.ToString(),
                trendDirection: futuresTdiSignal.TDI.ToStringFast(),
                trendStrength: futuresTdiSignal.TDIStrength.ToStringFast(),
                sourceSequence: futuresTdiSignal.SourceSequence,
                sourceEventTimestamp: futuresTdiSignal.SourceEventTimestamp
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a Futures MACD Signal asynchronously into the database.
    /// </summary>
    /// <param name="futuresMacdSignal">The Futures MACD Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesMacdSignalAsync(FuturesMacdSignalReadModel futuresMacdSignal)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesMacdSignal)}", MarketDataDbCql.InsertFuturesMacdSignal)
            .SetParameters(new InsertFuturesMacdSignal(
                contractId: futuresMacdSignal.ContractId,
                valueDate: futuresMacdSignal.ValueDate,
                timePeriod: futuresMacdSignal.TimePeriod.ToStringFast(),
                signalEmaPeriod: futuresMacdSignal.SignalEmaPeriod,
                fastEmaPeriod: futuresMacdSignal.FastEmaPeriod,
                slowEmaPeriod: futuresMacdSignal.SlowEmaPeriod,
                timestamp: futuresMacdSignal.Timestamp,
                futuresPrice: futuresMacdSignal.FuturesPrice,
                fastEma: futuresMacdSignal.FastEma,
                slowEma: futuresMacdSignal.SlowEma,
                macdLine: futuresMacdSignal.MacdLine,
                signalLine: futuresMacdSignal.SignalLine,
                histogram: futuresMacdSignal.Histogram,
                macd: futuresMacdSignal.MACD.ToStringFast(),
                macdStrength: futuresMacdSignal.MACDStrength.ToStringFast(),
                configurationId: futuresMacdSignal.Metadata?.CalculationConfigurationId,
                observationId: futuresMacdSignal.Metadata?.ObservationId.Value,
                marketDataAsOf: futuresMacdSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime,
                sourceSequence: futuresMacdSignal.Metadata?.SourceSequence,
                calculationVersion: futuresMacdSignal.Metadata?.CalculationVersion,
                calculationMethod: futuresMacdSignal.Metadata?.CalculationMethod.ToString(),
                schemaVersion: futuresMacdSignal.Metadata is { } macdMetadata ? macdMetadata.SchemaVersion : null,
                isValid: futuresMacdSignal.Metadata?.IsValid
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a Futures ATR Signal asynchronously into the database.
    /// </summary>
    /// <param name="futuresAtrSignal">The Futures ATR Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesAtrSignalAsync(FuturesAtrSignalReadModel futuresAtrSignal)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesAtrSignal)}", MarketDataDbCql.InsertFuturesAtrSignal)
            .SetParameters(new InsertFuturesAtrSignal(
                contractId: futuresAtrSignal.ContractId,
                valueDate: futuresAtrSignal.ValueDate,
                timePeriod: futuresAtrSignal.TimePeriod.ToStringFast(),
                periodLength: futuresAtrSignal.PeriodLength,
                timestamp: futuresAtrSignal.Timestamp,
                futuresPrice: futuresAtrSignal.FuturesPrice,
                atrValue: futuresAtrSignal.AtrValue,
                trueRange: futuresAtrSignal.TrueRange,
                atr: futuresAtrSignal.ATR.ToStringFast(),
                atrStrength: futuresAtrSignal.ATRStrength.ToStringFast(),
                configurationId: futuresAtrSignal.Metadata?.CalculationConfigurationId,
                observationId: futuresAtrSignal.Metadata?.ObservationId.Value,
                marketDataAsOf: futuresAtrSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime,
                sourceSequence: futuresAtrSignal.Metadata?.SourceSequence,
                calculationVersion: futuresAtrSignal.Metadata?.CalculationVersion,
                calculationMethod: futuresAtrSignal.Metadata?.CalculationMethod.ToString(),
                schemaVersion: futuresAtrSignal.Metadata is { } atrMetadata ? atrMetadata.SchemaVersion : null,
                isValid: futuresAtrSignal.Metadata?.IsValid,
                previousAtrValue: futuresAtrSignal.PreviousAtrValue,
                atrBaseline: futuresAtrSignal.AtrBaseline,
                atrRatio: futuresAtrSignal.AtrRatio,
                isWarm: futuresAtrSignal.IsWarm))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures ATR signal data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <param name="timePeriod">The signal time frame.</param>
    /// <param name="periodLength">The indicator period length.</param>
    public async Task DeleteFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesAtrSignal)}", MarketDataDbCql.DeleteFuturesAtrSignal)
            .SetParameters(new DeleteFuturesAtrSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a Futures ADX Signal
    /// </summary>
    /// <param name="futuresAdxSignal">The Futures ADX Signal to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesAdxSignalAsync(FuturesAdxSignalReadModel futuresAdxSignal)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesAdxSignal)}", MarketDataDbCql.InsertFuturesAdxSignal)
            .SetParameters(new InsertFuturesAdxSignal(
                contractId: futuresAdxSignal.ContractId,
                valueDate: futuresAdxSignal.ValueDate,
                timePeriod: futuresAdxSignal.TimePeriod.ToStringFast(),
                periodLength: futuresAdxSignal.PeriodLength,
                timestamp: futuresAdxSignal.Timestamp,
                futuresPrice: futuresAdxSignal.FuturesPrice,
                plusDI: futuresAdxSignal.PlusDI,
                minusDI: futuresAdxSignal.MinusDI,
                adxValue: futuresAdxSignal.AdxValue,
                adx: futuresAdxSignal.ADX.ToStringFast(),
                adxStrength: futuresAdxSignal.ADXStrength.ToStringFast(),
                configurationId: futuresAdxSignal.Metadata?.CalculationConfigurationId,
                observationId: futuresAdxSignal.Metadata?.ObservationId.Value,
                marketDataAsOf: futuresAdxSignal.Metadata?.MarketDataAsOfUtc.UtcDateTime,
                sourceSequence: futuresAdxSignal.Metadata?.SourceSequence,
                calculationVersion: futuresAdxSignal.Metadata?.CalculationVersion,
                calculationMethod: futuresAdxSignal.Metadata?.CalculationMethod.ToString(),
                schemaVersion: futuresAdxSignal.Metadata is { } metadata
                    ? metadata.SchemaVersion
                    : null,
                isValid: futuresAdxSignal.Metadata?.IsValid
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes futures ADX signal data for a given contract ID and value date.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <param name="timePeriod">The signal time frame.</param>
    /// <param name="periodLength">The indicator period length.</param>
    public async Task DeleteFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesAdxSignal)}", MarketDataDbCql.DeleteFuturesAdxSignal)
            .SetParameters(new DeleteFuturesAdxSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a futures trade signal
    /// </summary>
    /// <param name="FuturesTradeSignalV2ReadModel">The futures trade signal view model to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesTradeSignalAsync(FuturesTradeSignalV2ReadModel FuturesTradeSignalV2ReadModel)
    {
        var sequenceId = FuturesTradeSignalV2ReadModel.SequenceId > 0
            ? FuturesTradeSignalV2ReadModel.SequenceId
            : await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTradeSignal_SequenceId);
        var timePeriod = FuturesTradeSignalV2ReadModel.TimePeriod.ToStringFast();
        var db = _dbFactory.MarketDataDb;
        var insertSignal = db
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignal)}", MarketDataDbCql.InsertFuturesTradeSignal)
            .SetParameters(new InsertFuturesTradeSignal(
                contractId: FuturesTradeSignalV2ReadModel.ContractId,
                valueDate: FuturesTradeSignalV2ReadModel.ValueDate,
                timePeriod,
                sequenceId,
                timestamp: FuturesTradeSignalV2ReadModel.Timestamp,
                mean: FuturesTradeSignalV2ReadModel.Mean,
                stdDev: FuturesTradeSignalV2ReadModel.StdDev,
                futuresPrice: FuturesTradeSignalV2ReadModel.FuturesPrice,
                priceChangePercent: FuturesTradeSignalV2ReadModel.PriceChangePercent,
                fundRiskPercent: FuturesTradeSignalV2ReadModel.FundRiskPercent,
                rsi: FuturesTradeSignalV2ReadModel.RSI,
                rsiSlope: FuturesTradeSignalV2ReadModel.RSISlope,
                trendType: FuturesTradeSignalV2ReadModel.TrendType.ToStringFast(),
                trendStrength: FuturesTradeSignalV2ReadModel.TrendStrength.ToStringFast(),
                tradeSignal: FuturesTradeSignalV2ReadModel.TradeSignal.ToStringFast(),
                tdi: FuturesTradeSignalV2ReadModel.TDI.ToStringFast(),
                tdiStrength: FuturesTradeSignalV2ReadModel.TDIStrength.ToStringFast(),
                mdi: FuturesTradeSignalV2ReadModel.MDI,
                mdiTrend: FuturesTradeSignalV2ReadModel.MDITrend.ToStringFast(),
                mdiUpTrendLimit: FuturesTradeSignalV2ReadModel.MDIUpTrendLimit,
                mdiDownTrendLimit: FuturesTradeSignalV2ReadModel.MDIDownTrendLimit,
                upTrendingTrigger: FuturesTradeSignalV2ReadModel.UpTrendingTrigger,
                downTrendingTrigger: FuturesTradeSignalV2ReadModel.DownTrendingTrigger,
                entryTrigger: FuturesTradeSignalV2ReadModel.EntryTrigger,
                exitTrigger: FuturesTradeSignalV2ReadModel.ExitTrigger,
                trendDelta: FuturesTradeSignalV2ReadModel.TrendDelta,
                trendExtreme: FuturesTradeSignalV2ReadModel.TrendExtreme,
                trendReversal: FuturesTradeSignalV2ReadModel.TrendReversal,
                fiftyDma: FuturesTradeSignalV2ReadModel.FiftyDMA,
                twoHundredDma: FuturesTradeSignalV2ReadModel.TwoHundredDMA,
                tradeExecuteState: FuturesTradeSignalV2ReadModel.TradeExecuteState.ToStringFast()
            ))
            .QueueCommand();
        var insertLatestIndex = db
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalIndex)}", MarketDataDbCql.InsertFuturesTradeSignalIndex)
            .SetParameters(new InsertFuturesTradeSignalIndex(
                $"latest:{timePeriod}",
                "latest",
                sequenceId,
                FuturesTradeSignalV2ReadModel.ContractId,
                FuturesTradeSignalV2ReadModel.ValueDate,
                timePeriod))
            .QueueCommand();
        var insertDateIndex = db
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalIndex)}", MarketDataDbCql.InsertFuturesTradeSignalIndex)
            .SetParameters(new InsertFuturesTradeSignalIndex(
                $"date:{timePeriod}:{FuturesTradeSignalV2ReadModel.ValueDate.DayNumber}",
                FuturesTradeSignalV2ReadModel.ContractId,
                sequenceId,
                FuturesTradeSignalV2ReadModel.ContractId,
                FuturesTradeSignalV2ReadModel.ValueDate,
                timePeriod))
            .QueueCommand();
        await db.ExecuteQueuedCommandsAsync([insertSignal, insertLatestIndex, insertDateIndex]);
    }

    /// <summary>
    /// Inserts a collection of futures trade signals into the database asynchronously.
    /// </summary>
    /// <param name="futuresTradeSignals"></param>
    /// <returns></returns>
    public async Task InsertFuturesTradeSignalsAsync(ICollection<FuturesTradeSignalV2ReadModel> futuresTradeSignals)
    {
        var ftsQuery = new FuturesTradeSignalV2ReadModel[futuresTradeSignals.Count];
        var signalIndex = 0;
        foreach (var signal in futuresTradeSignals)
        {
            var sequenceId = await _sequenceIdGenerator
                .GetSequenceIdAsync(SequenceName.FuturesTradeSignal_SequenceId)
                .ConfigureAwait(false);
            ftsQuery[signalIndex++] = signal with { SequenceId = sequenceId };
        }
        var db = _dbFactory.MarketDataDb;
        var insertSignals = db
           .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignal)}", MarketDataDbCql.InsertFuturesTradeSignal)
           .SetParameters(ftsQuery.Select(e => new InsertFuturesTradeSignal(
               contractId: e.ContractId,
               valueDate: e.ValueDate,
               timePeriod: e.TimePeriod.ToStringFast(),
               sequenceId: e.SequenceId,
               timestamp: e.Timestamp,
               mean: e.Mean,
               stdDev: e.StdDev,
               futuresPrice: e.FuturesPrice,
               priceChangePercent: e.PriceChangePercent,
               fundRiskPercent: e.FundRiskPercent,
               rsi: e.RSI,
               rsiSlope: e.RSISlope,
               trendType: e.TrendType.ToStringFast(),
               trendStrength: e.TrendStrength.ToStringFast(),
               tradeSignal: e.TradeSignal.ToStringFast(),
               tdi: e.TDI.ToStringFast(),
               tdiStrength: e.TDIStrength.ToStringFast(),
               mdi: e.MDI,
               mdiTrend: e.MDITrend.ToStringFast(),
               mdiUpTrendLimit: e.MDIUpTrendLimit,
               mdiDownTrendLimit: e.MDIDownTrendLimit,
               upTrendingTrigger: e.UpTrendingTrigger,
               downTrendingTrigger: e.DownTrendingTrigger,
               entryTrigger: e.EntryTrigger,
               exitTrigger: e.ExitTrigger,
               trendDelta: e.TrendDelta,
               trendExtreme: e.TrendExtreme,
               trendReversal: e.TrendReversal,
               fiftyDma: e.FiftyDMA,
               twoHundredDma: e.TwoHundredDMA,
               tradeExecuteState: e.TradeExecuteState.ToStringFast()
           )))
           .QueueCommand();
        var insertIndex = db
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalIndex)}", MarketDataDbCql.InsertFuturesTradeSignalIndex)
            .SetParameters(ftsQuery.SelectMany(e =>
            {
                var timePeriod = e.TimePeriod.ToStringFast();
                return new InsertFuturesTradeSignalIndex[]
                {
                    new($"latest:{timePeriod}", "latest", e.SequenceId, e.ContractId, e.ValueDate, timePeriod),
                    new($"date:{timePeriod}:{e.ValueDate.DayNumber}", e.ContractId, e.SequenceId, e.ContractId, e.ValueDate, timePeriod)
                };
            }))
            .QueueCommand();
        await db.ExecuteQueuedCommandsAsync([insertSignals, insertIndex]).ConfigureAwait(false);
    }

    public async Task<long> InsertFuturesTradeSignalsAsync(IEnumerable<FuturesTradeSignalV2ReadModel> futuresTradeSignals)
    {
        var signals = futuresTradeSignals as IReadOnlyCollection<FuturesTradeSignalV2ReadModel>
            ?? futuresTradeSignals.ToArray();
        var rowCount = signals.Count;
        var ftsQuery = new FuturesTradeSignalV2ReadModel[rowCount];
        var signalIndex = 0;
        foreach (var signal in signals)
        {
            var sequenceId = await _sequenceIdGenerator
                .GetSequenceIdAsync(SequenceName.FuturesTradeSignal_SequenceId)
                .ConfigureAwait(false);
            ftsQuery[signalIndex++] = signal with { SequenceId = sequenceId };
        }
        var db = _dbFactory.MarketDataDb;
        var insertSignals = db
           .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignal)}", MarketDataDbCql.InsertFuturesTradeSignal)
           .SetParameters(ftsQuery.Select(e => new InsertFuturesTradeSignal(
               contractId: e.ContractId,
               valueDate: e.ValueDate,
               timePeriod: e.TimePeriod.ToStringFast(),
               sequenceId: e.SequenceId,
               timestamp: e.Timestamp,
               mean: e.Mean,
               stdDev: e.StdDev,
               futuresPrice: e.FuturesPrice,
               priceChangePercent: e.PriceChangePercent,
               fundRiskPercent: e.FundRiskPercent,
               rsi: e.RSI,
               rsiSlope: e.RSISlope,
               trendType: e.TrendType.ToStringFast(),
               trendStrength: e.TrendStrength.ToStringFast(),
               tradeSignal: e.TradeSignal.ToStringFast(),
               tdi: e.TDI.ToStringFast(),
               tdiStrength: e.TDIStrength.ToStringFast(),
               mdi: e.MDI,
               mdiTrend: e.MDITrend.ToStringFast(),
               mdiUpTrendLimit: e.MDIUpTrendLimit,
               mdiDownTrendLimit: e.MDIDownTrendLimit,
               upTrendingTrigger: e.UpTrendingTrigger,
               downTrendingTrigger: e.DownTrendingTrigger,
               entryTrigger: e.EntryTrigger,
               exitTrigger: e.ExitTrigger,
               trendDelta: e.TrendDelta,
               trendExtreme: e.TrendExtreme,
               trendReversal: e.TrendReversal,
               fiftyDma: e.FiftyDMA,
               twoHundredDma: e.TwoHundredDMA,
               tradeExecuteState: e.TradeExecuteState.ToStringFast()
           )))
           .QueueCommand();
        var insertIndex = db
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTradeSignalIndex)}", MarketDataDbCql.InsertFuturesTradeSignalIndex)
            .SetParameters(ftsQuery.SelectMany(e =>
            {
                var timePeriod = e.TimePeriod.ToStringFast();
                return new InsertFuturesTradeSignalIndex[]
                {
                    new($"latest:{timePeriod}", "latest", e.SequenceId, e.ContractId, e.ValueDate, timePeriod),
                    new($"date:{timePeriod}:{e.ValueDate.DayNumber}", e.ContractId, e.SequenceId, e.ContractId, e.ValueDate, timePeriod)
                };
            }))
            .QueueCommand();
        await db.ExecuteQueuedCommandsAsync([insertSignals, insertIndex]).ConfigureAwait(false);
        return rowCount;
    }

    /// <summary>
    /// Inserts a rate of return record into the database asynchronously.
    /// </summary>
    /// <param name="e">The rate of return data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertRateOfReturnAsync(RateOfReturnReadModel e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertRateOfReturn)}", MarketDataDbCql.InsertRateOfReturn)
            .SetParameters(new InsertRateOfReturn(
                symbol: e.Symbol,
                valueDate: e.ValueDate,
                rateOfReturn: e.RateOfReturn
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a market holiday record into the database asynchronously.
    /// </summary>
    /// <param name="e">The MarketHolidayReadModel containing the data to insert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertMarketHolidayAsync(MarketHolidayReadModel e)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketHoliday)}", MarketDataDbCql.InsertMarketHoliday)
            .SetParameters(new InsertMarketHoliday(
                currencyType: e.CurrencyType.ToStringFast(),
                holidayDate: e.HolidayDate,
                description: e.Description
            ))
            .ExecuteCommandAsync();

    /// <summary>
    /// load futures iti trend class  data by date range into
    /// </summary>
    /// <param name="e"></param>
    public async Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbReader = db as IMarketDataDbReadContext;
        var futuresItiSignals = await dbReader!.GetFuturesItiSignalTrendClassDataAsync(symbol, startDate, endDate);
        var sourceSignals = futuresItiSignals
            .GroupBy(e => (e.ContractId, e.ValueDate, e.IntrinsicTimeGroupId))
            .Select(e => e.OrderByDescending(signal => signal.SequenceId).First())
            .ToArray();
        if (sourceSignals.Length == 0)
            return FuturesItiTrendModelDataStatistics.Empty;

        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiTrendClassData)}", MarketDataDbCql.DeleteFuturesItiTrendClassData)
            .SetParameters(new DeleteFuturesItiTrendClassData(symbol, startDate, endDate))
            .ExecuteCommandAsync();
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiTrendClassData)}", MarketDataDbCql.InsertFuturesItiTrendClassData)
            .SetParameters(sourceSignals.Select(e => new InsertFuturesItiTrendClassData(
                symbol,
                e.ValueDate,
                e.IntrinsicTime,
                e.SequenceId,
                (float)e.IntrinsicTimeGroupId,
                (float)e.IntrinsicTimeTrend,
                (float)e.IntrinsicTimeMode,
                (float)e.TrendDelta,
                0f)))
            .ExecuteCommandAsync();
        return CalculateStatistics(sourceSignals.Select(e => (double)e.IntrinsicTimeGroupId));
    }

    /// <summary>
    /// load futures iti trend delta data by date range into
    /// </summary>
    /// <param name="e"></param>
    public async Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbReader = db as IMarketDataDbReadContext;
        var futuresItiSignals = await dbReader!.GetFuturesItiSignalTrendDeltaDataAsync(symbol, startDate, endDate);
        var sourceSignals = futuresItiSignals
            .GroupBy(e => (e.ContractId, e.ValueDate, e.IntrinsicTimeGroupId))
            .Select(e => e.OrderByDescending(signal => signal.SequenceId).First())
            .ToArray();
        if (sourceSignals.Length == 0)
            return FuturesItiTrendModelDataStatistics.Empty;

        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteFuturesItiTrendDeltaData)}", MarketDataDbCql.DeleteFuturesItiTrendDeltaData)
            .SetParameters(new DeleteFuturesItiTrendDeltaData(symbol, startDate, endDate))
            .ExecuteCommandAsync();
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiTrendDeltaData)}", MarketDataDbCql.InsertFuturesItiTrendDeltaData)
            .SetParameters(sourceSignals.Select(e => new InsertFuturesItiTrendDeltaData(
                symbol,
                e.ValueDate,
                e.IntrinsicTime,
                e.SequenceId,
                (float)e.TrendDelta,
                (float)e.IntrinsicTimeTrend,
                (float)e.IntrinsicTimeMode,
                (float)e.IntrinsicPrice,
                (float)e.TrendExtreme,
                0f)))
            .ExecuteCommandAsync();
        return CalculateStatistics(sourceSignals.Select(e => e.TrendDelta));
    }

    static FuturesItiTrendModelDataStatistics CalculateStatistics(IEnumerable<double> source)
    {
        var values = source.OrderBy(e => e).ToArray();
        if (values.Length == 0)
            return FuturesItiTrendModelDataStatistics.Empty;

        var mean = values.Average();
        var variance = values.Average(e => Math.Pow(e - mean, 2));
        var stdDev = Math.Sqrt(variance);
        var median = values.Length % 2 == 0
            ? (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2
            : values[values.Length / 2];
        var skewness = stdDev == 0
            ? 0
            : values.Average(e => Math.Pow((e - mean) / stdDev, 3));
        return new FuturesItiTrendModelDataStatistics(
            values.Length,
            values[^1],
            mean,
            median,
            values[0],
            skewness,
            stdDev,
            variance);
    }

    /// <summary>
    /// Upserts a FuturesEodDataV2ReadModel into the database.
    /// </summary>
    /// <param name="e">The futures EOD data to upsert.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InsertFuturesEodDataAsync(FuturesEodDataV2ReadModel e)
    {
        // check if the data already exists...
        var db = _dbFactory.MarketDataDb;
        var existingData = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesDataId)}", MarketDataDbCql.GetFuturesDataId)
            .SetParameters(new GetFuturesDataId(contractId: e.ContractId, valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesDataId!);

        if (existingData is null)
        {
            // insert new data if it doesn't exist...
            var openPrice = e.OpenPrice;
            await ExecuteMaintainedProjectionMutationAsync(
                FuturesEodProjection,
                new[] { GetFuturesEodScopeKey(e.ValueDate) },
                async () =>
            {
                await InsertFuturesEodDataIndexAsync(new FuturesEodDataIndexReadModel(e.ValueDate, e.ContractId));
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodData)}", MarketDataDbCql.InsertFuturesEodData)
                .SetParameters(new InsertFuturesEodData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    symbol: e.Symbol,
                    openPrice,
                    highPrice: e.HighPrice,
                    lowPrice: e.LowPrice,
                    closePrice: e.ClosePrice,
                    volume: e.Volume,
                    dailyPercentChange: e.DailyPercentChange,
                    dailyStdDev: e.DailyStdDev,
                    dailyStdDevAmount: e.DailyStdDevAmount,
                    upperBand: e.UpperBand,
                    mean: e.Mean,
                    lowerBand: e.LowerBand,
                    marketDirection: e.MarketDirection.ToStringFast(),
                    marketVolatility: e.MarketVolatility.ToStringFast(),
                    priceDirection: e.PriceDirection.ToStringFast(),
                    priceVolatility: e.PriceVolatility.ToStringFast(),
                    marketDirectionIndicator: e.MarketDirectionIndicator,
                    windowSize: e.WindowSize,
                    fiftyDMA: e.FiftyDMA,
                    twoHundredDMA: e.TwoHundredDMA
                ))
                    .ExecuteCommandAsync();

                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesIntraDayData)}", MarketDataDbCql.InsertFuturesIntraDayData)
                    .SetParameters(new InsertFuturesIntraDayData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    sequenceId: await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesIntraDay_SequenceId),
                    symbol: e.Symbol,
                    openPrice,
                    highPrice: e.HighPrice,
                    lowPrice: e.LowPrice,
                    closePrice: e.ClosePrice,
                    volume: e.Volume,
                    dailyPercentChange: e.DailyPercentChange,
                    dailyStdDev: e.DailyStdDev,
                    dailyStdDevAmount: e.DailyStdDevAmount,
                    upperBand: e.UpperBand,
                    mean: e.Mean,
                    lowerBand: e.LowerBand,
                    marketDirection: e.MarketDirection.ToStringFast(),
                    marketVolatility: e.MarketVolatility.ToStringFast(),
                    priceDirection: e.PriceDirection.ToStringFast(),
                    priceVolatility: e.PriceVolatility.ToStringFast(),
                    marketDirectionIndicator: e.MarketDirectionIndicator,
                    windowSize: e.WindowSize
                ))
                    .ExecuteCommandAsync();
                await UpsertFuturesEodProjectionAsync(e, openPrice);
            });
        }
        else
        {
            // Update existing data if it exists
            var openPrice = e.OpenPrice;
            await ExecuteMaintainedProjectionMutationAsync(
                FuturesEodProjection,
                new[] { GetFuturesEodScopeKey(e.ValueDate) },
                async () =>
            {
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpdateFuturesEodData)}", MarketDataDbCql.UpdateFuturesEodData)
                    .SetParameters(new UpdateFuturesEodData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    symbol: e.Symbol,
                    openPrice,
                    highPrice: e.HighPrice,
                    lowPrice: e.LowPrice,
                    closePrice: e.ClosePrice,
                    volume: e.Volume,
                    dailyPercentChange: e.DailyPercentChange,
                    dailyStdDev: e.DailyStdDev,
                    dailyStdDevAmount: e.DailyStdDevAmount,
                    upperBand: e.UpperBand,
                    mean: e.Mean,
                    lowerBand: e.LowerBand,
                    marketDirection: e.MarketDirection.ToStringFast(),
                    marketVolatility: e.MarketVolatility.ToStringFast(),
                    priceDirection: e.PriceDirection.ToStringFast(),
                    priceVolatility: e.PriceVolatility.ToStringFast(),
                    marketDirectionIndicator: e.MarketDirectionIndicator,
                    windowSize: e.WindowSize,
                    fiftyDMA: e.FiftyDMA,
                    twoHundredDMA: e.TwoHundredDMA
                ))
                    .ExecuteCommandAsync();

                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesIntraDayData)}", MarketDataDbCql.InsertFuturesIntraDayData)
                    .SetParameters(new InsertFuturesIntraDayData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    sequenceId: await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesIntraDay_SequenceId),
                    symbol: e.Symbol,
                    openPrice,
                    highPrice: e.HighPrice,
                    lowPrice: e.LowPrice,
                    closePrice: e.ClosePrice,
                    volume: e.Volume,
                    dailyPercentChange: e.DailyPercentChange,
                    dailyStdDev: e.DailyStdDev,
                    dailyStdDevAmount: e.DailyStdDevAmount,
                    upperBand: e.UpperBand,
                    mean: e.Mean,
                    lowerBand: e.LowerBand,
                    marketDirection: e.MarketDirection.ToStringFast(),
                    marketVolatility: e.MarketVolatility.ToStringFast(),
                    priceDirection: e.PriceDirection.ToStringFast(),
                    priceVolatility: e.PriceVolatility.ToStringFast(),
                    marketDirectionIndicator: e.MarketDirectionIndicator,
                    windowSize: e.WindowSize
                ))
                    .ExecuteCommandAsync();
                await UpsertFuturesEodProjectionAsync(e, openPrice);
            });
        }
    }

    /// <summary>
    /// Updates provider-supplied session prices and the two metrics that depend on
    /// the session open. The immutable intraday observation table is intentionally
    /// not appended for a statistics-only correction.
    /// </summary>
    public async Task UpdateFuturesEodSessionStatisticsAsync(
        FuturesEodDataV2ReadModel e)
    {
        var db = _dbFactory.MarketDataDb;
        var existingData = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesDataId)}", MarketDataDbCql.GetFuturesDataId)
            .SetParameters(new GetFuturesDataId(
                contractId: e.ContractId,
                valueDate: e.ValueDate))
            .ExecuteSingleAsync(MapToFuturesDataId!);
        if (existingData is null)
            throw new InvalidOperationException(
                $"Futures EOD row '{e.ContractId}:{e.ValueDate:yyyy-MM-dd}' does not exist.");

        await ExecuteMaintainedProjectionMutationAsync(
            FuturesEodProjection,
            new[] { GetFuturesEodScopeKey(e.ValueDate) },
            async () =>
            {
                List<object> commands =
                [
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpdateFuturesEodSessionStatistics)}", MarketDataDbCql.UpdateFuturesEodSessionStatistics)
                        .SetParameters(new UpdateFuturesEodSessionStatistics(
                            contractId: e.ContractId,
                            valueDate: e.ValueDate,
                            symbol: e.Symbol,
                            openPrice: e.OpenPrice,
                            highPrice: e.HighPrice,
                            lowPrice: e.LowPrice,
                            volume: e.Volume,
                            dailyPercentChange: e.DailyPercentChange,
                            priceDirection: e.PriceDirection.ToStringFast()))
                        .QueueCommand(),
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataByMonth)}", MarketDataDbCql.InsertFuturesEodDataByMonth)
                        .SetParameters(CreateFuturesEodDataByMonthParameters(e, e.OpenPrice))
                        .QueueCommand(),
                    db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                        .SetParameters(new InsertMarketDataProjectionMonth(
                            FuturesEodProjection,
                            ToYearMonth(e.ValueDate)))
                        .QueueCommand()
                ];
                await db.ExecuteQueuedCommandsAsync(commands);
            });
    }

    public async Task InsertFuturesEodDataAsync(ICollection<FuturesEodDataV2ReadModel> futuresEodData)
        => await InsertFuturesEodBatchAsync(futuresEodData);

    /// <summary>
    /// Inserts a collection of futures end-of-day (EOD) data records into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided futures EOD data and inserts it into the database. The
    /// method ensures that all records in the collection are processed sequentially, and the total count of processed
    /// records is returned.</remarks>
    /// <param name="futuresEodData">A collection of <see cref="FuturesEodDataV2ReadModel"/> objects representing the futures EOD data to be
    /// inserted. Each object must contain valid data for all required fields.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// records processed.</returns>
    public async Task<long> InsertFuturesEodDataAsync(IEnumerable<FuturesEodDataV2ReadModel> futuresEodData)
    {
        var rowCount = 0l;
        List<FuturesEodDataV2ReadModel> batch = new(ProjectionWriteBatchSize);
        foreach (var e in futuresEodData)
        {
            batch.Add(e);
            rowCount++;
            if (batch.Count == ProjectionWriteBatchSize)
            {
                await InsertFuturesEodBatchAsync(batch);
                batch.Clear();
            }
        }

        await InsertFuturesEodBatchAsync(batch);
        return rowCount;
    }

    /// <summary>
    /// Upserts a VixFuturesEodDataReadModel into the database.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertVixFuturesEodDataAsync(
        FuturesTickDataV2ReadModel e,
        FuturesSessionStatisticsSnapshot? sessionStatistics = null)
    {
        // check if the data already exists...
        var db = _dbFactory.MarketDataDb;
        var existingData = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodData)}", MarketDataDbCql.GetVixFuturesEodData)
            .SetParameters(new GetVixFuturesEodData(
                contractId: e.ContractId,
                valueDate: e.ValueDate
            ))
            .ExecuteSingleAsync(MapToVixFuturesEodData!);

        if (existingData == null)
        {
            var hasPrices = sessionStatistics is { HasPriceStatistics: true };
            var openPrice = hasPrices ? sessionStatistics.Value.OpenPrice : e.Price;
            var highPrice = hasPrices ? sessionStatistics.Value.HighPrice : e.Price;
            var lowPrice = hasPrices ? sessionStatistics.Value.LowPrice : e.Price;
            var volume = sessionStatistics is { HasVolume: true }
                ? sessionStatistics.Value.Volume
                : e.Size;
            await ExecuteMaintainedProjectionMutationAsync(
                VixFuturesContractIndexProjection,
                new[] { GetVixContractIndexScopeKey(e.ContractId) },
                async () =>
            {
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertVixFuturesEodData)}", MarketDataDbCql.InsertVixFuturesEodData)
                   .SetParameters(new InsertVixFuturesEodData(
                       contractId: e.ContractId,
                       valueDate: e.ValueDate,
                       openPrice,
                       highPrice,
                       lowPrice,
                       closePrice: e.Price,
                       volume
                   ))
                   .ExecuteCommandAsync();
                await UpsertVixFuturesContractIndexAsync(e.ContractId);
            });
        }
        else
        {
            // The contract index is unchanged for an update to an existing canonical
            // partition, so avoid an index and projection-state write on every VX observation.
            // Derive the rolling row only from its current stored state and the incoming
            // trade-or-quote observation; tick storage is an independent realtime projection.
            var hasPrices = sessionStatistics is { HasPriceStatistics: true };
            var openPrice = hasPrices
                ? sessionStatistics.Value.OpenPrice
                : existingData.OpenPrice;
            var highPrice = hasPrices
                ? sessionStatistics.Value.HighPrice
                : Math.Max(existingData.HighPrice, e.Price);
            var lowPrice = hasPrices
                ? sessionStatistics.Value.LowPrice
                : Math.Min(existingData.LowPrice, e.Price);
            var volume = sessionStatistics is { HasVolume: true }
                ? sessionStatistics.Value.Volume
                : checked(existingData.Volume + e.Size);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpdateVixFuturesEodData)}", MarketDataDbCql.UpdateVixFuturesEodData)
               .SetParameters(new UpdateVixFuturesEodData(
                    contractId: e.ContractId,
                    valueDate: e.ValueDate,
                    openPrice,
                    highPrice,
                    lowPrice,
                    closePrice: e.Price,
                    volume
                ))
               .ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// Gets a collection of Futures ITI Signal MDI for a given entity ID.
    /// </summary>
    /// <param name="e">The entity ID containing the contract ID and value date.</param>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref="FuturesItiSignalMDIViewModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate)
    {
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged
        };
        var latest = await Task.WhenAll(modes.Select(mode => ReadLastFuturesItiTrendModeAsync(
            contractId, valueDate, IntrinsicTimeTrendType.UpTrend, mode)));
        var maxValueDate = latest.Where(static row => row is not null)
            .Select(static row => row!.ValueDate)
            .DefaultIfEmpty()
            .Max();
        if (maxValueDate == default)
            return [];
        var rows = await Task.WhenAll(modes.Select(mode =>
            ReadFuturesItiDayModeAsync(contractId, maxValueDate, mode)));
        return [.. rows.SelectMany(static values => values).Select(ToFuturesItiSignalMdi)];
    }

    /// <summary>
    /// Gets a collection of Futures ITI Signal MDI by trend for a given entity ID, intrinsic time trend, and intrinsic time group ID.
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="valueDate"> </param>
    /// <param name="intrinsicTimeTrend">The intrinsic time trend.</param>
    /// <param name="intrinsicTimeGroupId">The intrinsic time group ID.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref="FuturesItiSignalMDIViewModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, int intrinsicTimeGroupId)
    {
        _ = intrinsicTimeGroupId; // The legacy query never applied this argument.
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged
        };
        var latest = await Task.WhenAll(modes.Select(mode => ReadLastFuturesItiTrendModeAsync(
            contractId, valueDate, intrinsicTimeTrend, mode)));
        var maxValueDate = latest.Where(static row => row is not null)
            .Select(static row => row!.ValueDate)
            .DefaultIfEmpty()
            .Max();
        if (maxValueDate == default)
            return [];
        var rows = await Task.WhenAll(modes.Select(mode =>
            ReadFuturesItiDayModeAsync(contractId, maxValueDate, mode)));
        return [.. rows.SelectMany(static values => values)
            .Where(row => row.IntrinsicTimeTrend == intrinsicTimeTrend)
            .Select(ToFuturesItiSignalMdi)];
    }

    /// <summary>
    /// Gets a collection of futures ITI trend direction changed signals for a given entity ID.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="timestamp"></param>
    /// <param name="lookbackInterval"></param>
    /// <param name="startTime"></param>
    /// <param name="endTime"></param>
    /// <returns></returns>
    public async Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime)
    {
        var db = _dbFactory.MarketDataDb;
        var rsiValues = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesRsiSignalsForTrend)}", MarketDataDbCql.GetFuturesRsiSignalsForTrend)
            .SetParameters(new GetFuturesRsiSignalsForTrend(
                contractId,
                timePeriod.ToStringFast(),
                periodLength,
                valueDate,
                TimeOnly.FromDateTime(startTime),
                TimeOnly.FromDateTime(endTime)
            ))
            .ExecuteQueryAsync(MapToRsi!);
        var upTrendCount = rsiValues.Count(static rsi => rsi >= 50);
        var downTrendCount = rsiValues.Count(static rsi => rsi < 50);

        var trendDirection = default(FuturesTrendType) switch
        {
            _ when upTrendCount > downTrendCount => FuturesTrendType.UpTrending,
            _ when upTrendCount < downTrendCount => FuturesTrendType.DownTrending,
            _ when upTrendCount == downTrendCount => FuturesTrendType.RangeBound,
            _ => FuturesTrendType.RangeBound
        };
        return new FuturesTrendDirectionReadModel(
            ContractId: contractId,
            ValueDate: valueDate,
            Timestamp: TimeOnly.FromDateTime(DateTime.Now),
            LookbackInterval: lookbackInterval,
            UpTrendCount: upTrendCount,
            DownTrendCount: downTrendCount,
            TrendDirection: trendDirection);
    }

    /// <summary>
    /// return last futures intrinsic time indicator signal
    /// </summary>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate)
        => (await ReadFuturesItiDayModeAsync(
            contractId, valueDate, IntrinsicTimeModeType.TrendReversalChanged))
            .FirstOrDefault();

    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => (await ReadFuturesItiDayModeAsync(
            contractId,
            valueDate,
            IntrinsicTimeModeType.TrendReversalChanged,
            cancellationToken: cancellationToken).ConfigureAwait(false))
            .FirstOrDefault();

    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesItiSignalByTimePeriod)}", MarketDataDbCql.GetLastFuturesItiSignalByTimePeriod)
            .SetParameters(new GetLastFuturesItiSignalByTimePeriod(
                contractId,
                valueDate,
                timePeriod.ToString()))
            .ExecuteSingleAsync(MapToFuturesItiSignal!);

    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesItiSignalByTimePeriod)}", MarketDataDbCql.GetLastFuturesItiSignalByTimePeriod)
            .SetParameters(new GetLastFuturesItiSignalByTimePeriod(
                contractId,
                valueDate,
                timePeriod.ToString()))
            .ExecuteSingleAsync(MapToFuturesItiSignal!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// return last futures intrinsic time indicator signal from trend direction change
    /// </summary>\
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateOnly valueDate)
        => (await ReadFuturesItiDayModeAsync(
            contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged))
            .FirstOrDefault();

    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendDirectionChangeAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => (await ReadFuturesItiDayModeAsync(
            contractId,
            valueDate,
            IntrinsicTimeModeType.TrendDirectionChanged,
            cancellationToken: cancellationToken).ConfigureAwait(false))
            .FirstOrDefault();

    /// <summary>
    /// return last futures intrinsic time indicator signal from trend extreme change
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateOnly valueDate)
    {
        var direction = (await ReadFuturesItiDayModeAsync(
            contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged)).FirstOrDefault();
        return (await ReadFuturesItiDayModeAsync(
            contractId,
            valueDate,
            IntrinsicTimeModeType.TrendExtremeChanged,
            direction?.SequenceId ?? 0)).FirstOrDefault();
    }

    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendExtremeChangeAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        var direction = (await ReadFuturesItiDayModeAsync(
            contractId,
            valueDate,
            IntrinsicTimeModeType.TrendDirectionChanged,
            cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        return (await ReadFuturesItiDayModeAsync(
            contractId,
            valueDate,
            IntrinsicTimeModeType.TrendExtremeChanged,
            direction?.SequenceId ?? 0,
            cancellationToken).ConfigureAwait(false)).FirstOrDefault();
    }

    /// <summary>
    /// return last futures intrinsic time indicator signal from trend reversal change
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateOnly valueDate)
    {
        var direction = (await ReadFuturesItiDayModeAsync(
            contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged)).FirstOrDefault();
        return (await ReadFuturesItiDayModeAsync(
            contractId,
            valueDate,
            IntrinsicTimeModeType.TrendReversalChanged,
            direction?.SequenceId ?? 0)).FirstOrDefault();
    }

    public async Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendReversalChangeAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        var direction = (await ReadFuturesItiDayModeAsync(
            contractId,
            valueDate,
            IntrinsicTimeModeType.TrendDirectionChanged,
            cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        return (await ReadFuturesItiDayModeAsync(
            contractId,
            valueDate,
            IntrinsicTimeModeType.TrendReversalChanged,
            direction?.SequenceId ?? 0,
            cancellationToken).ConfigureAwait(false)).FirstOrDefault();
    }

    /// <summary>
    /// Gets the last Futures RSI signal by value date.
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="valueDate">The value date.</param>
    /// <param name="timePeriod">The time period.</param>
    /// <param name="periodLength">The period length.</param>
    /// <returns></returns>
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesRsiSignal)}", MarketDataDbCql.GetLastFuturesRsiSignal)
            .SetParameters(new GetLastFuturesRsiSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(MapToFuturesRsiSignal);

    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesRsiSignal)}", MarketDataDbCql.GetLastFuturesRsiSignal)
            .SetParameters(new GetLastFuturesRsiSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(MapToFuturesRsiSignal, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the last Futures RSI signal by time period and period length.
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="timePeriod">The time period.</param>
    /// <param name="periodLength">The period length.</param>
    /// <returns></returns>
    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesRsiDailySignal)}", MarketDataDbCql.GetLastFuturesRsiDailySignal)
            .SetParameters(new GetLastFuturesRsiDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(MapToFuturesRsiSignal);

    public async Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesRsiDailySignal)}", MarketDataDbCql.GetLastFuturesRsiDailySignal)
            .SetParameters(new GetLastFuturesRsiDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(MapToFuturesRsiSignal, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the last Futures TDI signal for a given entity ID.
    /// </summary>
    /// <param name="e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesTdiSignalReadModel"/>.</returns>
    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate)
        => await GetLastFuturesTdiSignalAsync(
            contractId,
            valueDate,
            TimeFrameType.OneMinute,
            FuturesTdiConfiguration.StandardConfigurationId);

    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        string configurationId)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTdiSignal)}", MarketDataDbCql.GetLastFuturesTdiSignal)
            .SetParameters(new GetLastFuturesTdiSignal(
                contractId,
                timePeriod.ToStringFast(),
                configurationId,
                valueDate
            ))
            .ExecuteSingleAsync(MapToFuturesTdiSignal!);

    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(
        string contractId, DateOnly valueDate, CancellationToken cancellationToken)
        => await GetLastFuturesTdiSignalAsync(
            contractId,
            valueDate,
            TimeFrameType.OneMinute,
            FuturesTdiConfiguration.StandardConfigurationId,
            cancellationToken);

    public async Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        string configurationId,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTdiSignal)}", MarketDataDbCql.GetLastFuturesTdiSignal)
            .SetParameters(new GetLastFuturesTdiSignal(
                contractId,
                timePeriod.ToStringFast(),
                configurationId,
                valueDate))
            .ExecuteSingleAsync(MapToFuturesTdiSignal!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the last Futures MACD signal
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="valueDate">The value date.</param>
    /// <param name="timePeriod"></param>
    /// <param name="periodLength"></param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesMacdSignalReadModel"/>.</returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
        => await GetLastFuturesMacdSignalAsync(
            contractId,
            valueDate,
            timePeriod,
            periodLength,
            FuturesMacdConfiguration.ConventionalFastEmaPeriod,
            FuturesMacdConfiguration.ConventionalSlowEmaPeriod);

    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesMacdSignal)}", MarketDataDbCql.GetLastFuturesMacdSignal)
            .SetParameters(new GetLastFuturesMacdSignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, valueDate))
            .ExecuteSingleAsync(MapToFuturesMacdSignal!);

    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken)
        => await GetLastFuturesMacdSignalAsync(
            contractId,
            valueDate,
            timePeriod,
            periodLength,
            FuturesMacdConfiguration.ConventionalFastEmaPeriod,
            FuturesMacdConfiguration.ConventionalSlowEmaPeriod,
            cancellationToken);

    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesMacdSignal)}", MarketDataDbCql.GetLastFuturesMacdSignal)
            .SetParameters(new GetLastFuturesMacdSignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, valueDate))
            .ExecuteSingleAsync(MapToFuturesMacdSignal!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="timePeriod"></param>
    /// <param name="periodLength"></param>
    /// <returns></returns>
    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength)
        => await GetLastFuturesMacdDailySignalAsync(
            contractId,
            timePeriod,
            periodLength,
            FuturesMacdConfiguration.ConventionalFastEmaPeriod,
            FuturesMacdConfiguration.ConventionalSlowEmaPeriod);

    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesMacdDailySignal)}", MarketDataDbCql.GetLastFuturesMacdDailySignal)
            .SetParameters(new GetLastFuturesMacdDailySignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod))
            .ExecuteSingleAsync(MapToFuturesMacdSignal!);

    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken)
        => await GetLastFuturesMacdDailySignalAsync(
            contractId,
            timePeriod,
            periodLength,
            FuturesMacdConfiguration.ConventionalFastEmaPeriod,
            FuturesMacdConfiguration.ConventionalSlowEmaPeriod,
            cancellationToken);

    public async Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(
        string contractId,
        TimeFrameType timePeriod,
        int signalEmaPeriod,
        int fastEmaPeriod,
        int slowEmaPeriod,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesMacdDailySignal)}", MarketDataDbCql.GetLastFuturesMacdDailySignal)
            .SetParameters(new GetLastFuturesMacdDailySignal(contractId, timePeriod.ToStringFast(), signalEmaPeriod, fastEmaPeriod, slowEmaPeriod))
            .ExecuteSingleAsync(MapToFuturesMacdSignal!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the last Futures ATR signal
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesAtrSignalReadModel"/>.</returns>
    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAtrSignal)}", MarketDataDbCql.GetLastFuturesAtrSignal)
            .SetParameters(new GetLastFuturesAtrSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(MapToFuturesAtrSignal!);

    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAtrSignal)}", MarketDataDbCql.GetLastFuturesAtrSignal)
            .SetParameters(new GetLastFuturesAtrSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(MapToFuturesAtrSignal!, cancellationToken)
            .ConfigureAwait(false);

    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(string contractId,  TimeFrameType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesDailyAtrSignal)}", MarketDataDbCql.GetLastFuturesDailyAtrSignal)
            .SetParameters(new GetLastFuturesAtrDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(MapToFuturesAtrSignal!);

    public async Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesDailyAtrSignal)}", MarketDataDbCql.GetLastFuturesDailyAtrSignal)
            .SetParameters(new GetLastFuturesAtrDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(MapToFuturesAtrSignal!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the last Futures ADX signal
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesAdxSignalReadModel"/>.</returns>
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAdxSignal)}", MarketDataDbCql.GetLastFuturesAdxSignal)
            .SetParameters(new GetLastFuturesAdxSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(MapToFuturesAdxSignal!);

    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAdxSignal)}", MarketDataDbCql.GetLastFuturesAdxSignal)
            .SetParameters(new GetLastFuturesAdxSignal(contractId, timePeriod.ToStringFast(), periodLength, valueDate))
            .ExecuteSingleAsync(MapToFuturesAdxSignal!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the last Futures ADX daily signal
    /// </summary>
    /// <param name="contractId">The contract ID.</param>
    /// <param name="timePeriod">The value date.</param>
    /// <param name="periodLength"></param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesAdxSignalReadModel"/>.</returns>
    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(string contractId, TimeFrameType timePeriod, int periodLength)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAdxDailySignal)}", MarketDataDbCql.GetLastFuturesAdxDailySignal)
            .SetParameters(new GetLastFuturesAdxDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(MapToFuturesAdxSignal!);

    public async Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(
        string contractId, TimeFrameType timePeriod, int periodLength,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesAdxDailySignal)}", MarketDataDbCql.GetLastFuturesAdxDailySignal)
            .SetParameters(new GetLastFuturesAdxDailySignal(contractId, timePeriod.ToStringFast(), periodLength))
            .ExecuteSingleAsync(MapToFuturesAdxSignal!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the last futures trade signal
    /// </summary>
    /// <param name="contractId">The entity ID containing the contract ID and value date.</param>
    /// <param name="valueDate"> The value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="FuturesTradeSignalV2ReadModel"/>.</returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(string contractId, DateOnly valueDate) 
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignalById)}", MarketDataDbCql.GetLastFuturesTradeSignalById)
            .SetParameters(new GetLastFuturesTradeSignalById(
                contractId,
                valueDate,
                TimeFrameType.FifteenSeconds.ToStringFast()
            ))
            .ExecuteSingleAsync(MapToFuturesTradeSignal!);

    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(
        string contractId, DateOnly valueDate, CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignalById)}", MarketDataDbCql.GetLastFuturesTradeSignalById)
            .SetParameters(new GetLastFuturesTradeSignalById(
                contractId,
                valueDate,
                TimeFrameType.FifteenSeconds.ToStringFast()))
            .ExecuteSingleAsync(MapToFuturesTradeSignal!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// gets the last futures trade signal asynchronously.
    /// </summary>
    /// <returns></returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync()
    {
        var id = await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignal)}", MarketDataDbCql.GetLastFuturesTradeSignal)
            .SetParameters(new GetLastFuturesTradeSignal(
                $"latest:{TimeFrameType.FifteenSeconds.ToStringFast()}"))
            .ExecuteSingleAsync(MapToFuturesTradeSignalId);
        return id is null
            ? null
            : await GetLastFuturesTradeSignalAsync(id.ContractId, id.ValueDate);
    }

    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(
        CancellationToken cancellationToken)
    {
        var id = await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignal)}", MarketDataDbCql.GetLastFuturesTradeSignal)
            .SetParameters(new GetLastFuturesTradeSignal(
                $"latest:{TimeFrameType.FifteenSeconds.ToStringFast()}"))
            .ExecuteSingleAsync(MapToFuturesTradeSignalId, cancellationToken)
            .ConfigureAwait(false);
        return id is null
            ? null
            : await GetLastFuturesTradeSignalAsync(id.ContractId, id.ValueDate, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>
    /// gets all futures trade signals asynchronously.
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalsAsync()
    {
        ICollection<FuturesTradeSignalV2ReadModel> resultSet = [];
        await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTradeSignalAll)}", MarketDataDbCql.GetFuturesTradeSignalAll)
            .ExecuteMapReduceAsync(MapToFuturesTradeSignal, reducer => resultSet = [.. reducer]);
        return resultSet;
    }

    /// <summary>
    /// Gets the last futures trade signal for a given symbol and value date asynchronously.    
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate)
    {
        var db = _dbFactory.MarketDataDb;
        var dbSec = (_dbFactory.SecuritiesDb as ISecuritiesDbReadContext)!;
        List<string> contractIds = [.. (await dbSec.GetFuturesContractsBySymbolAsync(symbol)).Select(e => e.ContractId)];
        return await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignalBySymbol)}", MarketDataDbCql.GetLastFuturesTradeSignalBySymbol)
            .SetParameters(new GetLastFuturesTradeSignalBySymbol(contractIds, valueDate))
            .ExecuteSingleAsync(MapToFuturesTradeSignal);
    }

    /// <summary>
    /// Gets the last rate of return for a given symbol asynchronously.
    /// </summary>
    /// <param name="symbol">The symbol to get the rate of return for.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="RateOfReturnReadModel"/>.</returns>
    public async Task<RateOfReturnReadModel?> GetLastRateOfReturnAsync(string symbol)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastRateOfReturn)}", MarketDataDbCql.GetLastRateOfReturn)
            .SetParameters(new GetLastRateOfReturn(symbol))
            .ExecuteSingleAsync(MapToRateOfReturn);

    public async Task<RateOfReturnReadModel?> GetLastRateOfReturnAsync(
        string symbol,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastRateOfReturn)}", MarketDataDbCql.GetLastRateOfReturn)
            .SetParameters(new GetLastRateOfReturn(symbol))
            .ExecuteSingleAsync(MapToRateOfReturn, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the last VIX futures EOD data for a given VixFuturesEodDataEntityId.
    /// </summary>
    /// <param name="e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="VixFuturesEodDataReadModel"/>.</returns>
    public async Task<VixFuturesEodDataReadModel?> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastVixFuturesEodData)}", MarketDataDbCql.GetLastVixFuturesEodData)
            .SetParameters(new GetLastVixFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToVixFuturesEodData);

	/// <summary>
	/// Gets the VIX futures EOD data for a given VixFuturesEodDataEntityId.
	/// </summary>
	/// <param name="contractId">The entity ID containing the contract ID and value date.</param>
    /// <param name="valueDate"></param>
	public async Task<VixFuturesEodDataReadModel?> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodData)}", MarketDataDbCql.GetVixFuturesEodData)
            .SetParameters(new GetVixFuturesEodData(contractId, valueDate))
            .ExecuteSingleAsync(MapToVixFuturesEodData);

	/// <summary>
	/// Gets the VIX futures EOD data for a given by value date asynchronously.
	/// </summary>
	/// <param name="valueDate"></param>
	/// <returns></returns>
	public async Task<ICollection<VixFuturesEodDataReadModel>> GetVixFuturesEodDataByValueDateAsync(DateOnly valueDate)
    {
        var stamp = await GetProjectionScopeReadStampAsync(
            VixFuturesContractIndexProjection,
            Enumerable.Range(0, VixContractBucketCount).Select(GetVixContractIndexScopeKey));
        if (stamp is null)
            return await ReadLegacyVixFuturesEodDataByValueDateAsync(valueDate);

        var results = await ReadIndexedVixFuturesEodDataByValueDateAsync(valueDate);
        if (await IsProjectionScopeReadStampValidAsync(stamp.Value))
            return results;

        return await ReadLegacyVixFuturesEodDataByValueDateAsync(valueDate);
    }

	/// <summary>
	/// Gets the last updated yield curve rate from the database.
	/// </summary>
	/// <returns>A task representing the asynchronous operation, containing the <see cref="YieldCurveRateReadModel"/>.</returns>
	public async Task<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync()
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastYieldCurveRate)}", MarketDataDbCql.GetLastYieldCurveRate)
            .ExecuteSingleAsync(MapToYieldCurveRate!);

    public async Task<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync(
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastYieldCurveRate)}", MarketDataDbCql.GetLastYieldCurveRate)
            .ExecuteSingleAsync(MapToYieldCurveRate!, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the yield curve rate for a given value date.
    /// </summary>
    /// <param name="valueDate">The value date to retrieve the yield curve rate for.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="YieldCurveRateReadModel"/> if found; otherwise, null.</returns>
    public async Task<YieldCurveRateReadModel?> GetYieldCurveRateAsync(DateOnly valueDate) 
        =>  await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRate)}", MarketDataDbCql.GetYieldCurveRate)
            .SetParameters(new GetYieldCurveRate(valueDate))
            .ExecuteSingleAsync(MapToYieldCurveRate);

    /// <summary>
    /// Gets the collection of YieldCurveRateReadModel for a given start date and end date.
    /// </summary>
    /// <param name="startDate">The start value date.</param>
    /// <param name="endDate">The end value date.</param>
    /// <returns>A task representing the asynchronous operation, containing the collection of YieldCurveRateReadModel.</returns>
    public async Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate)
        => await GetYieldCurveRatesCoreAsync(startDate, endDate, CancellationToken.None);

    public async Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
        => await GetYieldCurveRatesCoreAsync(startDate, endDate, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets a collection of integer values representing the years for yield curve rates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the collection of integer years.</returns>
    public async Task<ICollection<int>> GetYieldCurveRateYearsAsync()
        => await GetYieldCurveRateYearsAsync(CancellationToken.None);

    public async Task<ICollection<int>> GetYieldCurveRateYearsAsync(
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRateYears)}", MarketDataDbCql.GetYieldCurveRateYears)
            .SetParameters(new GetYieldCurveRateYears(YieldCurveLookupId))
            .ExecuteQueryAsync(MapToYearMonth, cancellationToken)
            .ConfigureAwait(false);

    async Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesCoreAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (endDate < startDate)
            return [];
        if (endDate.DayNumber - startDate.DayNumber + 1 > YieldCurveMaximumRangeDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endDate),
                $"Yield-curve ranges may span at most {YieldCurveMaximumRangeDays} days.");
        }

        return await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRates)}", MarketDataDbCql.GetYieldCurveRates)
            .SetParameters(new GetYieldCurveRates(startDate, endDate))
            .ExecuteQueryAsync(MapToYieldCurveRate, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves market holidays for a given currency type.
    /// </summary>
    /// <param name="currencyType">The currency type.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of MarketHolidayReadModel.</returns>
    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysAsync(CurrencyType currencyType)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketHolidays)}", MarketDataDbCql.GetMarketHolidays)
            .SetParameters(new GetMarketHolidays(currencyType: currencyType.ToStringFast()))
            .ExecuteQueryAsync(MapToMarketHoliday);

    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysAsync(
        CurrencyType currencyType,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketHolidays)}", MarketDataDbCql.GetMarketHolidays)
            .SetParameters(new GetMarketHolidays(currencyType: currencyType.ToStringFast()))
            .ExecuteQueryAsync(MapToMarketHoliday, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Asynchronously retrieves the live feed data for a specific trade identified by the provided order and trade
    /// identifiers.
    /// </summary>
    /// <remarks>This method queries the market data database for the specified trade. Ensure that both
    /// orderId and tradeId are valid to avoid unexpected results.</remarks>
    /// <param name="orderId">The unique identifier of the order associated with the trade. Must be a positive integer.</param>
    /// <param name="tradeId">The unique identifier of the trade for which to retrieve live feed data. Must be a positive integer.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a TradeLiveFeedReadModel object with
    /// the live feed data if found; otherwise, null.</returns>
    public async Task<TradeLiveFeedReadModel?> GetTradeLiveFeedAsync(int orderId, int tradeId)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetTradeLiveFeed)}", MarketDataDbCql.GetTradeLiveFeed)
            .SetParameters(new GetTradeLiveFeed(orderId, tradeId))
            .ExecuteSingleAsync(MapToTradeLiveFeed!);

    /// <summary>
    /// Retrieves market holidays for a given currency type within a specified date range.
    /// </summary>
    /// <param name="currencyType">The currency type.</param>
    /// <param name="startDate">The start date of the range.</param>
    /// <param name="endDate">The end date of the range.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of MarketHolidayReadModel.</returns>
    public async Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysByDateRangeAsync(CurrencyType currencyType, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketHolidaysByDateRange)}", MarketDataDbCql.GetMarketHolidaysByDateRange)
            .SetParameters(new GetMarketHolidaysByDateRange(currencyType: currencyType.ToStringFast(), startDate, endDate))
            .ExecuteQueryAsync(MapToMarketHoliday);

    /// <summary>
    /// return number of trading days...
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="marketType"></param>
    /// <param name="currencyType"  ></param>
    /// <returns></returns>
    public async Task<int> GetTradingDaysAsync(
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType = MarketType.Futures,
        CurrencyType currencyType = CurrencyType.USD)
    {
        var key = new TradingDaysKey(
            StartDate: startDate,
            EndDate: endDate,
            MarketType: marketType,
            CurrencyType: currencyType);

        if (_tradingDaysMap.TryGetValue(key, out int value))
            return value;

        // load market holidays by currency type..
        var dbReader = (_dbFactory.MarketDataDb as IMarketDataDbReadContext)!;
        var marketHolidays = await dbReader.GetMarketHolidaysAsync(currencyType)!;

        // build holiday map...
        var holidayMap = new Dictionary<DateOnly, MarketHolidayReadModel>();
        foreach (var e in marketHolidays)
            holidayMap.Add(e.HolidayDate, e);

        // calculate trading days based on total number of days from start date to end date
        // that do not fall on a weekend or holiday...
        var dateIndex = 0;
        var tradingDays = 0;
        while (startDate.AddDays(dateIndex) <= endDate)
        {
            var tradeDate = startDate.AddDays(dateIndex++);
            if (tradeDate.DayOfWeek == DayOfWeek.Saturday
                || tradeDate.DayOfWeek == DayOfWeek.Sunday
                || holidayMap.ContainsKey(tradeDate))
                continue;
            tradingDays++;
        }
        _tradingDaysMap.Add(key, tradingDays);
        return tradingDays;
    }

    /// <summary>
    /// return all normal curve data
    /// </summary>
    public async Task<ICollection<NormalCurveDataReadModel>> GetNormalCurveDataAsync()
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetNormalCurveData)}", MarketDataDbCql.GetNormalCurveData)
            .ExecuteQueryAsync(MapToNormalCurveData!);

	/// <summary>
	/// return futures trade signal id by value date
	/// </summary>
	/// <param name="valueDate"></param>
	/// <returns></returns>
	public async Task<ICollection<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateOnly valueDate)
	     => await _dbFactory.MarketDataDb
                .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTradeSignalIdByValueDate)}", MarketDataDbCql.GetFuturesTradeSignalIdByValueDate)
                .SetParameters(new GetFuturesTradeSignalIdByValueDate(
                    $"date:{TimeFrameType.FifteenSeconds.ToStringFast()}:{valueDate.DayNumber}"))
                .ExecuteQueryAsync(MapToFuturesTradeSignalId);        

    public async Task<ICollection<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTradeSignalIdByValueDate)}", MarketDataDbCql.GetFuturesTradeSignalIdByValueDate)
            .SetParameters(new GetFuturesTradeSignalIdByValueDate(
                $"date:{TimeFrameType.FifteenSeconds.ToStringFast()}:{valueDate.DayNumber}"))
            .ExecuteQueryAsync(MapToFuturesTradeSignalId, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// return normal curve table
    /// </summary>
    /// <returns></returns>
    public async Task<NormalCurveTableReadModel> GetNormalCurveTableAsync()
    {
        _normalCurveTable ??= new NormalCurveTableReadModel( [.. await GetNormalCurveDataAsync()]);
        return _normalCurveTable!;
    }
    /// <summary>
    /// return trading dates...
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="marketType"></param>
    /// <param name="currencyType"></param>
    /// <returns></returns>
    public async Task<DateOnly[]> GetTradingDatesAsync(
       DateOnly startDate,
       DateOnly endDate,
       MarketType marketType = MarketType.Futures,
       CurrencyType currencyType = CurrencyType.USD)
    {

        // load market holidays by currency type..
        var dbReader = (_dbFactory.MarketDataDb as IMarketDataDbReadContext)!;
        var marketHolidays = await dbReader.GetMarketHolidaysAsync(currencyType)!;

        // build holiday map...
        var holidayMap = new Dictionary<DateOnly, MarketHolidayReadModel>();
        foreach (var e in marketHolidays)
            holidayMap.Add(e.HolidayDate, e);

        // calculate trading days based on total number of days from start date to end date
        // that do not fall on a weekend or holiday...
        var dateIndex = 0;
        var tradingDates = new List<DateOnly>();
        while (startDate.AddDays(dateIndex) <= endDate)
        {
            var tradeDate = startDate.AddDays(dateIndex++);
            if (tradeDate.DayOfWeek == DayOfWeek.Saturday
                || tradeDate.DayOfWeek == DayOfWeek.Sunday
                || holidayMap.ContainsKey(tradeDate))
                continue;
            tradingDates.Add(tradeDate);
        }
        return [.. tradingDates];
    }

    public async Task<DateOnly[]> GetTradingDatesAsync(
       DateOnly startDate,
       DateOnly endDate,
       MarketType marketType,
       CurrencyType currencyType,
       CancellationToken cancellationToken)
    {
        var dbReader = (_dbFactory.MarketDataDb as IMarketDataDbReadContext)!;
        var marketHolidays = await dbReader
            .GetMarketHolidaysAsync(currencyType, cancellationToken)
            .ConfigureAwait(false);
        var holidayDates = marketHolidays
            .Select(static holiday => holiday.HolidayDate)
            .ToHashSet();
        var tradingDates = new List<DateOnly>();
        for (var tradeDate = startDate; tradeDate <= endDate; tradeDate = tradeDate.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tradeDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                || holidayDates.Contains(tradeDate))
                continue;
            tradingDates.Add(tradeDate);
        }
        return [.. tradingDates];
    }

    public async Task<int> GetTradingDayCountAsync(
       DateOnly startDate,
       DateOnly endDate,
       MarketType marketType = MarketType.Futures,
       CurrencyType currencyType = CurrencyType.USD)
    {
        var dbReader = (_dbFactory.MarketDataDb as IMarketDataDbReadContext)!;
        var marketHolidays = await dbReader.GetMarketHolidaysAsync(currencyType)!;
        var holidayDates = marketHolidays
            .Select(static holiday => holiday.HolidayDate)
            .ToHashSet();
        var tradingDayCount = 0;
        for (var tradeDate = startDate; tradeDate <= endDate; tradeDate = tradeDate.AddDays(1))
        {
            if (tradeDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                || holidayDates.Contains(tradeDate))
                continue;
            tradingDayCount++;
        }
        return tradingDayCount;
    }

    public async Task<int> GetTradingDayCountAsync(
       DateOnly startDate,
       DateOnly endDate,
       MarketType marketType,
       CurrencyType currencyType,
       CancellationToken cancellationToken)
    {
        var dbReader = (_dbFactory.MarketDataDb as IMarketDataDbReadContext)!;
        var marketHolidays = await dbReader
            .GetMarketHolidaysAsync(currencyType, cancellationToken)
            .ConfigureAwait(false);
        var holidayDates = marketHolidays
            .Select(static holiday => holiday.HolidayDate)
            .ToHashSet();
        var tradingDayCount = 0;
        for (var tradeDate = startDate; tradeDate <= endDate; tradeDate = tradeDate.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tradeDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                || holidayDates.Contains(tradeDate))
                continue;
            tradingDayCount++;
        }
        return tradingDayCount;
    }

    /// <summary>
    /// Checks if yield curve rate data exists for a given value date.
    /// </summary>
    /// <param name="valueDate">The value date to check.</param>
    /// <returns>A task representing the asynchronous operation, containing a boolean indicating whether the data exists.</returns>
    public async Task<bool> GetYieldCurveRateExistsAsync(DateOnly valueDate)
        => (await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRate)}", MarketDataDbCql.GetYieldCurveRate)
            .SetParameters(new GetYieldCurveRate(valueDate))
            .ExecuteSingleAsync(MapToYieldCurveRate!)) is not null;

    public async Task<bool> GetYieldCurveRateExistsAsync(
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => (await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetYieldCurveRate)}", MarketDataDbCql.GetYieldCurveRate)
            .SetParameters(new GetYieldCurveRate(valueDate))
            .ExecuteSingleAsync(MapToYieldCurveRate!, cancellationToken)
            .ConfigureAwait(false)) is not null;

    /// <summary>
    /// Retrieves a list of intrinsic time mode types as string representations.
    /// </summary>
    /// <remarks>The returned list includes the following intrinsic time mode types: <see
    /// cref="IntrinsicTimeModeType.TrendExtremeChanged"/>,  <see cref="IntrinsicTimeModeType.TrendReversalChanged"/>,
    /// and  <see cref="IntrinsicTimeModeType.TrendDirectionChanged"/>.</remarks>
    /// <returns>A list of strings, where each string represents an intrinsic time mode type.</returns>
    static List<string> GetIntrinsicTimeModes()
              => [IntrinsicTimeModeType.TrendExtremeChanged.ToStringFast(),
                IntrinsicTimeModeType.TrendReversalChanged.ToStringFast(),
                IntrinsicTimeModeType.TrendDirectionChanged.ToStringFast()];

    /// <summary>
    /// return stream request id
    /// </summary>
    /// <param name="streamId"></param>
    /// <returns></returns>
    public async Task<int> GetStreamingRequestIdAsync()
        => Convert.ToInt32(await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.StreamingRequest_RequestId));

    /// <summary>
    /// Gets a collection of futures ITI trend direction changed signals for a given entity ID.
    /// </summary>
    /// <param name="e">The entity ID containing the contract ID and value date.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of <see cref="FuturesItiSignalV2ReadModel"/>.</returns>
    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate)
        => await ReadFuturesItiDayModeAsync(
            contractId, valueDate, IntrinsicTimeModeType.TrendDirectionChanged);

    public async Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => await ReadFuturesItiDayModeAsync(
            contractId,
            valueDate,
            IntrinsicTimeModeType.TrendDirectionChanged,
            cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Idempotently rebuilds the non-signal MarketData V2 query projections from canonical tables.
    /// </summary>
    public async Task<MarketDataProjectionBackfillResult> BackfillQueryProjectionsV2Async(
        int batchSize = ProjectionWriteBatchSize,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        if (staleOperationCutoffUtc is { Kind: not DateTimeKind.Utc })
        {
            throw new ArgumentException(
                "The stale operation cutoff must use DateTimeKind.Utc.",
                nameof(staleOperationCutoffUtc));
        }
        if (staleOperationCutoffUtc > DateTime.UtcNow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(staleOperationCutoffUtc),
                staleOperationCutoffUtc,
                "The stale operation cutoff cannot be in the future.");
        }

        var db = _dbFactory.MarketDataDb;
        string[] projectionNames =
        [
            FuturesTickByTimeProjection,
            FuturesEodProjection,
            VixFuturesContractIndexProjection,
            FuturesItiSignalQueryProjection
        ];
        var failedMutationIds = new Dictionary<string, Guid[]>(StringComparer.Ordinal);
        var backfillMutationIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var backfillScopes = projectionNames.ToDictionary(
            static projectionName => projectionName,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var backfillStartedScopes = projectionNames.ToDictionary(
            static projectionName => projectionName,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var backfillAcknowledgedScopes = projectionNames.ToDictionary(
            static projectionName => projectionName,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var backfillGlobalOperationsAcknowledged = new HashSet<string>(StringComparer.Ordinal);
        var targetMutationSubmissionStarted = false;

        try
        {
        // Failed operations are safe to reclaim automatically because their writer
        // reached a terminal catch path. Other old operations require an explicit
        // operator cutoff after every writer has been drained.
        var scopedMutations = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)}", MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)
            .ExecuteQueryAsync(MapToProjectionScopeMutation);
        var recoverableScopedMutations = scopedMutations
            .Where(mutation => projectionNames.Contains(mutation.ProjectionName, StringComparer.Ordinal))
            .Where(mutation => mutation.IsFailed ||
                staleOperationCutoffUtc.HasValue && mutation.StartedOn <= staleOperationCutoffUtc.Value)
            .ToArray();
        if (recoverableScopedMutations.Length > 0)
        {
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.RemoveMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.RemoveMarketDataProjectionScopeOperationV3)
                .SetParameters(recoverableScopedMutations.Select(mutation =>
                    new RemoveMarketDataProjectionScopeOperationV3(
                        mutation.ProjectionName,
                        mutation.ScopeKey,
                        mutation.MutationId)))
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                .SetParameters(recoverableScopedMutations.Select(mutation =>
                    new DeleteMarketDataProjectionScopeMutationV3(
                        mutation.ProjectionName,
                        mutation.ScopeKey,
                        mutation.MutationId)))
                .ExecuteCommandAsync(cancellationToken);
        }

        // Supplying a cutoff is an explicit operator assertion that all older writers
        // have been drained or terminated. Time alone is not treated as a lease.
        if (staleOperationCutoffUtc.HasValue)
        {
            foreach (var projectionName in projectionNames)
            {
                var mutations = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMutations)}", MarketDataDbCql.GetMarketDataProjectionMutations)
                    .SetParameters(new GetMarketDataProjectionMutation(projectionName))
                    .ExecuteQueryAsync(MapToProjectionMutation);
                var staleMutationIds = mutations
                    .Where(mutation => mutation.StartedOn <= staleOperationCutoffUtc.Value)
                    .Select(mutation => mutation.MutationId)
                    .ToHashSet();
                if (staleMutationIds.Count == 0)
                    continue;

                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.RemoveMarketDataProjectionOperations)}", MarketDataDbCql.RemoveMarketDataProjectionOperations)
                    .SetParameters(new RemoveMarketDataProjectionOperations(
                        projectionName,
                        staleMutationIds))
                    .ExecuteCommandAsync(cancellationToken);
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionMutation)}", MarketDataDbCql.DeleteMarketDataProjectionMutation)
                    .SetParameters(staleMutationIds.Select(mutationId =>
                        new DeleteMarketDataProjectionMutation(projectionName, mutationId)))
                    .ExecuteCommandAsync(cancellationToken);
            }
        }

        // Publish the repair markers before touching any projection. Readers keep using
        // canonical tables until every rebuilt projection has reconciled successfully.
        foreach (var projectionName in projectionNames)
        {
            var mutationId = Guid.NewGuid();
            backfillMutationIds.Add(projectionName, mutationId);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMutation)}", MarketDataDbCql.InsertMarketDataProjectionMutation)
                .SetParameters(new InsertMarketDataProjectionMutation(
                    projectionName,
                    mutationId,
                    DateTime.UtcNow))
                .ExecuteCommandAsync(cancellationToken);
            async Task ActivateGlobalProjectionAsync()
                => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.BeginMarketDataProjectionOperation)}", MarketDataDbCql.BeginMarketDataProjectionOperation)
                    .SetParameters(new BeginMarketDataProjectionOperation(
                        projectionName,
                        mutationId,
                        new HashSet<Guid> { mutationId }))
                    .ExecuteCommandAsync(cancellationToken);

            if (ProjectionBackfillGlobalActivationForTestingAsync is { } globalActivation)
                await globalActivation(ActivateGlobalProjectionAsync);
            else
                await ActivateGlobalProjectionAsync();
            backfillGlobalOperationsAcknowledged.Add(projectionName);

            var existingMutations = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMutations)}", MarketDataDbCql.GetMarketDataProjectionMutations)
                .SetParameters(new GetMarketDataProjectionMutation(projectionName))
                .ExecuteQueryAsync(MapToProjectionMutation);
            failedMutationIds.Add(
                projectionName,
                [.. existingMutations
                    .Where(existingMutation =>
                        existingMutation.MutationId != mutationId && existingMutation.IsFailed)
                    .Select(existingMutation => existingMutation.MutationId)]);
        }

        // Claim every guard before discovering data scopes. Ordinary writers touch a
        // deterministic guard as well as their data scope; a post-discovery write then
        // prevents the guard's conditional release without creating a global hot row.
        foreach (var projectionName in projectionNames)
        {
            var guards = GetProjectionGuardScopeKeys();
            backfillScopes[projectionName].UnionWith(guards);
            await BeginBackfillScopesAsync(projectionName, guards);
        }

        // Discover the union of canonical, target, and prior state scopes while the
        // projection-wide gate is closed. Existing targets/states are included so a
        // replay also clears deleted or previously mis-bucketed partitions.
        await foreach (var scope in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickProjectionScopesSource)}", MarketDataDbCql.GetFuturesTickProjectionScopesSource)
            .ExecuteStreamAsync(MapToFuturesTickProjectionScope, cancellationToken))
        {
            backfillScopes[FuturesTickByTimeProjection].Add(scope);
        }
        await foreach (var scope in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickProjectionScopesTarget)}", MarketDataDbCql.GetFuturesTickProjectionScopesTarget)
            .ExecuteStreamAsync(MapToFuturesTickProjectionScope, cancellationToken))
        {
            backfillScopes[FuturesTickByTimeProjection].Add(scope);
        }
        await foreach (var scope in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodProjectionScopesSource)}", MarketDataDbCql.GetFuturesEodProjectionScopesSource)
            .ExecuteStreamAsync(MapToFuturesEodProjectionSourceScope, cancellationToken))
        {
            backfillScopes[FuturesEodProjection].Add(scope);
        }
        await foreach (var scope in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodProjectionScopesTarget)}", MarketDataDbCql.GetFuturesEodProjectionScopesTarget)
            .ExecuteStreamAsync(MapToFuturesEodProjectionTargetScope, cancellationToken))
        {
            backfillScopes[FuturesEodProjection].Add(scope);
        }
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalProjectionScopesSource)}", MarketDataDbCql.GetFuturesItiSignalProjectionScopesSource)
            .ExecuteStreamAsync(MapToFuturesItiProjectionScope, cancellationToken))
        {
            AddFuturesItiScopes(row);
        }
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalProjectionScopesDayTarget)}", MarketDataDbCql.GetFuturesItiSignalProjectionScopesDayTarget)
            .ExecuteStreamAsync(MapToFuturesItiProjectionScope, cancellationToken))
        {
            AddFuturesItiScopes(row);
        }
        await foreach (var state in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionScopeStatesV3All)}", MarketDataDbCql.GetMarketDataProjectionScopeStatesV3All)
            .ExecuteStreamAsync(MapToProjectionScopeState, cancellationToken))
        {
            if (backfillScopes.TryGetValue(state.ProjectionName, out var scopes))
                scopes.Add(state.ScopeKey);
        }
        foreach (var bucket in Enumerable.Range(0, VixContractBucketCount))
            backfillScopes[VixFuturesContractIndexProjection].Add(GetVixContractIndexScopeKey(bucket));

        // Journal and claim every discovered scope with the projection backfill's ID.
        // A concurrent ordinary writer adds a second ID, causing scoped completion to
        // fail without affecting unrelated partitions.
        foreach (var projectionName in projectionNames)
        {
            var scopesToStart = backfillScopes[projectionName]
                .Except(backfillStartedScopes[projectionName], StringComparer.Ordinal)
                .ToArray();
            await BeginBackfillScopesAsync(projectionName, scopesToStart);
        }

        // A clean rebuild is what makes reconciliation detect deleted and stale rows,
        // rather than merely proving that the source and projection have equal counts.
        targetMutationSubmissionStarted = true;
        if (ProjectionBackfillTargetMutationSubmittingForTestingAsync is { } targetMutationSubmitting)
            await targetMutationSubmitting();
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateFuturesTickDataByTime)}", MarketDataDbCql.TruncateFuturesTickDataByTime)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateFuturesEodDataByMonth)}", MarketDataDbCql.TruncateFuturesEodDataByMonth)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateVixFuturesContractIndex)}", MarketDataDbCql.TruncateVixFuturesContractIndex)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateFuturesItiSignalByContractDay)}", MarketDataDbCql.TruncateFuturesItiSignalByContractDay)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateFuturesItiSignalByContractMonth)}", MarketDataDbCql.TruncateFuturesItiSignalByContractMonth)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateFuturesItiSignalByTrendModeMonth)}", MarketDataDbCql.TruncateFuturesItiSignalByTrendModeMonth)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.TruncateMarketDataProjectionMonth)}", MarketDataDbCql.TruncateMarketDataProjectionMonth)
            .ExecuteCommandAsync(cancellationToken);

        var futuresTickSourceIdentityBuilder = new ProjectionIdentityBuilder();
        var tickBatch = new List<InsertFuturesTickDataByTime>(batchSize);
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickDataAll)}", MarketDataDbCql.GetFuturesTickDataAll)
            .ExecuteStreamAsync(MapToFuturesTickData!, cancellationToken))
        {
            futuresTickSourceIdentityBuilder.Add(GetFuturesTickIdentity(row));
            tickBatch.Add(new InsertFuturesTickDataByTime(
                row.ContractId,
                row.ValueDate,
                row.TickTime,
                row.TickId,
                row.Price,
                row.Size));
            if (tickBatch.Count == batchSize)
                await FlushTicksAsync();
        }
        await FlushTicksAsync();

        var futuresEodSourceIdentityBuilder = new ProjectionIdentityBuilder();
        var eodBatch = new List<InsertFuturesEodDataByMonth>(batchSize);
        var eodMonths = new HashSet<int>();
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataAll)}", MarketDataDbCql.GetFuturesEodDataAll)
            .ExecuteStreamAsync(MapToFuturesEodData!, cancellationToken))
        {
            futuresEodSourceIdentityBuilder.Add(GetFuturesEodIdentity(row));
            eodBatch.Add(CreateFuturesEodDataByMonthParameters(row, row.OpenPrice));
            eodMonths.Add(ToYearMonth(row.ValueDate));
            if (eodBatch.Count == batchSize)
                await FlushFuturesEodAsync();
        }
        await FlushFuturesEodAsync();

        var futuresItiSourceIdentityBuilder = new ProjectionIdentityBuilder();
        var itiDayBatch = new List<InsertFuturesItiSignal>(batchSize);
        var itiMonthBatch = new List<InsertFuturesItiSignalByContractMonth>(batchSize);
        var itiMonths = new HashSet<int>();
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsAll)}", MarketDataDbCql.GetFuturesItiSignalsAll)
            .ExecuteStreamAsync(MapToFuturesItiSignal!, cancellationToken))
        {
            futuresItiSourceIdentityBuilder.Add(GetFuturesItiSignalIdentity(row));
            itiDayBatch.Add(CreateFuturesItiSignalParameters(row, row.SequenceId));
            itiMonthBatch.Add(CreateFuturesItiSignalMonthParameters(row, row.SequenceId));
            itiMonths.Add(ToYearMonth(row.ValueDate));
            if (itiDayBatch.Count == batchSize)
                await FlushFuturesItiAsync();
        }
        await FlushFuturesItiAsync();

        long vixFuturesEodRowsSource = 0;
        var vixContracts = new HashSet<string>(StringComparer.Ordinal);
        var vixContractsSourceIdentityBuilder = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesEodDataAll)}", MarketDataDbCql.GetVixFuturesEodDataAll)
            .ExecuteStreamAsync(MapToVixFuturesEodData, cancellationToken))
        {
            vixFuturesEodRowsSource++;
            if (vixContracts.Add(row.ContractId))
                vixContractsSourceIdentityBuilder.Add(GetVixContractIdentity(row.ContractId));
        }

        var vixContractBatch = new List<InsertVixFuturesContractIndex>(batchSize);
        foreach (var contractId in vixContracts.OrderBy(static contractId => contractId, StringComparer.Ordinal))
        {
            vixContractBatch.Add(new InsertVixFuturesContractIndex(
                GetVixContractBucket(contractId),
                contractId));
            if (vixContractBatch.Count == batchSize)
                await FlushVixContractsAsync();
        }
        await FlushVixContractsAsync();

        var futuresTickProjectedIdentityBuilder = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesTickDataByTimeAll)}", MarketDataDbCql.GetFuturesTickDataByTimeAll)
            .ExecuteStreamAsync(MapToFuturesTickData!, cancellationToken))
        {
            futuresTickProjectedIdentityBuilder.Add(GetFuturesTickIdentity(row));
        }

        var futuresEodProjectedIdentityBuilder = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesEodDataByMonthAll)}", MarketDataDbCql.GetFuturesEodDataByMonthAll)
            .ExecuteStreamAsync(MapToFuturesEodData!, cancellationToken))
        {
            futuresEodProjectedIdentityBuilder.Add(GetFuturesEodIdentity(row));
        }

        var futuresItiDayIdentityBuilder = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalByContractDayAll)}", MarketDataDbCql.GetFuturesItiSignalByContractDayAll)
            .ExecuteStreamAsync(MapToFuturesItiSignal!, cancellationToken))
        {
            futuresItiDayIdentityBuilder.Add(GetFuturesItiSignalIdentity(row));
        }

        var futuresItiMonthIdentityBuilder = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalByContractMonthAll)}", MarketDataDbCql.GetFuturesItiSignalByContractMonthAll)
            .ExecuteStreamAsync(MapToFuturesItiSignal!, cancellationToken))
        {
            futuresItiMonthIdentityBuilder.Add(GetFuturesItiSignalIdentity(row));
        }

        var futuresItiTrendModeIdentityBuilder = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalByTrendModeMonthAll)}", MarketDataDbCql.GetFuturesItiSignalByTrendModeMonthAll)
            .ExecuteStreamAsync(MapToFuturesItiSignal!, cancellationToken))
        {
            futuresItiTrendModeIdentityBuilder.Add(GetFuturesItiSignalIdentity(row));
        }

        var vixContractsIndexedIdentityBuilder = new ProjectionIdentityBuilder();
        await foreach (var indexRow in db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetVixFuturesContractIndexAll)}", MarketDataDbCql.GetVixFuturesContractIndexAll)
            .ExecuteStreamAsync(MapToVixFuturesContractIndex, cancellationToken))
        {
            vixContractsIndexedIdentityBuilder.Add(GetVixContractIdentity(
                indexRow.Bucket,
                indexRow.ContractId));
        }

        var futuresTickSourceIdentity = futuresTickSourceIdentityBuilder.Build();
        var futuresTickProjectedIdentity = futuresTickProjectedIdentityBuilder.Build();
        var futuresEodSourceIdentity = futuresEodSourceIdentityBuilder.Build();
        var futuresEodProjectedIdentity = futuresEodProjectedIdentityBuilder.Build();
        var vixContractsSourceIdentity = vixContractsSourceIdentityBuilder.Build();
        var vixContractsIndexedIdentity = vixContractsIndexedIdentityBuilder.Build();
        var futuresItiSourceIdentity = futuresItiSourceIdentityBuilder.Build();
        var futuresItiDayIdentity = futuresItiDayIdentityBuilder.Build();
        var futuresItiMonthIdentity = futuresItiMonthIdentityBuilder.Build();
        var futuresItiTrendModeIdentity = futuresItiTrendModeIdentityBuilder.Build();
        var reconciled =
            futuresTickSourceIdentity == futuresTickProjectedIdentity &&
            futuresEodSourceIdentity == futuresEodProjectedIdentity &&
            vixContractsSourceIdentity == vixContractsIndexedIdentity &&
            futuresItiSourceIdentity == futuresItiDayIdentity &&
            futuresItiSourceIdentity == futuresItiMonthIdentity &&
            futuresItiSourceIdentity == futuresItiTrendModeIdentity;

        if (ProjectionBackfillReconciledForTestingAsync is { } backfillReconciled)
            await backfillReconciled();

        var cutoverPublished = false;
        if (reconciled)
        {
            foreach (var projectionName in projectionNames)
            {
                var failedOperations = failedMutationIds[projectionName];
                if (failedOperations.Length == 0)
                    continue;

                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.RemoveMarketDataProjectionOperations)}", MarketDataDbCql.RemoveMarketDataProjectionOperations)
                    .SetParameters(new RemoveMarketDataProjectionOperations(
                        projectionName,
                        failedOperations.ToHashSet()))
                    .ExecuteCommandAsync(cancellationToken);
            }

            var futuresTickScopesCompleted = await CompleteProjectionScopesAsync(
                FuturesTickByTimeProjection,
                guardScopes: false);
            var futuresEodScopesCompleted = await CompleteProjectionScopesAsync(
                FuturesEodProjection,
                guardScopes: false);
            var vixContractIndexScopesCompleted = await CompleteProjectionScopesAsync(
                VixFuturesContractIndexProjection,
                guardScopes: false);
            var futuresItiScopesCompleted = await CompleteProjectionScopesAsync(
                FuturesItiSignalQueryProjection,
                guardScopes: false);
            var allDataScopesCompleted = futuresTickScopesCompleted &&
                futuresEodScopesCompleted &&
                vixContractIndexScopesCompleted &&
                futuresItiScopesCompleted;

            var futuresTickCompleted = false;
            var futuresEodCompleted = false;
            var vixContractIndexCompleted = false;
            var futuresItiCompleted = false;
            if (allDataScopesCompleted)
            {
                futuresTickCompleted = await CompleteProjectionAsync(
                    FuturesTickByTimeProjection,
                    futuresTickSourceIdentity,
                    futuresTickProjectedIdentity);
                futuresEodCompleted = await CompleteProjectionAsync(
                    FuturesEodProjection,
                    futuresEodSourceIdentity,
                    futuresEodProjectedIdentity);
                vixContractIndexCompleted = await CompleteProjectionAsync(
                    VixFuturesContractIndexProjection,
                    vixContractsSourceIdentity,
                    vixContractsIndexedIdentity);
                futuresItiCompleted = await CompleteProjectionAsync(
                    FuturesItiSignalQueryProjection,
                    futuresItiSourceIdentity,
                    futuresItiDayIdentity);
            }

            var allGlobalStatesCompleted = allDataScopesCompleted &&
                futuresTickCompleted &&
                futuresEodCompleted &&
                vixContractIndexCompleted &&
                futuresItiCompleted;
            var allGuardScopesCompleted = false;
            if (allGlobalStatesCompleted)
            {
                // Global mutation markers remain present while guards are released, so
                // readers cannot observe global readiness between these phases.
                var futuresTickGuardsCompleted = await CompleteProjectionScopesAsync(
                    FuturesTickByTimeProjection,
                    guardScopes: true);
                var futuresEodGuardsCompleted = await CompleteProjectionScopesAsync(
                    FuturesEodProjection,
                    guardScopes: true);
                var vixContractIndexGuardsCompleted = await CompleteProjectionScopesAsync(
                    VixFuturesContractIndexProjection,
                    guardScopes: true);
                var futuresItiGuardsCompleted = await CompleteProjectionScopesAsync(
                    FuturesItiSignalQueryProjection,
                    guardScopes: true);
                allGuardScopesCompleted = futuresTickGuardsCompleted &&
                    futuresEodGuardsCompleted &&
                    vixContractIndexGuardsCompleted &&
                    futuresItiGuardsCompleted;
            }

            cutoverPublished = allGlobalStatesCompleted && allGuardScopesCompleted;
            if (cutoverPublished)
            {
                // Only explicitly failed operations plus this repair's own marker are
                // removed. Any live/unclassified operation keeps reads on canonical data.
                foreach (var projectionName in projectionNames)
                {
                    var backfillMutationId = backfillMutationIds[projectionName];
                    var scopes = backfillScopes[projectionName];
                    if (scopes.Count > 0)
                    {
                        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                            .SetParameters(scopes.Select(scope =>
                                new DeleteMarketDataProjectionScopeMutationV3(
                                    projectionName,
                                    scope,
                                    backfillMutationId)))
                            .ExecuteCommandAsync(cancellationToken);
                    }
                    foreach (var failedMutationId in failedMutationIds[projectionName])
                        await DeleteProjectionMutationAsync(projectionName, failedMutationId);
                    await DeleteProjectionMutationAsync(projectionName, backfillMutationId);
                }
            }
            else
            {
                if (futuresTickCompleted)
                    await CloseCompletedProjectionAsync(FuturesTickByTimeProjection);
                if (futuresEodCompleted)
                    await CloseCompletedProjectionAsync(FuturesEodProjection);
                if (vixContractIndexCompleted)
                    await CloseCompletedProjectionAsync(VixFuturesContractIndexProjection);
                if (futuresItiCompleted)
                    await CloseCompletedProjectionAsync(FuturesItiSignalQueryProjection);
                await FailBackfillMutationsAsync();
            }
        }
        else
            await FailBackfillMutationsAsync();

        var cutoverCompleted = cutoverPublished &&
            (await GetQueryProjectionReadinessAsync(cancellationToken)).IsReady;
        return new MarketDataProjectionBackfillResult(
            futuresTickSourceIdentity.Count,
            futuresTickProjectedIdentity.Count,
            futuresTickSourceIdentity.Fingerprint,
            futuresTickProjectedIdentity.Fingerprint,
            futuresEodSourceIdentity.Count,
            futuresEodProjectedIdentity.Count,
            futuresEodSourceIdentity.Fingerprint,
            futuresEodProjectedIdentity.Fingerprint,
            vixFuturesEodRowsSource,
            vixContractsSourceIdentity.Count,
            vixContractsIndexedIdentity.Count,
            vixContractsSourceIdentity.Fingerprint,
            vixContractsIndexedIdentity.Fingerprint,
            futuresItiSourceIdentity.Count,
            futuresItiDayIdentity.Count,
            futuresItiMonthIdentity.Count,
            futuresItiTrendModeIdentity.Count,
            futuresItiSourceIdentity.Fingerprint,
            futuresItiDayIdentity.Fingerprint,
            futuresItiMonthIdentity.Fingerprint,
            futuresItiTrendModeIdentity.Fingerprint,
            cutoverCompleted);

        async Task FlushTicksAsync()
        {
            if (tickBatch.Count == 0)
                return;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesTickDataByTime)}", MarketDataDbCql.InsertFuturesTickDataByTime)
                .SetParameters(tickBatch)
                .ExecuteCommandAsync(cancellationToken);
            tickBatch.Clear();
        }

        async Task FlushFuturesEodAsync()
        {
            if (eodBatch.Count == 0)
                return;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesEodDataByMonth)}", MarketDataDbCql.InsertFuturesEodDataByMonth)
                .SetParameters(eodBatch)
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                .SetParameters(eodMonths.Select(yearMonth =>
                    new InsertMarketDataProjectionMonth(FuturesEodProjection, yearMonth)))
                .ExecuteCommandAsync(cancellationToken);
            eodBatch.Clear();
            eodMonths.Clear();
        }

        async Task FlushFuturesItiAsync()
        {
            if (itiDayBatch.Count == 0)
                return;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByContractDay)}", MarketDataDbCql.InsertFuturesItiSignalByContractDay)
                .SetParameters(itiDayBatch)
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByContractMonth)}", MarketDataDbCql.InsertFuturesItiSignalByContractMonth)
                .SetParameters(itiMonthBatch)
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesItiSignalByTrendModeMonth)}", MarketDataDbCql.InsertFuturesItiSignalByTrendModeMonth)
                .SetParameters(itiMonthBatch)
                .ExecuteCommandAsync(cancellationToken);
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionMonth)}", MarketDataDbCql.InsertMarketDataProjectionMonth)
                .SetParameters(itiMonths.Select(yearMonth =>
                    new InsertMarketDataProjectionMonth(FuturesItiSignalQueryProjection, yearMonth)))
                .ExecuteCommandAsync(cancellationToken);
            itiDayBatch.Clear();
            itiMonthBatch.Clear();
            itiMonths.Clear();
        }

        async Task FlushVixContractsAsync()
        {
            if (vixContractBatch.Count == 0)
                return;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertVixFuturesContractIndex)}", MarketDataDbCql.InsertVixFuturesContractIndex)
                .SetParameters(vixContractBatch)
                .ExecuteCommandAsync(cancellationToken);
            vixContractBatch.Clear();
        }

        void AddFuturesItiScopes(FuturesItiProjectionScopeData row)
        {
            var yearMonth = ToYearMonth(row.ValueDate);
            var scopes = backfillScopes[FuturesItiSignalQueryProjection];
            scopes.Add(GetFuturesItiDayScopeKey(row.ContractId, row.ValueDate));
            scopes.Add(GetFuturesItiMonthScopeKey(row.ContractId, yearMonth));
            scopes.Add(GetFuturesItiTimelineScopeKey(
                row.ContractId,
                row.IntrinsicTimeTrend,
                row.IntrinsicTimeMode,
                yearMonth));
        }

        async Task BeginBackfillScopesAsync(
            string projectionName,
            IEnumerable<string> scopeKeys)
        {
            var scopes = scopeKeys
                .Distinct(StringComparer.Ordinal)
                .Except(backfillStartedScopes[projectionName], StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (scopes.Length == 0)
                return;

            // Record these before issuing commands: either command can fail after a
            // partial server-side apply, and the catch path must conservatively end and
            // classify every possibly started scope.
            backfillStartedScopes[projectionName].UnionWith(scopes);
            var mutationId = backfillMutationIds[projectionName];
            var startedOn = DateTime.UtcNow;
            await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
                .SetParameters(scopes.Select(scope => new InsertMarketDataProjectionScopeMutationV3(
                    projectionName,
                    scope,
                    mutationId,
                    startedOn)))
                .ExecuteCommandAsync(cancellationToken);
            var activeOperations = new HashSet<Guid> { mutationId };
            async Task ActivateBackfillScopesAsync()
                => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)
                    .SetParameters(scopes.Select(scope => new BeginMarketDataProjectionScopeOperationV3(
                        projectionName,
                        scope,
                        mutationId,
                        activeOperations)))
                    .ExecuteCommandAsync(cancellationToken);

            if (ProjectionBackfillScopeActivationForTestingAsync is { } scopeActivation)
                await scopeActivation(ActivateBackfillScopesAsync);
            else
                await ActivateBackfillScopesAsync();
            backfillAcknowledgedScopes[projectionName].UnionWith(scopes);
        }

        async Task<bool> CompleteProjectionScopesAsync(
            string projectionName,
            bool guardScopes)
        {
            var mutationId = backfillMutationIds[projectionName];
            var activeOperations = new HashSet<Guid> { mutationId };
            var allCompleted = true;
            var scopes = backfillScopes[projectionName]
                .Where(scope => IsProjectionGuardScopeKey(scope) == guardScopes);
            foreach (var scopeBatch in scopes.Chunk(ProjectionReadConcurrency))
            {
                var completions = scopeBatch.Select(async scope =>
                    await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.CompleteMarketDataProjectionScopeOperationV3)}", MarketDataDbCql.CompleteMarketDataProjectionScopeOperationV3)
                        .SetParameters(new CompleteMarketDataProjectionScopeOperationV3(
                            projectionName,
                            scope,
                            mutationId,
                            activeOperations,
                            DateTime.UtcNow,
                            activeOperations))
                        .ExecuteSingleAsync(MapToBoolean) == true).ToArray();
                if ((await Task.WhenAll(completions)).Any(static completed => !completed))
                    allCompleted = false;
            }
            return allCompleted;
        }

        async Task<bool> CompleteProjectionAsync(
            string projectionName,
            ProjectionIdentity sourceIdentity,
            ProjectionIdentity projectedIdentity)
        {
            var activeOperations = new HashSet<Guid> { backfillMutationIds[projectionName] };
            return await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.CompleteMarketDataProjectionState)}", MarketDataDbCql.CompleteMarketDataProjectionState)
                .SetParameters(new CompleteMarketDataProjectionState(
                    projectionName,
                    backfillMutationIds[projectionName],
                    activeOperations,
                    sourceIdentity.Count,
                    projectedIdentity.Count,
                    sourceIdentity.Fingerprint,
                    projectedIdentity.Fingerprint,
                    DateTime.UtcNow,
                    activeOperations))
                .ExecuteSingleAsync(MapToBoolean) == true;
        }

        async Task CloseCompletedProjectionAsync(string projectionName)
        {
            var activeOperations = new HashSet<Guid> { backfillMutationIds[projectionName] };
            await EndProjectionOperationAsync(
                db,
                projectionName,
                activeOperations,
                cancellationToken);
        }

        async Task FailBackfillMutationsAsync()
        {
            foreach (var projectionName in projectionNames)
            {
                var mutationId = backfillMutationIds[projectionName];
                var scopes = backfillStartedScopes[projectionName];
                if (scopes.Count > 0)
                {
                    await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)
                        .SetParameters(scopes.Select(scope =>
                            new FailMarketDataProjectionScopeMutationV3(
                                projectionName,
                                scope,
                                mutationId,
                                DateTime.UnixEpoch)))
                        .ExecuteCommandAsync();
                }
                await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionMutation)}", MarketDataDbCql.FailMarketDataProjectionMutation)
                    .SetParameters(new FailMarketDataProjectionMutation(
                        projectionName,
                        mutationId,
                        DateTime.UnixEpoch))
                    .ExecuteCommandAsync();
            }
        }

        async Task DeleteProjectionMutationAsync(string projectionName, Guid mutationId)
            => await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.DeleteMarketDataProjectionMutation)}", MarketDataDbCql.DeleteMarketDataProjectionMutation)
                .SetParameters(new DeleteMarketDataProjectionMutation(projectionName, mutationId))
                .ExecuteCommandAsync(cancellationToken);
        }
        catch
        {
            if (targetMutationSubmissionStarted)
            {
                // A TRUNCATE or projection upsert may still be applied after a timeout
                // or cancellation. Preserve every original nonfailed journal and active
                // guard so no later repair can cut over until an operator has drained
                // writers and supplied an explicit stale-operation cutoff.
                throw;
            }

            foreach (var (projectionName, mutationId) in backfillMutationIds)
            {
                var acknowledgedScopes = backfillAcknowledgedScopes[projectionName];
                try
                {
                    if (acknowledgedScopes.Count > 0)
                    {
                        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)}", MarketDataDbCql.FailMarketDataProjectionScopeMutationV3)
                            .SetParameters(acknowledgedScopes.Select(scope =>
                                new FailMarketDataProjectionScopeMutationV3(
                                    projectionName,
                                    scope,
                                    mutationId,
                                    DateTime.UnixEpoch)))
                            .ExecuteCommandAsync();
                    }
                }
                catch
                {
                    // Leave original markers for operator-cutoff recovery.
                }

                if (backfillGlobalOperationsAcknowledged.Contains(projectionName))
                {
                    try
                    {
                        await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.FailMarketDataProjectionMutation)}", MarketDataDbCql.FailMarketDataProjectionMutation)
                            .SetParameters(new FailMarketDataProjectionMutation(
                                projectionName,
                                mutationId,
                                DateTime.UnixEpoch))
                            .ExecuteCommandAsync();
                    }
                    catch
                    {
                        // Leave an unclassified marker in place; a repair must not clear it.
                    }
                }

                // An unacknowledged Begin may still be applied later. Its original
                // nonfailed journal and possible active ID are intentionally untouched;
                // only an explicit cutoff after writers are drained may reclaim them.
            }

            throw;
        }
    }

}

/// <summary>
/// Represents a unique key for identifying a range of trading days within a specific market and currency context.
/// </summary>
/// <remarks>This record is used to encapsulate the start and end dates of a trading period, along with the
/// associated market type and currency type. It is primarily intended for scenarios where trading day ranges need to be
/// uniquely identified or compared.</remarks>
/// <param name="StartDate"></param>
/// <param name="EndDate"></param>
/// <param name="MarketType"></param>
/// <param name="CurrencyType"></param>
record TradingDaysKey(
            DateOnly StartDate,
            DateOnly EndDate,
            MarketType MarketType,
            CurrencyType CurrencyType)
{
    public override string ToString() => $"{StartDate:yyyy-MM-dd}|{EndDate:yyyy-MM-dd}|{MarketType}|{CurrencyType}";
}
