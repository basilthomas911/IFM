using System.Buffers.Binary;
using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Testing;

public static class RawFuturesJournalFixture
{
    public static readonly DateTime At = new(2026, 11, 23, 20, 59, 59, DateTimeKind.Utc);
    public static RawFuturesStorageBatch Batch(int count, bool mixed, long sequenceBase = 0, string contract = "ES-raw-test")
    {
        var date = DateOnly.FromDateTime(At); var entity = new TickDataEntityId(contract, date, AssetTypeId.Futures);
        var records = new RawFuturesStorageRecord[count];
        for (var index = 0; index < count; index++)
        {
            var ordinal = index + 1; var sequence = sequenceBase + ordinal; var id = new TickDataId(contract, date, sequence, At.AddTicks(index));
            var ns = checked((At.Ticks - DateTime.UnixEpoch.Ticks) * 100 + index);
            var kind = (byte)(mixed && index % 2 == 0 ? 2 : 1); byte[] payload;
            if (kind == 1)
                payload = MessagePackSerializer.Serialize(new FuturesTickTradeDataChangedEvent
                {
                    Subject = new(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor, FuturesTickTradeDataChangedEvent.Verb, entity.Format()),
                    Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = entity, AggregateId = entity.Format(), EventSource = "native-fixture", ReceivedOn = id.TimestampUtc,
                    TickDataId = id, AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", DefinitionDate = date, PublisherId = 1, InstrumentId = 42,
                    TradeData = new((uint)ordinal, ns, ns + 7, 0, 6000000000000, 6000m, (uint)ordinal, (byte)'T', (byte)'B', 8)
                });
            else
                payload = MessagePackSerializer.Serialize(new FuturesTickQuoteDataChangedEvent
                {
                    Subject = new(ActorType.Realtime, FuturesTickQuoteDataChangedEvent.Actor, FuturesTickQuoteDataChangedEvent.Verb, entity.Format()),
                    Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = entity, AggregateId = entity.Format(), EventSource = "native-fixture", ReceivedOn = id.TimestampUtc,
                    TickDataId = id, AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", DefinitionDate = date, PublisherId = 1, InstrumentId = 42,
                    EmissionReason = (QuoteEmissionReason)3, QuoteCount = 1,
                    QuoteData = new([new((uint)ordinal, ns, ns + 7, 0, 6000000000000, 6000m, (uint)ordinal, 1, 6000250000000, 6000.25m, (uint)(ordinal + 1), 2)], 1)
                });
            records[index] = new(ordinal, new Guid(index < count / 2 ? "11111111-1111-1111-1111-111111111111" : "22222222-2222-2222-2222-222222222222"), kind, payload);
        }
        return new(Guid.NewGuid(), 0, records);
    }
    public static string Write(string root, RawFuturesStorageBatch batch)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, batch.SourceEpoch.ToString("N") + ".wal");
        File.WriteAllBytes(Path.ChangeExtension(path, ".owner"), []);
        using var file = File.Create(path);
        file.Write("IFMRAW01"u8); file.Write(batch.SourceEpoch.ToByteArray());
        Span<byte> size = stackalloc byte[4];
        foreach (var value in batch.Records)
        {
            var record = new byte[25 + value.Payload.Length]; record[0] = value.Kind;
            value.PhysicalGeneration.TryWriteBytes(record.AsSpan(1)); BinaryPrimitives.WriteInt64LittleEndian(record.AsSpan(17), value.Ordinal);
            value.Payload.CopyTo(record.AsSpan(25));
            BinaryPrimitives.WriteInt32LittleEndian(size, record.Length);
            file.Write(size); file.Write(record); file.Write(SHA256.HashData(record));
        }
        file.Flush(true); return path;
    }
}
