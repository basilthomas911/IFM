using TomasAI.IFM.Application.MarketData.Rust.Interop;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>Frozen futures-option inputs; reference qualification remains a prerequisite.</summary>
/// <param name="Forward">Underlying futures price.</param><param name="Strike">Strike.</param>
/// <param name="Time">Expiry year fraction.</param><param name="Rate">Continuous rate.</param>
/// <param name="Input">Volatility for actions 0/1/3, observed mark for actions 2/4.</param>
/// <param name="Call">Call option.</param><param name="American">American exercise.</param>
/// <param name="FuturesStyle">Futures-style premium.</param><param name="LegacyRustProfile">Existing Rust European arithmetic.</param>
/// <param name="Steps">American tree levels.</param><param name="Iterations">Maximum IV iterations.</param>
/// <param name="MaximumVolatility">IV upper bound.</param><param name="Tolerance">IV repricing tolerance.</param>
public sealed record FuturesPricingInput(double Forward,double Strike,double Time,double Rate,double Input,
    bool Call,bool American,bool FuturesStyle,bool LegacyRustProfile=false,uint Steps=801,uint Iterations=100,double MaximumVolatility=4,double Tolerance=1e-8);

/// <summary>Native numerical output in unchanged per-option, annual sensitivity units.</summary>
/// <param name="Failure">0 success; 1 invalid input, 2 expired, 3 undefined Greeks, 4 price bounds,
/// 5 IV unidentifiable, 6 volatility not bracketed, 7 nonconvergence, 8 numerical failure,
/// 9 interruption, 10 capacity. Runtime cancellation/expiry is delivered as a failed Task.</param>
/// <param name="Price">Theoretical price.</param><param name="Volatility">IV.</param><param name="Delta">Delta.</param>
/// <param name="Gamma">Gamma.</param><param name="Vega">Vega.</param><param name="Theta">Theta.</param><param name="Rho">Rho.</param>
public sealed record FuturesPricingResult(uint Failure,double Price,double Volatility,double Delta,double Gamma,double Vega,double Theta,double Rho);

public sealed partial class NativeMarketDataRuntime {
    /// <summary>Submits asynchronous theoretical (0), full Greek (1), IV/Greek (2),
    /// price/delta (3) or IV-only (4) work. Unrequested numerical fields are zero.
    /// calculation. Explicit remaining budget includes queue time and is never renewed.
    /// Completion is copied before native storage release; no UI/actor thread waits.</summary>
    public Task<FuturesPricingResult> CalculateFuturesOptionAsync(FuturesPricingInput input,uint action,TimeSpan budget,CancellationToken cancellationToken=default) {
        ArgumentNullException.ThrowIfNull(input);cancellationToken.ThrowIfCancellationRequested();
        if(action>4) throw new ArgumentOutOfRangeException(nameof(action));
        if(budget<TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(budget));
        var native=new NativePricingRequest{Size=80,Action=action,Flags=(input.Call?1u:0)|(input.American?2u:0)|(input.FuturesStyle?4u:0),Profile=input.LegacyRustProfile?1u:0,
            Forward=input.Forward,Strike=input.Strike,Time=input.Time,Rate=input.Rate,Input=input.Input,Steps=input.Steps,Iterations=input.Iterations,MaximumVolatility=input.MaximumVolatility,Tolerance=input.Tolerance};
        lock(gate) {
            ObjectDisposedException.ThrowIf(disposal is not null,this);
            if(deliveryFailure is not null) throw new InvalidOperationException("Native completion delivery failed.",deliveryFailure);
            var operation=checked(++nextOperation);var request=new Pending{Pricing=new(TaskCreationOptions.RunContinuationsAsynchronously)};
            pending.Add(operation,request);
            var status=library.SubmitPricing(handle.Identity,operation,in native,(ulong)Math.Ceiling(Math.Min(budget.TotalMilliseconds,120_000)));
            if(status!=0){pending.Remove(operation);NativeMarketDataLibrary.Check(status,"submit pricing");}
            request.Cancellation=cancellationToken.UnsafeRegister(_=>library.Cancel(handle.Identity,operation),null);
            return request.Pricing.Task;
        }
    }
}
