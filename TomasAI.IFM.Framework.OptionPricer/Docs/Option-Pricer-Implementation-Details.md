# Option Pricer Implementation Details

## Purpose

`TomasAI.IFM.Framework.OptionPricer` provides the in-process quantitative implementation used by the Option Pricer domain. Its current implementation is a managed Black-76 model for European options on futures, batch pricing and Greeks, implied-volatility inversion, credit-spread assembly, and a MAD-based iron-condor loss-risk estimate.

The project targets .NET 10, enables nullable reference types, implicit usings, and unsafe blocks, and references Shared, Domain OptionPricer Shared, and Domain Trade Shared.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Framework.OptionPricer/`. Each leaf path includes all intermediate parent folders.

```text
Black76/
Docs/
bin/Debug/net10.0/
bin/Release/net10.0/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
obj/Release/net10.0/ref/
obj/Release/net10.0/refint/
```

- `Black76/` contains every source implementation: `OptionModel.cs`, `OptionSpreadPricer.cs`, and `LossProbability.cs`.
- `Docs/` contains this implementation record.
- `bin/Debug/net10.0/` and `bin/Release/net10.0/` contain generated assemblies and dependency outputs.
- `obj/Debug/net10.0/` and `obj/Release/net10.0/` contain generated build state; `ref/` and `refint/` contain reference and intermediate reference assemblies.
- The project root contains `TomasAI.IFM.Framework.OptionPricer.csproj`.

## Black-76 value types

`OptionModel.cs` defines immutable record structs used to describe pricing inputs and outputs:

- `Black76Result` contains Price, Delta, Gamma, Vega, Theta, and Rho.
- `Black76PriceParameter` groups one option's forward, strike, rate, volatility, expiry, and call/put indicator.
- `Black76ImpliedVolatilityParameter` additionally carries market price and solver settings.
- `Black76PriceBatchParameter` groups list-based batch inputs and result storage.

The public pricing methods currently accept primitive values and spans directly; the parameter record structs are passive carriers and are not overload arguments in this project.

Option type uses an integer convention throughout: values greater than zero are calls; zero or negative values are puts.

## Scalar price calculation

`OptionModel.Price` implements the Black-76 closed form with a continuously compounded discount factor:

1. Expired options (`timeToExpiry <= 0`) return intrinsic value.
2. Non-positive volatility returns discounted intrinsic value.
3. Otherwise it calculates `d1`, `d2`, and the call price.
4. Put price is derived from put-call parity.

The normal CDF uses a private complementary-error-function approximation based on Abramowitz-Stegun formula 7.1.26. Constants for `1/sqrt(2)` and `1/sqrt(2*pi)` are precomputed, and `Math.FusedMultiplyAdd` is used in hot arithmetic paths.

Unlike `PriceWithGreeks`, scalar `Price` does not explicitly reject non-positive forward or strike values before its logarithm. Callers must provide meaningful positive inputs.

## Price and Greeks

`OptionModel.PriceWithGreeks` returns theoretical value plus:

- Delta: discounted first derivative with respect to the forward.
- Gamma: discounted curvature with respect to the forward.
- Vega: sensitivity to annualized volatility.
- Theta: the implementation's time derivative convention.
- Rho: `-timeToExpiry * price` for discount-rate sensitivity.

Forward and strike must be positive. Expired options return intrinsic price, intrinsic delta, and zero remaining Greeks. Non-positive volatility returns discounted intrinsic price, zero delta/gamma/vega/theta, and price-based rho.

The module applies `SkipLocalsInit`, aggressive inlining, and aggressive optimization attributes to reduce hot-path overhead.

## Implied volatility

`OptionModel.ImpliedVolatility` solves for volatility using Newton-Raphson iterations:

1. Validate positive forward, strike, market price, and time to expiry.
2. Start from the supplied initial guess or 20%.
3. Compute price and vega using loop-invariant forward, strike, rate, and expiry values.
4. Return when absolute pricing error is below tolerance.
5. Divide by a vega floor of `1e-12` to avoid near-zero derivatives.
6. Clamp non-positive iterates to `1e-6`.
7. Return `double.NaN` when the iteration limit is exhausted.

Tolerance, maximum iteration count, and the optional initial guess are not independently range-validated. A non-positive iteration count returns `NaN`, and a problematic tolerance/guess can prevent convergence.

## Batch pricing

`PriceBatch` and `PriceWithGreeksBatch` accept read-only input spans and caller-provided output spans. Every input length must exactly equal the output length or an `ArgumentException` is thrown.

The loops use `MemoryMarshal.GetReference` and `Unsafe.Add` to avoid repeated span bounds checks, then delegate each element to the scalar implementation. Inputs and outputs are caller-owned; methods do not allocate result collections.

The design is optimized for throughput but does not vectorize multiple contracts or parallelize batches. Callers must not pass overlapping storage in a way that mutates unread inputs.

## Credit-spread and iron-condor pricing

`OptionSpreadPricer.PriceIronCondor` accepts separate put and call `CreditSpreadPricerArgs`, times the calculation, and returns one `OptionSpreadResult` for each side plus elapsed milliseconds.

For each credit spread it:

- Converts asset price to `double`.
- Converts days to maturity using `days / 365.0`.
- Prices the short and long legs independently with their own strike and implied volatility.
- Uses option type `-1` for puts and `1` for calls.
- Stores each theoretical price as a single-value path and marks both legs complete.

The returned result is a deterministic point valuation, not a Monte Carlo distribution. A separate private `PriceIronCondor` helper computes four option values and returns only aggregate price with all Greeks set to zero; it is currently unused and marked in source as placeholder behavior.

## Loss probability

`LossProbability` receives simulated put/call spread-value lists and a maximum-loss threshold.

`GetExpectedPnlValues` converts stored spread values into per-path P&L using quantity, contract multiplier, net premium, and option-specific sign conventions. `ToViewModel` combines supplied put and call P&L lists element by element, then returns a `LossProbabilityDataModel` with threshold fields currently set to zero.

The internal estimator:

1. Returns `1.0` when the minimum combined P&L reaches or breaches maximum loss.
2. Selects the middle sorted P&L value as the median.
3. Calculates the median absolute deviation.
4. Forms the lower-tail boundary `median - 3.5 * MAD`.
5. Returns the absolute boundary-to-maximum-loss ratio.

The result is called a probability but is not clamped to `[0, 1]`. Empty collections, unequal put/call list lengths, or a zero maximum-loss denominator are not validated and can throw or yield a non-finite value. The constructor also retains caller-provided mutable lists rather than copying them.

## Consumers and execution flow

`IronCondorSpreadDistributionJobService` in the Domain OptionPricer project calls `OptionSpreadPricer.PriceIronCondor`, then uses `LossProbability` with generated spread distributions. `Black76OptionPricer.TestApp` exercises scalar pricing, Greeks, implied volatility, batches, and edge cases as an executable demonstration.

The framework project itself has no dependency injection registration or mutable service state. `OptionModel` is static, while the other two classes are instantiated or invoked directly by the domain layer.

## Testing status

There is no dedicated `TomasAI.IFM.Framework.OptionPricer.*Tests` project. The test application provides manual/example verification, and domain tests may cover portions indirectly. Numerical regression tests should be added for known Black-76 values, put-call parity, finite-difference Greeks, implied-volatility round trips, span-length validation, expiry/zero-volatility behavior, and loss-probability edge cases.

## Safe extension points

When extending this framework:

1. Preserve the call/put and annualization conventions across all APIs.
2. Validate financial inputs consistently before logarithms or division.
3. Add immutable parameter overloads only if they reduce ambiguity without adding hot-path allocations.
4. Keep batch APIs allocation-free and document any aliasing requirements.
5. Return explicit convergence information if callers must distinguish solver failure modes.
6. Separate point spread valuation from simulated distribution generation.
7. Add automated numerical baselines before changing approximations or optimization code.
