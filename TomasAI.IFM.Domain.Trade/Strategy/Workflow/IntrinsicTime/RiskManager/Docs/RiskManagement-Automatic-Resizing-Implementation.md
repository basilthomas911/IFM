# Automatic Risk re-sizing after contention

Date: 2026-09-09. Owner-approved addition to RM-D05. Implemented in the working tree; this record does not mark the other history/UI/explanation/Fund synchronization gates complete.

## Behavior

- The original preparation is attempt 1. Re-sizing can prepare attempts 2 and 3; a further proven contention ends the workflow with NoTrade, Stop and `RM.CAPACITY.CONTENTION_EXHAUSTED`.
- Before a reservation request exists, changed authoritative sizing inputs can trigger a fresh invocation.
- After a reservation request exists, first query its exact operation receipt. A found grant continues the original handoff. An unavailable read, or a missing receipt at the original revision, does not authorize replacement.
- Re-sizing is allowed when a coherent authoritative receipt query returns NotFound at a financial revision greater than the old request's expected revision, followed by a fresh admission read at least as recent. `FinancialQueryStore` reads receipt and revision under `FOR SHARE`; reservation admission locks the same authority and requires equality with the expected revision. The old request can therefore never newly commit after this proof, even if a delayed delivery arrives later.
- Recalculate the largest feasible whole-unit quantity from refreshed cash, usage and applicable admission limits. Preserve exact candidate/contracts, all upstream envelopes, policy/configuration hashes, margin/funding evidence, environment, financial authority and original expiry. No refreshed quote or authority epoch is silently adopted.
- Each attempt receives a distinct deterministic invocation ID derived from the preceding invocation and `Resize/{ordinal}`, a new workflow revision, request hash, result and eventual reservation identity. Earlier committed snapshots and Function completions remain intact.
- Stale candidate evidence or original expiry ends re-sizing without extending its lifetime. Freshness and all normal Function/acceptance validation still apply. Zero feasible size follows the existing business-rejection path.
- FundPending, Authorized and historical consumption/submission checkpoints are not re-sized. Risk still ends at Authorized intent and never consumes/submits an order.

## Persistence and compatibility

Workflow view key 35 adds optional `RiskResizeEvidence`. Its keys are: 0 PreviousInvocationId, 1 PreviousReservationOperationId, 2 PreviousExpectedFinancialRevision, 3 AbsenceFinancialRevision, 4 AdmissionFinancialRevision, 5 ObservedAtUtc. Before any reservation, operation ID and absence/expected revisions are zero. For a pending reservation they identify the exact excluded request and the proof revision. The latest checkpoint is retained in the view; earlier checkpoints remain in committed workflow events.

Existing Risk request/result keys, result schema and hash algorithms are unchanged. Normal original-request replay remains unchanged. The new workflow field is additive; no historical execution enum is reused.

## Implementation

- [RiskResizing](../Model/RiskResizing.cs): authority validation, revision exclusion proof, bounded next invocation and terminal behavior.
- [AdvanceRiskFinancialHandoff](../../Command/AdvanceRiskFinancialHandoff.cs): reconcile before re-sizing and commit the new prepared invocation before dispatch. Admission rereads consistently use the normalized underlying scope key.
- [ExecuteRiskFinancialHandoff](../Realtime/ExecuteRiskFinancialHandoff.cs): terminal capacity refusals prompt authoritative reconciliation, rather than requiring only a successful grant before advancement.
- Existing realtime dispatch, Function acceptance and workflow revision guards dispatch/recover the saved new invocation and ignore stale completions/advancement commands.

## Verification

RiskResizingTests covers a 10-to-2 unit change, deterministic independent request identities, unchanged old bytes/upstream/policy/funding/expiry, serialized evidence, zero feasible size, insufficient absence evidence, prohibited financial phases, authority change, expiry, three-attempt exhaustion, duplicate advancement and terminal-refusal dispatch.

CapacityReservationIntegrationTests exercises the actual PostgreSQL query/exclusive-fence protocol: an absent old receipt at a newer revision, rejection of delayed original delivery, smaller subsequent admission and rejection of another late original delivery. No synthetic receipt is used for that proof.

The real Risk actor test runs both original and resized calculations over isolated NATS, reloads their PostgreSQL completions and replays both independently. Its changing financial snapshot is explicitly a fixture; the separate PostgreSQL test proves reservation exclusion. These are distinct qualified boundaries, not a claim of a broker-connected end-to-end run.

Final verification: 1,003 Trade unit tests, 25 Portfolio reservation/query PostgreSQL integration tests, and 6 selected real Risk runtime cases passed, with zero failures/skips in their final runs. Runtime cases include all 12 variants for each of three horizons, original/resized completion replay, original Function reconstruction, and audited preparation recovery. Reports are under `TestResults/risk-resizing`: `trade-units.trx`, `financial-fencing.trx`, and `risk-resizing-runtime-final.trx`.

An earlier broader runtime run recorded two RM.TIME.EXPIRED failures while other qualification work was running; the final isolated sequential runtime run passed without relaxing production deadlines. The new test initializes assertion diagnostics before creating its short-lived input. Exploratory failures remain in the earlier report and are not counted as passing evidence.

These targeted results do not requalify the entire financial gate suite or the planned history/UI features.

API build passed with zero warnings/errors; Development `--verify-startup-only` passed (`TestResults/risk-resizing/startup.log`). Risk documentation links and `git diff --check` passed. The isolated NATS container used for verification was stopped. No application capital, legacy records or external execution were changed.
