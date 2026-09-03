using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

/// <summary>PostgreSQL authority for current Databento contracts and ordered watchdog history.</summary>
public sealed class MarketDataServiceDbContext(
    IDbConnectionSettings settings,
    IDbContextFactory factory,
    ISequenceIdDbContext sequenceIds,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<MarketDataServiceDbContext>(settings[MarketDataServiceDbConnection], logger),
      IMarketDataServiceStore
{
    public const string MarketDataServiceDbConnection = "MarketDataServiceDbConnection";
    public override MarketDataServiceDbContext Database => this;

    public Task<FuturesRolloverContractAssignment?> GetAssignmentAsync(DatabentoContractRole role,
        CancellationToken cancellationToken = default) => Database
        .Use("MarketDataService.GetAssignment", MarketDataServiceDbSql.GetAssignment)
        .SetParameters(new RoleParameter(role)).ExecuteSingleAsync<FuturesRolloverContractAssignment?>(MapAssignment, cancellationToken);

    public async Task<IReadOnlyList<FuturesRolloverContractAssignment>> ListAssignmentsAsync(
        CancellationToken cancellationToken = default) => [.. await Database
        .Use("MarketDataService.ListAssignments", MarketDataServiceDbSql.ListAssignments)
        .ExecuteQueryAsync(MapAssignment, cancellationToken).ConfigureAwait(false)];

    public async Task<FuturesRolloverContractAssignment> UpsertAssignmentAsync(
        FuturesRolloverContractAssignment assignment, long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        await ValidateSourceAsync(assignment, cancellationToken).ConfigureAwait(false);
        var sql = expectedRowVersion == 0 ? MarketDataServiceDbSql.InsertAssignment : MarketDataServiceDbSql.UpdateAssignment;
        var saved = await Database.Use("MarketDataService.UpsertAssignment", sql)
            .SetParameters(new AssignmentParameter(assignment, expectedRowVersion))
            .ExecuteSingleAsync<FuturesRolloverContractAssignment?>(MapAssignment, cancellationToken).ConfigureAwait(false);
        return saved ?? throw new InvalidOperationException("The assignment was concurrently changed or already exists.");
    }

    public async Task DeleteAssignmentAsync(DatabentoContractRole role, long expectedRowVersion, string deletedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedBy);
        var count = await Database.Use("MarketDataService.DeleteAssignment", MarketDataServiceDbSql.DeleteAssignment)
            .SetParameters(new DeleteParameter(role, expectedRowVersion)).ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        if (count.Sum() != 1) throw new InvalidOperationException("The assignment was concurrently changed or does not exist.");
    }

    public async Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReplaceVxAssignmentsAsync(
        FuturesRolloverContractAssignment front, FuturesRolloverContractAssignment second,
        long expectedFrontVersion, long expectedSecondVersion, CancellationToken cancellationToken = default)
    {
        if (front.ContractRole != DatabentoContractRole.VxFrontMonth
            || second.ContractRole != DatabentoContractRole.VxSecondMonth
            || second.LastTradeDate <= front.LastTradeDate || front.ContractId == second.ContractId)
            throw new ArgumentException("VX assignments must be distinct, ordered front and second contracts.");
        await ValidateSourceAsync(front, cancellationToken).ConfigureAwait(false);
        await ValidateSourceAsync(second, cancellationToken).ConfigureAwait(false);
        var transaction = BeginTransaction();
        try
        {
            var changed = await Database.Use("MarketDataService.ReplaceVxAssignments", MarketDataServiceDbSql.UpsertVxPair)
                .SetParameters(new VxPairParameter(front, expectedFrontVersion, second, expectedSecondVersion))
                .ExecuteScalarAsync(static row => row.GetInt(0), cancellationToken).ConfigureAwait(false);
            if (changed != 2)
                throw new InvalidOperationException("A VX assignment was concurrently changed; neither role was committed.");
            transaction?.Commit();
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }
        return [
            (await GetAssignmentAsync(DatabentoContractRole.VxFrontMonth, cancellationToken).ConfigureAwait(false))!,
            (await GetAssignmentAsync(DatabentoContractRole.VxSecondMonth, cancellationToken).ConfigureAwait(false))!
        ];
    }

    public async Task<DatabentoWatchdogObservation> AppendObservationAsync(DatabentoWatchdogObservation observation,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetObservationByIdentityAsync(observation.ObservationId, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return existing;
        var id = await sequenceIds.GetNextSequenceIdAsync(
            SequenceName.MarketDataService_WatchdogStatusLogId, cancellationToken).ConfigureAwait(false);
        var saved = observation with { WatchdogStatusLogId = id, RowVersion = 1 };
        _ = await Database.Use("MarketDataService.InsertObservation", MarketDataServiceDbSql.InsertObservation)
            .SetParameters(new ObservationParameter(saved)).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        return await GetObservationByIdentityAsync(observation.ObservationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The watchdog observation was not persisted.");
    }

    public Task<DatabentoWatchdogObservation?> GetObservationAsync(long id,
        CancellationToken cancellationToken = default) => Database
        .Use("MarketDataService.GetObservation", MarketDataServiceDbSql.GetObservation)
        .SetParameters(new IdParameter(id)).ExecuteSingleAsync<DatabentoWatchdogObservation?>(MapObservation, cancellationToken);

    public async Task<DatabentoWatchdogObservation> UpdateObservationAsync(
        DatabentoWatchdogObservation observation, long expectedRowVersion, string changedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);
        var saved = await Database.Use("MarketDataService.UpdateObservation", MarketDataServiceDbSql.UpdateObservation)
            .SetParameters(new ObservationUpdateParameter(observation, expectedRowVersion, changedBy))
            .ExecuteSingleAsync<DatabentoWatchdogObservation?>(MapObservation, cancellationToken).ConfigureAwait(false);
        return saved ?? throw new InvalidOperationException("The watchdog observation was concurrently changed or does not exist.");
    }

    public async Task DeleteObservationAsync(long id, long expectedRowVersion, string deletedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedBy);
        var count = await Database.Use("MarketDataService.DeleteObservation", MarketDataServiceDbSql.DeleteObservation)
            .SetParameters(new ObservationDeleteParameter(id, expectedRowVersion)).ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        if (count.Sum() != 1)
            throw new InvalidOperationException("The watchdog observation was concurrently changed or does not exist.");
    }

    public async Task<IReadOnlyList<DatabentoWatchdogObservation>> ListObservationsAsync(
        DateOnly? valueDate = null, DatabentoMajorStatus? status = null, int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return [.. await Database.Use("MarketDataService.ListObservations", MarketDataServiceDbSql.ListObservations)
            .SetParameters(new ObservationListParameter(valueDate, status, pageSize))
            .ExecuteQueryAsync(MapObservation, cancellationToken).ConfigureAwait(false)];
    }

    Task<DatabentoWatchdogObservation?> GetObservationByIdentityAsync(Guid id, CancellationToken cancellationToken)
        => Database.Use("MarketDataService.GetObservationByIdentity", MarketDataServiceDbSql.GetObservationByIdentity)
            .SetParameters(new IdentityParameter(id)).ExecuteSingleAsync<DatabentoWatchdogObservation?>(MapObservation, cancellationToken);

    async Task ValidateSourceAsync(FuturesRolloverContractAssignment assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        var source = await factory.SecuritiesDb.GetFuturesContractAsync(assignment.ContractId, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
                $"Source futures contract '{assignment.ContractId}' does not exist.");
        var expectedRoot = assignment.ContractRole == DatabentoContractRole.EsQuarterly ? "ES" : "VX";
        if (!string.Equals(source.Symbol, expectedRoot, StringComparison.Ordinal)
            || !string.Equals(assignment.RootSymbol, expectedRoot, StringComparison.Ordinal))
            throw new InvalidOperationException($"Role {assignment.ContractRole} requires source root {expectedRoot}.");
        var hash = DatabentoContractAuthority.Hash(source);
        if (!string.Equals(assignment.SourceContractHash, hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The copied contract fingerprint does not match the source catalog.");
    }

    static FuturesRolloverContractAssignment MapAssignment(IObjectDataRecord row) => new()
    {
        ContractRole = Enum.Parse<DatabentoContractRole>(row.GetString(0)), RootSymbol = row.GetString(1),
        ContractId = row.GetString(2), Description = row.GetString(3), LocalSymbol = row.GetString(4),
        SecurityType = row.GetString(5), Currency = row.GetString(6), Exchange = row.GetString(7),
        Multiplier = row.GetString(8), LastTradeDate = row.GetDateOnly(9), NextRolloverDate = row.GetDateOnly(10),
        SourceContractHash = row.GetString(11), RowVersion = row.GetLong(12),
        CreatedOnUtc = Utc(row.GetDateTime(13)), CreatedBy = row.GetString(14),
        UpdatedOnUtc = Utc(row.GetDateTime(15)), UpdatedBy = row.GetString(16)
    };

    static DatabentoWatchdogObservation MapObservation(IObjectDataRecord row) => new()
    {
        WatchdogStatusLogId = row.GetLong(0), ObservationId = row.GetGuid(1), CorrelationId = row.GetGuid(2),
        ValueDate = row.GetDateOnly(3), ObservedOnUtc = Utc(row.GetDateTime(4)),
        OperationReason = Enum.Parse<DatabentoOperationReason>(row.GetString(5)),
        MajorStatus = Enum.Parse<DatabentoMajorStatus>(row.GetString(6)),
        DisplayHealth = Enum.Parse<DatabentoDisplayHealth>(row.GetString(7)), CoreContractsReady = row.GetBool(8),
        RecoveryAttempt = row.GetInt(9), NativeBackend = row.GetString(10), NativeAbiVersion = row.GetInt(11),
        NativeGeneration = row.GetGuid(12), FailureStage = row.GetString(13), FailureDetail = row.GetString(14),
        FeedStatusDetails = JsonSerializer.Deserialize<DatabentoFeedWatchdogStatus[]>(row.GetString(15)) ?? [],
        RowVersion = row.GetLong(16)
    };

    static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    readonly record struct RoleParameter(DatabentoContractRole Role) : IBindValue { public object Bind() => Values(Text(Role.ToString())); }
    readonly record struct DeleteParameter(DatabentoContractRole Role, long Version) : IBindValue { public object Bind() => Values(Text(Role.ToString()), Bigint(Version)); }
    readonly record struct IdParameter(long Id) : IBindValue { public object Bind() => Values(Bigint(Id)); }
    readonly record struct IdentityParameter(Guid Id) : IBindValue { public object Bind() => Values(Uuid(Id)); }
    readonly record struct ObservationDeleteParameter(long Id, long Version) : IBindValue
    { public object Bind() => Values(Bigint(Id), Bigint(Version)); }
    readonly record struct AssignmentParameter(FuturesRolloverContractAssignment A, long Expected) : IBindValue
    {
        public object Bind() => BindAssignment(A, Expected);
    }
    readonly record struct VxPairParameter(FuturesRolloverContractAssignment Front, long FrontExpected,
        FuturesRolloverContractAssignment Second, long SecondExpected) : IBindValue
    {
        public object Bind() => Values([.. BindAssignment(Front, FrontExpected), .. BindAssignment(Second, SecondExpected)]);
    }
    readonly record struct ObservationParameter(DatabentoWatchdogObservation O) : IBindValue
    {
        public object Bind() => Values(Bigint(O.WatchdogStatusLogId), Uuid(O.ObservationId), Uuid(O.CorrelationId),
            Date(O.ValueDate), TimestampTz(O.ObservedOnUtc), Text(O.OperationReason.ToString()), Text(O.MajorStatus.ToString()),
            Text(O.DisplayHealth.ToString()), Boolean(O.CoreContractsReady), Integer(O.RecoveryAttempt), Text(O.NativeBackend),
            Integer(O.NativeAbiVersion), Uuid(O.NativeGeneration), Text(O.FailureStage), Text(O.FailureDetail),
            Text(JsonSerializer.Serialize(O.FeedStatusDetails)), Text("DatabentoMarketDataWatchdogService"));
    }
    readonly record struct ObservationListParameter(DateOnly? DateValue, DatabentoMajorStatus? Status, int PageSize) : IBindValue
    { public object Bind() => Values(Date(DateValue), Text(Status?.ToString()), Integer(PageSize)); }
    readonly record struct ObservationUpdateParameter(DatabentoWatchdogObservation O, long Expected, string ChangedBy) : IBindValue
    {
        public object Bind() => Values(Bigint(O.WatchdogStatusLogId), Uuid(O.ObservationId), Uuid(O.CorrelationId),
            Date(O.ValueDate), TimestampTz(O.ObservedOnUtc), Text(O.OperationReason.ToString()), Text(O.MajorStatus.ToString()),
            Text(O.DisplayHealth.ToString()), Boolean(O.CoreContractsReady), Integer(O.RecoveryAttempt), Text(O.NativeBackend),
            Integer(O.NativeAbiVersion), Uuid(O.NativeGeneration), Text(O.FailureStage), Text(O.FailureDetail),
            Text(JsonSerializer.Serialize(O.FeedStatusDetails)), Bigint(Expected), Text(ChangedBy));
    }

    static Npgsql.NpgsqlParameter[] BindAssignment(FuturesRolloverContractAssignment a, long expected) => Values(
        Text(a.ContractRole.ToString()), Text(a.RootSymbol), Text(a.ContractId), Text(a.Description), Text(a.LocalSymbol),
        Text(a.SecurityType), Text(a.Currency), Text(a.Exchange), Text(a.Multiplier), Date(a.LastTradeDate),
        Date(a.NextRolloverDate), Text(a.SourceContractHash), TimestampTz(a.CreatedOnUtc), Text(a.CreatedBy),
        TimestampTz(a.UpdatedOnUtc), Text(a.UpdatedBy), Bigint(expected));
}
