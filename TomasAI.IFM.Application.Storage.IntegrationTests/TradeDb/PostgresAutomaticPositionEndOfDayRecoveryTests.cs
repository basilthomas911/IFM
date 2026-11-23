using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.EventSourcing.Persistence.Command;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[Collection("PostgreSQL plan atomicity")]
public sealed partial class PostgresAutomaticPositionEndOfDayRecoveryTests(PostgresTradePlanFixture fixture)
{
    static readonly DateOnly Date=new(2026,11,17);
    static readonly DateTime Boundary=FuturesTradingValueDate.GetSessionEndUtc(Date).UtcDateTime;
    async Task<TradeDbContext> Context()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement+
            "CREATE SCHEMA IF NOT EXISTS market_data;"+MarketDataSchemaSql.PositionEodQuoteEvidenceCreate+SecuritiesSchemaDb.CreateStatement,[],ct));
        return new(new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres"),
            Mock.Of<IDbContextFactory>(),Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
    }
    static StrategyPositionSnapshot Position(TradeStrategyKind kind)
    {
        var id=StrategyPositionId.Create(new(Random.Shared.Next(60000000,70000000),1,1,1),kind);
        return new() { Id=id,StrategyKind=kind,IsOpen=true,Phase=StrategyPositionPhase.EndOfDay,PositionSequence=1,
            ValueDate=Date,LatestSealedValueDate=Date,CloseBoundaryUtc=Boundary,AsOfUtc=Boundary,
            HistoryAsOfUtc=Boundary.AddHours(-4),HistoryPositionSequence=1,LastMarketObservationUtc=Boundary.AddHours(-4),
            ContractCashMultiplier=50,OpeningCommission=0,OpeningValueDate=Date.AddDays(-2),DailyOpeningPnl=2,UnrealizedPnl=4,
            Legs=Enumerable.Range(1,2).Select(n=>new StrategyPositionLeg {TradeLegId=Guid.NewGuid(),ContractId=$"FIXTURE-{id.PositionId:N}-{n}",
                SignedQuantity=1,OpeningPrice=10,CurrentPrice=12,LastPriceAtUtc=Boundary.AddHours(-4),CashMultiplier=50}).ToArray() };
    }
    async Task References(StrategyPositionSnapshot position)
    {
        foreach(var leg in position.Legs)
        {
            var future=new FuturesContractV3ReadModel {ContractId=leg.ContractId,Dataset="GLBX.MDP3",InstrumentId=42,DefinitionTimestampUtc=new DateTimeOffset(Boundary.AddDays(-5))};
            var option=new FuturesOptionContractReadModel {ContractId=leg.ContractId,Dataset="GLBX.MDP3",InstrumentId=42,DefinitionTimestampUtc=new DateTimeOffset(Boundary.AddDays(-5))};
            var reference=new ReferenceContractVersion(position.StrategyKind==TradeStrategyKind.FuturesOutright?future:null,
                position.StrategyKind==TradeStrategyKind.FuturesOutright?null:option,null);
            await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync("INSERT INTO securities.securities_reference_version(contract_id,version,payload,published) VALUES($1,'v1',$2,true);",
                [leg.ContractId,MessagePackBinarySerializer.Shared.Serialize(reference)],ct));
        }
    }
    static AcquiredPositionClosingQuote Quote(PositionClosingInstrument instrument,bool verifiedBook=false)
    {
        if(verifiedBook)
        {
            var ns=(Boundary.AddHours(-1).Ticks-DateTime.UnixEpoch.Ticks)*100;
            var raw=Encoding.UTF8.GetBytes($"ts_recv,ts_event,rtype,publisher_id,instrument_id,action,sequence,flags,bid_px_00,ask_px_00,bid_sz_00,ask_sz_00\n{ns},{ns},1,1,42,M,5,128,13000000000,15000000000,1,1\n");
            var proof=new VerifiedClosingBook{Dataset=instrument.Dataset,PublisherId=1,RangeStartUtc=Boundary.AddHours(-2),RangeEndUtc=Boundary.AddMinutes(10),VerifiedThroughUtc=Boundary,CapturedAtUtc=Boundary.AddHours(1),ExpectedRecords=1};
            var book=ClosingBookArchive.Reconstruct(instrument.ContractId,instrument.InstrumentId,Date,Boundary,proof,raw,instrument.IsOption)!;
            book=book with{EvidenceId=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(book)))};
            return new(book,raw);
        }
        var record=Encoding.UTF8.GetBytes($"authentic-fixture-record:{instrument.ContractId}");
        var quote=new StoredPositionClosingQuote {ContractId=instrument.ContractId,InstrumentId=instrument.InstrumentId,ValueDate=Date,
            EventTimestampNanoseconds=(Boundary.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100,
            ReceiveTimestampNanoseconds=(Boundary.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100,
            SourceSequence=5,Bid=13,Ask=15,BidSize=1,AskSize=1,SourceArchiveSha256=Convert.ToHexString(SHA256.HashData(record))};
        quote=quote with {EvidenceId=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(quote)))};
        return new(quote,record);
    }
    static string Actor(TradeStrategyKind kind)=>kind switch {TradeStrategyKind.IronCondor=>"FuturesIronCondorTradePositionCommand",TradeStrategyKind.VerticalSpread=>"FuturesVerticalSpreadTradePositionCommand",_=>"FuturesTradePositionCommand"};
    static PositionChangedEvent Fact(StrategyPositionSnapshot p)=>p.StrategyKind switch {
        TradeStrategyKind.IronCondor=>new IronCondorPositionChangedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=p.Id,PositionSnapshot=p},
        TradeStrategyKind.VerticalSpread=>new VerticalSpreadPositionChangedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=p.Id,PositionSnapshot=p},
        _=>new FuturesPositionChangedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=p.Id,PositionSnapshot=p}};

    [Theory]
    [InlineData(TradeStrategyKind.IronCondor,false,false)]
    [InlineData(TradeStrategyKind.VerticalSpread,false,false)]
    [InlineData(TradeStrategyKind.FuturesOutright,false,false)]
    [InlineData(TradeStrategyKind.IronCondor,true,false)]
    [InlineData(TradeStrategyKind.VerticalSpread,true,false)]
    [InlineData(TradeStrategyKind.FuturesOutright,true,false)]
    [InlineData(TradeStrategyKind.IronCondor,true,true)]
    [InlineData(TradeStrategyKind.FuturesOutright,false,true)]
    [InlineData(TradeStrategyKind.IronCondor,true,false,true)]
    [InlineData(TradeStrategyKind.VerticalSpread,true,false,true)]
    [InlineData(TradeStrategyKind.FuturesOutright,true,false,true)]
    public async Task Durable_jobs_resume_partial_quotes_after_restart_and_lost_reply_without_duplicate_correction(TradeStrategyKind kind,bool nextSession,bool missingSeal,bool verifiedBook=false)
    {
        var db=await Context();var original=Position(kind) with {Phase=missingSeal?StrategyPositionPhase.MarkToMarket:StrategyPositionPhase.EndOfDay,LatestSealedValueDate=missingSeal?Date.AddDays(-1):Date,CloseBoundaryUtc=missingSeal?null:Boundary};var clock=new Clock(new DateTimeOffset(Boundary.AddMinutes(2)));
        var current=nextSession?original with {ValueDate=Date.AddDays(1),Phase=StrategyPositionPhase.MarkToMarket,PositionSequence=2,
            HistoryPositionSequence=2,HistoryAsOfUtc=Boundary.AddHours(2),AsOfUtc=Boundary.AddHours(2),DailyOpeningPnl=4}:original;
        var stream=$"Command.{Actor(kind)}.{original.Id.Format()}";var originalFact=Fact(original);
        await fixture.Transactions.ExecuteOnceAsync(async(tx,ct)=>{
            await tx.AppendAsync(new EnlistedEventAppend(stream,originalFact.CommandId,originalFact,0),ct);
            await db.ProjectPositionAsync(tx,original,ct);if(nextSession)await db.ProjectPositionAsync(tx,current,ct);return true;
        });
        await References(original);
        var commands=new Mock<IStrategyPositionCommandApi>(MockBehavior.Strict);
        if(missingSeal) commands.Setup(c=>c.EndOfDayAsync(original.Id,kind,Date,Boundary,It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Injected normal seal transport timeout"));
        var source=Mock.Of<IEventSourceActorDbContext>(s=>s.ProviderName=="System.Data.Postgres" && s.ConnectionString==fixture.Connection);
        commands.Setup(c=>c.RepairEndOfDayAsync(It.IsAny<StrategyPositionSnapshot>(),It.IsAny<long>(),Date,Boundary,It.IsAny<StoredPositionClosingQuote[]>(),It.IsAny<CancellationToken>()))
            .Returns(async(StrategyPositionSnapshot supplied,long sourceId,DateOnly date,DateTime boundary,StoredPositionClosingQuote[] quotes,CancellationToken ct)=>{
                var result=StoredQuotePositionEndOfDayRecovery.Recover(supplied,(await db.GetStrategyPositionAsync(original.Id,ct))!,date,boundary,quotes);
                var commandId=Guid.NewGuid();var subject=new ActorSubject(ActorType.Command,Actor(kind),"EndOfDay",original.Id.Format());
                ICommand command=kind switch {
                    TradeStrategyKind.IronCondor=>new EndOfDayIronCondorPositionCommand {CommandId=commandId,Subject=subject,EntityId=original.Id,ValueDate=date,EffectiveAtUtc=boundary,HistoricalSnapshot=supplied,HistoricalSourceEventId=sourceId,HistoricalClosingQuotes=quotes},
                    TradeStrategyKind.VerticalSpread=>new EndOfDayVerticalSpreadPositionCommand {CommandId=commandId,Subject=subject,EntityId=original.Id,ValueDate=date,EffectiveAtUtc=boundary,HistoricalSnapshot=supplied,HistoricalSourceEventId=sourceId,HistoricalClosingQuotes=quotes},
                    _=>new EndOfDayFuturesPositionCommand {CommandId=commandId,Subject=subject,EntityId=original.Id,ValueDate=date,EffectiveAtUtc=boundary,HistoricalSnapshot=supplied,HistoricalSourceEventId=sourceId,HistoricalClosingQuotes=quotes}};
                PositionChangedEvent fact=Fact(result.Current) switch {
                    IronCondorPositionChangedEvent iron=>iron with {CommandId=commandId,HistoricalEndOfDay=result.Seal,HistoricalSourceEventId=sourceId},
                    VerticalSpreadPositionChangedEvent vertical=>vertical with {CommandId=commandId,HistoricalEndOfDay=result.Seal,HistoricalSourceEventId=sourceId},
                    FuturesPositionChangedEvent future=>future with {CommandId=commandId,HistoricalEndOfDay=result.Seal,HistoricalSourceEventId=sourceId},_=>throw new Exception()};
                await TradeAtomicPersistence.CommitAsync(command,new DomainEventCollection([fact]),1,source,db,fixture.Transactions,ct);
                throw new TimeoutException("Injected lost reply after durable commit");
            });
        var provider=new Mock<IPositionClosingQuoteSource>(MockBehavior.Strict);var acquired=0;
        provider.Setup(p=>p.AcquireAsync(It.IsAny<PositionClosingInstrument>(),Date,Boundary,It.IsAny<CancellationToken>()))
            .Returns((PositionClosingInstrument instrument,DateOnly _,DateTime _,CancellationToken _)=>{
                acquired++;return Task.FromResult<AcquiredPositionClosingQuote?>(acquired==2?null:Quote(instrument,verifiedBook));
            });
        try
        {
            var audit=await new OpenPositionEndOfDaySweep(db,commands.Object,new TomasAI.IFM.Application.MarketData.Databento.Historical.CmeFuturesMarketSessionCalendar(),clock,new(),db).RunAsync(default);
            Assert.Equal(missingSeal?"MissingSeal":"StaleCloseObservation",Assert.Single(audit.Issues).Code);
            Assert.True(await new PositionEndOfDayRecovery(db,db,provider.Object,commands.Object,clock,new()).RunOneAsync(default));
            Assert.Equal(2,acquired);Assert.NotNull(await db.ReadQuoteAsync(original.Legs[0].ContractId,42,Date,default));
            var stillBlocked=await new OpenPositionEndOfDaySweep(db,commands.Object,new TomasAI.IFM.Application.MarketData.Databento.Historical.CmeFuturesMarketSessionCalendar(),clock,new(),db).RunAsync(default);
            Assert.Equal(1,stillBlocked.FailedPositions);
            Assert.Equal(1,await db.CountOutstandingAsync(clock.Now.AddDays(1),default));Assert.Equal(missingSeal?"MissingSeal":"StaleCloseObservation",Assert.Single(stillBlocked.Issues).Code);
            Assert.False(await new PositionEndOfDayRecovery(db,db,provider.Object,commands.Object,clock,new()).RunOneAsync(default));
            clock.Now=clock.Now.AddMinutes(1);var reopened=await Context();
            Assert.True(await new PositionEndOfDayRecovery(reopened,reopened,provider.Object,commands.Object,clock,new()).RunOneAsync(default));
            Assert.Equal(3,acquired);
            var diagnostic=await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ScalarAsync("SELECT last_error FROM trade.position_eod_recovery WHERE position_id=$1 AND value_date=$2;",[original.Id.PositionId,Date],ct));
            Assert.True(await fixture.EventCountAsync(stream)==2,"Recovery failed: "+diagnostic);
            var seal=await ((IOpenPositionEndOfDayStore)reopened).ReadSealAsync(original.Id,Date,Boundary.AddDays(-1),Boundary,default);
            Assert.True(PositionEndOfDayRecovery.IsQualified(seal,Date,Boundary,TimeSpan.FromMinutes(15)));
            var saved=(await reopened.GetStrategyPositionAsync(original.Id))!;Assert.True(saved.IsOpen);
            if(nextSession) {Assert.Equal(current.AsOfUtc,saved.AsOfUtc);Assert.Equal(current.Legs[0].LastPriceAtUtc,saved.Legs[0].LastPriceAtUtc);Assert.Equal(8,saved.DailyOpeningPnl);}
            // A crash after the correction but before job completion must recheck the seal, not resubmit the correction.
            await reopened.ScheduleAsync(original.Id,Date,Boundary,default);
            clock.Now=clock.Now.AddMinutes(1);
            Assert.True(await new PositionEndOfDayRecovery(reopened,reopened,provider.Object,commands.Object,clock,new()).RunOneAsync(default));
            Assert.Equal(2,await fixture.EventCountAsync(stream));Assert.Equal(3,acquired);
            Assert.Equal(0,await reopened.CountOutstandingAsync(clock.Now,default));
            commands.Verify(c=>c.RepairEndOfDayAsync(It.IsAny<StrategyPositionSnapshot>(),It.IsAny<long>(),Date,Boundary,It.IsAny<StoredPositionClosingQuote[]>(),It.IsAny<CancellationToken>()),Times.Once);
        }
        finally {var saved=(await db.GetStrategyPositionAsync(original.Id))!;await db.UpsertStrategyPositionAsync(saved with {IsOpen=false,PositionSequence=saved.PositionSequence+1});}
    }

    [Fact]
    public async Task Previous_close_is_verified_at_commit_and_repaired_daily_pnl_uses_that_exact_baseline()
    {
        var db=await Context();var original=Position(TradeStrategyKind.IronCondor) with{PositionSequence=2,HistoryPositionSequence=2};
        var prior=original with{ValueDate=Date.AddDays(-1),LatestSealedValueDate=Date.AddDays(-1),PositionSequence=1,HistoryPositionSequence=1,
            AsOfUtc=Boundary.AddDays(-1),HistoryAsOfUtc=Boundary.AddDays(-1),CloseBoundaryUtc=Boundary.AddDays(-1)};
        var stream=$"Command.{Actor(original.StrategyKind)}.{original.Id.Format()}";
        await fixture.Transactions.ExecuteOnceAsync(async(tx,ct)=>{
            var first=Fact(prior);await tx.AppendAsync(new EnlistedEventAppend(stream,first.CommandId,first,0),ct);await db.ProjectPositionAsync(tx,prior,ct);
            var second=Fact(original);await tx.AppendAsync(new EnlistedEventAppend(stream,second.CommandId,second,1),ct);await db.ProjectPositionAsync(tx,original,ct);return true;
        });
        var exact=(await db.ReadSourceAsync(original.Id,Date,default))!;
        var savedPrior=(await db.ReadPreviousSealAsync(original.Id,Date,default))!;
        Assert.Equal(prior.ValueDate,savedPrior.ValueDate);
        var quotes=new List<StoredPositionClosingQuote>();
        foreach(var leg in original.Legs){var acquired=Quote(new(leg.ContractId,42,"GLBX.MDP3",true),true);await db.SaveQuoteAsync(acquired,default);quotes.Add(acquired.Quote);}
        var source=Mock.Of<IEventSourceActorDbContext>(x=>x.ProviderName=="System.Data.Postgres" && x.ConnectionString==fixture.Connection);
        async Task Commit(StrategyPositionSnapshot baseline, StoredPositionClosingQuote[]? selectedQuotes=null)
        {
            var suppliedQuotes=selectedQuotes??quotes.ToArray();
            var result=StoredQuotePositionEndOfDayRecovery.Recover(exact.Snapshot,original,Date,Boundary,suppliedQuotes,baseline);
            var command=new EndOfDayIronCondorPositionCommand{CommandId=Guid.NewGuid(),Subject=new(ActorType.Command,Actor(original.StrategyKind),"EndOfDay",original.Id.Format()),
                EntityId=original.Id,ValueDate=Date,EffectiveAtUtc=Boundary,HistoricalSnapshot=exact.Snapshot,HistoricalSourceEventId=exact.EventId,HistoricalClosingQuotes=suppliedQuotes,PreviousEndOfDay=baseline};
            var fact=new IronCondorPositionChangedEvent{Id=Guid.NewGuid(),CommandId=command.CommandId,EntityId=original.Id,
                PositionSnapshot=result.Current,HistoricalEndOfDay=result.Seal,HistoricalSourceEventId=exact.EventId};
            await TradeAtomicPersistence.CommitAsync(command,new DomainEventCollection([fact]),2,source,db,fixture.Transactions,default);
        }
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(()=>Commit(savedPrior with{UnrealizedPnl=999}));
            Assert.Equal(2,await fixture.EventCountAsync(stream));
            // Even an inserted, correctly hashed payload must match the archived provider book at commit.
            var tampered=quotes[0] with{Ask=16,EvidenceId=""};
            tampered=tampered with{EvidenceId=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(tampered)))};
            await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync("""
                INSERT INTO market_data.position_eod_quote_evidence(evidence_id,payload,source_archive_sha256,contract_id,value_date,observed_at)
                VALUES($1,$2,$3,$4,$5,$6);
                """,[tampered.EvidenceId,MessagePackBinarySerializer.Shared.Serialize(tampered),tampered.SourceArchiveSha256,tampered.ContractId,tampered.ValueDate,tampered.ObservedAtUtc],ct));
            await Assert.ThrowsAsync<InvalidDataException>(()=>Commit(savedPrior,[tampered,quotes[1]]));
            Assert.Equal(2,await fixture.EventCountAsync(stream));
            await Commit(savedPrior);Assert.Equal(3,await fixture.EventCountAsync(stream));
            var seal=(await ((IOpenPositionEndOfDayStore)db).ReadSealAsync(original.Id,Date,Boundary.AddDays(-1),Boundary,default))!;
            Assert.True(PositionEndOfDayRecovery.IsQualified(seal,Date,Boundary,TimeSpan.FromMinutes(15)));
            Assert.Equal(4,seal.DailyOpeningPnl);Assert.Equal(4,seal.DailyPnlPoints);
        }
        finally{var saved=(await db.GetStrategyPositionAsync(original.Id))!;await db.UpsertStrategyPositionAsync(saved with{IsOpen=false,PositionSequence=saved.PositionSequence+1});}
    }

    [Fact]
    public async Task Closing_book_archive_must_replay_exactly_before_it_can_be_saved()
    {
        var db=await Context();var good=Quote(new("BOOK-FIXTURE",42,"GLBX.MDP3",true),true);
        await db.SaveQuoteAsync(good,default);
        var loaded=await db.ReadQuoteAsync(good.Quote.ContractId,42,Date,default);
        Assert.Equal(good.Quote,loaded);
        var wrong=good.Quote with{Ask=16,EvidenceId=""};
        wrong=wrong with{EvidenceId=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(wrong)))};
        await Assert.ThrowsAsync<InvalidDataException>(()=>db.SaveQuoteAsync(new(wrong,good.OriginalRecord),default));
        var truncated=good.OriginalRecord[..^2];
        await Assert.ThrowsAsync<InvalidDataException>(()=>db.SaveQuoteAsync(new(good.Quote,truncated),default));
    }

    [Fact]
    public async Task Later_sessions_wait_for_earlier_closing_book_gaps_on_the_same_position()
    {
        var db=await Context();var p=Position(TradeStrategyKind.IronCondor);var now=new DateTimeOffset(Boundary.AddDays(2));
        await db.ScheduleAsync(p.Id,Date.AddDays(1),Boundary.AddDays(1),default);
        await db.ScheduleAsync(p.Id,Date,Boundary,default);
        var first=(await db.ClaimAsync(now,TimeSpan.FromSeconds(10),default))!;Assert.Equal(Date,first.ValueDate);
        Assert.Null(await db.ClaimAsync(now,TimeSpan.FromSeconds(10),default));
        await db.FinishAsync(first,now,"EOD.BOOK.GAP",default);
        Assert.Null(await db.ClaimAsync(now,TimeSpan.FromSeconds(10),default));
        var retry=(await db.ClaimAsync(now.AddMinutes(1),TimeSpan.FromSeconds(10),default))!;Assert.Equal(Date,retry.ValueDate);
        await db.FinishAsync(retry,now.AddMinutes(1),null,default);
        var next=(await db.ClaimAsync(now.AddMinutes(1),TimeSpan.FromSeconds(10),default))!;Assert.Equal(Date.AddDays(1),next.ValueDate);
        await db.FinishAsync(next,now.AddMinutes(1),null,default);
    }

    [Fact]
    public async Task Claims_are_exclusive_expired_leases_recover_and_old_workers_cannot_finish_them()
    {
        var db=await Context();var p=Position(TradeStrategyKind.FuturesOutright);var now=new DateTimeOffset(Boundary.AddDays(2));
        await db.ScheduleAsync(p.Id,Date,Boundary,default);
        var claims=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>db.ClaimAsync(now,TimeSpan.FromSeconds(10),default)));
        var original=Assert.Single(claims,c=>c is not null)!;
        Assert.Null(await db.ClaimAsync(now.AddSeconds(5),TimeSpan.FromSeconds(10),default));
        var replacement=(await (await Context()).ClaimAsync(now.AddSeconds(11),TimeSpan.FromSeconds(10),default))!;
        Assert.NotEqual(original.LeaseId,replacement.LeaseId);Assert.Equal(original.Attempts+1,replacement.Attempts);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.FinishAsync(original,now.AddSeconds(12),null,default));
        await db.FinishAsync(replacement,now.AddSeconds(12),null,default);
    }

    [Fact]
    public async Task Quote_evidence_populated_upgrade_and_reapplication_preserve_original_rows()
    {
        var schema="eod_upgrade_"+Guid.NewGuid().ToString("N");
        var migration=MarketDataSchemaSql.PositionEodQuoteEvidenceCreate.Replace("market_data.",schema+".",StringComparison.Ordinal);
        try
        {
            await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{
                await db.ExecuteAsync($"CREATE SCHEMA {schema}; CREATE TABLE {schema}.position_eod_quote_evidence(evidence_id text PRIMARY KEY,payload bytea NOT NULL,source_archive_sha256 text NOT NULL);",[],ct);
                await db.ExecuteAsync($"INSERT INTO {schema}.position_eod_quote_evidence VALUES($1,$2,$3);",[new string('A',64),new byte[]{1,2},new string('B',64)],ct);
                await db.ExecuteAsync(migration,[],ct);await db.ExecuteAsync(migration,[],ct);
                var count=await db.ScalarAsync($"SELECT count(*) FROM {schema}.position_eod_quote_evidence WHERE payload=$1 AND contract_id IS NULL AND value_date IS NULL AND observed_at IS NULL;",[new byte[]{1,2}],ct);
                Assert.Equal(1L,count);return true;
            });
        }
        finally {await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync($"DROP SCHEMA IF EXISTS {schema} CASCADE;",[],ct));}
    }

    sealed class Clock(DateTimeOffset now):TimeProvider {public DateTimeOffset Now {get;set;}=now;public override DateTimeOffset GetUtcNow()=>Now;}
}
