using TomasAI.IFM.Application.EventSourcing.Projections.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresAcceptedSignalTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Vx_checkpoint_only_event_commits_and_replays_without_inserting_or_erasing_a_paired_signal()
    {
        var settings = new DbConnectionSettings().Add("MarketDataDbConnection",fixture.Connection,"System.Data.Postgres");
        await new MarketDataSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var key = "vx-checkpoint-"+Guid.NewGuid().ToString("N");
        var date = new DateOnly(2026,10,13);var now = new DateTimeOffset(2026,10,13,12,0,0,TimeSpan.Zero);
        var checkpoint = new FuturesVxTermStructureSignalUpdatedEvent { CommandId=Guid.NewGuid(),FuturesVxTermStructureCheckpoint=new() {Front=new(){ContractId=key,Expiry=date.AddDays(20),Price=20.25m,SourceSequence=1,SourceTimestampUtc=now,StreamEpochId=Guid.NewGuid()}} };
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await db.AppendAsync(key,checkpoint.CommandId,checkpoint,0,ct);
            Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,checkpoint,ct));return true;
        });
        var persisted=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT n.EventTypeName,l.EventVersion,l.EventPayload FROM event_log l JOIN event_name_id n ON n.EventNameId=l.EventNameId JOIN event_stream_id s ON s.EventStreamId=l.EventStreamId WHERE s.EventStream=$1 ORDER BY l.EventVersion;",[key],reader=>new EventStreamReadModel{EventTypeName=reader.GetString(0),EventVersion=reader.GetInt64(1),EventData=reader.GetFieldValue<byte[]>(2)}.ToDomainEvent(),ct));
        var restored=Assert.IsType<FuturesVxTermStructureSignalUpdatedEvent>(Assert.Single(persisted));
        Assert.Null(restored.FuturesVxTermStructureSignal);Assert.Equal(checkpoint.FuturesVxTermStructureCheckpoint,restored.FuturesVxTermStructureCheckpoint);
        async Task<long> CountAsync()=>Convert.ToInt64(await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM market_data.futures_vx_term_structure_signal WHERE configurationId=$1",[key],ct)));
        Assert.Equal(0,await CountAsync());
        var paired=checkpoint with{CommandId=Guid.NewGuid(),FuturesVxTermStructureSignal=new(){ValueDate=date,ConfigurationId=key,FrontVxContractId=key,BackVxContractId=key+"back",FrontExpiry=date.AddDays(20),BackExpiry=date.AddDays(50),FrontVxPrice=20.25m,BackVxPrice=21.5m,FrontSourceTimestampUtc=now,BackSourceTimestampUtc=now,CalculatedAtUtc=now,IsValid=true}};
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await db.AppendAsync(key,paired.CommandId,paired,1,ct);Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,paired,ct));
            Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,restored,ct));return true;
        });
        Assert.Equal(1,await CountAsync());
    }

    [Fact]
    public async Task Postgres_committed_stream_version_includes_non_snapshot_source_events()
    {
        var stream = "filtered-stream-metadata-" + Guid.NewGuid().ToString("N");
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.AppendAsync(stream, Guid.NewGuid(), new FuturesRsiSignalStartedEvent(), 0, token);
            await transaction.AppendAsync(stream, Guid.NewGuid(), new FuturesRsiSignalStartedEvent(), 1, token);
            return true;
        });
        var id = Convert.ToInt64(await fixture.Transactions.ExecuteOnceAsync((transaction, token) =>
            transaction.ScalarAsync("SELECT EventStreamId FROM event_stream_id WHERE EventStream=$1", [stream], token)));
        await using var context = new EventSourceActorDbContext(fixture.Settings,
            NSubstitute.Substitute.For<IDbContextFactory>(), NSubstitute.Substitute.For<TomasAI.IFM.Application.Blackboard.IBlackboardService>(),
            NullLogger<DbProvider>.Instance);
        Assert.Equal(2, await context.GetCommittedStreamVersionAsync(id));
    }

    [Fact]
    public async Task Startup_can_initialize_named_option_chain_tables_without_replacing_existing_policy_rows()
    {
        var settings = new DbConnectionSettings().Add("MarketDataDbConnection", fixture.Connection, "System.Data.Postgres");
        var schema = new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance);
        string[] names = ["strategy_option_chain_parameter_version", "strategy_option_chain_parameter_current"];
        await schema.CreateAsync(names);
        var id = Guid.NewGuid();
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.ExecuteAsync("INSERT INTO market_data.strategy_option_chain_parameter_version (set_id,version,payload) VALUES ($1,1,$2)", [id,"named-schema-fixture"], token);
            return true;
        });
        try
        {
            await schema.CreateAsync(names);
            var payload = await fixture.Transactions.ExecuteOnceAsync((transaction, token) =>
                transaction.ScalarAsync("SELECT payload FROM market_data.strategy_option_chain_parameter_version WHERE set_id=$1 AND version=1", [id], token));
            Assert.Equal("named-schema-fixture", payload);
        }
        finally
        {
            await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
            {
                await transaction.ExecuteAsync("DELETE FROM market_data.strategy_option_chain_parameter_version WHERE set_id=$1 AND version=1", [id], token);
                return true;
            });
        }
    }

    [Fact]
    public async Task Postgres_option_chain_policy_publication_roundtrips_and_rejects_stale_revision()
    {
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection, fixture.Connection, "System.Data.Postgres");
        await new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAsync(["strategy_option_chain_parameter_version", "strategy_option_chain_parameter_current"]);
        var db = MarketDataDbContextTestFactory.Create(settings[MarketDataDbContext.MarketDataDbConnection]);
        var policy = TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterDefaults.IronCondor(Guid.NewGuid(), Guid.NewGuid(), 1) with { Enabled = true };
        try
        {
            await db.ProjectAsync(policy, 2, true, default);
            Assert.Equal(policy.Hash(), (await db.ReadVersionAsync(policy.ParameterSetId, 1, default))!.Hash());
            Assert.Contains(await db.ReadPublishedAsync("Development", default), value => value.ParameterSetId == policy.ParameterSetId);
            await db.ProjectAsync(policy, 3, false, default);
            await db.ProjectAsync(policy, 2, true, default);
            Assert.DoesNotContain(await db.ReadPublishedAsync("Development", default), value => value.ParameterSetId == policy.ParameterSetId);
        }
        finally
        {
            await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
            {
                await transaction.ExecuteAsync("DELETE FROM market_data.strategy_option_chain_parameter_current WHERE set_id=$1", [policy.ParameterSetId], token);
                await transaction.ExecuteAsync("DELETE FROM market_data.strategy_option_chain_parameter_version WHERE set_id=$1", [policy.ParameterSetId], token);
                return true;
            });
        }
    }

    [Fact]
    public async Task Accepted_analytic_sources_and_rows_are_atomic_and_reconstruct_identically_without_calculators()
    {
        var settings=new DbConnectionSettings().Add("MarketDataDbConnection",fixture.Connection,"System.Data.Postgres");
        await new MarketDataSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var key="accepted-"+Guid.NewGuid().ToString("N");
        var date=new DateOnly(2026,10,11);
        var time=date.ToDateTime(new TimeOnly(12,0),DateTimeKind.Utc);
        var asOf=new DateTimeOffset(time);
        var series=MarketSeriesIdentity.ForFuturesSeries(new FuturesSeriesId(key,"calendar-front","unadjusted",1));
        MarketAnalyticsSignalMetadata Metadata(MarketAnalyticsSignalKind kind)=>new()
        {
            SignalKey=new(series,kind,TimeFrameType.Daily,key),ContractId=key,ValueDate=date,
            ObservationId=new FuturesTradeSessionBarId(Guid.NewGuid()),MarketDataAsOfUtc=asOf,CalculatedAtUtc=asOf,
            SourceSequence=901,SchemaVersion=1,CalculationVersion="integration-v1",
            CalculationMethod=MarketSignalCalculationMethod.NormalizedHistoricalAggregate,IsValid=true
        };
        IEvent[] sources=[
            new FuturesBarDataInsertedEvent {CommandId=Guid.NewGuid(),FuturesBarData=new(){ContractId=key,Symbol="ES",ValueDate=date,BarDate=time,BarRateType=BarRateType.Minute,BarValue=6000.125m,UpTrendTrigger=10.25,DownTrendTrigger=-10.25}},
            new VixFuturesEodDataInsertedEvent {CommandId=Guid.NewGuid(),VixFuturesTickData=new(){ContractId=key,ValueDate=date,Price=20.125m,Size=5}},
            new FuturesEodDataInsertedEvent {CommandId=Guid.NewGuid(),FuturesEodData=new(){ContractId=key,ValueDate=date.AddDays(1)},RawObservation=new(){ContractId=key,ValueDate=date.AddDays(1),MarketSeriesIdentity=series,SessionStartUtc=asOf.AddDays(1),SessionEndUtc=asOf.AddDays(1).AddHours(7),Open=6000.125m,High=6010.125m,Low=5990.125m,Close=6005.125m,Volume=20m,TradeCount=2,PriceVolumeSum=120002.5m,ObservationId=new(Guid.NewGuid()),FirstSourceSequence=1,LastSourceSequence=2,FirstMarketEventUtc=asOf.AddDays(1),LastMarketEventUtc=asOf.AddDays(1).AddHours(6),SchemaVersion=1,IsComplete=true,IsValid=true}},
            new FuturesTradeSessionBarPublishedEvent {CommandId=Guid.NewGuid(),Bar=new(){ContractId=key,ValueDate=date,MarketSeriesIdentity=series,TimeFrame=TimeFrameType.Daily,IntervalStartUtc=asOf,IntervalEndUtc=asOf.AddHours(7),ObservationId=new(Guid.NewGuid()),Open=6000.125m,High=6010.125m,Low=5990.125m,Close=6005.125m,Volume=20m,TradeCount=2,PriceVolumeSum=120002.5m,FirstSourceSequence=1,LastSourceSequence=2,FirstMarketEventUtc=asOf,LastMarketEventUtc=asOf.AddHours(6),CalculatedAtUtc=asOf.AddHours(7),SchemaVersion=1,CalculationVersion="integration-v1",IsComplete=true,IsValid=true}},
            new FuturesClosingPriceInsertedEvent {CommandId=Guid.NewGuid(),FuturesClosingPriceId=new(key,date),ClosingPrice=6000.125m,CreatedOn=time,CreatedBy="integration"},
            new MarketOutlookSnapshotInsertedEvent {CommandId=Guid.NewGuid(),MarketOutlook=new(){ContractId=key,ValueDate=date,UpdatedAtUtc=time,MarketDataAsOfUtc=time,MissingInputs="accepted integration inputs",FuturesTradeSignal=SampleData.FuturesTradeSignal with {ContractId=key,ValueDate=date}}},
            new FuturesItiSignalGeneratedEvent {CommandId=Guid.NewGuid(),FuturesItiSignal=SampleData.FuturesItiSignal1 with {ContractId=key,ValueDate=date,SequenceId=0,IntrinsicTime=time}},
            new FuturesTradeSignalUpdatedEvent {CommandId=Guid.NewGuid(),FuturesTradeSignal=SampleData.FuturesTradeSignal with {ContractId=key,ValueDate=date}},
            new FuturesAdxSignalGeneratedEvent {CommandId=Guid.NewGuid(),FuturesAdxSignal=new(){ContractId=key,ValueDate=date,TimePeriod=TimeFrameType.Daily,PeriodLength=14,Timestamp=new(12,0),FuturesPrice=6000.125m,AdxValue=30.25}},
            new FuturesAtrSignalGeneratedEvent {CommandId=Guid.NewGuid(),FuturesAtrSignal=new(){ContractId=key,ValueDate=date,TimePeriod=TimeFrameType.Daily,PeriodLength=14,Timestamp=new(12,0),FuturesPrice=6000.125m,AtrValue=14.25}},
            new FuturesMacdSignalGeneratedEvent {CommandId=Guid.NewGuid(),FuturesMacdSignal=new(){ContractId=key,ValueDate=date,TimePeriod=TimeFrameType.Daily,SignalEmaPeriod=9,FastEmaPeriod=12,SlowEmaPeriod=26,Timestamp=new(12,0),FuturesPrice=6000.125m,MacdLine=10.25}},
            new FuturesRsiSignalGeneratedEvent {CommandId=Guid.NewGuid(),FuturesRsiSignal=SampleData.FuturesRsiSignal with {ContractId=key,ValueDate=date,Timestamp=new(12,0),SourceEventTimestamp=time}},
            new FuturesTdiSignalGeneratedEvent {CommandId=Guid.NewGuid(),FuturesTdiSignal=SampleData.FuturesTdiSignal with {ContractId=key,ValueDate=date,Timestamp=new(12,0),SourceEventTimestamp=time}},
            new FuturesEmaSignalGeneratedEvent {CommandId=Guid.NewGuid(),FuturesEmaSignal=new(){Metadata=Metadata(MarketAnalyticsSignalKind.Ema),Price=6100.125m,Ema10=6090m,IsWarm=true}},
            new FuturesBbSignalGeneratedEvent {CommandId=Guid.NewGuid(),FuturesBbSignal=new(){Metadata=Metadata(MarketAnalyticsSignalKind.BollingerBand),Price=6100m,Ema10Center=6090m,Width10=40m,IsWarm=true}},
            new FuturesVwapSignalUpdatedEvent {CommandId=Guid.NewGuid(),FuturesVwapSignal=new(){ContractId=key,ValueDate=date,ConfigurationId=key,SessionStartUtc=asOf,SessionEndUtc=asOf.AddHours(7),AsOfUtc=asOf,LastTradeOrdinal=1,CumulativePriceVolume=12000.25m,CumulativeVolume=2,LastPrice=6000.125m,Vwap=6000.125m,StreamEpochId=Guid.NewGuid(),IsValid=true}},
            new FuturesVxTermStructureSignalUpdatedEvent {CommandId=Guid.NewGuid(),FuturesVxTermStructureSignal=new(){ValueDate=date,ConfigurationId=key,FrontVxContractId=key,BackVxContractId=key+"back",FrontExpiry=date.AddDays(20),BackExpiry=date.AddDays(50),FrontVxPrice=20.25m,BackVxPrice=21.5m,FrontSourceTimestampUtc=asOf,BackSourceTimestampUtc=asOf,CalculatedAtUtc=asOf,IsValid=true}}
        ];
        var tables=new[]{"futures_bar_data","vix_futures_eod_data","futures_trade_session_bar","futures_eod_observation","futures_closing_price","market_outlook_snapshot","futures_iti_signal","futures_iti_timeframe_state","futures_trade_signal","futures_trade_signal_lookup_by_scope","futures_adx_signal","futures_atr_signal","futures_macd_signal","futures_rsi_signal","futures_traders_dynamic_index_signal","futures_ema_signal","futures_bollinger_band_signal","futures_vwap_signal","futures_vx_term_structure_signal"};
        async Task<IReadOnlyList<string>> RowsAsync(string table)=>await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT row_to_json(t)::text FROM market_data."+table+" t WHERE "+(table=="futures_vx_term_structure_signal"?"configurationId":"contractId")+"=$1 ORDER BY row_to_json(t)::text;",[key],reader=>reader.GetString(0),ct));
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(db,ct)=>
        {
            foreach(var source in sources)
            {
                var accepted = source is VixFuturesEodDataInsertedEvent vix
                    ? await AcceptedMarketSignalProjection.AcceptVixAsync(db,vix,ct) : source;
                await db.AppendAsync(key+source.GetType().Name,source.CommandId,accepted,0,ct);
                Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,accepted,ct));
            }
            throw new InvalidOperationException("Injected failure after every analytic materialization");
        })).Should().ThrowAsync<InvalidOperationException>();
        foreach(var table in tables) (await RowsAsync(table)).Should().BeEmpty();
        foreach(var source in sources) (await fixture.EventCountAsync(key+source.GetType().Name)).Should().Be(0);
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            foreach(var source in sources)
            {
                var accepted = source is VixFuturesEodDataInsertedEvent vix
                    ? await AcceptedMarketSignalProjection.AcceptVixAsync(db,vix,ct) : source;
                await db.AppendAsync(key+source.GetType().Name,source.CommandId,accepted,0,ct);
                Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,accepted,ct));
            }
            return true;
        });
        var originals=new Dictionary<string,string>();
        foreach(var table in tables)
        {
            var rows=await RowsAsync(table);
            rows.Should().HaveCount(table is "futures_trade_signal_lookup_by_scope" or "futures_eod_observation"?2:1,"accepted source must materialize table {0}",table);
            originals.Add(table,string.Join("\n",rows));
        }
        var persisted=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT n.EventTypeName,l.EventVersion,l.EventPayload FROM event_log l JOIN event_name_id n ON n.EventNameId=l.EventNameId JOIN event_stream_id s ON s.EventStreamId=l.EventStreamId WHERE s.EventStream LIKE $1 ORDER BY l.EventVersion;",[key+"%"],reader=>new EventStreamReadModel{EventTypeName=reader.GetString(0),EventVersion=reader.GetInt64(1),EventData=reader.GetFieldValue<byte[]>(2)}.ToDomainEvent(),ct));
        persisted.Should().HaveCount(17);
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            foreach(var table in tables)
                await db.ExecuteAsync("DELETE FROM market_data."+table+" WHERE "+(table=="futures_vx_term_structure_signal"?"configurationId":"contractId")+"=$1;",[key],ct);
            foreach(var source in persisted) Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,source,ct));
            // Repeated materialization must not reapply VX tick volume or mutate accepted row facts.
            foreach(var source in persisted) Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,source,ct));
            return true;
        });
        foreach(var table in tables) string.Join("\n",await RowsAsync(table)).Should().Be(originals[table]);
    }
    [Fact]
    public async Task Live_feed_state_commits_with_source_and_rebuilds_without_starting_subscriptions()
    {
        var settings = new DbConnectionSettings().Add("MarketDataDbConnection",fixture.Connection,"System.Data.Postgres");
        await new MarketDataSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var key = "accepted-live-"+Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(10000000,int.MaxValue);
        var on = new TradeLiveFeedTurnedOnEvent { CommandId=Guid.NewGuid(),OrderId=id,TradeId=id };
        var off = new TradeLiveFeedTurnedOffEvent { CommandId=Guid.NewGuid(),OrderId=id,TradeId=id };
        async Task<long> RowsAsync() => Convert.ToInt64(await fixture.Transactions.ExecuteOnceAsync((db,ct) =>
            db.ScalarAsync("SELECT count(*) FROM market_data.trade_live_feed WHERE orderId=$1 AND tradeId=$1;",[id],ct)));
        async Task PersistAsync(IEvent source,long version,bool fail) => await fixture.Transactions.ExecuteOnceAsync(async(db,ct) =>
        {
            await db.AppendAsync(key,source.CommandId,source,version,ct);
            Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,source,ct));
            if (fail) throw new InvalidOperationException("Injected live-feed materialization failure");
            return true;
        });
        await FluentActions.Awaiting(() => PersistAsync(on,0,true)).Should().ThrowAsync<InvalidOperationException>();
        (await RowsAsync()).Should().Be(0);
        (await fixture.EventCountAsync(key)).Should().Be(0);
        await PersistAsync(on,0,false);
        (await RowsAsync()).Should().Be(1);
        await FluentActions.Awaiting(() => PersistAsync(off,1,true)).Should().ThrowAsync<InvalidOperationException>();
        (await RowsAsync()).Should().Be(1);
        (await fixture.EventCountAsync(key)).Should().Be(1);
        await PersistAsync(off,1,false);
        (await RowsAsync()).Should().Be(0);
        var persisted = await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync(
            "SELECT n.EventTypeName,l.EventVersion,l.EventPayload FROM event_log l JOIN event_name_id n ON n.EventNameId=l.EventNameId JOIN event_stream_id s ON s.EventStreamId=l.EventStreamId WHERE s.EventStream=$1 ORDER BY l.EventVersion;",
            [key],reader=>new EventStreamReadModel {EventTypeName=reader.GetString(0),EventVersion=reader.GetInt64(1),EventData=reader.GetFieldValue<byte[]>(2)}.ToDomainEvent(),ct));
        persisted.Should().HaveCount(2);
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct) =>
        {
            Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,persisted[0],ct));
            Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,persisted[0],ct));
            return true;
        });
        (await RowsAsync()).Should().Be(1);
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct) =>
        {
            Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,persisted[1],ct));
            Assert.True(await AcceptedMarketSignalProjection.TryProjectAsync(db,persisted[1],ct));
            return true;
        });
        (await RowsAsync()).Should().Be(0);
    }
}
