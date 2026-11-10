using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Adopts an existing approved order without allocating another order or trade identity.</summary>
public static class RegisterStrategyFundOrder
{
    extension(RegisterStrategyFundOrderCommand command)
    {
        public ServiceResult<GuidResult> Execute(PortfolioFundAggregate state, DateTime now, string principal)
        {
            var hash = FinancialCanonicalHash.Compute(new { command.Order, command.WorkflowId, command.SourceCommandId, command.SourceEventVersion, command.UnderlyingContractId });
            if (state.TryComposition(command.Order.PortfolioApprovalId, out var existing))
                return existing.CanonicalRequestSha256 == hash ? new ServiceOk<GuidResult>(new(command.CommandId))
                    : new ServiceFailed<GuidResult>(command.ErrorCode, "STRATEGY_FUND_ORDER.IDEMPOTENCY_CONFLICT");
            var computed = (FundCompositionReservedCompute)state.ComputeRegisterStrategyOrder(command.CommandId,
                command.Order, command.WorkflowId, command.UnderlyingContractId, hash, now, principal);
            var source = new FundCompositionReservedEvent
            {
                Id = computed.Id, CommandId = command.CommandId, ReceivedOn = now, Revision = computed.Revision,
                OccurredOnUtc = now, Principal = principal, OriginatedOnUtc = now, Reservation = computed.FundCompositionReservation
            };
            return state.Update(source, command) ? new ServiceOk<GuidResult>(new(command.CommandId))
                : new ServiceFailed<GuidResult>(command.ErrorCode, "STRATEGY_FUND_ORDER.APPLY_FAILED");
        }
    }
}
