using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Securities.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Xunit.Abstractions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure", "SelfContained")]
public sealed class PostgresSecuritiesReferenceTests(PostgresTradePlanFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Reused_provider_id_preserves_expired_legacy_binding_and_stages_new_lifecycle()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(SecuritiesSchemaSql.Create,[],ct));
        var settings = new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection, fixture.Connection, "System.Data.Postgres");
        var context = new SecuritiesDbContext(settings, Mock.Of<IDbContextFactory>(), NullLogger<DbProvider>.Instance);
        var symbol = "UT" + Guid.NewGuid().ToString("N"); var instrument = (uint)Random.Shared.Next(100000,10000000);
        var old = new FuturesOptionContractReadModel(symbol+"20261008C7475", "Expired", symbol, "old", "FOP", "USD", "XCME", "50", new DateOnly(2026,10,8),7475,"Call")
        { Dataset="GLBX.MDP3",PublisherId=1,InstrumentId=instrument,StrikePriceDecimal=7475,UnderlyingContractId="ES20261218",UnderlyingInstrumentId=10252,UnderlyingPublisherId=1 };
        var oldBinding = OptionReferenceIdentity.Serialize(old); var legacy = OptionReferenceIdentity.LegacyProviderKey(old);
        var store = new PostgresSecuritiesReferenceStore();
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=> { await store.ClaimIdentityAsync(db,legacy,oldBinding,ct); return true; });
        var current = old with { ContractId=symbol+"20261207C5100",ContractMonth=new DateOnly(2026,12,7),StrikePrice=5100,StrikePriceDecimal=5100,
            DefinitionTimestampUtc=new DateTimeOffset(2026,11,8,0,0,0,TimeSpan.Zero),ExpirationUtc=new DateTimeOffset(2026,12,7,21,0,0,TimeSpan.Zero),
            DefinitionDigest=new string('a',64),RawDefinitionReference="verified-definition",SchemaVersion=1,ReviewState=ReferenceReviewState.Draft };
        var row = new CachedOptionContractDefinitionReadModel { Symbol=symbol,ExpiryDate=current.ContractMonth,UnderlyingContractId="ES20261218",ProviderRoot="ES",Definition=current };
        (await context.ResolveOptionReferenceIdentitiesAsync([row],CancellationToken.None)).Single().Definition.Should().Be(current);
        var pending = await context.StageReferenceVersionAsync(current,CancellationToken.None);
        await context.CommitReferenceVersionAsync(pending,CancellationToken.None);
        var bindings = await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=> (
            await db.ScalarAsync("SELECT binding FROM securities.securities_reference_identity WHERE identity_key=$1;",[legacy],ct),
            await db.ScalarAsync("SELECT binding FROM securities.securities_reference_identity WHERE identity_key=$1;",[OptionReferenceIdentity.ProviderKey(current)],ct)));
        bindings.Item1.Should().Be(oldBinding); bindings.Item2.Should().Be(OptionReferenceIdentity.Serialize(current));
        await FluentActions.Awaiting(()=>context.StageReferenceVersionAsync(current with { UnderlyingContractId="ES20270319" },CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(()=>context.StageReferenceVersionAsync(current with { ContractId=symbol+"20261120C5100",ContractMonth=new DateOnly(2026,11,20),DefinitionTimestampUtc=DateTimeOffset.MinValue },CancellationToken.None))
            .Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Same_expiry_options_on_different_underlyings_keep_existing_binding_and_stage_distinct_identities()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(SecuritiesSchemaSql.Create,[],ct));
        var settings=new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection,fixture.Connection,"System.Data.Postgres");
        var context=new SecuritiesDbContext(settings,Mock.Of<IDbContextFactory>(),NullLogger<DbProvider>.Instance);
        var symbol="UT"+Guid.NewGuid().ToString("N"); var expiry=new DateOnly(2026,12,18);
        var firstInstrument=(uint)Random.Shared.Next(100000,10000000);
        var id=TomasAI.IFM.Domain.MarketData.Shared.FuturesOptionContractId.Create(symbol,expiry,TomasAI.IFM.Domain.MarketData.Shared.OptionType.Call,7500);
        var original=new FuturesOptionContractReadModel(id,"December option",symbol,"raw","FOP","USD","XCME","50",expiry,7500,"Call")
        { SchemaVersion=1,ReviewState=ReferenceReviewState.Draft,Dataset="GLBX.MDP3",PublisherId=1,InstrumentId=firstInstrument,
            StrikePriceDecimal=7500,UnderlyingContractId="ES20261218",UnderlyingInstrumentId=10252,UnderlyingPublisherId=1 };
        var pending=await context.StageReferenceVersionAsync(original,CancellationToken.None);
        await context.CommitReferenceVersionAsync(pending,CancellationToken.None);
        var second=original with { InstrumentId=firstInstrument+1,UnderlyingContractId="ES20270319",UnderlyingInstrumentId=42140860 };
        var rows=new[]{new CachedOptionContractDefinitionReadModel{Symbol=symbol,ExpiryDate=expiry,UnderlyingContractId=original.UnderlyingContractId!,ProviderRoot="ES",Definition=original},
            new CachedOptionContractDefinitionReadModel{Symbol=symbol,ExpiryDate=expiry,UnderlyingContractId=second.UnderlyingContractId!,ProviderRoot="ES",Definition=second}};
        var resolved=await context.ResolveOptionReferenceIdentitiesAsync(rows,CancellationToken.None);
        resolved[0].Definition.Should().Be(original);
        resolved[1].Definition.ContractId.Should().Be(TomasAI.IFM.Domain.MarketData.Shared.FuturesOptionContractId.WithProviderIdentity(id,1,firstInstrument+1));
        var next=await context.StageReferenceVersionAsync(resolved[1].Definition,CancellationToken.None);
        await context.CommitReferenceVersionAsync(next,CancellationToken.None);
        var repeated=await context.ResolveOptionReferenceIdentitiesAsync(rows,CancellationToken.None);
        repeated.Should().BeEquivalentTo(resolved,options=>options.WithStrictOrdering());
        var binding=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT binding FROM securities.securities_reference_identity WHERE identity_key=$1;",["ifm:"+id],ct));
        binding.Should().Be(OptionReferenceIdentity.Serialize(original));
        await FluentActions.Awaiting(()=>context.StageReferenceVersionAsync(original with{UnderlyingContractId="ES20270319"},CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*identity collision*IdentityKey=*");
    }

    [Fact]
    public async Task Immutable_bundle_context_reads_saved_payload_and_refuses_conflicting_content()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(SecuritiesSchemaSql.Create,[],ct));
        var settings=new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection,fixture.Connection,"System.Data.Postgres");
        var context=new SecuritiesDbContext(settings,Mock.Of<IDbContextFactory>(),NullLogger<DbProvider>.Instance);
        var id="bundle-"+Guid.NewGuid().ToString("N");
        await context.InsertOptionPricingReferenceBundleAsync(id,"{\"accepted\":true}");
        await context.InsertOptionPricingReferenceBundleAsync(id,"{\"accepted\":true}");
        await FluentActions.Awaiting(()=>context.InsertOptionPricingReferenceBundleAsync(id,"different")).Should().ThrowAsync<InvalidOperationException>();
        (await context.GetOptionPricingReferenceBundlePayloadAsync(id)).Should().Be("{\"accepted\":true}");
        (await context.GetOptionPricingReferenceBundlePayloadAsync("missing-"+id)).Should().BeNull();
    }

    [Fact]
    public async Task Conflicting_stage_rolls_back_both_identity_claims_and_exact_stage_preserves_publication()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(SecuritiesSchemaSql.Create,[],ct));
        var store=new PostgresSecuritiesReferenceStore();
        var id=Guid.NewGuid().ToString("N");var provider="provider:"+id;
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await store.StageAsync(db,id,provider,"binding","v1","digest",[1,2,3],ct);return true;});
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await store.PublishAsync(db,id,"v1","digest",ct);return true;});
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await store.StageAsync(db,id,provider,"binding","v1","digest",[1,2,3],ct);return true;});
        var published=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT published FROM securities.securities_reference_version WHERE contract_id=$1 AND version='v1';",[id],ct));
        published.Should().Be(true);
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await store.StageAsync(db,id,provider+"new","binding","v1","changed",[4],ct);return true;})).Should().ThrowAsync<InvalidOperationException>();
        var leaked=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM securities.securities_reference_identity WHERE identity_key=$1;",[provider+"new"],ct));
        Convert.ToInt64(leaked).Should().Be(0);
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await store.PublishAsync(db,id,"v1","wrong",ct);return true;})).Should().ThrowAsync<InvalidOperationException>();
        var other=id+"other";
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await store.StageAsync(db,other,provider,"different","v1","digest",[1],ct);return true;})).Should().ThrowAsync<InvalidOperationException>();
        leaked=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM securities.securities_reference_identity WHERE identity_key=$1;",["ifm:"+other],ct));
        Convert.ToInt64(leaked).Should().Be(0);
    }
    [Fact]
    public async Task Published_definitions_are_available_offline_and_failed_refresh_preserves_previous_generation()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(SecuritiesSchemaSql.Create,[],ct));
        var settings=new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory=new Mock<IDbContextFactory>();
        var context=new SecuritiesDbContext(settings,factory.Object,NullLogger<DbProvider>.Instance);
        factory.SetupGet(x=>x.SecuritiesDb).Returns(context);
        var symbol="TEST"+Guid.NewGuid().ToString("N");var expiry=new DateOnly(2026,11,20);
        var definition=new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel(
            "test-contract","Saved option",symbol,"local","FOP","USD","CME","50",expiry,6000,"Put");
        var row=new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.CachedOptionContractDefinitionReadModel
        {
            Symbol=symbol,UnderlyingContractId="underlying",ExpiryDate=expiry,ProviderRoot="ew",OptionFamily="weekly",
            Definition=definition,RefreshedAtUtc=DateTime.UtcNow
        };
        await new SecuritiesOptionDefinitionPublisher(context.DbWriter).PublishAsync(symbol,expiry.AddDays(-7),expiry.AddDays(7),[row]);
        var expiries=await context.GetOptionContractExpiriesAsync(
            new OptionDefinitionExpiryRange(symbol, expiry, expiry));
        expiries.Should().ContainSingle();expiries[0].ContractId.Should().Be("underlying");
        var reader = new SecuritiesOptionDefinitionReader(context);
        var rows=await reader.GetOptionDefinitionsAsync(symbol,"underlying",expiry,["EW"]);
        rows.Should().ContainSingle();rows[0].Definition.ContractId.Should().Be(definition.ContractId);
        var store=new PostgresSecuritiesReferenceStore();var generation=Guid.NewGuid();
        var failedRows = DefinitionRows("failed", expiry, 128);
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(db,ct)=>
        {
            await store.PublishDefinitionsAsync(db,symbol,generation,expiry,expiry,DateTime.UtcNow,failedRows,ct);
            throw new InvalidOperationException("Injected definition refresh failure");
        })).Should().ThrowAsync<InvalidOperationException>();
        Assert.Equal(0, await CountDefinitionRowsAsync(symbol, generation));
        Assert.Equal(definition.ContractId, Assert.Single(await reader.GetOptionDefinitionsAsync(symbol, "underlying", expiry)).Definition.ContractId);

        failedRows[^1] = failedRows[0];
        var duplicate = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            await store.PublishDefinitionsAsync(db, symbol, generation, expiry, expiry, DateTime.UtcNow, failedRows, ct);
            return true;
        }));
        Assert.Equal(Npgsql.PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        Assert.Equal(0, await CountDefinitionRowsAsync(symbol, generation));
        Assert.Equal(definition.ContractId, Assert.Single(await reader.GetOptionDefinitionsAsync(symbol, "underlying", expiry)).Definition.ContractId);

        await new SecuritiesOptionDefinitionPublisher(context.DbWriter).PublishAsync(symbol,expiry,expiry,[]);
        (await reader.GetOptionDefinitionsAsync(symbol,"underlying",expiry)).Should().BeEmpty();
    }

    [Fact]
    public async Task Concurrent_definition_publishers_wait_for_the_symbol_lock_and_commit_complete_generations()
    {
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(SecuritiesSchemaSql.Create, [], ct));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var symbol = "CONCURRENT" + Guid.NewGuid().ToString("N");
        var expiry = new DateOnly(2026, 11, 20);
        var previous = Guid.NewGuid();
        var firstGeneration = Guid.NewGuid();
        var secondGeneration = Guid.NewGuid();
        var store = new PostgresSecuritiesReferenceStore();
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            await store.PublishDefinitionsAsync(db, symbol, previous, expiry, expiry, DateTime.UtcNow, DefinitionRows("previous", expiry, 1), ct);
            return true;
        }, deadline.Token);

        var firstStaged = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            var pid = (int)(await db.ScalarAsync("SELECT pg_backend_pid()", [], ct))!;
            await store.PublishDefinitionsAsync(db, symbol, firstGeneration, expiry, expiry, DateTime.UtcNow, DefinitionRows("first", expiry, 128), ct);
            firstStaged.SetResult(pid);
            await allowFirstCommit.Task.WaitAsync(ct);
            return true;
        }, deadline.Token);
        Task? second = null;
        try
        {
            var firstPid = await firstStaged.Task.WaitAsync(deadline.Token);
            second = fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
            {
                secondStarted.SetResult((int)(await db.ScalarAsync("SELECT pg_backend_pid()", [], ct))!);
                await store.PublishDefinitionsAsync(db, symbol, secondGeneration, expiry, expiry, DateTime.UtcNow, DefinitionRows("second", expiry, 64), ct);
                return true;
            }, deadline.Token);
            var secondPid = await secondStarted.Task.WaitAsync(deadline.Token);
            while (!await fixture.Transactions.ExecuteOnceAsync(async (db, ct) => await db.ScalarAsync(
                "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid=$2 AND locktype='advisory' AND NOT granted AND $1=ANY(pg_blocking_pids(pid)))",
                [firstPid, secondPid], ct) is true, deadline.Token))
                await Task.Delay(20, deadline.Token);

            Assert.Equal(previous, await CurrentGenerationAsync());
            Assert.Equal(0, await CountDefinitionRowsAsync(symbol, firstGeneration));
            Assert.Equal(0, await CountDefinitionRowsAsync(symbol, secondGeneration));
        }
        finally
        {
            allowFirstCommit.TrySetResult();
            await Task.WhenAll(first, second ?? Task.CompletedTask);
        }

        Assert.Equal(secondGeneration, await CurrentGenerationAsync());
        Assert.Equal(1, await CountDefinitionRowsAsync(symbol, previous));
        Assert.Equal(128, await CountDefinitionRowsAsync(symbol, firstGeneration));
        Assert.Equal(64, await CountDefinitionRowsAsync(symbol, secondGeneration));

        Task<Guid> CurrentGenerationAsync() => fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
            (Guid)(await db.ScalarAsync("SELECT generation FROM securities.option_contract_expiry_calendar_state WHERE symbol=$1", [symbol], ct))!, deadline.Token);
    }

    Task<long> CountDefinitionRowsAsync(string symbol, Guid generation) => fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        (long)(await db.ScalarAsync("SELECT count(*) FROM securities.option_contract_expiry_calendar WHERE symbol=$1 AND generation=$2",
            [symbol, generation], ct))!);

    static (DateOnly Expiry, string Root, string ContractId, string UnderlyingId, string Family, byte[] Payload)[] DefinitionRows(
        string prefix, DateOnly expiry, int count) => Enumerable.Range(0, count)
        .Select(index => (expiry, "EW", $"{prefix}-{index}", "underlying", "weekly", new byte[] { (byte)index }))
        .ToArray();

    [Fact]
    public async Task Batched_definitions_match_serial_order_and_pin_the_generation_across_refresh()
    {
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(SecuritiesSchemaSql.Create, [], ct));
        var settings = new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection, fixture.Connection, "System.Data.Postgres");
        var symbol = "BATCH" + Guid.NewGuid().ToString("N");
        var first = new DateOnly(2026, 11, 20);
        const int expiryCount = 16;
        const int definitionsPerExpiry = 12;
        var last = first.AddDays(expiryCount - 2); // Include the nearest published expiry after the requested range.
        var rows = Enumerable.Range(0, expiryCount).SelectMany(day =>
            Enumerable.Range(0, definitionsPerExpiry).Select(strike =>
            {
                var expiry = first.AddDays(day);
                var definition = new FuturesOptionContractReadModel($"option-{day}-{strike}", "Saved option", symbol,
                    "local", "FOP", "USD", "CME", "50", expiry, 6000 + strike, "Put");
                return new CachedOptionContractDefinitionReadModel
                {
                    Symbol = symbol, UnderlyingContractId = strike % 3 == 0 ? "future-a" : "future-b",
                    ExpiryDate = expiry, ProviderRoot = strike % 2 == 0 ? "EW" : "ES", OptionFamily = "weekly",
                    Definition = definition, RefreshedAtUtc = DateTime.UtcNow
                };
            })).ToArray();
        var setup = Open();
        await new SecuritiesOptionDefinitionPublisher(setup.DbWriter).PublishAsync(symbol, first, first.AddDays(expiryCount - 1), rows);
        var expected = await SerialAsync(Open(), null);
        Assert.Equal(rows.Length, expected.Count);
        Assert.Equal(expected, await BatchedAsync(Open(), null));
        Assert.Equal(await SerialAsync(Open(), ["ew"]), await BatchedAsync(Open(), ["ew"]));

        var timings = new List<object>();
        var warmBefore = Open();
        var warmAfter = Open();
        await SerialAsync(warmBefore, null);
        await BatchedAsync(warmAfter, null);
        for (var pair = 0; pair < 5; pair++)
        {
            var coldBefore = Open();
            var coldAfter = Open();
            foreach (var (mode, before, after) in new[]
                     { ("cold", coldBefore, coldAfter), ("warm", warmBefore, warmAfter) })
            {
                var reversed = pair % 2 == 1;
                var firstMs = await TimeAsync(reversed ? () => BatchedAsync(after, null) : () => SerialAsync(before, null));
                var secondMs = await TimeAsync(reversed ? () => SerialAsync(before, null) : () => BatchedAsync(after, null));
                timings.Add(new { mode, beforeMs = reversed ? secondMs : firstMs, afterMs = reversed ? firstMs : secondMs });
            }
        }
        output.WriteLine("reference-definition-timings " + JsonSerializer.Serialize(timings));

        await using (var inFlight = Open().ReadPublishedOptionDefinitionsAsync(new OptionDefinitionExpiryRange(symbol, first, last, 2048)).GetAsyncEnumerator())
        {
            Assert.True(await inFlight.MoveNextAsync());
            var oldGeneration = new List<string> { inFlight.Current.Definition.ContractId };
            await new SecuritiesOptionDefinitionPublisher(setup.DbWriter).PublishAsync(symbol, first, first.AddDays(expiryCount - 1),
                [rows[0] with { Definition = rows[0].Definition with { ContractId = "replacement" } }]);
            while (await inFlight.MoveNextAsync())
                oldGeneration.Add(inFlight.Current.Definition.ContractId);
            Assert.Equal(expected, oldGeneration);
        }
        Assert.Equal(["replacement"], await BatchedAsync(warmAfter, null));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var row in Open().ReadPublishedOptionDefinitionsAsync(new OptionDefinitionExpiryRange(symbol, first, last, 2048), cancellationToken: cancellation.Token))
                _ = row;
        });

        SecuritiesDbContext Open()
        {
            var factory = new Mock<IDbContextFactory>();
            var context = new SecuritiesDbContext(settings, factory.Object, NullLogger<DbProvider>.Instance);
            factory.SetupGet(x => x.SecuritiesDb).Returns(context);
            return context;
        }
        async Task<List<string>> SerialAsync(SecuritiesDbContext db, string[]? roots)
        {
            var result = new List<string>();
            var expiries = await db.GetOptionContractExpiriesAsync(
                new OptionDefinitionExpiryRange(symbol, first, last));
            foreach (var (expiry, underlying) in expiries.Select(x => (x.ExpiryDate, x.ContractId)).Distinct())
                result.AddRange((await db.GetCachedOptionContractDefinitionsAsync(new OptionDefinitionPartition(symbol, underlying, expiry), roots))
                    .Select(row => row.Definition.ContractId));
            return result;
        }
        async Task<List<string>> BatchedAsync(SecuritiesDbContext db, string[]? roots)
        {
            var result = new List<string>();
            await foreach (var row in db.ReadPublishedOptionDefinitionsAsync(new OptionDefinitionExpiryRange(symbol, first, last, 2048), roots))
                result.Add(row.Definition.ContractId);
            return result;
        }
        async Task<double> TimeAsync(Func<Task<List<string>>> run)
        {
            var start = Stopwatch.GetTimestamp();
            var actual = await run();
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            Assert.Equal(expected, actual);
            return elapsed;
        }
    }

    [Fact]
    public async Task Contract_source_canonical_and_symbol_projection_commit_and_roll_back_together()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(SecuritiesSchemaSql.Create,[],ct));
        var settings=new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory=new Mock<IDbContextFactory>();
        var context=new SecuritiesDbContext(settings,factory.Object,NullLogger<DbProvider>.Instance);
        factory.SetupGet(value=>value.SecuritiesDb).Returns(context);
        var strike=Random.Shared.Next(10_000,20_000);
        var id="ES20261120P"+strike;
        var contract=new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel(
            id,"Accepted option","ATOMIC","local","FOP","USD","CME","50",new DateOnly(2026,11,20),strike,"Put");
        var commandId=Guid.NewGuid();
        var source=new TomasAI.IFM.Domain.MarketData.Shared.Events.FuturesOptionContractAddedEvent
        { CommandId=commandId,Contract=contract,CreatedOn=DateTime.UtcNow,CreatedBy="test" };
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await db.AppendAsync(new EnlistedEventAppend(id,commandId,source,0),ct);
            await context.ProjectOptionContractAsync(db,contract,token:ct);
            return true;
        });
        (await context.GetFuturesOptionContractAsync(id)).Should().BeEquivalentTo(contract);
        var failedId="ES20261120P"+(strike+1);
        var failed=source with {CommandId=Guid.NewGuid(),Contract=contract with {ContractId=failedId,StrikePrice=strike+1}};
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(db,ct)=>
        {
            await db.AppendAsync(new EnlistedEventAppend(failedId,failed.CommandId,failed,0),ct);
            await context.ProjectOptionContractAsync(db,failed.Contract,token:ct);
            throw new InvalidOperationException("Failure after source and both projections");
        })).Should().ThrowAsync<InvalidOperationException>();
        (await context.GetFuturesOptionContractAsync(failedId)).Should().BeNull();
        var counts=await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            var canonical=await db.ScalarAsync("SELECT count(*) FROM securities.futures_option_contract_by_symbol WHERE contractId=$1;",[failedId],ct);
            var events=await db.ScalarAsync("SELECT count(*) FROM public.event_log e JOIN public.event_stream_id s ON s.EventStreamId=e.EventStreamId WHERE s.EventStream=$1;",[failedId],ct);
            return (Convert.ToInt64(canonical),Convert.ToInt64(events));
        });
        counts.Should().Be((0L,0L));
        // Reconstruction uses saved accepted facts and never performs broker enrichment.
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await context.RemoveOptionContractAsync(db,id,ct);
            await context.ProjectOptionContractAsync(db,source.Contract,token:ct);
            return true;
        });
        (await context.GetFuturesOptionContractAsync(id)).Should().BeEquivalentTo(contract);
    }

}
