using System.Buffers;
using System.Security.Cryptography;
using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

/// <summary>Durable decision evidence only. No order IDs or reservations exist until this proposal is committed.</summary>
[MessagePackObject]
public sealed record PortfolioEntryApproval
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public EvaluatePortfolioOrderCompositionCommand Request { get; init; } = new();
    PortfolioFundEntryAssessment[] funds = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the established MessagePack array contract with defensive ownership.")]
    [Key(2)] public PortfolioFundEntryAssessment[] Funds { get => [.. funds]; init => funds = value is null ? [] : [.. value]; }
    [Key(3)] public DateTime ProducedAtUtc { get; init; }
    [Key(4)] public DateTime ValidUntilUtc { get; init; }
    [Key(5)] public string PayloadSha256 { get; init; } = string.Empty;
    [Key(6)] public Guid DecisionSessionId { get; init; }

    public PortfolioEntryApproval Seal() => this with { PayloadSha256 = Fingerprint() };
    public bool HasValidFingerprint() => SchemaVersion == 1 && string.Equals(PayloadSha256, Fingerprint(), StringComparison.Ordinal);

    string Fingerprint()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("IFM.PortfolioEntryApproval.v1\0"u8);
        using var buffer = new ApprovalHashWriter(hash);
        MessagePackSerializer.Serialize(buffer, this with { PayloadSha256 = string.Empty });
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>Hashes each written segment; a large evidence document never requires a whole-message hash buffer.</summary>
    sealed class ApprovalHashWriter(IncrementalHash hash) : IBufferWriter<byte>, IDisposable
    {
        const int MaximumBytes = 1024 * 1024;
        byte[] bytes = ArrayPool<byte>.Shared.Rent(4096);
        int count;
        public void Advance(int value)
        {
            if (value < 0 || value > bytes.Length) throw new ArgumentOutOfRangeException(nameof(value));
            if (value > MaximumBytes - count) throw new InvalidOperationException("ENTRY.APPROVAL.TOO_LARGE");
            hash.AppendData(bytes.AsSpan(0, value));
            count += value;
        }
        public Memory<byte> GetMemory(int sizeHint = 0) { Ensure(sizeHint); return bytes; }
        public Span<byte> GetSpan(int sizeHint = 0) { Ensure(sizeHint); return bytes; }
        void Ensure(int sizeHint)
        {
            if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
            var required = Math.Max(1, sizeHint);
            if (required > MaximumBytes) throw new InvalidOperationException("ENTRY.APPROVAL.TOO_LARGE");
            if (required <= bytes.Length) return;
            var replacement = ArrayPool<byte>.Shared.Rent(required);
            ArrayPool<byte>.Shared.Return(bytes, clearArray: true);
            bytes = replacement;
        }
        public void Dispose() { ArrayPool<byte>.Shared.Return(bytes, clearArray: true); bytes = []; }
    }
}

public enum PortfolioEntryCommitOutcome { Committed, Rejected, Unknown }
public sealed record PortfolioEntryCommitResult(PortfolioEntryCommitOutcome Outcome,
    PortfolioOrderCompositionCompletedEvent? Completed, string Reason);
public interface IPortfolioEntryCommitApi
{
    Task<PortfolioEntryCommitResult> CommitOnceAsync(PortfolioEntryApproval approval, CancellationToken cancellationToken);
    Task<PortfolioEntryCommitResult> ReconcileAsync(PortfolioEntryApproval approval, CancellationToken cancellationToken);
}

/// <summary>A new host lifetime cannot restart a previously interrupted financial attempt.</summary>
public sealed class WorkflowDecisionSession
{
    public Guid Id { get; } = Guid.NewGuid();
}
