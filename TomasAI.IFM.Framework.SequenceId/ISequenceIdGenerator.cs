namespace TomasAI.IFM.Framework.SequenceId;

/// <summary>
/// Allocates application-wide identifiers from durable named sequences.
/// </summary>
public interface ISequenceIdGenerator
{
    /// <summary>
    /// Returns the next identifier for <paramref name="sequenceName"/>.
    /// Identifiers are unique system-wide but may contain gaps and are not
    /// guaranteed to be issued in wall-clock order across application instances.
    /// </summary>
    ValueTask<long> GetSequenceIdAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the highest identifier currently reserved from PostgreSQL for the named sequence.
    /// The value may be greater than the highest identifier already issued because ranges are cached.
    /// </summary>
    ValueTask<long> GetHighWatermarkAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default);
}
