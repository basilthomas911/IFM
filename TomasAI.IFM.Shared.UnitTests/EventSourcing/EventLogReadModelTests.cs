using System;
using FluentAssertions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventSourcing;

public sealed class EventLogReadModelTests
{
    [Fact]
    public void ToDomainEvent_returns_unknown_event_when_persisted_assembly_is_unavailable()
    {
        var eventLog = new EventLogReadModel(
            EventStreamId: 10,
            EventName: "RemovedEvent",
            EventTypeName: "Example.RemovedEvent, Example.RemovedAssembly",
            EventVersion: 42,
            EventData: "{}",
            CommandId: Guid.NewGuid(),
            EventTimestamp: DateTime.UtcNow.ToString("O"));

        var domainEvent = eventLog.ToDomainEvent();

        domainEvent.Should().BeOfType<UnknownEvent>();
        domainEvent.EventId.Should().Be(42);
    }
}
