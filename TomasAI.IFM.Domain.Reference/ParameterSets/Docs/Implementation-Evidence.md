# Parameter Sets implementation evidence

## Status

Parameter Sets management implementation has progressed through authoring/lifecycle/assignment handlers and the initial editor. The complete system-wide release checklist remains open. The current user-directed order is Parameter Sets first; RSI seeding and Regime Discovery runtime changes are deferred. Market Outlook selection/update is explicitly deferred by the user and is not a blocker for this phase.

## Domain gate RD-G1

User decision: preserve current calculation dependencies; allow supported reductions. No new specialist scoring or partial fusion policy is introduced.

The Daily seed contains 69 unique rows: 35 required and 34 optional. Dependency validation rejects removing or weakening mandatory inputs. Schema-2 snapshot requests use the enabled rows; legacy schema-1 expansion remains unchanged. Reduced request construction is tested; a complete reduced calculation with live evidence has not been verified.

## Assignment ownership decision approved

`StartRegimeDiscoveryPipeline.StartPipelineAsync` resolves configuration by request time and target horizon through `ResolveEffectiveRegimeDiscoveryAsync`. It does not use a deployment parameter reference. `IntrinsicTimeStrategyWorkflowEntityId` contains workflow-definition and futures ITI identities. Deployment candidates are bound later by Trade Selection, after Regime Discovery has completed.

User approved workflow definition + horizon. Scope keys must be stable, must separate Daily/Weekly/Monthly, and must reject a payload whose target horizon differs. Deployment selection order remains as established. Application policy is NextStartup; a pending assignment cannot replace the applied generation in a running process.

## Approved assignment implementation

- Generic keyed assignment scope/revision/applied-reference contracts.
- WorkflowParameterScopeModel validates the existing workflow definition and Daily/Weekly/Monthly horizons, with deterministic distinct assignment identities.
- ParameterAssignmentModel requires authoritative Published exact versions, matching payload identity/hash/horizon and optimistic revision; changes have NextStartup policy.
- ParameterStartupSnapshotModel freezes validated enabled assignments per startup generation. Later pending changes do not alter that snapshot; stale generation resolution and duplicate assignment identities are rejected.

Assign/disable Command actors, extension handlers, event-sourced assignment state, idempotent projection tables and authoritative assignment queries are now implemented. Runtime activation remains deliberately unconnected in this phase.

## Implemented foundation

- ParameterSets contracts, explicit MessagePack keys, canonical JSON/hash model and draft/publish lifecycle foundation.
- Standard Command and Query actors with maps and per-message extensions; domain actions in Model. No new Function/Realtime actors or recovery polling service.
- Event-source state and ConfigurationDb projection foundation, including per-set revision and operation replay checks.
- Reference Data Parameter Sets navigation and initial 69-row editor with optional reductions, age, preparation and monitor fields; create/save/validate/publish API paths.
- New drafts allocate matching embedded payload identity/version and immutable version reference.
- Schema-2 Regime snapshot request validation prevents bypassing mandatory dependencies.

## Current management implementation

- Set create/save/publish/rename/retire command paths and event-derived committed operation receipts.
- Authoritative GetParameterSetState query for exact committed versions after save/publication, independent of list-projection timing.
- Workflow-definition + horizon assignment/disable commands; eligibility reads the authoritative published version and verifies its exact reference/hash.
- Retirement checks all currently registered workflow/horizon assignment scopes through authoritative state. Future scope registrations and applied runtime generations must extend usage protection before runtime activation.
- Shared PostgreSQL advisory writer lease held across authoritative state load, decision and event persistence for set and assignment mutations. This serializes assignment-versus-retirement decisions across hosts. It does not make ConfigurationDb projection and event append one transaction.
- A default-no-op command completion hook releases per-command resources even when cancellation bypasses exception handling; only parameter actors acquire this lease. Duplicate audit reservations opt into authoritative payload verification.
- ConfigurationDb rejects direct saved-payload/identity/schema edits, deletes and illegal lifecycle reversals; projection receipts reject changed operation identities.
- Bounded keyset pagination instead of the former silent 100-version truncation.
- Editor: 69-row signals tab, Disable optional working-copy action, scalar calculation-parameter tab, full validation issue grid, metadata rename, version comparison, committed-operation history, publication, retirement and pending assignment dialog.
- Unsaved version switching and Reference form navigation/closure are guarded; in-flight operations cannot be discarded by navigation.

