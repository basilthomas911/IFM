# Market Data Resiliency Stage 4 Pricing Specification v1.0

| Item | Value |
| --- | --- |
| Specification ID | `MDR-S4-PRICE` |
| Date | 2026-09-05 |
| Status | Owner pricing requirements recorded; implementation design; not implemented/qualified by this document |
| Gates | `S4G-00`, `S4G-04`, `S4G-08` |
| Parent | [Stage 4 implementation plan](Market-Data-Resiliency-Stage-4-Implementation-Plan-v1.0.md) |
| Consumer | [Order Composition selection specification](Order-Composition-Strategy-Selection-Specification-v1.0.md) |

## 1. Owner decisions and limits of approval

The owner specified these requirements on 2026-09-05:

- Select one Treasury tenor from remaining **trading days**, without interpolation.
- Add continuously compounded annual decimal conversion to the Treasury curve interface.
- Use Toronto/New York time and contract-specific day-count conventions.
- Use daily Treasury data from Financial Modeling Prep (FMP); intraday rates are unnecessary.
- Missing inputs or calculation failure return **Failed with error information**, never invented values.

This resolves the requested source, tenor-selection and output-rate direction. Engineering details
below make the rules testable. They do not approve live quote thresholds, arbitrary contract
metadata, strategy risk parameters or production enablement. Daily outright futures do not depend
on Treasury curves, option IV or option Greeks.

## 2. Tenor selection: exact boundaries

Validate that valuation precedes the exact option expiry before selecting a tenor.

| Remaining trading days `N` | Required point |
| --- | --- |
| `0 <= N < 30` | `TreasuryTenor.OneMonth` |
| `30 <= N < 60` | `TreasuryTenor.TwoMonth` |
| `60 <= N < 90` | `TreasuryTenor.ThreeMonth` |
| `N >= 90` | `Failed: TreasuryHorizonUnsupported` |
| Negative or inconsistent count | `Failed: InvalidTradingDayCount` |

At 30 days use two months; at 60 use three months; at 90 fail. Do not interpolate, use a longer
point, use calendar-day buckets, or substitute another tenor if the required point is absent.
An unexpired same-session contract may have `N = 0`; that is not permission to price an expired one.

Proposed counting convention `ExchangeTradingDatesExclusiveStartInclusiveExpiry/v1`: resolve the
valuation's exchange value date and the option's expiry trading date using the versioned product
calendar, then count trading dates in `(valuationValueDate, expiryTradingDate]`. An early-close
trading date counts once; a fully closed date does not count. Overnight sessions use their exchange
value date, not the UTC date. Record the two dates, count and calendar version in the context.
This endpoint convention is a design choice, not an assertion that the owner specified it.

The trading-day count selects a rate point only. It is **not** the pricing year fraction and must
not silently become `N / 252`, `N / 365`, or a guessed calendar duration.

## 3. Rate units and continuous conversion

### Verified source distinction

