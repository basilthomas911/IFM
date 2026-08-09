namespace TomasAI.IFM.Framework.SequenceId;

public interface ISequenceIdDbContext
{
    Task<long> GetSequenceAllocationSizeAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default);

    Task<long> GetCurrentSequenceIdAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default);

    Task<long> GetNextSequenceIdAsync(
        SequenceName sequenceName,
        CancellationToken cancellationToken = default);
}

