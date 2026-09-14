using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;

/// <summary>Maps a strategy close composition to the Portfolio's atomic financial decision.</summary>
public static class PositionExitRiskModel
{
    /// <summary>Submits the close composition to Portfolio and validates its atomic financial decision.</summary>
    /// <param name="command">The strategy-specific exit-risk request.</param>
    /// <param name="portfolio">The Portfolio order-composition authority.</param>
    /// <param name="timeProvider">The clock used for request validity and timestamps.</param>
    /// <param name="cancellationToken">Cancels Portfolio evaluation.</param>
    /// <returns>The validated close-risk decision.</returns>
    public static async ValueTask<PortfolioCloseRiskDecision> EvaluateAsync(
        EvaluatePositionExitRiskCommand command, IPortfolioOrderCompositionApi portfolio,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var composition = command.Composition;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (composition.PositionType != TradeOrderPositionType.Closing ||
            composition.StrategyKind != command.StrategyKind ||
            composition.WorkflowId != command.EntityId || command.CompositionEventId == Guid.Empty)
            throw new InvalidOperationException("EXIT.RISK.INVALID_COMPOSITION");
        var portfolioId = composition.Position.Id.Trade.PortfolioId;
        var operationId = TradePlanContractIdentity.DeterministicId(
            $"{composition.WorkflowId.Format()}|portfolio-close");
        var body = new PortfolioCloseOrderCandidate
        {
            CompositionId = TradePlanContractIdentity.DeterministicId(
                $"{composition.WorkflowId.Format()}|composition"),
            WorkflowId = composition.WorkflowId,
            Position = composition.Position,
            StrategyKind = composition.StrategyKind,
            ValueDate = composition.WorkflowId.ValueDate,
            ValidUntilUtc = now.AddMinutes(2),
            Origin = $"{composition.StrategyKind}ExitPositionWorkflow",
            Component = composition.Component,
            EvidenceHash = composition.CompositionHash,
            PositionType = TradeOrderPositionType.Closing
        };
        var entityId = new FinancialExecutionId(portfolioId, operationId);
        var request = new EvaluatePortfolioCloseOrderCompositionCommand
        {
            CommandId = operationId,
            Subject = new(ActorType.Function, EvaluatePortfolioCloseOrderCompositionCommand.Actor,
                EvaluatePortfolioCloseOrderCompositionCommand.Verb, entityId.Format()),
            EntityId = entityId,
            OperationId = operationId,
            PortfolioId = portfolioId,
            CorrelationId = composition.WorkflowId.ExitDecisionId,
            CausationId = command.CompositionEventId,
            RequestedAtUtc = now,
            ExpiresAtUtc = body.ValidUntilUtc,
            ExpectedFinancialRevision = 0,
            Body = body,
            Access = new("StrategyPositionExitWorkflow", ["OrderCompositionClose"], [portfolioId])
        };
        request = request with { InputSha256 = FinancialCanonicalHash.Request(request) };
        var response = await portfolio.EvaluateCloseAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.Success || response.Value is null)
            throw new InvalidOperationException(
                $"EXIT.PORTFOLIO.REQUEST_FAILED;ErrorCode={response.ErrorCode};ErrorMessage={response.ErrorMessage}");
        if (response.Value.Failed is { } failed)
            throw new InvalidOperationException(
                $"EXIT.PORTFOLIO.DECISION_FAILED;ErrorCode={failed.ErrorCode};ErrorMessage={failed.ErrorMessage};ErrorData={failed.ErrorData}");
        var completed = response.Value.Completed ??
            throw new InvalidOperationException("EXIT.PORTFOLIO.RESULT_MISSING");
        var receipt = completed.Receipt;
        if (completed.CommandId != request.CommandId || completed.OperationId != request.OperationId ||
            completed.InputHash != request.InputSha256 || receipt.WorkflowId != composition.WorkflowId ||
            receipt.PortfolioId != portfolioId ||
            receipt.Status == PortfolioCloseOrderCompositionStatus.ExecuteTradeOrder && receipt.TradeOrder is null ||
            receipt.Status == PortfolioCloseOrderCompositionStatus.NoTradeOrder && receipt.TradeOrder is not null)
            throw new InvalidOperationException("EXIT.PORTFOLIO.RESULT_INVALID");
        if (receipt.TradeOrder is { } order &&
            (order.PositionType != TradeOrderPositionType.Closing ||
             order.TargetPositionId != composition.Position.Id || order.Components.Length != 1 ||
             order.Components[0].ReservedTradeId != composition.Position.Id.Trade.TradeId))
            throw new InvalidOperationException("EXIT.PORTFOLIO.CLOSE_ORDER_INVALID");
        return new()
        {
            PortfolioCompletedEventId = completed.Id,
            TradeOrder = receipt.TradeOrder,
            ExecuteTradeOrder = receipt.Status == PortfolioCloseOrderCompositionStatus.ExecuteTradeOrder,
            ReasonCode = receipt.ReasonCode,
            FinancialRevision = receipt.FinancialRevision
        };
    }
}
