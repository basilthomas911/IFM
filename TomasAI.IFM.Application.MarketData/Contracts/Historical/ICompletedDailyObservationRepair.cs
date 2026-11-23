using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Recovers a completed Daily session from qualified original intraday coverage before Daily publication catches up.</summary>
public interface ICompletedDailyObservationRepair
{
    Task RepairAsync(MarketSeriesIdentity series, string contractId, DateOnly valueDate, CancellationToken token);
}
