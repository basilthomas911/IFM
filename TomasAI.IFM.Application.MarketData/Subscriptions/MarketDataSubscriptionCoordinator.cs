using TomasAI.IFM.Application.MarketData.Contracts;
using System.Threading.Channels;

namespace TomasAI.IFM.Application.MarketData.Subscriptions;

/// <summary>Per-dataset host intent authority. Never performs provider/native/network I/O.</summary>
public sealed class MarketDataSubscriptionCoordinator : IAsyncDisposable
{
    readonly string scope;
    readonly string dataset;
    readonly DateOnly valueDate;
    readonly TickerLeasePolicy policy;
    readonly TimeProvider time;
    readonly Channel<Work> commands;
    readonly Dictionary<Guid, Entry> leases = [];
    readonly Dictionary<Guid, RememberedOperation> operations = [];
    readonly Task pump;
    readonly ITimer sweepTimer;
    DesiredSubscriptionManifest current;
    SubscriptionDatasetAvailability availability = SubscriptionDatasetAvailability.Recovering;
    DateTimeOffset admissionUtc;
    long admissionTimestamp;
    long revision;
    int sweepQueued;
    int disposed;

    public MarketDataSubscriptionCoordinator(string scope, string dataset, DateOnly valueDate,
        TickerLeasePolicy? policy = null, TimeProvider? timeProvider = null)
    {
        SubscriptionIdentity.Validate(scope, 128);
        SubscriptionIdentity.Validate(dataset, 64);
        if (valueDate == default) throw new ArgumentException("A value date is required.", nameof(valueDate));
        this.scope = scope;
        this.dataset = dataset;
        this.valueDate = valueDate;
        this.policy = (policy ?? new()).Validate();
        time = timeProvider ?? TimeProvider.System;
        HostEpochId = Guid.NewGuid();
        admissionUtc = time.GetUtcNow();
        admissionTimestamp = time.GetTimestamp();
        current = new(HostEpochId, scope, dataset, valueDate, 0, []);
        commands = Channel.CreateBounded<Work>(new BoundedChannelOptions(this.policy.CommandCapacity)
        {
            SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        pump = Task.Run(RunAsync);
        sweepTimer = time.CreateTimer(_ => QueueSweep(), null, this.policy.SweepInterval, this.policy.SweepInterval);
    }

    public Guid HostEpochId { get; }
    public DesiredSubscriptionManifest Current => Volatile.Read(ref current);

    public Task<SubscriptionLeaseResult> AcquireAsync(SubscriptionAcquireRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Submit(new(Kind.Acquire, request, request.OperationId, request.CorrelationId, request.Owner,
            request.DeadlineUtc, cancellationToken));
    }
    public Task<SubscriptionLeaseResult> RenewAsync(SubscriptionRenewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Submit(new(Kind.Renew, request, request.OperationId, request.CorrelationId, request.Owner,
            request.DeadlineUtc, cancellationToken));
    }
    public Task<SubscriptionLeaseResult> AcquireBatchAsync(SubscriptionAcquireBatchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Submit(new(Kind.AcquireBatch, request, request.OperationId, request.CorrelationId, request.Owner,
            request.DeadlineUtc, cancellationToken));
    }

    public Task<SubscriptionLeaseResult> QueryAsync(SubscriptionOwnerQuery request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Submit(new(Kind.Query, request, owner: request.Owner,
            deadlineUtc: time.GetUtcNow() + policy.CommandTimeout, cancellation: cancellationToken));
    }
    public Task<SubscriptionLeaseResult> ReleaseAsync(SubscriptionReleaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Submit(new(Kind.Release, request, request.OperationId, request.CorrelationId, request.Owner,
            request.DeadlineUtc, cancellationToken));
    }

    // Internal integration controls, not a second public feed/reset API. A full queue returns false:
    // the supervisor must never assume an unacknowledged availability transition was applied.
    public Task<bool> SetAvailabilityAsync(SubscriptionDatasetAvailability next)
    {
        if (!Enum.IsDefined(next)) throw new ArgumentOutOfRangeException(nameof(next));
        return SubmitControl(new(Kind.Availability, next));
    }
    public Task<bool> SweepAsync() => SubmitControl(new(Kind.Sweep, null));

    Task<SubscriptionLeaseResult> Submit(Work work)
    {
        if (work.Cancellation.IsCancellationRequested || Volatile.Read(ref disposed) != 0)
            return Task.FromResult(Result(work, SubscriptionResultCode.Cancelled));
        if (!commands.Writer.TryWrite(work))
            return Task.FromResult(Result(work, SubscriptionResultCode.CapacityExceeded, reason: "The bounded command queue is full."));
        return work.Completion.Task;
    }

    async Task<bool> SubmitControl(Work work)
    {
        if (Volatile.Read(ref disposed) != 0 || !commands.Writer.TryWrite(work)) return false;
        return (await work.Completion.Task.ConfigureAwait(false)).Code == SubscriptionResultCode.DesiredAccepted;
    }

    void QueueSweep()
    {
        if (Volatile.Read(ref disposed) != 0 || Interlocked.CompareExchange(ref sweepQueued, 1, 0) != 0) return;
        if (!commands.Writer.TryWrite(new(Kind.TimerSweep, null))) Volatile.Write(ref sweepQueued, 0);
    }

    async Task RunAsync()
    {
        await foreach (var work in commands.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                if (work.Kind == Kind.TimerSweep) Volatile.Write(ref sweepQueued, 0);
                if (Volatile.Read(ref disposed) != 0 || work.Cancellation.IsCancellationRequested)
                {
                    work.Completion.TrySetResult(Result(work, SubscriptionResultCode.Cancelled));
                    continue;
                }
                SweepExpired();
                if (work.Kind == Kind.Query)
                {
                    if (BeforeCommit(work) is { } expiredQuery)
                    {
                        work.Completion.TrySetResult(expiredQuery);
                        continue;
                    }
                    var query = (SubscriptionOwnerQuery)work.Payload!;
                    var valid = query.Owner is not null && query.Owner.Scope == scope
                        && query.PageSize is >= 1 and <= 128 && query.Offset >= 0 && query.Offset <= policy.MaximumLeases;
                    work.Completion.TrySetResult(valid
                        ? Result(work, SubscriptionResultCode.DesiredAccepted, reason: "Current desired intent only; no route/pricing readiness is implied.") with
                        {
                            SelectedLeases = Array.AsReadOnly(Current.Leases.Where(lease => lease.Owner == query.Owner)
                                .Skip(query.Offset).Take(query.PageSize).ToArray())
                        }
                        : Result(work, SubscriptionResultCode.OwnershipUnverified));
                    continue;
                }
                if (work.Kind is Kind.Sweep or Kind.TimerSweep or Kind.Availability)
                {
                    if (work.Kind == Kind.Availability) availability = (SubscriptionDatasetAvailability)work.Payload!;
                    work.Completion.TrySetResult(Result(work, SubscriptionResultCode.DesiredAccepted));
                    continue;
                }
                work.Completion.TrySetResult(Execute(work));
            }
            catch (Exception error)
            {
                // No fire-and-forget exception and no abandoned caller if a programming error is found.
                // Do not convert an unexpected exception into a successful ownership transition.
                work.Completion.TrySetException(error);
            }
        }
    }

    SubscriptionLeaseResult Execute(Work work)
    {
        if (work.OperationId == Guid.Empty || work.CorrelationId == Guid.Empty || work.Owner is null
            || work.Owner.Scope != scope)
            return Result(work, SubscriptionResultCode.OwnershipUnverified, reason: "A scoped authorized owner and operation identity are required.");
        if (operations.TryGetValue(work.OperationId, out var remembered))
            return remembered.Kind == work.Kind && Equals(remembered.Payload, work.Payload)
                ? remembered.Result : Result(work, SubscriptionResultCode.Conflict, reason: "Operation ID was reused with different content.");
        var now = AdmissionUtc();
        if (!TryOperationWindow(work.OperationId, out var issuedAt, out var validUntil))
            return Result(work, SubscriptionResultCode.Conflict, reason: "Ephemeral operation IDs must be UUIDv7 identities.");
        if (validUntil <= now) return Result(work, SubscriptionResultCode.Timeout);
        if (issuedAt > now || work.DeadlineUtc > validUntil)
            return Result(work, SubscriptionResultCode.Conflict, reason: "Operation issue time/deadline is outside its immutable retry window.");
        if (work.DeadlineUtc <= now) return Result(work, SubscriptionResultCode.Timeout);
        if (work.DeadlineUtc - now > policy.CommandTimeout)
            return Result(work, SubscriptionResultCode.Conflict, reason: "The command deadline exceeds the configured bound.");
        if (operations.Count >= policy.MaximumRememberedOperations)
            return Result(work, SubscriptionResultCode.CapacityExceeded, reason: "The bounded operation-result window is full; retry after its deadline.");

        var result = work.Kind switch
        {
            Kind.Acquire => Acquire(work, (SubscriptionAcquireRequest)work.Payload!),
            Kind.AcquireBatch => AcquireBatch(work, (SubscriptionAcquireBatchRequest)work.Payload!),
            Kind.Renew => Renew(work, (SubscriptionRenewRequest)work.Payload!),
            Kind.Release => Release(work, (SubscriptionReleaseRequest)work.Payload!),
            _ => throw new InvalidOperationException("Unknown ownership command.")
        };
        // UUIDv7 embeds an immutable issue time. After pruning, even an edited deadline cannot
        // resurrect that ID. Durable 30-day result storage is a separate, not-yet-installed boundary.
        operations.Add(work.OperationId, new(work.Kind, work.Payload!, result, validUntil));
        return result;
    }

    SubscriptionLeaseResult Acquire(Work work, SubscriptionAcquireRequest request)
        => AcquireMany(work, request.HostEpochId, [new(request.Owner, request.Target)], request.Purpose);

    SubscriptionLeaseResult AcquireBatch(Work work, SubscriptionAcquireBatchRequest request)
        => AcquireMany(work, request.HostEpochId, request.Selections, request.Purpose);

    SubscriptionLeaseResult AcquireMany(Work work, Guid hostEpochId,
        IReadOnlyList<SubscriptionLeaseSelection> selections, SubscriptionLeasePurpose purpose)
    {
        if (hostEpochId != HostEpochId)
            return Result(work, SubscriptionResultCode.NotOwned, reason: "Reacquire with the current host epoch.");
        if (!Enum.IsDefined(purpose) || selections.Any(selection => selection.Owner is null
                || !SameWorkflow(selection.Owner, work.Owner!) || selection.Target is null
                || selection.Target.Dataset != dataset
                || selection.Target.Chain is { } chain && chain.ValueDate != valueDate))
            return Result(work, SubscriptionResultCode.InvalidContract);

        var selected = new List<SubscriptionLeaseView>(selections.Count);
        var additions = new List<SubscriptionLeaseView>(selections.Count);
        foreach (var selection in selections)
        {
            var existing = leases.Values.FirstOrDefault(entry => entry.View.Owner == selection.Owner
                && entry.View.Target == selection.Target && entry.View.Purpose == purpose);
            if (existing is not null) selected.Add(existing.View);
            else
            {
                var next = new SubscriptionLeaseView(new(HostEpochId, Guid.NewGuid(), 1), selection.Owner,
                    selection.Target, purpose, time.GetUtcNow() + policy.EphemeralTimeToLive);
                selected.Add(next);
                additions.Add(next);
            }
        }
        if (additions.Count == 0) return SelectionResult(work, SubscriptionResultCode.AlreadyOwned, selected);
        if (availability != SubscriptionDatasetAvailability.Open)
            return Result(work, availability == SubscriptionDatasetAvailability.Closed
                ? SubscriptionResultCode.Closed : SubscriptionResultCode.Recovering);
        if (purpose is SubscriptionLeasePurpose.Strategy or SubscriptionLeasePurpose.WorkingOrder or SubscriptionLeasePurpose.Position)
            return Result(work, SubscriptionResultCode.PersistenceUnavailable, reason: "No transactional durable-intent store is installed.");

        // Reject simple limits before copying/hashing all accepted intent.
        if (leases.Count + additions.Count > policy.MaximumLeases || leases.Values.Count(entry =>
                SameWorkflow(entry.View.Owner, work.Owner!)) + additions.Count > policy.MaximumLeasesPerOwner)
            return Result(work, SubscriptionResultCode.CapacityExceeded);
        var allChains = leases.Values.Select(entry => entry.View.Target.Chain)
            .Concat(additions.Select(lease => lease.Target.Chain)).OfType<SubscriptionChainKey>().Distinct().ToArray();
        if (allChains.GroupBy(chain => (chain.Underlying, chain.ValueDate, chain.MaturityDate)).Any(group => group.Count() > 1))
            return Result(work, SubscriptionResultCode.Conflict, reason: "An exact chain universe conflicts with current or batched intent.");
        var candidateManifest = new DesiredSubscriptionManifest(HostEpochId, scope, dataset, valueDate,
            checked(revision + 1), leases.Values.Select(entry => entry.View).Concat(additions));
        if (candidateManifest.Leases.Count > policy.MaximumLeases
            || candidateManifest.Leases.Count(lease => SameWorkflow(lease.Owner, work.Owner!)) > policy.MaximumLeasesPerOwner
            || candidateManifest.Leases.Where(lease => lease.Target.Chain is not null).Select(lease => lease.Target.Chain).Distinct().Count() > policy.MaximumChains
            || candidateManifest.Routes.Count(route => route.Ticker.AssetKind == SubscriptionAssetKind.FuturesOption) > policy.MaximumOptions
            || candidateManifest.Routes.Count(route => route.Ticker.AssetKind == SubscriptionAssetKind.Futures) > policy.MaximumFutures)
            return Result(work, SubscriptionResultCode.CapacityExceeded);
        var timestamp = time.GetTimestamp();
        if (BeforeCommit(work) is { } refused) return refused;
        foreach (var next in additions) leases.Add(next.Token.LeaseId, new(next, timestamp));
        revision = candidateManifest.Revision;
        Volatile.Write(ref current, candidateManifest);
        return SelectionResult(work, SubscriptionResultCode.DesiredAccepted, selected);
    }

    static bool SameWorkflow(SubscriptionOwnerKey first, SubscriptionOwnerKey second) => first.Scope == second.Scope
        && first.Owner.WorkflowType == second.Owner.WorkflowType && first.Owner.WorkflowId == second.Owner.WorkflowId;

    SubscriptionLeaseResult SelectionResult(Work work, SubscriptionResultCode code, List<SubscriptionLeaseView> selected)
        => Result(work, code, selected.Count == 1 ? selected[0] : null) with { SelectedLeases = Array.AsReadOnly(selected.ToArray()) };

    SubscriptionLeaseResult Renew(Work work, SubscriptionRenewRequest request)
    {
        var entry = Find(request.Owner, request.Lease);
        if (entry is null) return Result(work, SubscriptionResultCode.Expired);
        if (entry.View.Token.Version != request.Lease.Version) return Result(work, SubscriptionResultCode.Conflict);
        if (entry.View.IsDurable) return Result(work, SubscriptionResultCode.Conflict, reason: "Durable ownership has no renewable TTL.");
        var next = entry.View with
        {
            Token = entry.View.Token with { Version = checked(entry.View.Token.Version + 1) },
            ExpiresAtUtc = time.GetUtcNow() + policy.EphemeralTimeToLive
        };
        var prepared = new DesiredSubscriptionManifest(HostEpochId, scope, dataset, valueDate,
            checked(revision + 1), leases.Values.Select(value => value.View.Token.LeaseId == next.Token.LeaseId ? next : value.View));
        if (BeforeCommit(work) is { } refused) return refused;
        var timestamp = time.GetTimestamp();
        if (time.GetElapsedTime(entry.RenewedAtTimestamp, timestamp) >= policy.EphemeralTimeToLive)
        {
            SweepExpired();
            return Result(work, SubscriptionResultCode.Expired);
        }
        leases[next.Token.LeaseId] = new(next, timestamp);
        revision = prepared.Revision;
        Volatile.Write(ref current, prepared);
        return Result(work, SubscriptionResultCode.DesiredAccepted, next);
    }

    SubscriptionLeaseResult Release(Work work, SubscriptionReleaseRequest request)
    {
        var entry = Find(request.Owner, request.Lease);
        if (entry is null) return Result(work, SubscriptionResultCode.NotOwned);
        if (entry.View.Token.Version != request.Lease.Version) return Result(work, SubscriptionResultCode.Conflict);
        if (entry.View.IsDurable) return Result(work, SubscriptionResultCode.PersistenceUnavailable);
        var prepared = new DesiredSubscriptionManifest(HostEpochId, scope, dataset, valueDate,
            checked(revision + 1), leases.Values.Where(value => value.View.Token.LeaseId != request.Lease.LeaseId).Select(value => value.View));
        if (BeforeCommit(work) is { } refused) return refused;
        leases.Remove(request.Lease.LeaseId);
        revision = prepared.Revision;
        Volatile.Write(ref current, prepared);
        return Result(work, SubscriptionResultCode.Released);
    }

    Entry? Find(SubscriptionOwnerKey owner, SubscriptionLeaseToken token) => token.HostEpochId == HostEpochId
        && leases.TryGetValue(token.LeaseId, out var entry) && entry.View.Owner == owner ? entry : null;

    SubscriptionLeaseResult? BeforeCommit(Work work)
    {
        if (work.Cancellation.IsCancellationRequested || Volatile.Read(ref disposed) != 0)
            return Result(work, SubscriptionResultCode.Cancelled);
        return work.DeadlineUtc <= AdmissionUtc() ? Result(work, SubscriptionResultCode.Timeout) : null;
    }

    void SweepExpired()
    {
        var timestamp = time.GetTimestamp();
        var expired = leases.Where(pair => !pair.Value.View.IsDurable
            && time.GetElapsedTime(pair.Value.RenewedAtTimestamp, timestamp) >= policy.EphemeralTimeToLive)
            .Select(pair => pair.Key).ToArray();
        foreach (var lease in expired) leases.Remove(lease);
        if (expired.Length != 0) Publish();
        var now = AdmissionUtc();
        foreach (var id in operations.Where(pair => pair.Value.DeadlineUtc <= now).Select(pair => pair.Key).ToArray())
            operations.Remove(id);
    }

    DateTimeOffset AdmissionUtc()
    {
        var timestamp = time.GetTimestamp();
        var monotonicUtc = admissionUtc + time.GetElapsedTime(admissionTimestamp, timestamp);
        var observedUtc = time.GetUtcNow();
        admissionUtc = observedUtc > monotonicUtc ? observedUtc : monotonicUtc;
        admissionTimestamp = timestamp;
        return admissionUtc;
    }

    bool TryOperationWindow(Guid id, out DateTimeOffset issuedAt, out DateTimeOffset validUntil)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes, bigEndian: true, out _);
        issuedAt = validUntil = default;
        if (bytes[6] >> 4 != 7 || bytes[8] >> 6 != 2) return false;
        long milliseconds = 0;
        for (var i = 0; i < 6; i++) milliseconds = (milliseconds << 8) | bytes[i];
        try
        {
            issuedAt = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            // UUID timestamps have millisecond precision; permit only the discarded submillisecond.
            validUntil = issuedAt + policy.CommandTimeout + TimeSpan.FromMilliseconds(1);
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    void Publish() => Volatile.Write(ref current, new(HostEpochId, scope, dataset, valueDate,
        checked(++revision), leases.Values.Select(entry => entry.View)));

    SubscriptionLeaseResult Result(Work work, SubscriptionResultCode code,
        SubscriptionLeaseView? lease = null, string? reason = null)
        => new(work.OperationId, code, lease, Current.Revision, 0, reason);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            await sweepTimer.DisposeAsync().ConfigureAwait(false);
            commands.Writer.TryComplete();
        }
        await pump.ConfigureAwait(false);
    }

    enum Kind { Acquire, AcquireBatch, Renew, Release, Availability, Sweep, TimerSweep, Query }
    sealed record Entry(SubscriptionLeaseView View, long RenewedAtTimestamp);
    sealed record RememberedOperation(Kind Kind, object Payload, SubscriptionLeaseResult Result, DateTimeOffset DeadlineUtc);
    sealed class Work(Kind kind, object? payload, Guid operationId = default, Guid correlationId = default,
        SubscriptionOwnerKey? owner = null, DateTimeOffset deadlineUtc = default, CancellationToken cancellation = default)
    {
        public Kind Kind { get; } = kind;
        public object? Payload { get; } = payload;
        public Guid OperationId { get; } = operationId;
        public Guid CorrelationId { get; } = correlationId;
        public SubscriptionOwnerKey? Owner { get; } = owner;
        public DateTimeOffset DeadlineUtc { get; } = deadlineUtc;
        public CancellationToken Cancellation { get; } = cancellation;
        public TaskCompletionSource<SubscriptionLeaseResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
