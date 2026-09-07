using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Read-only completion evidence over Core NATS. These requests never initiate an import.</summary>
public sealed class DownloadLogQueryApi(IActorProducer producer) : TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IDownloadLogQueryApi
{
    public ValueTask<ServiceResult<MarketDataDownloadLogResult>> GetAttemptAsync(MarketDataDownloadPartition partition, MarketDataDownloadCursor attempt, CancellationToken cancellationToken = default)
    {
        var query = new GetMarketDataDownloadLogQuery(partition) { Attempt = attempt };
        return producer.RequestAsync<MarketDataDownloadLogResult, GetMarketDataDownloadLogQuery>(query.Subject, query, cancellationToken);
    }
    public ValueTask<ServiceResult<MarketDataDownloadHistoryResult>> GetHistoryAsync(MarketDataDownloadPartition partition, int pageSize = 100, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        var query = new GetMarketDataDownloadHistoryQuery(partition) { PageSize = pageSize, Cursor = cursor };
        return producer.RequestAsync<MarketDataDownloadHistoryResult, GetMarketDataDownloadHistoryQuery>(query.Subject, query, cancellationToken);
    }
    public ValueTask<ServiceResult<MarketDataDownloadStatusResult>> GetStatusAsync(MarketDataDownloadPartition partition, Guid? requiredImportCommandId = null, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        var query = new GetMarketDataDownloadStatusQuery(partition) { RequiredImportCommandId = requiredImportCommandId, Cursor = cursor };
        return producer.RequestAsync<MarketDataDownloadStatusResult, GetMarketDataDownloadStatusQuery>(query.Subject, query, cancellationToken);
    }
}
