# Qualification stopped: partial-close trade lifecycle blocker

Historical blocker report: the user subsequently authorized the [partial-close lifecycle fix](Event-Log-V2-Partial-Close-Lifecycle-Fix-2026-09-20.md). See that report for implementation and current qualification limits; the reproduction below describes the pre-fix behavior.

## Outcome

The request was to complete all remaining tests unless a blocker was found. A production-code lifecycle mismatch was verified, so full qualification is **not complete**. This turn adds only two characterization test cases and this report; it does not change production behavior, database schemas, or deployments.

`partial-close-lifecycle-blocker.trx` under `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/emulator-qualification/`: **31/31 TradeFlowStateMachineTests passed**. Two new cases reproduce the blocker for futures and futures-option trade handlers. Their success means the existing rejection was reproduced, not that partial-close support passed.

## Exact reproduction

1. Establish an open trade with two units per leg and matching opening fill evidence.
2. Run BeginClose, which succeeds and moves the trade to Closing.
3. Supply a closing execution for one unit per leg, half the open position.
4. CloseFuturesTrade and CloseOptionTrade both reject with `TRADE.INVALID_CLOSE_EVIDENCE`.
5. Trade remains Closing, its ClosingFills remain empty, and no closing state-change event is emitted.

Evidence:

- `TomasAI.IFM.Domain.Trade/Model/TradeCloseEvidence.cs`: IsExact requires each closing leg sum to equal the negative of the entire established leg quantity. It does not accumulate partial closes.
- `TomasAI.IFM.Domain.Trade/Futures/Command/CloseFuturesTrade.cs` and `Futures/Option/Command/CloseOptionTrade.cs`: both use IsExact and only implement final Closed transitions.
- `TomasAI.IFM.Domain.Trade/Order/Execution/Model/OrderExecutionActorStateMachine.cs`: accepted closing executions are handed off as PositionCloseExecution without a partial-versus-final lifecycle distinction.
- `TomasAI.IFM.Domain.Trade/Order/Execution/Command/EventProjector/OrderExecutionEventProjector.cs`: invokes PortfolioAccounting first, then BeginClose/CloseTrade, then full ClosePosition. This order means accounting can commit while a later trade-lifecycle step rejects. This consequence is established by code trace plus separate accounting integration and handler tests; a complete callback-driven reproduction was not run.

The existing accounting API integration tests deliberately call the accounting boundary directly and therefore do not establish end-to-end trade lifecycle support. Their prior passing counts remain valid within that narrower scope.

## Required implementation before continuing end-to-end acceptance

- Represent cumulative accepted closing fills and remaining actual filled quantity per position leg.
- Apply an idempotent partial-close transition that retains the remaining open position rather than invoking final closure.
- Finalize trade/position closure only when all actual opening quantity has been offset; do not infer opening quantity from unfilled requested quantity.
- Update execution handoff, trade state, position valuation/risk quantities and read models consistently, preserving recovery when accounting has already committed.
- Test sequential and cancelled partial closing attempts, duplicate/out-of-order callbacks, short and multi-leg positions, subsequent final close, and restart between accounting and lifecycle updates.

No change to the two-trades-per-order policy or automatic hedging is implied. Broader migration/reconciliation, uneven-leg MTM evidence, production mappings and process-kill qualification remain outstanding as recorded in the preceding reports. Other suites were not run after this blocker was verified; this is not an all-tests-green or cutover approval.
