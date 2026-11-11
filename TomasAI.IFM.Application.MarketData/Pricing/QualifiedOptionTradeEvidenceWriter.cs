using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Qualifies provider evidence before its immutable persistence write.</summary>
public sealed class QualifiedOptionTradeEvidenceWriter(IOptionTradeEvidenceWriter persistence) : IOptionTradeEvidenceWriter
{
    /// <inheritdoc/>
    public async ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
    {
        RequireEvidence(evidence);
        await persistence.WriteAsync(evidence, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WriteFirstAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
    {
        RequireEvidence(evidence);
        return await persistence.WriteFirstAsync(evidence, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool[]> WriteFirstBatchAsync(IReadOnlyList<OptionTradeEvidence> evidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Count is < 1 or > IOptionTradeEvidenceWriter.MaximumFirstWriteBatch)
            throw new ArgumentOutOfRangeException(nameof(evidence));
        foreach (var item in evidence) RequireEvidence(item);
        return await persistence.WriteFirstBatchAsync(evidence, cancellationToken).ConfigureAwait(false);
    }

    static void RequireEvidence(OptionTradeEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        evidence.Validate();
        if (MessagePackBinarySerializer.MeasureContent(evidence) > 131072)
            throw new ArgumentException("Trade evidence exceeds its bounded payload.", nameof(evidence));
    }
}
