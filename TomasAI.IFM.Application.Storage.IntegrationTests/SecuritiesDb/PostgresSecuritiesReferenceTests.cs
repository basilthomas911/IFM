using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure", "SelfContained")]
public sealed class PostgresSecuritiesReferenceTests(PostgresTradePlanFixture fixture)
{
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
        await context.ReplaceOptionContractDefinitionsAsync(symbol,expiry.AddDays(-7),expiry.AddDays(7),[row]);
        var expiries=await context.GetOptionContractExpiriesAsync(symbol,expiry,expiry);
        expiries.Should().ContainSingle();expiries[0].ContractId.Should().Be("underlying");
        var rows=await context.GetCachedOptionContractDefinitionsAsync(symbol,"underlying",expiry,["EW"]);
        rows.Should().ContainSingle();rows[0].Definition.ContractId.Should().Be(definition.ContractId);
        var store=new PostgresSecuritiesReferenceStore();var generation=Guid.NewGuid();
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(db,ct)=>
        {
            await store.PublishDefinitionsAsync(db,symbol,generation,expiry,expiry,DateTime.UtcNow,[],ct);
            throw new InvalidOperationException("Injected definition refresh failure");
        })).Should().ThrowAsync<InvalidOperationException>();
        (await context.GetCachedOptionContractDefinitionsAsync(symbol,"underlying",expiry)).Should().ContainSingle();
        await context.ReplaceOptionContractDefinitionsAsync(symbol,expiry,expiry,[]);
        (await context.GetCachedOptionContractDefinitionsAsync(symbol,"underlying",expiry)).Should().BeEmpty();
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
            await db.AppendAsync(id,commandId,source,0,ct);
            await context.ProjectOptionContractAsync(db,contract,token:ct);
            return true;
        });
        (await context.GetFuturesOptionContractAsync(id)).Should().BeEquivalentTo(contract);
        var failedId="ES20261120P"+(strike+1);
        var failed=source with {CommandId=Guid.NewGuid(),Contract=contract with {ContractId=failedId,StrikePrice=strike+1}};
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(db,ct)=>
        {
            await db.AppendAsync(failedId,failed.CommandId,failed,0,ct);
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
