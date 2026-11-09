using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Persistence;

/// <summary>Persists Portfolio command history only in the shared PostgreSQL EventSourceDb.</summary>
public sealed class PortfolioEventStore(IEventSourceActorDbContext eventSourceDb, IPortfolioAuthorityFence? financialFence = null, TimeProvider? timeProvider = null) : IPortfolioEventStore
{
    readonly TimeProvider businessClock = timeProvider ?? TimeProvider.System;
    readonly IEventSourceActorDbContext _eventSourceDb = eventSourceDb ?? throw new ArgumentNullException(nameof(eventSourceDb));

    public async Task VerifyReferencePreparationAsync(ValidatePortfolioReferenceCommand request, CancellationToken cancellationToken = default)
    {
        var events = await LoadAsync(request.StreamId, cancellationToken).ConfigureAwait(false);
        if (events.Count != 1 || events.Single().ToDomainEvent() is not PortfolioReferenceValidationCompletedEvent receipt
            || receipt.CommandId != request.CommandId || receipt.EntityId != request.EntityId
            || !string.Equals(receipt.InputHash, request.ComputeInputHash(), StringComparison.Ordinal))
            throw new InvalidOperationException("The command does not match its committed Reference validation receipt.");
        var age = businessClock.GetUtcNow().UtcDateTime - receipt.ReceivedOn;
        if (age < TimeSpan.FromSeconds(-5) || age > TimeSpan.FromMinutes(2))
            throw new InvalidOperationException("The command's Reference validation receipt has expired. Prepare the command again.");
    }

    public static string PortfolioStream(PortfolioId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        ThrowIfInvalid(id.Validate(), nameof(id));
        return $"Portfolio.{id.Id}";
    }

    public static string FundStream(PortfolioFundId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        ThrowIfInvalid(id.Validate(), nameof(id));
        return $"PortfolioFund.{id.PortfolioId}.{id.FundId}";
    }

    public static string PolicyStream(PortfolioFinancialPolicyId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        ThrowIfInvalid(id.Validate(), nameof(id));
        return $"PortfolioFinancialPolicy.{id.PortfolioId}.{id.PolicyId}";
    }

    public static string PortfolioSnapshotStream(PortfolioId id) => $"{PortfolioStream(id)}.Snapshot";
    public static string FundSnapshotStream(PortfolioFundId id) => $"{FundStream(id)}.Snapshot";
    public static string PolicySnapshotStream(PortfolioFinancialPolicyId id) => $"{PolicyStream(id)}.Snapshot";

    const int CheckpointInterval = 128;

    public Task AppendPortfolioAsync(PortfolioId portfolioId, IPortfolioDomainEvent domainEvent,
        long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default)
        => AppendPortfolioCoreAsync(portfolioId, domainEvent, expectedRevision, metadata, null, null, cancellationToken);

    public Task AppendPortfolioWithStateAsync(PortfolioId portfolioId, IPortfolioDomainEvent domainEvent,
        long expectedRevision, PortfolioAggregate aggregate, PortfolioEventMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCheckpointRevision(aggregate.Revision, domainEvent.Revision);
        var checkpoint = ShouldCapture(domainEvent.Revision, aggregate.IsDeleted)
            ? CapturePortfolio(portfolioId, aggregate, domainEvent.OccurredOnUtc, domainEvent.Principal)
            : null;
        var childFunds = domainEvent is DraftPortfolioDeletedEvent ? aggregate.FundIds.ToArray() : null;
        return AppendPortfolioCoreAsync(portfolioId, domainEvent, expectedRevision, metadata, checkpoint, childFunds, cancellationToken);
    }

    async Task AppendPortfolioCoreAsync(
        PortfolioId portfolioId,
        IPortfolioDomainEvent domainEvent,
        long expectedRevision,
        PortfolioEventMetadata? metadata,
        IEvent? checkpoint,
        IReadOnlyList<int>? draftPortfolioFundIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(portfolioId);
        ArgumentNullException.ThrowIfNull(domainEvent);
        ValidateAppend(domainEvent.Revision, expectedRevision);
        if (domainEvent.ProjectionFacts is not null && financialFence is null)
            throw new InvalidOperationException("Accepted Portfolio projections require the owning PostgreSQL transaction fence.");
        PreparePortfolio(portfolioId,domainEvent,metadata);
        await AppendAsync(portfolioId.Id, new(null, PortfolioStream(portfolioId), domainEvent, expectedRevision, true)
            { Checkpoint = checkpoint, DraftPortfolioFundIds = draftPortfolioFundIds }, cancellationToken).ConfigureAwait(false);
    }

