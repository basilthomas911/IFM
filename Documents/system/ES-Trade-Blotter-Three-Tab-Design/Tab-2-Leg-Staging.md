# Tab 2 — Leg Staging

## Purpose

Receives selections from Tab 1, presents the complete composition, calculates provisional values, validates it, and exposes manual submission only after authoritative checks pass. Automated and historical Trades show read-only composition and risk evidence.

## Mockup

```text
┌──────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [ Market Selection ] [ Leg Staging (4) ] [ Orders and Fills ]       Strategy: [ Iron Condor      ▼ ] │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ IRON CONDOR   ESZ26 @ 5,420.50   EXP 16 Oct 26 (28 DTE)   IV 16.4% (Rank 32)                        │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ STAGED LEGS 4 / 4                                                                                     │
│ Action │ Contract    │Type│Strike│ Expiry    │Role│ Bid  │ Ask  │ Mid  │Age │Qty│Ratio│
├────────┼─────────────┼────┼──────┼───────────┼────┼──────┼──────┼──────┼────┼───┼─────┤
│ BUY    │ ESZ26 P5250 │PUT │5250  │16 Oct 26  │+LP │14.25 │14.75 │14.50 │120 │ 1 │  1  │
│ SELL   │ ESZ26 P5300 │PUT │5300  │16 Oct 26  │−SP │22.00 │22.50 │22.25 │115 │ 1 │  1  │
│ SELL   │ ESZ26 C5550 │CALL│5550  │16 Oct 26  │−SC │19.50 │20.00 │19.75 │130 │ 1 │  1  │
│ BUY    │ ESZ26 C5600 │CALL│5600  │16 Oct 26  │+LC │11.00 │11.50 │11.25 │125 │ 1 │  1  │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Contracts [1]  Limit [16.50]  Type [Limit ▼] │ Credit 16.50  Max profit $825  Max loss $1,675       │
│ Opening/closing [Opening ▼]                   │ Δ -.02  Γ -.004  Vega -32.40  Theta +45.10           │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ ✓ Structure ✓ Compatibility ✓ Quotes ✓ Tick/sign ✓ Fund policy ✓ Broker ✓ Risk                     │
│ Risk: APPROVED   Revision 47   Reservation RSK-20261016-009                                           │
├──────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ [ Clear staged legs ]       [ Recalculate / validate ]                         [ Submit order ]      │
└──────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

## Minimum composition

| Strategy | Requirement |
| --- | --- |
| Iron Condor | Four unique, correctly ordered legs |
| Vertical Spread | Two compatible legs |
| Futures Outright | One valid future |

Submission requires valid roles, compatibility, strike order, quantities, fresh quotes, valid price conventions, Fund Order policy, broker qualification and Risk Manager authorization. UI calculations are provisional; Order Composer and Risk Manager remain authoritative.

## Commands

- Clear staged legs
- Recalculate or validate
- Submit manual opening or closing order

These replace the old outer Submit Order controls.
