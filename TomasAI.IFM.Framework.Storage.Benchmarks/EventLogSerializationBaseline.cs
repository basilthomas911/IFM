using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using TomasAI.IFM.Application.Storage.CommandLogBenchmark;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

public static class EventLogSerializationBaseline
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public sealed record Fixture(string Id, string File, string Type, long EventVersion, int Utf8Bytes, string Sha256, string[] VolatileDiagnostics);
    public sealed record Corpus(int Version, string Source, Fixture[] Events);
    sealed record Row(string Type, long Version, byte[] Data);
    public sealed record Sample(double MicrosecondsPerOperation, double AllocatedBytesPerOperation);
    public sealed record Result(string Id, string Type, int Utf8Bytes, string Operation, int BatchSize, double MedianMicroseconds,
        double MeanMicroseconds, double StandardDeviationMicroseconds, double AllocatedBytesPerOperation, Sample[] Samples, int EncodedBytes);

    public static async Task RunAsync(string[] args)
    {
        if (args[0] == "--event-log-compare") { Compare(args); return; }
        var directory = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "TomasAI.IFM.Framework.Storage.Benchmarks/EventLogCorpus/v1");
        if (args[0] == "--event-log-verify") { Console.WriteLine($"Verified {Load(directory).Length} checksummed event fixtures and semantic round trips."); return; }
        if (args[0] == "--event-log-capture") { await Capture(directory); return; }
        var binary = args[0] is "--event-log-messagepack" or "--event-log-messagepack-verify";
        if (!binary && args[0] != "--event-log-baseline") throw new ArgumentException("Unknown event-log mode.");
        var compression = binary ? args.ElementAtOrDefault(3) ?? "none" : "none";
        if (compression is not ("none" or "lz4")) throw new ArgumentException("Compression must be none or lz4.");
        var codec = binary ? new EventLogMessagePackCodec(compression == "lz4") : null;
        var output = Path.GetFullPath(args.ElementAtOrDefault(2) ?? "BenchmarkDotNet.Artifacts/event-log-baseline");
        if (File.Exists(Path.Combine(output, "results.json"))) throw new InvalidOperationException("Use a new result directory; existing baselines are immutable.");
        var cases = Load(directory);
        // Verify every binary round trip before timing any case; never benchmark silently lost fields.
        var binaries = new Dictionary<string, byte[]>();
        var failures = new List<string>();
        if (codec is not null)
            foreach (var (fixture, value, row) in cases)
            {
                try
                {
                    var bytes = codec.Serialize(value);
                    var restored = codec.Deserialize(fixture.Type, fixture.EventVersion, bytes);
                    VerifySemantic(fixture, row.EventData, restored);
                    binaries.Add(fixture.Id, bytes);
                }
                catch (Exception ex) { failures.Add(fixture.Id + ": " + ex.Message); }
            }
        if (failures.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, failures));
        if (args[0] == "--event-log-messagepack-verify")
        {
            foreach (var (fixture, _, _) in cases)
            {
                var bytes = binaries[fixture.Id];
                var wrongVersion = (byte[])bytes.Clone(); wrongVersion[1] = 2;
                foreach (var invalid in new[] { Array.Empty<byte>(), bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(), wrongVersion })
                {
                    var rejected = false;
                    try { _ = codec!.Deserialize(fixture.Type, fixture.EventVersion, invalid); }
                    catch (Exception ex) when (ex is MessagePack.MessagePackSerializationException or EndOfStreamException or InvalidDataException or ArgumentException)
                    { rejected = true; }
                    if (!rejected) throw new InvalidDataException($"Malformed payload accepted for {fixture.Id}");
                }
            }
            Console.WriteLine($"Verified 20 {compression} binary round trips and 80 malformed-payload rejections.");
            return;
        }
        Directory.CreateDirectory(output);
        var results = new List<Result>();
        foreach (var (fixture, value, row) in cases)
        {
            foreach (var operation in new[] { "Serialize", "Deserialize", "RoundTrip" })
            {
                Func<object> run = operation switch
                {
                    "Serialize" => () => value.ToEventData(),
                    "Deserialize" => () => row.ToDomainEvent(),
                    _ => () => (new LegacyJsonEventStreamReadModel { EventTypeName = fixture.Type, EventVersion = fixture.EventVersion,
                        EventData = value.ToEventData() }).ToDomainEvent()
                };
                if (codec is not null)
                    run = operation switch
                    {
                        "Serialize" => () => codec.Serialize(value),
                        "Deserialize" => () => codec.Deserialize(fixture.Type, fixture.EventVersion, binaries[fixture.Id]),
                        _ => () => codec.Deserialize(fixture.Type, fixture.EventVersion, codec.Serialize(value))
                    };
                // Per-case warmup includes type resolution, serializer contracts and tiered JIT.
                var warmup = Stopwatch.StartNew();
                do { GC.KeepAlive(run()); } while (warmup.ElapsedMilliseconds < 300);
                var count = 1;
                while (Measure(run, count).MicrosecondsPerOperation * count < 50_000 && count < 8192) count *= 2;
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                var samples = Enumerable.Range(0, 9).Select(_ => Measure(run, count)).ToArray();
                var times = samples.Select(x => x.MicrosecondsPerOperation).Order().ToArray();
                var mean = times.Average();
                results.Add(new(fixture.Id, fixture.Type.Split(',')[0], fixture.Utf8Bytes, operation, count, times[4], mean,
                    Math.Sqrt(times.Sum(x => Math.Pow(x - mean, 2)) / (times.Length - 1)), samples.Average(x => x.AllocatedBytesPerOperation), samples, binary ? binaries[fixture.Id].Length : fixture.Utf8Bytes));
                Console.WriteLine($"{fixture.Id} {operation,-11} {fixture.Utf8Bytes,9} B {times[4],12:F2} us/op");
            }
        }
        var report = new { SchemaVersion = 2, SerializationCodec = binary ? "MessagePack" : "NewtonsoftJson", Compression = compression, MessagePackAssembly = typeof(MessagePack.MessagePackSerializer).Assembly.FullName, Harness = typeof(EventLogSerializationBaseline).Assembly.ManifestModule.ModuleVersionId, CapturedAtUtc = DateTime.UtcNow, CorpusSha256 = Hash(File.ReadAllBytes(Path.Combine(directory, "manifest.json"))),
            Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount, ServerGC = GCSettings.IsServerGC, Stopwatch.Frequency,
            Codec = typeof(LegacyJsonEventStreamReadModel).Assembly.ManifestModule.ModuleVersionId, JsonCodec = typeof(Newtonsoft.Json.JsonConvert).Assembly.FullName,
            Command = Environment.CommandLine, Results = results };
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(report, Json));
        var csv = new StringBuilder("Id,EventType,Utf8Bytes,Operation,BatchSize,MedianUs,MeanUs,StdDevUs,AllocatedBytesPerOp,EncodedBytes\n");
        foreach (var r in results) csv.AppendLine(FormattableString.Invariant($"{r.Id},{r.Type},{r.Utf8Bytes},{r.Operation},{r.BatchSize},{r.MedianMicroseconds:F4},{r.MeanMicroseconds:F4},{r.StandardDeviationMicroseconds:F4},{r.AllocatedBytesPerOperation:F1},{r.EncodedBytes}"));
        File.WriteAllText(Path.Combine(output, "results.csv"), csv.ToString());
    }

    static void Compare(string[] args)
    {
        if (args.Length != 4) throw new ArgumentException("Compare requires baseline.json candidate.json output.csv.");
        using var baseline = JsonDocument.Parse(File.ReadAllText(args[1]));
        using var candidate = JsonDocument.Parse(File.ReadAllText(args[2]));
        if (baseline.RootElement.GetProperty("CorpusSha256").GetString() != candidate.RootElement.GetProperty("CorpusSha256").GetString())
            throw new InvalidDataException("Refusing to compare different corpora.");
        var original = baseline.RootElement.GetProperty("Results").EnumerateArray().ToDictionary(x => x.GetProperty("Id").GetString() + "/" + x.GetProperty("Operation").GetString());
        var revised = candidate.RootElement.GetProperty("Results").EnumerateArray().ToDictionary(x => x.GetProperty("Id").GetString() + "/" + x.GetProperty("Operation").GetString());
        if (original.Count != 60 || !original.Keys.ToHashSet().SetEquals(revised.Keys)) throw new InvalidDataException("Expected the same 60 event/operation pairs.");
        var text = new StringBuilder("EventOperation,BaselineMedianUs,CandidateMedianUs,TimeRatio,CandidateOverBaselineAllocatedRatio\n");
        foreach (var (key, a) in original)
        {
            var b = revised[key];
            var before = a.GetProperty("MedianMicroseconds").GetDouble();
            var after = b.GetProperty("MedianMicroseconds").GetDouble();
            var allocationRatio = b.GetProperty("AllocatedBytesPerOperation").GetDouble() / a.GetProperty("AllocatedBytesPerOperation").GetDouble();
            text.AppendLine(FormattableString.Invariant($"{key},{before:F4},{after:F4},{after / before:F4},{allocationRatio:F4}"));
        }
        if (File.Exists(args[3])) throw new InvalidOperationException("Comparison output already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
        File.WriteAllText(args[3], text.ToString());
    }

    static Sample Measure(Func<object> run, int count)
    {
        object? last = null;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < count; i++) last = run();
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMicroseconds;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        GC.KeepAlive(last);
        return new(elapsed / count, (double)bytes / count);
    }

    public static (Fixture Fixture, IEvent Value, LegacyJsonEventStreamReadModel Row)[] Load(string directory)
    {
        var corpus = JsonSerializer.Deserialize<Corpus>(File.ReadAllText(Path.Combine(directory, "manifest.json")))!;
        if (corpus.Version != 1 || corpus.Events.Length != 20 || corpus.Events.Select(x => x.Id).Distinct().Count() != 20)
            throw new InvalidDataException("The fixed v1 corpus must contain exactly 20 distinct events.");
        return corpus.Events.Select(f =>
        {
            var data = File.ReadAllBytes(Path.Combine(directory, f.File));
            if (data.Length != f.Utf8Bytes || Hash(data) != f.Sha256) throw new InvalidDataException($"Changed fixture: {f.Id}");
            var row = new LegacyJsonEventStreamReadModel { EventTypeName = f.Type, EventVersion = f.EventVersion, EventData = Encoding.UTF8.GetString(data) };
            var value = row.ToDomainEvent();
            if (value.GetType() != Type.GetType(f.Type, throwOnError: true)) throw new InvalidDataException($"Cannot decode {f.Id}");
            VerifySemantic(f, row.EventData, value);
            return (f, value, row);
        }).ToArray();
    }

    public static void VerifySemantic(Fixture f, string json, IEvent value)
    {
        if (value.GetType() != Type.GetType(f.Type, throwOnError: true)) throw new InvalidDataException($"Wrong decoded type: {f.Id}");
        var expected = JToken.Parse(json);
        var actual = JToken.Parse(value.ToEventData());
        foreach (var path in f.VolatileDiagnostics)
        {
            // Only these existing computed diagnostic getters are non-persistent by design.
            if (path is not ("State.CompositionDispatch.OriginatedOn" or "State.CompositionDispatch.OriginatedBy"))
                throw new InvalidDataException("Unapproved semantic comparison exclusion.");
            (expected.SelectToken(path) ?? throw new InvalidDataException(path)).Replace("<computed diagnostic>");
            (actual.SelectToken(path) ?? throw new InvalidDataException(path)).Replace("<computed diagnostic>");
        }
        if (!JToken.DeepEquals(expected, actual))
        {
            var fields = ((JObject)expected).Descendants().OfType<JValue>();
            var differences = fields.Where(x => !JToken.DeepEquals(x, actual.SelectToken(x.Path))).Take(10).Select(x => x.Path);
            throw new InvalidDataException($"Semantic roundtrip mismatch: {f.Id}: {string.Join(", ", differences)}");
        }
    }

    static async Task Capture(string directory)
    {
        if (Directory.Exists(directory)) throw new InvalidOperationException("Capture requires a new directory; never overwrite a frozen corpus.");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            "Host=localhost;Port=5432;Database=event-source-test-db", "System.Data.Postgres");
        var store = new PostgresCommandLogBenchmarkStore(settings, NullLogger<DbProvider>.Instance);
        // Read only the local test store. Rank representative small/large payloads per production event type.
        var rows = await store.Database.Use("EventLogBenchmark.CorpusCapture", """
            WITH ranked AS (
              SELECT n.eventtypename, e.eventversion, e.eventpayload,
                row_number() OVER (PARTITION BY n.eventtypename ORDER BY octet_length(e.eventpayload), e.eventversion DESC) AS small,
                row_number() OVER (PARTITION BY n.eventtypename ORDER BY octet_length(e.eventpayload) DESC, e.eventversion DESC) AS large
              FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
              WHERE n.eventtypename LIKE 'TomasAI.IFM.Domain.%'
                AND n.eventtypename NOT LIKE '%Tests%'
            ) SELECT eventtypename,eventversion,eventpayload FROM ranked WHERE small=1 OR large=1
            ORDER BY octet_length(eventpayload), eventtypename,eventversion
            """).ExecuteQueryAsync<Row>(r => new(r.GetString(0), r.GetLong(1), r.GetBytes(2)));
        var valid = new List<(Row Row, IEvent Value)>();
        foreach (var row in rows)
        {
            var decoded = EventLogMessagePackCodec.Shared.Deserialize(row.Type, row.Version, row.Data);
            if (decoded.GetType() == Type.GetType(row.Type, false)) valid.Add((row, decoded));
        }
        // Prefer twenty distinct event types; retain the largest example of each to exercise nested structures.
        var types = valid.GroupBy(x => x.Row.Type).Select(x => x.MaxBy(v => v.Row.Data.Length)).OrderBy(x => x.Row.Data.Length).ToArray();
        if (types.Length < 20) throw new InvalidOperationException($"Only {types.Length} decodable event types; need 20. Available: {string.Join(",", types.Select(x=>x.Row.Type.Split(',')[0]))}");
        var selected = Enumerable.Range(0, 20).Select(i => types[(int)Math.Round(i * (types.Length - 1) / 19d)]).ToArray();
        Directory.CreateDirectory(directory);
        var fixtures = new List<Fixture>();
        for (var i = 0; i < selected.Length; i++)
        {
            var (row, value) = selected[i];
            var id = $"E{i + 1:00}";
            // Normalize persisted EventId to its authoritative event-log version once, then freeze exact bytes.
            var data = Encoding.UTF8.GetBytes(value.ToEventData());
            File.WriteAllBytes(Path.Combine(directory, id + ".json"), data);
            var diagnostics = JToken.Parse(Encoding.UTF8.GetString(data)).SelectToken("State.CompositionDispatch.OriginatedOn") is null
                ? Array.Empty<string>() : new[] { "State.CompositionDispatch.OriginatedOn", "State.CompositionDispatch.OriginatedBy" };
            fixtures.Add(new(id, id + ".json", row.Type, row.Version, data.Length, Hash(data), diagnostics));
            Console.WriteLine($"{id} {data.Length,9} B {value.GetType().Name}");
        }
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new Corpus(1, "Local synthetic integration-test event store; EventId normalized to persisted version", fixtures.ToArray()), Json));
        _ = Load(directory);
    }

    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
