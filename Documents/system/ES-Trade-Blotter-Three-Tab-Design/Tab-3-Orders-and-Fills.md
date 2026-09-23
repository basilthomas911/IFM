# Tab 3 — Orders and Fills

## Purpose

Monitors submitted execution. A parent row represents the strategy or outright order and expandable child rows represent its legs. Broker-confirmed state is authoritative. This is the primary operational tab after submission.

## Mockup

```text
┌──────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [ Market Selection ] [ Leg Staging (4) ] [ Orders and Fills ]       Strategy: [ Iron Condor      ▼ ] │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ EXECUTION LOG                 [✓] Working only  [ ] Today only                  [ Refresh / resync ] │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Order / Contract       │Side│Req│Fill│Rem│Type│ Limit/Mid │ Status          │ Updated │
├────────────────────────┼────┼───┼────┼───┼────┼───────────┼─────────────────┼─────────┤
│ ▼ ORD-20261016-042     │CRDT│ 5 │ 2  │ 3 │LMT │16.50/16.25│PARTIALLY FILLED │10:16:03 │
│   ├─ ESZ26 P5250 Long  │BUY │ 5 │ 2  │ 3 │    │           │PARTIALLY FILLED │10:16:03 │
│   ├─ ESZ26 P5300 Short │SELL│ 5 │ 2  │ 3 │    │           │PARTIALLY FILLED │10:16:03 │
│   ├─ ESZ26 C5550 Short │SELL│ 5 │ 2  │ 3 │    │           │PARTIALLY FILLED │10:16:03 │
│   └─ ESZ26 C5600 Long  │BUY │ 5 │ 2  │ 3 │    │           │PARTIALLY FILLED │10:16:03 │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ SELECTED ORD-20261016-042  Portfolio P-01  Fund F-04  Fund Order 16001  Trade T-02                  │
│ Avg fill 16.40  Fees $12.50  Posting: Pending completion  Broker ID BRK-88214                        │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Current limit 16.50  New limit [16.25]  Mid 16.25   Requested 5  Filled 2  Remaining 3              │
│ [ Cancel unfilled quantity ]                    [ Submit cancel / replace ]                           │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ [ End-of-day processing ] [ Permitted Trade / position transition ]                                  │
└──────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

## Information

- Parent/child identity and Portfolio/Fund/Fund Order/Trade context
- Strategy, opening/closing intent, requested/filled/remaining quantities
- Order type, limit, midpoint, statuses and average fills
- Broker/exchange IDs, timestamps, fees, commissions
- Cancel/replace lineage and posting status

States include `Staged`, `Sent`, `Working`, `PartiallyFilled`, `Filled`, `CancelPending`, `Cancelled`, `ReplacePending`, and `Rejected`.

## Commands

- Cancel unfilled quantity
- Submit cancel/replace
- Refresh/resynchronize execution evidence
- Permitted EOD and Trade/position lifecycle transitions

API acceptance is not terminal confirmation. Pending state remains until broker acknowledgement or rejection; partial fills constrain replacements. The tab displays posting status but does not post ledger entries.
