using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Subscriptions;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataServiceDb;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Stage4DurableIntentPostgresCollection : ICollectionFixture<Stage4DurableIntentPostgresFixture>
{
    public const string Name = "Stage 4 isolated PostgreSQL durable intent";
}

/// <summary>Never truncates: all cleanup is parameterized against registered randomized test scopes.</summary>
public sealed class Stage4DurableIntentPostgresFixture : IAsyncLifetime
{
    private const string ScopePrefix = "stage4-test-";
    private readonly ConcurrentDictionary<string, byte> _scopes = new(StringComparer.Ordinal);
    private readonly ILogger<DbProvider> _logger = Substitute.For<ILogger<DbProvider>>();
    private IDbConnectionSettings _settings = null!;
    private TestRepository? _repository;
    private string? _previousDotnet;
    private string? _previousAspnet;
    private bool _environmentChanged;
    private bool _schemaCreated;
    public PostgresDurableSubscriptionIntentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION to the dedicated local test database.");
        ValidateDedicatedConnection(connection); // No connection, credentials or mutation before this guard.
        _previousDotnet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        _previousAspnet = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
        _environmentChanged = true;
        try
        {
            _settings = new DbConnectionSettings().Add(MarketDataServiceDbContext.MarketDataServiceDbConnection,
                connection, "System.Data.Postgres");
            _repository = new TestRepository(_settings[MarketDataServiceDbContext.MarketDataServiceDbConnection], _logger);
            var database = await _repository.Use("Stage4Fixture.VerifyDedicatedDatabase", "SELECT current_database();")
                .ExecuteSingleAsync(row => row.GetString(0));
            if (database != "event-source-test-db") throw new InvalidOperationException("Connected database is not the dedicated Stage 4 test database.");
            _ = await _repository.Use("Stage4Fixture.AdditiveSchema", Stage4SubscriptionSchemaSql.Create).ExecuteCommandAsync();
            _schemaCreated = true;
            Store = NewStore();
        }
        catch
        {
            RestoreEnvironment();
            throw;
        }
    }

    public static void ValidateDedicatedConnection(string value)
    {
        var builder = new NpgsqlConnectionStringBuilder(value);
        if (builder.Host is not ("localhost" or "127.0.0.1" or "::1") || builder.Port != 5432
            || builder.Database != "event-source-test-db")
            throw new InvalidOperationException("Stage 4 tests require localhost:5432/event-source-test-db; application databases are refused.");
    }

    public string NewScope()
    {
        var scope = ScopePrefix + Guid.NewGuid().ToString("N");
        _scopes.TryAdd(scope, 0);
        return scope;
    }

    public PostgresDurableSubscriptionIntentStore NewStore() => new(_settings, _logger);
    internal PostgresDurableSubscriptionIntentStore FaultingStore(Action<DurableStoreWriteStage> observer) =>
        new(_settings, _logger, null, observer);

    public Task<long> ReadWatermarkVersionAsync(string scope, string sourceId) => _repository!
        .Use("Stage4Fixture.ReadWatermark", """
            SELECT COALESCE(MAX(source_version),0) FROM market_data_service.stage4_authority_watermark
            WHERE scope=$1 AND dataset='GLBX.MDP3' AND source_id=$2;
            """).SetParameters(new Parameters(Values(Text(scope), Text(sourceId))))
        .ExecuteScalarAsync(row => row.GetLong(0));

    public Task<long> ReadRetiredLeaseCountAsync(string scope) => _repository!
        .Use("Stage4Fixture.ReadRetiredLeaseIdentities", """
            SELECT COUNT(*) FROM market_data_service.stage4_lease_identity
            WHERE scope=$1 AND dataset='GLBX.MDP3' AND released_revision IS NOT NULL;
            """).SetParameters(new Parameters(Values(Text(scope))))
        .ExecuteScalarAsync(row => row.GetLong(0));

    public async Task DisposeAsync()
    {
        try
        {
            if (!_schemaCreated || _repository is null) return;
            foreach (var scope in _scopes.Keys)
            {
                if (!scope.StartsWith(ScopePrefix, StringComparison.Ordinal) || scope.Length != ScopePrefix.Length + 32)
                    throw new InvalidOperationException("Refusing cleanup outside an exact randomized test scope.");
                // Deliberately no TRUNCATE, DROP, wildcard, or cleanup of any existing application table.
                foreach (var statement in new[]
                {
                    "DELETE FROM market_data_service.stage4_intent_outbox WHERE scope=$1;",
                    "DELETE FROM market_data_service.stage4_intent_operation WHERE scope=$1;",
                    "DELETE FROM market_data_service.stage4_authority_watermark WHERE scope=$1;",
                    "DELETE FROM market_data_service.stage4_lease_identity WHERE scope=$1;",
                    "DELETE FROM market_data_service.stage4_intent_current WHERE scope=$1;"
                })
                    _ = await _repository.Use("Stage4Fixture.CleanupExactScope", statement)
                        .SetParameters(new Parameters(Values(Text(scope)))).ExecuteCommandAsync();
            }
        }
        finally { RestoreEnvironment(); }
    }

    private void RestoreEnvironment()
    {
        if (!_environmentChanged) return;
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _previousDotnet);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _previousAspnet);
        _environmentChanged = false;
    }
    private readonly record struct Parameters(NpgsqlParameter[] Items) : IBindValue { public object Bind() => Items; }
    private sealed class TestRepository(IDbConnectionSetting setting, ILogger<DbProvider> logger)
        : ObjectDataRepository<TestRepository>(setting, logger)
    {
        public override IObjectRepository Database => this;
    }
}

