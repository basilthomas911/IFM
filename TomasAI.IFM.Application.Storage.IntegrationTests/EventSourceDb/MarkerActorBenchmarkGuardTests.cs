using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class MarkerActorBenchmarkGuardTests
{
    [Theory]
    [InlineData("Host=127.0.0.1;Port=25432;Database=event-source-dev-db")]
    [InlineData("Host=remote;Port=25432;Database=ifm_eventlog_bench_123456abcdef_actor")]
    [InlineData("Host=127.0.0.1;Port=5432;Database=ifm_eventlog_bench_123456abcdef_actor")]
    [InlineData("Host=localhost;Port=25432;Database=ifm_eventlog_bench_123456abcdef_actor")]
    public void Internal_actor_constructor_rejects_nonisolated_targets_for_both_writers(string connection)
    {
        var settings = new DbConnectionSettings().Add("EventSourceActorDbConnection", connection, "System.Data.Postgres");
        foreach (var batched in new[] { false, true })
            Assert.Throws<ArgumentException>(() => new EventSourceActorDbContext(settings,
                Substitute.For<IDbContextFactory>(), Substitute.For<IBlackboardService>(), NullLogger<DbProvider>.Instance,
                new EventLogPersistenceOptions { WriteMode = EventLogWriteMode.BinaryCopy }, null, batched));
    }

    [Fact]
    public void Public_construction_exposes_no_candidate_switch()
    {
        var constructor = Assert.Single(typeof(EventSourceActorDbContext).GetConstructors());
        Assert.Equal(6, constructor.GetParameters().Length);
        Assert.DoesNotContain(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(bool));
        Assert.False(EventLogSqlLayout.Current.BatchProjectionMarkers);
    }
}
