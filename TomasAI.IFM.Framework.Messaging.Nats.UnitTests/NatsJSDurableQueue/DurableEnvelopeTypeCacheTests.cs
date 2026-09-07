using System.Reflection;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests.NatsJSDurableQueue;

public sealed class DurableEnvelopeTypeCacheTests
{
    static readonly Func<IEvent, string, byte[]> Serialize = typeof(NatsJSDurableReplayQueue)
        .GetMethod("Serialize", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<IEvent, string, byte[]>>();
    static readonly Func<byte[], IEvent> Deserialize = typeof(NatsJSDurableReplayQueue)
        .GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<byte[], IEvent>>();

    [Fact]
    public void ConcurrentBinaryAndLegacyEnvelopes_PreserveEventPayloadAndIdentity()
    {
        var value = SampleData.Event("cached-type");
        var binary = Serialize(value, "type-cache-test");
        var legacy = Legacy(typeof(SampleEvent).AssemblyQualifiedName!, JsonConvert.SerializeObject(value));
        Parallel.For(0, 256, i =>
        {
            var result = Deserialize(i % 2 == 0 ? binary : legacy);
            result.Should().BeOfType<SampleEvent>().Which.Should().BeEquivalentTo(value);
        });
    }

    [Fact]
    public void WarmTypeCache_DoesNotReuseAnEarlierEventsPayload()
    {
        foreach (var label in new[] { "first", "second" })
        {
            var value = SampleData.Event(label);
            var result = (SampleEvent)Deserialize(Serialize(value, "type-cache-test"));
            result.Id.Should().Be(value.Id);
            result.Value.Should().Be(label);
        }
    }

    [Fact]
    public void InvalidAndMissingTypes_RemainRejectedAfterSuccessfulResolution()
    {
        _ = Deserialize(Serialize(SampleData.Event(), "type-cache-test"));
        for (var i = 0; i < 2; i++)
        {
            Action invalid = () => Deserialize(Legacy(typeof(string).AssemblyQualifiedName!, "\"invalid\""));
            invalid.Should().Throw<InvalidOperationException>().WithMessage("*does not implement IEvent*");
            Action missing = () => Deserialize(Legacy("MissingEvent, MissingAssembly", "{}"));
            missing.Should().Throw<FileNotFoundException>();
        }
    }

    static byte[] Legacy(string typeName, string eventJson) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
    {
        EventProjectorName = "type-cache-test",
        EventType = typeName,
        EventJson = eventJson,
        EnqueuedAtUtc = DateTimeOffset.UtcNow,
        FailedAtUtc = (DateTimeOffset?)null,
        ErrorMessage = (string?)null
    }));
}
