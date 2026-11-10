using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Applies a source-verified release without granting workflow callers general Fund administration.</summary>
public static class ReleaseExpiredStrategyFundOrder
{
    extension(ReleaseExpiredStrategyFundOrderCommand command)
    {
        public ServiceResult<GuidResult> Execute(PortfolioFundAggregate state, DateTime now, string principal)
        {
            var change = (FundCompositionStateChangedCompute)state.ComputeCancelComposition(command.CommandId,
                state.Revision, command.OrderId, command.ExpectedVersion,
                "Strategy opening expired before broker submission; accepted capacity released.", now, principal);
            var source = new FundCompositionStateChangedEvent
            {
                Id = change.Id, CommandId = command.CommandId, ReceivedOn = now, Revision = change.Revision,
                OccurredOnUtc = now, Principal = principal, OriginatedOnUtc = now, Order = change.FundOrder
            };
            return state.Update(source, command) ? new ServiceOk<GuidResult>(new(command.CommandId))
                : new ServiceFailed<GuidResult>(command.ErrorCode, "STRATEGY_FUND_EXPIRY.APPLY_FAILED");
        }
    }
}
