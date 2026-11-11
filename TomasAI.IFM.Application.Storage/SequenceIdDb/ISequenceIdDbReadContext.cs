using TomasAI.IFM.Shared.Identity;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

/// <summary>Reads sequence metadata without reserving new identifiers.</summary>
public interface ISequenceIdDbReadContext
{
    /// <summary>Reads the configured allocation increment.</summary>
    /// <param name="sequenceName">The persisted sequence identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The number of identifiers reserved by one allocation.</returns>
    Task<long> GetSequenceAllocationSizeAsync(SequenceName sequenceName, CancellationToken cancellationToken = default);

    /// <summary>Reads the highest reserved identifier without allocating another block.</summary>
    /// <param name="sequenceName">The persisted sequence identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The highest reserved identifier, or zero before the first allocation.</returns>
    Task<long> GetCurrentSequenceIdAsync(SequenceName sequenceName, CancellationToken cancellationToken = default);
}