    public Task AppendFundAsync(PortfolioFundId fundId, IPortfolioFundDomainEvent domainEvent,
        long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default)
        => AppendFundCoreAsync(fundId, domainEvent, expectedRevision, metadata, null, cancellationToken);

    public Task AppendFundWithStateAsync(PortfolioFundId fundId, IPortfolioFundDomainEvent domainEvent,
        long expectedRevision, PortfolioFundAggregate aggregate, PortfolioEventMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCheckpointRevision(aggregate.Revision, domainEvent.Revision);
        var checkpoint = ShouldCapture(domainEvent.Revision)
            ? CaptureFund(fundId, aggregate, domainEvent.OccurredOnUtc, domainEvent.Principal)
            : null;
        return AppendFundCoreAsync(fundId, domainEvent, expectedRevision, metadata, checkpoint, cancellationToken);
    }

    async Task AppendFundCoreAsync(
        PortfolioFundId fundId,
        IPortfolioFundDomainEvent domainEvent,
        long expectedRevision,
        PortfolioEventMetadata? metadata,
        IEvent? checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fundId);
        using var trace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.append");
        ArgumentNullException.ThrowIfNull(domainEvent);
        ValidateAppend(domainEvent.Revision, expectedRevision);
        if (domainEvent.ProjectionFacts is not null && financialFence is null)
            throw new InvalidOperationException("Accepted Portfolio projections require the owning PostgreSQL transaction fence.");
        var entity = fundId.Format();
        metadata ??= PortfolioEventMetadata.ForCommand(domainEvent.CommandId, domainEvent.Id, domainEvent.OccurredOnUtc);
        metadata.Validate();
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.Subject), new ActorSubject(ActorType.Event, "PortfolioFund", domainEvent.EventName, entity));
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.AggregateId), entity);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.EntityId), new ActorEntityId(entity));
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.EventSource), nameof(PortfolioFundAggregate));
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.ReceivedOn), domainEvent.OccurredOnUtc);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.CorrelationId), metadata.CorrelationId);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.CausationId), metadata.CausationId);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.OriginatedOnUtc), metadata.OriginatedOnUtc);
        if (domainEvent is TomasAI.IFM.Domain.Portfolio.Shared.Financial.IFundRiskTerminalEvent { TerminalRisk: not null } && financialFence is null)
            throw new InvalidOperationException("Terminal Risk synchronization requires the financial fence.");
        if (domainEvent is TomasAI.IFM.Domain.Portfolio.Shared.Financial.IFundRiskAuthorizedEvent { FinancialAuthorization: not null } && financialFence is null)
            throw new InvalidOperationException("Financial authorization requires the transactional Portfolio fence.");
        await AppendAsync(fundId.PortfolioId, new(fundId.FundId, FundStream(fundId), domainEvent, expectedRevision,
            domainEvent is not (FundCompositionReservedEvent or FundCompositionStateChangedEvent))
        {
            Checkpoint = checkpoint,
            ExpectedPortfolioRevision = metadata.ExpectedPortfolioRevision,
            ExpectedPortfolioVersion = metadata.ExpectedPortfolioVersion,
            ExpectedPortfolioOperatingState = metadata.ExpectedPortfolioOperatingState,
            PreparedIdentifiers = metadata.PreparedIdentifiers
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task AppendPolicyAsync(PortfolioFinancialPolicyId policyId, IPortfolioFinancialPolicyDomainEvent domainEvent,
        long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default)
        => AppendPolicyCoreAsync(policyId, domainEvent, expectedRevision, metadata, null, cancellationToken);

    public Task AppendPolicyWithStateAsync(PortfolioFinancialPolicyId policyId, IPortfolioFinancialPolicyDomainEvent domainEvent,
        long expectedRevision, PortfolioFinancialPolicyAggregate aggregate, PortfolioEventMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCheckpointRevision(aggregate.Revision, domainEvent.Revision);
        var checkpoint = ShouldCapture(domainEvent.Revision, aggregate.IsDeleted)
            ? CapturePolicy(policyId, aggregate, domainEvent.OccurredOnUtc, domainEvent.Principal)
            : null;
        return AppendPolicyCoreAsync(policyId, domainEvent, expectedRevision, metadata, checkpoint, cancellationToken);
    }

    async Task AppendPolicyCoreAsync(PortfolioFinancialPolicyId policyId, IPortfolioFinancialPolicyDomainEvent domainEvent,
        long expectedRevision, PortfolioEventMetadata? metadata, IEvent? checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policyId);
        ArgumentNullException.ThrowIfNull(domainEvent);
        ValidateAppend(domainEvent.Revision, expectedRevision);
        if (domainEvent.ProjectionFacts is not null && financialFence is null)
            throw new InvalidOperationException("Accepted Portfolio projections require the owning PostgreSQL transaction fence.");
        PreparePolicy(policyId,domainEvent,metadata);
        await AppendAsync(policyId.PortfolioId, new(null, PolicyStream(policyId), domainEvent, expectedRevision, true)
            {
                Checkpoint = checkpoint,
                RequiredUnreferencedPolicyId = domainEvent is PortfolioFinancialPolicyRetiredEvent or DraftPortfolioFinancialPolicyDeletedEvent
                    ? policyId.PolicyId : null
            }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Commits policy activation and its Portfolio assignment together with both accepted projections.</summary>
    public Task AppendPolicyAndPortfolioAsync(PortfolioFinancialPolicyId policyId,IPortfolioFinancialPolicyDomainEvent policyEvent,
        PortfolioId portfolioId,IPortfolioDomainEvent portfolioEvent,CancellationToken cancellationToken = default)
        => AppendPolicyAndPortfolioCoreAsync(policyId, policyEvent, null, portfolioId, portfolioEvent, null, cancellationToken);

    public Task AppendPolicyAndPortfolioWithStateAsync(PortfolioFinancialPolicyId policyId, IPortfolioFinancialPolicyDomainEvent policyEvent,
        PortfolioFinancialPolicyAggregate policy, PortfolioId portfolioId, IPortfolioDomainEvent portfolioEvent,
        PortfolioAggregate portfolio, CancellationToken cancellationToken = default)
    {
        ValidateCheckpointRevision(policy.Revision, policyEvent.Revision);
        ValidateCheckpointRevision(portfolio.Revision, portfolioEvent.Revision);
        return AppendPolicyAndPortfolioCoreAsync(policyId, policyEvent,
            ShouldCapture(policy.Revision) ? CapturePolicy(policyId, policy, policyEvent.OccurredOnUtc, policyEvent.Principal) : null,
            portfolioId, portfolioEvent, ShouldCapture(portfolio.Revision)
                ? CapturePortfolio(portfolioId, portfolio, portfolioEvent.OccurredOnUtc, portfolioEvent.Principal) : null,
            cancellationToken);
    }

    Task AppendPolicyAndPortfolioCoreAsync(PortfolioFinancialPolicyId policyId, IPortfolioFinancialPolicyDomainEvent policyEvent,
        IEvent? policyCheckpoint, PortfolioId portfolioId, IPortfolioDomainEvent portfolioEvent, IEvent? portfolioCheckpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policyId);
        ArgumentNullException.ThrowIfNull(portfolioId);
        ArgumentNullException.ThrowIfNull(policyEvent);
        ArgumentNullException.ThrowIfNull(portfolioEvent);
        if (policyId.PortfolioId != portfolioId.Id || policyEvent.CommandId != portfolioEvent.CommandId)
            throw new ArgumentException("Activation and assignment must belong to the same Portfolio and command.",nameof(policyEvent));
        if (policyEvent.ProjectionFacts is null || portfolioEvent.ProjectionFacts is null)
            throw new ArgumentException("Both compound transitions require accepted projection facts.",nameof(policyEvent));
        PreparePolicy(policyId,policyEvent,null);
        PreparePortfolio(portfolioId,portfolioEvent,null);
        return (financialFence ?? throw new InvalidOperationException("Policy activation and assignment require the transaction fence."))
            .AppendBatchAsync(portfolioId.Id,[
                new(null,PolicyStream(policyId),policyEvent,policyEvent.Revision-1,true) { Checkpoint = policyCheckpoint },
                new(null,PortfolioStream(portfolioId),portfolioEvent,portfolioEvent.Revision-1,true) { Checkpoint = portfolioCheckpoint }],cancellationToken);
    }

    async Task AppendAsync(int portfolioId, PortfolioAuthorityAppend operation, CancellationToken cancellationToken)
    {
        if (financialFence is not null)
        {
            if (operation is { Checkpoint: null, ExpectedPortfolioRevision: null, PreparedIdentifiers: null,
                    RequiredUnreferencedPolicyId: null, DraftPortfolioFundIds: null })
                await financialFence.AppendAsync(portfolioId, operation.FundId, operation.Stream, operation.Source,
                    operation.ExpectedRevision, operation.ChangesAuthority, cancellationToken).ConfigureAwait(false);
            else
                await financialFence.AppendAsync(portfolioId, operation, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (operation.ExpectedPortfolioRevision is not null || operation.PreparedIdentifiers is not null ||
            operation.RequiredUnreferencedPolicyId is not null || operation.DraftPortfolioFundIds is not null)
            throw new InvalidOperationException("Prepared Portfolio authority requires the transaction fence.");
        if (operation.Checkpoint is { } checkpoint && _eventSourceDb is EventSourceActorDbContext postgres)
        {
            await postgres.SaveAggregateSourceWithCheckpointAsync(operation.Stream, operation.ExpectedRevision, operation.Source,
                checkpoint, cancellationToken).ConfigureAwait(false);
            return;
        }
        await _eventSourceDb.SaveEventsAsync(operation.Stream, operation.Source.CommandId,
            new DomainEventCollection([operation.Source]), operation.ExpectedRevision, cancellationToken).ConfigureAwait(false);
    }

    static void PreparePortfolio(PortfolioId portfolioId,IPortfolioDomainEvent domainEvent,PortfolioEventMetadata? metadata)
    {
        var entity = portfolioId.Format();
        metadata ??= PortfolioEventMetadata.ForCommand(domainEvent.CommandId, domainEvent.Id, domainEvent.OccurredOnUtc);
        metadata.Validate();
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.Subject), new ActorSubject(ActorType.Event, "Portfolio", domainEvent.EventName, entity));
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.AggregateId), entity);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.EntityId), new ActorEntityId(entity));
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.EventSource), nameof(PortfolioAggregate));
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.ReceivedOn), domainEvent.OccurredOnUtc);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.CorrelationId), metadata.CorrelationId);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.CausationId), metadata.CausationId);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.OriginatedOnUtc), metadata.OriginatedOnUtc);
    }

    static void PreparePolicy(PortfolioFinancialPolicyId policyId,IPortfolioFinancialPolicyDomainEvent domainEvent,PortfolioEventMetadata? metadata)
    {
        var entity = policyId.Format();
        metadata ??= PortfolioEventMetadata.ForCommand(domainEvent.CommandId, domainEvent.Id, domainEvent.OccurredOnUtc);
        metadata.Validate();
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.Subject), new ActorSubject(ActorType.Event, "PortfolioFinancialPolicy", domainEvent.EventName, entity));
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.AggregateId), entity);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.EntityId), new ActorEntityId(entity));
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.EventSource), nameof(PortfolioFinancialPolicyAggregate));
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.ReceivedOn), domainEvent.OccurredOnUtc);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.CorrelationId), metadata.CorrelationId);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.CausationId), metadata.CausationId);
        EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.OriginatedOnUtc), metadata.OriginatedOnUtc);
    }

    public Task<PortfolioFinancialPolicyAggregate> LoadPolicyAsync(PortfolioFinancialPolicyId policyId, CancellationToken cancellationToken = default)
        => LoadAggregateAsync<PortfolioFinancialPolicyAggregate, IPortfolioFinancialPolicyDomainEvent,
            PortfolioFinancialPolicyCreatedEvent, PortfolioFinancialPolicySnapshotCaptured>(
            PolicyStream(policyId), PolicySnapshotStream(policyId), x => x.SourceRevision, (state, snapshot, _) =>
            {
                state.RestoreSnapshot(snapshot.State);
                return state.Current?.PortfolioId == policyId.PortfolioId && state.Current.PolicyId == policyId.PolicyId;
            }, x => x.Revision, x => x.Revision, (state, tail) => state.Replay(tail), cancellationToken);

    public async Task<IReadOnlyList<IPortfolioFinancialPolicyDomainEvent>> LoadPolicyHistoryAsync(PortfolioFinancialPolicyId policyId, CancellationToken cancellationToken = default) =>
        ConvertRequired<IPortfolioFinancialPolicyDomainEvent>(await LoadAsync(PolicyStream(policyId), cancellationToken).ConfigureAwait(false), PolicyStream(policyId))
            .OrderBy(x => x.Revision).ToArray();

    public Task<IPortfolioFinancialPolicyDomainEvent?> FindCommittedPolicyCommandAsync(PortfolioFinancialPolicyId policyId, Guid commandId, CancellationToken cancellationToken = default)
        => FindCommittedAsync<IPortfolioFinancialPolicyDomainEvent>(PolicyStream(policyId), commandId, cancellationToken);

    public Task<PortfolioAggregate> LoadPortfolioAsync(PortfolioId portfolioId, CancellationToken cancellationToken = default)
        => LoadAggregateAsync<PortfolioAggregate, IPortfolioDomainEvent, PortfolioCreatedEvent, PortfolioSnapshotCaptured>(
            PortfolioStream(portfolioId), PortfolioSnapshotStream(portfolioId), x => x.SourceRevision, (state, snapshot, head) =>
            {
                // Earlier payloads could not record terminal deletion. Replay the head when that field is absent.
                if (snapshot.State is null || (snapshot.State.IsDeleted is null && snapshot.SourceRevision == head)) return false;
                state.RestoreSnapshot(snapshot.State);
                return state.Current?.PortfolioId == portfolioId.Id;
            }, x => x.Revision, x => x.Revision, (state, tail) => state.Replay(tail), cancellationToken);

    public async Task<IReadOnlyList<IPortfolioDomainEvent>> LoadPortfolioHistoryAsync(PortfolioId portfolioId, CancellationToken cancellationToken = default) =>
        ConvertRequired<IPortfolioDomainEvent>(await LoadAsync(PortfolioStream(portfolioId), cancellationToken).ConfigureAwait(false), PortfolioStream(portfolioId))
            .OrderBy(x => x.Revision).ToArray();

    public async Task<IReadOnlyList<IPortfolioFundDomainEvent>> LoadFundHistoryAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default) =>
        ConvertRequired<IPortfolioFundDomainEvent>(await LoadAsync(FundStream(fundId), cancellationToken).ConfigureAwait(false), FundStream(fundId))
            .OrderBy(x => x.Revision).ToArray();

    public Task<PortfolioFundAggregate> LoadFundAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default)
        => LoadAggregateAsync<PortfolioFundAggregate, IPortfolioFundDomainEvent, FundMandateCreatedEvent, PortfolioFundSnapshotCaptured>(
            FundStream(fundId), FundSnapshotStream(fundId), x => x.SourceRevision, (state, snapshot, _) =>
            {
                state.RestoreSnapshot(snapshot.State);
                return state.Current?.PortfolioId == fundId.PortfolioId && state.Current.FundId == fundId.FundId;
            }, x => x.Revision, x => x.Revision, (state, tail) => state.Replay(tail), cancellationToken);

    public Task SavePortfolioSnapshotAsync(PortfolioId portfolioId, PortfolioAggregate aggregate, DateTime nowUtc,
        string principal, CancellationToken cancellationToken = default)
        => SaveCheckpointAsync(PortfolioStream(portfolioId), aggregate.Revision,
            CapturePortfolio(portfolioId, aggregate, nowUtc, principal), cancellationToken);

    static PortfolioSnapshotCaptured CapturePortfolio(PortfolioId portfolioId, PortfolioAggregate aggregate,
        DateTime nowUtc, string principal)
    {
        ArgumentNullException.ThrowIfNull(portfolioId);
        ArgumentNullException.ThrowIfNull(aggregate);
        ValidateSnapshotAudit(nowUtc, principal);
        if (aggregate.Current?.PortfolioId != portfolioId.Id) throw new InvalidOperationException("Snapshot aggregate identity does not match the stream.");
        var entity = portfolioId.Format();
        return new PortfolioSnapshotCaptured(Guid.NewGuid(), Guid.NewGuid(), aggregate.Revision, aggregate.CaptureSnapshot(), nowUtc, principal)
        {
            Subject = new ActorSubject(ActorType.Event, "Portfolio", nameof(PortfolioSnapshotCaptured), entity),
            AggregateId = entity,
            EntityId = new ActorEntityId(entity)
        };
    }

    public Task SaveFundSnapshotAsync(PortfolioFundId fundId, PortfolioFundAggregate aggregate, DateTime nowUtc,
        string principal, CancellationToken cancellationToken = default)
        => SaveCheckpointAsync(FundStream(fundId), aggregate.Revision, CaptureFund(fundId, aggregate, nowUtc, principal),
            cancellationToken);

    static PortfolioFundSnapshotCaptured CaptureFund(PortfolioFundId fundId, PortfolioFundAggregate aggregate,
        DateTime nowUtc, string principal)
    {
        ArgumentNullException.ThrowIfNull(fundId);
        ArgumentNullException.ThrowIfNull(aggregate);
        ValidateSnapshotAudit(nowUtc, principal);
        if (aggregate.Current?.PortfolioId != fundId.PortfolioId || aggregate.Current.FundId != fundId.FundId) throw new InvalidOperationException("Snapshot aggregate identity does not match the stream.");
        var entity = fundId.Format();
        return new PortfolioFundSnapshotCaptured(Guid.NewGuid(), Guid.NewGuid(), aggregate.Revision, aggregate.CaptureSnapshot(), nowUtc, principal)
        {
            Subject = new ActorSubject(ActorType.Event, "PortfolioFund", nameof(PortfolioFundSnapshotCaptured), entity),
            AggregateId = entity,
            EntityId = new ActorEntityId(entity)
        };
    }

    public Task SavePolicySnapshotAsync(PortfolioFinancialPolicyId policyId, PortfolioFinancialPolicyAggregate aggregate,
        DateTime nowUtc, string principal, CancellationToken cancellationToken = default)
        => SaveCheckpointAsync(PolicyStream(policyId), aggregate.Revision, CapturePolicy(policyId, aggregate, nowUtc, principal),
            cancellationToken);

    static PortfolioFinancialPolicySnapshotCaptured CapturePolicy(PortfolioFinancialPolicyId policyId,
        PortfolioFinancialPolicyAggregate aggregate, DateTime nowUtc, string principal)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ValidateSnapshotAudit(nowUtc, principal);
        if (aggregate.Current?.PortfolioId != policyId.PortfolioId || aggregate.Current.PolicyId != policyId.PolicyId)
            throw new InvalidOperationException("Snapshot aggregate identity does not match the stream.");
        var entity = policyId.Format();
        return new PortfolioFinancialPolicySnapshotCaptured(Guid.NewGuid(), Guid.NewGuid(), aggregate.Revision,
            aggregate.CaptureSnapshot(), nowUtc, principal)
        {
            Subject = new ActorSubject(ActorType.Event, "PortfolioFinancialPolicy", nameof(PortfolioFinancialPolicySnapshotCaptured), entity),
            AggregateId = entity,
            EntityId = new ActorEntityId(entity)
        };
    }

    async Task SaveCheckpointAsync(string stream, long revision, IEvent snapshot, CancellationToken cancellationToken)
    {
        var sidecar = $"{stream}.Snapshot";
        if (_eventSourceDb is EventSourceActorDbContext postgres)
        {
            await postgres.SaveAggregateCheckpointAsync(stream, sidecar, revision, snapshot, cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        var expected = await CurrentStreamVersionAsync(sidecar, cancellationToken).ConfigureAwait(false);
        await _eventSourceDb.SaveEventsAsync(sidecar, snapshot.CommandId, new DomainEventCollection([snapshot]), expected,
            cancellationToken).ConfigureAwait(false);
    }

    static bool ShouldCapture(long revision, bool terminal = false) =>
        revision > 1 && (terminal || revision % CheckpointInterval == 0);

    static void ValidateCheckpointRevision(long stateRevision, long sourceRevision)
    {
        if (stateRevision != sourceRevision)
            throw new InvalidOperationException("Checkpoint state must include the accepted source event.");
    }

    public Task<IPortfolioDomainEvent?> FindCommittedPortfolioCommandAsync(PortfolioId portfolioId, Guid commandId, CancellationToken cancellationToken = default)
        => FindCommittedAsync<IPortfolioDomainEvent>(PortfolioStream(portfolioId), commandId, cancellationToken);

    public async Task<IPortfolioFundDomainEvent?> FindCommittedFundCommandAsync(PortfolioFundId fundId, Guid commandId, CancellationToken cancellationToken = default)
    {
        using var trace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.find_committed_command");
        return await FindCommittedAsync<IPortfolioFundDomainEvent>(FundStream(fundId), commandId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PortfolioCreatedEvent?> FindPortfolioCreateByIdempotencyKeyAsync(PortfolioId portfolioId, Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        var created = await FindCreationAsync<PortfolioCreatedEvent>(PortfolioStream(portfolioId), cancellationToken)
            .ConfigureAwait(false);
        return created?.IdempotencyKey == idempotencyKey ? created : null;
    }

    public async Task<FundMandateCreatedEvent?> FindFundCreateByIdempotencyKeyAsync(PortfolioFundId fundId, Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        var created = await FindCreationAsync<FundMandateCreatedEvent>(FundStream(fundId), cancellationToken)
            .ConfigureAwait(false);
        return created?.IdempotencyKey == idempotencyKey ? created : null;
    }

    async Task<TEvent?> FindCommittedAsync<TEvent>(string stream, Guid commandId, CancellationToken cancellationToken)
        where TEvent : class, IEvent
    {
        if (commandId == Guid.Empty) throw new ArgumentException("CommandId is required.", nameof(commandId));
        if (_eventSourceDb is EventSourceActorDbContext postgres)
        {
            var rows = await postgres.FindAggregateEventsAsync(stream, commandId, cancellationToken).ConfigureAwait(false);
            var result = ConvertRequired<TEvent>(rows, stream).SingleOrDefault();
            if (result is not null && result.CommandId != commandId)
                throw new InvalidDataException("Indexed command identity disagrees with its source event.");
            return result;
        }
        return ConvertRequired<TEvent>(await LoadAsync(stream, cancellationToken).ConfigureAwait(false), stream)
            .SingleOrDefault(source => source.CommandId == commandId);
    }

    async Task<TCreation?> FindCreationAsync<TCreation>(string stream, CancellationToken cancellationToken)
        where TCreation : class, IEvent
    {
        var rows = _eventSourceDb is EventSourceActorDbContext postgres
            ? await postgres.FindAggregateEventsAsync(stream, null, cancellationToken).ConfigureAwait(false)
            : (await LoadAsync(stream, cancellationToken).ConfigureAwait(false)).Where(row => row.StreamVersion == 1).ToArray();
        return ConvertRequired<TCreation>(rows, stream).SingleOrDefault();
    }

    async Task<TState> LoadAggregateAsync<TState, TEvent, TCreation, TCheckpoint>(
        string stream, string sidecar, Func<TCheckpoint, long> sourceRevision,
        Func<TState, TCheckpoint, long, bool> restore, Func<TState, long> revision, Func<TEvent, long> eventRevision,
        Action<TState, IEnumerable<TEvent>> replay, CancellationToken cancellationToken)
        where TState : class, new()
        where TEvent : class, IEvent
        where TCreation : TEvent
        where TCheckpoint : class, IEvent
    {
        using var trace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.aggregate.load");
        TState? restored = null;
        IReadOnlyCollection<EventStreamReadModel> sourceRows;
        long head;
        if (_eventSourceDb is EventSourceActorDbContext postgres)
        {
            var window = await postgres.ReadAggregateCheckpointWindowAsync(stream, sidecar, cancellationToken)
                .ConfigureAwait(false);
            head = window.Head;
            var rowsRead = window.Rows.Count(row => row.Event is not null);
            var indexed = window.Rows.SingleOrDefault(row => row.Kind == AggregateCheckpointRowKind.IndexedCheckpoint);
            while (indexed is not null)
            {
                restored = Restore(indexed.Event!, indexed.SourceRevision, head);
                if (restored is not null) break;
                window = await postgres.ReadAggregateCheckpointWindowAsync(stream, sidecar, cancellationToken,
                    through: head, maximumCheckpointRevision: indexed.SourceRevision - 1).ConfigureAwait(false);
                rowsRead += window.Rows.Count(row => row.Event is not null);
                indexed = window.Rows.SingleOrDefault(row => row.Kind == AggregateCheckpointRowKind.IndexedCheckpoint);
            }

            var legacy = window.Rows.Where(row => row.Kind == AggregateCheckpointRowKind.LegacyCheckpoint).ToArray();
            if (legacy.Length > 0)
            {
                var compatible = LatestLegacy(legacy.Select(row => row.Event!), head);
                if (restored is null)
                {
                    restored = compatible;
                    window = await postgres.ReadAggregateCheckpointWindowAsync(stream, sidecar, cancellationToken,
                        through: head, replayAfter: restored is null ? 0 : revision(restored)).ConfigureAwait(false);
                    rowsRead += window.Rows.Count(row => row.Event is not null);
                }
                else if (compatible is not null && revision(compatible) > revision(restored))
                    restored = compatible;
            }
            var after = restored is null ? 0 : revision(restored);
            sourceRows = window.Rows.Where(row => row.Kind == AggregateCheckpointRowKind.SourceEvent)
                .Select(row => row.Event!).Where(row => row.StreamVersion > after).ToArray();
            trace?.SetTag("portfolio.events.loaded", rowsRead);
        }
        else
        {
            var rows = await LoadAsync(stream, cancellationToken).ConfigureAwait(false);
            head = rows.Count == 0 ? 0 : rows.Max(row => row.StreamVersion);
            restored = LatestLegacy(await LoadAsync(sidecar, cancellationToken).ConfigureAwait(false), head);
            sourceRows = rows.Where(row => row.StreamVersion > (restored is null ? 0 : revision(restored))).ToArray();
        }

        var state = restored ?? new TState();
        var checkpointRevision = revision(state);
        var nextRevision = checkpointRevision + 1;
        var tail = ConvertRequired<TEvent>(sourceRows, stream);
        foreach (var source in tail)
        {
            if (eventRevision(source) != nextRevision || (nextRevision == 1 && source is not TCreation))
                throw new InvalidDataException($"Aggregate stream '{stream}' has an invalid source event at revision {nextRevision}.");
            nextRevision++;
        }
        if (nextRevision - 1 != head)
            throw new InvalidDataException($"Aggregate stream '{stream}' did not reach its captured head {head}.");
        trace?.SetTag("portfolio.snapshot.used", restored is not null);
        trace?.SetTag("portfolio.snapshot.source_revision", checkpointRevision);
        trace?.SetTag("portfolio.events.replayed", tail.Length);
        replay(state, tail);
        if (revision(state) != head)
            throw new InvalidDataException($"Aggregate '{stream}' replay ended at revision {revision(state)}, expected {head}.");
        return state;

        TState? Restore(EventStreamReadModel row, long? indexedRevision, long capturedHead)
        {
            try
            {
                if (row.ToDomainEvent() is not TCheckpoint snapshot) return null;
                var recordedRevision = sourceRevision(snapshot);
                if (recordedRevision <= 0 || recordedRevision > capturedHead
                    || (indexedRevision.HasValue && recordedRevision != indexedRevision.Value)) return null;
                var candidate = new TState();
                return restore(candidate, snapshot, capturedHead) && revision(candidate) == recordedRevision ? candidate : null;
            }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException
                or ArgumentException or MessagePack.MessagePackSerializationException)
            {
                // The source stream remains authoritative if an optional checkpoint cannot recreate state.
                return null;
            }
        }

        TState? LatestLegacy(IEnumerable<EventStreamReadModel> rows, long capturedHead)
        {
            TState? newest = null;
            foreach (var row in rows.OrderByDescending(row => row.StreamVersion))
            {
                var candidate = Restore(row, null, capturedHead);
                if (candidate is not null && (newest is null || revision(candidate) > revision(newest)))
                    newest = candidate;
            }
            return newest;
        }
    }

    async Task<ICollection<TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel>> LoadAsync(
        string stream,
        CancellationToken cancellationToken)
    {
        using var trace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.events.load");
        cancellationToken.ThrowIfCancellationRequested();
        using var streamTrace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.events.resolve_stream");
        var streamId = await _eventSourceDb.GetEventStreamIdFromDbAsync(stream).ConfigureAwait(false);
        streamTrace?.Stop();
        if (streamId is null) return [];
        using var readTrace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.events.read");
        var events = await _eventSourceDb.LoadActorEventStreamAsync<PortfolioEventStoreState>(streamId.EventStreamId).ConfigureAwait(false);
        readTrace?.Stop();
        trace?.SetTag("portfolio.events.loaded", events.Count);
        cancellationToken.ThrowIfCancellationRequested();
        return events;
    }

    static void ValidateAppend(long eventRevision, long expectedRevision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        if (eventRevision != expectedRevision + 1)
            throw new InvalidOperationException("The event revision must immediately follow the expected stream revision.");
    }

    async Task<long> CurrentStreamVersionAsync(string stream, CancellationToken cancellationToken)
    {
        var rows = await LoadAsync(stream, cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? 0 : rows.Max(x => x.StreamVersion);
    }

    static TEvent[] ConvertRequired<TEvent>(IEnumerable<TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel> rows, string stream)
        where TEvent : class, IEvent
    {
        using var trace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.events.deserialize");
        var converted = new List<TEvent>();
        foreach (var row in rows.OrderBy(x => x.StreamVersion))
        {
            if (row.ToDomainEvent() is not TEvent domainEvent)
                throw new InvalidOperationException($"Event stream '{stream}' contains an unknown or incompatible event contract at stream version {row.StreamVersion}.");
            converted.Add(domainEvent);
        }
        trace?.SetTag("portfolio.events.deserialized", converted.Count);
        return [.. converted];
    }

    static void ValidateSnapshotAudit(DateTime nowUtc, string principal)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Snapshot time must be UTC.", nameof(nowUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
    }

    static void ThrowIfInvalid(IReadOnlyList<string> errors, string parameterName)
    {
        if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors), parameterName);
    }

    sealed class PortfolioEventStoreState : IActorState<PortfolioEventStoreState>
    {
        public ActorThreadId Id { get; set; }
    }
}
