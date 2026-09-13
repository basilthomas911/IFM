using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Model;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;

public static class PortfolioOrderCompositionModel
{
    public static async ValueTask<PortfolioOrderCompositionReceipt> EvaluateAsync(
        EvaluatePortfolioOrderCompositionCommand request,
        FinancialBookConfiguration book,
        long nextRevision,
        IPortfolioBusinessIdAllocator identities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(identities);
        var candidate = request.Body;
        if (request.PortfolioId <= 0 || request.OperationId == Guid.Empty || candidate.CompositionId == Guid.Empty
            || candidate.WorkflowId == Guid.Empty || candidate.Components.Length == 0
            || candidate.Components.Any(component => component.Legs.Length == 0)
            || candidate.ValidUntilUtc.Kind != DateTimeKind.Utc || candidate.ValidUntilUtc <= request.RequestedAtUtc
            || candidate.EvidenceHash.Length != 64 || request.InputSha256.Length != 64)
            throw new ArgumentException("Portfolio order composition input is incomplete or invalid.", nameof(request));
        if (book.PortfolioId != request.PortfolioId || !book.MigrationQualified)
            throw new InvalidOperationException("Portfolio financial authority is unavailable.");

        var decisions = new List<PortfolioFundOrderDecision>(book.Funds.Length);
        var orders = new List<TradeOrderDefinition>(book.Funds.Length);
        foreach (var fund in book.Funds.OrderBy(value => value.FundId))
        {
            if (!fund.CanSpend)
            {
                decisions.Add(new(fund.FundId, false, "FundSpendingDisabled", null));
                continue;
            }
            var orderId = await identities.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
            var components = new TradeOrderComponentDefinition[candidate.Components.Length];
            for (var componentIndex = 0; componentIndex < candidate.Components.Length; componentIndex++)
            {
                var component = candidate.Components[componentIndex];
                components[componentIndex] = component with
                {
                    Legs = [.. component.Legs],
                    ReservedTradeId = await identities.AllocateTradeIdAsync(cancellationToken).ConfigureAwait(false)
                };
            }
            decisions.Add(new(fund.FundId, true, "Accepted", orderId));
            orders.Add(new TradeOrderDefinition
            {
                Id = new(request.PortfolioId, fund.FundId, orderId), Revision = 1,
                Status = TradeOrderStatus.Approved, ValueDate = candidate.ValueDate,
                ValidUntilUtc = candidate.ValidUntilUtc, Origin = candidate.Origin,
                Components = components,
                DefinitionHash = candidate.EvidenceHash
            });
        }
        return new()
        {
            CompositionId = candidate.CompositionId, WorkflowId = candidate.WorkflowId,
            Status = orders.Count == 0 ? PortfolioOrderCompositionStatus.NoTradeOrders : PortfolioOrderCompositionStatus.ExecuteTradeOrders,
            FundDecisions = [.. decisions], TradeOrders = [.. orders], FinancialRevision = nextRevision
        };
    }
}
