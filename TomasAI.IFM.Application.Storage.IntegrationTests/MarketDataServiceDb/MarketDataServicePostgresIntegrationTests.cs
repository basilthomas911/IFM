using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataServiceDb;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MarketDataServicePostgresCollection : ICollectionFixture<MarketDataServicePostgresFixture>
{
    public const string Name = "Market Data Service PostgreSQL integration";
}

public sealed class MarketDataServicePostgresFixture : IAsyncLifetime
{
    const string ConnectionVariable = "IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION";
    public MarketDataServiceDbContext Store { get; private set; } = null!;
    public IReadOnlyDictionary<string, FuturesContractV3ReadModel> Sources { get; private set; } = null!;
    CleanupRepository _cleanup = null!;
    string? _previousEnvironment;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)
            ?? throw new InvalidOperationException($"Set {ConnectionVariable} to the dedicated PostgreSQL integration-test database.");
        _previousEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings().Add(
            MarketDataServiceDbContext.MarketDataServiceDbConnection,
            connectionString,
            "System.Data.Postgres");
        await new MarketDataServiceSchemaDb(settings, logger).CreateAllAsync();
        _cleanup = new CleanupRepository(settings[MarketDataServiceDbContext.MarketDataServiceDbConnection], logger);
        await CleanupAsync();

        Sources = new[]
        {
            Contract("ES20261218", "ES", new(2026, 12, 18)),
            Contract("VX20260916", "VX", new(2026, 9, 16)),
            Contract("VX20261021", "VX", new(2026, 10, 21)),
            Contract("VX20261118", "VX", new(2026, 11, 18))
        }.ToDictionary(value => value.ContractId, StringComparer.Ordinal);
        var securities = Substitute.For<ISecuritiesDbContext>();
        securities.GetFuturesContractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Sources.GetValueOrDefault(call.Arg<string>())));
        var factory = Substitute.For<IDbContextFactory>();
        factory.SecuritiesDb.Returns(securities);
        Store = new MarketDataServiceDbContext(settings, factory, new TestSequenceIds(), logger);
    }

    public async Task DisposeAsync()
    {
        try { await CleanupAsync(); }
        finally { Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _previousEnvironment); }
    }

    async Task CleanupAsync()
    {
        if (_cleanup is null) return;
        _ = await _cleanup.Use("MarketDataServicePostgresFixture.Cleanup",
            "TRUNCATE TABLE market_data_service.dataset_incident_transition, market_data_service.dataset_incident_current, market_data_service.watchdog_status_log, market_data_service.futures_rollover_contract_assignment;")
            .ExecuteCommandAsync();
    }

    static FuturesContractV3ReadModel Contract(string id, string root, DateOnly maturity) => new(
        id, id, root, id, "FUT", "USD", "CME", "1000", maturity, true, true);

    sealed class TestSequenceIds : ISequenceIdDbContext
    {
        long _value;
        public Task<long> GetNextSequenceIdAsync(SequenceName sequenceName, CancellationToken cancellationToken = default)
            => Task.FromResult(Interlocked.Increment(ref _value));
        public Task<long> GetCurrentSequenceIdAsync(SequenceName sequenceName, CancellationToken cancellationToken = default)
            => Task.FromResult(Volatile.Read(ref _value));
        public Task<long> GetSequenceAllocationSizeAsync(SequenceName sequenceName, CancellationToken cancellationToken = default)
            => Task.FromResult(1L);
    }

    sealed class CleanupRepository(IDbConnectionSetting connection, ILogger<DbProvider> logger)
        : ObjectDataRepository<CleanupRepository>(connection, logger)
    {
        public override IObjectRepository Database => this;
    }
}

[Collection(MarketDataServicePostgresCollection.Name)]
public sealed class MarketDataServicePostgresIntegrationTests(MarketDataServicePostgresFixture fixture)
{
    [Fact]
    public async Task Assignment_crud_and_vx_pair_use_optimistic_atomic_storage()
    {
        var es = Assignment(DatabentoContractRole.EsQuarterly, fixture.Sources["ES20261218"]);
        var front = Assignment(DatabentoContractRole.VxFrontMonth, fixture.Sources["VX20260916"]);
        var second = Assignment(DatabentoContractRole.VxSecondMonth, fixture.Sources["VX20261021"]);
        var savedEs = await fixture.Store.UpsertAssignmentAsync(es, 0);
        var savedVx = await fixture.Store.ReplaceVxAssignmentsAsync(front, second, 0, 0);

        (await fixture.Store.ListAssignmentsAsync()).Should().HaveCount(3);
        savedEs.RowVersion.Should().Be(1);
        savedVx.Should().OnlyContain(value => value.RowVersion == 1);

        var replacement = Assignment(DatabentoContractRole.VxSecondMonth, fixture.Sources["VX20261118"]);
        var stalePair = () => fixture.Store.ReplaceVxAssignmentsAsync(front, replacement, 1, 0);
        await stalePair.Should().ThrowAsync<Exception>();
        (await fixture.Store.GetAssignmentAsync(DatabentoContractRole.VxSecondMonth))!.ContractId
            .Should().Be("VX20261021", "the failed statement must roll back both VX changes");

        var updatedEs = await fixture.Store.UpsertAssignmentAsync(savedEs with { Description = "updated" }, 1);
        updatedEs.RowVersion.Should().Be(2);
        await fixture.Store.DeleteAssignmentAsync(DatabentoContractRole.EsQuarterly, 2, "integration-test");
        (await fixture.Store.GetAssignmentAsync(DatabentoContractRole.EsQuarterly)).Should().BeNull();
    }

