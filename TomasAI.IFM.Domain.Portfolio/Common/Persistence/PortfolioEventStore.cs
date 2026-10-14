using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Persistence;

/// <summary>Persists Portfolio command history only in the shared PostgreSQL EventSourceDb.</summary>
public sealed class PortfolioEventStore(IEventSourceActorDbContext eventSourceDb, IPortfolioAuthorityFence? financialFence = null) : IPortfolioEventStore
{
    readonly IEventSourceActorDbContext _eventSourceDb = eventSourceDb ?? throw new ArgumentNullException(nameof(eventSourceDb));

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

    public async Task AppendPortfolioAsync(
        PortfolioId portfolioId,
        IPortfolioDomainEvent domainEvent,
        long expectedRevision,
        PortfolioEventMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(portfolioId);
        ArgumentNullException.ThrowIfNull(domainEvent);
        ValidateAppend(domainEvent.Revision, expectedRevision);
        if (domainEvent.ProjectionFacts is not null && financialFence is null)
            throw new InvalidOperationException("Accepted Portfolio projections require the owning PostgreSQL transaction fence.");
        PreparePortfolio(portfolioId,domainEvent,metadata);
        if (financialFence is not null)
            await financialFence.AppendAsync(portfolioId.Id, null, PortfolioStream(portfolioId), domainEvent, expectedRevision, true, cancellationToken).ConfigureAwait(false);
        else
            await _eventSourceDb.SaveEventsAsync(PortfolioStream(portfolioId), domainEvent.CommandId, new DomainEventCollection([domainEvent]), expectedRevision, cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendFundAsync(
        PortfolioFundId fundId,
        IPortfolioFundDomainEvent domainEvent,
        long expectedRevision,
        PortfolioEventMetadata? metadata = null,
        CancellationToken cancellationToken = default)
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
        if (financialFence is not null)
            await financialFence.AppendAsync(fundId.PortfolioId, fundId.FundId, FundStream(fundId), domainEvent, expectedRevision,
                domainEvent is not (FundCompositionReservedEvent or FundCompositionStateChangedEvent), cancellationToken).ConfigureAwait(false);
        else
            await _eventSourceDb.SaveEventsAsync(FundStream(fundId), domainEvent.CommandId, new DomainEventCollection([domainEvent]), expectedRevision, cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendPolicyAsync(PortfolioFinancialPolicyId policyId, IPortfolioFinancialPolicyDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policyId);
        ArgumentNullException.ThrowIfNull(domainEvent);
        ValidateAppend(domainEvent.Revision, expectedRevision);
        if (domainEvent.ProjectionFacts is not null && financialFence is null)
            throw new InvalidOperationException("Accepted Portfolio projections require the owning PostgreSQL transaction fence.");
        PreparePolicy(policyId,domainEvent,metadata);
        if (financialFence is not null)
            await financialFence.AppendAsync(policyId.PortfolioId, null, PolicyStream(policyId), domainEvent, expectedRevision, true, cancellationToken).ConfigureAwait(false);
        else
            await _eventSourceDb.SaveEventsAsync(PolicyStream(policyId), domainEvent.CommandId, new DomainEventCollection([domainEvent]), expectedRevision, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Commits policy activation and its Portfolio assignment together with both accepted projections.</summary>
    public Task AppendPolicyAndPortfolioAsync(PortfolioFinancialPolicyId policyId,IPortfolioFinancialPolicyDomainEvent policyEvent,
        PortfolioId portfolioId,IPortfolioDomainEvent portfolioEvent,CancellationToken cancellationToken = default)
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
                new(null,PolicyStream(policyId),policyEvent,policyEvent.Revision-1,true),
                new(null,PortfolioStream(portfolioId),portfolioEvent,portfolioEvent.Revision-1,true)],cancellationToken);
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

    public async Task<PortfolioFinancialPolicyAggregate> LoadPolicyAsync(PortfolioFinancialPolicyId policyId, CancellationToken cancellationToken = default)
    {
        var history = await LoadPolicyHistoryAsync(policyId, cancellationToken).ConfigureAwait(false);
        ValidateHistory(history.Select(x => x.Revision));
        var aggregate = new PortfolioFinancialPolicyAggregate();
        aggregate.Replay(history);
        return aggregate;
    }

    public async Task<IReadOnlyList<IPortfolioFinancialPolicyDomainEvent>> LoadPolicyHistoryAsync(PortfolioFinancialPolicyId policyId, CancellationToken cancellationToken = default) =>
        ConvertRequired<IPortfolioFinancialPolicyDomainEvent>(await LoadAsync(PolicyStream(policyId), cancellationToken).ConfigureAwait(false), PolicyStream(policyId))
            .OrderBy(x => x.Revision).ToArray();

    public async Task<IPortfolioFinancialPolicyDomainEvent?> FindCommittedPolicyCommandAsync(PortfolioFinancialPolicyId policyId, Guid commandId, CancellationToken cancellationToken = default)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("CommandId is required.", nameof(commandId));
        return ConvertRequired<IPortfolioFinancialPolicyDomainEvent>(
                await LoadAsync(PolicyStream(policyId), cancellationToken).ConfigureAwait(false), PolicyStream(policyId))
            .SingleOrDefault(x => x.CommandId == commandId);
    }

    public async Task<PortfolioAggregate> LoadPortfolioAsync(PortfolioId portfolioId, CancellationToken cancellationToken = default)
    {
        var events = await LoadAsync(PortfolioStream(portfolioId), cancellationToken).ConfigureAwait(false);
        var history = ConvertRequired<IPortfolioDomainEvent>(events, PortfolioStream(portfolioId)).OrderBy(x => x.Revision).ToArray();
        ValidateHistory(history.Select(x => x.Revision));
        var aggregate = new PortfolioAggregate();
        var snapshot = (await LoadAsync(PortfolioSnapshotStream(portfolioId), cancellationToken).ConfigureAwait(false))
            .Select(x => x.ToDomainEvent()).OfType<PortfolioSnapshotCaptured>()
            .Where(x => x.SourceRevision <= history.LastOrDefault()?.Revision)
            .OrderByDescending(x => x.SourceRevision).FirstOrDefault();
        if (snapshot is not null) aggregate.RestoreSnapshot(snapshot.State);
        aggregate.Replay(history.Where(x => x.Revision > aggregate.Revision));
        return aggregate;
    }

    public async Task<IReadOnlyList<IPortfolioDomainEvent>> LoadPortfolioHistoryAsync(PortfolioId portfolioId, CancellationToken cancellationToken = default) =>
        ConvertRequired<IPortfolioDomainEvent>(await LoadAsync(PortfolioStream(portfolioId), cancellationToken).ConfigureAwait(false), PortfolioStream(portfolioId))
            .OrderBy(x => x.Revision).ToArray();

    public async Task<IReadOnlyList<IPortfolioFundDomainEvent>> LoadFundHistoryAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default) =>
        ConvertRequired<IPortfolioFundDomainEvent>(await LoadAsync(FundStream(fundId), cancellationToken).ConfigureAwait(false), FundStream(fundId))
            .OrderBy(x => x.Revision).ToArray();

