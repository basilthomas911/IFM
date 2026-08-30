using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Domain.Portfolio.Identity;

public interface IPortfolioBusinessIdAllocator
{
    ValueTask<PortfolioId> AllocatePortfolioIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocateFundIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocateOrderIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocateTradeIdAsync(CancellationToken cancellationToken = default);
}

/// <summary>Allocates operator-facing business identities from their PostgreSQL-backed named sequences.</summary>
public sealed class PortfolioBusinessIdAllocator(ISequenceIdGenerator sequenceIdGenerator)
    : IPortfolioBusinessIdAllocator
{
    readonly ISequenceIdGenerator _sequenceIdGenerator =
        sequenceIdGenerator ?? throw new ArgumentNullException(nameof(sequenceIdGenerator));

    public async ValueTask<PortfolioId> AllocatePortfolioIdAsync(CancellationToken cancellationToken = default) =>
        new(await AllocatePositiveInt32Async(SequenceName.Portfolio_PortfolioId, cancellationToken));

    public ValueTask<int> AllocateFundIdAsync(CancellationToken cancellationToken = default) =>
        AllocatePositiveInt32Async(SequenceName.Fund_FundId, cancellationToken);

    public ValueTask<int> AllocateOrderIdAsync(CancellationToken cancellationToken = default) =>
        AllocatePositiveInt32Async(SequenceName.Trade_OrderId, cancellationToken);

    public ValueTask<int> AllocateTradeIdAsync(CancellationToken cancellationToken = default) =>
        AllocatePositiveInt32Async(SequenceName.Trade_TradeId, cancellationToken);

    async ValueTask<int> AllocatePositiveInt32Async(
        SequenceName sequenceName,
        CancellationToken cancellationToken)
    {
        var value = await _sequenceIdGenerator
            .GetSequenceIdAsync(sequenceName, cancellationToken)
            .ConfigureAwait(false);
        if (value <= 0)
            throw new InvalidOperationException(
                $"Sequence '{sequenceName}' returned non-positive business ID {value}.");
        return checked((int)value);
    }
}
