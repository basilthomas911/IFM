using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class OptionChainStatisticsLiveTests
{
    [Fact]
    public unsafe void Live_option_chain_publishes_dated_volume_and_open_interest_without_order_mutations()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var ready = Stopwatch.StartNew();
        while (true)
        {
            try { if (http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult().IsSuccessStatusCode) break; }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            Assert.True(ready.Elapsed < TimeSpan.FromSeconds(90), "Backend launch readiness did not pass");
            Thread.Sleep(500);
        }
        var previous = Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");
        Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE", "live");
        delegate* unmanaged[Cdecl]<uint, ApiV1*, uint, int> get = &BridgeBootstrap.GetApi;
        ApiV2 api; Assert.Equal(0,get(2,(ApiV1*)&api,(uint)sizeof(ApiV2)));
        ulong handle; Assert.Equal(0,api.Base.Create(&handle));
        try
        {
            var current = Send(api,handle,"load",new());
            current = Send(api,handle,"compose",new(){["tradeId"]=current.Result.Context!["tradeId"]});
            current = Send(api,handle,"chain",new()
            {
                ["live"]="true",["expiry"]="2026-11-20",
                ["putDelta"]="16",["callDelta"]="16",["putWidth"]="50",["callWidth"]="50"
            });
            var watch = Stopwatch.StartNew();
            while (true)
            {
                var row = current.Result.Rows.SingleOrDefault(x=>x.Fields.GetValueOrDefault("kind")=="market");
                if (row is not null)
                {
                    using var chain = JsonDocument.Parse(row.Fields["data"]);
                    var contracts = chain.RootElement.GetProperty("contracts").EnumerateArray().ToArray();
                    var volume = contracts.Count(x=>x.GetProperty("volume").ValueKind==JsonValueKind.Number);
                    var interest = contracts.Count(x=>x.GetProperty("openInterest").ValueKind==JsonValueKind.Number);
                    var evidenceOutput=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
                    File.WriteAllText(Path.Combine(evidenceOutput,"option-chain-live-statistics-observation.json"),JsonSerializer.Serialize(new
                    { expiry="2026-11-20",contracts=contracts.Length,withVolume=volume,withOpenInterest=interest,elapsedSeconds=watch.Elapsed.TotalSeconds }));
                    if (volume>0 && interest>0)
                    {
                        Assert.Equal("True",current.Result.Context!["live"]);
                        foreach(var contract in contracts)
                        {
                            if(contract.GetProperty("volume").ValueKind==JsonValueKind.Number)
                            {
                                Assert.True(contract.GetProperty("volume").GetInt64()>=0);
                                Assert.Equal(JsonValueKind.String,contract.GetProperty("volumeValueDate").ValueKind);
                            }
                            if(contract.GetProperty("openInterest").ValueKind==JsonValueKind.Number)
                                Assert.Equal(JsonValueKind.String,contract.GetProperty("openInterestValueDate").ValueKind);
                        }
                        var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
                        File.WriteAllText(Path.Combine(output,"option-chain-live-statistics.json"),JsonSerializer.Serialize(new
                        { expiry="2026-11-20",contracts=contracts.Length,withVolume=volume,withOpenInterest=interest,elapsedSeconds=watch.Elapsed.TotalSeconds,chain=chain.RootElement }));
                        return;
                    }
                }
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60),"Live chain did not publish both volume and open interest");
                Thread.Sleep(1000);
                current=Send(api,handle,"refresh-chain",new());
            }
        }
        finally { api.Base.Destroy(handle);Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",previous); }
    }

    static unsafe ContractEditorPublication Read(ApiV2 api,ulong handle)
    {
        for (var retry=0;retry<10;retry++)
        {
            int length;var probe=api.Base.Copy(handle,8,null,0,&length);Assert.True(probe is 0 or 1);
            Assert.InRange(length,1,16*1024*1024);
            var bytes=new byte[length];int copied;
            fixed(byte* p=bytes)copied=api.Base.Copy(handle,8,p,length,&length);
            if(copied==1)continue;
            Assert.Equal(0,copied);
            return JsonSerializer.Deserialize<ContractEditorPublication>(bytes.AsSpan(0,length),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
        throw new TimeoutException("Trade publication grew repeatedly while copying");
    }
    static unsafe ContractEditorPublication Send(ApiV2 api,ulong handle,string action,Dictionary<string,string> fields)
    {
        var id=Guid.NewGuid();
        for(var retry=0;;retry++)
        {
            var current=Read(api,handle);
            var request=new ContractEditorIntent(1,id,"trade-orders",action,current.Generation,current.Result.Context?.GetValueOrDefault("orderId"),fields);
            var bytes=JsonSerializer.SerializeToUtf8Bytes(request,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            int status;fixed(byte* p=bytes)status=api.Submit(handle,p,bytes.Length);
            if(status==0)break;
            Assert.True(retry<5 && status is -4 or -5,"Read request rejected: "+status);Thread.Sleep(100);
        }
        var watch=Stopwatch.StartNew();
        while(watch.Elapsed<TimeSpan.FromSeconds(90))
        {
            var current=Read(api,handle);var outcome=current.Outcomes.FirstOrDefault(x=>x.RequestId==id);
            if(outcome is not null && outcome.State!="Pending")
            { Assert.True(outcome.State=="Completed",outcome.Message);return current; }
            Thread.Sleep(100);
        }
        throw new TimeoutException("No acknowledgement for "+action);
    }
}
