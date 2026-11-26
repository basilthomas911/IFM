namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Immutable creation provenance, separate from source timestamps and delivery attempts.</summary>
public sealed record MessageOrigin
{
    string? json, binary;
    internal string GetJson() => Volatile.Read(ref json)
        ?? Cache(ref json, System.Text.Json.JsonSerializer.Serialize(this));
    internal string GetBinary() => Volatile.Read(ref binary)
        ?? Cache(ref binary, MessageOriginBinary.EncodeCore(this));
    static string Cache(ref string? target, string value) => Interlocked.CompareExchange(ref target, value, null) ?? value;
    internal void RememberBinary(string value) => Cache(ref binary, value);
    public bool Equals(MessageOrigin? other) => other is not null
        && OriginatedAtUtc == other.OriginatedAtUtc && ClockDomainId == other.ClockDomainId
        && RunId == other.RunId && Generation == other.Generation && CausationId == other.CausationId;
    public override int GetHashCode() => HashCode.Combine(OriginatedAtUtc, ClockDomainId, RunId, Generation, CausationId);
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
