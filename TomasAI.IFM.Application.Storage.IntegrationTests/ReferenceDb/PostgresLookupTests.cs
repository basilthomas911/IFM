using TomasAI.IFM.Shared.EventSourcing;
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure", "SelfContained")]
public sealed class PostgresLookupTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Persisted_lookup_changes_reconstruct_exact_ordered_rows_without_notifications()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(ReferenceSchemaSql.Create,[],ct));
        var name="lookup-rebuild-"+Guid.NewGuid().ToString("N");
        var time=DateTime.UtcNow;
        var first=new LookupTypeReadModel(name,"A",0,"First",time,"integration");
        var second=first with {OrderId=1,ShortCode="B",Description="Second"};
        TomasAI.IFM.Shared.EventSourcing.IEvent[] sources=[
            new LookupTypeAddedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=first.Id,LookupType=first,ReceivedOn=time},
            new LookupTypeAddedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=second.Id,LookupType=second,ReceivedOn=time},
            new LookupTypeChangedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=second.Id,LookupTypeId=second.Id,LookupType=second with {Description="Changed accepted description"},ReceivedOn=time,ChangedOn=time,ChangedBy="integration"},
            new LookupTypeRemovedEvent {Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=first.Id,LookupTypeId=first.Id,ReceivedOn=time,RemovedOn=time,RemovedBy="integration"}];
        async Task Apply(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction db,TomasAI.IFM.Shared.EventSourcing.IEvent source,System.Threading.CancellationToken ct)
        {
            switch(source){
                case LookupTypeAddedEvent added:await db.InsertLookupAsync(added.LookupType,ct);break;
                case LookupTypeChangedEvent changed:await db.UpdateLookupAsync(changed.EntityId,changed.LookupType,ct);break;
                case LookupTypeRemovedEvent removed:await db.DeleteLookupAsync(removed.EntityId,ct);break;
                default:throw new InvalidOperationException("Unexpected accepted lookup event.");}
        }
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{
            long revision=0;
            foreach(var source in sources){await db.AppendAsync(name,source.CommandId,source,revision++,ct);await Apply(db,source,ct);}
            return true;
        });
        var original=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT row_to_json(t)::text FROM reference.lookup_type t WHERE lookupTypeName=$1 ORDER BY orderId;",[name],r=>r.GetString(0),ct));
        original.Should().HaveCount(1);
        var accepted=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT n.EventTypeName,l.EventVersion,l.EventPayload FROM event_log l JOIN event_name_id n ON n.EventNameId=l.EventNameId JOIN event_stream_id s ON s.EventStreamId=l.EventStreamId WHERE s.EventStream=$1 ORDER BY l.EventVersion;",[name],r=>new TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel {EventTypeName=r.GetString(0),EventVersion=r.GetInt64(1),EventData=r.GetFieldValue<byte[]>(2)}.ToDomainEvent(),ct));
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{
            await db.ExecuteAsync("DELETE FROM reference.lookup_type WHERE lookupTypeName=$1;",[name],ct);
            foreach(var source in accepted)await Apply(db,source,ct);
            return true;
        });
        var rebuilt=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT row_to_json(t)::text FROM reference.lookup_type t WHERE lookupTypeName=$1 ORDER BY orderId;",[name],r=>r.GetString(0),ct));
        rebuilt.Should().Equal(original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_lookup_and_source_commit_or_roll_back_together(bool fail)
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(ReferenceSchemaSql.Create,[],ct));
        var name="lookup-"+Guid.NewGuid().ToString("N");
        var row=new LookupTypeReadModel(name,"A",0,"Accepted description",DateTime.UtcNow,"fixture");
        var source=new LookupTypeAddedEvent { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=row.Id,LookupType=row,ReceivedOn=row.CreatedOn };
        async Task Save()=>await fixture.Transactions.ExecuteOnceAsync(async (db,ct)=>
        {
            await db.AppendAsync(name,source.CommandId,source,0,ct);
            await db.InsertLookupAsync(row,ct);
            if(fail) throw new InvalidOperationException("Injected source projection rollback");
            return true;
        });
        if(fail) await FluentActions.Awaiting(Save).Should().ThrowAsync<InvalidOperationException>(); else await Save();
        (await fixture.EventCountAsync(name)).Should().Be(fail?0:1);
        var count=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM reference.lookup_type WHERE lookupTypeName=$1;",[name],ct));
        Convert.ToInt64(count).Should().Be(fail?0:1);
    }

    [Fact]
    public async Task Ordered_delete_preserves_creation_facts_and_update_failure_preserves_old_row()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(ReferenceSchemaSql.Create,[],ct));
        var name="lookup-"+Guid.NewGuid().ToString("N");var created=DateTime.UtcNow;
        var a=new LookupTypeReadModel(name,"A",0,"First",created,"fixture");
        var b=a with {ShortCode="B",OrderId=1,Description="Second"};
        var c=a with {ShortCode="C",OrderId=2,Description="Third"};
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await db.InsertLookupAsync(a,ct);await db.InsertLookupAsync(b,ct);await db.InsertLookupAsync(c,ct);return true;});
        await fixture.Transactions.ExecuteOnceAsync(async(db,ct)=>{await db.DeleteLookupAsync(b.Id,ct);return true;});
        var rows=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.QueryAsync("SELECT shortCode,orderId,createdOn FROM reference.lookup_type WHERE lookupTypeName=$1 ORDER BY orderId;",[name],r=>(r.GetString(0),r.GetInt32(1),r.GetDateTime(2)),ct));
        rows.Should().HaveCount(2);rows[0].Item1.Should().Be("A");rows[1].Item1.Should().Be("C");rows[1].Item2.Should().Be(1);
        rows[0].Item3.Should().BeCloseTo(created,TimeSpan.FromTicks(10));rows[1].Item3.Should().Be(rows[0].Item3);
        await FluentActions.Awaiting(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(db,ct)=>{await db.UpdateLookupAsync(a.Id,a with {ShortCode="X"},ct);throw new InvalidOperationException("Injected update rollback");})).Should().ThrowAsync<InvalidOperationException>();
        var original=await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ScalarAsync("SELECT shortCode FROM reference.lookup_type WHERE lookupTypeName=$1 AND orderId=0;",[name],ct));
        original.Should().Be("A");
    }
    [Fact]
    public async Task DbContext_queries_and_mutations_use_native_postgres_with_deterministic_order()
    {
        await fixture.Transactions.ExecuteOnceAsync((db,ct)=>db.ExecuteAsync(ReferenceSchemaSql.Create,[],ct));
        var settings=new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add(ReferenceDbContext.ReferenceDbConnection,fixture.Connection,"System.Data.Postgres");
        var factory=new Mock<IDbContextFactory>();
        var context=new ReferenceDbContext(settings,factory.Object,Mock.Of<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        factory.SetupGet(x=>x.ReferenceDb).Returns(context);
        var name="lookup-context-"+Guid.NewGuid().ToString("N");
        var a=new LookupTypeReadModel(name,"A",0,"First",DateTime.UtcNow,"fixture");
        var b=a with {ShortCode="B",OrderId=1};
        await context.InsertLookupTypeAsync(b);await context.InsertLookupTypeAsync(a);await context.InsertLookupTypeAsync(a);
        var rows=await context.GetLookupTypeAsync(name);
        rows.Should().HaveCount(2);rows.Should().BeInAscendingOrder(x=>x.OrderId);
        (await context.GetLookupTypeAsync(a.Id)).ShortCode.Should().Be("A");
        (await context.GetLookupTypeShortCodesAsync(name)).Should().HaveCount(2);
        await context.UpdateLookupTypeAsync(b.Id,b with {Description="Updated"});
        (await context.GetLookupTypeAsync(b.Id)).Description.Should().Be("Updated");
        await context.DeleteLookupTypeAsync(a.Id);
        rows=await context.GetLookupTypeAsync(name);rows.Should().ContainSingle();
        rows.Should().Contain(x=>x.ShortCode=="B" && x.OrderId==0 && x.Description=="Updated");
        (await context.GetLookupTypeNamesAsync()).Should().Contain(name);
    }

}
