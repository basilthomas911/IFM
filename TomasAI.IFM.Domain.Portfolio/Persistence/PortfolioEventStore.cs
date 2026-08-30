using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Persistence;

public interface IPortfolioEventStore
{
    Task AppendPortfolioAsync(PortfolioId portfolioId, PortfolioDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default);
    Task AppendFundAsync(PortfolioFundId fundId, PortfolioFundDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default);
    Task<PortfolioAggregate> LoadPortfolioAsync(PortfolioId portfolioId, CancellationToken cancellationToken = default);
    Task<PortfolioFundAggregate> LoadFundAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default);
    Task SavePortfolioSnapshotAsync(PortfolioId portfolioId, PortfolioAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default);
    Task SaveFundSnapshotAsync(PortfolioFundId fundId, PortfolioFundAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default);
    Task<PortfolioDomainEvent?> FindCommittedPortfolioCommandAsync(PortfolioId portfolioId, Guid commandId, CancellationToken cancellationToken = default);
    Task<PortfolioFundDomainEvent?> FindCommittedFundCommandAsync(PortfolioFundId fundId, Guid commandId, CancellationToken cancellationToken = default);
    Task<PortfolioCreated?> FindPortfolioCreateByIdempotencyKeyAsync(PortfolioId portfolioId, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<FundMandateCreated?> FindFundCreateByIdempotencyKeyAsync(PortfolioFundId fundId, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortfolioDomainEvent>> LoadPortfolioHistoryAsync(PortfolioId portfolioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortfolioFundDomainEvent>> LoadFundHistoryAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default);
}

/// <summary>Persists Portfolio command history only in the shared PostgreSQL EventSourceDb.</summary>
public sealed class PortfolioEventStore(IEventSourceActorDbContext eventSourceDb) : IPortfolioEventStore
{
    readonly IEventSourceActorDbContext _eventSourceDb = eventSourceDb ?? throw new ArgumentNullException(nameof(eventSourceDb));

    public static string PortfolioStream(PortfolioId id)
    {
        ThrowIfInvalid(id.Validate(), nameof(id));
        return $"Portfolio.{id.Id}";
    }

    public static string FundStream(PortfolioFundId id)
    {
        ThrowIfInvalid(id.Validate(), nameof(id));
        return $"PortfolioFund.{id.PortfolioId}.{id.FundId}";
    }

    public static string PortfolioSnapshotStream(PortfolioId id) => $"{PortfolioStream(id)}.Snapshot";
    public static string FundSnapshotStream(PortfolioFundId id) => $"{FundStream(id)}.Snapshot";

    public async Task AppendPortfolioAsync(
        PortfolioId portfolioId,
        PortfolioDomainEvent domainEvent,
        long expectedRevision,
        PortfolioEventMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ValidateAppend(domainEvent.Revision, expectedRevision);
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
        await _eventSourceDb.SaveEventsAsync(
            PortfolioStream(portfolioId), domainEvent.CommandId, new DomainEventCollection([domainEvent]),
            expectedRevision,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendFundAsync(
        PortfolioFundId fundId,
        PortfolioFundDomainEvent domainEvent,
        long expectedRevision,
        PortfolioEventMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ValidateAppend(domainEvent.Revision, expectedRevision);
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
        await _eventSourceDb.SaveEventsAsync(
            FundStream(fundId), domainEvent.CommandId, new DomainEventCollection([domainEvent]),
            expectedRevision,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<PortfolioAggregate> LoadPortfolioAsync(PortfolioId portfolioId, CancellationToken cancellationToken = default)
    {
        var events = await LoadAsync(PortfolioStream(portfolioId), cancellationToken).ConfigureAwait(false);
        var history = ConvertRequired<PortfolioDomainEvent>(events, PortfolioStream(portfolioId)).OrderBy(x => x.Revision).ToArray();
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

    public async Task<IReadOnlyList<PortfolioDomainEvent>> LoadPortfolioHistoryAsync(PortfolioId portfolioId, CancellationToken cancellationToken = default) =>
        ConvertRequired<PortfolioDomainEvent>(await LoadAsync(PortfolioStream(portfolioId), cancellationToken).ConfigureAwait(false), PortfolioStream(portfolioId))
            .OrderBy(x => x.Revision).ToArray();

    public async Task<IReadOnlyList<PortfolioFundDomainEvent>> LoadFundHistoryAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default) =>
        ConvertRequired<PortfolioFundDomainEvent>(await LoadAsync(FundStream(fundId), cancellationToken).ConfigureAwait(false), FundStream(fundId))
            .OrderBy(x => x.Revision).ToArray();

    public async Task<PortfolioFundAggregate> LoadFundAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default)
    {
        var events = await LoadAsync(FundStream(fundId), cancellationToken).ConfigureAwait(false);
        var history = ConvertRequired<PortfolioFundDomainEvent>(events, FundStream(fundId)).OrderBy(x => x.Revision).ToArray();
        ValidateHistory(history.Select(x => x.Revision));
        var aggregate = new PortfolioFundAggregate();
        var snapshot = (await LoadAsync(FundSnapshotStream(fundId), cancellationToken).ConfigureAwait(false))
            .Select(x => x.ToDomainEvent()).OfType<PortfolioFundSnapshotCaptured>()
            .Where(x => x.SourceRevision <= history.LastOrDefault()?.Revision)
            .OrderByDescending(x => x.SourceRevision).FirstOrDefault();
        if (snapshot is not null) aggregate.RestoreSnapshot(snapshot.State);
        aggregate.Replay(history.Where(x => x.Revision > aggregate.Revision));
        return aggregate;
    }

    public async Task SavePortfolioSnapshotAsync(PortfolioId portfolioId, PortfolioAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ValidateSnapshotAudit(nowUtc, principal);
        if (aggregate.Current?.PortfolioId != portfolioId.Id) throw new InvalidOperationException("Snapshot aggregate identity does not match the stream.");
        var stream = PortfolioSnapshotStream(portfolioId);
        var expected = await CurrentStreamVersionAsync(stream, cancellationToken).ConfigureAwait(false);
        var entity = portfolioId.Format();
        var captured = new PortfolioSnapshotCaptured(Guid.NewGuid(), Guid.NewGuid(), aggregate.Revision, aggregate.CaptureSnapshot(), nowUtc, principal)
        {
            Subject = new ActorSubject(ActorType.Event, "Portfolio", nameof(PortfolioSnapshotCaptured), entity), AggregateId = entity, EntityId = new ActorEntityId(entity)
        };
        await _eventSourceDb.SaveEventsAsync(stream, captured.CommandId, new DomainEventCollection([captured]), expected, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveFundSnapshotAsync(PortfolioFundId fundId, PortfolioFundAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ValidateSnapshotAudit(nowUtc, principal);
        if (aggregate.Current?.PortfolioId != fundId.PortfolioId || aggregate.Current.FundId != fundId.FundId) throw new InvalidOperationException("Snapshot aggregate identity does not match the stream.");
        var stream = FundSnapshotStream(fundId);
        var expected = await CurrentStreamVersionAsync(stream, cancellationToken).ConfigureAwait(false);
        var entity = fundId.Format();
        var captured = new PortfolioFundSnapshotCaptured(Guid.NewGuid(), Guid.NewGuid(), aggregate.Revision, aggregate.CaptureSnapshot(), nowUtc, principal)
        {
            Subject = new ActorSubject(ActorType.Event, "PortfolioFund", nameof(PortfolioFundSnapshotCaptured), entity), AggregateId = entity, EntityId = new ActorEntityId(entity)
        };
        await _eventSourceDb.SaveEventsAsync(stream, captured.CommandId, new DomainEventCollection([captured]), expected, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PortfolioDomainEvent?> FindCommittedPortfolioCommandAsync(PortfolioId portfolioId, Guid commandId, CancellationToken cancellationToken = default)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("CommandId is required.", nameof(commandId));
        return ConvertRequired<PortfolioDomainEvent>(await LoadAsync(PortfolioStream(portfolioId), cancellationToken).ConfigureAwait(false), PortfolioStream(portfolioId))
            .SingleOrDefault(x => x.CommandId == commandId);
    }

    public async Task<PortfolioFundDomainEvent?> FindCommittedFundCommandAsync(PortfolioFundId fundId, Guid commandId, CancellationToken cancellationToken = default)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("CommandId is required.", nameof(commandId));
        return ConvertRequired<PortfolioFundDomainEvent>(await LoadAsync(FundStream(fundId), cancellationToken).ConfigureAwait(false), FundStream(fundId))
            .SingleOrDefault(x => x.CommandId == commandId);
    }

    public async Task<PortfolioCreated?> FindPortfolioCreateByIdempotencyKeyAsync(PortfolioId portfolioId, Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        return ConvertRequired<PortfolioDomainEvent>(await LoadAsync(PortfolioStream(portfolioId), cancellationToken).ConfigureAwait(false), PortfolioStream(portfolioId))
            .OfType<PortfolioCreated>()
            .SingleOrDefault(x => x.IdempotencyKey == idempotencyKey);
    }

    public async Task<FundMandateCreated?> FindFundCreateByIdempotencyKeyAsync(PortfolioFundId fundId, Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        return ConvertRequired<PortfolioFundDomainEvent>(await LoadAsync(FundStream(fundId), cancellationToken).ConfigureAwait(false), FundStream(fundId))
            .OfType<FundMandateCreated>()
            .SingleOrDefault(x => x.IdempotencyKey == idempotencyKey);
    }

    async Task<ICollection<TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel>> LoadAsync(
        string stream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var streamId = await _eventSourceDb.GetEventStreamIdFromDbAsync(stream).ConfigureAwait(false);
        if (streamId is null) return [];
        var events = await _eventSourceDb.LoadActorEventStreamAsync<PortfolioEventStoreState>(streamId.EventStreamId).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return events;
    }

    static void ValidateAppend(long eventRevision, long expectedRevision)
    {
        if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
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
        var converted = new List<TEvent>();
        foreach (var row in rows.OrderBy(x => x.StreamVersion))
        {
            if (row.ToDomainEvent() is not TEvent domainEvent)
                throw new InvalidOperationException($"Event stream '{stream}' contains an unknown or incompatible event contract at stream version {row.StreamVersion}.");
            converted.Add(domainEvent);
        }
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
