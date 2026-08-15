# Rust Black-76 Native ABI Version 1

## Status and scope

This document freezes Stage 1 of the Rust Black-76 implementation. It defines the managed compatibility contract and
the native ABI implemented under `native.rust/OptionPricer.Rust` beginning in Stage 2. Stage 1 itself introduced no
Rust crate or native implementation.

Implementation status as of 2026-08-15: Stages 2 and 3 provide the Windows x64 `cdylib` scaffold, public C header,
scalar price kernel, scalar price-and-Greeks kernel, and their panic-safe version-1 exports. Implied volatility, fused
calculator processing, batches, managed P/Invoke integration, differential testing, and benchmarking remain later
stages. The managed implementation therefore remains the only application runtime path.

ABI version 1 covers the numerical kernel used by `OptionModel` and `OptionCalculator`:

- scalar Black-76 price;
- scalar Black-76 price and Greeks;
- scalar implied-volatility inversion;
- fused implied-volatility inversion and Greeks;
- price batches; and
- price-and-Greeks batches.

`OptionSpreadPricer`, `LossProbability`, domain view models, dates, option-right strings, collections, configuration,
and implementation selection remain managed. Existing callers continue to call the current public C# API without
source changes.

## Managed API compatibility boundary

The following signatures and value types remain public and unchanged:

```csharp
double OptionModel.Price(
    double forwardPrice,
    double strikePrice,
    double riskFreeRate,
    double volatility,
    double timeToExpiry,
    int optionType);

Black76Result OptionModel.PriceWithGreeks(
    double forwardPrice,
    double strikePrice,
    double riskFreeRate,
    double volatility,
    double timeToExpiry,
    int optionType);

double OptionModel.ImpliedVolatility(
    double forwardPrice,
    double strikePrice,
    double riskFreeRate,
    double marketPrice,
    double timeToExpiry,
    int optionType,
    double tolerance = 1e-10,
    int maxIterations = 100,
    double? initialGuess = null);

void OptionModel.PriceBatch(
    ReadOnlySpan<double> forwardPrices,
    ReadOnlySpan<double> strikePrices,
    ReadOnlySpan<double> riskFreeRates,
    ReadOnlySpan<double> volatilities,
    ReadOnlySpan<double> timesToExpiry,
    ReadOnlySpan<int> optionTypes,
    Span<double> results);

void OptionModel.PriceWithGreeksBatch(
    ReadOnlySpan<double> forwardPrices,
    ReadOnlySpan<double> strikePrices,
    ReadOnlySpan<double> riskFreeRates,
    ReadOnlySpan<double> volatilities,
    ReadOnlySpan<double> timesToExpiry,
    ReadOnlySpan<int> optionTypes,
    Span<Black76Result> results);

OptionGreeks OptionCalculator.GetOptionGreeks(
    string optionTypeName,
    double assetPrice,
    double strikePrice,
    double optionValue,
    double riskFreeRate);
```

`Black76Result` has sequential layout and exactly six binary64 fields in this order:

| Offset | Field | Size |
|---:|---|---:|
| 0 | `Price` | 8 |
| 8 | `Delta` | 8 |
| 16 | `Gamma` | 8 |
| 24 | `Vega` | 8 |
| 32 | `Theta` | 8 |
| 40 | `Rho` | 8 |

Its total size is 48 bytes with 8-byte alignment. `OptionGreeks` is not an ABI structure because its managed `bool`
and field order should not become a cross-language layout dependency.

## Frozen numerical behavior

The Rust implementation must port the current algorithm, constants, operation order, and branch behavior rather than
substituting another Black-76 or normal-distribution package.

### Common conventions

- All numeric values use IEEE-754 binary64.
- `optionType > 0` means call; `optionType <= 0` means put.
- Rates are continuously compounded annual rates.
- Volatility is an annualized decimal.
- Time to expiry is expressed in Actual/365 Fixed years by `OptionCalculator`.
- The normal CDF uses the existing Abramowitz-Stegun complementary-error-function approximation and coefficients.
- Fused multiply-add sites and evaluation order are part of the parity target.
- Small platform math differences are compared with explicit absolute/relative tolerances, not bitwise equality.

### `Price`

- Does not validate forward or strike before logarithm and division. The native implementation must preserve resulting
  IEEE-754 behavior rather than introduce a new exception.
- `timeToExpiry <= 0` returns undiscounted intrinsic value.
- Otherwise, `volatility <= 0` returns discounted intrinsic value.
- A call is calculated directly; a put is derived using put-call parity.

### `PriceWithGreeks`

