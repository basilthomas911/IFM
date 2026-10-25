using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

public interface IDownloadLogQueryApi
{
    ValueTask<ServiceResult<MarketDataDownloadLogResult>> GetAttemptAsync(MarketDataDownloadPartition partition, MarketDataDownloadCursor attempt, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<MarketDataDownloadHistoryResult>> GetHistoryAsync(MarketDataDownloadPartition partition, int pageSize = 100, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<MarketDataDownloadStatusResult>> GetStatusAsync(MarketDataDownloadPartition partition, Guid? requiredImportCommandId = null, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default);
}
