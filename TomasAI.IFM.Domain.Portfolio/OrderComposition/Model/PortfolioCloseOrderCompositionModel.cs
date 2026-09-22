using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;

/// <summary>Validates and creates one reduce-only order for an existing Portfolio position.</summary>
public static class PortfolioCloseOrderCompositionModel
{
    public static async ValueTask<PortfolioCloseOrderCompositionReceipt> EvaluateAsync(
        EvaluatePortfolioCloseOrderCompositionCommand request,
        FinancialBookConfiguration book,
        long nextRevision,
        PortfolioExecutionOrderInstruction openingOrder,
        IPortfolioBusinessIdAllocator identities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(openingOrder);
        ArgumentNullException.ThrowIfNull(identities);

        var candidate = request.Body;
        var position = candidate.Position;
        if (request.PortfolioId <= 0 || request.OperationId == Guid.Empty ||
            candidate.CompositionId == Guid.Empty || !candidate.WorkflowId.IsValid ||
            candidate.PositionType != PortfolioExecutionPositionType.Closing || !position.Id.IsValid ||
            position.Id != candidate.WorkflowId.Position || !position.IsOpen ||
            position.StrategyKind != candidate.StrategyKind || candidate.ValueDate == default ||
            candidate.ValidUntilUtc.Kind != DateTimeKind.Utc || candidate.ValidUntilUtc <= request.RequestedAtUtc ||
            candidate.Component.ComponentId == Guid.Empty || candidate.Component.StrategyKind != candidate.StrategyKind ||
            candidate.Component.ReservedTradeId != position.Id.TradeId ||
            candidate.EvidenceHash.Length != 64 || request.InputSha256.Length != 64)
            throw new ArgumentException("Portfolio close-order composition input is incomplete or invalid.", nameof(request));

        if (book.PortfolioId != request.PortfolioId ||
            book.Funds.All(fund => fund.FundId != position.Id.FundId))
            throw new InvalidOperationException("The target position is outside this Portfolio financial authority.");

        ValidateOpeningOrder(openingOrder, position);
        ValidateReduceOnly(candidate.Component, position);

        var orderId = await identities.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
        var order = new PortfolioExecutionOrderInstruction
        {
            Id = new(request.PortfolioId, position.Id.FundId, orderId),
            Revision = 1,
            Status = PortfolioExecutionOrderStatus.Approved,
            PositionType = PortfolioExecutionPositionType.Closing,
            TargetPosition = position.Id,
            ValueDate = candidate.ValueDate,
            ValidUntilUtc = candidate.ValidUntilUtc,
            Origin = candidate.Origin,
            Components = [candidate.Component with { Legs = [.. candidate.Component.Legs] }],
            DefinitionHash = candidate.EvidenceHash,
            BrokerAccountAlias = openingOrder.BrokerAccountAlias,
            BrokerEnvironment = openingOrder.BrokerEnvironment,
            PortfolioApprovalId = request.OperationId,
            MicroExecutionProfileId = openingOrder.MicroExecutionProfileId,
            MicroExecutionProfileVersion = openingOrder.MicroExecutionProfileVersion,
            MicroExecutionProfileHash = openingOrder.MicroExecutionProfileHash,
            AccountPromotionApprovalReference = openingOrder.AccountPromotionApprovalReference,
            RequiredCapital = openingOrder.RequiredCapital,
            MaximumLoss = openingOrder.MaximumLoss,
            BrokerOrderType = openingOrder.BrokerOrderType,
            BrokerAlgorithm = openingOrder.BrokerAlgorithm
        };

        return new PortfolioCloseOrderCompositionReceipt
        {
            CompositionId = candidate.CompositionId,
            WorkflowId = candidate.WorkflowId,
            Status = PortfolioCloseOrderCompositionStatus.ExecuteTradeOrder,
            TradeOrder = order,
            FinancialRevision = nextRevision,
            PortfolioId = request.PortfolioId,
            ReasonCode = "Accepted"
        };
    }

    static void ValidateOpeningOrder(PortfolioExecutionOrderInstruction openingOrder, PortfolioPositionSnapshot position)
    {
        if (openingOrder.PositionType != PortfolioExecutionPositionType.Opening ||
            openingOrder.Id.PortfolioId != position.Id.PortfolioId ||
            openingOrder.Id.FundId != position.Id.FundId ||
            openingOrder.Id.OrderId != position.Id.OrderId)
            throw new InvalidOperationException("The target opening Trade Order does not match the position identity.");

        var component = openingOrder.Components.SingleOrDefault(value =>
            value.ReservedTradeId == position.Id.TradeId);
        if (component is null || component.StrategyKind != position.StrategyKind)
            throw new InvalidOperationException("The target Trade is not present in its opening Trade Order.");
    }

    static void ValidateReduceOnly(PortfolioExecutionComponent close, PortfolioPositionSnapshot position)
    {
        if (close.Legs.Length != position.Legs.Length || close.Legs.Length == 0)
            throw new InvalidOperationException("The close order must contain every remaining position leg.");

        foreach (var openLeg in position.Legs)
        {
            var closeLeg = close.Legs.SingleOrDefault(value => value.TradeLegId == openLeg.TradeLegId);
            if (closeLeg is null || !string.Equals(closeLeg.ContractId, openLeg.ContractId, StringComparison.Ordinal) ||
                closeLeg.AssetFamily != openLeg.AssetFamily || closeLeg.SignedQuantity != checked(-openLeg.SignedQuantity) ||
                closeLeg.SignedQuantity == 0)
                throw new InvalidOperationException(
                    $"Close leg {openLeg.TradeLegId} must exactly reverse the remaining position quantity.");
        }

        if (close.Legs.Select(static leg => leg.TradeLegId).Distinct().Count() != close.Legs.Length)
            throw new InvalidOperationException("The close order contains duplicate Trade Leg identities.");
    }
}
