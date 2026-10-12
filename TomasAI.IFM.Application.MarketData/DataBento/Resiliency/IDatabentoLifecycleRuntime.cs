using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public interface IDatabentoLifecycleRuntime
{
    DateOnly? ActiveValueDate { get; }
    Task PrepareContractsAsync(DateOnly valueDate, CancellationToken cancellationToken);
    Task StartAsync(DateOnly valueDate, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<DatabentoDatasetResetResult> ResetDatasetAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken) =>
        Task.FromException<DatabentoDatasetResetResult>(
            new NotSupportedException("Per-dataset reset is not supported by this runtime."));
    ValueTask<DatabentoBulkWatchdogSnapshot> GetWatchdogSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
