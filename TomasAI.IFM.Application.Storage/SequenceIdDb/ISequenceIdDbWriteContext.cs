using TomasAI.IFM.Shared.Identity;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

/// <summary>Reserves identifiers through PostgreSQL sequences.</summary>
public interface ISequenceIdDbWriteContext
{
    /// <summary>Reserves the next block of identifiers.</summary>
    /// <param name="sequenceName">The persisted sequence identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The first identifier in the newly reserved block.</returns>
    Task<long> GetNextSequenceIdAsync(SequenceName sequenceName, CancellationToken cancellationToken = default);
}
