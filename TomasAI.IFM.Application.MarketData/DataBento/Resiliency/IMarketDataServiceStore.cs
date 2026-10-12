using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public interface IMarketDataServiceStore
{
    Task<FuturesRolloverContractAssignment?> GetAssignmentAsync(DatabentoContractRole role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FuturesRolloverContractAssignment>> ListAssignmentsAsync(CancellationToken cancellationToken = default);
    Task<FuturesRolloverContractAssignment> UpsertAssignmentAsync(FuturesRolloverContractAssignment assignment, long expectedRowVersion, CancellationToken cancellationToken = default);
    Task DeleteAssignmentAsync(DatabentoContractRole role, long expectedRowVersion, string deletedBy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReplaceVxAssignmentsAsync(FuturesRolloverContractAssignment front, FuturesRolloverContractAssignment second, long expectedFrontVersion, long expectedSecondVersion, CancellationToken cancellationToken = default);
    Task<DatabentoWatchdogObservation> AppendObservationAsync(DatabentoWatchdogObservation observation, CancellationToken cancellationToken = default);
    Task<DatabentoWatchdogObservation> UpdateObservationAsync(DatabentoWatchdogObservation observation, long expectedRowVersion, string changedBy, CancellationToken cancellationToken = default);
    Task<DatabentoWatchdogObservation?> GetObservationAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DatabentoWatchdogObservation>> ListObservationsAsync(DateOnly? valueDate = null, DatabentoMajorStatus? status = null, int pageSize = 100, CancellationToken cancellationToken = default);
    Task DeleteObservationAsync(long id, long expectedRowVersion, string deletedBy, CancellationToken cancellationToken = default);
    Task<DatasetIncidentTransition> PersistDatasetIncidentAsync(
        DatasetIncidentTransition transition,
        CancellationToken cancellationToken = default) => Task.FromResult(transition);
    Task<IReadOnlyList<DatasetIncidentTransition>> ListOpenDatasetIncidentsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DatasetIncidentTransition>>([]);
}