## Verification evidence

- Reference ParameterSets unit/actor/model tests: 19 passed (lifecycle, scope, startup snapshot model, assignment handlers, serialization and receipt checks).
- Shared command audit/dispatch tests: 27 passed, including completion cleanup on cancellation after state loading and duplicate command completion.
- UI ParameterSets model tests: 4 passed (working-copy immutability, post-commit refresh, generic field editing/type checks and comparison).
- Isolated PostgreSQL integration: 1 passed, exercising projection idempotency, direct SQL payload protection, metadata rename, publication, assignment revision replay, retirement, pagination and writer-lease serialization. This is storage/handler evidence, not a full live NATS-host test.
- Final isolated UI build: zero warnings, zero errors. Debugger-held normal output was left alone.
- Earlier Regime calculation/configured-requirement tests: 13 passed in the prior phase; Regime processing was not modified in the current phase.

## Remaining release gates and practical limits

Runtime assignment application, producer preparation/monitoring and Regime Discovery integration are intentionally deferred. Assignment UI explicitly reports that runtime activation is pending. Editable Prepare/Monitor fields do not yet control producers.

The management foundation is not the full specification schema registry, comprehensive audit/permission model or legacy migration facility. Those gates remain open, along with live transport/host registration and visual UI verification. Timeout/unknown-operation UX needs broader fault-injection coverage; callers can inspect authoritative committed operation receipts. The seed's legacy strategy-owner field remains a draft placeholder and must be resolved before runtime use.

Do not treat these tests as full AT-001 through AT-030 acceptance or completed end-to-end trading-system verification. No live trading, production deployment, RSI seeding, Market Outlook selection change or Regime processing change was performed in this phase.

## Plan revision 1.2

User decisions incorporated into the implementation plan only: optional full contiguous seed or empty start; preserve restored state; workflow admission on eligible ITI triggers with terminal pipeline-owned WaitingForInputs outcomes; bounded degraded operation; RSI across timeframe routes first, followed by approximately one trading day of observation before other signal families. RSI seeding and the observation run have NOT been implemented or performed by this documentation update. Existing test results above refer to earlier foundation work.

## Plan revision 1.3

Parameter Sets management now precedes the RSI pilot. RSI still requires the user's acceptance after its observation period before seeding is generalized to other signal families. Market Outlook is deferred until the user returns to it. The missing-history startup blocker remains superseded by valid empty startup.


## Schema-3 implementation in progress (2026-09-10)

- Added membership-only schema 3 while retaining schemas 1 and 2.
- Added approved Daily/Weekly/Monthly draft interval sets and a horizon selector. No included schema-3 signal is optional. VX dependency remains explicit.
- Added reviewable legacy upgrade and interval editing with dependency regeneration. Saved versions remain unchanged.
- Added the user-approved single-user Development access policy to all Parameter Sets command/query handlers. Production permission mapping remains deferred; the policy rejects non-Development environments.
- Tests and final build results for this increment are recorded below when completed. Runtime assignments, producer planning, RSI acquisition/seeding and live observation are not yet completed by this increment.


### Verified results for the explicit-set increment

- Reference ParameterSets model/actor/access tests: 32 passed.
- Regime configured-request tests: 4 passed, including schema-3 included-input enforcement and legacy schema-2 behavior.
- Parameter editor model tests: 5 passed.
- Isolated PostgreSQL integration: 1 passed, including schema-3 persistence alongside legacy versions and idempotent audit projection.
- Real WinForms control/rendering tests: 3 passed (Daily 55 catalogue rows, Weekly 69, Monthly 55). The generated Daily image was inspected after correcting a blank test-host rendering issue. These are controlled component tests, not live NATS-host acceptance.
- API-host and UI isolated serial builds passed with zero warnings/errors. Parallel API build initially hit a shared native-DLL copy lock; serial isolated build resolved it.
- Audit evidence now accompanies set/assignment facts, is reconstructed from authoritative state, and is projected in the same ConfigurationDb transaction as each projected mutation. This does not claim that EventSourceDb and ConfigurationDb share one transaction.
- Remaining: full component/schema registry and legacy migration facility; live transport/host acceptance; runtime assignment activation and producer planning/monitoring; RSI historical acquisition, seed/live handoff and observation/acceptance. The current increment does not complete these gates.


