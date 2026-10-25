using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public interface ICompositionPreparationStore
{
    Task<CompositionPreparation?> ReadAsync(CompositionPreparationKey key, CancellationToken cancellationToken);
    /// <summary>Atomically saves the first capture. A racing caller receives that winner; conflicting workflow inputs fail.</summary>
    Task<CompositionPreparation> CommitAsync(CompositionPreparation proposed, CancellationToken cancellationToken);
}
