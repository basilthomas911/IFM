using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

public interface IDownloadLogCommandApi
{
    ValueTask<ServiceResult<GuidResult>> RecordAsync(MarketDataDownloadOutcome outcome, CancellationToken cancellationToken = default);
}