[Collection(Stage4DurableIntentPostgresCollection.Name)]
public sealed class Stage4DurableIntentPostgresTests(Stage4DurableIntentPostgresFixture fixture)
{
    private const string Dataset = "GLBX.MDP3";

    [Fact]
    public async Task Four_leg_commit_lost_response_and_host_recreation_return_the_same_durable_outcome()
    {
        var request = Mutation(fixture.NewScope(), "position-1", 0, 1, Options(4));
        var committed = await fixture.Store.ApplyAsync(request);
        committed.Code.Should().Be(DurableIntentResultCode.Committed);
        committed.Revision.Should().Be(1);
        (await fixture.Store.ApplyAsync(request)).Should().Be(committed);
        (await fixture.NewStore().FindOperationAsync(request.Scope, Dataset, request.OperationId)).Should().Be(committed);
        var restored = await fixture.NewStore().ReadAsync(request.Scope, Dataset);
        restored.Authorities.Should().ContainSingle();
        restored.Authorities[0].Leases.Should().HaveCount(4);
        (await fixture.Store.ReadPendingOutboxAsync(request.Scope, Dataset)).Should().ContainSingle();
        (await fixture.ReadWatermarkVersionAsync(request.Scope, request.SourceId)).Should().Be(1);
        var conflict = await fixture.Store.ApplyAsync(request with { ReasonCode = "DIFFERENT_CONTENT" });
        conflict.Code.Should().Be(DurableIntentResultCode.OperationConflict);
        (await fixture.Store.FindOperationAsync(request.Scope, Dataset, request.OperationId)).Should().Be(committed);
    }

