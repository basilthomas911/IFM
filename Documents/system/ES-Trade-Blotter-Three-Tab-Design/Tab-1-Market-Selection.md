# Tab 1 — Market Selection

## Purpose

Displays the current ES future or futures-option chain and lets an eligible manual operator stage contracts. Clicking bid or ask stages a leg; it never submits. Automated and historical Trades show their persisted market snapshot rather than reconstructed current quotes.

## Mockup

```text
┌──────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [ Market Selection ] [ Leg Staging (4) ] [ Orders and Fills ]       Strategy: [ Iron Condor      ▼ ] │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ ESZ26  LAST 5,420.50  CHG +14.25 (+0.26%)  IV 16.4%  IV RANK 32  QUOTE AGE 120 ms                  │
│ EXPIRY [16 Oct 26 ▼]  DTE 28  MULTIPLIER $50  TICK 0.25  SETTLEMENT AM                              │
│ EXPECTED MOVE ±45.00  PRESET [16 Delta Condor ▼]  WING [50 ▼]  LIQUIDITY [Qualified ▼]             │
├───────────────────────────────────────────────────┬────────┬───────────────────────────────────────────┤
│ CALLS: Role  Vol   OI  Delta   Bid    Ask         │ STRIKE │ PUTS: Bid   Ask  Delta   OI   Vol  Role  │
├───────────────────────────────────────────────────┼────────┼───────────────────────────────────────────┤
│ +LC    2.1K 6.1K   .10  11.00  11.50             │ 5600   │ 69.00 70.50  -.90  420    88             │
│ −SC    5.6K 12.4K  .16  19.50  20.00             │ 5550   │ 41.00 42.50  -.84  1.4K  510             │
├───────────────────────────────────────────────────┴────────┴───────────────────────────────────────────┤
│ UPPER EXPECTED MOVE 5,465.50  |  LAST UNDERLYING 5,420.50  |  LOWER EXPECTED MOVE 5,375.50          │
├───────────────────────────────────────────────────┬────────┬───────────────────────────────────────────┤
│       4.2K 8.9K   .84 124.50 126.00              │ 5300   │ 22.00 22.50  -.16 19.5K 11.2K  −SP      │
│       1.1K 4.0K   .90 159.00 161.00              │ 5250   │ 14.25 14.75  -.10 12.1K  6.4K  +LP      │
└──────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

## Information

- Underlying, last price, change, IV and IV rank/percentile
- Expiration, DTE, multiplier, tick, settlement and exercise style
- Expected move, quote age and liquidity quality
- Bid, ask, volume, open interest and delta
- Underlying/expected-move markers, role tags and staged-leg count

| Strategy | Selection |
| --- | --- |
| Iron Condor | Long put, short put, short call, long call |
| Vertical Spread | Two compatible option legs |
| Futures Outright | One futures contract |

No submit, cancel, replace, EOD, or lifecycle command belongs on this tab.
