# Trade Order Execution-Driven Lifecycle Implementation Plan

**Date:** 2026-10-09  
**Status:** Completed and verified

## Objective

Remove the manual Close Order and Change State controls and their commands. Confirmed execution must drive trade, position and portfolio setup lifecycle updates. Closing fills must realize PnL through the existing financial accounting path.

Follow [Actor Event Modeling Conventions](Actor-Event-Modeling-Conventions.md) and [Actor Implementation Conventions](Actor-Implementation-Conventions.md).

## Verified starting point

- OrderExecutionEventProjector posts confirmed execution accounting, establishes created trades, closes accepted closed positions and completes the execution order.
- Closing execution sends BeginCloseOptionTradeCommand, CloseOptionTradeCommand and the strategy-specific position close command.
- BrokerExecutionAccountingModel calculates realized PnL from opening and closing signed settlement; commissions are separate expense postings.
- CloseManualFundOrderCommand only finalizes the portfolio setup order as Executed. It does not execute a closing trade.
- ChangeManualFundOrderTradeStateCommand has internal callers for submission, setup-to-execution identity linking and marking a trade Open, despite its disabled UI button.
- Execution completion does not currently finalize the corresponding setup order. These internal dependencies must be replaced before removing the commands.

## Intended lifecycle

Submission carries setup identities into backend execution. Backend acceptance records the execution link and submitted status. Successful opening establishment drives Open. Successful closing accounting and actual trade/position closure drive setup closing completion and setup order finalization. Committed Fund events project ScyllaDB read models and notify the UI.

Event listeners send concrete commands to the owning Fund CommandActor. They do not mutate its state directly. Command handlers emit source events with CommandId equal to the originating command CommandId and business-specific property names. Query actors read persisted ScyllaDB projections.

## Stage 1 — Define ownership and contracts

- Inventory every caller, service, actor registration and validator for both retiring commands.
- Define concrete backend operations to record accepted submission, established opening execution and completed closing execution. Do not expose arbitrary target-state mutation.
- Define the mapping between portfolio/fund/setup order/setup trade and execution order/execution trade identities.
- Specify rejected, cancelled and partial execution handling. Submission cannot mean Open, and remaining exposure cannot mean Closed.
- Preserve supported balanced partial-fill behavior without declaring a completely closed position prematurely.

**Complete when:** Every internal caller has an explicit replacement and each transition has execution evidence.

## Stage 2 — Move submission linking to the backend

- Carry setup identities through submission contracts and committed execution events before fills can arrive.
- Record accepted submission and the setup-to-execution link through the owning Fund CommandActor.
- Remove the UI post-submission generic state-change call.
- Make early completion and duplicate acceptance independent of the UI reply or selected screen row.

**Complete when:** A headless submission links the correct setup trade even when execution completes before the UI receives its reply.

## Stage 3 — Automate opening and closing setup transitions

- Route successful establishment and closing completion to the Fund CommandActor using concrete commands.
- Replace the UI callback that marks a trade Open.
- Finalize the compatible setup closing trade and setup order automatically after successful closing execution.
- Retain existing accounting and actual trade/position close commands. Financial execution outcomes do not use the disposable monitoring-snapshot drop policy.
- Use deterministic command identities and correlation to prevent duplicate effects and reject stale or unrelated outcomes.
- Log failures with execution and setup identities and the failed stage. Do not publish successful closure before its required effects succeed.

**Complete when:** Opening and closing work without the editor running, and duplicate completion cannot duplicate financial postings or terminal history.

## Stage 4 — Remove overrides and refresh the UI

- Remove Close Order and Change State buttons, designer fields, handlers and unused layout space.
- Remove their view-model, service/API methods, command handlers, validators, actor registrations and public command contracts after their internal callers are replaced.
- Remove unused mutation fields only where safe. Do not reuse published message keys or verbs; preserve source-event readers needed by existing streams.
- Retain actual execution close, cancellation and amendment capabilities.
- Refresh setup trade/order rows from projected lifecycle notifications.
- Preserve Load Trade eligibility and the prohibition on enabling live feed for closed trades.

**Complete when:** No runtime route or UI exposes manual lifecycle overrides, and automatic status updates reach the screen.

## Stage 5 — Verify lifecycle and failure cases

- Unit tests for submission identity linking, opening completion and closing setup finalization.
- Tests for rejection, cancellation, remaining partial exposure, early completion, duplicates, stale outcomes and downstream projection failures.
- Accounting tests for realized PnL, commissions and duplicate-delivery protection.
- Development emulator integration: create setup, submit and fill opening, verify Open and opening history; submit and fill opposite closing trade, verify Closed, final history and fund/portfolio accounting.
- Verify actual trade date and longest leg maturity are populated after establishment and remain null at manual entry.
- FlaUI walkthrough: both buttons absent; order/trade status refreshes automatically; Load Trade works after opening; closed live feed remains disabled.

