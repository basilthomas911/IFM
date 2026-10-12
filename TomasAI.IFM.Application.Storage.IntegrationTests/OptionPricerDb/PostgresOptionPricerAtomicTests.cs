using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.OptionPricerDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresOptionPricerAtomicTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Accepted_source_pair_rolls_back_together_and_latest_selection_uses_source_order()
    {
        var settings=new DbConnectionSettings().Add(OptionPricerDbContext.ConnectionName,PostgresTradePlanFixture.Connection,"System.Data.Postgres");
        await new OptionPricerSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var factory=Substitute.For<IDbContextFactory>();
        var context=new OptionPricerDbContext(settings,factory,Substitute.For<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        factory.OptionPricerDb.Returns(context);
        var trade=Random.Shared.Next(30_000_000,40_000_000);
        var date=new DateOnly(2026,10,11);
        var put=new SpreadDistributionReadModel(0,trade,date,TradeType.PutCreditSpread,TradeStatus.Open,30,
            12.125,0.16,1234.567890123456789m,12,0.21,0.2,0.85,DateTime.UtcNow);
        var first=new SpreadDistributionInsertedEvent
        { CommandId=Guid.NewGuid(),PutSpreadDistribution=put,CallSpreadDistribution=put with {TradeType=TradeType.CallCreditSpread} };
        var stream="pricing-atomic-"+Guid.NewGuid().ToString("N");
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await db.AppendAsync(stream,first.CommandId,first,0,ct);
            await context.ProjectAcceptedEventAsync(db,first,ct);
            return true;
        });
        var second=first with {CommandId=Guid.NewGuid(),PutSpreadDistribution=put with {ForwardPrice=13},EventId=0};
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await db.AppendAsync(stream,second.CommandId,second,1,ct);
            await context.ProjectAcceptedEventAsync(db,second,ct);
            return true;
        });
        var latest=await context.GetSpreadDistributionAsync(trade,put.TradeType,put.TradeStatus,date,30);
        latest!.ForwardPrice.Should().Be(13);
        latest.LossThreshold.Should().Be(put.LossThreshold);
        var failed=second with {CommandId=Guid.NewGuid(),PutSpreadDistribution=put with {ForwardPrice=99},EventId=0};
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(db,ct)=>
        {
            await db.AppendAsync(stream,failed.CommandId,failed,2,ct);
            await context.ProjectAcceptedEventAsync(db,failed,ct);
            throw new InvalidOperationException("Failure after source and pair materialization");
        })).Should().ThrowAsync<InvalidOperationException>();
        (await fixture.EventCountAsync(stream)).Should().Be(2);
        (await context.GetSpreadDistributionAsync(trade,put.TradeType,put.TradeStatus,date,30))!.ForwardPrice.Should().Be(13);
        // An older event applied later must not replace the newest accepted calculation.
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await context.ProjectAcceptedEventAsync(db,first,ct);
            return true;
        });
        (await context.GetSpreadDistributionAsync(trade,put.TradeType,put.TradeStatus,date,30))!.ForwardPrice.Should().Be(13);
    }
}