`TreasuryRatePoint.RatePercent` currently stores percentage points; `DecimalRate` only divides by
100. The current `ITreasuryCurve` exposes `GetLatestAsync` and `GetRangeAsync`, not a continuous-rate
function. FMP's official endpoint documents latest/historical Treasury data, but the public page
does not specify a compounding convention. The adapter must establish which series its fields
represent before assigning convention metadata. [FMP Treasury Rates API](https://site.financialmodelingprep.com/developer/docs/stable/treasury-rates).

Treasury describes CMT par yields as semiannual bond-equivalent quotations, not effective annual
yields, and gives effective annual yield as `(1 + y/2)^2 - 1`. This supports the conversion below
**when the FMP field has been verified as that CMT series**. Do not apply it to Treasury bill bank
discount rates or a differently defined investment-yield series. [Treasury interest-rate FAQ](https://home.treasury.gov/policy-issues/financing-the-government/interest-rate-statistics/interest-rates-frequently-asked-questions).

### Conversion contract

For verified convention `UsTreasuryCmtNominalSemiannual/v1`, with percent input `P`:

```text
y    = P / 100
r_cc = 2 * ln(1 + y / 2)
DF(T) = exp(-r_cc * T) = (1 + y / 2)^(-2 * T)
```

The continuous formula is derived from Treasury's quoted-yield convention. For a synthetic 5%
input, `y = 0.05` and `r_cc = 0.0493852251807428`, not `5`, `0.05`, or `ln(1.05)`.
Use numerically stable log-one-plus evaluation, validate the logarithm domain and finite output,
and retain full calculation precision. Display rounding must not change the calculation rate.
A genuinely published zero rate is valid; an absent rate replaced by zero is not. Negative rates
are not clamped by the converter; validate them against the declared source/conversion domain.

V1 uses the selected converted **par-yield proxy as a flat pricing rate** over the option's
contract-specific year fraction. Compounding conversion does not bootstrap a zero curve or claim
an exact Treasury discount factor for every maturity. No interpolation or bootstrapping is in
scope. Record `FlatSelectedCmtProxy/v1` as the modeling policy so this approximation is auditable.

Add an interface function, proposed signature:

```csharp
TreasuryContinuousRateResult GetContinuouslyCompoundedAnnualRate(
    TreasuryCurveSnapshot snapshot,
    TreasuryTenor tenor,
    TreasuryRateConversionPolicy policy);
```

This is a pure calculation over an already fetched immutable snapshot: no network request per
leg, quote or tick. Define the result/policy alongside the existing reference-data contracts.
Result is `Succeeded(value, provenance)` or `Failed(error)`; no nullable successful rate. Preserve
existing fetch APIs and percentage fields; do not change `DecimalRate` semantics underneath old
callers. Adapt implementations/test doubles explicitly; do not silently default to a rate.

Success records selected tenor, original percentage, convention and conversion-policy versions,
continuous annual decimal, curve value date, source, source-series ID and canonical curve digest.
Unknown convention returns `RateConventionUnsupported`. Before production wiring, pin dated FMP
fixtures against matching official 1/2/3-month CMT observations and obtain sufficient provider
series evidence; matching numbers alone do not prove the convention. Tests must not need API keys.

## 4. Valuation, expiry, calendars and day count

- Capture one `ValuationAtUtc` from the host clock for a pricing pass. Store instants in UTC;
  present Eastern time with a timezone ID and offset. Never treat Eastern as fixed UTC-5.
- Use `America/New_York` for market logic; Toronto display may use `America/Toronto`. Windows
  resolution may use `Eastern Standard Time`. Test DST transitions on Windows and Linux.
- Resolve exact option last-trading/expiry instant, exercise style, settlement behavior, product
  family, underlying canonical futures contract, multiplier and tick rules from versioned
  contract definitions. Date-only maturity or symbol-string inference is insufficient.
- Resolve `DayCountConventionId`, version and fractional-day treatment from the contract's
  approved pricing metadata. Where the exchange does not publish a pricing day-count convention,
  a reviewed product-specific mapping supplies it; do not claim it came from the feed.
- Calculate positive finite `T = YearFraction(valuation, expiry, convention)` using that mapping.
  An Actual/365 Fixed profile includes intraday elapsed time; other conventions require their
  own defined algorithms and tests. No global fallback to Actual/365 or business/252.
- Expired contracts, missing definitions, ambiguous local times, unknown calendar coverage and
  unsupported conventions fail explicitly. Do not normalize a bad expiry to midnight or Friday.

The existing `CmeFuturesMarketSessionCalendar` supports configured holidays/early closes but an
empty configuration is not verified production calendar coverage. Reuse its boundary where
suitable; supply and version complete product-calendar data. Treasury publication uses a separate
calendar from CME trading. Toronto holidays alone do not determine either calendar.

The current `Black76.OptionCalculator` accepts `DateOnly` and computes days/365. Add an explicit
year-fraction path and carry it through managed and native pricing, keeping legacy behavior
compatible. Do not round intraday expiry away before invoking the calculator. Black-76's European
exercise assumption must match the selected product; American-style options return
`PricingModelUnsupported` until an appropriate model is separately qualified.

CME distinguishes European weekly/EOM ES options from American-style ES options, and maps option
underlyings by the relevant expiry. Therefore monthly workflow horizon must not be used as an
exercise-style or underlying-contract shortcut. [CME weekly/EOM FAQ](https://www.cmegroup.com/trading/equity-index/weekly-eom-options-faq.html).

## 5. Daily Treasury freshness and quote freshness

FMP is the runtime source. Treasury's website is specification/fixture evidence, not an automatic
fallback provider. Capture a current daily curve once and share it across pricing contexts;
bounded refresh can retry a missing daily observation without polling FMP on each tick.

Define a versioned `TreasuryPublicationPolicy` with calendar, expected publication deadline,
FMP availability allowance, retry/backoff and maximum wait. The FMP allowance is operational
configuration requiring verification, not an assumed Treasury/provider SLA.

```text
requiredValueDate = latest publication date whose configured availability deadline has passed
accept only a validated curve observable by valuation, with ValueDate >= requiredValueDate
```

Before today's deadline, yesterday's expected published observation remains usable. On weekends
and non-publication holidays, the last expected published rate remains usable. An earlier validated
arrival of today's rate may be used immediately. After the deadline, a missing expected observation
fails `TreasuryStale`; an FMP outage may use the cache only while it still qualifies. Never treat
fresh retrieval of an old observation as fresh data. No future-dated or not-yet-observed curve may
be used. Corrections create a new digest/context even when value date is unchanged.

The adapter's existing 14-day latest-curve search is a retrieval window, **not** approved pricing
freshness. This design requires daily-publication freshness, not arbitrary 14-day acceptance.

Option and underlying quotes have independent seconds-based freshness. Carry forward `D4-05` as
**offline defaults only**: age <=5 seconds, inter-leg skew <=2 seconds, bounded qualification wait
<=10 seconds. Use source event times plus bounded clock-skew checks, not just recent receipt of an
old tick. Treasury publication age is never compared to five seconds. Quote limits need live
provider qualification/owner approval independently of the daily Treasury decision.

## 6. Coherent context and failed results

`IOptionPricingContextProvider` belongs in Application.MarketData; conversion and calculator
contracts remain provider-neutral. The host resolves calendars, definitions and a validated
Treasury snapshot before creating a new option session; workers receive immutable serialized
pricing inputs, not permission to query Trade/Portfolio or fetch curves on the quote path.

Every pricing pass binds valuation, expiry, T, rate/curve digest, calendar/definition/convention
versions, model version, one underlying quote and dataset/host generation. Final selected-leg
Greeks use that same context and a bounded, qualified set of leg quotes. Revalidate time validity
at completion. A reset, new underlying/curve context or expired input invalidates earlier readiness;
never mix old-generation Greeks with new-generation prices. Daily curve refresh changes pricing
context, not canonical subscription ownership and not an instruction to restart the dataset.

For IV inversion, the proposed mark is the midpoint of a valid two-sided non-crossed quote,
explicitly labeled a valuation mark. Enforce no-arbitrage bounds and solver success. Bid/ask
execution estimates are separate; midpoint/Greeks are not a promised fill or probability of profit.
State units explicitly: IV annual decimal; delta per underlying point; gamma per point squared;
theta per year, vega per 1.00 volatility, rho per 1.00 rate in the current calculator. Convert units
only through named output adapters, then apply signed ratios and contract multipliers once.

Structured errors contain stable code, safe message, retryability, missing/invalid input names,
contract/context identity, source value date, observed/allowed age where relevant and correlation
ID. Suggested codes include `TreasuryUnavailable`, `TreasuryTenorMissing`, `TreasuryStale`,
`TreasuryHorizonUnsupported`, `RateConventionUnsupported`, `ContractMetadataUnavailable`,
`DayCountUnsupported`, `ExpiredContract`, `InvalidQuote`, `StaleData`, `Recovering`,
`PricingModelUnsupported` and `GreeksCalculationFailed`.

Map pricing errors to a workflow **Failed** terminal outcome, not a successful empty candidate.
Existing `PricingUnavailable` readiness may remain the market-data transport category, with the
typed cause attached. Failed values are absent, never success-shaped zero Greeks. Retain existing
position/order subscriptions and clearly marked non-ready monitoring data; do not cancel or close.

## 7. Implementation packages and acceptance tests

| Package | Required evidence before the relevant gate passes |
| --- | --- |
| P1: interface and converter | Percent normalization once; verified semiannual conversion; 0/negative/domain failure; missing tenor/source; discount-factor identity; immutable provenance; legacy fetch API compatibility |
| P2: tenor/calendar/time | Counts 0/29/30/59/60/89/90; negative count; same-day before/at/after expiry; overnight value dates; weekends/full holidays/early closes; DST and leap-year conventions; missing metadata and calendar coverage |
| P3: publication cache | Before/after deadline; provider delay; weekend/holiday reuse; stale value date with fresh retrieval; correction digest; outage/cache qualification; no look-ahead; no per-tick HTTP |
| P4: explicit T calculator | Managed/native parity with approved references for each enabled convention; existing DateOnly regression; fractional expiry; unsupported exercise style; no-arbitrage and solver failures; Greek-unit checks |
| P5: coherent pricing wiring | All selected legs share context; reset mid-pass; rate update mid-pass; aged queued quote; clock skew; no resources on invalid prerequisites; stale failure retains durable monitoring |
| P6: independent futures path | Outright futures succeeds with qualified futures inputs while Treasury/option solver is unavailable; no option dependency acquired |

All packages need unit and integration evidence using deterministic offline fixtures before
live qualification. Extend the Stage 4 runners once implemented. Documentation checks do not pass
`S4G-04`; the current production option-chain guard remains until actual wiring is qualified.

Remaining activation inputs: verified FMP series/convention metadata, publication deadline and
allowance, product-specific expiry/calendar/day-count mappings, approved quote thresholds and
provider/native/live evidence. These are not a request to reconsider the owner's tenor/source rules.
