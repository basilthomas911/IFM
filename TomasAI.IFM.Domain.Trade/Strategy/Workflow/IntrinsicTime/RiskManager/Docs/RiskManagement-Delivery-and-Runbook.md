# Risk Manager delivery and operating runbook

Delivery date: 2026-09-09. Boundary: durable **Authorized intent**. Broker consumption and submission remain outside this delivery. Legacy records remain read-only and do not create spendable capital; development capital is entered separately through the existing Portfolio controls.

## Delivered implementation

| Gate | Delivered behavior | Evidence area |
|---|---|---|
| RM-IP01 | Additive explanation, observation, terminal evidence and scoped query contracts; unchanged Risk result schema/hash | RiskObservationTests; existing RiskFunctionTests and contract suites |
| RM-IP02 | Deterministic per-quantity cash/loss/scope checks, frozen limits and market restrictions | RiskExplanationModel; RiskObservationTests; RiskAcceptanceTests |
| RM-IP03 | Idempotent Fund rejection/cancellation/expiry synchronization from exact committed workflow evidence | FundRiskTerminalTests; FundRiskTerminalIntegrationTests; RiskOutcomeRecoveryTests |
| RM-IP04 | Immutable revision history, monotonic summaries, bounded committed-source scans and durable recovery cursor | RiskHistoryRuntimeTests; RiskHistoryJournal; TradeDbContext.RiskHistory |
| RM-IP05 | Exact invocation/result and scoped paged history over NATS; history remains readable when current authority is unavailable | RiskHistoryRuntimeTests |
| RM-IP06 | Read-only Fund history and exact Risk details from Portfolio Administration and Strategy Observation; separate evidence tab | RiskHistoryUiTests; rendered artifacts |
| RM-IP07 | Original/resized replay, contention fencing, delayed reservation exclusion, lost Fund acknowledgement and restarted cursor/service checks | RiskResizingTests; financial integration; Risk runtime and recovery tests |
| RM-IP08 | API/desktop registration, schema installation, explicit backfill command and sequential qualification runner | Startup verification; Test-RiskManagerDelivery.ps1 |

## Contract and storage manifest

* Existing Risk calculation request/result schemas, keys and semantic hashes are preserved. The explanation is a workflow sidecar, independently reconstructed while accepting the verified result. It is not added to the schema-1 result hash. This replaces the plan's proposed result-version bump without reinterpreting old results.
* Workflow view key 35 is optional resize evidence; key 36 is optional RiskExplanation. Explanation keys 0-9 contain schema, result hash, quantity ceiling, cash, loss budget, multiplier, conditions, shared limits, quantity checks and content hash. Quantity checks refer to shared limit indexes and retain exact decimals. The explanation ceiling is 524,288 uncompressed bytes, without changing the existing Risk request/result byte ceilings.
* FundOrderProjectionReadModel key 27 adds optional RiskTerminalEvidence. Its keys 0-11 identify source command/event, workflow, Portfolio/Fund/order, Composer hash, target state, Risk result ID/hash, reason and original decision time. Existing lifecycle enum values are unchanged.
* Query actor RiskManagementQuery exposes GetRiskInvocation (23340), GetRiskResult (23341), and GetRiskHistoryPage (23342). Access uses the existing Portfolio read convention. Cursor schema 2 binds Portfolio/Fund, UTC date and page size and cannot reuse an Order Composer cursor. Default page size is 25; maximum is 100.
* Scylla risk_management_invocation partitions by exact workflow/invocation with immutable descending revisions. risk_management_history partitions by Portfolio/Fund/UTC date and orders by evaluation time and invocation ID. Conditional writes reject conflicting revisions and prevent old notifications replacing newer summaries. Full snapshot revisions provide lifecycle evidence without a third denormalized lifecycle table.
* History hash normalization first round-trips the wire contract, normalizing transport timestamps, then applies the existing semantic hash, which ignores diagnostic properties and decimal storage scale. PostgreSQL JSON and MessagePack replays therefore compare semantic evidence, while changed content still fails.
* PostgreSQL risk_history_projection_progress stores independent history and Fund recovery cursors. Pages contain at most 32 committed snapshots. At the end of a cycle the cursor returns to zero so a late commit behind a previous source cut is recovered. Successful history writes precede cursor advancement; retries are idempotent. Fund recovery advances independently even when a history page fails, so an outage cannot strand outcomes on later pages.

## Financial and recovery behavior

Risk approval is a sizing proposal. Current Authorized intent additionally requires the exact Fund authorization, an unexpired Reserved hold, and unchanged current financial authority. The query displays historical acceptance separately from this current check. Failure to read current authority is Unavailable, never an inferred approval.

