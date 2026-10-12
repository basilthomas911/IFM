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
        
    {
        ArgumentNullException.ThrowIfNull(context);
        return GetLatestAsync(context, contractId, valueDate, cancellationToken);
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This message boundary converts arbitrary provider failures into the typed failed reply; failures must not escape the actor protocol.")] 
    internal static async Task<ServiceResult<MarketOutlookReadModel>> GetLatestAsync(
        IMarketOutlookSnapshotQueryContext context,
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = valueDate;
            while (true)
            {
                var snapshot = await context.DbFactory.MarketDataDb.GetMarketOutlookSnapshotAsync(
                    contractId, cutoff, cancellationToken).ConfigureAwait(false);
                if (snapshot is null)
                    return Missing(contractId, valueDate);
                if (!context.Policy.RejectSyntheticSnapshots
                    || snapshot.SnapshotSource != MarketOutlookSnapshotSource.Synthetic)
                    return new ServiceOk<MarketOutlookReadModel>(snapshot);
                if (snapshot.ValueDate == DateOnly.MinValue)
                    return Missing(contractId, valueDate);
                cutoff = snapshot.ValueDate.AddDays(-1);
            }
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

    static ServiceFailed<MarketOutlookReadModel> Missing(
        string contractId,
        DateOnly valueDate) => new(
            GetMarketOutlookSnapshotQuery.ErrorId,
            $"No Market Outlook snapshot is available for {contractId} on or before {valueDate:yyyy-MM-dd}.");
}
