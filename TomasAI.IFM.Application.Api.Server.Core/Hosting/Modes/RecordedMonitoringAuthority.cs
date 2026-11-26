using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Time;

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;

/// <summary>Test-only original handler-permission evidence. It contains no strategy approval or calculated price.</summary>
public sealed class RecordedMonitoringAuthority : IRecordedRealtimeGenerationAuthority
{
    public const int MaximumEntries = 1_000_000;
    public const long MaximumBytes = 512L * 1024 * 1024;
    public sealed record Header(int Version, string Kind, string SourceArchiveCopyManifestSha256,
        string InitialArchiveBoundarySha256, int Entries);
    public Header Evidence { get; }
    readonly Dictionary<Guid, RecordedRealtimeGenerationAcceptance> accepted;
    public IReadOnlyDictionary<Guid, RecordedRealtimeGenerationAcceptance> Entries => accepted;
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public RecordedMonitoringAuthority(Header header, IEnumerable<RecordedRealtimeGenerationAcceptance> entries)
    {
        if (header.Version != 1 || header.Kind != "OriginalAcceptedMonitoringAuthority" || !IsSha(header.SourceArchiveCopyManifestSha256)
            || !IsSha(header.InitialArchiveBoundarySha256) || header.Entries is < 0 or > MaximumEntries)
            throw new InvalidDataException("Invalid original monitoring authority header.");
        Evidence = header; accepted = new(Math.Min(header.Entries, 65536));
        foreach (var item in entries)
        {
            Validate(item);
            if (accepted.Count >= header.Entries || !accepted.TryAdd(item.EventId, item))
                throw new InvalidDataException("Original monitoring authority has extra or duplicate identities.");
        }
        if (accepted.Count != header.Entries) throw new InvalidDataException("Original monitoring authority is incomplete.");
    }

    public bool Allows(RecordedRealtimeGenerationAcceptance original, Guid currentGenerationId)
        => currentGenerationId != Guid.Empty && accepted.TryGetValue(original.EventId, out var proof) && Same(proof, original);

    public static bool Same(RecordedRealtimeGenerationAcceptance left, RecordedRealtimeGenerationAcceptance right)
        => left.Version == right.Version && left.Subject == right.Subject && left.EventId == right.EventId
           && left.OriginalGenerationId == right.OriginalGenerationId && left.PositionSequence == right.PositionSequence
           && left.RouteGeneration == right.RouteGeneration && string.Equals(left.PayloadSha256, right.PayloadSha256, StringComparison.OrdinalIgnoreCase);

    public static void Validate(RecordedRealtimeGenerationAcceptance item)
    {
        if (item.Version != 1 || !item.Subject.Is(ActorType.Realtime, "IronCondorTradePositionRealtime", "IronCondorMonitoringInputsChanged")
            || string.IsNullOrWhiteSpace(item.Subject.EntityId) || item.Subject.EntityId.Length > 512 || item.EventId == Guid.Empty
            || item.OriginalGenerationId == Guid.Empty || !IsSha(item.PayloadSha256) || item.PositionSequence < 0 || item.RouteGeneration < 0)
            throw new InvalidDataException("Invalid original accepted monitoring identity.");
    }
    static bool IsSha(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    public static RecordedMonitoringAuthority Read(string path, string sha256)
    {
        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Original monitoring evidence exceeds its byte bound.");
        using var file = File.OpenRead(path);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(file)), sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Original monitoring evidence changed.");
        file.Position = 0;
        using var input = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        var header = JsonSerializer.Deserialize<Header>(ReadLine(input) ?? throw new InvalidDataException("Missing authority header."), Json)
            ?? throw new InvalidDataException("Invalid authority header.");
        IEnumerable<RecordedRealtimeGenerationAcceptance> Rows()
        {
            while (ReadLine(input) is { } line)
            {
                var encoded = JsonSerializer.Deserialize<string>(line) ?? throw new InvalidDataException("Missing authority row.");
                yield return MessagePackSerializer.Deserialize<RecordedRealtimeGenerationAcceptance>(Convert.FromBase64String(encoded));
            }
        }
        return new(header, Rows());
    }
    static string? ReadLine(StreamReader reader)
    {
        var line = new StringBuilder(512);
        while (reader.Read() is var value && value != -1)
        {
            if (value == '\n') return line.ToString().TrimEnd('\r');
            if (line.Length >= 8192) throw new InvalidDataException("Original authority row exceeds its metadata bound.");
            line.Append((char)value);
        }
        return line.Length == 0 ? null : line.ToString();
    }
    public void WriteNew(string path)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using (var output = new StreamWriter(file, new UTF8Encoding(false), leaveOpen: true))
        {
            output.WriteLine(JsonSerializer.Serialize(Evidence));
            foreach (var item in accepted.Values.OrderBy(x => x.EventId))
                output.WriteLine(JsonSerializer.Serialize(Convert.ToBase64String(MessagePackSerializer.Serialize(item))));
            output.Flush();
            if (file.Length > MaximumBytes) throw new InvalidDataException("Original authority file exceeds its byte bound.");
            file.Flush(true);
        }
    }
    public static RecordedMonitoringAuthority? LoadFromConfiguration(JsonElement configuration, string restoreRoot)
    {
        if (!configuration.TryGetProperty("recordedMonitoringAuthority", out var setting)) return null;
        var path = Path.GetFullPath(setting.GetProperty("path").GetString()!);
        var relative = Path.GetRelativePath(restoreRoot, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Original monitoring authority must be inside the isolated restore root.");
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Original monitoring authority cannot traverse links.");
        var authority = Read(path, setting.GetProperty("sha256").GetString()!);
        if (authority.Evidence.SourceArchiveCopyManifestSha256 != setting.GetProperty("sourceArchiveCopyManifestSha256").GetString()
            || authority.Evidence.InitialArchiveBoundarySha256 != setting.GetProperty("initialArchiveBoundarySha256").GetString())
            throw new InvalidDataException("Original monitoring authority is bound to a different source boundary.");
        return authority;
    }
}
