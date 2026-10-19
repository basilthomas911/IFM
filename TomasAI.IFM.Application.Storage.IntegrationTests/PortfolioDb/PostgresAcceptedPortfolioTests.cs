using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.PortfolioDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresAcceptedPortfolioTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task New_fund_version_disables_older_projections_and_replay_cannot_reactivate_them()
    {
        var settings=new DbConnectionSettings().Add(PortfolioDbContext.PortfolioDbConnection,fixture.Connection,"System.Data.Postgres");
        await new PortfolioSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var id=Random.Shared.Next(100000000,900000000);var time=DateTime.UtcNow;
        var first=new FundMandateReadModel{PortfolioId=id,FundId=id,FundMandateVersion=1,Name="Versioned Fund",TradingYear=2026,DecisionHorizon="Daily",OperatingState=FundOperatingState.Active,EffectiveFromUtc=time,CreatedOnUtc=time};
        var second=first with {FundMandateVersion=2};
        await fixture.Transactions.ExecuteOnceAsync(async(db,token)=>{
            await db.WriteFundProjectionAsync(PortfolioProjection<FundMandateReadModel>.Create(first,1,1,time),token);
            await db.WriteFundProjectionAsync(PortfolioProjection<FundMandateReadModel>.Create(second,2,2,time),token);
            await db.WriteFundProjectionAsync(PortfolioProjection<FundMandateReadModel>.Create(first,1,1,time),token);
            var rows=await db.QueryAsync("SELECT operating_state,payload_json::text,payload_hash FROM portfolio.fund_by_portfolio WHERE fund_id=$1 ORDER BY fund_mandate_version",[id],r=>(State:r.GetString(0),Value:System.Text.Json.JsonSerializer.Deserialize<FundMandateReadModel>(r.GetString(1))!,Hash:r.GetString(2)),token);
            rows.Should().HaveCount(2);rows[0].State.Should().Be("Disabled");rows[0].Value.OperatingState.Should().Be(FundOperatingState.Disabled);
            rows[0].Hash.Should().Be(PortfolioProjection<FundMandateReadModel>.Create(rows[0].Value,2,2,time).PayloadHash);
            rows[1].State.Should().Be("Active");
            var active=await db.QueryAsync("SELECT fund_mandate_version FROM portfolio.active_fund_by_portfolio_horizon WHERE fund_id=$1",[id],r=>r.GetInt64(0),token);
            active.Should().Equal(2L);
            await db.WriteFundProjectionAsync(PortfolioProjection<FundMandateReadModel>.Create(second with{OperatingState=FundOperatingState.Disabled},3,3,time),token);
            (await db.QueryAsync("SELECT fund_id FROM portfolio.active_fund_by_portfolio_horizon WHERE fund_id=$1",[id],r=>r.GetInt32(0),token)).Should().BeEmpty();
            return true;
        });
    }

    [Fact]
    public async Task Financial_history_context_write_preserves_identity_payload_and_duplicate_delivery()
    {
        var settings = new DbConnectionSettings().Add(PortfolioDbContext.PortfolioDbConnection, fixture.Connection, "System.Data.Postgres");
        await new PortfolioSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var context = new PortfolioDbContext(settings, NullLogger<DbProvider>.Instance);
        var portfolioId = Random.Shared.Next(100000000, 900000000);
        var operationId = Guid.NewGuid();
        var time = DateTime.UtcNow;
        var month = time.Year * 100 + time.Month;
        for (var delivery = 0; delivery < 2; delivery++)
            await context.SaveFinancialHistoryAsync(portfolioId, month, 7, operationId, 123,
                "AcceptedFinancialEvent", time, "{\"amount\":12.5}");
        var rows = await fixture.Transactions.ExecuteOnceAsync((db, token) => db.QueryAsync(
            "SELECT source_event_id,operation_id,financial_revision,payload_json->>'amount' FROM portfolio.financial_operation_by_portfolio_month WHERE portfolio_id=$1;",
            [portfolioId], reader => (Event: reader.GetInt64(0), Operation: reader.GetGuid(1), Revision: reader.GetInt64(2), Amount: reader.GetString(3)), token));
        rows.Should().ContainSingle().Which.Should().Be((123L, operationId, 7L, "12.5"));
    }

    [Fact]
    public async Task Accepted_fund_policy_and_composition_rows_rebuild_without_aggregate_reads()
    {
        var settings=new DbConnectionSettings().Add("PortfolioDbConnection",fixture.Connection,"System.Data.Postgres");
        await new PortfolioSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var id=Random.Shared.Next(100000000,900000000);
        var time=DateTime.UtcNow;
        var workflow=Guid.NewGuid();
        var facts=new PortfolioProjectionFacts {Revision=1,UpdatedAtUtc=time,OrderRevision=1,
            Fund=new(){PortfolioId=id,FundId=id,FundMandateVersion=1,TradingYear=2026,DecisionHorizon="Weekly",OperatingState=FundOperatingState.Active,EffectiveFromUtc=time,CreatedOnUtc=time},
            Assignment=new(){PortfolioId=id,FundId=id,FundMandateVersion=1,TradeTemplateId=Guid.NewGuid(),TradeTemplateVersion=1,EffectiveFromUtc=time,CreatedOnUtc=time},
            Allocation=new(){PortfolioId=id,FundId=id,AllocationVersion=1,EffectiveFromUtc=time,CreatedOnUtc=time},RiskEnvelope=new(){PortfolioId=id,FundId=id,EnvelopeVersion=1,EffectiveFromUtc=time,CreatedOnUtc=time,ExpiresAtUtc=time.AddDays(1)},
            Policies=[new(){PortfolioId=id,PolicyId=id,PolicyVersion=1,OperatingState=PortfolioFinancialPolicyState.Active,EffectiveFromUtc=time,CreatedOnUtc=time}],
            Order=new(){PortfolioId=id,FundId=id,OrderId=id,WorkflowId=workflow,CreatedOnUtc=time,ExpiresAtUtc=time.AddDays(1),Status="TemplateSelected"},
            Trades=[new(){PortfolioId=id,FundId=id,OrderId=id,TradeId=id,LegOrdinal=1,CreatedOnUtc=time}],
            Composition=new(){PortfolioId=id,FundId=id,OrderId=id,WorkflowId=workflow,Status="TemplateSelected",UpdatedOnUtc=time,AggregateVersion=1}};
        var source=new TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events.FundCompositionReservedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),ReceivedOn=time,OccurredOnUtc=time,Revision=1,ProjectionFacts=facts};
        string[] tables=["fund_by_portfolio","active_fund_by_portfolio_horizon","fund_template_assignment","fund_allocation","fund_risk_envelope","portfolio_policy","active_portfolio_policy","fund_order","fund_order_trade","fund_composition"];
        async Task<string[]> RowsAsync(string table)=>(await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT row_to_json(t)::text FROM portfolio."+table+" t WHERE portfolio_id=$1 ORDER BY 1;",[id],r=>r.GetString(0),ct))).ToArray();
        var stream="accepted-fund-"+Guid.NewGuid().ToString("N");
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{
            await db.AppendAsync(stream,source.CommandId,source,0,ct);
            await PortfolioDbContextExtensions.PersistAcceptedPortfolioAsync(db,source,facts,ct);
            return true;
        });
        var original=new System.Collections.Generic.Dictionary<string,string[]>();
        foreach(var table in tables){original[table]=await RowsAsync(table);original[table].Should().HaveCount(1,table);}
        var accepted=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT n.EventTypeName,l.EventVersion,l.EventPayload FROM event_log l JOIN event_name_id n ON n.EventNameId=l.EventNameId JOIN event_stream_id s ON s.EventStreamId=l.EventStreamId WHERE s.EventStream=$1;",[stream],r=>(TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events.FundCompositionReservedEvent)new EventStreamReadModel {EventTypeName=r.GetString(0),EventVersion=r.GetInt64(1),EventData=r.GetFieldValue<byte[]>(2)}.ToDomainEvent(),ct));
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{
            foreach(var table in tables) await db.ExecuteAsync("DELETE FROM portfolio."+table+" WHERE portfolio_id=$1;",[id],ct);
            await PortfolioDbContextExtensions.PersistAcceptedPortfolioAsync(db,accepted.Single(),accepted.Single().ProjectionFacts!,ct);
            await PortfolioDbContextExtensions.PersistAcceptedPortfolioAsync(db,accepted.Single(),accepted.Single().ProjectionFacts!,ct);
            return true;
        });
        foreach(var table in tables)(await RowsAsync(table)).Should().Equal(original[table],table);
    }

    [Fact]
    public async Task Compound_fence_rolls_back_both_streams_and_rows_when_second_append_conflicts()
    {
        var settings=new DbConnectionSettings().Add("PortfolioDbConnection",fixture.Connection,"System.Data.Postgres");
        await new PortfolioSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new TomasAI.IFM.Domain.Portfolio.Financial.Repository.PortfolioFinancialSchema(fixture.Transactions).InitializeAsync();
        var id=Random.Shared.Next(100000000,900000000);
        var time=DateTime.UtcNow;
        var command=Guid.NewGuid();
        var aggregate=new PortfolioAggregate();
        var first=aggregate.Create(command,new PortfolioReadModel {PortfolioId=id,PortfolioVersion=1,Name="compound fence",OperatingState=PortfolioOperatingState.Draft,EffectiveFromUtc=time,CreatedOnUtc=time,CreatedBy="integration"},time,"integration");
        PortfolioAcceptedProjection.Capture(first,aggregate);
        var second=new PortfolioOperatingStateChangedEvent {Id=Guid.NewGuid(),CommandId=command,Revision=1,OccurredOnUtc=time,ReceivedOn=time,
            State=PortfolioOperatingState.Disabled,ProjectionFacts=first.ProjectionFacts! with {Portfolio=aggregate.Current! with {OperatingState=PortfolioOperatingState.Disabled}}};
        var prefix="compound-fence-"+Guid.NewGuid().ToString("N");
        var fence=new TomasAI.IFM.Domain.Portfolio.Financial.Repository.PortfolioAuthorityFence(fixture.Transactions);
        await FluentActions.Awaiting(()=>fence.AppendBatchAsync(id,[new(null,prefix+"-first",first,0,true),new(null,prefix+"-second",second,1,true)]))
            .Should().ThrowAsync<Exception>();
        (await fixture.EventCountAsync(prefix+"-first")).Should().Be(0);
        (await fixture.EventCountAsync(prefix+"-second")).Should().Be(0);
        var count=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM portfolio.portfolio_by_id WHERE portfolio_id=$1;",[id],ct));
        Convert.ToInt64(count).Should().Be(0);
        await fence.AppendBatchAsync(id,[new(null,prefix+"-first",first,0,true),new(null,prefix+"-second",second,0,true)]);
        (await fixture.EventCountAsync(prefix+"-first")).Should().Be(1);
        (await fixture.EventCountAsync(prefix+"-second")).Should().Be(1);
        var rows=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT operating_state,source_event_id FROM portfolio.portfolio_by_id WHERE portfolio_id=$1;",[id],r=>(r.GetString(0),r.GetInt64(1)),ct));
        rows.Single().Should().Be(("Disabled",second.EventId));
    }

    [Fact]
    public void All_accepted_portfolio_event_contracts_retain_projection_facts_on_the_wire()
    {
        var facts=new PortfolioProjectionFacts { Revision=2,UpdatedAtUtc=DateTime.UtcNow,
            Portfolio=new PortfolioReadModel {PortfolioId=101,Name="accepted wire facts"},DeletedTradeIds=[123,456] };
        var types=typeof(IPortfolioDomainEvent).Assembly.GetTypes().Where(t=>!t.IsAbstract && typeof(IEvent).IsAssignableFrom(t)
            && t.GetProperty("ProjectionFacts")?.PropertyType == typeof(PortfolioProjectionFacts)).ToArray();
        types.Should().HaveCount(22);
        foreach (var type in types)
        {
            var source=Activator.CreateInstance(type)!;
            type.GetProperty("ProjectionFacts")!.SetValue(source,facts);
            var encoded=MessagePack.MessagePackSerializer.Serialize(type,source);
            var restored=MessagePack.MessagePackSerializer.Deserialize(type,encoded);
            type.GetProperty("ProjectionFacts")!.GetValue(restored).Should().BeEquivalentTo(facts,type.Name);
        }
    }

    [Fact]
    public async Task Financial_admission_fence_writes_accepted_portfolio_rows_in_its_source_transaction()
    {
        var settings=new DbConnectionSettings().Add("PortfolioDbConnection",fixture.Connection,"System.Data.Postgres");
        await new PortfolioSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new TomasAI.IFM.Domain.Portfolio.Financial.Repository.PortfolioFinancialSchema(fixture.Transactions).InitializeAsync();
        var id=Random.Shared.Next(100000000,900000000);
        var time=DateTime.UtcNow;
        var aggregate=new PortfolioAggregate();
        var source=aggregate.Create(Guid.NewGuid(),new PortfolioReadModel {PortfolioId=id,PortfolioVersion=1,Name="accepted fence",OperatingState=PortfolioOperatingState.Draft,EffectiveFromUtc=time,CreatedOnUtc=time,CreatedBy="integration"},time,"integration");
        PortfolioAcceptedProjection.Capture(source,aggregate);
        var stream="accepted-fence-"+Guid.NewGuid().ToString("N");
        await new TomasAI.IFM.Domain.Portfolio.Financial.Repository.PortfolioAuthorityFence(fixture.Transactions)
            .AppendAsync(id,null,stream,source,0,true);
        (await fixture.EventCountAsync(stream)).Should().Be(1);
        var rows=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT source_event_id,payload_json::text FROM portfolio.portfolio_by_id WHERE portfolio_id=$1;",[id],r=>(r.GetInt64(0),r.GetString(1)),ct));
        rows.Should().HaveCount(1);
        rows.Single().Item1.Should().Be(source.EventId);
        rows.Single().Item2.Should().Contain("accepted fence");
    }

    [Fact]
    public async Task Accepted_portfolio_state_and_source_roll_back_together_and_rebuild_from_exact_persisted_facts()
    {
        var settings=new DbConnectionSettings().Add("PortfolioDbConnection",fixture.Connection,"System.Data.Postgres");
        await new PortfolioSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var id=Random.Shared.Next(100000000,900000000);
        var time=DateTime.UtcNow;
        var aggregate=new PortfolioAggregate();
        var source=aggregate.Create(Guid.NewGuid(),new PortfolioReadModel {PortfolioId=id,PortfolioVersion=1,Name="accepted integration",OperatingState=PortfolioOperatingState.Draft,EffectiveFromUtc=time,CreatedOnUtc=time,CreatedBy="integration"},time,"integration");
        PortfolioAcceptedProjection.Capture(source,aggregate);
        source.ProjectionFacts.Should().NotBeNull();
        var stream="accepted-portfolio-"+Guid.NewGuid().ToString("N");
        async Task<string[]> RowsAsync()=> (await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT row_to_json(t)::text FROM portfolio.portfolio_by_id t WHERE portfolio_id=$1 UNION ALL SELECT row_to_json(t)::text FROM portfolio.portfolio_by_state t WHERE portfolio_id=$1 ORDER BY 1;",[id],r=>r.GetString(0),ct))).ToArray();
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(db,ct)=>{
            await db.AppendAsync(stream,source.CommandId,source,0,ct);
            await PortfolioDbContextExtensions.PersistAcceptedPortfolioAsync(db,source,source.ProjectionFacts!,ct);
            await db.ExecuteAsync("SELECT 1/0;",[],ct);
            return true;
        })).Should().ThrowAsync<Npgsql.PostgresException>();
        (await fixture.EventCountAsync(stream)).Should().Be(0);
        (await RowsAsync()).Should().BeEmpty();
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{
            await db.AppendAsync(stream,source.CommandId,source,0,ct);
            await PortfolioDbContextExtensions.PersistAcceptedPortfolioAsync(db,source,source.ProjectionFacts!,ct);
            return true;
        });
        var original=await RowsAsync();
        original.Should().HaveCount(2);
        var persisted=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT n.EventTypeName,l.EventVersion,l.EventPayload FROM event_log l JOIN event_name_id n ON n.EventNameId=l.EventNameId JOIN event_stream_id s ON s.EventStreamId=l.EventStreamId WHERE s.EventStream=$1;",[stream],r=>(IPortfolioDomainEvent)new EventStreamReadModel {EventTypeName=r.GetString(0),EventVersion=r.GetInt64(1),EventData=r.GetFieldValue<byte[]>(2)}.ToDomainEvent(),ct));
        var accepted=persisted.Single();
        accepted.ProjectionFacts.Should().BeEquivalentTo(source.ProjectionFacts);
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{
            await db.ExecuteAsync("DELETE FROM portfolio.portfolio_by_state WHERE portfolio_id=$1;",[id],ct);
            await db.ExecuteAsync("DELETE FROM portfolio.portfolio_by_id WHERE portfolio_id=$1;",[id],ct);
            await PortfolioDbContextExtensions.PersistAcceptedPortfolioAsync(db,accepted,accepted.ProjectionFacts!,ct);
            await PortfolioDbContextExtensions.PersistAcceptedPortfolioAsync(db,accepted,accepted.ProjectionFacts!,ct);
            return true;
        });
        (await RowsAsync()).Should().Equal(original);
    }
}
