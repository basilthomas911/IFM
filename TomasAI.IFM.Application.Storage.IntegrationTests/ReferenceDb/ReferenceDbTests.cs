using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.JobScheduler;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure","SelfContained")]
public sealed class ReferenceDbTests(PostgresTradePlanFixture fixture)
{
    static ReferenceDbContext Context(string connection)
    {
        var settings=new DbConnectionSettings().Add(ReferenceDbContext.ReferenceDbConnection,connection,"System.Data.Postgres");
        var sequence=Substitute.For<ISequenceIdGenerator>();
        long id=100;
        sequence.GetSequenceIdAsync(Arg.Any<SequenceName>(),Arg.Any<CancellationToken>()).Returns(_=>new ValueTask<long>(Interlocked.Increment(ref id)));
        var objects=new Dictionary<Type,object>();
        var factory=new DbContextFactory(new DbContextResolver(type=>objects[type]));
        var context=new ReferenceDbContext(settings,factory,sequence,NullLogger<DbProvider>.Instance);
        objects.Add(typeof(IObjectRepository<ReferenceDbContext>),context);
        return context;
    }
    async Task<PostgresInstrumentTestDatabase> Database()
    {
        var isolated=await PostgresInstrumentTestDatabase.CreateAsync(fixture.Connection);
        var settings=new DbConnectionSettings().Add(ReferenceDbContext.ReferenceDbConnection,isolated.Connection,"System.Data.Postgres");
        await new ReferenceSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        return isolated;
    }
    static ScheduledJobReadModel Job(string name) => new(0,name,JobScheduleType.Daily,DateTime.UtcNow,0,"fixture",true,DateTime.UtcNow,"fixture",DateTime.UtcNow,"fixture");

    [Fact]
    public async Task Lookup_crud_preserves_order_creation_metadata_and_short_code_queries()
    {
        await using var isolated=await Database();var db=Context(isolated.Connection);
        var timestamp = DateTime.UtcNow;
        timestamp = new DateTime(timestamp.Ticks / 10 * 10, DateTimeKind.Utc);
        var first=new LookupTypeReadModel("fixture","A",0,"First",timestamp,"creator");
        var second=first with {OrderId=1,ShortCode="B",Description="Second"};
        await db.InsertLookupTypeAsync(first);await db.InsertLookupTypeAsync(second);
        Assert.Equal(2,(await db.GetLookupTypesAsync()).Count);
        Assert.Equal(new[]{"A","B"},(await db.GetLookupTypeShortCodesAsync("fixture")).Select(x=>x.ShortCode));
        Assert.True(await db.LookupTypeShortCodeExistsAsync("fixture","B"));
        Assert.Equal(new[]{"fixture"},await db.GetLookupTypeNamesAsync());
        await db.UpdateLookupTypeAsync(second.Id,second with {Description="Updated"});
        Assert.Equal("Updated",(await db.GetLookupTypeAsync(second.Id))!.Description);
        await db.DeleteLookupTypeAsync(first.Id);
        var remaining=Assert.Single(await db.GetLookupTypeAsync("fixture"));
        Assert.Equal(0,remaining.OrderId);Assert.Equal(second.CreatedOn,remaining.CreatedOn);Assert.Equal(second.CreatedBy,remaining.CreatedBy);
    }
    [Fact]
    public async Task Scheduled_job_rename_and_weekdays_survive_context_restart_and_delete()
    {
        await using var isolated=await Database();var db=Context(isolated.Connection);var source=Job("nightly");
        source.DaysOfWeek=new(0,true,false,true,false,true,false,false);
        await db.InsertScheduledJobAsync(source);var id=await db.GetScheduledJobIdAsync(source.JobName);
        var row=Assert.Single(await Context(isolated.Connection).GetScheduledJobsAsync());
        Assert.Equal(id,row.JobId);Assert.True(row.DaysOfWeek!.Monday);Assert.False(row.DaysOfWeek.Tuesday);
        await db.UpdateScheduledJobAsync(row with {JobName="renamed"});
        Assert.Equal(0,await db.GetScheduledJobIdAsync("nightly"));Assert.Equal(id,await db.GetScheduledJobIdAsync("renamed"));
        Assert.True((await db.ReconcileQueryProjectionsV2Async()).IsConsistent);
        Assert.Equal(0,(await db.BackfillQueryProjectionsV2Async()).ScheduledJobs);
        await db.DeleteScheduledJobAsync(id);await db.DeleteScheduledJobAsync(id);
        Assert.Empty(await db.GetScheduledJobsAsync());
    }
    [Fact]
    public async Task Exact_name_uniqueness_allows_case_variants_and_rename_collision_rolls_back()
    {
        await using var isolated=await Database();var db=Context(isolated.Connection);
        await db.InsertScheduledJobAsync(Job("Daily"));await db.InsertScheduledJobAsync(Job("daily"));
        await Assert.ThrowsAsync<PostgresException>(()=>db.InsertScheduledJobAsync(Job("Daily")));
        var rows=(await db.GetScheduledJobsAsync()).ToArray();var first=rows.Single(x=>x.JobName=="Daily");
        await Assert.ThrowsAsync<PostgresException>(()=>db.UpdateScheduledJobAsync(first with {JobName="daily"}));
        Assert.Equal(first.JobId,await db.GetScheduledJobIdAsync("Daily"));
        Assert.Equal(2,(await db.GetScheduledJobsAsync()).Count);
    }
}