- A non-positive forward or strike throws `ArgumentOutOfRangeException` at the managed boundary.
- `timeToExpiry <= 0` returns intrinsic price, discrete intrinsic delta, and zero for all other Greeks.
- At expiry, an exactly at-the-money option has zero delta.
- Otherwise, `volatility <= 0` returns discounted intrinsic price, zero delta/gamma/vega/theta, and
  `rho = -timeToExpiry * price`.
- The result field order is Price, Delta, Gamma, Vega, Theta, Rho.

### `ImpliedVolatility`

- A non-positive forward, strike, market price, or time to expiry throws `ArgumentOutOfRangeException` at the managed
  boundary.
- `initialGuess == null` selects `0.20`.
- Newton-Raphson uses the current price calculation, a vega floor of `1e-12`, and the caller-provided absolute price
  tolerance.
- A non-positive volatility iterate is replaced with `1e-6`.
- Exhausting `maxIterations`, including a non-positive iteration count, returns `double.NaN`.
- Tolerance, iteration count, and initial guess receive no additional range validation in ABI version 1.

### `OptionCalculator`

- Only the exact ordinal strings `CALL` and `PUT` are recognized.
- Non-finite or non-positive asset, strike, and option values, non-finite rate, or non-positive/non-finite expiry return
  `OptionGreeks.Failed`.
- The current discounted lower and upper market-price bounds remain managed validations.
- Implied volatility must be finite, positive, and no greater than `4.0`.
- All published Greeks must be finite.
- Expected invalid or unsolvable inputs return `OptionGreeks.Failed`; they do not throw.
- The managed wrapper uses one fused native call after validation to avoid two P/Invoke transitions.

### Batch operations

- Managed code validates that every input span length equals the output span length before entering native code.
- An empty batch succeeds without requiring non-null native pointers.
- `PriceBatch` has the same per-element behavior as scalar `Price`.
- `PriceWithGreeksBatch` stops at the first invalid forward or strike. Earlier results remain populated, the failing and
  later results remain untouched, and the managed wrapper throws `ArgumentOutOfRangeException`.
- Overlapping storage that could overwrite unread input is unsupported by the current managed contract. The wrapper may
  use the managed implementation when it detects aliasing rather than pass aliased pointers to Rust.
- The native implementation must not allocate, retain, or access a supplied buffer after the call returns.

## Native library and platform contract

- Logical library name: `ifm_option_pricer_native`.
- Stage 1 target: Windows x64 using the MSVC Rust target.
- Calling convention: C (`cdecl`).
- Integer widths are explicit; no C/Rust/managed `bool`, enum, `long`, `size_t`, string, date, nullable, or collection is
  passed across the boundary.
- All functions are thread-safe, reentrant, synchronous, and contain no shared mutable state.
- Native code must catch Rust panics. A panic must never unwind through P/Invoke.
- No native function may create worker threads or invoke managed callbacks.
- The DLL is loaded once from `runtimes/win-x64/native`; ABI compatibility is verified once before the first calculation.

## ABI types

The following C declarations are normative. Rust uses `#[repr(C)]`; managed code uses sequential-layout blittable
structures and source-generated `LibraryImport` declarations.

```c
#include <stdint.h>

#define IFM_OPTION_PRICER_ABI_V1 1u
#define IFM_OPTION_PRICER_NO_ERROR_INDEX UINT32_MAX

typedef int32_t ifm_option_pricer_status_v1;

enum {
    IFM_OPTION_PRICER_OK_V1 = 0,
    IFM_OPTION_PRICER_NULL_POINTER_V1 = 1,
    IFM_OPTION_PRICER_INVALID_ARGUMENT_V1 = 2,
    IFM_OPTION_PRICER_NO_CONVERGENCE_V1 = 3,
    IFM_OPTION_PRICER_PANIC_V1 = 4
};

typedef struct ifm_black76_result_v1 {
    double price;
    double delta;
    double gamma;
    double vega;
    double theta;
    double rho;
} ifm_black76_result_v1;

typedef struct ifm_black76_implied_greeks_result_v1 {
    double implied_volatility;
    double price;
    double delta;
    double gamma;
    double vega;
    double theta;
    double rho;
} ifm_black76_implied_greeks_result_v1;
```

The required layout assertions are:

| Type | Size | Alignment |
|---|---:|---:|
| `ifm_black76_result_v1` | 48 | 8 |
| `ifm_black76_implied_greeks_result_v1` | 56 | 8 |

Every ABI test suite must verify size, alignment, and every field offset on both the Rust and managed sides.

## ABI exports

All exports return a status except the ABI-version query. Scalar arguments are passed directly to avoid constructing an
input structure on every sub-microsecond call. Versioning is carried by the export suffix and the library ABI query.

