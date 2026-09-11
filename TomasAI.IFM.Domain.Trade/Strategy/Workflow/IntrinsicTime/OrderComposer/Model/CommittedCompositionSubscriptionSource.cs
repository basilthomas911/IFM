using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;

/// <summary>Reloads genuine committed domain events; callers cannot supply ownership claims or selected legs.</summary>
public sealed class CommittedCompositionSubscriptionSource(IEventSourceActorDbContext events,
    ICommittedBusinessEventJournal journal, IDurableSubscriptionIntentStore intent,
    ICompositionRoutePlanStore plans, string authorityScope = "IFM") : ICommittedBusinessSubscriptionSource
{
    public static readonly string[] TradeSnapshotNames = [nameof(OptionTradeOrderPlacedEvent), nameof(OptionTradeToOpenEvent),
        nameof(OptionTradeToCloseEvent), nameof(OptionTradeSnapshotEvent)];
    public static readonly string[] EventNames = [nameof(WorkflowStrategyStateUpdatedEvent), .. TradeSnapshotNames,
        nameof(OptionTradePositionOpenedEvent), nameof(OptionTradePositionClosedEvent), nameof(OptionTradeDeletedEvent)];

    public async Task<DurableAuthorityMutation?> ReadAsync(BusinessSubscriptionSourceReference reference, CancellationToken cancellationToken)
    {
        if (reference.EventLogId <= 0) throw new ArgumentException("Committed event-log identity is required.");
        var row = await events.GetEventLogByEventIdAsync(reference.EventLogId, cancellationToken).ConfigureAwait(false);
        if (row is null) return null;
        var value = row.ToDomainEvent();
        var version = row.StreamVersion > 0 ? row.StreamVersion : row.EventVersion;
        if (value.Id != reference.EventId || version != reference.Version || !EventNames.Contains(row.EventName))
            throw new InvalidDataException("Source reference differs from the committed event.");
        return await ReadCommittedAsync(value, reference.Kind, version, row.EventStreamId, row.EventVersion,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<DurableAuthorityMutation?> ReadCommittedAsync(IEvent value,
        BusinessSubscriptionSourceKind kind, long sourceVersion, long eventStreamId, long eventLogId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (sourceVersion <= 0) throw new ArgumentOutOfRangeException(nameof(sourceVersion));
        if (!EventNames.Contains(value.GetType().Name))
            throw new InvalidDataException("The committed event type cannot authorize a business subscription.");
        CompositionContractSelection? selected = null;
        DurableAuthorityStatus status;
        string entity;
        if (value is WorkflowStrategyStateUpdatedEvent workflow)
        {
            if (workflow.State.WorkflowId != workflow.WorkflowId || workflow.State.WorkflowRevision != workflow.WorkflowRevision
                || workflow.State.EntityId != workflow.EntityId || workflow.WorkflowId.Value == Guid.Empty)
                throw new InvalidDataException("Committed workflow snapshot identity is inconsistent.");
            if (kind != BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow)
                throw new InvalidDataException("Workflow event cannot authorize an order or position.");
            entity = workflow.WorkflowId.ToString();
            selected = workflow.State.CompositionContracts;
            status = workflow.State.Status switch
            {
                WorkflowStrategyMachineStatus.Failed or WorkflowStrategyMachineStatus.Cancelled or WorkflowStrategyMachineStatus.TimedOut => DurableAuthorityStatus.Terminal,
                WorkflowStrategyMachineStatus.Started when selected is not null => DurableAuthorityStatus.Active,
                WorkflowStrategyMachineStatus.Completed when workflow.State.Outcome == StrategyWorkflowOutcome.NoTrade => DurableAuthorityStatus.Terminal,
                // Completion alone is not proof that an order owns the contracts. Retain until a committed transfer/terminal fact.
                _ => DurableAuthorityStatus.Unknown
            };
        }
        else
        {
            if (kind == BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow)
                throw new InvalidDataException("Trade event cannot authorize a workflow.");
            entity = value.Subject.EntityId;
            var trade = TradeSnapshot(value);
            if (trade is null && value is not OptionTradeDeletedEvent)
            {
                var prior = await journal.ReadPriorAsync(eventStreamId, eventLogId, TradeSnapshotNames, cancellationToken).ConfigureAwait(false);
                trade = prior is null ? null : TradeSnapshot(prior.ToDomainEvent());
            }
            selected = trade?.CompositionContracts;
            if (selected is not null && (trade!.EntityId.Format() != entity || trade.OptionLegs is null
                || !trade.OptionLegs.Select(x => x.ContractId).Order(StringComparer.Ordinal)
                    .SequenceEqual(selected.ContractIds.Order(StringComparer.Ordinal))))
                throw new InvalidDataException("Committed trade legs differ from the selected contract set.");
            status = kind == BusinessSubscriptionSourceKind.TradePosition
                ? value switch
                {
                    OptionTradePositionOpenedEvent opened when opened.TradePositionState == TradePositionState.Opened => DurableAuthorityStatus.Active,
                    OptionTradePositionClosedEvent closed when closed.TradePositionState == TradePositionState.Closed => DurableAuthorityStatus.Terminal,
                    OptionTradeDeletedEvent => DurableAuthorityStatus.Unknown, // Deleting UI data is not proof a position was closed.
                    _ => DurableAuthorityStatus.Unknown
                }
                : value switch
                {
                    OptionTradePositionOpenedEvent => DurableAuthorityStatus.Terminal, // Position acquisition projects first.
                    OptionTradePositionClosedEvent => DurableAuthorityStatus.Unknown, // Prior metadata cannot resurrect an old working order.
                    OptionTradeDeletedEvent => DurableAuthorityStatus.Unknown,
                    _ when trade?.TradeState == TradeState.OrderCancelled => DurableAuthorityStatus.Terminal,
                    _ when trade?.TradeState is TradeState.OrderPlaced or TradeState.OrderSubmitted or TradeState.OrderPartiallyFilled
                        or TradeState.TradeToOpen or TradeState.TradeToClose => DurableAuthorityStatus.Active,
                    _ => DurableAuthorityStatus.Unknown
                };
        }
        if (status == DurableAuthorityStatus.Active && selected is null) status = DurableAuthorityStatus.Unknown;
        var purpose = kind switch
        {
            BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow => SubscriptionLeasePurpose.Strategy,
            BusinessSubscriptionSourceKind.TradeOrder => SubscriptionLeasePurpose.WorkingOrder,
            _ => SubscriptionLeasePurpose.Position
        };
        var sourceId = kind + ":" + entity;
        var owner = new DurableSubscriptionOwner(kind.ToString(), entity, "SelectedContracts");
        var current = await intent.ReadAsync(authorityScope, "GLBX.MDP3", cancellationToken).ConfigureAwait(false);
        var old = current.Authorities.SingleOrDefault(x => x.SourceId == sourceId);
        if (kind == BusinessSubscriptionSourceKind.TradeOrder && value is OptionTradePositionOpenedEvent
            && (selected is null || !current.Authorities.Where(x => x.Owner.WorkflowType == BusinessSubscriptionSourceKind.TradePosition.ToString()
                    && x.Owner.WorkflowId == entity && x.Status == DurableAuthorityStatus.Active)
                .SelectMany(x => x.Leases).Select(x => x.Ticker.ContractId).ToHashSet(StringComparer.Ordinal)
                .IsSupersetOf(selected.ContractIds)))
            status = DurableAuthorityStatus.Unknown;
        var adds = new List<DurableSubscriptionLease>();
        if (status == DurableAuthorityStatus.Active)
        {
            selected!.Validate();
            var plan = await plans.ReadAsync(selected.PricingPlanId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Committed selection references an unavailable route plan.");
            plan.Validate();
            foreach (var contract in selected.ContractIds.Order(StringComparer.Ordinal))
            {
                var option = plan.Options.SingleOrDefault(x => x.ContractId == contract);
                var future = plan.Futures.SingleOrDefault(x => x.ContractId == contract);
                if (option is null && future is null) throw new InvalidDataException("Selected leg is outside the committed plan.");
                var ticker = new DurableSubscriptionTicker("Databento", plan.Dataset, contract, "mbp-1",
                    option is null ? SubscriptionAssetKind.Futures : SubscriptionAssetKind.FuturesOption,
                    option?.Definition.Underlying, plan.PlanId);
                adds.Add(old?.Leases.SingleOrDefault(x => x.Purpose == purpose && x.Ticker == ticker)
                    ?? new(Identity(sourceId + ":" + value.Id + ":" + contract), 1, purpose, ticker));
            }
        }
        return DurableSubscriptionContract.Freeze(new DurableAuthorityMutation(authorityScope, "GLBX.MDP3",
            Identity(sourceId + ":" + value.Id + ":" + current.Revision), value.Id, current.Revision,
            sourceId, sourceVersion, value.Id, owner, status, "Committed" + status, adds, [], CompleteSourceSnapshot: true));
    }

    public static BusinessSubscriptionSourceReference Reference(EventLogReadModel row, BusinessSubscriptionSourceKind kind)
    {
        var value = row.ToDomainEvent();
        return new(kind, value is WorkflowStrategyStateUpdatedEvent workflow ? workflow.WorkflowId.ToString() : value.Subject.EntityId,
            row.StreamVersion > 0 ? row.StreamVersion : row.EventVersion, value.Id, row.EventVersion);
    }

    static OptionTradeReadModel? TradeSnapshot(IEvent value) => value switch
    {
        OptionTradeOrderPlacedEvent placed => placed.OptionTrade,
        OptionTradeToOpenEvent opened => opened.OptionTrade,
        OptionTradeToCloseEvent closed => closed.OptionTrade,
        OptionTradeSnapshotEvent snapshot => snapshot.OptionTrade,
        _ => null
    };

    static Guid Identity(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
}
