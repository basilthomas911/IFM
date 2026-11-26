using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public class OwnedMessageOriginTests
{
    [Fact]
    public void Owned_origin_is_not_part_of_serialized_body_value_equality_or_implicit_record_copies()
    {
        var value = new FuturesTickTradeDataChangedEvent { ReceivedOn = DateTime.UnixEpoch };
        var copy = value with { };
        var bytes = MessagePackSerializer.Serialize(value);
        var hash = value.GetHashCode();
        var origin = MessageOrigins.Capture(value, TimeProvider.System, causationId: Guid.NewGuid());
        Assert.Equal(copy, value); Assert.Equal(hash, value.GetHashCode());
        Assert.Equal(bytes, MessagePackSerializer.Serialize(value));
        var derived = value with { Id = Guid.NewGuid() };
        Assert.Null(MessageOrigins.Get(derived));
        var fresh = MessageOrigins.Capture(derived, TimeProvider.System);
        Assert.Null(fresh.CausationId);
        var forwarded = MessageOrigins.Copy(value, value with { });
        Assert.Same(origin, MessageOrigins.Get(forwarded));
        var decoded = MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(bytes);
        Assert.Null(MessageOrigins.Get(decoded));
        MessageOrigins.Restore(decoded, origin); Assert.Equal(origin, MessageOrigins.Get(decoded));
    }
    [Fact]
    public void Encoding_caches_preserve_value_equality_hashes_and_exact_optional_identities()
    {
        var origin = new MessageOrigin(new DateTimeOffset(2026, 11, 23, 20, 0, 0, TimeSpan.Zero).AddTicks(123), "replay/λ",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var hash = origin.GetHashCode(); var equal = origin with { };
        var json = MessageOriginJson.Serialize(origin); var binary = MessageOriginBinary.Serialize(origin);
        Assert.Same(json, MessageOriginJson.Serialize(origin)); Assert.Same(binary, MessageOriginBinary.Serialize(origin));
        Assert.Equal(equal, origin); Assert.Equal(hash, origin.GetHashCode());
        Assert.Equal(origin, MessageOriginJson.Deserialize(json)); Assert.Equal(origin, MessageOriginBinary.Deserialize(binary));
    }
    [Fact]
    public void Unknown_legacy_remains_unknown_even_on_owned_messages_and_concurrent_restore_is_immutable()
    {
        var unknown = new FuturesTickTradeDataChangedEvent(); MessageOrigins.Restore(unknown, null);
        Assert.Null(MessageOrigins.ForPublication(unknown, TimeProvider.System));
        var value = new FuturesTickTradeDataChangedEvent(); var origin = new MessageOrigin(DateTimeOffset.UtcNow, "test");
        Parallel.For(0, 1000, _ => MessageOrigins.Restore(value, origin));
        Assert.Equal(origin, MessageOrigins.Get(value));
        Assert.Throws<InvalidOperationException>(() => MessageOrigins.Restore(value, new MessageOrigin(DateTimeOffset.UtcNow.AddDays(1), "test")));
    }
}
