using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>Connection-local immutable reference dictionaries. Never share across pipe generations or directions.</summary>
internal sealed class WorkerPricingContextCodec
{
    public const int Capacity = 4096;
    public readonly SemaphoreSlim Gate = new(1, 1);
    public bool Faulted;
    public MessagePackSerializerOptions Options { get; } = MessagePackBinarySerializer.ContentOptions
        .WithSecurity(MessagePackSecurity.UntrustedData).WithResolver(CompositeResolver.Create(
            new IMessagePackFormatter[] { new ReferenceFormatter<OptionPricingContext>(),
                new ReferenceFormatter<OptionPricingCalendar>(), new ReferenceFormatter<TreasuryContinuousRate>() },
            new[] { MessagePackBinarySerializer.ContentOptions.Resolver }));

    // Definitions and references use a versioned internal pipe protocol; public/NATS contracts stay self-contained.
    sealed class ReferenceFormatter<T> : IMessagePackFormatter<T?> where T : class
    {
        readonly IMessagePackFormatter<T> original = MessagePackBinarySerializer.ContentOptions.Resolver.GetFormatterWithVerify<T>();
        readonly Dictionary<T, int> sent = new();
        readonly Queue<T> sendOrder = new();
        readonly Dictionary<int, T> received = new();
        readonly Queue<int> receiveOrder = new();
        int sendId, receiveId;

        public void Serialize(ref MessagePackWriter writer, T? value, MessagePackSerializerOptions options)
        {
            if (value is null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(2);
            if (sent.TryGetValue(value, out var id)) { writer.Write(id); writer.WriteNil(); return; }
            id = checked(++sendId);
            sent.Add(value, id); sendOrder.Enqueue(value);
            if (sendOrder.Count > Capacity) sent.Remove(sendOrder.Dequeue());
            writer.Write(id);
            original.Serialize(ref writer, value, options);
        }

        public T? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            options.Security.DepthStep(ref reader);
            try
            {
                if (reader.ReadArrayHeader() != 2) throw new InvalidDataException("Invalid pricing context reference.");
                var id = reader.ReadInt32();
                if (reader.TryReadNil())
                    return received.TryGetValue(id, out var existing) ? existing
                        : throw new InvalidDataException("Unknown pricing context reference; reconnect the worker pipe.");
                if (id != checked(receiveId + 1)) throw new InvalidDataException("Invalid pricing context definition sequence.");
                var value = original.Deserialize(ref reader, options)
                    ?? throw new InvalidDataException("Empty pricing context definition.");
                receiveId = id; received.Add(id, value); receiveOrder.Enqueue(id);
                if (receiveOrder.Count > Capacity) received.Remove(receiveOrder.Dequeue());
                return value;
            }
            finally { reader.Depth--; }
        }
    }
}
