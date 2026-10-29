using System.Text.Json;
using TomasAI.IFM.Framework.OptionPricer.Pricing;
using TomasAI.IFM.Shared.OptionPricing;
if(args.Length>1) {
    AppContext.SetData("TomasAI.IFM.OptionPricer.Implementation","Rust");
    var nativePath=Path.Combine(AppContext.BaseDirectory,"runtimes","win-x64","native","ifm_option_pricer_native.dll");
    Directory.CreateDirectory(Path.GetDirectoryName(nativePath)!);
    File.Copy(Path.GetFullPath(args[1]),nativePath,overwrite:true);
}
var cases=new List<object>();
ulong Bits(double x)=>BitConverter.DoubleToUInt64Bits(x);
ulong[]? Values(PricingResult r)=>r.Success?new[]{r.Value!.Value.Price,r.Value.Value.Volatility,r.Value.Value.Delta,r.Value.Value.Gamma,r.Value.Value.Vega,r.Value.Value.Theta,r.Value.Value.Rho}.Select(Bits).ToArray():null;
foreach(var steps in new[]{64,801})foreach(var american in new[]{false,true})foreach(var futuresStyle in new[]{false,true})foreach(var call in new[]{false,true})foreach(var forward in steps==801?new[]{6500.0}:new[]{6400.0,6500,6600})foreach(var t in steps==801?new[]{.01}:new[]{1.0/365,30.0/365})foreach(var sigma in steps==801?new[]{.2}:new[]{0.0,.1,.3}) {
    var calculator=new OptionCalculator(new PricingSettings{Steps=steps});
    var request=new OptionPricingRequest(UnderlyingKind.Futures,american?ExerciseKind.American:ExerciseKind.European,futuresStyle?PremiumKind.FuturesStyle:PremiumKind.PaidUpfront,call?OptionSide.Call:OptionSide.Put,forward,6500,t,.04);
    var theoretical=calculator.TheoreticalPrice(request,sigma);var priced=calculator.Price(request,sigma);var market=theoretical.Price??0;
    var iv=calculator.ImpliedVolatility(request,market);
    var delta=calculator.PriceAndDelta(request,sigma);var solve=calculator.SolveImpliedVolatility(request,market);
    cases.Add(new{steps,american,futuresStyle,call,forward=Bits(forward),strike=Bits(6500),time=Bits(t),rate=Bits(.04),sigma=Bits(sigma),market=Bits(market),theoreticalFailure=theoretical.Failure.ToString(),theoretical=theoretical.Price is {} p?(ulong?)Bits(p):null,priceFailure=priced.Failure.ToString(),price=Values(priced),ivFailure=iv.Failure.ToString(),iv=Values(iv),engine=priced.EngineVersion,
        deltaFailure=delta.Failure.ToString(),delta=delta.Success?new[]{Bits(delta.Value!.Value.Price),Bits(delta.Value.Value.Delta)}:null,
        solveFailure=solve.Failure.ToString(),solve=solve.Success?new[]{Bits(solve.Value!.Value.Volatility),Bits(solve.Value.Value.RepricedPrice),Bits(solve.Value.Value.Residual),(ulong)solve.Value.Value.Iterations}:null});
}
File.WriteAllText(args[0],JsonSerializer.Serialize(cases));Console.WriteLine($"Unchanged unified C# futures pricer: {cases.Count} model cases, exact IEEE bits.");
