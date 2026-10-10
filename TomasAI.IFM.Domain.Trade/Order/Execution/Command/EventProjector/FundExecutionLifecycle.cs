using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.EventProjector;

/// <summary>Forwards committed execution outcomes to the Fund command owner before publishing completion.</summary>
internal static class FundExecutionLifecycle
{
    /// <summary>Records backend acceptance before the execution worker can receive fills.</summary>
    public static Task SubmittedAsync(IActorService actors, TradeOrderDefinition order, Guid attempt, DateTime atUtc) =>
        SendAsync(actors, order, attempt, atUtc, RecordFundTradeSubmissionCommand.Verb);

    /// <summary>Records an established trade after its actual trade and position effects succeed.</summary>
    public static Task OpenedAsync(IActorService actors, TradeOrderDefinition order, EstablishedTradeDefinition trade) =>
        SendAsync(actors, order, trade.ExecutionAttemptId, trade.EstablishedAtUtc, RecordFundTradeOpeningCommand.Verb,
            trade.TradeDate, trade.MaturityDate);

    /// <summary>Finalizes the setup after accounting and actual position closure succeed.</summary>
    public static Task ClosedAsync(IActorService actors, TradeOrderDefinition order, PositionCloseExecution close) =>
        SendAsync(actors, order, close.ExecutionAttemptId, close.CompletedAtUtc, RecordFundTradeClosingCommand.Verb,
            fullyClosed: order.Components.SelectMany(x => x.Legs).All(leg =>
                close.Fills.Where(fill => fill.TradeLegId == leg.TradeLegId).Sum(fill => fill.SignedQuantity) == leg.SignedQuantity));

    /// <summary>Releases an unfilled cancelled/rejected execution without changing an established position.</summary>
    public static Task ReleasedAsync(IActorService actors, OrderExecutionDefinition execution) =>
        SendAsync(actors, execution.Order, execution.ExecutionAttemptId, execution.CompletedAtUtc ?? execution.StartedAtUtc,
            ReleaseFundTradeSubmissionCommand.Verb);

    static async Task SendAsync(IActorService actors, TradeOrderDefinition order, Guid attempt, DateTime atUtc,
        string verb, DateOnly? tradeDate = null, DateOnly? maturityDate = null, bool fullyClosed = false)
    {
        if (order.SetupTrade is not { } setup) return; // Existing fully automated and legacy orders remain supported.
        if (order.Components.Length != 1)
            throw new InvalidOperationException($"FUND_EXECUTION.AMBIGUOUS_SETUP;{order.Id.Format()};{attempt}");
        var id = new PortfolioFundId(order.Id.PortfolioId, order.Id.FundId);
        var evidence = new FundTradeExecutionEvidence
        {
            PortfolioId = id.PortfolioId, FundId = id.FundId, SetupTrade = setup,
            ExecutionOrderId = order.Id.OrderId, ExecutionTradeId = order.Components[0].ReservedTradeId,
            ExecutionAttemptId = attempt, OccurredAtUtc = atUtc, TradeDate = tradeDate, MaturityDate = maturityDate,
            ClosingExecution = order.PositionType == TradeOrderPositionType.Closing,
            OpeningExecutionTradeId = order.TargetPositionId?.Trade.TradeId, FullyClosed = fullyClosed
        };
        var commandId = TradeHandoffIdentity.Create(verb, order.Id.Format(), attempt.ToString("N"));
        var subject = new ActorSubject(ActorType.Command, "PortfolioFundCommand", verb, id.Format());
        var access = PortfolioAccessContext.Workflow("TradeExecution");
        ServiceResult<Guid> result;
        switch (verb)
        {
            case RecordFundTradeSubmissionCommand.Verb:
                result = await actors.SendAsync<RecordFundTradeSubmissionCommand, PortfolioFundId>(new()
                { CommandId = commandId, Subject = subject, EntityId = id, ExecutionEvidence = evidence, Access = access,
                    CorrelationId = attempt, RequestedOnUtc = atUtc }, id).ConfigureAwait(false); break;
            case RecordFundTradeOpeningCommand.Verb:
                result = await actors.SendAsync<RecordFundTradeOpeningCommand, PortfolioFundId>(new()
                { CommandId = commandId, Subject = subject, EntityId = id, ExecutionEvidence = evidence, Access = access,
                    CorrelationId = attempt, RequestedOnUtc = atUtc }, id).ConfigureAwait(false); break;
            case RecordFundTradeClosingCommand.Verb:
                result = await actors.SendAsync<RecordFundTradeClosingCommand, PortfolioFundId>(new()
                { CommandId = commandId, Subject = subject, EntityId = id, ExecutionEvidence = evidence, Access = access,
                    CorrelationId = attempt, RequestedOnUtc = atUtc }, id).ConfigureAwait(false); break;
            case ReleaseFundTradeSubmissionCommand.Verb:
                result = await actors.SendAsync<ReleaseFundTradeSubmissionCommand, PortfolioFundId>(new()
                { CommandId = commandId, Subject = subject, EntityId = id, ExecutionEvidence = evidence, Access = access,
                    CorrelationId = attempt, RequestedOnUtc = atUtc }, id).ConfigureAwait(false); break;
            default: throw new ArgumentOutOfRangeException(nameof(verb));
        }
        if (!result.Success)
            throw new InvalidOperationException($"FUND_EXECUTION.HANDOFF_FAILED;Operation={verb};Portfolio={id.PortfolioId};Fund={id.FundId};SetupOrder={setup.OrderId};SetupTrade={setup.TradeId};ExecutionOrder={order.Id.OrderId};Attempt={attempt};{result.ErrorCode};{result.ErrorMessage}");
    }
}
