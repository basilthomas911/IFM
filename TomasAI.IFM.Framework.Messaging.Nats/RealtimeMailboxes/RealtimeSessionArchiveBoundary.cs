using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>Exact immutable archive offsets bound to an independently frozen initial owner snapshot.</summary>
public sealed record RealtimeSessionArchiveBoundary(int Version, string Kind, string ArchiveSessionId, string Protocol,
    string BoundaryId, string LeaseId, string Owner, string OwnerCheckpointSha256, string PrefixEvidenceSha256,
    long LastCommittedOrdinal, RealtimeSessionArchiveBoundaryFile[] Files);
public sealed record RealtimeSessionArchiveBoundaryFile(string RelativePath, long Length, string Sha256);

/// <summary>Verifies source bytes/commit offsets, not financial ownership or handler success.</summary>
public static class RealtimeSessionArchiveBoundaryVerifier
{
    public static long VerifyInitial(string epochDirectory, RealtimeSessionArchiveBoundary boundary)
    {
        if (boundary.Version != 1 || boundary.Kind != "FrozenOwnerSourceArchiveFloor" || boundary.Owner != "source-outbox"
            || string.IsNullOrWhiteSpace(boundary.BoundaryId) || string.IsNullOrWhiteSpace(boundary.LeaseId)
            || !Hash(boundary.OwnerCheckpointSha256) || !Hash(boundary.PrefixEvidenceSha256))
            throw new InvalidDataException("Initial archive floor is not bound to a frozen owner proof.");
        return VerifyPrefix(epochDirectory, boundary.ArchiveSessionId, boundary.Protocol, boundary.LastCommittedOrdinal, boundary.Files);
    }
    public static long VerifyPrefix(string epochDirectory, string sessionId, string protocol, long declaredRecords,
        IReadOnlyList<RealtimeSessionArchiveBoundaryFile> files)
    {
        if (protocol != "session-archive:v1:ownership-only" || sessionId.Length is < 1 or > 256 || declaredRecords < 0 || files.Count is < 3 or > 100000)
            throw new InvalidDataException("Invalid original archive floor metadata.");
        var expectedEpoch = "archive_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)))[..24];
        var root = Path.GetFullPath(epochDirectory);
        if (Path.GetFileName(root) != expectedEpoch) throw new InvalidDataException("Archive floor belongs to another session epoch.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0, bodies = 0, receiptRecords = -1;
        bool marker = false, delivery = false;
        foreach (var file in files)
        {
            var relative = file.RelativePath.Replace('\\', '/');
            if (!seen.Add(relative) || !Hash(file.Sha256) || file.Length < 0 || file.Length > 145L * 1024 * 1024 * 1024)
                throw new InvalidDataException("Invalid/repeated original archive offset.");
            total = checked(total + file.Length); if (total > 145L * 1024 * 1024 * 1024) throw new InvalidDataException("Archive prefix exceeds finite replay budget.");
            var path = Child(root, relative);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (input.Length < file.Length || HashPrefix(input, file.Length) != file.Sha256.ToLowerInvariant())
                throw new InvalidDataException("Original owner archive prefix changed or was truncated.");
            input.Position = 0;
            if (relative == "archive-only.protocol")
            {
                var expected = Encoding.UTF8.GetBytes(protocol);
                if (file.Length != expected.Length) throw new InvalidDataException("Archive protocol prefix changed.");
                var actual = new byte[expected.Length]; input.ReadExactly(actual);
                if (!actual.AsSpan().SequenceEqual(expected)) throw new InvalidDataException("Foreign archive ownership mode.");
                marker = true;
            }
            else if (relative == "sources/receipts.bin") receiptRecords = VerifyReceipts(input, file.Length);
            else if (relative == "sources/delivery-receipts.bin")
            { if (file.Length != 0) throw new InvalidDataException("Archive-only floor cannot claim domain handler receipts."); delivery = true; }
            else if (relative.StartsWith("sources/", StringComparison.Ordinal) && relative.EndsWith(".rto", StringComparison.Ordinal)
                && !relative[8..].Contains('/')) bodies = checked(bodies + VerifyBodies(input, file.Length));
            else throw new InvalidDataException("Unexpected authoritative archive prefix file.");
        }
        if (!marker || !delivery || receiptRecords != declaredRecords || bodies != declaredRecords)
            throw new InvalidDataException("Archive ordinal disagrees with its original receipt/body commit offsets.");
        return declaredRecords;
    }
    static long VerifyReceipts(Stream input, long length)
    {
        if (length % 148 != 0) throw new InvalidDataException("Torn original archive ownership receipt.");
        var row = new byte[148];
        for (long offset = 0; offset < length; offset += row.Length)
        {
            input.ReadExactly(row);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(row.AsSpan(0, 116)), row.AsSpan(116)))
                throw new InvalidDataException("Original archive receipt checksum changed.");
        }
        return length / 148;
    }
    static long VerifyBodies(Stream input, long length)
    {
        var header = new byte[36]; int group = 0; long records = 0;
        while (input.Position < length)
        {
            if (length - input.Position < 4) throw new InvalidDataException("Torn original archive commit offset.");
            input.ReadExactly(header.AsSpan(0, 4)); var size = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (size == 0) { records += group; group = 0; continue; }
            if (size < 0 || size > 16 * 1024 * 1024 + 131072 || length - input.Position < 32L + size || ++group > 64)
                throw new InvalidDataException("Original archive frame/commit bounds changed.");
            input.ReadExactly(header.AsSpan(4));
            var expected = Convert.ToHexStringLower(header.AsSpan(4));
            if (HashPrefix(input, size) != expected) throw new InvalidDataException("Original archive body checksum changed.");
        }
        if (group != 0) throw new InvalidDataException("Original archive offset is inside an uncommitted group.");
        return records;
    }
    static string HashPrefix(Stream stream, long length)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (length > 0)
            { var read = stream.Read(buffer, 0, (int)Math.Min(length, buffer.Length)); if (read == 0) throw new EndOfStreamException(); hash.AppendData(buffer, 0, read); length -= read; }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
    static bool Hash(string value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    static string Child(string root, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (relative.Split('/', '\\').Any(part=>part is "" or "." or "..") || Path.IsPathRooted(relative) || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive prefix escapes epoch.");
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Archive floor cannot traverse filesystem links.");
        return path;
    }
}