```c
uint32_t ifm_option_pricer_get_abi_version(void);

ifm_option_pricer_status_v1 ifm_black76_price_v1(
    double forward_price,
    double strike_price,
    double risk_free_rate,
    double volatility,
    double time_to_expiry,
    int32_t option_type,
    double* result);

ifm_option_pricer_status_v1 ifm_black76_price_with_greeks_v1(
    double forward_price,
    double strike_price,
    double risk_free_rate,
    double volatility,
    double time_to_expiry,
    int32_t option_type,
    ifm_black76_result_v1* result);

ifm_option_pricer_status_v1 ifm_black76_implied_volatility_v1(
    double forward_price,
    double strike_price,
    double risk_free_rate,
    double market_price,
    double time_to_expiry,
    int32_t option_type,
    double tolerance,
    int32_t max_iterations,
    int32_t has_initial_guess,
    double initial_guess,
    double* result);

ifm_option_pricer_status_v1 ifm_black76_implied_volatility_with_greeks_v1(
    double forward_price,
    double strike_price,
    double risk_free_rate,
    double market_price,
    double time_to_expiry,
    int32_t option_type,
    double tolerance,
    int32_t max_iterations,
    int32_t has_initial_guess,
    double initial_guess,
    ifm_black76_implied_greeks_result_v1* result);
```

Batch exports retain the managed structure-of-arrays form so existing spans can be pinned and passed without packing or
copying:

```c
ifm_option_pricer_status_v1 ifm_black76_price_batch_v1(
    const double* forward_prices,
    const double* strike_prices,
    const double* risk_free_rates,
    const double* volatilities,
    const double* times_to_expiry,
    const int32_t* option_types,
    uint32_t count,
    double* results,
    uint32_t* error_index);

ifm_option_pricer_status_v1 ifm_black76_price_with_greeks_batch_v1(
    const double* forward_prices,
    const double* strike_prices,
    const double* risk_free_rates,
    const double* volatilities,
    const double* times_to_expiry,
    const int32_t* option_types,
    uint32_t count,
    ifm_black76_result_v1* results,
    uint32_t* error_index);
```

For batch success, `error_index` is set to `IFM_OPTION_PRICER_NO_ERROR_INDEX`. For a per-contract invalid argument it is
set to the zero-based failing index. Null pointers are invalid when `count > 0`; data pointers may be null when
`count == 0`, but a non-null `error_index` is always required.

## Status mapping

| Native status | Direct `OptionModel` mapping | Fused `OptionCalculator` mapping |
|---|---|---|
| `OK` | Return native result | Return finite successful result |
| `NULL_POINTER` | Throw `InvalidOperationException` because this indicates a wrapper defect | Throw `InvalidOperationException` |
| `INVALID_ARGUMENT` | Apply the existing method-specific `ArgumentOutOfRangeException` | `OptionGreeks.Failed` |
| `NO_CONVERGENCE` | Return `double.NaN` from implied volatility | `OptionGreeks.Failed` |
| `PANIC` | Throw `InvalidOperationException` | Throw `InvalidOperationException` |

Normal managed argument validation remains ahead of P/Invoke. Native validation is defense in depth and provides stable
behavior for direct ABI tests. Wrapper defects and native panics are infrastructure failures, not ordinary pricing
failures, and therefore are not hidden as `OptionGreeks.Failed`.

## P/Invoke performance rules

1. Use source-generated `LibraryImport`; do not use delegate invocation, reflection, or per-call library lookup.
2. Pass scalar primitives directly and return larger results through a caller-provided pointer.
3. Convert dates, option strings, nullable initial guesses, statuses, and exceptions in managed code.
4. Pin spans only for the duration of one batch call; never create persistent `GCHandle` instances.
5. Use the fused implied-volatility-and-Greeks export for `OptionCalculator`.
6. Do not allocate native vectors for caller-provided batches.
7. Do not enable internal Rust parallelism; application-level callers retain concurrency ownership.
8. Benchmark `SuppressGCTransition` separately. It is not permitted for iterative or unbounded batch calls without
   evidence that GC latency remains safe.
9. Benchmark managed and Rust implementations at batch sizes 1, 2, 4, 8, 16, 32, 64, 256, 1,024, and 16,384 before
   selecting a crossover threshold.
10. Resolve the selected implementation once. Do not read configuration or branch on strings in the hot path.

## Stage 1 verification gates

Stage 1 is complete when:

- current scalar, solver, calculator, and batch behavior is described and covered by behavior-freeze tests;
- `Black76Result` is explicitly sequential and its 48-byte field order is tested;
- ABI version, symbols, calling convention, statuses, structures, field offsets, and batch rules are frozen here;
- the managed implementation remains the only runtime implementation; and
- focused OptionPricer unit tests pass without creating the Rust crate.

Any change to this contract before production use updates the version-1 draft. After an ABI version ships, incompatible
changes require new versioned export names and a new ABI version.
