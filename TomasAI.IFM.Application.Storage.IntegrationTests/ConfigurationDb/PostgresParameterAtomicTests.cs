using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresParameterAtomicTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Accepted_parameters_audit_assignments_startup_and_cache_policy_share_source_transaction_and_rebuild()
    {
        var sourceSettings = new TomasAI.IFM.Shared.Storage.DbConnectionSettings();
        sourceSettings.Add(ConfigurationDbContext.ConfigurationDbConnection, PostgresTradePlanFixture.Connection, "System.Data.Postgres");
        sourceSettings.Add(MarketDataDbContext.MarketDataDbConnection, PostgresTradePlanFixture.Connection, "System.Data.Postgres");
        await new ConfigurationSchemaDb(sourceSettings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new MarketDataSchemaDb(sourceSettings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var factory = new Mock<IDbContextFactory>();
        var context = new ConfigurationDbContext(sourceSettings, factory.Object, NullLogger<DbProvider>.Instance);
        factory.SetupGet(x => x.ConfigurationDb).Returns(context);
        var id = Guid.NewGuid(); var assignmentId = Guid.NewGuid(); var run = Guid.NewGuid();
        var policy = StrategyOptionChainParameterDefaults.IronCondor(id, Guid.NewGuid(), 1) with { Enabled = true };
        var version = new ParameterSetVersion(new(id, 1, ParameterSchemaRegistry.StrategyOptionChainCacheComponent, policy.Hash()),
            "Atomic policy", "accepted facts", 1, ParameterVersionStatus.Published, policy.Serialize(), DateTime.UtcNow, "integration", CatalogRevision: 1);
        var set = new ParameterSetCreatedEvent { CommandId = Guid.NewGuid(), EntityId = new(id), Revision = 1,
            RequestHash = new string('a',64), VersionJson = JsonSerializer.Serialize(version) };
        set = set with { AuditJson = JsonSerializer.Serialize(new ParameterAuditEntry(set.CommandId,id,1,"Create","integration",DateTime.UtcNow,"null",JsonSerializer.Serialize(version.Reference))) };
        var assignment = new ParameterAssignmentRevision(assignmentId, new("integration",id.ToString(),"risk",version.Reference.ComponentCode,"{}",new string('b',64)),
            1,version.Reference,true,ParameterApplicationPolicy.NextStartup,DateTime.UtcNow,"integration");
        var changed = new ParameterAssignmentChangedEvent { CommandId=Guid.NewGuid(),EntityId=new(assignmentId),Revision=1,RequestHash=new string('c',64),AssignmentJson=JsonSerializer.Serialize(assignment) };
        var startup = new ParameterStartupChangedEvent { CommandId=Guid.NewGuid(),EntityId=new(run),RunId=run,Revision=1,RunJson="{\"accepted\":true}" };
        IEvent[] sources = [set, changed, startup];
        var stream = "parameter-atomic-"+id.ToString("N");
        async Task ApplyAsync(IEnlistedPostgresTransaction tx,IEvent fact,System.Threading.CancellationToken token)
        {
            switch(fact)
            {
                case IParameterSetFact value:
                    await context.ProjectParameterSetAsync(tx,value,token);
                    var accepted=JsonSerializer.Deserialize<ParameterSetVersion>(value.VersionJson)!;
                    await MarketDataDbContextExtensions.PersistOptionChainParametersAsync(tx,StrategyOptionChainParameterSet.Read(accepted.PayloadJson),value.Revision,true,token);
                    break;
                case ParameterAssignmentChangedEvent value: await context.ProjectParameterAssignmentAsync(tx,value,token); break;
                case ParameterStartupChangedEvent value: await context.ProjectParameterStartupAsync(tx,value,token); break;
            }
        }
        async Task SaveAsync(bool fail) => await fixture.Transactions.ExecuteOnceAsync(async(tx,token)=>
        {
            long sequence=0;
            foreach(var fact in sources)
            {
                await tx.AppendAsync(stream,fact.CommandId,fact,sequence++,token);
                await ApplyAsync(tx,fact,token);
            }
            if(fail) await tx.ExecuteAsync("SELECT 1/0;",[],token);
            return true;
        });
        await Assert.ThrowsAnyAsync<Exception>(()=>SaveAsync(true));
        (await fixture.EventCountAsync(stream)).Should().Be(0);
        (await context.GetParameterSetsAsync(version.Reference.ComponentCode,id)).Should().BeEmpty();
        var tables=new Dictionary<string,Guid>{["parameter_set"]=id,["parameter_set_version"]=id,["parameter_operation"]=id,["parameter_set_audit"]=id,
            ["parameter_assignment"]=assignmentId,["parameter_assignment_revision"]=assignmentId,["parameter_startup_run"]=run,
            ["strategy_option_chain_parameter_version"]=id,["strategy_option_chain_parameter_current"]=id};
        string Column(string table)=>table=="parameter_set_audit"?"entity_id":table.StartsWith("parameter_assignment",StringComparison.Ordinal)?"assignment_id":table=="parameter_startup_run"?"run_id":"set_id";
        string Schema(string table)=>table.StartsWith("strategy_",StringComparison.Ordinal)?"market_data":"reference_configuration";
        async Task<string[]> RowsAsync(string table)=> (await fixture.Transactions.ExecuteOnceAsync((tx,token)=>tx.QueryAsync("SELECT row_to_json(t)::text FROM "+Schema(table)+"."+table+" t WHERE "+Column(table)+"=$1 ORDER BY row_to_json(t)::text;",[tables[table]],reader=>reader.GetString(0),token))).ToArray();
        foreach(var table in tables.Keys) (await RowsAsync(table)).Should().BeEmpty(table);
        await SaveAsync(false);
        (await fixture.EventCountAsync(stream)).Should().Be(3);
        var original=new Dictionary<string,string[]>();
        foreach(var table in tables.Keys) {original[table]=await RowsAsync(table);original[table].Should().ContainSingle(table);}
        var persisted=await fixture.Transactions.ExecuteOnceAsync((tx,token)=>tx.QueryAsync("SELECT n.EventTypeName,l.EventVersion,l.EventPayload FROM public.event_log l JOIN public.event_name_id n USING(EventNameId) JOIN public.event_stream_id s USING(EventStreamId) WHERE s.EventStream=$1 ORDER BY l.EventVersion;",
            [stream],reader=>new TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel{EventTypeName=reader.GetString(0),EventVersion=reader.GetInt64(1),EventData=(byte[])reader.GetValue(2)}.ToDomainEvent(),token));
        await fixture.Transactions.ExecuteOnceAsync(async(tx,token)=>
        {
            await tx.ExecuteAsync("SET LOCAL session_replication_role=replica;",[],token);
            foreach(var table in tables.Keys.Reverse()) await tx.ExecuteAsync("DELETE FROM "+Schema(table)+"."+table+" WHERE "+Column(table)+"=$1;",[tables[table]],token);
            foreach(var fact in persisted) await ApplyAsync(tx,fact,token);
            return true;
        });
        foreach(var table in tables.Keys) (await RowsAsync(table)).Should().Equal(original[table]);
    }
}
