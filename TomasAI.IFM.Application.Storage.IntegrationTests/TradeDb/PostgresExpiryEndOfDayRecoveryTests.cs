using System.Security.Cryptography;
using Moq;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using F=TomasAI.IFM.Tests.Support.ExpiryFixingTestScenario;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;
public sealed partial class PostgresAutomaticPositionEndOfDayRecoveryTests
{
    [Theory]
    [InlineData(TradeStrategyKind.IronCondor)]
    [InlineData(TradeStrategyKind.VerticalSpread)]
    public async Task Expiry_eod_recovery_verifies_reference_and_archive_and_survives_lost_reply_and_restart(TradeStrategyKind kind)
    {
        var db=await Context();var original=F.Position() with{Id=StrategyPositionId.Create(new(Random.Shared.Next(70000000,80000000),1,1,1),kind),StrategyKind=kind};
        var stream=$"Command.{Actor(kind)}.{original.Id.Format()}";
        foreach(var leg in original.Legs)
        {
            var reference=F.Reference(F.Convention(leg.Strike!.Value));
            await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync("INSERT INTO securities.securities_reference_version(contract_id,version,payload,published) VALUES($1,'expiry-test-v1',$2,true) ON CONFLICT DO NOTHING;",
                [leg.ContractId,MessagePackBinarySerializer.Shared.Serialize(reference)],ct));
        }
        var sourceFact=Fact(original);
        await fixture.Transactions.ExecuteOnceAsync(async(tx,ct)=>{await tx.AppendAsync(new EnlistedEventAppend(stream,sourceFact.CommandId,sourceFact,0),ct);await db.ProjectPositionAsync(tx,original,ct);return true;});
        var exact=(await db.ReadSourceAsync(original.Id,F.Date,default))!;
        var quotes=original.Legs.Select(l=>F.Quote(F.Convention(l.Strike!.Value)).Quote).ToArray();
        foreach(var quote in quotes)await db.SaveQuoteAsync(new(quote,F.Quote(F.Convention(quote.ExpiryFixing!.Convention.Strike!.Value)).OriginalRecord),default);
        var source=Mock.Of<IEventSourceActorDbContext>(x=>x.ProviderName=="System.Data.Postgres" && x.ConnectionString==fixture.Connection);
        async Task Commit(StoredPositionClosingQuote[] supplied)
        {
            var result=StoredQuotePositionEndOfDayRecovery.Recover(exact.Snapshot,original,F.Date,F.Boundary,supplied);
            var commandId=Guid.NewGuid();var subject=new ActorSubject(ActorType.Command,Actor(kind),"EndOfDay",original.Id.Format());
            ICommand command=kind==TradeStrategyKind.IronCondor
                ? new EndOfDayIronCondorPositionCommand{CommandId=commandId,Subject=subject,EntityId=original.Id,ValueDate=F.Date,EffectiveAtUtc=F.Boundary,HistoricalSnapshot=exact.Snapshot,HistoricalSourceEventId=exact.EventId,HistoricalClosingQuotes=supplied}
                : new EndOfDayVerticalSpreadPositionCommand{CommandId=commandId,Subject=subject,EntityId=original.Id,ValueDate=F.Date,EffectiveAtUtc=F.Boundary,HistoricalSnapshot=exact.Snapshot,HistoricalSourceEventId=exact.EventId,HistoricalClosingQuotes=supplied};
            PositionChangedEvent fact=Fact(result.Current) switch{
                IronCondorPositionChangedEvent iron=>iron with{CommandId=commandId,HistoricalEndOfDay=result.Seal,HistoricalSourceEventId=exact.EventId},
                VerticalSpreadPositionChangedEvent vertical=>vertical with{CommandId=commandId,HistoricalEndOfDay=result.Seal,HistoricalSourceEventId=exact.EventId},_=>throw new Exception()};
            await TradeAtomicPersistence.CommitAsync(command,new DomainEventCollection([fact]),1,source,db,fixture.Transactions,default);
        }
        try
        {
            var bad=quotes[0] with{EvidenceId="",ExpiryFixing=quotes[0].ExpiryFixing! with{ReferencePayloadSha256=new('b',64)}};
            bad=bad with{EvidenceId=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(bad)))};
            await db.SaveQuoteAsync(new(bad,F.Quote(F.Convention()).OriginalRecord),default);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>Commit([bad,quotes[1]]));
            Assert.Equal(1,await fixture.EventCountAsync(stream));
            // Remove only the deliberately invalid auxiliary evidence before the worker reads this fixture.
            await fixture.Transactions.ExecuteOnceAsync((tx,ct)=>tx.ExecuteAsync("DELETE FROM market_data.position_eod_quote_evidence WHERE evidence_id=$1;",[bad.EvidenceId],ct));
            var commandApi=new Mock<IStrategyPositionCommandApi>(MockBehavior.Strict);
            commandApi.Setup(x=>x.RepairEndOfDayAsync(It.IsAny<StrategyPositionSnapshot>(),It.IsAny<long>(),F.Date,F.Boundary,It.IsAny<StoredPositionClosingQuote[]>(),It.IsAny<CancellationToken>()))
                .Returns(async()=>{await Commit(quotes);throw new TimeoutException("Lost committed expiry correction reply");});
            var provider=new Mock<IPositionClosingQuoteSource>(MockBehavior.Strict);
            var clock=new Clock(new DateTimeOffset(F.Boundary.AddHours(1)));
            var options=new OpenPositionEndOfDayOptions();
            await db.ScheduleAsync(original.Id,F.Date,F.Boundary,default);
            var worker=new PositionEndOfDayRecovery(db,db,provider.Object,commandApi.Object,clock,options);
            Assert.True(await worker.RunOneAsync(default));
            Assert.Equal(2,await fixture.EventCountAsync(stream));
            var current=(await db.GetStrategyPositionAsync(original.Id))!;
            Assert.True(current.IsOpen);Assert.Empty(current.ClosingFills);Assert.Equal(-42m,current.UnrealizedPnl);
            Assert.Equal(original.LastMarketObservationUtc,current.LastMarketObservationUtc);
            Assert.Equal(original.Legs.Select(l=>l.LastPriceAtUtc),current.Legs.Select(l=>l.LastPriceAtUtc));
            clock.Now=clock.Now.AddMinutes(11);
            var restarted=new PositionEndOfDayRecovery(await Context(),db,provider.Object,commandApi.Object,clock,options);
            Assert.True(await restarted.RunOneAsync(default));
            Assert.Equal(2,await fixture.EventCountAsync(stream));Assert.Equal(0,await restarted.GetOutstandingCountAsync(default));
            commandApi.Verify(x=>x.RepairEndOfDayAsync(It.IsAny<StrategyPositionSnapshot>(),It.IsAny<long>(),F.Date,F.Boundary,It.IsAny<StoredPositionClosingQuote[]>(),It.IsAny<CancellationToken>()),Times.Once);
            Assert.True(PositionEndOfDayRecovery.IsQualified(current,F.Date,F.Boundary,TimeSpan.FromMinutes(15)));
            await db.ScheduleAsync(original.Id,F.Date,F.Boundary,default);Assert.True(await restarted.RunOneAsync(default));
            Assert.Equal(2,await fixture.EventCountAsync(stream));
        }
        finally{var saved=(await db.GetStrategyPositionAsync(original.Id))!;await db.UpsertStrategyPositionAsync(saved with{IsOpen=false,PositionSequence=saved.PositionSequence+1});}
    }
}
