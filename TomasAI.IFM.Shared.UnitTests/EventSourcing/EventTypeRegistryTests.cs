using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventSourcing;

public sealed class EventTypeRegistryTests
{
    const string Stable = "test.event-replay/v1";
    const string Removed = "Old.Business.Event, Old.RemovedAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
    const string Moved = "New.Business.Event, New.Assembly";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Legacy_current_moved_and_stable_names_decode_identical_bytes_through_every_event_log_entry_point(bool compressed)
    {
        var registry = new EventTypeRegistry();
        registry.Register<UnknownEvent>(Stable, Removed, Moved);
        var codec = new EventLogMessagePackCodec(compressed, registry);
        var source = Sample() with { AggregateId = null! };
        var bytes = codec.Serialize(source);
        foreach (var identity in new[] { typeof(UnknownEvent).AssemblyQualifiedName!, Stable, Removed, Moved })
        {
            var expected = source with { EventId = 987 };
            Assert.Equal(expected, codec.Deserialize(identity, 987, bytes));
            Assert.Equal(expected, new EventStreamReadModel { EventTypeName = identity, EventVersion = 987,
                StreamVersion = 17, EventData = bytes }.ToDomainEvent(registry));
            Assert.Equal(expected, new EventLogReadModel(1, nameof(UnknownEvent), identity, 987, bytes,
                source.CommandId, source.ReceivedOn.ToString("O"), 17).ToDomainEvent(registry));
            Assert.Equal(bytes, codec.Serialize(source)); // Registration never changes payload bytes.
        }
    }

    [Fact]
    public void Registration_is_idempotent_but_conflicts_are_atomic_and_never_rebind_an_identity()
    {
        var registry = new EventTypeRegistry();
        registry.Register<UnknownEvent>(Stable, Removed);
        registry.Register<UnknownEvent>(Stable, Removed);
        Assert.Throws<InvalidOperationException>(() => registry.Register<ErrorEvent>("test.other-event/v1", Removed));
        Assert.Null(registry.Resolve("test.other-event/v1"));
        Assert.Equal(typeof(UnknownEvent), registry.Resolve(Removed));
        Assert.Throws<InvalidOperationException>(() => registry.Register<ErrorEvent>("test.other-event/v1", typeof(UnknownEvent).AssemblyQualifiedName!));
        Assert.Null(registry.Resolve("test.other-event/v1"));
    }

    [Fact]
    public void Unknown_versions_and_names_remain_observable_and_non_events_are_rejected()
    {
        var registry = new EventTypeRegistry();
        registry.Register<UnknownEvent>(Stable);
        Assert.Null(registry.Resolve("test.event-replay/v2"));
        Assert.Null(registry.Resolve("Test.event-replay/v1"));
        Assert.Throws<TypeLoadException>(() => registry.ResolveRequired("test.event-replay/v2"));
        Assert.Throws<InvalidDataException>(() => registry.Resolve(typeof(string).AssemblyQualifiedName!));
        Assert.Throws<InvalidDataException>(() => registry.Register(typeof(IEvent), "test.interface/v1"));
        var missing = Assert.IsType<UnknownEvent>(new EventStreamReadModel { EventTypeName = Removed,
            EventVersion = 12, EventData = [1, 2, 3] }.ToDomainEvent(registry));
        Assert.Equal(Removed, missing.EventTypeName);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), missing.EventData);
        Assert.Equal(12, missing.EventId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("trade.position-changed")]
    [InlineData("trade.position-changed/v0")]
    [InlineData("Trade.position-changed/v1")]
    [InlineData("trade.position-changed/v01")]
    public void Invalid_stable_names_do_not_change_the_registry(string identity)
    {
        var registry = new EventTypeRegistry();
        Assert.ThrowsAny<ArgumentException>(() => registry.Register<UnknownEvent>(identity));
    }

    [Fact]
    public void Concurrent_registration_and_reads_cannot_observe_partial_alias_batches()
    {
        var registry = new EventTypeRegistry();
        Parallel.For(0, 100, index =>
        {
            var stable = $"test.concurrent-event-{index}/v1";
            var historical = $"Missing.Event{index}, Missing.Assembly{index}";
            registry.Register<UnknownEvent>(stable, historical);
            Assert.Equal(typeof(UnknownEvent), registry.Resolve(stable));
            Assert.Equal(typeof(UnknownEvent), registry.Resolve(historical));
        });
        Parallel.For(0, 100, _ => registry.Register<UnknownEvent>(Stable, Removed, Moved));
        Assert.Equal(typeof(UnknownEvent), registry.ResolveRequired(Stable));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Aliased_known_event_corruption_never_becomes_an_unknown_event(bool compressed)
    {
        var registry = new EventTypeRegistry();
        registry.Register<UnknownEvent>(Stable);
        var payload = new EventLogMessagePackCodec(compressed).Serialize(Sample());
        var codec = new EventLogMessagePackCodec(compressed, registry);
        foreach (var malformed in new[] { payload[..^1], payload.Concat(new byte[] { 0 }).ToArray(), new byte[] { 0x93, 2, 0xc2, 0xc0 } })
        {
            Assert.ThrowsAny<Exception>(() => codec.Deserialize(Stable, 100, malformed));
            Assert.ThrowsAny<Exception>(() => new EventStreamReadModel { EventTypeName = Stable,
                EventVersion = 100, EventData = malformed }.ToDomainEvent(registry));
        }
    }

    static UnknownEvent Sample() => new(default, Guid.NewGuid(), default, 0, Guid.NewGuid(),
        "aggregate", "test", new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc), 3, 4,
        "missing-payload-type", "diagnostic", new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc));
}
