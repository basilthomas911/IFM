using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Subscriptions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Genuine committed PostgreSQL events, source readers, intent transactions and restartable projection receipts.</summary>
public sealed partial class CompositionBusinessProjectionTests
{
    [Fact]
    public async Task Legacy_workflow_without_selected_contracts_is_receipted_without_consuming_authority_capacity()
    {
        await using var fixture = await Fixture.Create();
        var workflow = new WorkflowStrategyStateUpdatedEvent
        {
            Id = Guid.NewGuid(), WorkflowId = new(Guid.NewGuid()), WorkflowRevision = 1,
            State = new() { Status = WorkflowStrategyMachineStatus.Started }
        };
        workflow = workflow with { State = workflow.State with { WorkflowId = workflow.WorkflowId,
            WorkflowRevision = workflow.WorkflowRevision, EntityId = workflow.EntityId } };
        await fixture.Append(workflow, 1);
        Assert.Equal(1, await fixture.Projector().ProjectPendingAsync(default));
        Assert.Empty((await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3")).Authorities);
        Assert.Empty(await fixture.Store.ReadPendingOutboxAsync(fixture.Scope, "GLBX.MDP3"));
        Assert.Equal(0, await fixture.Projector().ProjectPendingAsync(default));
    }

    [Fact]
    public async Task Composer_NoTrade_without_selected_legs_projects_terminal_authority_for_discovery_release()
    {
        await using var fixture = await Fixture.Create();
        var workflow = new WorkflowStrategyStateUpdatedEvent { Id = Guid.NewGuid(), WorkflowId = new(Guid.NewGuid()), WorkflowRevision = 6,
            State = new() { Status = WorkflowStrategyMachineStatus.Completed, Outcome = StrategyWorkflowOutcome.NoTrade } };
        workflow = workflow with { State = workflow.State with { WorkflowId = workflow.WorkflowId, WorkflowRevision = 6, EntityId = workflow.EntityId } };
        await fixture.Append(workflow, 6);
        Assert.Equal(1, await fixture.Projector().ProjectPendingAsync(default));
        var authority = Assert.Single((await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3")).Authorities);
        Assert.Equal(DurableAuthorityStatus.Terminal, authority.Status); Assert.Empty(authority.Leases);
        Assert.Single(await fixture.Journal.ReadPendingHandoffsAsync(default));
    }

    [Theory]
    [InlineData(2)] [InlineData(4)]
    public async Task Committed_order_position_and_terminal_events_project_all_legs_and_replay_after_restart(int count)
    {
        await using var fixture = await Fixture.Create();
        var plan = Plan(count, fixture.Scope);
        await fixture.SavePlan(plan);
        var selected = new CompositionContractSelection(plan.PlanId, plan.Options.Select(x => x.ContractId).ToImmutableArray());
        var trade = new OptionTradeReadModel { OrderId = 9876, TradeId = 1, TradeState = TradeState.OrderPlaced,
            CompositionContracts = selected, UnderlyingContractId = "ES-future" }
            .AddOptionLegs(selected.ContractIds.Select(x => new OptionTradeLegReadModel { ContractId = x, Quantity = 1 }).ToArray());
        var entity = trade.EntityId;
        var placed = new OptionTradeOrderPlacedEvent { Id = Guid.NewGuid(), EntityId = entity, OptionTrade = trade,
            Subject = new(ActorType.Event, OptionTradeOrderPlacedEvent.Actor, OptionTradeOrderPlacedEvent.Verb, entity.Format()) };
        var placedId = await fixture.Append(placed, 10);
        var uncertain = new FailReceiptOnce(fixture.Journal);
        var failedProjector = new CommittedCompositionSubscriptionProjector(uncertain,
            new CommittedCompositionSubscriptionSource(fixture.Events, uncertain, fixture.Store, fixture.Plans, fixture.Scope),
            fixture.Store, Substitute.For<ILogger<CommittedCompositionSubscriptionProjector>>());
        await Assert.ThrowsAsync<IOException>(() => failedProjector.ProjectPendingAsync(default));
        var committedBeforeReceipt = await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3");
        Assert.Equal(1, await fixture.Projector().ProjectPendingAsync(default));
        var initial = await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3");
        Assert.Equal(committedBeforeReceipt.Revision, initial.Revision);
        Assert.Equal(count, initial.Authorities.Single(x => x.Owner.WorkflowType == "TradeOrder").Leases.Count);
        Assert.Equal(0, await fixture.Projector().ProjectPendingAsync(default));
        // Re-read exact persisted source and replay its version: no fabricated contiguous version or new lease IDs.
        var row = (await fixture.Events.GetEventLogByEventIdAsync(placedId))!;
        var fact = (await fixture.Source().ReadAsync(CommittedCompositionSubscriptionSource.Reference(row, BusinessSubscriptionSourceKind.TradeOrder), default))!;
        Assert.Equal(DurableIntentResultCode.AlreadyApplied, (await fixture.Store.ApplyAsync(fact)).Code);
        var opened = new OptionTradePositionOpenedEvent { Id = Guid.NewGuid(), EntityId = entity, OptionTradeId = entity,
            TradePositionState = TradePositionState.Opened,
            Subject = new(ActorType.Event, OptionTradePositionOpenedEvent.Actor, OptionTradePositionOpenedEvent.Verb, entity.Format()) };
        await fixture.Append(opened, 18);
        // New projector and adapter instances reconstruct exact legs from the earlier committed snapshot.
        Assert.Equal(1, await fixture.Projector().ProjectPendingAsync(default));
        var position = await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3");
        Assert.Empty(position.Authorities.Single(x => x.Owner.WorkflowType == "TradeOrder").Leases);
        Assert.Equal(count, position.Authorities.Single(x => x.Owner.WorkflowType == "TradePosition").Leases.Count);
        var deleted = new OptionTradeDeletedEvent { Id = Guid.NewGuid(), EntityId = entity,
            Subject = new(ActorType.Event, OptionTradeDeletedEvent.Actor, OptionTradeDeletedEvent.Verb, entity.Format()) };
        await fixture.Append(deleted, 24);
        await fixture.Projector().ProjectPendingAsync(default);
        Assert.Equal(count, (await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3")).Authorities
            .Single(x => x.Owner.WorkflowType == "TradePosition").Leases.Count);
        var closed = new OptionTradePositionClosedEvent { Id = Guid.NewGuid(), EntityId = entity, OptionTradeId = entity,
            TradePositionState = TradePositionState.Closed,
            Subject = new(ActorType.Event, OptionTradePositionClosedEvent.Actor, OptionTradePositionClosedEvent.Verb, entity.Format()) };
        await fixture.Append(closed, 31);
        await fixture.Projector().ProjectPendingAsync(default);
        Assert.All((await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3")).Authorities, x => Assert.Empty(x.Leases));
        Assert.NotEmpty(await fixture.Store.ReadPendingOutboxAsync(fixture.Scope, "GLBX.MDP3")); // Projection is not worker acknowledgement.
    }

    [Fact]
    public async Task Workflow_source_requires_exact_committed_identity_and_unknown_retains_its_selected_legs()
    {
        await using var fixture = await Fixture.Create(); var plan = Plan(4, fixture.Scope);
        await fixture.SavePlan(plan);
        var workflow = new WorkflowStrategyStateUpdatedEvent
        {
            Id = Guid.NewGuid(), WorkflowId = new(Guid.NewGuid()), WorkflowRevision = 7,
            State = new() { Status = WorkflowStrategyMachineStatus.Started,
                CompositionContracts = new(plan.PlanId, plan.Options.Select(x => x.ContractId).ToImmutableArray()) }
        };
        workflow = workflow with { State = workflow.State with { WorkflowId = workflow.WorkflowId,
            WorkflowRevision = workflow.WorkflowRevision, EntityId = workflow.EntityId } };
        var id = await fixture.Append(workflow, 7);
        await fixture.Projector().ProjectPendingAsync(default);
        Assert.Single(await fixture.Journal.ReadPendingHandoffsAsync(default));
        await fixture.Journal.CompleteHandoffAsync(id, default);
        Assert.Empty(await new PostgresCommittedBusinessEventJournal(fixture.Settings).ReadPendingHandoffsAsync(default));
        var row = (await fixture.Events.GetEventLogByEventIdAsync(id))!;
        var reference = CommittedCompositionSubscriptionSource.Reference(row, BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Source().ReadAsync(reference with { EventId = Guid.NewGuid() }, default));
        await fixture.Append(workflow with { Id = Guid.NewGuid(), State = workflow.State with { Status = WorkflowStrategyMachineStatus.Completed } }, 10);
        await fixture.Projector().ProjectPendingAsync(default);
        Assert.Equal(4, (await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3")).Authorities.Single().Leases.Count);
        await fixture.Append(workflow with { Id = Guid.NewGuid(), State = workflow.State with { Status = WorkflowStrategyMachineStatus.Cancelled } }, 12);
        await fixture.Projector().ProjectPendingAsync(default);
        Assert.Empty((await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3")).Authorities.Single().Leases);
    }

    static CompositionRoutePlan Plan(int count, string id)
    {
        var options = Enumerable.Range(1, count).Select(i => new OptionDefinitionCandidate(id + "-leg-" + i, "fixture/v1", new('a', 64), new()
        { Dataset = "GLBX.MDP3", Ticker = "ES", Underlying = "ES-future", Instrument = new(1, (uint)i), RawSymbol = id + i,
            Right = TomasAI.IFM.Framework.MarketData.DataBento.OptionRightSelection.Call, StrikePrice = 5000 + i, MaturityDate = new(2026, 10, 2) })).ToImmutableArray();
        return new CompositionRoutePlan(1, "", "GLBX.MDP3", new(2026, 10, 2), options, [],
            [new() { Dataset = "GLBX.MDP3", DomainContractId = "ES-future", ProviderContractName = "ESZ6", RootSymbol = "ES", AssetTypeId = AssetTypeId.Futures }],
            new("fixture/v1", "UTC", new(2026, 9, 8), new(2026, 10, 2), new(18, 0), [new(2026, 9, 8), new(2026, 10, 2)]),
            new("fixture/v1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), []),
            new("FinancialModelingPrep", "fixture", TreasuryRateConvention.UsTreasuryCmtNominalSemiannual, "fixture/v1", "synthetic-evidence")).Seal();
    }

    sealed class Fixture : IAsyncDisposable
    {
        public string Scope { get; } = "ocp-business-test-" + Guid.NewGuid().ToString("N");
        readonly string schema = "ocp_events_" + Guid.NewGuid().ToString("N");
        readonly List<string> planIds = [];
        NpgsqlConnection db = null!;
        IDbConnectionSettings settings = null!;
        public IDbConnectionSettings Settings => settings;
        public EventSourceActorDbContext Events { get; private set; } = null!;
        public PostgresDurableSubscriptionIntentStore Store { get; private set; } = null!;
        public PostgresCompositionRoutePlanStore Plans { get; private set; } = null!;
        public PostgresCommittedBusinessEventJournal Journal { get; private set; } = null!;
        public async Task SavePlan(CompositionRoutePlan plan)
        { await Plans.SaveAsync(plan, default); planIds.Add(plan.PlanId); }
        public CommittedCompositionSubscriptionSource Source() => new(Events, Journal, Store, Plans, Scope);
        public CommittedCompositionSubscriptionProjector Projector() => new(Journal, Source(), Store, Substitute.For<ILogger<CommittedCompositionSubscriptionProjector>>());

        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            var raw = Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION") ?? throw new InvalidOperationException("Dedicated PostgreSQL test connection required.");
            var c = new NpgsqlConnectionStringBuilder(raw);
            if (c.Host is not ("localhost" or "127.0.0.1") || c.Port != 5432 || c.Database != "event-source-test-db")
                throw new InvalidOperationException("Refusing a non-test database.");
            c.SearchPath = f.schema + ",public";
            f.db = new TomasAI.IFM.Framework.Storage.Postgres.PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(c.ConnectionString);
            await f.db.OpenAsync();
            await f.Sql($"CREATE SCHEMA {f.schema}; CREATE TABLE {f.schema}.event_name_id(eventNameId integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,eventName text,eventTypeName text); CREATE TABLE {f.schema}.event_log(eventStreamId bigint,eventNameId integer,eventVersion bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,streamVersion bigint,eventData text,commandId uuid,eventTimestamp text);");
            await f.Sql(PostgresCommittedBusinessEventJournal.CreateTable);
            await f.Sql("CREATE SCHEMA IF NOT EXISTS market_data_service;");
            await f.Sql(Stage4SubscriptionSchemaSql.Create);
            await f.Sql(PostgresCompositionRoutePlanStore.CreateTable);
            f.settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection, c.ConnectionString, "System.Data.Postgres")
                .Add(MarketDataServiceDbContext.MarketDataServiceDbConnection, c.ConnectionString, "System.Data.Postgres");
            var logger = Substitute.For<ILogger<DbProvider>>();
            var factory = Substitute.For<IDbContextFactory>();
            f.Events = new(f.settings, factory, Substitute.For<IBlackboardService>(), logger);
            factory.ActorEventSourceDb.Returns(f.Events);
            f.Store = new(f.settings, logger); f.Plans = new(f.settings); f.Journal = new(f.settings);
            return f;
        }

        public async Task<long> Append(IEvent value, long version)
        {
            await using var transaction = await db.BeginTransactionAsync();
            await using var name = new NpgsqlCommand("INSERT INTO event_name_id(eventName,eventTypeName) VALUES($1,$2) RETURNING eventNameId;", db, transaction);
            name.Parameters.Add(new NpgsqlParameter { Value = value.EventName });
            name.Parameters.Add(new NpgsqlParameter { Value = value.GetType().AssemblyQualifiedName! });
            var nameId = (int)(await name.ExecuteScalarAsync())!;
            await using var command = new NpgsqlCommand("INSERT INTO event_log(eventStreamId,eventNameId,streamVersion,eventData,commandId,eventTimestamp) VALUES(1,$1,$2,$3,$4,$5) RETURNING eventVersion;", db, transaction);
            foreach (var arg in new object[] { nameId, version, JsonConvert.SerializeObject(value), Guid.NewGuid(), DateTime.UtcNow.ToString("O") })
                command.Parameters.Add(new NpgsqlParameter { Value = arg });
            var result = (long)(await command.ExecuteScalarAsync())!;
            await transaction.CommitAsync(); return result;
        }

        async Task Sql(string sql) { await using var cmd = new NpgsqlCommand(sql, db); await cmd.ExecuteNonQueryAsync(); }
        public async ValueTask DisposeAsync()
        {
            foreach (var table in new[] { "stage4_intent_outbox", "stage4_authority_watermark", "stage4_lease_identity", "stage4_intent_operation", "stage4_intent_current" })
            {
                await using var command = new NpgsqlCommand($"DELETE FROM market_data_service.{table} WHERE scope=$1;", db);
                command.Parameters.Add(new NpgsqlParameter { Value = Scope }); await command.ExecuteNonQueryAsync();
            }
            await using (var command = new NpgsqlCommand("DELETE FROM market_data_service.composition_route_plan WHERE plan_id=ANY($1);", db))
            { command.Parameters.Add(new NpgsqlParameter { Value = planIds.ToArray() }); await command.ExecuteNonQueryAsync(); }
            await Sql($"DROP SCHEMA {schema} CASCADE;"); await db.DisposeAsync();
        }
    }

    sealed class FailReceiptOnce(ICommittedBusinessEventJournal inner) : ICommittedBusinessEventJournal
    {
        bool failed;
        public Task<IReadOnlyList<EventLogReadModel>> ReadPendingAsync(IReadOnlyList<string> names, CancellationToken ct) => inner.ReadPendingAsync(names, ct);
        public Task<EventLogReadModel?> ReadPriorAsync(long stream, long version, IReadOnlyList<string> names, CancellationToken ct) => inner.ReadPriorAsync(stream, version, names, ct);
        public Task AcknowledgeAsync(long id, CancellationToken ct)
        { if (!failed) { failed = true; throw new IOException("Injected crash after ownership commit before receipt."); } return inner.AcknowledgeAsync(id, ct); }
        public Task<IReadOnlyList<EventLogReadModel>> ReadPendingHandoffsAsync(CancellationToken ct) => inner.ReadPendingHandoffsAsync(ct);
        public Task CompleteHandoffAsync(long id, CancellationToken ct) => inner.CompleteHandoffAsync(id, ct);
    }
}
