using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class MarketChartRecoveryTests
{
    [Fact]
    public async Task Current_backend_returns_ES_ESBB_and_VX_histories()
    {
        var previous=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");
        Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
        try
        {
            using var session=new MarketChartSession();
            var timer=System.Diagnostics.Stopwatch.StartNew();
            while(true)
            {
                var current=session.Charts;
                if(new[]{"ES","ES-BB","VX"}.All(key=>current.TryGetValue(key,out var chart)&&
                    string.IsNullOrEmpty(chart.Status)&&chart.Series.Any(series=>series.Points.Length>0)))
                {
                    var summary=current.ToDictionary(x=>x.Key,x=>new{status=x.Value.Status,points=x.Value.Series.Sum(series=>series.Points.Length)});
                    File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!,"market-chart-live-recovery.json"),System.Text.Json.JsonSerializer.Serialize(summary));
                    break;
                }
                Assert.True(timer.Elapsed<TimeSpan.FromSeconds(60),"Chart histories unavailable: "+string.Join("; ",current.Select(x=>x.Key+": "+x.Value.Status)));
                await Task.Delay(100);
            }
        }
        finally {Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",previous);}
    }

    [Fact]
    public async Task Missing_responder_retries_retains_old_series_and_recovers_without_restarting_UI()
    {
        var retryStarted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRecovery=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls=0;
        MarketChartSnapshot Snapshot(double price)=>new("",1,2,price-1,price+1,[new("ES","Yellow",[[1,price]])]);
        using var session=new MarketChartSession(async (view,token)=>
        {
            if(Interlocked.Increment(ref calls)==1)
            {
                view.Publish(new(){["ES"]=Snapshot(100)});
                throw new InvalidOperationException("No responders are available for the request");
            }
            retryStarted.SetResult();await allowRecovery.Task.WaitAsync(token);
            view.Publish(new(){["ES"]=Snapshot(101)});recovered.SetResult();
            await Task.Delay(Timeout.Infinite,token);
        },TimeSpan.FromMilliseconds(10));
        await retryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Reconnecting",session.Charts["ES"].Status);
        Assert.Equal(100,session.Charts["ES"].Series[0].Points[0][1]);
        allowRecovery.SetResult();await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("",session.Charts["ES"].Status);
        Assert.Equal(101,session.Charts["ES"].Series[0].Points[0][1]);Assert.Equal(2,calls);
    }
}
