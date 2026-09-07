using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
﻿using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public interface IMarketDataDbContext : IObjectRepository<MarketDataDbContext> ,IMarketDataDbReadContext, IMarketDataDbWriteContext
{
    Task InsertMarketDataDownloadLogAsync(MarketDataDownloadOutcome outcome, Guid logCommandId, string payloadSha256, CancellationToken cancellationToken = default);
    Task<MarketDataDownloadLogResult> GetMarketDataDownloadLogAsync(MarketDataDownloadPartition partition, MarketDataDownloadCursor attempt, CancellationToken cancellationToken = default);
    Task<MarketDataDownloadHistoryResult> GetMarketDataDownloadHistoryAsync(MarketDataDownloadPartition partition, int pageSize = 100, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default);
    Task<MarketDataDownloadStatusResult> GetMarketDataDownloadStatusAsync(MarketDataDownloadPartition partition, Guid? requiredImportCommandId = null, MarketDataDownloadCursor? cursor = null, CancellationToken cancellationToken = default);

    IMarketDataDbReadContext DbReader { get; }
    IMarketDataDbWriteContext DbWriter { get; }
}
