# ES option-chain IV window

The live evaluated chain estimates how many cached strikes to subscribe to for the selected expiry. It is a **ballpark coverage rule**, not an exact 5-delta strike finder. Each selected option's actual delta is still calculated from its live quote.

## Formula

For each side of the current ES futures price:

```text
timeYears = seconds from evaluation time to actual option expiry / (365 × 86,400)
oneSidePoints = F × ATM_IV × sqrt(timeYears) × Z × (1 + buffer)
lowerEstimate = F - oneSidePoints
upperEstimate = F + oneSidePoints
```

- `F` is the current underlying ES futures price.
- `ATM_IV` is a recent, annualized near-at-the-money IV for **the selected expiry**, expressed as a decimal.
- `Z` defaults to `1.65`, an approximate 5-delta outer boundary. It is not an exact delta conversion; skew and the option pricing model can change actual deltas.
- `buffer` defaults to `0.15` (15%).
- The actual expiry instant, rather than integer days-to-expiry, is used so same-day expiries retain fractional time.

For example, `F = 7,822`, `ATM_IV = 0.16`, seven days to expiry, and `Z = 1.65` give about `285.97` points per side before padding. With 15% padding, the estimates are approximately `7,493` and `8,151`.

## Data flow and limits

1. Cached ScyllaDB option definitions supply the available strikes; live prices and IV do not come from that cache.
2. The existing value-date Bollinger window seeds a small live chain. Once a qualified near-ATM option quote produces IV, the following snapshots use the IV-derived window. A missing or stale IV leaves the method explicitly labelled `Bollinger2.5Sigma`.
3. The estimate is intersected with available cached strikes. **Every strike inside the IV window is selected**; there is no 80-strike truncation. Required staged-leg contracts remain included even if outside the estimate.
4. When IV is unavailable, the existing value-date 2.5-sigma Bollinger window selects every cached strike inside its bounds. If neither IV nor Bollinger inputs are available, the query reports an unavailable window instead of silently picking arbitrary nearest strikes.
5. Cached window inputs refresh every 30 seconds. Qualified IV observations must come from within 0.5% of the futures price and update the window at most once every 30 seconds; the last qualified IV expires after five minutes. This limits subscription churn while allowing price and IV updates. The worker still has a 2,048-contract operational capacity guard; exceeding it is an explicit failure, not selection truncation.

The reference-price and IV freshness gates remain separate from this approximate strike-selection rule. A live quote can be absent for an included strike without invalidating the rest of the chain.
