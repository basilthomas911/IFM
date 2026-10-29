using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Definitions;
if(args.Length!=1)throw new ArgumentException("Expected fixture output path.");
var output=new List<object>();var date=new DateOnly(2026,8,14);
foreach(var symbol in new[]{" es ","vx","ABC"})foreach(var mode in new[]{"normal","duplicate","settlement","fallback","missing-currency","null-multiplier","negative-multiplier","expiry-fallback","empty","unicode-order"}) {
 var root=symbol.Trim().ToUpperInvariant();
 Row[] rows=[new(root+"Q6",new(2026,8,19),null,"USD","USD",50),new(root+"U6",new(2026,9,18),null,"USD","USD",50),new(root+"Z6",new(2026,12,18),null,"USD","USD",50)];
 rows=mode switch {
  "empty"=>[],"duplicate"=>[rows[1],rows[1],rows[0],rows[2]],
  "settlement"=>rows.Select(x=>x with{Currency=" ",Settlement=" usd "}).ToArray(),
  "fallback" or "missing-currency"=>rows.Select(x=>x with{Currency="",Settlement=" "}).ToArray(),
  "null-multiplier"=>rows.Select(x=>x with{Multiplier=null}).ToArray(),
  "negative-multiplier"=>rows.Select(x=>x with{Multiplier=-5}).ToArray(),
  "expiry-fallback"=>rows.Select(x=>x with{Maturity=null,Expiration=checked((ulong)(new DateTimeOffset(x.Maturity!.Value.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero).UtcTicks-DateTime.UnixEpoch.Ticks)*100)}).ToArray(),
  "unicode-order"=>[rows[1] with{Symbol="\uE000"},rows[1] with{Symbol="\U00010000"},rows[2]],_=>rows};
 foreach(var count in new[]{1,2,4}) {
  var fallback=mode=="fallback"?" usd ":null;
  var options=new DatabentoMarketDataRuntimeOptions{FeedOptions=DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi,"GLBX.MDP3"),Contracts=[],
   FuturesContractCurrencyFallbacks=fallback is null?new Dictionary<string,string>():new Dictionary<string,string>{{root,fallback}}};
  var details=rows.Select((r,i)=>new ContractDetail {Dataset="test",RawSymbol=r.Symbol,Ticker=root,Underlying=root,Instrument=new(3,(uint)(i+1)),ContractKind=ContractKind.Future,
   MaturityDate=r.Maturity,ExpirationTimestampNanoseconds=r.Expiration,ContractMultiplier=r.Multiplier,Currency=r.Currency,SettlementCurrency=r.Settlement,Exchange="XCME",SecurityType="FUT",Cfi="",UnitOfMeasure="USD"}).ToArray();
  FuturesContractV3ReadModel[]? result=null;string? failure=null;var identity=root;
  try{result=(await new DatabentoCurrentFuturesContractResolver(new Factory(details),options).ResolveEligibleAsync(symbol,date,count)).ToArray();}
  catch(OnTheRunFuturesContractNotFoundException){failure="NotFound";}
  catch(MarketDataContractMappingException){failure="MissingCurrency";var selected=details.Where(d=>d.MaturityDate is{} m && m>=date.AddDays(1) && (root!="ES"||m.Month is 3 or 6 or 9 or 12)).OrderBy(d=>d.MaturityDate).ThenBy(d=>d.RawSymbol,StringComparer.Ordinal).First();identity=$"{root}{selected.MaturityDate:yyyyMMdd}";}
  output.Add(new{symbol,date=date.DayNumber,count,fallback,mode,rows,content=MessagePackSerializer.Serialize((result,failure,identity))});
 }
}
await File.WriteAllTextAsync(args[0],System.Text.Json.JsonSerializer.Serialize(output));Console.WriteLine($"PASS exported {output.Count} actual C# current-future/term-structure cases.");
record Row(string Symbol,DateOnly? Maturity,ulong? Expiration,string Currency,string Settlement,int? Multiplier);
sealed class Factory(IReadOnlyList<ContractDetail> rows):IDatabentoFeedFactory {
 public IDatabentoMarketDataQueries CreateMarketDataQueries(DatabentoFeedOptions options)=>new Queries(rows);
 public IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options)=>throw new NotSupportedException();
 public IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options)=>throw new NotSupportedException();
 public IDatabentoLatestPriceClient CreateLatestPriceClient(DatabentoFeedOptions options)=>throw new NotSupportedException();
}
sealed class Queries(IReadOnlyList<ContractDetail> rows):IDatabentoMarketDataQueries {
 public IReadOnlyList<ContractDetail> GetContractDetails(string ticker,TimeSpan? timeout=null)=>rows;
 public OptionChainDefinitions GetChainDefinitions(OptionChainDefinitionRequest request,TimeSpan? timeout=null)=>throw new NotSupportedException();
 public uint ContractIdToInstrumentId(string contractId,TimeSpan? timeout=null)=>throw new NotSupportedException();
 public string InstrumentIdToContractId(uint instrumentId,TimeSpan? timeout=null)=>throw new NotSupportedException();
 public ContractDetail? GetContractDetail(string contractName,TimeSpan? timeout=null)=>throw new NotSupportedException();
 public IReadOnlyList<ContractDetail?> GetContractDetails(string[] contractNames,TimeSpan? timeout=null)=>throw new NotSupportedException();
}
