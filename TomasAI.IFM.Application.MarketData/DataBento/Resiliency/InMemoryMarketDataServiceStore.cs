using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Deterministic store for tests and non-persistent composition; production uses PostgreSQL.</summary>
public sealed class InMemoryMarketDataServiceStore : IMarketDataServiceStore
{
    readonly System.Threading.Lock _sync = new();
    readonly Dictionary<DatabentoContractRole, FuturesRolloverContractAssignment> _assignments = [];
    readonly List<DatabentoWatchdogObservation> _observations = [];
    readonly Dictionary<string, DatasetIncidentTransition> _incidents = new(StringComparer.Ordinal);
    long _nextObservationId;

    public Task<FuturesRolloverContractAssignment?> GetAssignmentAsync(DatabentoContractRole role,
        CancellationToken cancellationToken = default)
    { lock (_sync) return Task.FromResult(_assignments.GetValueOrDefault(role)); }

    public Task<IReadOnlyList<FuturesRolloverContractAssignment>> ListAssignmentsAsync(CancellationToken cancellationToken = default)
    { lock (_sync) return Task.FromResult<IReadOnlyList<FuturesRolloverContractAssignment>>([.. _assignments.Values.OrderBy(x => x.ContractRole)]); }

    public Task<FuturesRolloverContractAssignment> UpsertAssignmentAsync(FuturesRolloverContractAssignment assignment,
        long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        Validate(assignment);
        lock (_sync)
        {
            var current = _assignments.GetValueOrDefault(assignment.ContractRole);
            if ((current?.RowVersion ?? 0) != expectedRowVersion)
                throw new InvalidOperationException("The current-contract assignment row version changed.");
            var saved = assignment with { RowVersion = checked(expectedRowVersion + 1) };
            _assignments[assignment.ContractRole] = saved;
            ValidateSet(_assignments.Values);
            return Task.FromResult(saved);
        }
    }

    public Task DeleteAssignmentAsync(DatabentoContractRole role, long expectedRowVersion, string deletedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedBy);
        lock (_sync)
        {
            if (!_assignments.TryGetValue(role, out var current) || current.RowVersion != expectedRowVersion)
                throw new InvalidOperationException("The current-contract assignment row version changed.");
            _assignments.Remove(role);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReplaceVxAssignmentsAsync(
        FuturesRolloverContractAssignment front, FuturesRolloverContractAssignment second,
        long expectedFrontVersion, long expectedSecondVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(front);
        ArgumentNullException.ThrowIfNull(second);
        if (front.ContractRole != DatabentoContractRole.VxFrontMonth || second.ContractRole != DatabentoContractRole.VxSecondMonth)
            throw new ArgumentException("The coupled replacement requires front and second VX roles.",nameof(front));
        Validate(front); Validate(second);
        lock (_sync)
        {
            var currentFront = _assignments.GetValueOrDefault(DatabentoContractRole.VxFrontMonth);
            var currentSecond = _assignments.GetValueOrDefault(DatabentoContractRole.VxSecondMonth);
            if ((currentFront?.RowVersion ?? 0) != expectedFrontVersion || (currentSecond?.RowVersion ?? 0) != expectedSecondVersion)
                throw new InvalidOperationException("A VX assignment row version changed.");
            var savedFront = front with { RowVersion = checked(expectedFrontVersion + 1) };
            var savedSecond = second with { RowVersion = checked(expectedSecondVersion + 1) };
            ValidateSet(_assignments.Values.Where(x => x.ContractRole is not (DatabentoContractRole.VxFrontMonth or DatabentoContractRole.VxSecondMonth))
                .Append(savedFront).Append(savedSecond));
            _assignments[front.ContractRole] = savedFront; _assignments[second.ContractRole] = savedSecond;
            return Task.FromResult<IReadOnlyList<FuturesRolloverContractAssignment>>([savedFront, savedSecond]);
        }
    }

    public Task<DatabentoWatchdogObservation> AppendObservationAsync(DatabentoWatchdogObservation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        lock (_sync)
        {
            if (_observations.Any(x => x.ObservationId == observation.ObservationId))
                return Task.FromResult(_observations.Single(x => x.ObservationId == observation.ObservationId));
            var saved = observation with { WatchdogStatusLogId = checked(++_nextObservationId), RowVersion = 1 };
            _observations.Add(saved); return Task.FromResult(saved);
        }
    }

    public Task<DatabentoWatchdogObservation?> GetObservationAsync(long id, CancellationToken cancellationToken = default)
    { lock (_sync) return Task.FromResult(_observations.SingleOrDefault(x => x.WatchdogStatusLogId == id)); }

    public Task<DatabentoWatchdogObservation> UpdateObservationAsync(DatabentoWatchdogObservation observation,
        long expectedRowVersion, string changedBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);
        lock (_sync)
        {
            var index = _observations.FindIndex(x => x.WatchdogStatusLogId == observation.WatchdogStatusLogId);
            if (index < 0 || _observations[index].RowVersion != expectedRowVersion)
                throw new InvalidOperationException("The watchdog observation row version changed.");
            var saved = observation with { RowVersion = checked(expectedRowVersion + 1) };
            _observations[index] = saved;
            return Task.FromResult(saved);
        }
    }