    [Fact]
    public async Task Watchdog_history_crud_persists_json_and_orders_by_observation_time()
    {
        var first = await fixture.Store.AppendObservationAsync(Observation(DateTime.UtcNow.AddMinutes(-1), DatabentoMajorStatus.Down));
        var second = await fixture.Store.AppendObservationAsync(Observation(DateTime.UtcNow, DatabentoMajorStatus.Up));

        second.WatchdogStatusLogId.Should().BeGreaterThan(first.WatchdogStatusLogId);
        (await fixture.Store.ListObservationsAsync()).Select(value => value.ObservationId)
            .Should().Equal(second.ObservationId, first.ObservationId);
        (await fixture.Store.ListObservationsAsync(status: DatabentoMajorStatus.Down)).Should().ContainSingle();

        var updated = await fixture.Store.UpdateObservationAsync(
            first with { FailureDetail = "operator-reviewed" }, first.RowVersion, "integration-test");
        updated.RowVersion.Should().Be(2);
        (await fixture.Store.GetObservationAsync(first.WatchdogStatusLogId))!.FailureDetail
            .Should().Be("operator-reviewed");
        await fixture.Store.DeleteObservationAsync(first.WatchdogStatusLogId, 2, "integration-test");
        (await fixture.Store.GetObservationAsync(first.WatchdogStatusLogId)).Should().BeNull();
    }

    [Fact]
    public async Task Dataset_incident_transition_updates_bounded_current_state_and_hydrates_open_incident()
    {
        var snapshot = new DatasetIncidentSnapshot
        {
            Dataset = "GLBX.MDP3", ValueDate = new(2026, 9, 4),
            IncidentId = Guid.NewGuid(), GenerationId = Guid.NewGuid(), IsOpen = true,
            CooperativeAttempts = 2, UnhealthyDuration = TimeSpan.FromMinutes(2),
            FailureReason = DatabentoDatasetFailureReason.NativeDrainStalled,
            LastAction = DatasetRecoveryAction.CooperativeReset, ObservedOnUtc = DateTime.UtcNow
        };
        var first = await fixture.Store.PersistDatasetIncidentAsync(new(
            Guid.NewGuid(), Guid.NewGuid(), snapshot));
        var closed = await fixture.Store.PersistDatasetIncidentAsync(new(
            Guid.NewGuid(), Guid.NewGuid(), snapshot with { IsOpen = false }));

        first.RowVersion.Should().Be(1);
        closed.RowVersion.Should().Be(2);
        (await fixture.Store.ListOpenDatasetIncidentsAsync()).Should().BeEmpty();

        var reopened = await fixture.Store.PersistDatasetIncidentAsync(new(
            Guid.NewGuid(), Guid.NewGuid(), snapshot with
            {
                IncidentId = Guid.NewGuid(), IsOpen = true, CooperativeAttempts = 1
            }));
        reopened.RowVersion.Should().Be(3);
        (await fixture.Store.ListOpenDatasetIncidentsAsync()).Should().ContainSingle(value =>
            value.Snapshot.Dataset == "GLBX.MDP3" && value.Snapshot.CooperativeAttempts == 1);
    }

    static FuturesRolloverContractAssignment Assignment(
        DatabentoContractRole role, FuturesContractV3ReadModel source)
    {
        var now = DateTime.UtcNow;
        return new()
        {
            ContractRole = role, RootSymbol = source.Symbol, ContractId = source.ContractId,
            Description = source.Description, LocalSymbol = source.LocalSymbol, SecurityType = source.SecurityType,
            Currency = source.Currency, Exchange = source.Exchange, Multiplier = source.Multiplier,
            LastTradeDate = source.LastTradeDate, NextRolloverDate = source.LastTradeDate,
            SourceContractHash = DatabentoContractAuthority.Hash(source), RowVersion = 0,
            CreatedOnUtc = now, CreatedBy = "integration-test", UpdatedOnUtc = now, UpdatedBy = "integration-test"
        };
    }

    static DatabentoWatchdogObservation Observation(DateTime observed, DatabentoMajorStatus major) => new()
    {
        ObservationId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), ValueDate = new(2026, 9, 2),
        ObservedOnUtc = observed, OperationReason = DatabentoOperationReason.WatchdogPoll,
        MajorStatus = major, DisplayHealth = major == DatabentoMajorStatus.Up
            ? DatabentoDisplayHealth.Green : DatabentoDisplayHealth.Red,
        CoreContractsReady = major == DatabentoMajorStatus.Up, RecoveryAttempt = 0,
        NativeBackend = "Cpp", NativeAbiVersion = 3, NativeGeneration = Guid.NewGuid(),
        FailureStage = major == DatabentoMajorStatus.Up ? string.Empty : "Transport",
        FailureDetail = major == DatabentoMajorStatus.Up ? string.Empty : "injected",
        FeedStatusDetails = []
    };
}
