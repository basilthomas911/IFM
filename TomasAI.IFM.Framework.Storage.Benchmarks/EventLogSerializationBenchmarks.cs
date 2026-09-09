using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>Longer BenchmarkDotNet validation using the identical checksummed twenty-event corpus.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class EventLogSerializationBenchmarks
{
    [Params(1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20)]
    public int Case { get; set; }
    [Params("json", "messagepack", "messagepack-lz4")]
    public string Codec { get; set; } = "json";
    EventLogMessagePackCodec? _binary;
    byte[] _payload = [];
    IEvent _value = null!;
    LegacyJsonEventStreamReadModel _row = null!;

    [GlobalSetup]
    public void Setup()
    {
        var data = EventLogSerializationBaseline.Load(Path.Combine(AppContext.BaseDirectory, "EventLogCorpus", "v1"))[Case - 1];
        _value = data.Value;
        _row = data.Row;
        if (Codec != "json")
        {
            _binary = new EventLogMessagePackCodec(Codec == "messagepack-lz4");
            _payload = _binary.Serialize(_value);
            EventLogSerializationBaseline.VerifySemantic(data.Fixture, _row.EventData,
                _binary.Deserialize(_row.EventTypeName, _row.EventVersion, _payload));
        }
    }

    [Benchmark] public object Serialize() => _binary is null ? _value.ToEventData() : _binary.Serialize(_value);
    [Benchmark] public IEvent Deserialize() => _binary is null ? _row.ToDomainEvent() : _binary.Deserialize(_row.EventTypeName, _row.EventVersion, _payload);
    [Benchmark] public IEvent RoundTrip() => _binary is not null ? _binary.Deserialize(_row.EventTypeName, _row.EventVersion, _binary.Serialize(_value)) : new LegacyJsonEventStreamReadModel {
        EventTypeName = _row.EventTypeName, EventVersion = _row.EventVersion, EventData = _value.ToEventData() }.ToDomainEvent();
}
