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
