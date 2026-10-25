using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TomasAI.IFM.Application.Api.Server.Core.Hosting;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class EventTypeCompatibilityAcceptanceTests
{
    [Theory]
    [InlineData("framework-contracts.json", 98)]
    [InlineData("wire-boundaries.json", 48)]
    public void Frozen_provider_contracts_keep_their_payloads_after_layer_extraction(string fixtureFile, int expectedCount)
    {
        // A maintenance/worker process must not require the API composition root to read or write old names.
        var reader = EventTypeRegistry.CreateCompatibilityRegistry();
        var writer = new EventWriteIdentityRegistry(reader);
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LayeringContractFixtures", fixtureFile)));
        Assert.Equal(expectedCount, fixture.RootElement.GetArrayLength());
        var pending = new Queue<System.Reflection.Assembly>(); pending.Enqueue(typeof(PersistedEventTypeAliases).Assembly);
        var seen = new HashSet<string>(); var types = new Dictionary<string, Type>();
        while (pending.TryDequeue(out var assembly))
        {
            if (!seen.Add(assembly.FullName!)) continue;
            foreach (var reference in assembly.GetReferencedAssemblies().Where(a => a.Name!.StartsWith("TomasAI.IFM.", StringComparison.Ordinal))) pending.Enqueue(System.Reflection.Assembly.Load(reference));
            foreach (var type in assembly.GetTypes()) types.TryAdd(type.FullName!, type);
        }
        foreach (var row in fixture.RootElement.EnumerateArray())
        {
            var oldName = row.GetProperty("fullName").GetString()!;
            var currentName = oldName.Replace("TomasAI.IFM.Framework.MarketData.Contracts", "TomasAI.IFM.Shared.MarketData.Contracts", StringComparison.Ordinal)
                .Replace("TomasAI.IFM.Framework.MarketData.Pricing", "TomasAI.IFM.Shared.MarketData.Pricing", StringComparison.Ordinal)
                .Replace("TomasAI.IFM.Framework.OptionPricer.Pricing", "TomasAI.IFM.Shared.OptionPricing", StringComparison.Ordinal);
            var type = types.TryGetValue(oldName, out var original) ? original : types[currentName];
            var bytes = Convert.FromBase64String(row.GetProperty("payload").GetString()!);
            Assert.Equal(row.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)));
            var codec = new EventLogMessagePackCodec(row.GetProperty("compressed").GetBoolean(), reader);
            if (row.GetProperty("isEvent").GetBoolean())
            {
                var identity = row.GetProperty("legacyIdentity").GetString()!;
                var value = codec.Deserialize(identity, 42, bytes);
                Assert.Equal(type, value.GetType());
                Assert.Equal(identity, writer.GetIdentity(type).EventTypeName);
                EventInitHelper.SetProperty(value, nameof(IEvent.EventId), 0L);
                Assert.Equal(bytes, codec.Serialize(value));
            }
            else
            {
                var options = (MessagePack.MessagePackSerializerOptions)typeof(EventLogMessagePackCodec)
                    .GetField("_writeOptions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(codec)!;
                var value = MessagePack.MessagePackSerializer.Deserialize(type, bytes, options);
                Assert.Equal(bytes, MessagePack.MessagePackSerializer.Serialize(type, value, options));
            }
        }
    }

    [Fact]
    public void Production_catalog_preserves_every_inventoried_event_identity()
    {
        var reader = new EventTypeRegistry();
        var writer = new EventWriteIdentityRegistry(reader);
        var count = TomasAI.IFM.Application.EventSourcing.PersistedEventIdentityCatalog.RegisterProductionClosure(
            typeof(PersistedEventTypeAliases).Assembly, writer);
        Assert.Equal(733, count);
        foreach (var type in new[] { typeof(IronCondorPositionChangedEvent), typeof(FuturesItiSignalGeneratedEvent), typeof(FundMandateCreatedEvent) })
        {
            var identity = writer.GetIdentity(type);
            using var manifest = LoadManifest();
            var legacy = manifest.RootElement.GetProperty("cases").EnumerateArray()
                .Select(row => row.GetProperty("legacyIdentity").GetString()!)
                .First(name => name.StartsWith(type.FullName + ",", StringComparison.Ordinal));
            Assert.Equal(legacy, identity.EventTypeName);
            Assert.Equal(type.Name, identity.EventName);
            Assert.Equal(type, reader.ResolveRequired(identity.EventTypeName));
        }
    }

    [Fact]
    public void Every_frozen_contract_keeps_its_exact_write_identity_and_both_CLR_read_spellings()
    {
        using var baseline = typeof(EventWriteIdentityRegistry).Assembly.GetManifestResourceStream("PersistedEventWriteBaseline.json")!;
        using var document = JsonDocument.Parse(baseline);
        var reader = EventTypeRegistry.CreateCompatibilityRegistry();
        var writer = new EventWriteIdentityRegistry(reader);
        Assert.Equal(733, document.RootElement.GetArrayLength());
        foreach (var row in document.RootElement.EnumerateArray())
        {
            var legacy = row.GetProperty("EventTypeName").GetString()!;
            var type = reader.ResolveRequired(legacy);
            var identity = writer.GetIdentity(type);
            Assert.Equal(row.GetProperty("EventName").GetString(), identity.EventName);
            Assert.Equal(legacy, identity.EventTypeName);
            Assert.Equal(type, reader.ResolveRequired(type.AssemblyQualifiedName!));
            Assert.Equal(type, reader.ResolveRequired($"{type.FullName}, {type.Assembly.GetName().Name}"));
            Assert.Contains(legacy, reader.GetReadIdentities(type));
        }
    }

    [Fact]
    public void Relocated_contract_preserves_outbox_and_durable_queue_write_names_and_payloads()
    {
        const string legacy = "IdentityFixture.Original.Observation, IdentityFixture.Original, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        var value = new IdentityFixture.Relocated.Observation { Value = 123.45m };
        var effect = new EventProjectorEffectIdentity("relocated-contract", 42, EventProjectorEffectKind.CompletedPublication);
        var before = EventProjectorOutboxSerializer.Serialize(value, effect);
        EventWriteIdentityRegistry.Default.RegisterLegacyIdentity(value.GetType(), "Observation", legacy);
        var after = EventProjectorOutboxSerializer.Serialize(value, effect);
        Assert.NotEqual(before.EventTypeName, after.EventTypeName);
        Assert.Equal(legacy, after.EventTypeName);
        Assert.Equal(before.MessageId, after.MessageId);
        Assert.Equal(before.EventPayload, after.EventPayload);
        Assert.Equal(value, EventProjectorOutboxSerializer.Deserialize(after.EventTypeName, after.EventPayload));

        var queue = typeof(TomasAI.IFM.Framework.Messaging.Nats.NatsJSDurableReplayQueue);
        var serialize = queue.GetMethod("Serialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .CreateDelegate<Func<IEvent, string, byte[]>>();
        var deserialize = queue.GetMethod("Deserialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .CreateDelegate<Func<byte[], IEvent>>();
        var bytes = serialize(value, "relocated-contract");
        var options = MessagePack.MessagePackSerializerOptions.Standard
            .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance)
            .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray);
        var envelope = MessagePack.MessagePackSerializer.Deserialize<Dictionary<string, object>>(bytes, options);
        Assert.Equal(legacy, envelope["EventType"]);
        Assert.Equal(value, deserialize(bytes));
    }

    [Fact]
    public void Frozen_pre_change_payloads_replay_through_legacy_stable_and_moved_names_and_reencode_identically()
    {
        var registry = new EventTypeRegistry();
        PersistedEventTypeAliases.Register(registry);
        PersistedEventTypeAliases.Register(registry); // Repeated host/container setup is safe.
        using var manifest = LoadManifest();
        Assert.Equal(6, manifest.RootElement.GetProperty("cases").GetArrayLength());
        foreach (var row in manifest.RootElement.GetProperty("cases").EnumerateArray())
        {
            var oldName = row.GetProperty("legacyIdentity").GetString()!;
            var stable = row.GetProperty("stableIdentity").GetString()!;
            var type = registry.ResolveRequired(oldName);
            var removed = $"Removed.Old.{type.Name}, Removed.Old.Contracts";
            var moved = $"Moved.New.{type.Name}, Moved.New.Contracts";
            registry.Register(type, stable, removed, moved);
            var payload = Convert.FromBase64String(row.GetProperty("payloadBase64").GetString()!);
            Assert.Equal(row.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(payload)));
            var expected = JsonNode.Parse(row.GetProperty("expected").GetRawText());
            var version = row.GetProperty("storageEventVersion").GetInt64();
            var codec = new EventLogMessagePackCodec(row.GetProperty("compressed").GetBoolean(), registry);
            foreach (var name in new[] { oldName, $"{type.FullName}, {type.Assembly.GetName().Name}", stable, removed, moved })
            {
                IEvent[] values = [codec.Deserialize(name, version, payload),
                    new EventStreamReadModel { EventTypeName = name, EventVersion = version, StreamVersion = 7, EventData = payload }.ToDomainEvent(registry),
                    new EventLogReadModel(1, type.Name, name, version, payload, Guid.Empty, "fixture", 7).ToDomainEvent(registry)];
                foreach (var value in values)
                {
                    Assert.Equal(type, value.GetType());
                    Assert.True(JsonNode.DeepEquals(expected, Comparable(value)), $"Frozen payload mismatch: {row.GetProperty("file")} as {name}");
                    EventInitHelper.SetProperty(value, nameof(IEvent.EventId), 0L);
                    Assert.Equal(payload, codec.Serialize(value));
                }
            }
        }
    }

    [Fact]
    public void Outbox_reads_both_identities_and_preserves_legacy_writer_and_deterministic_message_identity()
    {
        PersistedEventTypeAliases.Register(EventTypeRegistry.Default);
        using var manifest = LoadManifest();
        foreach (var row in manifest.RootElement.GetProperty("cases").EnumerateArray())
        {
            var oldName = row.GetProperty("legacyIdentity").GetString()!;
            var stable = row.GetProperty("stableIdentity").GetString()!;
            var value = EventLogMessagePackCodec.Shared.Deserialize(oldName, 42, Convert.FromBase64String(row.GetProperty("payloadBase64").GetString()!));
            var identity = new EventProjectorEffectIdentity("frozen-compatibility", 42, EventProjectorEffectKind.CompletedPublication);
            var outbox = EventProjectorOutboxSerializer.Serialize(value, identity);
            Assert.Equal(oldName, outbox.EventTypeName);
            Assert.Equal(identity.MessageId, outbox.MessageId);
            var expected = Comparable(value);
            foreach (var name in new[] { oldName, stable })
            {
                var read = EventProjectorOutboxSerializer.Deserialize(name, outbox.EventPayload);
                Assert.True(JsonNode.DeepEquals(expected, Comparable(read)));
            }
            Assert.ThrowsAny<Exception>(() => EventProjectorOutboxSerializer.Deserialize(stable, [0xc1]));
        }
        Assert.Throws<TypeLoadException>(() => EventProjectorOutboxSerializer.Deserialize("trade.iron-condor.position-changed/v999", []));
    }

    [Fact]
    public void Current_concrete_event_contracts_keep_their_existing_CLR_resolution()
    {
        var assemblies = new[] { typeof(IronCondorPositionChangedEvent).Assembly,
            typeof(FuturesItiSignalGeneratedEvent).Assembly, typeof(FundMandateCreatedEvent).Assembly };
        var events = assemblies.SelectMany(assembly => assembly.GetTypes()).Where(type =>
            typeof(IEvent).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface && !type.ContainsGenericParameters).ToArray();
        Assert.True(events.Length > 100, "The real business contract assemblies must be covered.");
        var registry = new EventTypeRegistry();
        PersistedEventTypeAliases.Register(registry);
        foreach (var type in events) Assert.Equal(type, registry.ResolveRequired(type.AssemblyQualifiedName!));
    }

    static JsonDocument LoadManifest() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "EventTypeCompatibilityFixtures", "manifest.json")));
    static JsonNode Comparable(IEvent value)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(value, value.GetType()))!;
        node.AsObject().Remove("UserName"); // Derived environment/display metadata is not persisted event data.
        return node;
    }
}