    public Task<IReadOnlyList<DatabentoWatchdogObservation>> ListObservationsAsync(DateOnly? valueDate = null,
        DatabentoMajorStatus? status = null, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize));
        lock (_sync) return Task.FromResult<IReadOnlyList<DatabentoWatchdogObservation>>([.. _observations
            .Where(x => valueDate is null || x.ValueDate == valueDate)
            .Where(x => status is null || x.MajorStatus == status)
            .OrderByDescending(x => x.ObservedOnUtc).Take(pageSize)]);
    }

    public Task DeleteObservationAsync(long id, long expectedRowVersion, string deletedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedBy);
        lock (_sync)
        {
            var index = _observations.FindIndex(x => x.WatchdogStatusLogId == id);
            if (index < 0 || _observations[index].RowVersion != expectedRowVersion)
                throw new InvalidOperationException("The watchdog observation row version changed.");
            _observations.RemoveAt(index);
        }
        return Task.CompletedTask;
    }

    public Task<DatasetIncidentTransition> PersistDatasetIncidentAsync(
        DatasetIncidentTransition transition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transition);
        lock (_sync)
        {
            var current = _incidents.GetValueOrDefault(transition.Snapshot.Dataset);
            if (current?.TransitionId == transition.TransitionId)
                return Task.FromResult(current);
            var saved = transition with { RowVersion = checked((current?.RowVersion ?? 0) + 1) };
            _incidents[transition.Snapshot.Dataset] = saved;
            return Task.FromResult(saved);
        }
    }

    public Task<IReadOnlyList<DatasetIncidentTransition>> ListOpenDatasetIncidentsAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
            return Task.FromResult<IReadOnlyList<DatasetIncidentTransition>>(
                [.. _incidents.Values.Where(value => value.Snapshot.IsOpen)]);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Name the public request owning the invalid nested property.")]
    static void Validate(FuturesRolloverContractAssignment value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.ContractId, nameof(value));
        ArgumentException.ThrowIfNullOrWhiteSpace(value.UpdatedBy, nameof(value));
        var expected = value.ContractRole == DatabentoContractRole.EsQuarterly ? "ES" : "VX";
        if (!string.Equals(value.RootSymbol, expected, StringComparison.Ordinal))
            throw new ArgumentException($"Role {value.ContractRole} requires root {expected}.",nameof(value));
        if (value.LastTradeDate == default || value.RowVersion < 0) throw new ArgumentException("Assignment metadata is invalid.",nameof(value));
    }

    static void ValidateSet(IEnumerable<FuturesRolloverContractAssignment> values)
    {
        var set = values.ToArray();
        if (set.Select(x => x.ContractId).Distinct(StringComparer.Ordinal).Count() != set.Length)
            throw new InvalidOperationException("Active contract IDs must be unique.");
        var front = set.SingleOrDefault(x => x.ContractRole == DatabentoContractRole.VxFrontMonth);
        var second = set.SingleOrDefault(x => x.ContractRole == DatabentoContractRole.VxSecondMonth);
        if (front is not null && second is not null && second.LastTradeDate <= front.LastTradeDate)
            throw new InvalidOperationException("The second VX contract must mature after the front contract.");
    }
}
