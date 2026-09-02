using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query;

public static class MarketOutlookSnapshotQueryExtensions
{
    public static Task<ServiceResult<MarketOutlookReadModel>> GetMarketOutlookSnapshotAsync(
        this IMarketOutlookSnapshotQueryContext context,
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
        => GetLatestAsync(context, contractId, valueDate, cancellationToken);

    static async Task<ServiceResult<MarketOutlookReadModel>> GetLatestAsync(
        IMarketOutlookSnapshotQueryContext context,
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await context.DbFactory.MarketDataDb.GetMarketOutlookSnapshotAsync(
                contractId, valueDate, cancellationToken).ConfigureAwait(false);
            return snapshot is null
                ? new ServiceFailed<MarketOutlookReadModel>(
                    GetMarketOutlookSnapshotQuery.ErrorId,
                    $"No Market Outlook snapshot is available for {contractId} on or before {valueDate:yyyy-MM-dd}.")
                : new ServiceOk<MarketOutlookReadModel>(snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ServiceFailed<MarketOutlookReadModel>(
                GetMarketOutlookSnapshotQuery.ErrorId, exception.Message);
        }
    }
}
