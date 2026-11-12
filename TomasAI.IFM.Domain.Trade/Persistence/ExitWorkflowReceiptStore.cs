using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Persistence;

internal static class ExitWorkflowReceiptStore
{
    internal static async Task<TEvent?> ReadAsync<TEvent>(EnlistedEventTransaction db, string stream, Guid commandId, CancellationToken token)
        where TEvent : class, IEvent
    {
        var rows = await db.QueryAsync(TradePlanDbSql.ReadCommittedExitWorkflowEvidence, [stream, commandId], Map, token).ConfigureAwait(false);
        return rows.Count == 1 ? rows[0].ToDomainEvent() as TEvent : null;
    }

    internal static async Task<IEvent?> LatestOwnerSourceAsync(EnlistedEventTransaction db, ExitPositionWorkflowId workflow,
        TradeStrategyKind kind, CancellationToken token)
    {
        var stream = new ActorSubject(ActorType.Command, ExitPositionWorkflowActorNames.Owner(kind), string.Empty, workflow.Format()).StreamId;
        var rows = await db.QueryAsync(TradePlanDbSql.ReadLatestExitWorkflowOwnerSource, [stream], Map, token).ConfigureAwait(false);
        return rows.SingleOrDefault()?.ToDomainEvent();
    }

    static EventLogReadModel Map(System.Data.Common.DbDataReader row) => new(row.GetInt64(0), row.GetString(1), row.GetString(2),
        row.GetInt64(3), row.GetFieldValue<byte[]>(4), row.GetGuid(5), row.GetString(6), row.GetInt64(7));

    internal static Task<ExitOrderCompositionCompletedEvent?> CompositionAsync(EnlistedEventTransaction db,
        ExitPositionWorkflowStartedEvent started, CancellationToken token)
        => ReadAsync<ExitOrderCompositionCompletedEvent>(db,
            FunctionStream(started.EntityId, ExitWorkflowLifecycleIdentity.Functions(started.StrategyKind).Composer, ComposeExitOrderCommand.Verb),
            ExitWorkflowLifecycleIdentity.Function(started.EntityId, ComposeExitOrderCommand.Verb), token);

    internal static Task<PositionExitRiskCompletedEvent?> RiskAsync(EnlistedEventTransaction db,
        ExitPositionWorkflowStartedEvent started, CancellationToken token)
        => ReadAsync<PositionExitRiskCompletedEvent>(db,
            FunctionStream(started.EntityId, ExitWorkflowLifecycleIdentity.Functions(started.StrategyKind).Risk, EvaluatePositionExitRiskCommand.Verb),
            ExitWorkflowLifecycleIdentity.Function(started.EntityId, EvaluatePositionExitRiskCommand.Verb), token);

    internal static Task<TradeOrderChangedEvent?> BindingAsync(EnlistedEventTransaction db,
        PortfolioCloseRiskDecision decision, CancellationToken token)
    {
        var order = decision.TradeOrder ?? throw new InvalidDataException("An execution binding requires an authorized Trade Order.");
        var stream = new ActorSubject(ActorType.Command, TradeOrderActorNames.Command, BindTradeOrderExecutionCommand.Verb, order.Id.Format()).StreamId;
        return ReadAsync<TradeOrderChangedEvent>(db, stream, ExitWorkflowLifecycleIdentity.OrderHandoff(decision, "bind"), token);
    }

    internal static async Task VerifyAsync(EnlistedEventTransaction db, ICommand command, IEvent source, CancellationToken token)
    {
        if (source is not ExitPositionWorkflowLifecycleEvent stage) return;
        var started = stage.Snapshot.Started;
        var verified = command switch
        {
            RecordExitOrderCompositionCommand composition =>
                await CompositionAsync(db, started, token).ConfigureAwait(false) is { } recorded &&
                SameReceipt(recorded, composition.Receipt, recorded.RequestFingerprint, composition.Receipt.RequestFingerprint) &&
                SamePayload(recorded.Composition, composition.Receipt.Composition),
            RecordExitRiskDecisionCommand risk =>
                await RiskAsync(db, started, token).ConfigureAwait(false) is { } recorded &&
                SameReceipt(recorded, risk.Receipt, recorded.RequestFingerprint, risk.Receipt.RequestFingerprint) &&
                SamePayload(recorded.Decision, risk.Receipt.Decision),
            CompleteExitPositionWorkflowCommand completion when stage.Snapshot.Projection.RiskDecision is { } decision =>
                await BindingAsync(db, decision, token).ConfigureAwait(false) is { } binding &&
                binding.CommandId == completion.BindingCommandId && binding.EntityId == decision.TradeOrder!.Id &&
                binding.ExecutionAttemptId == completion.ExecutionAttemptId && binding.ExecutionChannel == ExecutionChannel.Broker,
            FailExitPositionWorkflowCommand => true,
            _ => false
        };
        if (!verified) throw new InvalidDataException($"Missing or different committed evidence for {command.CommandName}.");
    }

    static bool SameReceipt(IEvent<ExitPositionWorkflowId> actual, IEvent<ExitPositionWorkflowId> supplied,
        string actualFingerprint, string suppliedFingerprint) =>
        actual.Id == supplied.Id && actual.CommandId == supplied.CommandId && actual.EntityId == supplied.EntityId &&
        actual.ReceivedOn == supplied.ReceivedOn && string.Equals(actualFingerprint, suppliedFingerprint, StringComparison.Ordinal);

    static bool SamePayload<T>(T actual, T supplied) =>
        string.Equals(FinancialCanonicalHash.Compute(actual), FinancialCanonicalHash.Compute(supplied), StringComparison.Ordinal);

    static string FunctionStream(ExitPositionWorkflowId workflow, string actor, string verb) =>
        new ActorSubject(ActorType.Function, actor, verb, workflow.Format()).StreamId;
}
