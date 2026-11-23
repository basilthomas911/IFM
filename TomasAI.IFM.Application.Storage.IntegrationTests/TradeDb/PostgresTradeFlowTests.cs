using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventSourcing.Persistence.Command;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.Serialization;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresTradeFlowTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Prior_session_eod_commits_before_the_first_quote_and_retries_safely(
        bool feedRecovery, bool noTick)
    {
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], ct));
        var settings = new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection, fixture.Connection, "System.Data.Postgres");
        var context = new TradeDbContext(settings, Mock.Of<IDbContextFactory>(), Mock.Of<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        var source = Mock.Of<IEventSourceActorDbContext>(
            value => value.ProviderName == "System.Data.Postgres" && value.ConnectionString == fixture.Connection);
        var id = StrategyPositionId.Create(new(Random.Shared.Next(60000000, 70000000), 1, 1, 1), TradeStrategyKind.IronCondor);
        var previous = new DateOnly(2026, 11, 12);
        var today = previous.AddDays(1);
        var originalDate = noTick ? previous.AddDays(-1) : previous;
        var observed = FuturesTradingValueDate.GetSessionEndUtc(originalDate).UtcDateTime.AddHours(-2);
        var boundary = FuturesTradingValueDate.GetSessionEndUtc(previous).UtcDateTime;
        var leg = new StrategyPositionLeg
        {
            TradeLegId = Guid.NewGuid(), ContractId = "catch-up-ES", SignedQuantity = 1,
            OpeningPrice = 10, CurrentPrice = 12, LastPriceAtUtc = observed, LastSourceSequence = 1
        };
        var original = new StrategyPositionSnapshot
        {
            Id = id, StrategyKind = TradeStrategyKind.IronCondor, IsOpen = true,
            Phase = noTick ? StrategyPositionPhase.EndOfDay : StrategyPositionPhase.MarkToMarket,
            ValueDate = originalDate, LatestSealedValueDate = noTick ? originalDate : default,
            AsOfUtc = noTick ? FuturesTradingValueDate.GetSessionEndUtc(originalDate).UtcDateTime : observed,
            CloseBoundaryUtc = noTick ? FuturesTradingValueDate.GetSessionEndUtc(originalDate).UtcDateTime : null,
            LastMarketObservationUtc = observed, PositionSequence = 1, RouteGeneration = 1,
            HistoryAsOfUtc = observed, HistoryPositionSequence = 1, UnrealizedPnl = 2, Legs = [leg]
        };
        var actor = "FuturesIronCondorTradePositionCommand";
        var tick = new ChangeTradeLegDataCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, Subject = new(ActorType.Command, actor, ChangeTradeLegDataCommand.Verb, id.Format()),
            TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, Price = 15, SourceSequence = 2,
            RouteGeneration = 1, EffectiveAtUtc = boundary.AddHours(2), TradeType = TradeStrategyKind.IronCondor
        };
        var opened = new IronCondorPositionChangedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = id, PositionSnapshot = original
        };
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            await db.AppendAsync(new EnlistedEventAppend(tick.StreamId, opened.CommandId, opened, 0), ct);
            await context.ProjectPositionAsync(db, original, ct);
            return true;
        });
        var commands = new Mock<IStrategyPositionCommandApi>(MockBehavior.Strict);
        var sealCalls = 0;
        if (feedRecovery)
        {
            commands.Setup(api => api.EndOfDayAsync(id, original.StrategyKind, previous, boundary, default))
                .Returns(async () =>
                {
                    sealCalls++;
                    var command = new EndOfDayIronCondorPositionCommand
                    {
                        CommandId = Guid.NewGuid(), EntityId = id, ValueDate = previous, EffectiveAtUtc = boundary,
                        Subject = new(ActorType.Command, actor, EndOfDayIronCondorPositionCommand.Verb, id.Format())
                    };
                    var owner = new IronCondorPositionCommandState();
                    owner.Update(opened, addEvent: false);
                    Assert.True(command.Execute(owner).Success);
                    await TradeAtomicPersistence.CommitAsync(command, new DomainEventCollection(owner.Events), 1, source, context, fixture.Transactions);
                    return new ServiceOk<Guid>(command.CommandId);
                });
            var trade = new EstablishedTradeDefinition
            {
                Id = id.Trade, Status = EstablishedTradeStatus.Open, StrategyKind = original.StrategyKind,
                Legs = [new() { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId }]
            };
            var catchUp = new TradeFeedEndOfDayCatchUp(context.GetStrategyPositionAsync, commands.Object);
            var store = new Mock<ITradeFeedRecoveryStore>();
            var runtime = new Mock<ITradeFeedRecoveryRuntime>();
            runtime.SetupGet(value => value.ActiveDate).Returns(today);
            runtime.Setup(value => value.LoadAsync(id.Trade, default)).ReturnsAsync(trade);
            store.Setup(value => value.ReadAsync(today, default)).ReturnsAsync([]);
            var recovery = new TradeFeedRecoveryService(store.Object, runtime.Object, endOfDay: catchUp);
            // A Rust UI load with no saved intent still repairs the seal without subscribing.
            Assert.False(await recovery.LoadAsync(id.Trade.Format(), today, false, default));
            runtime.Verify(value => value.StartAsync(It.IsAny<string>(), It.IsAny<string>(), today, default), Times.Never);
            // Startup restores a saved intent only after it sees the durable seal.
            store.Setup(value => value.ReadAsync(today, default)).ReturnsAsync(
                [new(id.Trade.Format(), today, [leg.ContractId], DateTimeOffset.UtcNow)]);
            runtime.Setup(value => value.StartAsync(It.IsAny<string>(), leg.ContractId, today, default))
                .Returns(async () => Assert.Equal(previous, (await context.GetStrategyPositionAsync(id))!.LatestSealedValueDate));
            Assert.True(await recovery.LoadAsync(id.Trade.Format(), today, false, default));
            runtime.Verify(value => value.StartAsync(It.IsAny<string>(), leg.ContractId, today, default), Times.Once);
            Assert.Equal(1, sealCalls);
            Assert.Equal(previous, (await context.GetStrategyPositionAsync(id))!.LatestSealedValueDate);
        }
        var state = new IronCondorPositionCommandState();
        state.Update(opened with { PositionSnapshot = (await context.GetStrategyPositionAsync(id))! }, addEvent: false);
        Assert.True(tick.Execute(state).Success);
        var events = new DomainEventCollection(state.Events);
        Assert.Equal(feedRecovery ? 1 : 2, events.Count);
        // Two copies of the same delivery can compete at the command reservation.
        async Task<bool> TryCommit()
        {
            try
            {
                var copy = new DomainEventCollection(events.Select(fact => ((IronCondorPositionChangedEvent)fact) with { }));
                await TradeAtomicPersistence.CommitAsync(tick, copy, feedRecovery ? 2 : 1, source, context, fixture.Transactions);
                return true;
            }
            catch (CommandAuditDuplicateException duplicate) when (duplicate.HasCommittedEvents)
            {
                return false;
            }
        }
        Assert.Single(await Task.WhenAll(TryCommit(), TryCommit()), committed => committed);
        Assert.Equal(3, await fixture.EventCountAsync(tick.StreamId));

        var history = await context.GetStrategyPositionHistoryAsync(id.PositionId, observed.AddMinutes(-1), tick.EffectiveAtUtc.AddMinutes(1), 100);
        var seal = Assert.Single(history.Items, row => row.ValueDate == previous);
        Assert.Equal(StrategyPositionPhase.EndOfDay, seal.Phase);
        Assert.Equal(boundary, seal.CloseBoundaryUtc);
        Assert.Equal(observed, seal.LastMarketObservationUtc);
        Assert.Equal(12m, Assert.Single(seal.Legs).CurrentPrice);
        var current = (await context.GetStrategyPositionAsync(id))!;
        Assert.Equal(today, current.ValueDate);
        Assert.Equal(previous, current.LatestSealedValueDate);
        Assert.Equal(2m, current.DailyOpeningPnl);
        Assert.Equal(tick.EffectiveAtUtc, current.LastMarketObservationUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stored_closing_quotes_repair_stale_seal_preserving_live_marks_and_reject_tampering(bool nextSession)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement + "CREATE SCHEMA IF NOT EXISTS market_data;" + TomasAI.IFM.Application.Storage.MarketDataDb.Schema.MarketDataSchemaSql.PositionEodQuoteEvidenceCreate,[],ct));
        var context=new TradeDbContext(new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres"),Mock.Of<IDbContextFactory>(),Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        var source=Mock.Of<TomasAI.IFM.Application.Storage.IEventSourceActorDbContext>(v=>v.ProviderName=="System.Data.Postgres" && v.ConnectionString==fixture.Connection);
        var date=new DateOnly(2026,11,17); var boundary=FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime;
        var id=StrategyPositionId.Create(new(Random.Shared.Next(60000000,70000000),1,1,1),TradeStrategyKind.IronCondor);
        var original=new StrategyPositionSnapshot { Id=id,StrategyKind=TradeStrategyKind.IronCondor,IsOpen=true,Phase=StrategyPositionPhase.EndOfDay,
            ValueDate=date,PositionSequence=10,HistoryPositionSequence=1,HistoryAsOfUtc=boundary.AddHours(-5),AsOfUtc=boundary,
            CloseBoundaryUtc=boundary,LatestSealedValueDate=date,LastMarketObservationUtc=boundary.AddHours(-3),
            ContractCashMultiplier=50,OpeningCommission=0,OpeningValueDate=date.AddDays(-1),UnrealizedPnl=2,DailyOpeningPnl=1,
            Legs=[new() { TradeLegId=Guid.NewGuid(),ContractId="CLOSE-FIXTURE",SignedQuantity=1,OpeningPrice=10,CurrentPrice=12,CashMultiplier=50,LastPriceAtUtc=boundary.AddHours(-3) }] };
        var current=nextSession ? original with { ValueDate=date.AddDays(1),Phase=StrategyPositionPhase.MarkToMarket,PositionSequence=11,HistoryPositionSequence=11,HistoryAsOfUtc=boundary.AddHours(4),AsOfUtc=boundary.AddHours(4),DailyOpeningPnl=2 } : original;
        var quote=new StoredPositionClosingQuote { ContractId="CLOSE-FIXTURE",ValueDate=date,InstrumentId=42,SourceSequence=5,
            EventTimestampNanoseconds=(boundary.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100,ReceiveTimestampNanoseconds=(boundary.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100,
            Bid=13,Ask=15,BidSize=1,AskSize=1,SourceArchiveSha256=new string('A',64) };
        quote=quote with { EvidenceId=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(quote))) };
        var command=new EndOfDayIronCondorPositionCommand { CommandId=Guid.NewGuid(),EntityId=id,ValueDate=date,EffectiveAtUtc=boundary,HistoricalSnapshot=original,HistoricalClosingQuotes=[quote],
            Subject=new(ActorType.Command,"FuturesIronCondorTradePositionCommand",EndOfDayIronCondorPositionCommand.Verb,id.Format()) };
        var originalFact=new IronCondorPositionChangedEvent { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=id,PositionSnapshot=original,ReceivedOn=boundary };
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=> {
            await db.AppendAsync(new EnlistedEventAppend(command.StreamId,originalFact.CommandId,originalFact,0),ct);
            await context.ProjectPositionAsync(db,original,ct);
            if(nextSession) await context.ProjectPositionAsync(db,current,ct);
            await db.ExecuteAsync("INSERT INTO market_data.position_eod_quote_evidence(evidence_id,payload,source_archive_sha256) VALUES($1,$2,$3) ON CONFLICT DO NOTHING;",[quote.EvidenceId,MessagePackBinarySerializer.Shared.Serialize(quote),quote.SourceArchiveSha256],ct);return true;
        });
        command=command with { HistoricalSourceEventId=Convert.ToInt64(await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT e.eventversion FROM event_log e JOIN event_stream_id s USING(eventstreamid) WHERE s.eventstream=$1;",[command.StreamId],ct))) };
        var recovered=TomasAI.IFM.Domain.Trade.Futures.Position.Model.StoredQuotePositionEndOfDayRecovery.Recover(original,current,date,boundary,[quote]);
        var fact=new IronCondorPositionChangedEvent { CommandId=command.CommandId,EntityId=id,PositionSnapshot=recovered.Current,HistoricalEndOfDay=recovered.Seal,HistoricalSourceEventId=command.HistoricalSourceEventId };
        async Task Commit(EndOfDayIronCondorPositionCommand intent)=>await TradeAtomicPersistence.CommitAsync(intent,new DomainEventCollection([fact]),1,source,context,fixture.Transactions);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Commit(command with { HistoricalClosingQuotes=[quote with { Bid=999 }] }));
        await Commit(command);
        var saved=(await context.GetStrategyPositionAsync(id))!;
        saved.IsOpen.Should().BeTrue(); saved.PositionSequence.Should().Be(current.PositionSequence+1);
        if(nextSession) { saved.DailyOpeningPnl.Should().Be(4); saved.Legs[0].LastPriceAtUtc.Should().Be(current.Legs[0].LastPriceAtUtc); saved.AsOfUtc.Should().Be(current.AsOfUtc); }
        else { saved.Legs[0].CurrentPrice.Should().Be(14); saved.LastMarketObservationUtc.Should().Be(boundary.AddMinutes(-1)); }
        var history=await context.GetStrategyPositionHistoryAsync(id.PositionId,boundary.AddDays(-1),boundary.AddDays(1),100);
        history.Items.Single(p=>p.ValueDate==date).Legs[0].CurrentPrice.Should().Be(14);
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(2);
        // Exact projection guard prevents applying the same source correction twice.
        await Assert.ThrowsAsync<TomasAI.IFM.Shared.Exceptions.ConcurrencyException>(()=>Commit(command with { CommandId=Guid.NewGuid() }));
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(2);
        Assert.Throws<InvalidOperationException>(()=>TomasAI.IFM.Domain.Trade.Futures.Position.Model.StoredQuotePositionEndOfDayRecovery.Recover(original,current,date,boundary,[quote with { EventTimestampNanoseconds=quote.EventTimestampNanoseconds+120_000_000_000 }]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stored_closing_quotes_repair_vertical_spread_with_same_guards(bool nextSession)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement + "CREATE SCHEMA IF NOT EXISTS market_data;" + TomasAI.IFM.Application.Storage.MarketDataDb.Schema.MarketDataSchemaSql.PositionEodQuoteEvidenceCreate,[],ct));
        var context=new TradeDbContext(new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres"),Mock.Of<IDbContextFactory>(),Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        var source=Mock.Of<TomasAI.IFM.Application.Storage.IEventSourceActorDbContext>(v=>v.ProviderName=="System.Data.Postgres" && v.ConnectionString==fixture.Connection);
        var date=new DateOnly(2026,11,17); var boundary=FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime;
        var id=StrategyPositionId.Create(new(Random.Shared.Next(60000000,70000000),1,1,1),TradeStrategyKind.VerticalSpread);
        var original=new StrategyPositionSnapshot { Id=id,StrategyKind=TradeStrategyKind.VerticalSpread,IsOpen=true,Phase=StrategyPositionPhase.EndOfDay,
            ValueDate=date,PositionSequence=10,HistoryPositionSequence=1,HistoryAsOfUtc=boundary.AddHours(-5),AsOfUtc=boundary,
            CloseBoundaryUtc=boundary,LatestSealedValueDate=date,LastMarketObservationUtc=boundary.AddHours(-3),
            ContractCashMultiplier=50,OpeningCommission=0,OpeningValueDate=date.AddDays(-1),UnrealizedPnl=2,DailyOpeningPnl=1,
            Legs=[new() { TradeLegId=Guid.NewGuid(),ContractId="CLOSE-FIXTURE",SignedQuantity=1,OpeningPrice=10,CurrentPrice=12,CashMultiplier=50,LastPriceAtUtc=boundary.AddHours(-3) }] };
        var current=nextSession ? original with { ValueDate=date.AddDays(1),Phase=StrategyPositionPhase.MarkToMarket,PositionSequence=11,HistoryPositionSequence=11,HistoryAsOfUtc=boundary.AddHours(4),AsOfUtc=boundary.AddHours(4),DailyOpeningPnl=2 } : original;
        var quote=new StoredPositionClosingQuote { ContractId="CLOSE-FIXTURE",ValueDate=date,InstrumentId=42,SourceSequence=5,
            EventTimestampNanoseconds=(boundary.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100,ReceiveTimestampNanoseconds=(boundary.AddMinutes(-1).Ticks-DateTime.UnixEpoch.Ticks)*100,
            Bid=13,Ask=15,BidSize=1,AskSize=1,SourceArchiveSha256=new string('A',64) };
        quote=quote with { EvidenceId=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(quote))) };
        var command=new EndOfDayVerticalSpreadPositionCommand { CommandId=Guid.NewGuid(),EntityId=id,ValueDate=date,EffectiveAtUtc=boundary,HistoricalSnapshot=original,HistoricalClosingQuotes=[quote],
            Subject=new(ActorType.Command,"FuturesVerticalSpreadTradePositionCommand",EndOfDayVerticalSpreadPositionCommand.Verb,id.Format()) };
        var originalFact=new VerticalSpreadPositionChangedEvent { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=id,PositionSnapshot=original,ReceivedOn=boundary };
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=> {
            await db.AppendAsync(new EnlistedEventAppend(command.StreamId,originalFact.CommandId,originalFact,0),ct);
            await context.ProjectPositionAsync(db,original,ct);
            if(nextSession) await context.ProjectPositionAsync(db,current,ct);
            await db.ExecuteAsync("INSERT INTO market_data.position_eod_quote_evidence(evidence_id,payload,source_archive_sha256) VALUES($1,$2,$3) ON CONFLICT DO NOTHING;",[quote.EvidenceId,MessagePackBinarySerializer.Shared.Serialize(quote),quote.SourceArchiveSha256],ct);return true;
        });
        command=command with { HistoricalSourceEventId=Convert.ToInt64(await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT e.eventversion FROM event_log e JOIN event_stream_id s USING(eventstreamid) WHERE s.eventstream=$1;",[command.StreamId],ct))) };
        var recovered=TomasAI.IFM.Domain.Trade.Futures.Position.Model.StoredQuotePositionEndOfDayRecovery.Recover(original,current,date,boundary,[quote]);
        var fact=new VerticalSpreadPositionChangedEvent { CommandId=command.CommandId,EntityId=id,PositionSnapshot=recovered.Current,HistoricalEndOfDay=recovered.Seal,HistoricalSourceEventId=command.HistoricalSourceEventId };
        async Task Commit(EndOfDayVerticalSpreadPositionCommand intent)=>await TradeAtomicPersistence.CommitAsync(intent,new DomainEventCollection([fact]),1,source,context,fixture.Transactions);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Commit(command with { HistoricalClosingQuotes=[quote with { Bid=999 }] }));
        await Commit(command);
        var saved=(await context.GetStrategyPositionAsync(id))!;
        saved.IsOpen.Should().BeTrue(); saved.PositionSequence.Should().Be(current.PositionSequence+1);
        if(nextSession) { saved.DailyOpeningPnl.Should().Be(4); saved.Legs[0].LastPriceAtUtc.Should().Be(current.Legs[0].LastPriceAtUtc); saved.AsOfUtc.Should().Be(current.AsOfUtc); }
        else { saved.Legs[0].CurrentPrice.Should().Be(14); saved.LastMarketObservationUtc.Should().Be(boundary.AddMinutes(-1)); }
        var history=await context.GetStrategyPositionHistoryAsync(id.PositionId,boundary.AddDays(-1),boundary.AddDays(1),100);
        history.Items.Single(p=>p.ValueDate==date).Legs[0].CurrentPrice.Should().Be(14);
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(2);
        // Exact projection guard prevents applying the same source correction twice.
        await Assert.ThrowsAsync<TomasAI.IFM.Shared.Exceptions.ConcurrencyException>(()=>Commit(command with { CommandId=Guid.NewGuid() }));
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(2);
        Assert.Throws<InvalidOperationException>(()=>TomasAI.IFM.Domain.Trade.Futures.Position.Model.StoredQuotePositionEndOfDayRecovery.Recover(original,current,date,boundary,[quote with { EventTimestampNanoseconds=quote.EventTimestampNanoseconds+120_000_000_000 }]));
    }

    [Fact]
    public async Task Historical_eod_requires_committed_mark_and_preserves_closed_current_and_routes()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var settings=new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres");
        var context=new TradeDbContext(settings,Mock.Of<IDbContextFactory>(),Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        var source=Mock.Of<TomasAI.IFM.Application.Storage.IEventSourceActorDbContext>(
            value=>value.ProviderName=="System.Data.Postgres" && value.ConnectionString==fixture.Connection);
        var id=StrategyPositionId.Create(new(Random.Shared.Next(60000000,70000000),1,1,1),TradeStrategyKind.IronCondor);
        var date=new DateOnly(2026,10,26); var at=new DateTime(2026,10,26,1,0,0,DateTimeKind.Utc);
        var mark=new StrategyPositionSnapshot { Id=id,StrategyKind=TradeStrategyKind.IronCondor,IsOpen=true,
            Phase=StrategyPositionPhase.MarkToMarket,PositionSequence=10,HistoryPositionSequence=1,
            ValueDate=date,AsOfUtc=at,HistoryAsOfUtc=at,LastMarketObservationUtc=at,
            ContractCashMultiplier=50,OpeningCommission=0,OpeningValueDate=date,
            Legs=[new() { TradeLegId=Guid.NewGuid(),ContractId="historical-ES",SignedQuantity=1,OpeningPrice=2,CurrentPrice=3,CashMultiplier=50 }] };
        var current=mark with { Phase=StrategyPositionPhase.Close,PositionSequence=100,HistoryPositionSequence=100,
            IsOpen=false,ValueDate=new(2026,11,6),AsOfUtc=at.AddDays(11),HistoryAsOfUtc=at.AddDays(11) };
        var command=new TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.EndOfDayIronCondorPositionCommand {
            CommandId=Guid.NewGuid(),EntityId=id,ValueDate=date,
            Subject=new(ActorType.Command,"FuturesIronCondorTradePositionCommand","EndOfDayIronCondorPosition",id.Format()),
            EffectiveAtUtc=TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime,
            HistoricalSnapshot=mark };
        var original=new TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorPositionChangedEvent {
            Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=id,PositionSnapshot=mark,ReceivedOn=at };
        await fixture.Transactions.ExecuteOnceAsync(async (db,ct)=> {
            await db.AppendAsync(new EnlistedEventAppend(command.StreamId,original.CommandId,original,0),ct);
            await context.ProjectPositionAsync(db,mark,ct);
            await context.ProjectPositionAsync(db,current,ct);
            return true;
        });
        var sourceId=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync(
            "SELECT e.eventversion FROM event_log e JOIN event_stream_id s USING(eventstreamid) WHERE s.eventstream=$1 AND e.streamversion=1;",[command.StreamId],ct));
        command=command with { HistoricalSourceEventId=Convert.ToInt64(sourceId) };
        var sealedHistory=TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.HistoricalPositionEndOfDay.Seal(mark,current,date,command.EffectiveAtUtc);
        TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorPositionChangedEvent Fact()=>new() {
            CommandId=command.CommandId,EntityId=id,PositionSnapshot=current,HistoricalSourceEventId=command.HistoricalSourceEventId,
            HistoricalEndOfDay=sealedHistory,ReceivedOn=DateTime.UtcNow };
        var forged=command with { HistoricalSnapshot=mark with { UnrealizedPnl=999 } };
        Func<Task> reject=()=>TomasAI.IFM.Domain.Trade.Persistence.TradeAtomicPersistence.CommitAsync(forged,
            new TomasAI.IFM.Shared.EventSourcing.DomainEventCollection([Fact()]),1,source,context,fixture.Transactions);
        await reject.Should().ThrowAsync<InvalidOperationException>();
        await TomasAI.IFM.Domain.Trade.Persistence.TradeAtomicPersistence.CommitAsync(command,
            new TomasAI.IFM.Shared.EventSourcing.DomainEventCollection([Fact()]),1,source,context,fixture.Transactions);
        var saved=await context.GetStrategyPositionAsync(id);
        saved!.IsOpen.Should().BeFalse(); saved.PositionSequence.Should().Be(100);
        var history=await context.GetStrategyPositionHistoryAsync(id.PositionId,at.AddHours(-1),at.AddDays(12),100);
        history.Items.Should().HaveCount(2);
        history.Items.Should().ContainSingle(p=>p.Phase==StrategyPositionPhase.EndOfDay);
        (await context.GetOpenPositionRoutesAsync("historical-ES")).Should().BeEmpty();
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(2,"forged source must roll back the event append");
    }

    [Fact]
    public async Task Stored_option_trade_lookup_binds_ordinary_values_and_filters_both_keys()
    {
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], ct));
        var settings = new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection, fixture.Connection, "System.Data.Postgres");
        var context = new TradeDbContext(settings, Mock.Of<IDbContextFactory>(), Mock.Of<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        var orderId = Random.Shared.Next(60_000_000, 70_000_000);
        const int tradeId = 7;
        Assert.Null(await context.GetOptionTradeAsync(orderId, tradeId));
        Assert.Null(await context.GetOptionTradeAsync(orderId, tradeId, System.Threading.CancellationToken.None));
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(
            "INSERT INTO trade.option_trade (orderid,tradeid) VALUES ($1,$2),($1,$3)", [orderId, tradeId, tradeId + 1], ct));
        var ids = await context.Use("StoredTrade.BindingRegression", TradeDbSql.GetOptionTrade)
            .SetParameters(new GetOptionTrade(orderId, tradeId))
            .ExecuteQueryAsync(row => (Order: row.GetInt(0), Trade: row.GetInt(1)));
        Assert.Equal((orderId, tradeId), Assert.Single(ids));
    }

    [Fact]
    public async Task Position_history_persists_currency_without_requiring_a_trade_plan()
    {
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], ct));
        var id = new TradeEntityId(Random.Shared.Next(60_000_000, 70_000_000), 1, 1, 1);
        var at = new DateTime(2026, 10, 16, 14, 0, 0, DateTimeKind.Utc);
        var leg = Guid.NewGuid();
        var trade = new EstablishedTradeDefinition { Id = id, EstablishedAtUtc = at, OpeningCommission = 2.60m,
            Legs = [new() { TradeLegId = leg, ContractId = "currency-test", CashMultiplier = 50, SignedQuantity = -2 }] };
        var position = new StrategyPositionSnapshot { Id = new(id, Guid.NewGuid()), ValueDate = new(2026, 10, 16),
            Phase = StrategyPositionPhase.EndOfDay, PositionSequence = 1, RouteGeneration = 1, AsOfUtc = at,
            DailyPnlPoints = .60m, Legs = [new() { TradeLegId = leg, ContractId = "currency-test", SignedQuantity = -2 }] };
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            await db.ExecuteAsync("INSERT INTO trade.established_trade(portfolioid,fundid,orderid,tradeid,payload) VALUES($1,$2,$3,$4,$5);",
                [id.PortfolioId,id.FundId,id.OrderId,id.TradeId,MessagePackBinarySerializer.Shared.Serialize(trade)], ct);
            await TradeDbContextExtensions.TradeFlowProjectPositionAsync(db, position, ct);
            return true;
        });
        var payload = await fixture.Transactions.ExecuteAsync((db, ct) => db.ScalarAsync(
            "SELECT payload FROM trade.strategy_position_history WHERE positionid=$1;", [position.Id.PositionId], ct));
        var stored = MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>((byte[])payload!);
        stored.DailyPnl.Should().Be(27.40m);
        stored.DailyPnlPoints.Should().Be(.60m);
        stored.ContractCashMultiplier.Should().Be(50m);
        StrategyPositionCurrencyPnl.Apply(stored, trade).DailyPnl.Should().Be(27.40m);
    }

    [Fact]
    public async Task Order_lifecycle_advances_within_one_definition_revision_and_conflicting_terms_roll_back()
    {
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], ct));
        var id = new TradeOrderId(Random.Shared.Next(50_000_000, 60_000_000), 1, 1);
        var accepted = DateTime.UtcNow;
        var order = new TradeOrderDefinition { Id = id, Revision = 1, DefinitionHash = "immutable-terms" };
        var stream = "lifecycle-projection-" + Guid.NewGuid().ToString("N");
        long version = 0;
        async Task Save(TradeOrderDefinition value) => await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            var commandId = Guid.NewGuid();
            await db.AppendAsync(new EnlistedEventAppend(stream, commandId, new TradeOrderChangedEvent
            { Id = Guid.NewGuid(), EntityId = id, CommandId = commandId, TradeOrderDefinition = value, ReceivedOn = accepted }, version), ct);
            await db.TradeFlowProjectOrderLifecycleAsync(value, accepted, ct);
            return true;
        });
        foreach (var status in new[] { TradeOrderStatus.Draft, TradeOrderStatus.Approved, TradeOrderStatus.Ready, TradeOrderStatus.Executing })
        {
            order = order with { Status = status };
            if (status == TradeOrderStatus.Executing)
                order = order with { BoundExecutionAttemptId = Guid.NewGuid(), BoundExecutionChannel = ExecutionChannel.Broker, ExecutionBoundAtUtc = accepted };
            await Save(order);
            version++;
        }
        var conflict = order with { DefinitionHash = "changed-terms" };
        await Assert.ThrowsAsync<System.IO.InvalidDataException>(() => Save(conflict));
        (await fixture.EventCountAsync(stream)).Should().Be(4);
        var payload = await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ScalarAsync(
            "SELECT payload FROM trade.trade_order WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND revision=1;",
            [id.PortfolioId, id.FundId, id.OrderId], ct));
        MessagePackBinarySerializer.Shared.Deserialize<TradeOrderDefinition>((byte[])payload!).Should().BeEquivalentTo(order);
    }

    [Fact]
    public async Task EOD_retry_can_resume_matching_orphan_audit_but_not_change_payload_or_repeat_committed_events()
    {
        var id = new StrategyPositionId(new(999, 1, 1, 1), Guid.NewGuid());
        var boundary = DateTime.UtcNow;
        var command = new EndOfDayFuturesPositionCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, ValueDate = DateOnly.FromDateTime(boundary), EffectiveAtUtc = boundary,
            Subject = new(ActorType.Command, FuturesPositionActorNames.Command, EndOfDayFuturesPositionCommand.Verb, id.Format())
        };
        // Reproduce the old separate audit reservation without committing a source fact.
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) => { await db.ReserveCommandAsync(command, ct); return true; });
        var conflict = () => fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        { await db.ReserveCommandAsync(command with { EffectiveAtUtc = boundary.AddSeconds(1) }, ct); return true; });
        await FluentActions.Awaiting(conflict).Should().ThrowAsync<CommandAuditPayloadConflictException>();
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(0);
        var source = new FuturesPositionChangedEvent
        {
            Id = Guid.NewGuid(), EntityId = id, CommandId = command.CommandId, ReceivedOn = boundary,
            PositionSnapshot = new() { Id = id, StrategyKind = TradeStrategyKind.FuturesOutright, ValueDate = command.ValueDate,
                LatestSealedValueDate = command.ValueDate, CloseBoundaryUtc = boundary, PositionSequence = 1, Phase = StrategyPositionPhase.EndOfDay }
        };
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            await db.ReserveCommandAsync(command, ct);
            await db.AppendAsync(new EnlistedEventAppend(command.StreamId, command.CommandId, source, 0), ct);
            return true;
        });
        var duplicate = () => fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        { await db.ReserveCommandAsync(command, ct); return true; });
        await FluentActions.Awaiting(duplicate).Should().ThrowAsync<CommandAuditDuplicateException>();
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Order_and_source_commit_together_or_rollback_together(bool injectFailure)
    {
        await fixture.Transactions.ExecuteAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var id=new TradeOrderId(Random.Shared.Next(10_000_000,20_000_000),1,1);
        var acceptedAt=DateTime.UtcNow;
        var order=new TradeOrderDefinition { Id=id,Revision=1,Status=TradeOrderStatus.Draft,ValueDate=DateOnly.FromDateTime(acceptedAt),ValidUntilUtc=acceptedAt.AddMinutes(5) };
        var source=new TradeOrderChangedEvent { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=id,TradeOrderDefinition=order,ReceivedOn=acceptedAt };
        var stream="native-trade-order-test-"+source.Id;

        async Task Write()=>await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await db.AppendAsync(new EnlistedEventAppend(stream,source.CommandId,source,0),ct);
            await TradeDbContextExtensions.TradeFlowProjectOrderAsync(db,order,source.ReceivedOn,ct);
            if(injectFailure) throw new InvalidOperationException("Injected after projection before commit.");
            return true;
        });
        if(injectFailure) await FluentActions.Awaiting(Write).Should().ThrowAsync<InvalidOperationException>();
        else await Write();
        (await fixture.EventCountAsync(stream)).Should().Be(injectFailure?0:1);
        var payload=await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT payload FROM trade.trade_order WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND revision=$4;",[id.PortfolioId,id.FundId,id.OrderId,1],ct));
        if(injectFailure) payload.Should().BeNull();
        else
        {
            MessagePackBinarySerializer.Shared.Deserialize<TradeOrderDefinition>((byte[])payload!).Should().BeEquivalentTo(order);
            var first=await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT to_jsonb(t)::text FROM trade.trade_order t WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND revision=$4;",[id.PortfolioId,id.FundId,id.OrderId,1],ct));
            await fixture.Transactions.ExecuteAsync((db,ct)=>TradeDbContextExtensions.TradeFlowProjectOrderAsync(db,source.TradeOrderDefinition,source.ReceivedOn,ct));
            var repeated=await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT to_jsonb(t)::text FROM trade.trade_order t WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND revision=$4;",[id.PortfolioId,id.FundId,id.OrderId,1],ct));
            repeated.Should().Be(first);
            await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.TradeFlowProjectOrderAsync(db,order with { Origin="Conflicting same revision" },source.ReceivedOn,ct)))
                .Should().ThrowAsync<System.IO.InvalidDataException>();
        }
    }

    [Fact]
    public async Task Established_trade_revisions_are_immutable_and_stale_history_cannot_replace_current_state()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var trade = new EstablishedTradeDefinition
        {
            Id = new TradeEntityId(Random.Shared.Next(60_000_000,70_000_000),1,1,1),
            StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = DateTime.UtcNow,
            EvidenceRevision = 1,
            OpeningValue = 100
        };

        async Task Write(EstablishedTradeDefinition value) => await fixture.Transactions.ExecuteOnceAsync(async (db,ct) =>
        {
            await TradeDbContextExtensions.TradeFlowProjectTradeAsync(db,value,ct);
            return true;
        });
        async Task<EstablishedTradeDefinition> Read() => MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(
            (byte[])(await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync(
                "SELECT payload FROM trade.established_trade WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4;",
                [trade.Id.PortfolioId,trade.Id.FundId,trade.Id.OrderId,trade.Id.TradeId],ct)))!);
        await Write(trade);
        await Write(trade);
        await FluentActions.Awaiting(()=>Write(trade with { OpeningValue=101 })).Should().ThrowAsync<System.IO.InvalidDataException>();
        (await Read()).Should().BeEquivalentTo(trade);
        // Even a changed history-key timestamp cannot admit different facts for the same current revision.
        await FluentActions.Awaiting(()=>Write(trade with { EstablishedAtUtc=trade.EstablishedAtUtc.AddSeconds(1) }))
            .Should().ThrowAsync<System.IO.InvalidDataException>();
        var later=trade with { EvidenceRevision=2, OpeningValue=110 };
        await Write(later);
        await Write(trade);
        (await Read()).Should().BeEquivalentTo(later);
    }

    [Fact]
    public async Task Position_daily_history_routes_and_close_are_atomic_and_stale_marks_cannot_restore_routes()
    {
        await fixture.Transactions.ExecuteAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var now=DateTime.UtcNow;var id=new StrategyPositionId(new TradeEntityId(Random.Shared.Next(20_000_000,30_000_000),1,1,1),Guid.NewGuid());

        var opening=new StrategyPositionSnapshot { Id=id,StrategyKind=TradeStrategyKind.IronCondor,Phase=StrategyPositionPhase.Open,
            PositionSequence=1,RouteGeneration=1,AsOfUtc=now,ValueDate=DateOnly.FromDateTime(now),IsOpen=true,
            Legs=[new StrategyPositionLeg { TradeLegId=Guid.NewGuid(),ContractId="native-route-"+id.PositionId,SignedQuantity=1 }] };
        async Task Write(StrategyPositionSnapshot position)=>await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=> { await TradeDbContextExtensions.TradeFlowProjectPositionAsync(db,position,ct); return true; });
        async Task<long> Count(string table)=>Convert.ToInt64(await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM trade."+table+" WHERE positionId=$1;",[id.PositionId],ct)));
        await Write(opening);
        var daily=opening with { PositionSequence=2,Phase=StrategyPositionPhase.MarkToMarket,AsOfUtc=now.AddSeconds(1),HistoryAsOfUtc=now.AddSeconds(1),HistoryPositionSequence=2,DailyPnl=-10 };
        await Write(daily);await Write(daily with {PositionSequence=3,AsOfUtc=now.AddSeconds(2),DailyPnl=-20});
        var eod=daily with {PositionSequence=4,Phase=StrategyPositionPhase.EndOfDay,AsOfUtc=now.AddSeconds(3),DailyPnl=-25};
        await Write(eod);(await Count("strategy_position_history")).Should().Be(2);
        var row=await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT payload FROM trade.strategy_position_history WHERE positionId=$1 AND positionSequence=2;",[id.PositionId],ct));
        MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>((byte[])row!).Should().BeEquivalentTo(eod);
        var closed=eod with {PositionSequence=5,Phase=StrategyPositionPhase.Close,AsOfUtc=now.AddSeconds(4),HistoryAsOfUtc=now.AddSeconds(4),HistoryPositionSequence=5,IsOpen=false};
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(db,ct)=> { await TradeDbContextExtensions.TradeFlowProjectPositionAsync(db,closed,ct);throw new InvalidOperationException("Close rollback"); })).Should().ThrowAsync<InvalidOperationException>();
        (await Count("open_position_route")).Should().Be(1);(await Count("open_position_route_recovery")).Should().Be(1);
        await Write(closed);await Write(daily);await Write(closed);
        (await Count("open_position_route")).Should().Be(0);(await Count("open_position_route_recovery")).Should().Be(0);(await Count("strategy_position_history")).Should().Be(3);
        var current=await fixture.Transactions.ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT payload FROM trade.strategy_position_current WHERE positionId=$1;",[id.PositionId],ct));
        MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>((byte[])current!).Should().BeEquivalentTo(closed);
    }
    [Fact]
    public async Task Position_history_pages_include_all_rows_after_200_and_reject_cross_position_cursors()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var id=new StrategyPositionId(new TradeEntityId(Random.Shared.Next(30_000_000,40_000_000),1,1,1),Guid.NewGuid());
        var from=new DateTime(2026,1,1,12,0,0,DateTimeKind.Utc);var to=from.AddDays(220);

        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            for(var index=0;index<205;index++)
            {
                var at=from.AddDays(index);
                await TradeDbContextExtensions.TradeFlowProjectPositionAsync(db,new StrategyPositionSnapshot {Id=id,StrategyKind=TradeStrategyKind.IronCondor,
                    PositionSequence=index+1,RouteGeneration=1,AsOfUtc=at,ValueDate=DateOnly.FromDateTime(at),
                    Phase=index==0?StrategyPositionPhase.Open:StrategyPositionPhase.EndOfDay,IsOpen=true,DailyPnl=-index},ct);
            }
            return true;
        });
        var seen=new List<StrategyPositionSnapshot>();byte[]? cursor=null;byte[]? firstCursor=null;
        do
        {
            var page=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.TradeReadPositionHistoryAsync(db,id.PositionId,from,to,17,cursor,ct));
            page.Items.Length.Should().BeLessThanOrEqualTo(17);seen.AddRange(page.Items);
            cursor=page.PagingState;firstCursor??=cursor;
        }while(cursor is not null);
        seen.Should().HaveCount(205);seen.Select(x=>x.PositionSequence).Should().Equal(Enumerable.Range(1,205).Select(x=>(long)x));
        seen[0].Phase.Should().Be(StrategyPositionPhase.Open);
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.TradeReadPositionHistoryAsync(db,Guid.NewGuid(),from,to,17,firstCursor,ct))).Should().ThrowAsync<ArgumentException>();
        var current=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>TradeDbContextExtensions.TradeReadPositionAsync(db,id,ct));
        current!.PositionSequence.Should().Be(205);
    }

    [Fact]
    public async Task Native_context_loads_latest_order_position_history_and_committed_routes()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var settings=new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory=new Mock<IDbContextFactory>();var context=new TradeDbContext(settings,factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        factory.SetupGet(x=>x.TradeDb).Returns(context);
        var trade=new TradeEntityId(Random.Shared.Next(40_000_000,50_000_000),1,1,1);var now=DateTime.UtcNow;
        var order=new TradeOrderDefinition {Id=new TradeOrderId(trade.PortfolioId,trade.FundId,trade.OrderId),Revision=1,DefinitionHash="native-1"};
        await context.UpsertTradeOrderAsync(order,now);
        await context.UpsertTradeOrderAsync(order with {Revision=2,DefinitionHash="native-2"},now.AddSeconds(1));
        (await context.GetTradeOrderAsync(order.Id))!.Revision.Should().Be(2);
        var contract="native-context-"+Guid.NewGuid().ToString("N");
        var position=new StrategyPositionSnapshot {Id=new StrategyPositionId(trade,Guid.NewGuid()),StrategyKind=TradeStrategyKind.IronCondor,
            PositionSequence=1,RouteGeneration=1,AsOfUtc=now,ValueDate=DateOnly.FromDateTime(now),IsOpen=true,Phase=StrategyPositionPhase.Open,
            Legs=[new StrategyPositionLeg {TradeLegId=Guid.NewGuid(),ContractId=contract,SignedQuantity=1}]};
        await context.UpsertStrategyPositionAsync(position);await context.ReplaceOpenPositionRoutesAsync(position);
        (await context.GetStrategyPositionAsync(position.Id)).Should().BeEquivalentTo(position);
        (await context.GetStrategyPositionHistoryAsync(position.Id.PositionId,now.AddSeconds(-1),now.AddMinutes(1),10)).Items.Should().ContainSingle();
        (await context.GetOpenPositionRoutesAsync(contract)).Should().ContainSingle();
        (await context.GetOpenPositionRouteSnapshotAsync()).Should().Contain(x=>x.Route.StrategyPositionId==position.Id.PositionId);
        var page=await context.GetOpenPositionRoutePageAsync(2);page.Items.Should().HaveCountLessThanOrEqualTo(2);
        var closed=position with {PositionSequence=2,AsOfUtc=now.AddSeconds(2),IsOpen=false,Phase=StrategyPositionPhase.Close};
        await context.UpsertStrategyPositionAsync(closed);await context.ReplaceOpenPositionRoutesAsync(closed);
        (await context.GetOpenPositionRoutesAsync(contract)).Should().BeEmpty();
    }

    [Fact]
    public async Task Audit_only_reservation_is_not_a_committed_duplicate()
    {
        var id = new TradeOrderId(Random.Shared.Next(50_000_000, 60_000_000), 1, 1);
        var command = new CreateTradeOrderCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, CreateTradeOrderCommand.Actor, CreateTradeOrderCommand.Verb, id.Format())
        };
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        { await db.ReserveCommandAsync(command, ct); return true; });
        var duplicate = await Assert.ThrowsAsync<CommandAuditDuplicateException>(() =>
            fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
            { await db.ReserveCommandAsync(command, ct); return true; }));
        duplicate.HasCommittedEvents.Should().BeFalse();
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Command_audit_source_and_order_share_commit_and_duplicate_boundary(bool fail)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var id=new TradeOrderId(Random.Shared.Next(50_000_000,60_000_000),1,1);var now=DateTime.UtcNow;
        var order=new TradeOrderDefinition {Id=id,Revision=1,DefinitionHash="audit-order"};
        var command=new CreateTradeOrderCommand {CommandId=Guid.NewGuid(),EntityId=id,Order=order,
            Subject=new ActorSubject(ActorType.Command,CreateTradeOrderCommand.Actor,CreateTradeOrderCommand.Verb,id.Format())};
        var source=new TradeOrderChangedEvent {Id=Guid.NewGuid(),EntityId=id,TradeOrderDefinition=order,CommandId=command.CommandId,ReceivedOn=now};
        async Task Save()=>await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await db.ReserveCommandAsync(command,ct);
            await db.AppendAsync(new EnlistedEventAppend(command.StreamId,command.CommandId,source,0),ct);
            await TradeDbContextExtensions.TradeFlowProjectOrderAsync(db,order,now,ct);
            if(fail) throw new InvalidOperationException("Injected audit/source/material rollback");
            return true;
        });
        if(fail) await FluentActions.Awaiting(Save).Should().ThrowAsync<InvalidOperationException>();else await Save();
        (await fixture.EventCountAsync(command.StreamId)).Should().Be(fail?0:1);
        var count=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM command_log WHERE commandid=$1;",[command.CommandId],ct));
        Convert.ToInt64(count).Should().Be(fail?0:1);
        if (fail)
        {
            fail = false;
            await Save();
            (await fixture.EventCountAsync(command.StreamId)).Should().Be(1);
        }
        if(!fail)
        {
            var duplicate = await Assert.ThrowsAsync<CommandAuditDuplicateException>(Save);
            duplicate.HasCommittedEvents.Should().BeTrue();
            await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await db.ReserveCommandAsync(command with {Order=order with {DefinitionHash="conflicting-command"}},ct);return true;})).Should().ThrowAsync<CommandAuditPayloadConflictException>();
            (await fixture.EventCountAsync(command.StreamId)).Should().Be(1);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Monitoring_limits_commit_together_and_saved_values_are_available_without_live_feed(bool fail)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var tradeId=Random.Shared.Next(70_000_000,80_000_000);var now=DateTime.UtcNow;
        var limit=new TradeLimitReadModel {TradeId=tradeId,TradeType=TradeType.ShortIronCondor,RiskMargin=1200.12m,MaxProfit=100.10m,MaxLoss=900.90m,
            MaxReturn=0.25m,MaxLossLimit=200.20m,MinProfitLimit=20.02m,MaxProfitLimit=80.08m,MinProfitTarget=30.03m,DailyProfitTarget=10.01m,
            CreatedOn=now,UpdatedOn=now,CreatedBy="fixture",UpdatedBy="fixture"};
        var spreads=new[] {new TradeTypeLimitReadModel {TradeId=tradeId,TradeType=TradeType.PutCreditSpread,MaxLossLimit=110.11m,MinProfitLimit=11.01m,MaxProfitLimit=40.04m},
            new TradeTypeLimitReadModel {TradeId=tradeId,TradeType=TradeType.CallCreditSpread,MaxLossLimit=90.09m,MinProfitLimit=9.01m,MaxProfitLimit=40.04m}};

        async Task Save()=>await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await TradeDbContextExtensions.TradeFlowProjectMonitoringLimitsAsync(db,limit,spreads,ct);
            if(fail) throw new InvalidOperationException("Injected limit projection rollback");
            return true;
        });
        if(fail) await FluentActions.Awaiting(Save).Should().ThrowAsync<InvalidOperationException>();else await Save();
        var settings=new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory=new Mock<IDbContextFactory>();var context=new TradeDbContext(settings,factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);factory.SetupGet(x=>x.TradeDb).Returns(context);
        var stored=await context.GetTradeLimitAsync(tradeId);var storedSpreads=await context.GetTradeTypeLimitsAsync(tradeId);
        if(fail) {stored.Should().BeNull();storedSpreads.Should().BeEmpty();}
        else
        {
            stored!.MaxLossLimit.Should().Be(limit.MaxLossLimit);stored.RiskMargin.Should().Be(limit.RiskMargin);
            storedSpreads.Should().BeEquivalentTo(spreads);
            await context.ProjectMonitoringLimitsAsync(limit,spreads);
            (await context.GetTradeTypeLimitsAsync(tradeId)).Should().HaveCount(2);
            (await context.GetTradeTypeLimitAsync(tradeId,TradeType.PutCreditSpread)).Should().BeEquivalentTo(spreads[0]);
        }
    }

    [Fact]
    public async Task Established_trade_and_approved_limits_rollback_commit_and_replay_together()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(TradeSchemaDb.CreateStatement,[],ct));
        var settings = new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory = new Mock<IDbContextFactory>();
        var context = new TradeDbContext(settings,factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        factory.SetupGet(x=>x.TradeDb).Returns(context);
        var now = DateTime.UtcNow;
        var id = new TradeEntityId(Random.Shared.Next(50_000_000,60_000_000), 1, 1, Random.Shared.Next(70_000_000,80_000_000));
        var limit = new TradeLimitReadModel { TradeId = id.TradeId, TradeType = TradeType.ShortIronCondor,
            RiskMargin = 5000, MaxLoss = -700, MaxProfit = 1300, CreatedOn = now, UpdatedOn = now, CreatedBy = "PortfolioApproval", UpdatedBy = "PortfolioApproval" };
        var snapshot = new EstablishedTradeMonitoringLimits { PortfolioApprovalId = Guid.NewGuid(), OrderRevision = 1,
            DefinitionHash = "approved", TradeLimit = limit,
            SpreadLimits = [new(id.TradeId,TradeType.PutCreditSpread,16,2,2),new(id.TradeId,TradeType.CallCreditSpread,10,1.25m,1.25m)] };
        var trade = new EstablishedTradeDefinition { Id = id, StrategyKind = TradeStrategyKind.IronCondor,
            AssetFamily = TradeAssetFamily.FuturesOption, EvidenceRevision = 1, EstablishedAtUtc = now, MonitoringLimits = snapshot };
        var stream = "approved-limits-" + Guid.NewGuid().ToString("N");
        var commandId = Guid.NewGuid();
        var source = new TomasAI.IFM.Domain.Trade.Shared.Futures.Option.OptionTradeChangedEvent
            { Id = Guid.NewGuid(), EntityId = id, CommandId = commandId, ReceivedOn = now, State = trade };
        async Task Save(bool fail) => await fixture.Transactions.ExecuteOnceAsync(async (db,ct) =>
        {
            await db.AppendAsync(new EnlistedEventAppend(stream,commandId,source,0),ct);
            await db.TradeFlowProjectTradeAsync(trade,ct);
            if (fail) throw new InvalidOperationException("Injected failure after trade and limits");
            return true;
        });
        await FluentActions.Awaiting(() => Save(true)).Should().ThrowAsync<InvalidOperationException>();
        (await context.GetEstablishedTradeAsync(id)).Should().BeNull();
        (await context.GetTradeLimitAsync(id.TradeId)).Should().BeNull();
        (await context.GetTradeTypeLimitsAsync(id.TradeId)).Should().BeEmpty();
        (await fixture.EventCountAsync(stream)).Should().Be(0);
        await Save(false);
        (await context.GetEstablishedTradeAsync(id))!.MonitoringLimits.Should().BeEquivalentTo(snapshot);
        (await context.GetTradeLimitAsync(id.TradeId))!.MaxLoss.Should().Be(-700);
        (await context.GetTradeTypeLimitsAsync(id.TradeId)).Should().HaveCount(2);
        // Rebuilding the trade must not overwrite subsequent authoritative monitoring adjustments.
        await context.ProjectMonitoringLimitsAsync(limit with { MaxLoss = -600 },snapshot.SpreadLimits);
        await context.UpsertEstablishedTradeAsync(trade);
        (await context.GetTradeLimitAsync(id.TradeId))!.MaxLoss.Should().Be(-600);
    }

}