    [Fact]
    public async Task Unknown_and_empty_active_facts_retain_leases_and_only_explicit_terminal_releases()
    {
        var first = Mutation(fixture.NewScope(), "position-2", 0, 1, Options(2));
        await fixture.Store.ApplyAsync(first);
        var repeatedFact = first with { OperationId = Guid.NewGuid(), ExpectedRevision = 100 };
        (await fixture.Store.ApplyAsync(repeatedFact)).Code.Should().Be(DurableIntentResultCode.AlreadyApplied);
        var unknown = Next(first, 1, 2) with { Status = DurableAuthorityStatus.Unknown, Adds = [], ReasonCode = "AUTHORITY_UNAVAILABLE" };
        (await fixture.Store.ApplyAsync(unknown)).Code.Should().Be(DurableIntentResultCode.Committed);
        var snapshot = await fixture.Store.ReadAsync(first.Scope, Dataset);
        snapshot.Authorities[0].Status.Should().Be(DurableAuthorityStatus.Unknown);
        snapshot.Authorities[0].Leases.Should().HaveCount(2);
        var empty = Next(first, 2, 3) with { Adds = [] };
        await fixture.Store.ApplyAsync(empty);
        (await fixture.Store.ReadAsync(first.Scope, Dataset)).Authorities[0].Leases.Should().HaveCount(2);
        var terminal = Next(first, 3, 4) with { Status = DurableAuthorityStatus.Terminal, Adds = [], ReasonCode = "POSITION_CLOSED" };
        await fixture.Store.ApplyAsync(terminal);
        var stale = Next(first, 4, 3);
        (await fixture.Store.ApplyAsync(stale)).Code.Should().Be(DurableIntentResultCode.StaleAuthority);
        snapshot = await fixture.Store.ReadAsync(first.Scope, Dataset);
        snapshot.Authorities[0].Leases.Should().BeEmpty();
        snapshot.Authorities[0].SourceVersion.Should().Be(4);
        (await fixture.ReadWatermarkVersionAsync(first.Scope, first.SourceId)).Should().Be(4);
    }

    [Fact]
    public async Task Duplicate_version_conflicts_and_gaps_cannot_replace_or_resurrect_current_intent()
    {
        var first = Mutation(fixture.NewScope(), "position-3", 0, 1, Options(2));
        await fixture.Store.ApplyAsync(first);
        var conflict = Next(first, 1, 1);
        (await fixture.Store.ApplyAsync(conflict)).Code.Should().Be(DurableIntentResultCode.AuthorityConflict);
        var gap = Next(first, 1, 3);
        (await fixture.Store.ApplyAsync(gap)).Code.Should().Be(DurableIntentResultCode.AuthorityGap);
        var sourceTakeover = first with { OperationId = Guid.NewGuid(), SourceId = "another-stream", ExpectedRevision = 1 };
        (await fixture.Store.ApplyAsync(sourceTakeover)).Code.Should().Be(DurableIntentResultCode.AuthorityConflict);
        (await fixture.Store.ReadAsync(first.Scope, Dataset)).Revision.Should().Be(1);
    }

    [Fact]
    public async Task Two_owners_share_target_but_terminal_evidence_only_removes_its_exact_owner()
    {
        var scope = fixture.NewScope();
        var first = Mutation(scope, "working-order", 0, 1, Options(1));
        var second = Mutation(scope, "filled-position", 1, 1,
            [first.Adds[0] with { LeaseId = Guid.NewGuid() }]);
        await fixture.Store.ApplyAsync(first);
        await fixture.Store.ApplyAsync(second);
        await fixture.Store.ApplyAsync(Next(first, 2, 2) with { Adds = [], Status = DurableAuthorityStatus.Terminal, ReasonCode = "ORDER_CANCELLED" });
        var snapshot = await fixture.Store.ReadAsync(scope, Dataset);
        snapshot.Authorities.Sum(value => value.Leases.Count).Should().Be(1);
        snapshot.Authorities.Single(value => value.SourceId == second.SourceId).Leases.Should().ContainSingle();
    }

