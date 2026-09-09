using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.FundDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-06")]
public sealed class LegacyFinancialInventoryIntegrationTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Theory]
    [InlineData("OpeningBalanceWithHistory")]
    [InlineData("ReadOnlyHistoryWithDevelopmentCapital")]
    public async Task Canonical_scylla_source_streams_into_replayable_postgres_quarantine_without_changing_money(string mode)
    {
        _=fixture;await new PortfolioFinancialSchema(Transactions()).InitializeAsync();
        var settings=new DbConnectionSettings().Add(FundDbContext.FundDbConnection,"Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db","System.Data.ScyllaDb");
        var logger=Substitute.For<ILogger<DbProvider>>();await new FundSchemaDb(settings,logger).CreateAllAsync();
        var repositories=new Dictionary<Type,object>();var factory=new DbContextFactory(new DbContextResolver(type=>repositories[type]));
        var sequences=Substitute.For<ISequenceIdGenerator>();long next=Random.Shared.Next(100000,900000000);
        sequences.GetSequenceIdAsync(Arg.Any<SequenceName>(),Arg.Any<CancellationToken>()).Returns(_=>new ValueTask<long>(Interlocked.Increment(ref next)));
        var fence=new LegacyFinancialWriterFence(Transactions());
        var source=new FundDbContext(settings,factory,sequences,logger,fence);repositories.Add(typeof(IObjectRepository<FundDbContext>),source);
        var id=Random.Shared.Next(100000,900000000);var when=new DateTime(2026,9,8,12,0,0,DateTimeKind.Utc);
        var row=new FundTransactionReadModel(0,when,FundTransactionType.OpeningTrade,id,1,1,TradeType.LongIronCondor,new(2026,9,8),TradeStatus.Open,"Owned migration fixture",10000,1000000);
        var retained=false;
        try
        {
            await source.InsertFundTransactionAsync(row);
            var scope=new LegacyFinancialInventoryScope(Guid.NewGuid(),id,id+1,id+2,new(2026,9,1),new(2026,9,30),"Scylla integration fixture",mode);
            var inventory=new LegacyFinancialInventory(source,new(Transactions()));
            var first=await inventory.RunAsync(scope,default);var replay=await inventory.RunAsync(scope,default);
            replay.Should().Be(first);first.Rows.Should().Be(1);first.Quarantined.Should().Be(mode=="OpeningBalanceWithHistory"?1:0);first.State.Should().Be("UnfencedInventory");
            var saved=await Transactions().ExecuteAsync((db,ct)=>db.QueryAsync("SELECT payload::text,reason FROM portfolio_financial.legacy_financial_inventory_row WHERE inventory_id=$1;",
                [scope.InventoryId],r=>(Payload:r.GetString(0),Reason:r.GetString(1)),ct));
            saved.Single().Reason.Should().Be(mode=="OpeningBalanceWithHistory"?"LEGACY.CURRENCY.UNQUALIFIED":"LEGACY.RETAINED_READ_ONLY.NO_CAPITAL");saved.Single().Payload.Should().Contain("1000000");
            (await source.HasPendingLegacyFinancialWritesAsync(id,scope.Start,scope.End)).Should().BeFalse();
            (await source.HasLegacyFinancialRecordsOutsideRangeAsync(id,scope.Start,scope.End)).Should().BeFalse();
            (await source.HasLegacyFinancialRecordsOutsideRangeAsync(id,new(2026,9,9),scope.End)).Should().BeTrue();
            (await source.HasLegacyFinancialRecordsOutsideRangeAsync(id,scope.Start,new(2026,9,7))).Should().BeTrue();
            (await new PortfolioFinancialDbContext(Transactions()).ReadBookAsync(scope.DestinationPortfolioId)).Should().BeNull();
            if(mode=="ReadOnlyHistoryWithDevelopmentCapital")
            {
                var sources=new TomasAI.IFM.Domain.Portfolio.Persistence.PortfolioEventStore(fixture.EventSourceDb);
                var fund=new TomasAI.IFM.Domain.Portfolio.Command.State.PortfolioFundAggregate();var now=DateTime.UtcNow;
                var portfolio=new TomasAI.IFM.Domain.Portfolio.Command.State.PortfolioAggregate();
                var portfolioCreated=portfolio.Create(Guid.NewGuid(),new() { PortfolioId=id+1,PortfolioVersion=1,OperatingState=TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioOperatingState.Draft,Name="Retention fixture",EffectiveFromUtc=now,CreatedOnUtc=now,CreatedBy="test" },now,"test");
                await sources.AppendPortfolioAsync(new(id+1),portfolioCreated,0);
                var added=portfolio.AddFund(Guid.NewGuid(),1,new(id+1,id+2),now,"test");await sources.AppendPortfolioAsync(new(id+1),added,1);
                var created=fund.Create(Guid.NewGuid(),new() { PortfolioId=id+1,FundId=id+2,FundCode=(id+2).ToString(),Name="Retained source fixture",FundMandateVersion=1,
                    OperatingState=TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundOperatingState.Draft,TradingYear=2026,DecisionHorizon="Daily",Objective="Read-only history",UnderlyingUniverse=["ES"],EligibleAssetTypes=["Futures"],
                    PermittedDirections=["Long"],PermittedConditions=["Trending"],PermittedTradeFamilies=["Futures"],EffectiveFromUtc=now,CreatedOnUtc=now,CreatedBy="test",
                    HistoricalSource="FundLegacyDb",HistoricalSourceFundId=id },now,"test");
                await sources.AppendFundAsync(new(id+1,id+2),created,0);
                var service=new LegacyFinancialRetention(source,sources,fence,new(Transactions()),new(Transactions()),new(true));
                var access=new FinancialAccess("retention integration fixture",["LedgerImport"],[id+1]);
                var sealedResult=await service.RetainAsync(scope,access,"Retain without capital conversion");retained=true;
                sealedResult.State.Should().Be("RetainedReadOnly");
                (await service.RetainAsync(scope,access,"Retain without capital conversion")).Should().Be(sealedResult);
                (await new PortfolioFinancialDbContext(Transactions()).ReadBookAsync(id+1)).Should().BeNull();
                await FluentActions.Awaiting(()=>source.InsertFundTransactionAsync(row with { TransactionDate=now.AddMinutes(1) })).Should().ThrowAsync<FinancialOperationException>();
                await FluentActions.Awaiting(()=>Transactions().ExecuteAsync((db,ct)=>db.ExecuteAsync("DELETE FROM portfolio_financial.ledger_migration WHERE migration_id=$1;",[scope.InventoryId],ct))).Should().ThrowAsync<Npgsql.PostgresException>();
            }
        }
        finally { if(!retained && mode!="ReadOnlyHistoryWithDevelopmentCapital") await source.DeleteFundTransactionAsync(id,row.ValueDate,row.OrderId,row.TradeId,row.TradeType,row.TransactionType,when); }
    }

    [Fact]
    public async Task Interrupted_inventory_resumes_original_rows_and_detects_changed_scope_content_and_missing_rows()
    {
        await new PortfolioFinancialSchema(Transactions()).InitializeAsync();var store=new LegacyFinancialInventoryStore(Transactions());
        var scope=new LegacyFinancialInventoryScope(Guid.NewGuid(),1,2,3,new(2026,1,1),new(2026,12,31),"isolated fixture","FullPostedHistory");
        var first=new LegacyFinancialInventoryRow(new('A',64),new('B',64),"{\"Amount\":1.123}","Quarantined","LEGACY.AMOUNT.PRECISION",1.123m);
        await store.BeginAsync(scope,default);await store.SaveRowAsync(scope.InventoryId,first,default);
        await new LegacyFinancialInventoryStore(Transactions()).BeginAsync(scope,default);
        await store.SaveRowAsync(scope.InventoryId,first,default);
        await FluentActions.Awaiting(()=>store.CompleteAsync(scope.InventoryId,0,default)).Should().ThrowAsync<FinancialOperationException>();
        await FluentActions.Awaiting(()=>store.SaveRowAsync(scope.InventoryId,first with { SourcePayload="{\"Amount\":2.123}",Amount=2.123m },default))
            .Should().ThrowAsync<FinancialOperationException>();
        await FluentActions.Awaiting(()=>store.BeginAsync(scope with { SourceFundId=99 },default)).Should().ThrowAsync<FinancialOperationException>();
        var completed=await store.CompleteAsync(scope.InventoryId,1,default);completed.SourceAmount.Should().Be(1.123m);
        await FluentActions.Awaiting(()=>store.SaveRowAsync(scope.InventoryId,first with { SourceKey=new('C',64) },default))
            .Should().ThrowAsync<FinancialOperationException>();
        await FluentActions.Awaiting(()=>Transactions().ExecuteAsync((db,ct)=>db.ExecuteAsync("DELETE FROM portfolio_financial.legacy_financial_inventory_row WHERE inventory_id=$1;",[scope.InventoryId],ct)))
            .Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact]
    public async Task Bounded_hash_pages_are_order_independent_and_detect_no_lost_rows()
    {
        await new PortfolioFinancialSchema(Transactions()).InitializeAsync();var store=new LegacyFinancialInventoryStore(Transactions());
        var scope=new LegacyFinancialInventoryScope(Guid.NewGuid(),1,2,3,new(2026,1,1),new(2026,12,31),"paging fixture","FullPostedHistory");
        await store.BeginAsync(scope,default);
        // Exceeds one 128-row hash page without retaining a source collection in the production scan.
        for(var i=130;i>0;i--)
        {
            var key=FinancialCanonicalHash.Compute(i);
            await store.SaveRowAsync(scope.InventoryId,new(key,key,"{}","Quarantined","LEGACY.SOURCE.UNQUALIFIED",i),default);
        }
        var result=await store.CompleteAsync(scope.InventoryId,130,default);result.Rows.Should().Be(130);result.SourceAmount.Should().Be(8515);
        (await store.CompleteAsync(scope.InventoryId,130,default)).ContentHash.Should().Be(result.ContentHash);
    }
}
