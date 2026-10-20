using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.UI.Net.Services.Trade;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>Owns preview request revisions; contains no strategy calculation or local fallback.</summary>
public sealed class BrokerTradePreviewQuery(TradeQueryService queries)
{
    long inputRevision;

    /// <summary>Requests backend totals for a draft and rejects failed or superseded responses.</summary>
    /// <param name="request">Exact selected-leg snapshot and draft inputs.</param>
    /// <param name="cancellationToken">Cancellation for this view's request lifetime.</param>
    /// <returns>Backend-computed totals for this request's revision.</returns>
    /// <exception cref="InvalidOperationException">The backend failed or the response belongs to another draft.</exception>
    public async Task<TradeOrderPreview> ExecuteAsync(TradeOrderPreviewRequest request, CancellationToken cancellationToken = default)
    {
        var revision = Interlocked.Increment(ref inputRevision);
        var result = await queries.QueryTradeOrderPreviewAsync(request with { InputRevision = revision }, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Success)
            throw new InvalidOperationException($"Trade preview unavailable ({result.ErrorCode}): {result.ErrorMessage}");
        if (result.Value is not { } preview || preview.InputRevision != revision || Volatile.Read(ref inputRevision) != revision)
            throw new InvalidOperationException("Trade preview response does not match the current draft revision.");
        return preview;
    }
}