Terminal Fund changes require the StrategyWorkflow role and the exact committed terminal workflow event. The financial writer holds the Portfolio admission lock, checks that the order has no active reserved/consumed exposure, and advances the financial revision with the terminal Fund append. A delayed original reserve request then fails its expected-revision check. An existing authorization or active reservation leaves synchronization pending for reconciliation; recovery does not invent a release, fill, cancellation or broker acknowledgement.

A lost Fund acknowledgement is resolved by reloading authoritative Fund history. Retries preserve the original terminal evidence and decision time. A conflicting Fund transition is retained and reported for reconciliation. The recovery worker scans committed records repeatedly, including terminal workflows; it does not depend on an active Strategy workflow actor.

Automatic re-sizing permits three total attempts and requires proof that a replaced reservation request can no longer commit. Candidate, upstream evidence, policy, funding source, financial authority and original expiry stay fixed. See [automatic re-sizing](RiskManagement-Automatic-Resizing-Implementation.md).

## Operation and repair

1. Build API and desktop from the same source revision. Normal API startup creates the additive Trade schema before workers start. Startup verification (`--verify-startup-only`) runs before any schema writes, backfill or hosted workers.
2. The worker maintains rebuildable Risk projections from committed snapshots. It changes no legacy inventory or capital. To explicitly rebuild or resume history without starting actors, feeds, listeners or Fund synchronization, run the API from its project directory with `--backfill-risk-history-only`. The command prints the next cursor after every bounded page; resume with `--risk-after=<printed cursor>`. A zero cursor means that scan reached its end. Re-running from zero repairs missing projections and discovers late commits.
3. A history storage outage retains the durable cursor and logs the failure. Restore storage and let replay repair the projection. A conflicting immutable revision requires investigating source/schema/serializer compatibility; do not delete financial records or rewrite a decision to force agreement.
4. For pending Fund synchronization, inspect the original workflow decision, Fund order and exact reservation receipt in Evidence and Portfolio Financials. Resolve an unknown financial commit using its original operation identity. If capacity remains active or Fund authorization differs, use the existing authorized reconciliation process; the Risk UI provides no financial mutation controls.
5. Expired/changed authority requires a new business workflow where appropriate. Do not refresh a historic result's expiry, replace its funding evidence, or treat an old Approved calculation as permission to execute.
6. Rollback must stop the new recovery worker before replacing binaries and preserve all additive tables/events. Old snapshot readers can ignore optional fields and retain existing terminal status names; an older writer must not resume a new terminal synchronization command. Historical ConsumePending/Consumed/Submitted phases remain read-only. Startup verification is safe for checking a replacement binary without starting work.

Run `scripts/Test-RiskManagerDelivery.ps1 -NatsUrl nats://127.0.0.1:14222` against an owned isolated broker. The runner executes suites sequentially, rejects failures/skips/empty selections, builds API/desktop and verifies Development startup. The broader Portfolio qualification runner also includes the new Risk history and UI tests.

## Verification record

Final passing evidence is under `TestResults/risk-delivery`:

| Suite | Passed | Report |
|---|---:|---|
| Trade units, including compatibility, explanation bounds and lost Fund reply recovery | 1,010 | trade-final.trx |
| Portfolio units, including terminal states and command maps | 202 | portfolio-units-final.trx |
| PostgreSQL capacity, Fund authorization and terminal fencing | 25 | financial-final.trx |
| Real Risk actor, three-horizon/twelve-variant matrix, resize replay, history and NATS queries | 8 | risk-runtime-qualified.trx |
| Risk UI rejected/expired/unavailable states and retained legacy-history regression | 4 | ui-final.trx |

There are 1,249 distinct passing cases in these suite reports, with zero failed/skipped cases in the final selections. The history/query case was rerun after the independent-cursor changes and passed (`risk-history-final.trx`); it is not counted twice. Rendered Risk windows were visually reviewed at minimum size; PNGs are in `ui/`. API and desktop builds passed with zero warnings/errors. Development startup verification is recorded in `startup-final.log`.

The bounded 100-quantity explanation case recorded 163.857 ms, 6,108,768 allocated bytes and 23,596 uncompressed explanation bytes on this Windows development machine. This is one local measurement including cold-path effects, not a production latency percentile or SLO. The test verifies all 100 rejected quantities are retained and cancellation is honored.

Financial terminal tests use committed, labelled workflow fixtures to qualify the actual PostgreSQL fence. Runtime query tests use a scoped Fund read fixture and real NATS/PostgreSQL/Scylla. Lost-acknowledgement service restart tests simulate the missing reply and reload committed Fund state. These qualified boundaries are not represented as a broker-connected full execution test. No application capital entry, legacy conversion, execution consumption or submission was performed.

Exploratory reports retain the failures fixed during implementation: serializer normalization, test-host registration, the UI split-container hang, an incomplete capacity fixture and the command-map inventory. Final passing reports above supersede those runs; failures are not counted as passing evidence.
