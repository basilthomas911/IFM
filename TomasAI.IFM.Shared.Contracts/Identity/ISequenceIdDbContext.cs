using TomasAI.IFM.Shared.Identity;
namespace TomasAI.IFM.Shared.Identity;

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

