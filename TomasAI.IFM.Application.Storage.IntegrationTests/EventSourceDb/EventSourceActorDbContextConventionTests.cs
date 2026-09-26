using System;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class EventSourceActorDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<EventSourceActorDbContext>)
            .IsAssignableFrom(typeof(IEventSourceActorDbContext)));
        Assert.True(typeof(IEventSourceActorDbReadContext)
            .IsAssignableFrom(typeof(IEventSourceActorDbContext)));
        Assert.True(typeof(IEventSourceActorDbWriteContext)
            .IsAssignableFrom(typeof(IEventSourceActorDbContext)));
        Assert.True(typeof(IEventSourceActorDbContext)
            .IsAssignableFrom(typeof(EventSourceActorDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_event_source_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.ActorEventSourceDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(IEventSourceActorDbContext), property.PropertyType);
    }

    [Fact]
    public void Read_and_write_contracts_are_separated_by_behavior()
    {
        Assert.NotNull(typeof(IEventSourceActorDbReadContext)
            .GetMethod(nameof(IEventSourceActorDbReadContext.GetEventLogByEventIdAsync)));
        Assert.Null(typeof(IEventSourceActorDbReadContext)
            .GetMethod(nameof(IEventSourceActorDbWriteContext.SaveEventsAsync)));
        Assert.NotNull(typeof(IEventSourceActorDbWriteContext)
            .GetMethod(nameof(IEventSourceActorDbWriteContext.SaveEventsAsync),
            [typeof(string), typeof(Guid), typeof(DomainEventCollection)]));
        Assert.Null(typeof(IEventSourceActorDbWriteContext)
            .GetMethod(nameof(IEventSourceActorDbReadContext.GetEventLogByEventIdAsync)));
    }
}
