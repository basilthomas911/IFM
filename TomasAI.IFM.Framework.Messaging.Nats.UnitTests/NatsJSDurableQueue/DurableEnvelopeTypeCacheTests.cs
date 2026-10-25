using System.Reflection;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests.NatsJSDurableQueue;

public sealed class DurableEnvelopeTypeCacheTests
{
    [Fact]
    public void Stable_and_moved_names_work_in_binary_and_legacy_json_envelopes_without_changing_writers()
    {
        const string stable = "test.durable-envelope-sample/v1";
        const string moved = "Moved.SampleEvent, Removed.SampleAssembly";
        EventTypeRegistry.Default.Register<SampleEvent>(stable, moved);
        var value = SampleData.Event("alias-preserves-payload");
        var binary = Serialize(value, "type-cache-test");
        var options = MessagePack.MessagePackSerializerOptions.Standard
            .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance)
            .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray);
        var envelope = MessagePack.MessagePackSerializer.Deserialize<Dictionary<string, object>>(binary, options);
        Assert.Equal(typeof(SampleEvent).AssemblyQualifiedName!, envelope["EventType"]);
        foreach (var name in new[] { stable, moved })
        {
            envelope["EventType"] = name;
            var aliased = MessagePack.MessagePackSerializer.Serialize(envelope, options);
            Deserialize(aliased).Should().BeEquivalentTo(value);
            Deserialize(Legacy(name, JsonConvert.SerializeObject(value))).Should().BeEquivalentTo(value);
        }
        Assert.Throws<TypeLoadException>(() => Deserialize(Legacy("test.durable-envelope-sample/v2", "{}")));
    }

    [Fact]
    public void Failed_resolution_is_not_cached_and_can_be_repaired_by_explicit_registration()
    {
        const string stable = "test.durable-envelope-late-registration/v1";
        var value = SampleData.Event("late-registration");
        var bytes = Legacy(stable, JsonConvert.SerializeObject(value));
        Assert.Throws<TypeLoadException>(() => Deserialize(bytes));
        EventTypeRegistry.Default.Register<SampleEvent>(stable);
        Deserialize(bytes).Should().BeEquivalentTo(value);
    }
    static readonly Func<IEvent, string, byte[]> Serialize = typeof(NatsJSDurableReplayQueue)
        .GetMethod("Serialize", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<IEvent, string, byte[]>>();
    static readonly Func<byte[], IEvent> Deserialize = typeof(NatsJSDurableReplayQueue)
        .GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<byte[], IEvent>>();

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
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
