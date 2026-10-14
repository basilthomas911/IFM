using TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed class TradeWorkspaceBridgeTests
{
    [Fact] public async Task Custom_workspace_intents_require_current_generation_and_are_deduplicated()
    {
        int calls=0;
        var gateway=new ContractEditorGateway(input=>{calls++;return Task.FromResult(new ContractEditorResult("trade-orders",[],[],Context:new(){["orderId"]="27"}));},input=>input.Schema==1 && input.View=="trade-orders" && input.RequestId!=Guid.Empty && input.Intent is "load" or "trade");
        var request=new ContractEditorIntent(1,Guid.NewGuid(),"trade-orders","load",0,null,[]);
        Assert.Equal(0,gateway.Submit(request));await gateway.DrainAsync();Assert.Equal(1,calls);
        Assert.Equal(0,gateway.Submit(request));Assert.Equal(1,calls);
        Assert.Equal(-1,gateway.Submit(request with{Intent="trade"}));
        Assert.Equal(-5,gateway.Submit(new(1,Guid.NewGuid(),"trade-orders","trade",0,"27",[])));Assert.Equal(1,calls);
        Assert.Equal(0,gateway.Submit(new(1,Guid.NewGuid(),"trade-orders","trade",gateway.Snapshot().Generation,"27",[])));await gateway.DrainAsync();Assert.Equal(2,calls);
    }
    [Fact] public async Task Repeated_read_refreshes_do_not_exhaust_the_gateway_or_remove_mutation_deduplication()
    {
        int mutations=0;
        var gateway=new ContractEditorGateway(input=>{
            if(input.Intent=="update-limit")mutations++;
            return Task.FromResult(new ContractEditorResult("trade-orders",[],[],Context:new(){["orderId"]="27"}));
        },input=>input.Schema==1&&input.View=="trade-orders");
        Assert.Equal(0,gateway.Submit(new(1,Guid.NewGuid(),"trade-orders","load",0,null,[])));await gateway.DrainAsync();
        var mutation=new ContractEditorIntent(1,Guid.NewGuid(),"trade-orders","update-limit",gateway.Snapshot().Generation,"27",[]);
        Assert.Equal(0,gateway.Submit(mutation));await gateway.DrainAsync();
        for(var i=0;i<300;i++){
            Assert.Equal(0,gateway.Submit(new(1,Guid.NewGuid(),"trade-orders","refresh-chain",gateway.Snapshot().Generation,"27",[])));
            await gateway.DrainAsync();
        }
        Assert.Equal(0,gateway.Submit(mutation));Assert.Equal(1,mutations);Assert.True(gateway.Snapshot().Outcomes.Length<=10);
    }
    [Fact] public async Task Read_refresh_timeout_preserves_the_display_and_allows_a_later_refresh()
    {
        var gateway=new ContractEditorGateway(input=>input.Intent=="refresh-chain"
            ? Task.FromException<ContractEditorResult>(new TimeoutException("Quote refresh timed out"))
            : Task.FromResult(new ContractEditorResult("trade-orders",[new("visible",[])],[])), _=>true);
        Assert.Equal(0,gateway.Submit(new(1,Guid.NewGuid(),"trade-orders","load",0,null,[])));await gateway.DrainAsync();
        Assert.Equal(0,gateway.Submit(new(1,Guid.NewGuid(),"trade-orders","refresh-chain",gateway.Snapshot().Generation,null,[])));await gateway.DrainAsync();
        Assert.Equal("trade-orders",gateway.Snapshot().Result.View);Assert.Single(gateway.Snapshot().Result.Rows);
        Assert.Equal(0,gateway.Submit(new(1,Guid.NewGuid(),"trade-orders","refresh-execution",gateway.Snapshot().Generation,null,[])));await gateway.DrainAsync();
        Assert.False(gateway.Snapshot().Busy);
    }
    [Fact] public unsafe void Live_trade_editor_load_returns_an_acknowledged_backend_publication()
    {
        using var http=new HttpClient();Assert.True(http.GetAsync("http://localhost:22543/health/launch-ready").GetAwaiter().GetResult().IsSuccessStatusCode);
        var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");
        delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV2 api;get(2,(ApiV1*)&api,(uint)sizeof(ApiV2));ulong handle;api.Base.Create(&handle);
        try{
            var id=Guid.NewGuid();var bytes=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{schema=1,requestId=id,view="trade-orders",intent="load",expectedGeneration=0,originalId=(string?)null,fields=new Dictionary<string,string>()});fixed(byte* p=bytes)Assert.Equal(0,api.Submit(handle,p,bytes.Length));
            var deadline=DateTime.UtcNow.AddSeconds(60);bool completed=false;
            do{int n;api.Base.Copy(handle,8,null,0,&n);var publication=new byte[n];fixed(byte* p=publication){if(api.Base.Copy(handle,8,p,n,&n)!=0)continue;}var result=System.Text.Json.JsonSerializer.Deserialize<ContractEditorPublication>(publication,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
                var outcome=result.Outcomes.FirstOrDefault(x=>x.RequestId==id);if(outcome is not null&&outcome.State!="Pending"){Assert.True(outcome.State=="Completed",outcome.Message);Assert.Equal("trade-orders",result.Result.View);Assert.NotEmpty(result.Result.Choices["portfolio"]);File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!,"trade-orders-live-publication.json"),publication);completed=true;break;}Thread.Sleep(100);
            }while(DateTime.UtcNow<deadline);Assert.True(completed,"Backend trade editor did not complete loading");
        }finally{api.Base.Destroy(handle);Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
    }
    [Fact] public unsafe void Live_blotter_returns_selected_trade_and_preview_without_mutations()
    {
        var old=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE");Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE","live");delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV2 api;get(2,(ApiV1*)&api,(uint)sizeof(ApiV2));ulong handle;api.Base.Create(&handle);
        try{ContractEditorPublication? current=null;foreach(var action in new[]{"load","compose","refresh-execution"}){var id=Guid.NewGuid();var request=new ContractEditorIntent(1,id,"trade-orders",action,current?.Generation??0,current?.Result.Context?.GetValueOrDefault("orderId"),new(){["tradeId"]=current?.Result.Context?.GetValueOrDefault("tradeId")??""});if(action=="load")request=request with{Fields=[]};var bytes=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));fixed(byte* p=bytes)Assert.Equal(0,api.Submit(handle,p,bytes.Length));var deadline=DateTime.UtcNow.AddSeconds(90);bool completed=false;do{int n;api.Base.Copy(handle,8,null,0,&n);var publication=new byte[n];fixed(byte* p=publication){if(api.Base.Copy(handle,8,p,n,&n)!=0)continue;}current=System.Text.Json.JsonSerializer.Deserialize<ContractEditorPublication>(publication,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;var outcome=current.Outcomes.FirstOrDefault(x=>x.RequestId==id);if(outcome is not null&&outcome.State!="Pending"){File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!,"blotter-live-"+action+".json"),publication);Assert.True(outcome.State=="Completed",outcome.Message);completed=true;break;}Thread.Sleep(100);}while(DateTime.UtcNow<deadline);Assert.True(completed,"No backend acknowledgement for "+action);}Assert.True(current!.Result.Context!.ContainsKey("compositionType"));Assert.Contains(current.Result.Rows,r=>r.Fields.GetValueOrDefault("kind")=="broker-preview");Assert.Contains(current.Result.Rows,r=>r.Fields.GetValueOrDefault("kind")=="account");Assert.DoesNotContain("PostgresObjectDataRepository",current.Result.Context.GetValueOrDefault("composerStatus")??"");
            Assert.Equal("False",current.Result.Context["live"]);
            var market=current.Result.Rows.Single(r=>r.Fields.GetValueOrDefault("kind")=="market");
            using var chain=System.Text.Json.JsonDocument.Parse(market.Fields["data"]);
            Assert.StartsWith("Frozen emulator preview",chain.RootElement.GetProperty("windowMethod").GetString());
            var last=chain.RootElement.GetProperty("lastUnderlyingPrice").GetDecimal();Assert.True(last>0);
            Assert.NotEqual(System.Text.Json.JsonValueKind.Null,chain.RootElement.GetProperty("lastUnderlyingTradeAtUtc").ValueKind);
            var metrics=current.Result.Rows.Single(r=>r.Fields.GetValueOrDefault("kind")=="market-metrics");
            using var prices=System.Text.Json.JsonDocument.Parse(metrics.Fields["data"]);
            Assert.Equal(last.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),prices.RootElement.GetProperty("Last").GetString());
            var legs=current.Result.Rows.Where(r=>r.Fields.GetValueOrDefault("kind")=="leg")
                .Select(r=>System.Text.Json.JsonSerializer.Deserialize<TomasAI.IFM.UI.Net.ViewModels.Trade.BrokerTradeLegData>(r.Fields["data"],new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!).ToArray();
            Assert.Equal(4,legs.Length);Assert.All(legs,x=>Assert.True(x.Frozen));
            foreach(var call in new[]{true,false}){
                var shortLeg=legs.Single(x=>x.Role==(call?"-SC":"-SP"));var wing=legs.Single(x=>x.Role==(call?"+LC":"+LP"));
                var ceiling=decimal.Parse(current.Result.Context[call?"callDelta":"putDelta"],System.Globalization.CultureInfo.InvariantCulture)/100m;
                var width=decimal.Parse(current.Result.Context[call?"callWidth":"putWidth"],System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(shortLeg.Delta.HasValue&&Math.Abs(shortLeg.Delta.Value)<=(double)ceiling);
                Assert.True((call?wing.Strike-shortLeg.Strike:shortLeg.Strike-wing.Strike)>=width);
            }
        }
        finally{api.Base.Destroy(handle);Environment.SetEnvironmentVariable("IFM_RUST_UI_MODE",old);}
    }
    [Fact] public unsafe void Trade_publication_is_available_without_initializing_live_trading_services()
    {
        delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;ApiV2 api;
        Assert.Equal(0,get(2,(ApiV1*)&api,(uint)sizeof(ApiV2)));ulong handle;Assert.Equal(0,api.Base.Create(&handle));
        try{int required;Assert.Equal(1,api.Base.Copy(handle,8,null,0,&required));Assert.True(required>0);}
        finally{Assert.Equal(0,api.Base.Destroy(handle));}
    }
}
