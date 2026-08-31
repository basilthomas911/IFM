using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Domain.Portfolio.Identity;

public interface IPortfolioBusinessIdHighWatermark
{
    /// <summary>Returns the highest Portfolio ID reserved by the authoritative PostgreSQL sequence without allocating an ID.</summary>
    ValueTask<int> GetPortfolioHighWatermarkAsync(CancellationToken cancellationToken = default);
}

public interface IPortfolioBusinessIdAllocator : IPortfolioBusinessIdHighWatermark
{
    ValueTask<PortfolioId> AllocatePortfolioIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocateFundIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocateOrderIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocateTradeIdAsync(CancellationToken cancellationToken = default);
    ValueTask<int> AllocatePolicyIdAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    ValueTask<int> IPortfolioBusinessIdHighWatermark.GetPortfolioHighWatermarkAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

/// <summary>Allocates operator-facing business identities from their PostgreSQL-backed named sequences.</summary>
public sealed class PortfolioBusinessIdAllocator(ISequenceIdGenerator sequenceIdGenerator)
    : IPortfolioBusinessIdAllocator
{
    readonly ISequenceIdGenerator _sequenceIdGenerator =
        sequenceIdGenerator ?? throw new ArgumentNullException(nameof(sequenceIdGenerator));

    public async ValueTask<PortfolioId> AllocatePortfolioIdAsync(CancellationToken cancellationToken = default) =>
        new(await AllocatePositiveInt32Async(SequenceName.Portfolio_PortfolioId, cancellationToken));

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
