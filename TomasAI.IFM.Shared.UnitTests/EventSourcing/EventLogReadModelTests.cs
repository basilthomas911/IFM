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

    [Fact]
    public void Stream_version_is_stream_local_and_does_not_replace_the_global_event_id()
    {
        var eventLog = new EventLogReadModel(
            EventStreamId: 10,
            EventName: "KnownEvent",
            EventTypeName: "Example.KnownEvent, Example.Assembly",
            EventVersion: 42,
            EventData: "{}",
            CommandId: Guid.NewGuid(),
            EventTimestamp: DateTime.UtcNow.ToString("O"),
            StreamVersion: 7);

        eventLog.EventVersion.Should().Be(42);
        eventLog.StreamVersion.Should().Be(7);
    }

    [Fact]
    public void Stream_version_defaults_to_zero_for_pre_migration_callers()
    {
        var eventLog = new EventLogReadModel(
            10, "LegacyEvent", "Legacy.Event, Legacy", 42, "{}", Guid.NewGuid(), DateTime.UtcNow.ToString("O"));

        eventLog.StreamVersion.Should().Be(0);
    }
}
