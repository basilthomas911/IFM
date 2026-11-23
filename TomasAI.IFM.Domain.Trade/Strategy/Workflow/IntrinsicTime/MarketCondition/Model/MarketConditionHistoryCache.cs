using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

/// <summary>Background-published historical context. A lookup cannot acquire missing history.</summary>
public sealed class MarketConditionHistoryCache
{
    sealed record Snapshot(string Contract, DateOnly ValueDate, DateTime CapturedAtUtc, FuturesBarDataReadModel[] Bars);
    Snapshot? current;

    public void Publish(string contract, DateOnly valueDate, DateTime capturedAtUtc, IEnumerable<FuturesBarDataReadModel> bars)
    {
        var valid = bars.Where(bar => bar.BarValue > 0m && bar.BarDate.Kind == DateTimeKind.Utc)
            .OrderByDescending(bar => bar.BarDate).Take(2048).ToArray();
        Volatile.Write(ref current, new(contract, valueDate, capturedAtUtc, valid));
    }

    public bool TryGetBaseline(string contract, DateOnly valueDate, DateTime target, DateTime now, out FuturesBarDataReadModel? bar)
    {
        var snapshot = Volatile.Read(ref current);
        bar = snapshot is not null && snapshot.Contract == contract && snapshot.ValueDate == valueDate
            && snapshot.CapturedAtUtc <= now && now < snapshot.CapturedAtUtc.AddSeconds(30)
            ? snapshot.Bars.FirstOrDefault(value => value.BarDate <= target) : null;
        return bar is not null;
    }
}