## Registry and runtime implementation increment (2026-09-10)

Implemented:
- Persisted application-area/component/schema registry, schema inspection query/UI, structural validation before draft persistence, and immutable database schema/audit/history guards.
- Registry-driven Reference Data navigation. Unsupported schema/unknown payload fields remain inspectable as their exact stored JSON and are read-only in the typed editor.
- Deterministic startup producer demand union, including existing intraday consumers. RSI(13) and RSI(14) remain distinct; TDI contributes RSI(13), and structure contributes EMA/ATR dependencies. Schema-3 publication rejects enabled producers without a supported route.
- Startup preview through the standard Query actor and Reference Data UI.
- ApplySignalStartupPlanCommand/ReleaseSignalStartupPlanCommand through ParameterStartupCommandActor, mapped extension handlers, bounded recent-startup inspection cache (100; no startup-count limit), existing writer lease, authoritative event-source persistence, and idempotent ConfigurationDb projection. Committed generation replay cannot reactivate a released generation; audited uncommitted commands may be retried explicitly.
- Retirement checks authoritative current assignments. Historical startup records do not prevent retirement; captured runtime versions remain usable.
- Application startup applies a process-boot generation and prepares additional RSI/ATR/ADX/MACD producers from its plan. Parameter application/preparation failures are optional startup failures: visible degradation does not prevent the feed's independent startup activities.
- Regime Discovery resolves workflow-definition/horizon assignments from the frozen generation, checks exact payload hash, and returns explicit initialization outcomes for unavailable startup/disabled assignment. Unassigned scopes retain the legacy resolver; an explicitly disabled scope does not fall back.
- New schema-3 seeds no longer invent an owning strategy-parameter-set identity. Their ownership comes from workflow-definition/horizon assignment scope.
- RSI(13) can no longer populate the RSI14 cache metric. Existing RSI(13) consumers remain in startup demand; assigned Regime profiles request RSI(14).
- Assignment UI now states next-application-startup activation and shows registered generations. Registered does not mean the owning process is still alive.

### Lifetime correction: no shutdown gate

User confirmed that parameter assignments persist until changed or disabled. ApplicationShutdown and manual release are not required and are not implementation blockers. Historical startup snapshots do not prevent retirement once current assignments are replaced or disabled. Running workflows retain their exact captured payload; retired payloads and durable startup history are retained. The latest 100 records are cached for inspection without imposing a limit on startup count. The existing release message remains compatible but is not required by normal operation.

### Still open; not completion claims

- Live NATS/host acceptance and reduced-profile observation; the host was not restarted for these changes.
- Complete legacy inventory/migration/rollback adapter and exact historical compatibility evidence.
- Current readiness/monitoring query/UI completion, durable per-producer terminal evidence, and full startup failure-injection qualification. Producer command acceptance is not a claim that a signal is warm.
- RSI historical seed acquisition/replay/handoff pilot, including Daily, its observation period and user acceptance. Other indicator historical seeding and Market Outlook remain deferred as instructed.
- Full specification acceptance-test mapping. Unit/storage/build checks below are targeted evidence, not system-wide release acceptance.


### Verified results for the runtime increment

- Reference Parameter Sets: 43 unit/model/actor checks passed.
- Regime runtime assignment resolution: 3 checks passed (pending startup, explicit disabled assignment, exact generic payload hash).
- Application startup/lifecycle contracts: 17 checks passed, including parameter failure isolation.
- Regime signal snapshot/cache adapter: 13 checks passed, including RSI(13) rejection for RSI14 requirements.
- Isolated PostgreSQL projection integration: 1 passed, including immutable schemas/audit records and no reactivation after release/replay.
- Parameter editor models: 7 passed; actual WinForms rendering: 3 passed (Daily/Weekly/Monthly). Latest Daily rendering was visually inspected.
- API host, Views, and full desktop application builds completed with zero warnings and zero errors using isolated output directories where required.

