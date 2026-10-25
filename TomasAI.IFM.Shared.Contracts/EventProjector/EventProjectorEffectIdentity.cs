using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Stable identity for one side effect of one immutable source event.
/// </summary>
public sealed record EventProjectorEffectIdentity
{
    public EventProjectorEffectIdentity(
        string projectorName,
        long eventId,
        EventProjectorEffectKind effectKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        if (eventId <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventId), "A persisted source event ID must be positive.");
        if (effectKind == EventProjectorEffectKind.None)
            throw new ArgumentOutOfRangeException(nameof(effectKind), "A concrete projector effect kind is required.");

        ProjectorName = projectorName;
        EventId = eventId;
        EffectKind = effectKind;
    }

    public string ProjectorName { get; }
    public long EventId { get; }
    public EventProjectorEffectKind EffectKind { get; }

    /// <summary>
    /// Gets a deterministic transport/outbox identity. Retry attempts return the same value.
    /// </summary>
    public string MessageId
    {
        get
        {
            var projectorBytes = Encoding.UTF8.GetBytes(ProjectorName);
            var input = new byte[sizeof(int) + projectorBytes.Length + sizeof(long) + sizeof(byte)];
            BinaryPrimitives.WriteInt32BigEndian(input, projectorBytes.Length);
            projectorBytes.CopyTo(input.AsSpan(sizeof(int)));
            BinaryPrimitives.WriteInt64BigEndian(
                input.AsSpan(sizeof(int) + projectorBytes.Length),
                EventId);
            input[^1] = (byte)EffectKind;
            return $"ifm-projector-{Convert.ToHexString(SHA256.HashData(input))}";
        }
    }

    public override string ToString()
        => $"{ProjectorName}:{EventId.ToString(CultureInfo.InvariantCulture)}:{EffectKind}";
}
