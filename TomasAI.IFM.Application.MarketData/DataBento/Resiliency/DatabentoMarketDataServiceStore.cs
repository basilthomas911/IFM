using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Owns watchdog duplicate handling and identifier allocation over typed persistence.</summary>
public sealed class DatabentoMarketDataServiceStore(IMarketDataServiceStateStore state,
    IWatchdogObservationPersistence observations, ISequenceIdDbContext ids) : IMarketDataServiceStore
{
    /// <inheritdoc/>
    public async Task<DatabentoWatchdogObservation> AppendObservationAsync(DatabentoWatchdogObservation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var existing = await observations.ReadWatchdogObservationAsync(observation.ObservationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null) return existing;
        var id = await ids.GetNextSequenceIdAsync(SequenceName.MarketDataService_WatchdogStatusLogId, cancellationToken)
            .ConfigureAwait(false);
        await observations.InsertWatchdogObservationAsync(observation with { WatchdogStatusLogId = id, RowVersion = 1 },
            cancellationToken).ConfigureAwait(false);
        return await observations.ReadWatchdogObservationAsync(observation.ObservationId, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("The watchdog observation was not persisted.");
    }

    /// <inheritdoc/>
    public Task<FuturesRolloverContractAssignment?> GetAssignmentAsync(DatabentoContractRole role, CancellationToken cancellationToken = default) =>
        state.GetAssignmentAsync(role, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<FuturesRolloverContractAssignment>> ListAssignmentsAsync(CancellationToken cancellationToken = default) =>
        state.ListAssignmentsAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<FuturesRolloverContractAssignment> UpsertAssignmentAsync(FuturesRolloverContractAssignment assignment, long expectedRowVersion, CancellationToken cancellationToken = default) =>
        state.UpsertAssignmentAsync(assignment, expectedRowVersion, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAssignmentAsync(DatabentoContractRole role, long expectedRowVersion, string deletedBy, CancellationToken cancellationToken = default) =>
        state.DeleteAssignmentAsync(role, expectedRowVersion, deletedBy, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReplaceVxAssignmentsAsync(
        FuturesRolloverContractAssignment front, FuturesRolloverContractAssignment second, long expectedFrontVersion,
        long expectedSecondVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(front);
        ArgumentNullException.ThrowIfNull(second);
        return state.ReplaceVxAssignmentsAsync(front, second, expectedFrontVersion, expectedSecondVersion, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<DatabentoWatchdogObservation> UpdateObservationAsync(DatabentoWatchdogObservation observation,
        long expectedRowVersion, string changedBy, CancellationToken cancellationToken = default) =>
        state.UpdateObservationAsync(observation, expectedRowVersion, changedBy, cancellationToken);

    /// <inheritdoc/>
    public Task<DatabentoWatchdogObservation?> GetObservationAsync(long id, CancellationToken cancellationToken = default) =>
        state.GetObservationAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<DatabentoWatchdogObservation>> ListObservationsAsync(DateOnly? valueDate = null,
        DatabentoMajorStatus? status = null, int pageSize = 100, CancellationToken cancellationToken = default) =>
        state.ListObservationsAsync(valueDate, status, pageSize, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteObservationAsync(long id, long expectedRowVersion, string deletedBy, CancellationToken cancellationToken = default) =>
        state.DeleteObservationAsync(id, expectedRowVersion, deletedBy, cancellationToken);

    /// <inheritdoc/>
    public Task<DatasetIncidentTransition> PersistDatasetIncidentAsync(DatasetIncidentTransition transition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return state.PersistDatasetIncidentAsync(transition, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DatasetIncidentTransition>> ListOpenDatasetIncidentsAsync(CancellationToken cancellationToken = default) =>
        state.ListOpenDatasetIncidentsAsync(cancellationToken);
}