Total: 87 targeted checks. Logs are under `.tmp/parametersets-*`; rendered screenshots are under `.tmp/parametersets-ui-evidence`. These do not constitute live-host acceptance or completion of the open release gates.

### Lifetime correction verification (2026-09-10)

Reference Parameter Sets: 44 tests passed, including retirement after assignment removal while an existing runtime retains its published snapshot, and 105 consecutive starts without manual release with a bounded 100-record inspection cache. Reference Data Views build passed with zero warnings and errors. No live application restart was performed.

## Reporting, monitoring, migration and provenance increment (2026-09-10)

Implemented standard startup Command/Query actor mappings for durable preparation reports. Each report covers every planned producer and distinguishes NotRequested, ExistingRoute, Accepted, Failed, TimedOut, MarketClosed and Cancelled. Accepted is command acceptance, not signal warmth. The host records terminal evidence under an independent five-second deadline; it does not introduce a recovery loop. Reports are immutable in ConfigurationDb and retained in the event stream; recent inspection state is bounded.

Reference Data now offers recent-startup preparation details and an explicit refresh of current monitored signals. Monitoring uses the frozen assignment's exact enabled Monitor rows, calculation identity and age/quality limits through the existing snapshot provider. Missing observations remain visible. This query does not admit, stop or park workflows.

Legacy Regime versions can be inspected read-only and copied into an exact new draft through the standard create handler. Schema-1 sources expand their own frozen horizon into schema 2; existing schema-2 settings and optionality are preserved. Migration does not use the new seed or silently promote optional evidence. Kind-qualified deterministic target identities and immutable lineage retain the original codec/hash. Publication/assignment remain explicit. Unsupported legacy optional producers produce visible plan issues without suppressing supported startup work; schema-3 unsupported included inputs still fail publication validation.

Successful Regime completion contracts now retain startup run, assignment identity and revision through execute/completed/complete-command/workflow state. Legacy contracts default this provenance to null.

Verified so far: 48 Parameter Sets unit tests (before the additional null-editor regression), four Trade runtime/provenance tests, three host preparation failure-injection cases, one expanded PostgreSQL projection/migration integration case, and three WinForms rendering cases. The real actor-host acceptance run is in progress; it is not yet a passing release claim. RSI seeding has not been changed by this increment.


## Current verification status: RSI pilot and real transport (2026-09-10)

This section supersedes earlier increment statements about work that was not yet implemented. It does not close every release checklist item.

Implemented and verified in this increment:
- Real NATS Command/Query actor acceptance passed against disposable NATS (24222), Redis (26379), and PostgreSQL (25432). The test covers create/retry, publish, assign, apply startup, report/retry/read, missing-signal monitoring, retirement protection, disabling the assignment, retirement and preservation of the already frozen published runtime version. No running trading application was restarted.
- RSI startup has an additive historical seed contract. Session-aligned completed windows cover 15 seconds, 1 minute, 5 minutes, 15 minutes, 1 hour, 4 hours and Daily. The current host pilot applies to the additional RSI producers requested by Parameter Sets; the existing RSI13/TDI startup route is unchanged.
- Intraday seed closes come from the retained tick time index. Daily seed closes come from stored completed contract EOD or the existing unadjusted ES continuation. This increment does not add Databento historical intraday downloads. Missing, invalid or incomplete coverage yields an empty seed and ordinary live warmup.
- Historical observations advance the existing Wilder accumulator in order. Only the final result/checkpoint is emitted. Restored checkpoints take precedence. Started events record seed count/reason, and the event handler logs that disposition. A restored warm result is made available again without waiting for another long interval.
- Development configuration enables `ParameterSets:RsiHistoricalPilotEnabled`; the host also requires Development. Setting it false bypasses acquisition and leaves existing durable state intact. This is a code/configuration change, not evidence that a running process has loaded it.