    [Fact]
    public async Task Concurrent_first_writers_commit_exactly_one_expected_revision_and_retry_is_explicit()
    {
        var scope = fixture.NewScope();
        var first = Mutation(scope, "owner-a", 0, 1, Options(2));
        var second = Mutation(scope, "owner-b", 0, 1, Options(2));
        var results = await Task.WhenAll(
            Task.Run(() => fixture.Store.ApplyAsync(first)), Task.Run(() => fixture.Store.ApplyAsync(second)));
        results.Count(value => value.Code == DurableIntentResultCode.Committed).Should().Be(1);
        results.Count(value => value.Code == DurableIntentResultCode.RevisionConflict).Should().Be(1);
        var loser = results[0].Code == DurableIntentResultCode.RevisionConflict ? first : second;
        (await fixture.Store.ApplyAsync(loser)).Code.Should().Be(DurableIntentResultCode.RevisionConflict);
        var retry = loser with { OperationId = Guid.NewGuid(), ExpectedRevision = 1 };
        (await fixture.Store.ApplyAsync(retry)).Code.Should().Be(DurableIntentResultCode.Committed);
        (await fixture.Store.ReadAsync(scope, Dataset)).Revision.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Failure_after_each_write_rolls_back_current_result_outbox_and_watermark(int stage)
    {
        var first = Mutation(fixture.NewScope(), "rollback-owner", 0, 1, Options(2));
        await fixture.Store.ApplyAsync(first);
        var next = Next(first, 1, 2) with { Status = DurableAuthorityStatus.Terminal, Adds = [], ReasonCode = "POSITION_CLOSED" };
        var faulting = fixture.FaultingStore(point =>
        {
            if ((int)point == stage) throw new InvalidOperationException("injected transaction failure");
        });
        var action = () => faulting.ApplyAsync(next);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("injected transaction failure");
        var restored = await fixture.NewStore().ReadAsync(first.Scope, Dataset);
        restored.Revision.Should().Be(1);
        restored.Authorities[0].Leases.Should().HaveCount(2);
        (await fixture.Store.FindOperationAsync(next.Scope, Dataset, next.OperationId)).Should().BeNull();
        (await fixture.Store.ReadPendingOutboxAsync(first.Scope, Dataset)).Should().ContainSingle();
        (await fixture.ReadWatermarkVersionAsync(first.Scope, first.SourceId)).Should().Be(1);
        (await fixture.ReadRetiredLeaseCountAsync(first.Scope)).Should().Be(0);
        (await fixture.Store.ApplyAsync(next)).Code.Should().Be(DurableIntentResultCode.Committed);
        (await fixture.ReadWatermarkVersionAsync(first.Scope, first.SourceId)).Should().Be(2);
        (await fixture.ReadRetiredLeaseCountAsync(first.Scope)).Should().Be(2);
    }

    [Fact]
    public async Task Released_GUID_cannot_be_reused_by_a_new_fact_even_with_lease_version_one()
    {
        var first = Mutation(fixture.NewScope(), "incarnation-owner", 0, 1, Options(1));
        await fixture.Store.ApplyAsync(first);
        await fixture.Store.ApplyAsync(Next(first, 1, 2) with
        {
            Adds = [], Releases = [new(first.Adds[0].LeaseId, 1)]
        });
        var reused = Next(first, 2, 3);
        (await fixture.NewStore().ApplyAsync(reused)).Code.Should().Be(DurableIntentResultCode.LeaseConflict);
        (await fixture.Store.ReadAsync(first.Scope, Dataset)).Revision.Should().Be(2);
        (await fixture.ReadWatermarkVersionAsync(first.Scope, first.SourceId)).Should().Be(2);
        (await fixture.ReadRetiredLeaseCountAsync(first.Scope)).Should().Be(1);
        var fresh = reused with { OperationId = Guid.NewGuid(), SourceEventId = Guid.NewGuid(), Adds = [first.Adds[0] with { LeaseId = Guid.NewGuid() }] };
        (await fixture.Store.ApplyAsync(fresh)).Code.Should().Be(DurableIntentResultCode.Committed);
    }

    [Fact]
    public async Task Terminal_owner_GUID_cannot_be_taken_over_by_another_authority_stream()
    {
        var first = Mutation(fixture.NewScope(), "retired-owner", 0, 1, Options(1));
        await fixture.Store.ApplyAsync(first);
        await fixture.Store.ApplyAsync(Next(first, 1, 2) with { Adds = [], Status = DurableAuthorityStatus.Terminal });
        var another = Mutation(first.Scope, "different-owner", 2, 1, first.Adds);
        (await fixture.Store.ApplyAsync(another)).Code.Should().Be(DurableIntentResultCode.LeaseConflict);
        (await fixture.Store.ReadAsync(first.Scope, Dataset)).Authorities.Should().ContainSingle();
    }

    [Fact]
    public async Task Wrong_incarnation_rejects_entire_batch_and_correct_handoff_has_no_partial_persistence()
    {
        var first = Mutation(fixture.NewScope(), "handoff-owner", 0, 1, Options(2));
        await fixture.Store.ApplyAsync(first);
        var added = Options(1).Select(value => value with { Ticker = value.Ticker with { ContractId = "ES-OPTION-NEW" } }).ToArray();
        var wrong = Next(first, 1, 2) with
        {
            Adds = added, Releases = [new(first.Adds[0].LeaseId, first.Adds[0].LeaseVersion + 1)]
        };
        (await fixture.Store.ApplyAsync(wrong)).Code.Should().Be(DurableIntentResultCode.LeaseConflict);
        (await fixture.Store.ReadAsync(first.Scope, Dataset)).Authorities[0].Leases.Should().HaveCount(2);
        var correct = wrong with { OperationId = Guid.NewGuid(), Releases = [new(first.Adds[0].LeaseId, first.Adds[0].LeaseVersion)] };
        (await fixture.Store.ApplyAsync(correct)).Code.Should().Be(DurableIntentResultCode.Committed);
        var leases = (await fixture.Store.ReadAsync(first.Scope, Dataset)).Authorities[0].Leases;
        leases.Should().HaveCount(2);
        leases.Should().NotContain(value => value.LeaseId == first.Adds[0].LeaseId);
        leases.Should().Contain(value => value.LeaseId == added[0].LeaseId);
    }

    [Fact]
    public async Task Cancellation_before_commit_rolls_back_and_retry_recovers_without_false_success()
    {
        using var cancellation = new CancellationTokenSource();
        var input = Mutation(fixture.NewScope(), "cancel-owner", 0, 1, Options(4));
        var store = fixture.FaultingStore(point => { if (point == DurableStoreWriteStage.AuthorityWatermark) cancellation.Cancel(); });
        var action = () => store.ApplyAsync(input, cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        (await fixture.Store.ReadAsync(input.Scope, Dataset)).Revision.Should().Be(0);
        (await fixture.Store.FindOperationAsync(input.Scope, Dataset, input.OperationId)).Should().BeNull();
        (await fixture.ReadWatermarkVersionAsync(input.Scope, input.SourceId)).Should().Be(0);
        (await fixture.Store.ApplyAsync(input)).Code.Should().Be(DurableIntentResultCode.Committed);
    }

    [Fact]
    public async Task Outbox_is_bounded_repeatable_until_ack_and_ack_is_scope_checked_and_idempotent()
    {
        var first = Mutation(fixture.NewScope(), "outbox-owner", 0, 1, Options(2));
        await fixture.Store.ApplyAsync(first);
        await fixture.Store.ApplyAsync(Next(first, 1, 2) with { Adds = [], Status = DurableAuthorityStatus.Unknown });
        var pending = await fixture.Store.ReadPendingOutboxAsync(first.Scope, Dataset, 1);
        pending.Should().ContainSingle();
        (await fixture.Store.ReadPendingOutboxAsync(first.Scope, Dataset, 1)).Should().Equal(pending);
        (await fixture.Store.AcknowledgeOutboxAsync(fixture.NewScope(), Dataset, pending[0].TransitionId)).Should().BeFalse();
        (await fixture.Store.AcknowledgeOutboxAsync(first.Scope, Dataset, pending[0].TransitionId)).Should().BeTrue();
        (await fixture.Store.AcknowledgeOutboxAsync(first.Scope, Dataset, pending[0].TransitionId)).Should().BeTrue();
        (await fixture.Store.ReadPendingOutboxAsync(first.Scope, Dataset)).Should().ContainSingle(value => value.Revision == 2);
        var oversized = () => fixture.Store.ReadPendingOutboxAsync(first.Scope, Dataset, 1001);
        await oversized.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private static DurableAuthorityMutation Mutation(string scope, string owner, long revision, long sourceVersion,
        IReadOnlyList<DurableSubscriptionLease> adds) => new(scope, Dataset, Guid.NewGuid(), Guid.NewGuid(), revision,
        "authority/" + owner, sourceVersion, Guid.NewGuid(), new("SyntheticAuthority", owner, "all"),
        DurableAuthorityStatus.Active, "EXPLICIT_INTENT", adds, []);

    private static DurableAuthorityMutation Next(DurableAuthorityMutation first, long revision, long sourceVersion) => first with
    {
        OperationId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), SourceEventId = Guid.NewGuid(),
        ExpectedRevision = revision, SourceVersion = sourceVersion
    };

    private static DurableSubscriptionLease[] Options(int count) => Enumerable.Range(0, count).Select(index =>
        new DurableSubscriptionLease(Guid.NewGuid(), 1, SubscriptionLeasePurpose.Position,
            new("Databento", Dataset, "ES-OPTION-" + index, "mbp-1", SubscriptionAssetKind.FuturesOption, "ES20261218"))).ToArray();
}

public sealed class Stage4DurableIntentSafetyTests
{
    [Fact]
    public void Null_lease_in_persisted_snapshot_is_explicitly_rejected()
    {
        var snapshot = new DurableSubscriptionSnapshot(1, "test", "GLBX.MDP3", 1,
            [new("source", 1, Guid.NewGuid(), new string('A', 64), new("Position", "1", "all"),
                DurableAuthorityStatus.Active, "EXPLICIT", [null!])]);
        var action = () => DurableSubscriptionContract.Freeze(snapshot);
        action.Should().Throw<System.IO.InvalidDataException>().WithMessage("Null lease*");
    }

    [Theory]
    [InlineData("Host=localhost;Database=application-db")]
    [InlineData("Host=remote-host;Database=event-source-test-db")]
    [InlineData("Host=localhost;Port=5433;Database=event-source-test-db")]
    [InlineData("Host=localhost,remote-host;Database=event-source-test-db")]
    public void Fixture_refuses_application_or_remote_database_before_any_mutation(string connection)
    {
        var action = () => Stage4DurableIntentPostgresFixture.ValidateDedicatedConnection(connection);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Typed_intent_rejects_ephemeral_purpose_and_unknown_deltas_before_storage()
    {
        var lease = new DurableSubscriptionLease(Guid.NewGuid(), 1, SubscriptionLeasePurpose.Discovery,
            new("Databento", "GLBX.MDP3", "ES20261218", "mbp-1", SubscriptionAssetKind.Futures));
        var request = new DurableAuthorityMutation("test", "GLBX.MDP3", Guid.NewGuid(), Guid.NewGuid(), 0,
            "source", 1, Guid.NewGuid(), new("Position", "1", "all"), DurableAuthorityStatus.Active, "EXPLICIT", [lease], []);
        var ephemeral = () => DurableSubscriptionContract.Freeze(request);
        ephemeral.Should().Throw<ArgumentException>();
        var unknown = () => DurableSubscriptionContract.Freeze(request with
        {
            Status = DurableAuthorityStatus.Unknown, Adds = [lease with { Purpose = SubscriptionLeasePurpose.Position }]
        });
        unknown.Should().Throw<ArgumentException>();
    }
}
