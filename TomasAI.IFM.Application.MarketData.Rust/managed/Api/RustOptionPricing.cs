using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;
using TomasAI.IFM.Shared.OptionPricing;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>
/// Adapts the existing application pricing contract to the native calculator.
/// Application reference/calendar validation and semantic identities are retained;
/// no old calculator, provider driver, worker process or native DLL is invoked.
/// This service does not itself constitute the complete IMarketDataApi facade.
/// </summary>
public sealed class RustOptionPricing(NativeMarketDataRuntime runtime)
{
    /// <summary>Produces the current application value/failure and exact semantic
    /// digest using asynchronous Rust IV/Greek execution. The original remaining
    /// budget also covers application qualification and is never renewed.</summary>
    public async Task<OptionPricingPassResult> CalculateAsync(OptionPricingContext context,OptionPricingQuote underlying,
        OptionPricingQuote option,decimal strike,bool isCall,DateTimeOffset at,TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(underlying);ArgumentNullException.ThrowIfNull(option);
        cancellationToken.ThrowIfCancellationRequested();
        if(remainingBudget<TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if(remainingBudget>TimeSpan.FromMinutes(2)) remainingBudget=TimeSpan.FromMinutes(2);
        var started=Stopwatch.GetTimestamp();
        var invalid=runtime.ValidateOptionInputs(context,underlying,option,strike,isCall,at);
        if(invalid is not null) return new(null,invalid);
        var contract=context.Contract;
        var t=OptionPricingQualification.YearFraction(contract,at);
        var forward=(double)(underlying.Bid/2m+underlying.Ask/2m);
        var mark=(double)(option.Bid/2m+option.Ask/2m);
        var request=Black76PricingModel.CreateRequest(contract,forward,strike,isCall,t,context.Rate.AnnualContinuousRate);
        var budget=remainingBudget-Stopwatch.GetElapsedTime(started);
        if(budget<=TimeSpan.Zero) throw new TimeoutException("Native pricing original deadline expired during qualification.");
        var input=new FuturesPricingInput(request.UnderlyingPrice,request.Strike,request.TimeToExpiry,request.Rate,mark,isCall,
            request.Exercise==ExerciseKind.American,request.Premium==PremiumKind.FuturesStyle,
            context.PricerVersion=="Black76.Rust/v1");
        var result=await runtime.CalculateFuturesOptionAsync(input,2,budget,cancellationToken).ConfigureAwait(false);
        if(result.Failure!=0) {
            var failure=result.Failure switch{1=>"InvalidInput",2=>"Expired",3=>"UndefinedGreeks",4=>"PriceOutOfBounds",5=>"ImpliedVolatilityNotIdentifiable",6=>"VolatilityNotBracketed",7=>"NonConvergence",8=>"NumericalFailure",_=>throw new InvalidOperationException($"Native pricing lifecycle/resource failure {result.Failure}.")};
            return new(null,new("GreeksCalculationFailed",failure,contract.ContractId,"Option pricing inputs do not satisfy the qualified context."));
        }
        var digest=PricingSemanticHash.Compute(new{
            Version=2,context,underlying,option,strike,isCall,at,t,
            EngineVersion=context.PricerVersion,NumericalPolicy=new PricingSettings(),PolicyVersion=PricingSettings.Version
        });
        return new(new(result.Volatility,result.Delta,result.Gamma,result.Theta,result.Vega,result.Rho,result.Price,t,digest),null);
    }
}
