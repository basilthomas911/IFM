using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

/// <summary>Combines persistence capabilities and retains the shared sequence-service contract.</summary>
public interface ISequenceIdDbContext : IObjectRepository<SequenceIdDbContext>, ISequenceIdDbReadContext,
    ISequenceIdDbWriteContext, TomasAI.IFM.Shared.Identity.ISequenceIdDbContext
{
    /// <summary>Gets the sequence read capability.</summary>
    ISequenceIdDbReadContext DbReader { get; }

    /// <summary>Gets the sequence allocation capability.</summary>
    ISequenceIdDbWriteContext DbWriter { get; }

    /// <inheritdoc cref="ISequenceIdDbReadContext.GetSequenceAllocationSizeAsync"/>
    new Task<long> GetSequenceAllocationSizeAsync(SequenceName sequenceName, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ISequenceIdDbReadContext.GetCurrentSequenceIdAsync"/>
    new Task<long> GetCurrentSequenceIdAsync(SequenceName sequenceName, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ISequenceIdDbWriteContext.GetNextSequenceIdAsync"/>
    new Task<long> GetNextSequenceIdAsync(SequenceName sequenceName, CancellationToken cancellationToken = default);
}
