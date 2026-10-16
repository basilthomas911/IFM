using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;
[MessagePack.MessagePackObject]
public sealed record MarketChartSeries([property: MessagePack.Key("name")] string Name, [property: MessagePack.Key("color")] string Color, [property: MessagePack.Key("points")] double[][] Points);
[MessagePack.MessagePackObject]
public sealed record MarketChartSnapshot([property: MessagePack.Key("status")] string Status, [property: MessagePack.Key("start")] double Start, [property: MessagePack.Key("end")] double End, [property: MessagePack.Key("minimum")] double? Minimum, [property: MessagePack.Key("maximum")] double? Maximum, [property: MessagePack.Key("series")] MarketChartSeries[] Series);
/// <summary>Loads the same optional chart histories as WinForms independently of dashboard publication and rendering.</summary>
sealed class MarketChartSession : IDisposable
{
    readonly CancellationTokenSource stopping = new();
    readonly Task worker;
    Dictionary<string,MarketChartSnapshot> charts = [];
    public Dictionary<string,MarketChartSnapshot> Charts => Volatile.Read(ref charts);
    public MarketChartSession() => worker = Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE") == "live"
        ? Task.Run(() => RetryAsync(RunConnectionAsync, TimeSpan.FromSeconds(3))) : Task.CompletedTask;
    internal MarketChartSession(Func<MarketChartSession,CancellationToken,Task> connection,TimeSpan retryDelay) =>
        worker=Task.Run(()=>RetryAsync(()=>connection(this,stopping.Token),retryDelay));

