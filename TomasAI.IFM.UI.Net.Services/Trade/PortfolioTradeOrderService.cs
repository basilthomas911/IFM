using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.UI.Net.Services.Trade;

/// <summary>Describes the Portfolio decision and Trade Order actor handoff produced by one desktop submission.</summary>
/// <param name="PortfolioEventId">The atomic Portfolio completion event.</param>
/// <param name="Status">The Portfolio decision status.</param>
/// <param name="TradeOrders">The accepted orders dispatched to Trade Order actors.</param>
public sealed record PortfolioTradeOrderSubmissionResult(
    Guid PortfolioEventId,
    PortfolioOrderCompositionStatus Status,
    IReadOnlyList<TradeOrderDefinition> TradeOrders);

/// <summary>Submits a broker-neutral candidate to Portfolio and starts every accepted Trade Order lifecycle.</summary>
/// <param name="portfolio">The Portfolio order-composition authority.</param>
/// <param name="lifecycle">The Trade Order actor lifecycle boundary.</param>
public sealed class PortfolioTradeOrderService(
    IPortfolioOrderCompositionApi portfolio,
    ITradeOrderLifecycleApi lifecycle)
{
    readonly IPortfolioOrderCompositionApi _portfolio =
        portfolio ?? throw new ArgumentNullException(nameof(portfolio));
    readonly ITradeOrderLifecycleApi _lifecycle =
        lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));

    /// <summary>Evaluates one opening candidate atomically and dispatches every Portfolio-accepted order.</summary>
    /// <param name="portfolioId">The Portfolio authority that evaluates the candidate.</param>
    /// <param name="candidate">The complete broker-neutral order candidate.</param>
    /// <param name="executionChannel">The channel bound to each accepted order.</param>
    /// <param name="cancellationToken">Cancels the submission.</param>
    /// <returns>The Portfolio completion and accepted Trade Orders.</returns>
    public async Task<PortfolioTradeOrderSubmissionResult> SubmitOpeningAsync(
        int portfolioId,
        PortfolioOrderCandidate candidate,
        ExecutionChannel executionChannel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (portfolioId <= 0)
            throw new ArgumentOutOfRangeException(nameof(portfolioId));
        if (candidate.PositionType != PortfolioExecutionPositionType.Opening)
            throw new ArgumentException("The desktop Trade Order editor can submit opening candidates only.", nameof(candidate));

        var operationId = Guid.NewGuid();
        var requestedAtUtc = DateTime.UtcNow;
        var entityId = new FinancialExecutionId(portfolioId, operationId);
        var request = new EvaluatePortfolioOrderCompositionCommand
        {
            CommandId = operationId,
            Subject = new ActorSubject(ActorType.Function,
                EvaluatePortfolioOrderCompositionCommand.Actor,
                EvaluatePortfolioOrderCompositionCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            OperationId = operationId,
            PortfolioId = portfolioId,
            CorrelationId = operationId,
            CausationId = candidate.CompositionId,
            RequestedAtUtc = requestedAtUtc,
            ExpiresAtUtc = candidate.ValidUntilUtc,
            ExpectedFinancialRevision = 0,
            Body = candidate,
            Access = new FinancialAccess(
                $"Desktop:{Environment.UserName}",
                ["OrderCompositionEvaluate"],
                [portfolioId])
        };
        request = request with { InputSha256 = FinancialCanonicalHash.Request(request) };

        var result = await _portfolio.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            throw new InvalidOperationException(
                $"Portfolio order composition failed ({result.ErrorCode}): {result.ErrorMessage}");
        if (result.Value.Failed is { } failed)
            throw new InvalidOperationException(
                $"Portfolio order composition failed ({failed.ErrorCode}): {failed.ErrorMessage}; {failed.ErrorData}");
        var completed = result.Value.Completed
            ?? throw new InvalidOperationException("Portfolio returned no terminal order-composition result.");
        if (completed.OperationId != operationId || completed.PortfolioId != portfolioId ||
            completed.Receipt.CompositionId != candidate.CompositionId)
            throw new InvalidOperationException("Portfolio returned a mismatched order-composition completion.");

        var instructions = completed.Receipt.TradeOrders;
        if ((completed.Receipt.Status == PortfolioOrderCompositionStatus.ExecuteTradeOrders && instructions.Length == 0) ||
            (completed.Receipt.Status == PortfolioOrderCompositionStatus.NoTradeOrders && instructions.Length != 0))
            throw new InvalidOperationException("Portfolio returned an inconsistent order-composition status.");
        var orders = instructions.Select(PortfolioExecutionContractMapper.ToTradeOrder).ToArray();
        if (orders.Any(order => !order.Id.IsValid || order.Id.PortfolioId != portfolioId ||
                order.PositionType != TradeOrderPositionType.Opening))
            throw new InvalidOperationException("Portfolio returned an invalid opening Trade Order identity.");

        foreach (var order in orders)
        {
            var dispatch = await _lifecycle.SubmitAcceptedAsync(
                order, completed.Id, executionChannel, cancellationToken).ConfigureAwait(false);
            if (!dispatch.Success)
                throw new InvalidOperationException(
                    $"Accepted Trade Order {order.Id.Format()} could not start execution ({dispatch.ErrorCode}): {dispatch.ErrorMessage}");
        }
        return new(completed.Id, completed.Receipt.Status, orders);
    }
}
