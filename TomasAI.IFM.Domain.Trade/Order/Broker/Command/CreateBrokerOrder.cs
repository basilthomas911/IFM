using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Broker.Model;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command;

/// <summary>Handles one durable logical broker-order creation intent.</summary>
public static class CreateBrokerOrder
{
    private static ServiceResult<GuidResult> ExecuteCore(
        CreateBrokerOrderCommand command, BrokerOrderCommandState state)
    {
        if (state.Current is { } current)
            return current.OperationId == command.OperationId
                ? new ServiceOk<GuidResult>(new(command.CommandId))
                : command.UpdateFailed("BO.CREATE.CONFLICT; broker order already exists for another operation.");
        if (!BrokerOrderRequestMapper.TryCreate(command.Order, command.EntityId.Execution.ExecutionAttemptId,
                command.EntityId.ComponentId, command.OperationId, out _, out var reason))
            return command.UpdateFailed(reason);
        var next = new BrokerOrderDefinition
        {
            Id = command.EntityId, Order = command.Order, OperationId = command.OperationId,
            Status = BrokerOrderStatus.PlacePending, Revision = 1, ChangedAtUtc = command.EffectiveAtUtc,
            CurrentSignedNetDebitLimit = command.Order.Components
                .Single(component => component.ComponentId == command.EntityId.ComponentId)
                .SignedNetDebitLimit ?? 0m,
            PendingMutation = BrokerMutationKind.Place,
            StatusBeforeMutation = BrokerOrderStatus.Unknown
        };
        return Apply(command, state, next);
    }

    /// <summary>Creates the broker order only when the durable account gate permits its risk direction.</summary>
    public static ServiceResult<GuidResult> Execute(this CreateBrokerOrderCommand command,
        BrokerOrderCommandState state, IBrokerAccountReadStore accounts)
    {
        var account = accounts.Get(new BrokerAccountId(command.Order.BrokerAccountAlias));
        if (account?.Snapshot is not { Complete: true })
            return command.UpdateFailed("BO.ACCOUNT.SNAPSHOT_INCOMPLETE");
        if (command.Order.PositionType == Domain.Trade.Shared.TradeOrderPositionType.Opening)
        {
            if (account.Gate != BrokerAccountOperationalGate.Open)
                return command.UpdateFailed("BO.ACCOUNT.NEW_RISK_GATE_CLOSED");
            if (account.ApprovalId == Guid.Empty ||
                !string.Equals(command.Order.AccountPromotionApprovalReference,
                    account.ApprovalId.ToString("N"), StringComparison.OrdinalIgnoreCase))
                return command.UpdateFailed("BO.ACCOUNT.APPROVAL_MISMATCH");
        }
        return ExecuteCore(command, state);
    }

    private static ServiceResult<GuidResult> Apply(CreateBrokerOrderCommand command,
        BrokerOrderCommandState state, BrokerOrderDefinition next)
    {
        var applied = state.Update(new BrokerOrderChangedEvent
        {
            Subject = new(ActorType.Event, BrokerOrderChangedEvent.Actor, BrokerOrderChangedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            State = next
        }, command);
        return applied ? new ServiceOk<GuidResult>(new(command.CommandId)) : command.UpdateFailed("BO.STATE.APPLY_FAILED");
    }
}
