using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

/// <summary>
/// Carries a domain projection together with its persistence revision metadata.
/// </summary>
/// <typeparam name="T">The projected domain read-model type.</typeparam>
/// <param name="Value">The projected domain value.</param>
/// <param name="SchemaVersion">The serialized payload schema version.</param>
/// <param name="AggregateVersion">The aggregate version represented by the projection.</param>
/// <param name="SourceEventId">The source event that produced the projection.</param>
/// <param name="UpdatedOnUtc">The UTC projection timestamp.</param>
/// <param name="PayloadHash">The canonical SHA-256 payload hash.</param>
public sealed record PortfolioProjection<T>(
    T Value,
    int SchemaVersion,
    long AggregateVersion,
    long SourceEventId,
    DateTime UpdatedOnUtc,
    string PayloadHash)
{
    /// <summary>Creates projection metadata and a stable hash for the supplied domain value.</summary>
    /// <param name="value">The projected domain value.</param>
    /// <param name="aggregateVersion">The aggregate version represented by the projection.</param>
    /// <param name="sourceEventId">The source event that produced the projection.</param>
    /// <param name="updatedOnUtc">The UTC projection timestamp.</param>
    /// <returns>The completed projection envelope.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "The projection factory returns the same generic typed projection envelope.")]
    public static PortfolioProjection<T> Create(
        T value,
        long aggregateVersion,
        long sourceEventId,
        DateTime updatedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (aggregateVersion <= 0 || sourceEventId <= 0)
            throw new ArgumentOutOfRangeException(nameof(aggregateVersion));
        if (updatedOnUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Projection timestamp must be UTC.", nameof(updatedOnUtc));
        var payload = JsonSerializer.Serialize(value);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return new(value, 1, aggregateVersion, sourceEventId, updatedOnUtc, hash);
    }
}
