namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Immutable creation provenance, separate from source timestamps and delivery attempts.</summary>
public sealed record MessageOrigin
{
    /// <summary>Creates a validated UTC origin without consulting a clock during deserialization.</summary>
    public MessageOrigin(DateTimeOffset originatedAtUtc, string clockDomainId,
        Guid? runId = null, Guid? generation = null, Guid? causationId = null)
    {
        if (originatedAtUtc == default || originatedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Message origin must be a non-default UTC instant.", nameof(originatedAtUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(clockDomainId);
        if (clockDomainId.Length > 128)
            throw new ArgumentException("Clock domain exceeds 128 characters.", nameof(clockDomainId));
        if (runId == Guid.Empty || generation == Guid.Empty || causationId == Guid.Empty)
            throw new ArgumentException("An optional origin identity must be absent or nonempty.");
        OriginatedAtUtc = originatedAtUtc;
        ClockDomainId = clockDomainId;
        RunId = runId;
        Generation = generation;
        CausationId = causationId;
    }

    /// <summary>Application creation time; retries preserve this exact value.</summary>
    public DateTimeOffset OriginatedAtUtc { get; }

    /// <summary>Explicit clock domain; independent of Activity tracing.</summary>
    public string ClockDomainId { get; }

    /// <summary>Optional execution scope; ordinary Development messages have no run.</summary>
    public Guid? RunId { get; }

    /// <summary>Optional worker generation, distinct from message identity.</summary>
    public Guid? Generation { get; }

    /// <summary>Optional triggering message identity; not a replacement for creation time.</summary>
    public Guid? CausationId { get; }
}