**Complete when:** Lifecycle and accounting assertions pass, with correlated logs showing each handoff.

## Stage 6 — Build and record completion

- Build affected projects and the solution; fix errors introduced by the removal.
- Update trade lifecycle and UI documentation.
- Record automated and emulator/FlaUI results, limitations and contract restart requirements.
- Handle existing setup records with missing execution links separately; do not bulk change financial balances or trade status.

**Complete when:** Build succeeds and verification evidence is recorded.

## Acceptance criteria

Both buttons and active manual commands are gone. Backend execution evidence controls setup status without a UI connection. Confirmed closing fills realize PnL and close the actual position through existing financial paths. Setup completion and UI refresh follow successful outcomes. Duplicate/out-of-order messages do not cause false terminal status or duplicate financial effects. Existing persisted streams remain readable.

## Blockers

A blocker exists if execution cannot be reliably mapped to its setup trade, completion evidence cannot distinguish remaining exposure from complete closure, or retiring a contract requires an unresolved persisted-schema compatibility decision. Report the specific missing evidence and affected stage.


## Implementation record ? 2026-10-09

All six stages are complete. Unit, build, FlaUI control and isolated emulator lifecycle verification pass.

- Four concrete Fund execution-evidence commands replace arbitrary lifecycle overrides. New source events use the command CommandId; existing persisted source event readers remain supported.
- Setup identity travels through Portfolio candidate, accepted instruction and Trade Order contracts. Manual candidates are restricted to one owning Fund and one component.
- Acceptance is recorded before execution starts. Opening setup becomes Open only after successful establishment. Unfilled cancellation/rejection releases the submission binding. Complete opposite closing fills realize PnL, close the actual position and finalize setup; partial closure retains remaining exposure.
- The desktop opposite order uses the reduce-only closing composition path. The live emulator test exposed and fixed a wrong permission name (`OrderCompositionEvaluate` instead of `OrderCompositionClose`). Existing account qualification controls are retained.
- Close Order/Change State UI, services, command contracts, actor routes and validators are removed. The editor reloads persisted setup projections from Fund lifecycle notifications. Existing Load Trade and closed-live-feed policies remain.
- The solution builds successfully with **zero warnings and errors** using isolated artifacts to avoid overwriting the running development binaries.

### Verification evidence

Artifacts are under `.artifacts/execution-lifecycle/`:

| Verification | Result | Evidence |
|---|---|---|
| Full solution build | Passed; 0 warnings/errors | `final-solution-build.log` |
| Portfolio unit suite | 469 passed | `final-portfolio-tests.log` |
| Trade execution/close/contract mapping tests | 77 passed | `final-trade-tests.log` |
| Editor, monitoring eligibility and submission tests | 28 passed | `final-ui-tests.log` |
| FlaUI real designer controls | Passed; removed buttons absent, retained Load Trade/Add Trade visible | `flaui-controls-detail.log` |
| Emulator opening/opposite closing lifecycle | Passed; opening/closing setup and position history, realized PnL and balanced trial balance | `flaui-lifecycle.log` |

The emulator fixture owns isolated NATS, PostgreSQL, Cassandra and Redis containers. It creates a source-backed Fund setup, submits/fills via FlaUI and the broker emulator, verifies the Scylla-compatible projection becomes Open, then performs a headless opposite closing execution and checks Closed/Executed, terminal position/history, realized-PnL posting and balanced trial balance. Development database contents are not modified.

Broader test discovery also exposed three TradeFlow fixtures whose frozen date is a Saturday and older UI architecture/baseline assertions outside this change. Those broader suites are not reported as clean. The canonical UI projection-field coverage failure found during this work was fixed by carrying ExecutionAttemptId into the editor model. The emulator also exposed a state-preserving closing handoff producing a non-contiguous order version; fresh evidence now advances the source-event version while retaining visible trade state, with a passing command-handler regression test. Duplicate CommandIds are still handled by the actor before computation.

### Deployment

Restart API and UI together using the normal development scripts to consume the updated contracts. Running processes were left intact and were not restarted by this implementation. Legacy setup rows without links are not bulk repaired, and financial balances are not reset.


Final emulator result: `Setup_closes_from_confirmed_opposite_execution_without_manual_state_commands` passed in 5.56 minutes including fixture creation/teardown. The closing execution ran without a Trade Order editor state callback. All required financial and setup assertions passed. The accounting query uses the supported 100-row page size. Background synthetic-host Databento catalog/monitoring diagnostics are outside this test's broker lifecycle scope; this fixture does not qualify live Databento monitoring.

The API and UI must be restarted together to load the completed implementation. There are no remaining implementation blockers in this plan.
