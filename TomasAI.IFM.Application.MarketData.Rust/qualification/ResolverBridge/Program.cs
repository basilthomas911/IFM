using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Definitions;

var seed=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement[0].GetProperty("json").GetString()!;
await using var runtime=new NativeMarketDataRuntime(Path.GetFullPath(args[0]),2);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
var port=((IPEndPoint)listener.LocalEndpoint).Port;
runtime.ConfigureHistory($"http://127.0.0.1:{port}/",new('x',32),["127.0.0.1:1"]);
var count=0;
foreach(var symbol in new[]{"es","VX"})
foreach(var scenario in new[]{"ordered","reverse","duplicates","same-date","no-eligible","currency","fallback","missing-currency"})
{
    var root=symbol.ToUpperInvariant();var date=new DateOnly(2026,8,14);var wanted=scenario=="no-eligible"?4:2;
    var maturities=root=="ES"?new[]{new DateOnly(2026,8,14),new DateOnly(2026,9,18),new DateOnly(2026,12,18)}:new[]{new DateOnly(2026,8,14),new DateOnly(2026,9,16),new DateOnly(2026,10,21)};
    var details=maturities.Select((day,index)=>new ContractDetail{Dataset="test",RawSymbol=root+index,Ticker=root,Underlying=root,Instrument=new(1,(uint)(index+42)),ContractKind=ContractKind.Future,MaturityDate=day,ContractMultiplier=50,
        Currency=scenario is "currency" or "fallback" or "missing-currency"?"":"USD",SettlementCurrency=scenario is "fallback" or "missing-currency"?"":" usd ",Exchange=root=="ES"?"XCME":"XCBF",SecurityType="FUT",Cfi="",UnitOfMeasure="USD"}).ToList();
    if(scenario=="reverse")details.Reverse();
    if(scenario=="duplicates")details.Add(details[1]);
    if(scenario=="same-date")details[2]=details[2] with{MaturityDate=details[1].MaturityDate};
    var options=new DatabentoMarketDataRuntimeOptions{FeedOptions=DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi,"GLBX.MDP3"),Contracts=[],FuturesContractCurrencyFallbacks=scenario=="missing-currency"?new Dictionary<string,string>():new Dictionary<string,string>{{root,"USD"}}};
    var body=string.Join("\n",details.Select(detail=>{var node=JsonNode.Parse(seed)!;node["raw_symbol"]=detail.RawSymbol;node["asset"]=root;node["exchange"]=detail.Exchange;node["currency"]=detail.Currency;node["settl_currency"]=detail.SettlementCurrency;
        node["hd"]!["instrument_id"]=detail.Instrument.InstrumentId;var day=detail.MaturityDate!.Value;node["maturity_year"]=day.Year;node["maturity_month"]=day.Month;node["maturity_day"]=day.Day;return node.ToJsonString(new JsonSerializerOptions{WriteIndented=false});}))+"\n";
    using var stopping=new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var server=Task.Run(async()=>{for(var request=0;request<2;request++){using var peer=await listener.AcceptTcpClientAsync(stopping.Token);var stream=peer.GetStream();using var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true);string? line;int length=0;while(!string.IsNullOrEmpty(line=await reader.ReadLineAsync(stopping.Token)))if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))length=int.Parse(line.Split(':')[1]);if(length is <0 or >8192)throw new InvalidDataException("Fixture HTTP request exceeded bound.");if(length>0){var buffer=new char[length];var read=0;while(read<length){var n=await reader.ReadAsync(buffer.AsMemory(read),stopping.Token);if(n==0)throw new EndOfStreamException();read+=n;}}
        var bytes=Encoding.UTF8.GetBytes(request==0?"{\"start\":\"1990-01-01T00:00:00Z\",\"end\":\"2028-12-31T00:00:00Z\",\"schema\":{\"definition\":{\"start\":\"1990-01-01T00:00:00Z\",\"end\":\"2028-12-31T00:00:00Z\"}}}":body);
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"),stopping.Token);await stream.WriteAsync(bytes,stopping.Token);}});
    Exception? expected=null,actual=null;byte[]? expectedBytes=null,actualBytes=null;
    try{expectedBytes=MessagePackBinarySerializer.Shared.Serialize(await new DatabentoCurrentFuturesContractResolver(new FakeFeedFactory(details),options).ResolveEligibleAsync(symbol,date,wanted));}catch(Exception error){expected=error;}
    try{actualBytes=MessagePackBinarySerializer.Shared.Serialize(await new NativeCurrentFuturesContractResolver(()=>runtime,options).ResolveEligibleAsync(symbol,date,wanted,stopping.Token));}catch(Exception error){actual=error;}
    try{await server.WaitAsync(TimeSpan.FromSeconds(10));}finally{stopping.Cancel();}
    if(expected?.GetType()!=actual?.GetType()||expected is null&&!(expectedBytes??[]).SequenceEqual(actualBytes??[]))throw new InvalidDataException($"Resolver parity differs: {symbol}/{scenario}; C#={expected}; Rust={actual}");count++;
}
listener.Stop();Console.WriteLine($"PASS native futures resolver: {count} exact current-C# result/typed-failure comparisons through localhost definition HTTP and the actual DLL, including sorting, duplicates, dates, ES quarters, VX dataset and currency fallback.");
sealed class FakeFeedFactory(IReadOnlyList<ContractDetail> details)
        : IDatabentoFeedFactory
    {
        internal string? LastDataset { get; private set; }
        internal string? LastTicker { get; set; }

        public IDatabentoMarketDataQueries CreateMarketDataQueries(DatabentoFeedOptions options)
        {
            LastDataset = options.Dataset;
            return new FakeQueries(details, this);
        }

        public IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options) =>
            throw new NotSupportedException();
        public IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options) =>
            throw new NotSupportedException();
        public IDatabentoLatestPriceClient CreateLatestPriceClient(DatabentoFeedOptions options) =>
            throw new NotSupportedException();
    }

sealed class FakeQueries(
        IReadOnlyList<ContractDetail> details,
        FakeFeedFactory owner) : IDatabentoMarketDataQueries
    {
        public IReadOnlyList<ContractDetail> GetContractDetails(
            string ticker, TimeSpan? timeout = null)
        {
            owner.LastTicker = ticker;
            var root = ticker.Split('.')[0];
            return details.Where(detail => detail.Ticker == root).ToArray();
        }

        public OptionChainDefinitions GetChainDefinitions(
            OptionChainDefinitionRequest request, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public uint ContractIdToInstrumentId(string contractId, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public string InstrumentIdToContractId(uint instrumentId, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public ContractDetail? GetContractDetail(string contractName, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
        public IReadOnlyList<ContractDetail?> GetContractDetails(
            string[] contractNames, TimeSpan? timeout = null) =>
            throw new NotSupportedException();
    }