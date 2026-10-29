using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
if(args.Length!=2) throw new ArgumentException("Expected new native DLL and baseline directory.");
var failures=new Dictionary<string,uint>{{"None",0},{"InvalidInput",1},{"Expired",2},{"UndefinedGreeks",3},{"PriceOutOfBounds",4},{"ImpliedVolatilityNotIdentifiable",5},{"VolatilityNotBracketed",6},{"NonConvergence",7},{"NumericalFailure",8}};
int compared=0;
await using(var runtime=new NativeMarketDataRuntime(Path.GetFullPath(args[0]))) {
    foreach(var legacy in new[]{false,true}) {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(args[1],legacy?"LegacyPricingOracle.json":"PricingOracle.json")));
        foreach(var c in document.RootElement.EnumerateArray()) {
            double D(string key)=>BitConverter.UInt64BitsToDouble(c.GetProperty(key).GetUInt64());
            foreach(uint action in new uint[]{0,1,2,3,4}) {
                var input=new FuturesPricingInput(D("forward"),D("strike"),D("time"),D("rate"),D(action is 2 or 4?"market":"sigma"),c.GetProperty("call").GetBoolean(),c.GetProperty("american").GetBoolean(),c.GetProperty("futuresStyle").GetBoolean(),legacy,c.GetProperty("steps").GetUInt32());
                var r=await runtime.CalculateFuturesOptionAsync(input,action,TimeSpan.FromMinutes(2));var name=action switch{0=>"theoretical",1=>"price",2=>"iv",3=>"delta",_=>"solve"};
                var expectedFailure=failures[c.GetProperty(name+"Failure").GetString()!];
                if(r.Failure!=expectedFailure) throw new InvalidDataException($"{name} failure mismatch {r.Failure} != {expectedFailure}");
                if(expectedFailure==0) {
                    var all=action==0?[c.GetProperty(name).GetUInt64()]:c.GetProperty(name).EnumerateArray().Select(v=>v.GetUInt64()).ToArray();
                    var expected=action==4?[all[1],all[0]]:all;
                    double[] actual=action switch{0=>[r.Price],3=>[r.Price,r.Delta],4=>[r.Price,r.Volatility],_=>[r.Price,r.Volatility,r.Delta,r.Gamma,r.Vega,r.Theta,r.Rho]};
                    if(!actual.Select(BitConverter.DoubleToUInt64Bits).SequenceEqual(expected)) throw new InvalidDataException($"{name} result bit mismatch");
                }
                compared++;
            }
        }
    }
    var expired=new FuturesPricingInput(6500,6500,.1,.04,.2,true,false,false);
    try{await runtime.CalculateFuturesOptionAsync(expired,1,TimeSpan.Zero);throw new InvalidDataException("Expired pricing ran");}catch(TimeoutException){}
}
for(var cycle=0;cycle<1000;cycle++) {
    var runtime=new NativeMarketDataRuntime(Path.GetFullPath(args[0]),1);
    using var cancel=new CancellationTokenSource();
    var input=new FuturesPricingInput(6500,6500,.1,.04,80,true,true,false,Steps:4096);
    var work=Enumerable.Range(0,4).Select(_=>runtime.CalculateFuturesOptionAsync(input,2,TimeSpan.FromMinutes(2),cancel.Token)).ToArray();
    cancel.Cancel();await runtime.DisposeAsync();foreach(var task in work){try{await task;}catch(OperationCanceledException){}}
}
Console.WriteLine($"Managed/native pricing: {compared} exact C# outcomes, zero-budget rejection, 1000 cancellation/disposal cycles passed.");
