using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Definitions;

// The original application catalog executes unchanged against a deterministic
// provider-query fixture. Its output is compared with real HTTP -> Rust -> FFI
// -> native catalog DTO mapping, including all serialized optional properties.
internal static class CatalogParity
{
    internal static async Task ExportOptions(string oracle,string output)
    {
        using var document=JsonDocument.Parse(await File.ReadAllTextAsync(oracle));var seed=document.RootElement[0].GetProperty("json").GetString()!;
        var future=ExactInstrumentDefinition.Parse("GLBX.MDP3",seed).Summary;
        var options=new DatabentoMarketDataRuntimeOptions{FeedOptions=DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development,"GLBX.MDP3"),Contracts=[new(){DomainContractId="ES20261225",ProviderContractName=future.RawSymbol,AssetTypeId=AssetTypeId.Futures}]};
        var catalogType=typeof(DatabentoMarketDataApi).Assembly.GetType("TomasAI.IFM.Application.MarketData.Databento.DatabentoMarketDataCatalog",true)!;
        var created=(Task)catalogType.GetMethod("CreateAsync",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[new Dictionary<string,IDatabentoOperationRunner>{["GLBX.MDP3"]=new Runner(new Queries([future]))},options,CancellationToken.None])!;await created;
        var catalog=created.GetType().GetProperty("Result")!.GetValue(created)!;var fixtures=new List<object>();
        foreach(var mode in new[]{"base","put","fractional","precision","negative","zero","missing-strike","currency","settlement-currency","fallback","no-currency","no-underlying","multiplier","no-multiplier","no-tick","subtick","no-expiry","max-expiry","activation","no-activation","max-activation","maturity","invalid-maturity","unicode"})foreach(var configured in new[]{false,true}){
            var node=JsonNode.Parse(seed)!;node["raw_symbol"]="OPTION0";node["instrument_class"]="C";node["hd"]!["instrument_id"]=43;node["underlying"]=future.RawSymbol;node["underlying_id"]=42;node["strike_price"]=6000L*1000000000;
            string? fallback="USD";
            switch(mode){case "put":node["instrument_class"]="P";break;case "fractional":node["strike_price"]=6000125000000L;break;case "precision":node["strike_price"]=6000123456789L;break;case "negative":node["strike_price"]=-1000L;break;case "zero":node["strike_price"]=0;break;case "missing-strike":node["strike_price"]=long.MaxValue;break;
                case "currency":node["currency"]=" eur ";break;case "settlement-currency":node["currency"]="";node["settl_currency"]="cad";break;case "fallback":case "no-currency":node["currency"]="";node["settl_currency"]="";fallback=mode=="fallback"?" gbp ":null;break;
                case "no-underlying":node["underlying"]="OTHER";break;case "multiplier":node["contract_multiplier"]=100;break;case "no-multiplier":node.AsObject().Remove("contract_multiplier");break;case "no-tick":node["min_price_increment"]=long.MaxValue;break;case "subtick":node["min_price_increment"]=1;break;
                case "no-expiry":node.AsObject().Remove("expiration");break;case "max-expiry":node["expiration"]=ulong.MaxValue;break;case "activation":node["activation"]=1793102400123456789UL;break;case "no-activation":node.AsObject().Remove("activation");break;case "max-activation":node["activation"]=ulong.MaxValue;break;
                case "maturity":node["maturity_year"]=2027;node["maturity_month"]=3;node["maturity_day"]=19;break;case "invalid-maturity":node["maturity_year"]=0;node["maturity_month"]=1;node["maturity_day"]=1;break;case "unicode":node["raw_symbol"]="OPTION-\uE000-??";break;}
            var source=node.ToJsonString();byte[]? content=null;string? error=null;
            try{
                var detail=LiveDetail(source);
                var id=configured?"ES20261225C6000":detail.RawSymbol;
                var method=catalogType.GetMethod(configured?"MapConfiguredOption":"MapOption",BindingFlags.NonPublic|(configured?BindingFlags.Static:BindingFlags.Instance))!;
                var model=(TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel)method.Invoke(configured?null:catalog,[id,detail,fallback])!;
                content=MessagePack.MessagePackSerializer.Serialize(model);
            }catch(Exception problem){error=(problem is TargetInvocationException invocation?invocation.InnerException!:problem).GetType().Name;}
            byte[]? futureContent=null;string? futureError=null;
            try{
                var projected=(TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel)catalogType.GetMethod("MapFutures",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,["ES20261225",LiveDetail(source),fallback,configured,!configured])!;
                futureContent=MessagePack.MessagePackSerializer.Serialize(projected);
            }catch(Exception problem){futureError=(problem is TargetInvocationException invocation?invocation.InnerException!:problem).GetType().Name;}
            fixtures.Add(new{mode,configured,source,fallback,underlying=mode=="no-underlying"?null:"ES20261225",underlyingPublisher=future.Instrument.PublisherId,content,error,futureContent,futureError});
        }
        await File.WriteAllTextAsync(output,JsonSerializer.Serialize(fixtures));Console.WriteLine($"PASS exported {fixtures.Count} original C# option read-model projections.");
    }
    static ContractDetail LiveDetail(string source){
        var node=JsonNode.Parse(source)!;
        var detail=ExactInstrumentDefinition.Parse("GLBX.MDP3",source).Summary;
                // Live query maturity is distinct from stored-summary maturity.
                // Exercise the unchanged CLR live-detail converter, not a native
                // reimplementation, for explicit provider calendar fields.
                var assembly=typeof(DatabentoFeedOptions).Assembly;
                var nativeType=assembly.GetType("TomasAI.IFM.Framework.MarketData.DataBento.NativeContractDetail",true)!;
                var nativeDetail=Activator.CreateInstance(nativeType)!;
                var year=node["maturity_year"]?.GetValue<int>()??0;var month=node["maturity_month"]?.GetValue<int>()??0;var day=node["maturity_day"]?.GetValue<int>()??0;
                if(month is >=1 and <=12 && day is >=1 and <=31){
                    nativeType.GetField("Flags")!.SetValue(nativeDetail,Enum.ToObject(nativeType.GetField("Flags")!.FieldType,32));
                    nativeType.GetField("MaturityYear")!.SetValue(nativeDetail,checked((ushort)year));nativeType.GetField("MaturityMonth")!.SetValue(nativeDetail,checked((byte)month));nativeType.GetField("MaturityDay")!.SetValue(nativeDetail,checked((byte)day));
                }
                var queryType=assembly.GetType("TomasAI.IFM.Framework.MarketData.DataBento.DatabentoMarketDataQueries",true)!;
                return detail with{MaturityDate=(DateOnly?)queryType.GetMethod("GetMaturityDate",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[nativeDetail])};
    }
    internal static async Task Run(string library, string seed)
    {
        var sources = new List<string> { seed };
        for (var index = 0; index < 8; index++)
        {
            var node = JsonNode.Parse(seed)!;
            node["hd"]!["instrument_id"] = 43 + index; node["raw_symbol"] = "OPTION" + index;
            node["instrument_class"] = index % 2 == 0 ? "C" : "P"; node["underlying"] = "ESZ6"; node["underlying_id"] = 42;
            node["strike_price"] = (6000L + index / 2 * 100) * 1_000_000_000;
            sources.Add(node.ToJsonString());
        }
        var options = new DatabentoMarketDataRuntimeOptions {
            FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3"),
            Contracts = [
                new() { DomainContractId = "ES20261225", ProviderContractName = "ESZ6", AssetTypeId = AssetTypeId.Futures, OnTheRun = true, Rollover = true },
                new() { DomainContractId = "ES20261225C6000", ProviderContractName = "OPTION0", AssetTypeId = AssetTypeId.FuturesOption }
            ]
        };
        var fixture = new Queries(sources.Select(s => ExactInstrumentDefinition.Parse("GLBX.MDP3", s).Summary).ToArray());
        var type = typeof(DatabentoMarketDataApi).Assembly.GetType("TomasAI.IFM.Application.MarketData.Databento.DatabentoMarketDataCatalog", true)!;
        var creation = (Task)type.GetMethod("CreateAsync", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
            [new Dictionary<string, IDatabentoOperationRunner> { ["GLBX.MDP3"] = new Runner(fixture) }, options, CancellationToken.None])!;
        await creation;
        var original = (IDatabentoMarketDataCatalog)creation.GetType().GetProperty("Result")!.GetValue(creation)!;

        await using var runtime = new NativeMarketDataRuntime(library);
        runtime.ConfigureCatalog(32, 1_048_576);
        static async IAsyncEnumerable<CatalogSourceLine> Lines(IEnumerable<string> values) { foreach (var source in values) { yield return new("GLBX.MDP3", source); await Task.Yield(); } }
        await runtime.HydrateCatalogAsync(Lines(sources));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var stopping = new CancellationTokenSource();
        var peers = new System.Collections.Concurrent.ConcurrentBag<Task>();
        var optionBody = Encoding.UTF8.GetBytes(string.Join('\n', sources.Skip(1)) + "\n");
        var exactBody = Encoding.UTF8.GetBytes(string.Join('\n', sources) + "\n");
        var server = Task.Run(async () => {
            try {
                while (true) {
                    var client = await listener.AcceptTcpClientAsync(stopping.Token);
                    peers.Add(Task.Run(async () => {
                        using (client) {
                            var stream = client.GetStream(); var header = new List<byte>(); var one = new byte[1];
                            while (!header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) {
                                await stream.ReadExactlyAsync(one, stopping.Token); header.Add(one[0]); if (header.Count > 16_384) throw new InvalidDataException("Request header limit.");
                            }
                            var text = Encoding.ASCII.GetString(header.ToArray());
                            var length = text.Split("\r\n").Where(s => s.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Select(s => int.Parse(s.Split(':')[1].Trim())).DefaultIfEmpty(0).Single();
                            var requestBody = new byte[length];
                            await stream.ReadExactlyAsync(requestBody, stopping.Token);
                            var form = Uri.UnescapeDataString(Encoding.UTF8.GetString(requestBody).Replace("+", " ", StringComparison.Ordinal));
                            var exact = form.Contains("stype_in=raw_symbol", StringComparison.Ordinal);
                            if (exact && !form.Contains("symbols=ESZ6,OPTION0", StringComparison.Ordinal))
                                throw new InvalidDataException("Bootstrap did not request its exact configured provider identities.");
                            var body = text.Contains("metadata.get_dataset_range", StringComparison.Ordinal)
                                ? Encoding.UTF8.GetBytes("""{"start":"1990-01-01T00:00:00Z","end":"2026-12-31T00:00:00Z","schema":{"definition":{"start":"1990-01-01T00:00:00Z","end":"2026-12-25T23:59:59Z"}}}""") : exact ? exactBody : optionBody;
                            await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"), stopping.Token);
                            await stream.WriteAsync(body, stopping.Token);
                        }
                    }));
                }
            } catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        });
        try
        {
            runtime.ConfigureHistory($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/", new string('x', 32), ["127.0.0.1:1"]);
            var native = await NativeContractCatalog.CreateAsync(runtime, options, TimeProvider.System, TimeSpan.FromSeconds(5));
            static void Same<T>(T expected, T actual, string operation)
            {
                var left = TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(expected);
                var right = TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(actual);
                if (!(left ?? []).AsSpan().SequenceEqual(right ?? [])) throw new InvalidDataException($"Catalog MessagePack parity failed: {operation}");
            }
            Same(original.FindFutures("ES20261225"), native.FindFutures("ES20261225"), "configured future");
            Same(original.FindFuturesOption("ES20261225C6000"), native.FindFuturesOption("ES20261225C6000"), "configured option");
            Same(original.FindOptionUnderlying("ES20261225C6000"), native.FindOptionUnderlying("ES20261225C6000"), "option underlying");
            Same(original.FindOptionUnderlying("ES20261225"), native.FindOptionUnderlying("ES20261225"), "future is not an option");
            var maturity = new DateOnly(2026, 12, 25);
            Same(await original.GetOptionChainAsync("ES20261225", maturity), await native.GetOptionChainAsync("ES20261225", maturity), "exact-underlying chain");
            Same(await original.GetOptionChainBySymbolAsync("ES", maturity), await native.GetOptionChainBySymbolAsync("ES", maturity), "parent chain");
            Same(await original.GetOptionChainByRootAsync("ES", "ES", maturity), await native.GetOptionChainByRootAsync("ES", "ES", maturity), "root chain");
            Same(await original.GetOptionChainByRootAsync("ES", "ES", maturity.AddDays(-1), maturity.AddDays(1)), await native.GetOptionChainByRootAsync("ES", "ES", maturity.AddDays(-1), maturity.AddDays(1)), "root range");
            Same(await original.GetOptionChainAsync("ES20261225", maturity.AddDays(1)), await native.GetOptionChainAsync("ES20261225", maturity.AddDays(1)), "empty chain");
            Console.WriteLine("Catalog application parity passed: configured futures/options, underlying mappings, exact/parent/root/range/empty chains, complete DTOs and semantic digests.");
        }
        finally { stopping.Cancel(); listener.Stop(); await server; await Task.WhenAll(peers); }
    }
    sealed class Runner(IDatabentoMarketDataQueries queries) : IDatabentoOperationRunner
    {
        public Task<T> RunAsync<T>(Func<IDatabentoMarketDataQueries, T> operation, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(operation(queries)); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    sealed class Queries(ContractDetail[] definitions) : IDatabentoMarketDataQueries
    {
        public IReadOnlyList<ContractDetail?> GetContractDetails(string[] names, TimeSpan? timeout = null) => names.Select(n => definitions.SingleOrDefault(d => d.RawSymbol == n)).ToArray();
        public ContractDetail? GetContractDetail(string name, TimeSpan? timeout = null) => definitions.SingleOrDefault(d => d.RawSymbol == name);
        public IReadOnlyList<ContractDetail> GetContractDetails(string ticker, TimeSpan? timeout = null) => definitions.Where(d => d.ContractKind is ContractKind.CallOption or ContractKind.PutOption).ToArray();
        public OptionChainDefinitions GetChainDefinitions(OptionChainDefinitionRequest request, TimeSpan? timeout = null)
        {
            var filter = typeof(DatabentoFeedOptions).Assembly.GetType("TomasAI.IFM.Framework.MarketData.DataBento.OptionChainDefinitionFilter", true)!;
            return (OptionChainDefinitions)filter.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
                ["GLBX.MDP3", request, request.UniversePolicy == OptionUniversePolicy.UnderlyingFuture ? definitions[0] : null, GetContractDetails("ES.OPT")])!;
        }
        public uint ContractIdToInstrumentId(string id, TimeSpan? timeout = null) => throw new UnreachableException("Unused qualification operation.");
        public string InstrumentIdToContractId(uint id, TimeSpan? timeout = null) => throw new UnreachableException("Unused qualification operation.");
    }
}