    async Task RetryAsync(Func<Task> connection,TimeSpan retryDelay)
    {
        while(!stopping.IsCancellationRequested)
        {
            try { await connection(); }
            catch(OperationCanceledException) when(stopping.IsCancellationRequested) { return; }
            catch(Exception exception)
            {
                Publish(new[]{"ES","ES-BB","VX"}.ToDictionary(key=>key,
                    _=>new MarketChartSnapshot("Reconnecting: "+exception.Message,0,0,null,null,[])));
            }
            try { await Task.Delay(retryDelay,stopping.Token); }
            catch(OperationCanceledException) when(stopping.IsCancellationRequested) { return; }
        }
    }
    internal void Publish(Dictionary<string,MarketChartSnapshot> next)
    {
        var previous=Charts;
        foreach(var key in next.Keys.ToArray())
            if(next[key].Series.Length==0 && !string.IsNullOrEmpty(next[key].Status) &&
                previous.TryGetValue(key,out var saved) && saved.Series.Any(series=>series.Points.Length>0))
                next[key]=saved with { Status="Reconnecting: "+next[key].Status.Replace("Reconnecting: ","") };
        Volatile.Write(ref charts,next);
    }
    async Task RunConnectionAsync()
    {
        await using var scope = new RustClientServiceScope(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL") ?? "nats://localhost:4222");
        await scope.StartAsync(stopping.Token);
        var market = scope.Container.GetInstance<IMarketDataQueryApi>();
        var analytics = scope.Container.GetInstance<IMarketDataAnalyticsQueryApi>();
        while (!stopping.IsCancellationRequested)
        {
            var end = DateTime.UtcNow;
            var start = end.AddHours(-6);
            var session = await market.GetMarketSessionAsync().WaitAsync(TimeSpan.FromSeconds(10),stopping.Token);
            if (!session.Success || session.Value is null) throw new InvalidOperationException(session.ErrorMessage ?? "Market session unavailable");
            var date = session.Value.OperationalValueDate;
            var entries = await Task.WhenAll(LoadBars("ES"),LoadBars("VX"),LoadBands());
            Publish(entries.ToDictionary(pair => pair.Key, pair => pair.Value));
            await Task.Delay(TimeSpan.FromSeconds(15),stopping.Token);

            async Task<KeyValuePair<string,MarketChartSnapshot>> LoadBars(string symbol)
            {
                try
                {
                    var contract = await market.GetOnTheRunFuturesContractAsync(symbol).WaitAsync(TimeSpan.FromSeconds(10),stopping.Token);
                    if (!contract.Success || contract.Value is null) throw new InvalidOperationException(contract.ErrorMessage ?? "Current contract unavailable");
                    FuturesBarDataReadModel[] bars = [];
                    string? error = null;
                    var model = scope.Services.FeedQueries;
                    model.OnError((_,message) => error = message);
                    await model.GetFuturesBarWindowAsync(contract.Value.ContractId,symbol,date,start,end,values => bars = values).WaitAsync(TimeSpan.FromSeconds(15),stopping.Token);
                    if (error is not null) throw new InvalidOperationException(error);
                    return new(symbol, MarketChartProjection.Bars(symbol,start,end,bars));
                }
                catch(Exception e) when(e is not OperationCanceledException || !stopping.IsCancellationRequested)
                { return new(symbol,new(e.Message,StrategyDetailsAdapter.MarketSeconds(start),StrategyDetailsAdapter.MarketSeconds(end),null,null,[])); }
            }
            async Task<KeyValuePair<string,MarketChartSnapshot>> LoadBands()
            {
                try
                {
                    var result = await analytics.GetFuturesBollingerBandHistoryAsync("ES",date,40).WaitAsync(TimeSpan.FromSeconds(15),stopping.Token);
                    if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
                    var signals = result.Value ?? [];
                    var contract = await market.GetOnTheRunFuturesContractAsync("ES").WaitAsync(TimeSpan.FromSeconds(10),stopping.Token);
                    if (contract.Success && contract.Value is { } es)
                    {
                        var current = await analytics.GetMarketOutlookSnapshotAsync(es.ContractId,date).WaitAsync(TimeSpan.FromSeconds(10),stopping.Token);
                        if (current.Success && current.Value?.FuturesBbSignal is { } point)
                            signals = [.. signals, point];
                    }
                    return new("ES-BB",MarketChartProjection.Bands(date,signals));
                }
                catch(Exception e) when(e is not OperationCanceledException || !stopping.IsCancellationRequested)
                { return new("ES-BB",new(e.Message,0,0,null,null,[])); }
            }
        }
    }
    public void Dispose() { stopping.Cancel();worker.GetAwaiter().GetResult();stopping.Dispose(); }
}
public static class MarketChartProjection
{
    public static MarketChartSnapshot Bars(string symbol,DateTime start,DateTime end,IEnumerable<FuturesBarDataReadModel> source)
    {
        var bars = source.Where(bar => bar.BarRateType == BarRateType.FifteenSeconds && bar.BarDate >= start && bar.BarDate <= end).OrderBy(bar => bar.BarDate).ToArray();
        var points = bars.Select(bar => new[]{StrategyDetailsAdapter.MarketSeconds(bar.BarDate),(double)bar.BarValue}).ToList();
        if (bars.Length == 1 && bars[0].BarDate < end) points.Add([StrategyDetailsAdapter.MarketSeconds(end),(double)bars[0].BarValue]);
        var series = new List<MarketChartSeries>{new(symbol == "ES" ? "ES Close" : "VX",symbol == "ES" ? "Yellow" : "Fuchsia",points.ToArray())};
        var minimum = bars.Length == 0 ? (double?)null : (double)bars.Min(bar => bar.BarValue);
        var maximum = bars.Length == 0 ? (double?)null : (double)bars.Max(bar => bar.BarValue);
        if(symbol == "ES" && bars.Length > 0)
        {
            var up = bars[^1].UpTrendTrigger;var down = bars[^1].DownTrendTrigger;
            series.Add(new("Upper Trigger","Lime",points.Select(p => new[]{p[0],up}).ToArray()));
            series.Add(new("Lower Trigger","Red",points.Select(p => new[]{p[0],down}).ToArray()));
            if (up > 0) maximum = Math.Max(maximum!.Value,up);
            if (down > 0) minimum = Math.Min(minimum!.Value,down);
        }
        var padding = symbol == "ES" ? 5d : 0.025d;
        return new(bars.Length == 0 ? "No saved bars in the last six hours" : "",StrategyDetailsAdapter.MarketSeconds(start),StrategyDetailsAdapter.MarketSeconds(end),minimum-padding,maximum+padding,series.ToArray());
    }
    public static MarketChartSnapshot Bands(DateOnly date,IEnumerable<FuturesBbSignalReadModel> source)
    {
        var signals = source.Where(signal => signal.Metadata.TimeFrame == TimeFrameType.Daily && signal.Metadata.IsValid && signal.Metadata.ValueDate <= date)
            .GroupBy(signal => signal.Metadata.ValueDate)
            .Select(group => group.OrderByDescending(signal => signal.Metadata.MarketDataAsOfUtc).First())
            .OrderBy(signal=>signal.Metadata.ValueDate).TakeLast(40).ToArray();
        double Coordinate(DateOnly day) => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc)).ToUnixTimeSeconds();
        var series = new[] {
            new MarketChartSeries("ES Close","Yellow",signals.Select(s=>new[]{Coordinate(s.Metadata.ValueDate),(double)s.Price}).ToArray()),
            new MarketChartSeries("20 EMA","Blue",signals.Where(s=>s.Ema20Center.HasValue).Select(s=>new[]{Coordinate(s.Metadata.ValueDate),(double)s.Ema20Center!.Value}).ToArray()),
            new MarketChartSeries("Upper Band","Green",signals.Where(s=>s.Upper20.HasValue).Select(s=>new[]{Coordinate(s.Metadata.ValueDate),(double)s.Upper20!.Value}).ToArray()),
            new MarketChartSeries("Lower Band","Red",signals.Where(s=>s.Lower20.HasValue).Select(s=>new[]{Coordinate(s.Metadata.ValueDate),(double)s.Lower20!.Value}).ToArray()) };
        var values = series.SelectMany(s=>s.Points).Select(p=>p[1]).ToArray();
        var minimum = values.Length == 0 ? (double?)null : values.Min();var maximum=values.Length==0 ? (double?)null : values.Max();
        var padding = minimum.HasValue ? Math.Max(1d,(maximum!.Value-minimum.Value)*0.05d) : 1d;
        var start = signals.Length == 0 ? Coordinate(date.AddDays(-39)) : Coordinate(signals[0].Metadata.ValueDate);
        return new(signals.Length==0 ? "No daily Bollinger observations" : "",start,Math.Max(start+86400,Coordinate(date)),minimum-padding,maximum+padding,series);
    }
}