    public async Task<PortfolioFundAggregate> LoadFundAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default)
    {
        using var trace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.load");
        var events = await LoadAsync(FundStream(fundId), cancellationToken).ConfigureAwait(false);
        trace?.SetTag("portfolio.events.loaded", events.Count);
        var history = ConvertRequired<IPortfolioFundDomainEvent>(events, FundStream(fundId)).OrderBy(x => x.Revision).ToArray();
        ValidateHistory(history.Select(x => x.Revision));
        var aggregate = new PortfolioFundAggregate();
        using var snapshotTrace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.load_snapshot");
        var snapshot = (await LoadAsync(FundSnapshotStream(fundId), cancellationToken).ConfigureAwait(false))
            .Select(x => x.ToDomainEvent()).OfType<PortfolioFundSnapshotCaptured>()
            .Where(x => x.SourceRevision <= history.LastOrDefault()?.Revision)
            .OrderByDescending(x => x.SourceRevision).FirstOrDefault();
        snapshotTrace?.Stop();
        trace?.SetTag("portfolio.snapshot.used", snapshot is not null);
        trace?.SetTag("portfolio.snapshot.source_revision", snapshot?.SourceRevision ?? 0);
        using var replayTrace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.replay");
        if (snapshot is not null) aggregate.RestoreSnapshot(snapshot.State);
        trace?.SetTag("portfolio.events.replayed", history.Count(x => x.Revision > aggregate.Revision));
        aggregate.Replay(history.Where(x => x.Revision > aggregate.Revision));
        return aggregate;
    }

    public async Task SavePortfolioSnapshotAsync(PortfolioId portfolioId, PortfolioAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(portfolioId);
        ArgumentNullException.ThrowIfNull(aggregate);
        ValidateSnapshotAudit(nowUtc, principal);
        if (aggregate.Current?.PortfolioId != portfolioId.Id) throw new InvalidOperationException("Snapshot aggregate identity does not match the stream.");
        var stream = PortfolioSnapshotStream(portfolioId);
        var expected = await CurrentStreamVersionAsync(stream, cancellationToken).ConfigureAwait(false);
        var entity = portfolioId.Format();
        var captured = new PortfolioSnapshotCaptured(Guid.NewGuid(), Guid.NewGuid(), aggregate.Revision, aggregate.CaptureSnapshot(), nowUtc, principal)
        {
            Subject = new ActorSubject(ActorType.Event, "Portfolio", nameof(PortfolioSnapshotCaptured), entity),
            AggregateId = entity,
            EntityId = new ActorEntityId(entity)
        };
        await _eventSourceDb.SaveEventsAsync(stream, captured.CommandId, new DomainEventCollection([captured]), expected, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveFundSnapshotAsync(PortfolioFundId fundId, PortfolioFundAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundId);
        ArgumentNullException.ThrowIfNull(aggregate);
        ValidateSnapshotAudit(nowUtc, principal);
        if (aggregate.Current?.PortfolioId != fundId.PortfolioId || aggregate.Current.FundId != fundId.FundId) throw new InvalidOperationException("Snapshot aggregate identity does not match the stream.");
        var stream = FundSnapshotStream(fundId);
        var expected = await CurrentStreamVersionAsync(stream, cancellationToken).ConfigureAwait(false);
        var entity = fundId.Format();
        var captured = new PortfolioFundSnapshotCaptured(Guid.NewGuid(), Guid.NewGuid(), aggregate.Revision, aggregate.CaptureSnapshot(), nowUtc, principal)
        {
            Subject = new ActorSubject(ActorType.Event, "PortfolioFund", nameof(PortfolioFundSnapshotCaptured), entity),
            AggregateId = entity,
            EntityId = new ActorEntityId(entity)
        };
        await _eventSourceDb.SaveEventsAsync(stream, captured.CommandId, new DomainEventCollection([captured]), expected, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IPortfolioDomainEvent?> FindCommittedPortfolioCommandAsync(PortfolioId portfolioId, Guid commandId, CancellationToken cancellationToken = default)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("CommandId is required.", nameof(commandId));
        return ConvertRequired<IPortfolioDomainEvent>(await LoadAsync(PortfolioStream(portfolioId), cancellationToken).ConfigureAwait(false), PortfolioStream(portfolioId))
            .SingleOrDefault(x => x.CommandId == commandId);
    }

    public async Task<IPortfolioFundDomainEvent?> FindCommittedFundCommandAsync(PortfolioFundId fundId, Guid commandId, CancellationToken cancellationToken = default)
    {
        using var trace = PortfolioTelemetry.ActivitySource.StartActivity("portfolio.fund.find_committed_command");
        if (commandId == Guid.Empty) throw new ArgumentException("CommandId is required.", nameof(commandId));
        return ConvertRequired<IPortfolioFundDomainEvent>(await LoadAsync(FundStream(fundId), cancellationToken).ConfigureAwait(false), FundStream(fundId))
            .SingleOrDefault(x => x.CommandId == commandId);
    }

    public async Task<PortfolioCreatedEvent?> FindPortfolioCreateByIdempotencyKeyAsync(PortfolioId portfolioId, Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        return ConvertRequired<IPortfolioDomainEvent>(await LoadAsync(PortfolioStream(portfolioId), cancellationToken).ConfigureAwait(false), PortfolioStream(portfolioId))
            .OfType<PortfolioCreatedEvent>()
            .SingleOrDefault(x => x.IdempotencyKey == idempotencyKey);
    }

    public async Task<FundMandateCreatedEvent?> FindFundCreateByIdempotencyKeyAsync(PortfolioFundId fundId, Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        return ConvertRequired<IPortfolioFundDomainEvent>(await LoadAsync(FundStream(fundId), cancellationToken).ConfigureAwait(false), FundStream(fundId))
            .OfType<FundMandateCreatedEvent>()
            .SingleOrDefault(x => x.IdempotencyKey == idempotencyKey);
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

    static void ValidateHistory(IEnumerable<long> revisions)
    {
        var expected = 1L;
        foreach (var revision in revisions)
        {
            if (revision != expected++) throw new InvalidOperationException("Portfolio event history contains a revision gap.");
        }
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
