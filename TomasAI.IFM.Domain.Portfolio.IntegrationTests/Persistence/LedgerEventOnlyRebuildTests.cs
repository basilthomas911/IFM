using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Extensions;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"), Trait("Category", "PortfolioFinancial")]
[Trait("Infrastructure", "SelfContained")]
public sealed class LedgerEventOnlyRebuildTests(PortfolioEventStoreFixture fixture) : IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Serialized_source_events_rebuild_journals_balances_obligations_and_overdrawn_state_once()
    {
        var book = await CreateBook();
        var requests = new[]
        {
            Request(book, LedgerTransactionKind.DepositConfirmed, 100, 0),
            Request(book, LedgerTransactionKind.WithdrawalRequested, 25, 1),
            Request(book, LedgerTransactionKind.Commission, 150, 2)
        };
        foreach (var request in requests) await Post(request);
        // Read the source event bytes through the event-store codec, not the returned completion object or journal rows.
        var events = new List<LedgerPostingCompletedEvent>();
        foreach (var request in requests)
            events.Add((await new PortfolioFinancialStore(Transactions()).ReadOperationAsync<LedgerPostingCompletedEvent>(
                book.PortfolioId, request.OperationId))!);
        events.Should().OnlyContain(x => x.SchemaVersion == 2 && x.ProjectionFacts != null);
        var before = await LedgerState(book.BookId);
        var exportRequest=new AccountingExportRequest(Guid.NewGuid(),book.PortfolioId,book.BookId,3,[events[0].Receipt.JournalId!.Value],
            new("rebuild-company/"+book.PortfolioId,1,[new(101,1,"cash"),new(102,1,"equity")]));
        var access=new FinancialAccess("integration",["PortfolioAdministrator"]);
        var exports=new AccountingExportStore(Transactions());
        await exports.PrepareAsync(exportRequest,access,AccountingExportModel.Create);
        var attemptId=Guid.NewGuid();var delivered=await exports.RecordAttemptAsync(book.PortfolioId,exportRequest.Mapping.DestinationCompany,exportRequest.ExportId,attemptId,"Delivered","test-external-receipt",access);
        var preparedExport=(await Transactions().ExecuteAsync((db,ct)=>CapacityReservationStore.ReadEvidenceAsync<AccountingExportSnapshotEvent>(db,exportRequest.ExportId,ct)))!;
        var deliveredExport=(await Transactions().ExecuteAsync((db,ct)=>CapacityReservationStore.ReadEvidenceAsync<AccountingExportSnapshotEvent>(db,attemptId,ct)))!;
        var intentCommand=await BrokerAccountingIntentIntegrationTests.Command();
        var intents=new BrokerAccountingIntentStore(Transactions());await intents.ClaimAsync(intentCommand,new string('A',64));
        var capturedIntent=(await Transactions().ExecuteAsync((db,ct)=>CapacityReservationStore.ReadEvidenceAsync<BrokerAccountingIntentCapturedEvent>(db,intentCommand.CommandId,ct)))!;
        // Rebuild into a new database. Posted-history guards remain enabled in both source and target.
        var database = "ifm_ledger_rebuild_" + Guid.NewGuid().ToString("N");
        var sourceConnection = Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")!;
        var builder = new NpgsqlConnectionStringBuilder(sourceConnection);
        if (builder.Host is not ("localhost" or "127.0.0.1") || builder.Database != "ifm_storage_refactor_test")
            throw new InvalidOperationException("This test requires the explicitly isolated storage-refactor database.");
        builder.Database = "postgres";
        await using var admin = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(builder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            builder.Database = database;
            var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection, builder.ConnectionString, "System.Data.Postgres");
            await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
            var target = new PostgresEventTransaction(settings);
            await new PortfolioFinancialSchema(target).InitializeAsync();
            // Book setup is a fixture prerequisite for this ledger-only test. Configuration reconstruction is tested separately.
            await new PortfolioFinancialStore(target).CreateBookAsync(book,
                [new(101, 1, "Cash", PostingSide.Debit, true, "cash"), new(102, 1, "Equity", PostingSide.Credit, true, "equity"), new(103, 1, "Expense", PostingSide.Debit, true, "expense")],
                [Rule(LedgerTransactionKind.DepositConfirmed), Rule(LedgerTransactionKind.WithdrawalRequested), Rule(LedgerTransactionKind.Commission)],
                new DateOnly(2020, 1, 1), new DateOnly(2099, 12, 31));
            foreach (var source in events)
                await target.ExecuteAsync((db, ct) => db.AppendAsync("LedgerRebuild.Test." + book.BookId, source.CommandId, source,
                    source.Receipt.FinancialRevision - 1, ct));
        foreach(var source in new TomasAI.IFM.Shared.EventSourcing.IEvent[] {preparedExport,deliveredExport,capturedIntent})
            await target.ExecuteAsync((db,ct)=>db.AppendAsync("OperationalRebuild."+source.Id,source.CommandId,source,0,ct));
        var projection = new LedgerFinancialProjection(target);
        foreach (var source in events) await projection.ReplayAsync(source);
        (await LedgerState(book.BookId, target)).Should().Be(before);
        foreach (var source in events) await projection.ReplayAsync(source);
        (await LedgerState(book.BookId, target)).Should().Be(before);
        var state = await target.ExecuteAsync((db, ct) => db.ScalarAsync(
            "SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;", [book.PortfolioId], ct));
        state.Should().Be("Overdrawn");
        var rebuiltExports=new AccountingExportStore(target);var rebuiltIntents=new BrokerAccountingIntentStore(target);
        for(var pass=0;pass<2;pass++)
        {
            await rebuiltExports.ReplayAsync(preparedExport);await rebuiltExports.ReplayAsync(deliveredExport);await rebuiltIntents.ReplayAsync(capturedIntent);
            var actual=await rebuiltExports.ReadAsync(book.PortfolioId,exportRequest.Mapping.DestinationCompany,exportRequest.ExportId,access);
            actual.Should().BeEquivalentTo(delivered);
            var transport=await rebuiltIntents.ReadAsync(intentCommand.PortfolioId,intentCommand.OperationId,new string('A',64));
            transport.Should().BeEquivalentTo(intentCommand);
        }
        }
        finally
        {
            // Drop only the exact database name generated and created by this test.
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Configuration_and_ledger_rebuild_from_serialized_events_into_an_empty_database()
    {
        var id = Random.Shared.Next(100000, 1000000000);
        var book = new FinancialBookConfiguration { BookId=id,PortfolioId=id,AccountingEntityId=Guid.NewGuid(),
            Environment="Integration",ExecutionAccountReference=Guid.NewGuid().ToString("N"),Funds=[new() { FundId=id+1,PortfolioStreamVersion=1,FundStreamVersion=1 }] };
        await Transactions().ExecuteAsync(async (db, ct) =>
        {
            foreach (var stream in new[] { $"Portfolio.{id}", $"PortfolioFund.{id}.{id+1}" })
            {
                var command=Guid.NewGuid();
                await db.AppendAsync(stream,command,new TomasAI.IFM.Domain.Portfolio.Shared.Events.PortfolioCreatedEvent(Guid.NewGuid(),command,1,DateTime.UtcNow,"integration",new()),0,ct);
            }
            return true;
        });
        var create=LedgerConfigurationIntegrationTests.Command(book,0,new() { Action=LedgerConfigurationAction.CreateBook,BookId=id,Book=book,
            Accounts=[new(101,1,"Cash",PostingSide.Debit,true,"cash"),new(102,1,"Equity",PostingSide.Credit,true,"equity"),new(103,1,"Expense",PostingSide.Debit,true,"expense")],
            Rules=[Rule(LedgerTransactionKind.DepositConfirmed)],PeriodId=Guid.NewGuid(),PeriodStart=new(2020,1,1),PeriodEnd=new(2099,12,31) });
        await new LedgerConfigurationStore(Transactions()).ConfigureAsync(create,r=>create.Complete(r),FinancialCanonicalHash.Compute);
        var deposit=Request(book,LedgerTransactionKind.DepositConfirmed,100,1);
        await Post(deposit);
        var reconcile=LedgerConfigurationIntegrationTests.Command(book,2,new() { Action=LedgerConfigurationAction.Reconcile,BookId=id,SourceCut="source:2" });
        await new LedgerConfigurationStore(Transactions()).ConfigureAsync(reconcile,r=>reconcile.Complete(r),FinancialCanonicalHash.Compute);
        var store=new PortfolioFinancialStore(Transactions());
        var first=(await store.ReadOperationAsync<LedgerConfigurationCompletedEvent>(id,create.OperationId))!;
        var second=(await store.ReadOperationAsync<LedgerPostingCompletedEvent>(id,deposit.OperationId))!;
        var third=(await store.ReadOperationAsync<LedgerConfigurationCompletedEvent>(id,reconcile.OperationId))!;
        first.ProjectionFacts.Should().NotBeNull(); third.ProjectionFacts!.Reconciliation!.Debits.Should().Be(100);
        var sourceCut=await Transactions().ExecuteAsync(async(db,ct)=>Convert.ToInt64(await db.ScalarAsync("SELECT max(eventversion) FROM event_log;",[],ct)));
        var selected=new List<TomasAI.IFM.Shared.EventSourcing.IEvent>();
        await foreach(var source in new FinancialEventLogReader(Transactions()).ReadAsync(sourceCut,new HashSet<int>{id},true)) selected.Add(source);
        selected.Select(x=>x.Id).Should().Equal(first.Id,second.Id,third.Id);
        var before=await LedgerState(id);
        var database="ifm_ledger_rebuild_"+Guid.NewGuid().ToString("N");
        var builder=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")!);
        if (builder.Host is not ("localhost" or "127.0.0.1") || builder.Database!="ifm_storage_refactor_test") throw new InvalidOperationException("Isolated database required.");
        builder.Database="postgres";
        await using var admin=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(builder.ConnectionString);
        await admin.OpenAsync();
        await using (var command=new NpgsqlCommand($"CREATE DATABASE {database}",admin)) await command.ExecuteNonQueryAsync();
        try
        {
            builder.Database=database;
            var settings=new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,builder.ConnectionString,"System.Data.Postgres");
            await new EventSourceSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
            var target=new PostgresEventTransaction(settings);
            await new PortfolioFinancialSchema(target).InitializeAsync();
            var sources=new TomasAI.IFM.Shared.EventSourcing.IEvent[] { first,second,third };
            foreach (var source in sources)
                await target.ExecuteAsync((db,ct)=>db.AppendAsync("Rebuild."+source.Id,Guid.NewGuid(),source,0,ct));
            var configProjection=new LedgerConfigurationProjection(target);
            var ledgerProjection=new LedgerFinancialProjection(target);
            for (var pass=0;pass<2;pass++)
            {
                await configProjection.ReplayAsync(first); await ledgerProjection.ReplayAsync(second); await configProjection.ReplayAsync(third);
                (await LedgerState(id,target)).Should().Be(before);
                var actual=await new PortfolioFinancialStore(target).ReadBookAsync(id);
                actual.Should().BeEquivalentTo(first.ProjectionFacts!.AcceptedBook);
                var totals=await target.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT (totals->>'Debits')::numeric FROM portfolio_financial.ledger_reconciliation WHERE reconciliation_id=$1;",[reconcile.OperationId],ct));
                totals.Should().Be(100m);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop=new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin); await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Recorded_capacity_deltas_rebuild_reserve_consume_and_partial_fill_without_reassessment()
    {
        var book=await CapacityReservationIntegrationTests.FundedBook();
        var reserve=await CapacityReservationIntegrationTests.ReserveRequest(book,700);
        await CapacityReservationIntegrationTests.Reserve(reserve);
        var consume=CapacityReservationIntegrationTests.ConsumeRequest(reserve,2);
        await CapacityReservationIntegrationTests.Accept(reserve,consume);
        await CapacityReservationIntegrationTests.Consume(consume);
        var fill=CapacityReservationIntegrationTests.ChangeRequest(consume,CapacityChangeKind.RecordFill,3,2,4,0,6);
        await CapacityReservationIntegrationTests.Change(fill);
        var sourceStore=new PortfolioFinancialStore(Transactions());
        var reserved=(await sourceStore.ReadOperationAsync<CapacityReservationCompletedEvent>(book.PortfolioId,reserve.OperationId))!;
        var consumed=(await sourceStore.ReadOperationAsync<CapacityConsumptionCompletedEvent>(book.PortfolioId,consume.OperationId))!;
        var filled=(await sourceStore.ReadOperationAsync<CapacityLifecycleCompletedEvent>(book.PortfolioId,fill.OperationId))!;
        var before=await CapacityState(Transactions(),book.PortfolioId);
        var database="ifm_ledger_rebuild_"+Guid.NewGuid().ToString("N");
        var builder=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")!);
        if(builder.Host is not("localhost" or "127.0.0.1") || builder.Database!="ifm_storage_refactor_test") throw new InvalidOperationException("Isolated database required.");
        builder.Database="postgres";
        await using var admin=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(builder.ConnectionString); await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE DATABASE {database}",admin)) await create.ExecuteNonQueryAsync();
        try
        {
            builder.Database=database;
            var settings=new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,builder.ConnectionString,"System.Data.Postgres");
            await new EventSourceSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
            var target=new PostgresEventTransaction(settings);await new PortfolioFinancialSchema(target).InitializeAsync();
            // Capacity-only fixture: the preceding book and funding projection are prerequisites here.
            await new PortfolioFinancialStore(target).CreateBookAsync(book,[new(101,1,"Cash",PostingSide.Debit,true,"cash"),new(102,1,"Equity",PostingSide.Credit,true,"equity")],
                [Rule(LedgerTransactionKind.DepositConfirmed)],new(2020,1,1),new(2099,12,31));
            await target.ExecuteAsync(async(db,ct)=> { await db.ExecuteAsync("UPDATE portfolio_financial.financial_authority SET financial_revision=1 WHERE portfolio_id=$1;",[book.PortfolioId],ct);return true; });
            foreach(var source in new TomasAI.IFM.Shared.EventSourcing.IEvent[] {reserved,consumed,filled})
                await target.ExecuteAsync((db,ct)=>db.AppendAsync("CapacityRebuild."+source.Id,Guid.NewGuid(),source,0,ct));
            var projection=new CapacityFinancialProjection(target);
            for(var pass=0;pass<2;pass++)
            {
                await projection.ReplayAsync(reserved);await projection.ReplayAsync(consumed);await projection.ReplayAsync(filled);
                (await CapacityState(target,book.PortfolioId)).Should().Be(before);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();await using var drop=new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }

    static Task<string> CapacityState(IPostgresEventTransaction owner,int portfolioId)=>owner.ExecuteAsync(async(db,ct)=>
    {
        var parts=new List<string>();
        foreach(var table in new[]{"capacity_reservation","capacity_usage"})
        {
            var rows=await db.QueryAsync($"SELECT (to_jsonb(t)-'created_transaction_id')::text FROM portfolio_financial.{table} t WHERE portfolio_id=$1 ORDER BY (to_jsonb(t)-'created_transaction_id')::text;",
                [portfolioId],r=>r.GetString(0),ct);parts.Add(table+":"+string.Join("|",rows));
        }
        return string.Join("\n",parts);
    });

    [Fact]
    public async Task Authority_fence_records_and_rebuilds_its_accepted_state_without_fund_commands()
    {
        var book=await CreateBook();var command=Guid.NewGuid();
        var original=new TomasAI.IFM.Domain.Portfolio.Shared.Events.PortfolioCreatedEvent(Guid.NewGuid(),command,1,DateTime.UtcNow,"integration",new());
        await new PortfolioAuthorityFence(Transactions()).AppendAsync(book.PortfolioId,null,$"Portfolio.{book.PortfolioId}",original,1,true);
        var source=(await Transactions().ExecuteAsync((db,ct)=>CapacityReservationStore.ReadEvidenceAsync<FinancialAuthoritySnapshotEvent>(db,command,ct)))!;
        source.SourceEventId.Should().Be(original.Id);source.FinancialRevision.Should().Be(1);source.AuthorityEpoch.Should().Be(1);
        var database="ifm_ledger_rebuild_"+Guid.NewGuid().ToString("N");
        var builder=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")!);
        if(builder.Host is not("localhost" or "127.0.0.1") || builder.Database!="ifm_storage_refactor_test") throw new InvalidOperationException("Isolated database required.");
        builder.Database="postgres";await using var admin=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(builder.ConnectionString);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE DATABASE {database}",admin)) await create.ExecuteNonQueryAsync();
        try
        {
            builder.Database=database;var settings=new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,builder.ConnectionString,"System.Data.Postgres");
            await new EventSourceSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();var target=new PostgresEventTransaction(settings);await new PortfolioFinancialSchema(target).InitializeAsync();
            await new PortfolioFinancialStore(target).CreateBookAsync(book,[new(101,1,"Cash",PostingSide.Debit,true,"cash"),new(102,1,"Equity",PostingSide.Credit,true,"equity")],
                [Rule(LedgerTransactionKind.DepositConfirmed)],new(2020,1,1),new(2099,12,31));
            await target.ExecuteAsync((db,ct)=>db.AppendAsync("AuthorityRebuild."+source.Id,command,source,0,ct));
            var projection=new FinancialAuthorityProjection(target);await projection.ReplayAsync(source);await projection.ReplayAsync(source);
            var state=await target.ExecuteAsync((db,ct)=>db.QueryAsync("SELECT authority_epoch,financial_revision,operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",
                [book.PortfolioId],r=>(r.GetInt64(0),r.GetInt64(1),r.GetString(2)),ct));state.Single().Should().Be((1L,1L,"NeedsRefresh"));
        }
        finally { NpgsqlConnection.ClearAllPools();await using var drop=new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Verified_baseline_rebuilds_existing_financial_facts_without_replaying_old_receipts(bool includeComposition)
    {
        var database="ifm_ledger_rebuild_"+Guid.NewGuid().ToString("N");
        var sourceDatabase="ifm_ledger_rebuild_"+Guid.NewGuid().ToString("N");
        var builder=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")!);
        if(builder.Host is not("localhost" or "127.0.0.1") || builder.Database!="ifm_storage_refactor_test") throw new InvalidOperationException("Isolated database required.");
        builder.Database="postgres";await using var admin=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(builder.ConnectionString);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE DATABASE {database}",admin)) await create.ExecuteNonQueryAsync();
        await using(var create=new NpgsqlCommand($"CREATE DATABASE {sourceDatabase}",admin)) await create.ExecuteNonQueryAsync();
        try
        {
            builder.Database=sourceDatabase;
            var sourceSettings=new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,builder.ConnectionString,"System.Data.Postgres");
            await new EventSourceSchemaDb(sourceSettings,NullLogger<DbProvider>.Instance).CreateAllAsync();var sourceOwner=new PostgresEventTransaction(sourceSettings);
            await sourceOwner.ExecuteAsync((db,ct)=>db.ExecuteAsync(TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbSql.Schema.Create,[],ct));
            var book=await PortfolioOrderCompositionIntegrationTests.CreateOrderBook(owner:sourceOwner);
            if(includeComposition) await new TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition.PortfolioOrderCompositionStore(sourceOwner)
                .EvaluateAsync(PortfolioOrderCompositionIntegrationTests.Request(book),PortfolioOrderCompositionIntegrationTests.Evaluate);
            var service=new FinancialProjectionBaseline(sourceOwner);
            await FluentActions.Awaiting(()=>service.CaptureAsync(false)).Should().ThrowAsync<ArgumentException>();
            var captured=await service.CaptureAsync(true,includeComposition:includeComposition);
            var source=(await new FinancialEventLogReader(sourceOwner).ReadLatestBaselineAsync(captured.EventId))!;
            source.Tables.Should().BeEquivalentTo(captured.Tables);source.SourceCut.Should().BeLessThan(source.EventId);
            var before=await LedgerState(book.BookId,sourceOwner);
            var laterRequest=Request(book,LedgerTransactionKind.DepositConfirmed,50,includeComposition?2:1);
            await Post(laterRequest,sourceOwner);
            var after=await LedgerState(book.BookId,sourceOwner);
            var later=new List<TomasAI.IFM.Shared.EventSourcing.IEvent>();
            var laterCut=Convert.ToInt64(await sourceOwner.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT max(eventversion) FROM event_log;",[],ct)));
            await foreach(var item in new FinancialEventLogReader(sourceOwner).ReadAsync(laterCut,new HashSet<int>{book.PortfolioId},true,afterEventId:source.EventId)) later.Add(item);
            later.Should().ContainSingle();
            builder.Database=database;var settings=new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,builder.ConnectionString,"System.Data.Postgres");
            await new EventSourceSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();var target=new PostgresEventTransaction(settings);await new PortfolioFinancialSchema(target).InitializeAsync();
            await target.ExecuteAsync((db,ct)=>db.ExecuteAsync(TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbSql.Schema.Create,[],ct));
            // The new target schema pre-registers event names. Replace only that empty target registry with source identities.
            await target.ExecuteAsync((db,ct)=>db.ExecuteAsync("DELETE FROM event_name_id WHERE NOT EXISTS(SELECT 1 FROM event_log);",[],ct));
            // Preserve the source log identities referenced by baseline receipts. No source rows are removed or overwritten.
            foreach(var table in new[]{"event_stream_id","event_name_id","event_log"})
            {
                var json=await sourceOwner.ExecuteAsync((db,ct)=>db.ScalarAsync($"SELECT COALESCE(jsonb_agg(to_jsonb(t)),'[]'::jsonb)::text FROM {table} t;",[],ct));
                await target.ExecuteAsync((db,ct)=>db.ExecuteAsync($"INSERT INTO {table} SELECT * FROM jsonb_populate_recordset(NULL::{table},$1::jsonb);",[json],ct));
            }
            var replay=new FinancialProjectionBaseline(target);
            var corrupted=source with { Tables=source.Tables.Select((x,i)=>i==0?x with { RowsJson="[]" }:x).ToArray() };
            await FluentActions.Awaiting(()=>replay.ReplayAsync(corrupted)).Should().ThrowAsync<InvalidDataException>();
            await replay.ReplayAsync(source);await replay.ReplayAsync(source);
            (await LedgerState(book.BookId,target)).Should().Be(before);
            foreach(var table in source.Tables)
            {
                var actual=await target.ExecuteAsync((db,ct)=>db.ScalarAsync($"SELECT COALESCE(jsonb_agg(j ORDER BY j::text),'[]'::jsonb)::text FROM (SELECT to_jsonb(t)-'created_transaction_id' AS j FROM {(table.Name.Contains('.')?table.Name:"portfolio_financial."+table.Name)} t) q;",[],ct));
                actual.Should().Be(table.RowsJson);
            }
            await new LedgerFinancialProjection(target).ReplayAsync((LedgerPostingCompletedEvent)later.Single());
            (await LedgerState(book.BookId,target)).Should().Be(after);
        }
        finally { NpgsqlConnection.ClearAllPools();foreach(var owned in new[]{database,sourceDatabase}) { await using var drop=new NpgsqlCommand($"DROP DATABASE {owned} WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync(); } }
    }

    [Fact]
    public async Task Receipt_only_events_are_explicitly_rejected_without_inventing_missing_financial_facts()
    {
        var old = new LedgerPostingCompletedEvent { Id = Guid.NewGuid(), EventId = 1, Receipt = new() { FinancialRevision = 1 } };
        await FluentActions.Awaiting(() => new LedgerFinancialProjection(Transactions()).ReplayAsync(old))
            .Should().ThrowAsync<InvalidDataException>();
    }

    // PostgreSQL physical transaction IDs differ across databases; all business columns must match.
    static Task<string> LedgerState(int bookId, IPostgresEventTransaction? owner = null) => (owner ?? Transactions()).ExecuteAsync(async (db, ct) =>
    {
        var parts = new List<string>();
        foreach (var table in new[] { "ledger_transaction", "ledger_journal", "ledger_entry", "ledger_account_balance", "financial_encumbrance", "financial_source_receipt", "ledger_valuation" })
        {
            var rows = await db.QueryAsync($"SELECT (to_jsonb(t)-'created_transaction_id')::text FROM portfolio_financial.{table} t WHERE book_id=$1 ORDER BY (to_jsonb(t)-'created_transaction_id')::text;",
                [bookId], r => r.GetString(0), ct);
            parts.Add(table + ":" + string.Join("|", rows));
        }
        return string.Join("\n", parts);
    });
}
