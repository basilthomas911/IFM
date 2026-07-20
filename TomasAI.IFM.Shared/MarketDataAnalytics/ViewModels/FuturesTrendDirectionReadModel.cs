using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels
{
    public record FuturesTrendDirectionReadModel(
        string ContractId,
        DateOnly ValueDate,
        TimeOnly Timestamp,
        int LookbackInterval,
        int UpTrendCount,
        int DownTrendCount,
        FuturesTrendType TrendDirection)
    {
    }
}