Latest passing checks (separate suites, not a cumulative count of reruns):

| Verification | Result | Log |
| --- | --- | --- |
| Reference Parameter Sets unit/model/actor checks | 49 passed | `.tmp/parametersets-reference-final.log` |
| RSI and relevant signal regression checks | 21 passed | `.tmp/parametersets-rsi-final.log` |
| Pilot source and startup outcome tests | 11 passed | `.tmp/parametersets-rsi-source-tests.log` |
| Real nearest-tick Scylla read in dedicated `parameter_sets_rsi_test` keyspace | 1 passed | `.tmp/parametersets-rsi-storage.log` |
| Real Parameter Sets actor lifecycle | 1 passed | `.tmp/parametersets-real-actor.log` |
| PostgreSQL projection/migration/immutability | Previously passed in this increment | Earlier storage log |
| Current WinForms Daily/Weekly/Monthly rendering | 3 passed; Daily visually inspected | `.tmp/parametersets-ui-current.log` |
| API host build | 0 warnings, 0 errors | `.tmp/parametersets-server-final.log` |
| Full desktop build | 0 warnings, 0 errors | `.tmp/parametersets-desktop-final.log` |

The Scylla test verifies nearest preceding tick, exact cutoff, no future tick, and explicit value-date isolation. It uses a dedicated test keyspace, not application market data. Source tests cover all seven intervals and whole-seed rejection on a gap. Accumulator tests verify numeric RSI for rising prices, replay, duplicate/stale rejection, next-live-bar advancement and restored-result serialization. These are not proof of uninterrupted live transport delivery.

### Remaining qualification; do not claim full completion

- The approximately one useful trading-day RSI observation and explicit user acceptance are still required before expansion. Actual feed coverage, seed/live delivery ordering across the actor/event transport, controlled interruption/restart and later successful Regime progression need runtime evidence.
- The full specification-to-test acceptance matrix, all retained workflow-reference migration/rollback inventory and exhaustive fault-injection qualification remain open. The passing exact legacy adapter tests are narrower than a full installed-database migration audit.
- Nullable-reference compatibility is resolved by schema 4. Schemas 1-3 retain their original generated JSON and SHA-256 hashes. New drafts and editor working copies use schema 4; editing schema 1-3 produces a schema-4 working copy and saving appends a new immutable parameter-set version.
- No new historical intraday provider acquisition was added. Local history gaps are an allowed empty-start condition, not a promise of next-day readiness.
- Production authorization, other indicator historical seeding and Market Outlook changes remain outside the approved current rollout.

ApplicationShutdown/manual generation release is not a blocker or part of normal parameter-set lifetime.

## Schema 4 compatible migration (2026-09-10)

Schema 4 retains schema 3 membership and dependency semantics and corrects the generated JSON schema to use C# nullable-reference metadata. Non-nullable domain objects such as Trend reject JSON null during structural validation. Intentionally nullable compatibility members such as SignalRequirements declare their nullable shape; Regime domain validation still requires a populated signal list for schemas 2-4.

The registry builds schemas 1-3 with the original generator and registers schema 4 separately. PostgreSQL initialization inserts schema 4 with ON CONFLICT DO NOTHING; immutable guards continue to reject updates and deletes. Unit verification compares every schema 1-3 JSON document and hash against a registry constructed with the legacy generator.

New Regime drafts are schema 4. Opening a schema 1, 2 or 3 version for editing returns a schema-4 working copy. Identity, horizon, supported row identities and editable settings are preserved. Saving allocates the next parameter-set version; it does not mutate the source version. Publication and assignment remain explicit.

Verification: 52 Parameter Sets tests, 8 Regime compatibility tests, 7 editor tests, 3 rendered UI tests, one PostgreSQL registry/projection integration test and one real NATS actor lifecycle test passed. API and desktop builds completed with zero warnings and zero errors. The nullable-schema compatibility blocker is closed. Trading-day RSI observation and the broader qualification items already listed remain separate.