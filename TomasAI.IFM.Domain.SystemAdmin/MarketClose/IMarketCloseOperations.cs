using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.SystemAdmin.MarketClose;

/// <summary>Backend transport and durable receipt operations used by the close coordinator.</summary>
public interface IMarketCloseOperations
{
    Task<DateOnly?> GetCompletedDateAsync(CancellationToken token);
    Task<DateTime?> GetPreviousCloseBoundaryAsync(DateOnly valueDate, CancellationToken token);
    Task<Guid> BeginAsync(DateOnly valueDate, DateTime closeBoundaryUtc, CancellationToken token);
    Task StopFeedsAsync(DateOnly valueDate, CancellationToken token);
    Task RecordStageAsync(Guid runId, DateOnly valueDate, string stage, string detail, CancellationToken token);
    Task<ScheduledMarketPositionPage> LoadPositionsAsync(DateOnly valueDate, byte[]? cursor, CancellationToken token);
    Task FinalizePositionAsync(StrategyPositionSnapshot position, DateOnly valueDate, DateTime boundary, CancellationToken token);
    Task CompleteAsync(Guid runId, CancellationToken token);
    Task FailAsync(Guid runId, Exception error, CancellationToken token);
    Task PurgeCompletedMessagesAsync(CancellationToken token);
}
