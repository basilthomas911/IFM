using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Application.Identity;

namespace TomasAI.IFM.Domain.Portfolio.Identity;

/// <summary>Allocates operator-facing business identities from their PostgreSQL-backed named sequences.</summary>
public sealed class PortfolioBusinessIdAllocator(ISequenceIdApi sequenceIdGenerator)
    : IPortfolioBusinessIdAllocator
{
    readonly ISequenceIdApi _sequenceIdGenerator =
        sequenceIdGenerator ?? throw new ArgumentNullException(nameof(sequenceIdGenerator));

    public async ValueTask<PortfolioId> AllocatePortfolioIdAsync(CancellationToken cancellationToken = default) =>
        new(await (AllocatePositiveInt32Async(SequenceName.Portfolio_PortfolioId, cancellationToken)).ConfigureAwait(false));

    public async ValueTask<int> GetPortfolioHighWatermarkAsync(CancellationToken cancellationToken = default)
    {
        var value = await _sequenceIdGenerator
            .GetHighWatermarkAsync(SequenceName.Portfolio_PortfolioId, cancellationToken)
            .ConfigureAwait(false);
        return value <= 0 ? 0 : checked((int)value);
    }

    public ValueTask<int> AllocateFundIdAsync(CancellationToken cancellationToken = default) =>
        AllocatePositiveInt32Async(SequenceName.Fund_FundId, cancellationToken);

    public ValueTask<int> AllocateOrderIdAsync(CancellationToken cancellationToken = default) =>
        AllocatePositiveInt32Async(SequenceName.Trade_OrderId, cancellationToken);

    public ValueTask<int> AllocateTradeIdAsync(CancellationToken cancellationToken = default) =>
        AllocatePositiveInt32Async(SequenceName.Trade_TradeId, cancellationToken);

    public ValueTask<int> AllocatePolicyIdAsync(CancellationToken cancellationToken = default) =>
        AllocatePositiveInt32Async(SequenceName.PortfolioPolicy_PolicyId, cancellationToken);

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
