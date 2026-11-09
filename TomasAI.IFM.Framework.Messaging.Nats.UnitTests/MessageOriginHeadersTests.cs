using System.Buffers;
using System.Diagnostics;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Framework.Messaging.NatsJetStream.UnitTests.NatsOwnedQueryMessageTests;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class MessageOriginHeadersTests
{
    [Fact]
    public void MetadataRoundTripsWithoutActivityOrBodyChanges()
    {
        var origin = Origin();
        Assert.Null(Activity.Current);
        var query = Query();
        var before = Serialize(query);
        MessageOrigins.Restore(query, origin);
        var headers = MessageOriginHeaders.Add(null, origin);
        Assert.Equal(origin, MessageOriginHeaders.Read(headers));
        Assert.Equal(before, Serialize(query));
        Assert.Null(MessageOriginHeaders.Read(null));
        Assert.Null(MessageOriginHeaders.Add(null, null));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("not-json")]
    public void MalformedOriginFailsExplicitly(string value)
    {
        var headers = new NatsHeaders { [MessageOriginHeaders.Header] = value };
        Assert.Throws<InvalidDataException>(() => MessageOriginHeaders.Read(headers));
    }

    [Fact]
    public void PooledQueryRetainsOriginAfterPayloadRelease()
    {
        var query = Query();
        var bytes = Serialize(query);
        var owner = NatsMemoryOwner<byte>.Allocate(bytes.Length);
        bytes.CopyTo(owner.Span);
        var origin = Origin();
        var source = new NatsMsg<NatsMemoryOwner<byte>>(query.Subject.ToString(), null,
            default, MessageOriginHeaders.Add(null, origin), owner, default!, default);
        using var message = new NatsOwnedQueryMessage(source, query.Subject);
        var decoded = message.AsQuery<TestQuery, TestQueryResult>();
        message.ReleasePayload();
        Assert.Equal(origin, message.Origin);
        Assert.Equal(origin, MessageOrigins.Get(decoded!));
        Assert.Equal(origin, new ActorMessageInfo(message, decoded!).Origin);
    }

    static MessageOrigin Origin() => new(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero),
        "test-business", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    static TestQuery Query() => new()
    {
        Subject = new ActorSubject(ActorType.Query, "TestQuery", "Get", "42"),
        Value = 42
    };

    static byte[] Serialize(TestQuery query)
    {
        var writer = new ArrayBufferWriter<byte>();
        NatsMessagePackSerializer<TestQuery>.Default.Serialize(writer, query);
        return writer.WrittenSpan.ToArray();
    }
}
