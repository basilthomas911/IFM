namespace TomasAI.IFM.Shared.EventModelActor;

public enum RealtimeMailboxStorage { Memory, File }

/// <summary>Finite development starting budgets. Qualification is required before activation.</summary>
public sealed class RealtimeMailboxOptions
{
    public bool Enabled { get; set; }
    public bool CapacityQualified { get; set; }
    public RealtimeMailboxStorage Storage { get; set; } = RealtimeMailboxStorage.Memory;
    public string StreamPrefix { get; set; } = "IFM_RT_V1";
    public string Epoch { get; set; } = "development_v1";
    public string Directory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IFM", "realtime-mailboxes");
    public long MaximumActorBytes { get; set; } = 128L * 1024 * 1024;
    public int MaximumMessagesPerMailbox { get; set; } = 4096;
    public int MaximumPayloadBytes { get; set; } = 512 * 1024;
    public int MaximumMailboxes { get; set; } = 16384;
    public int MaximumPendingSources { get; set; } = 4096;
    public long MaximumOutboxBytes { get; set; } = 512L * 1024 * 1024;
    public long MaximumReceiptBytes { get; set; } = 8L * 1024 * 1024 * 1024;
    // Asynchronous broker/receipt I/O slots, not actor CPU workers. Each slot
    // still owns at most one bounded physical message for one mailbox.
    public int DeliveryConcurrency { get; set; } = 64;
    public int CaptureBatchSize { get; set; } = 64;
    public TimeSpan CaptureMaximumDelay { get; set; } = TimeSpan.Zero;
    public TimeSpan FailureRedeliveryDelay { get; set; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(2);

    public void Validate()
    {
        if (Enabled && !CapacityQualified) throw new InvalidOperationException("Realtime mailbox capacity is not qualified; the current backend remains required.");
        if (string.IsNullOrWhiteSpace(Directory) || string.IsNullOrWhiteSpace(Epoch) || string.IsNullOrWhiteSpace(StreamPrefix)
            || StreamPrefix.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') || Epoch.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_')
            || MaximumActorBytes <= 0 || MaximumMessagesPerMailbox <= 0 || MaximumPayloadBytes <= 0 || MaximumMailboxes <= 0
            || MaximumPendingSources <= 0 || MaximumOutboxBytes <= MaximumPayloadBytes || MaximumReceiptBytes < 148 || DeliveryConcurrency is < 1 or > 128 || CaptureBatchSize is < 1 or > 64
            || CaptureMaximumDelay < TimeSpan.Zero || CaptureMaximumDelay > TimeSpan.FromMilliseconds(50) || FailureRedeliveryDelay <= TimeSpan.Zero || PublishTimeout <= TimeSpan.Zero
            || !Enum.IsDefined(Storage)) throw new InvalidOperationException("Invalid realtime mailbox options.");
    }
}
