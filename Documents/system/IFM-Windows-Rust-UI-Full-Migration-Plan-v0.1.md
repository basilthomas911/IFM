# IFM Windows Rust UI full migration plan

Updated: **2026-10-24**. Target project directory: **`TomasAI.IFM.UI.Rust.eGui`**. Target desktop artifact: **`TomasAI.IFM.UI.Rust.eGui.exe`**.

Status: native controllers, services and event consumers implemented; isolated backend and workstation FlaUI qualification passed. Clean-Windows and later live-session qualification remain separate gates. The hybrid remains unchanged and remains the default UI.

Current ownership is recorded per source file in `TomasAI.IFM.UI.Rust.eGui/migration/native-ownership.json` (297 files, with API methods tracked separately). Reproduce it with `python TomasAI.IFM.UI.Rust.eGui/tools/audit_native_ownership.py`; it also verifies all 79 original Rust source hashes. The checkpoint paragraphs below are chronological implementation history, not a cumulative list of current outstanding work. The final acceptance record must identify remaining gates explicitly.

### Current acceptance record (2026-10-24)

The tested x64 package is `.artifacts/standalone-rust-ui/app/TomasAI.IFM.UI.Rust.eGui.exe`, **35,984,896 bytes**, SHA-256 **`1140c051eeaefea554a4119fe7ce6641a6aebcb1b32da7e764f6481b7622b7d9`**. It is a byte-for-byte copy of the executable used by every final desktop scenario. PE inspection found no CLR header or managed runtime import; every standalone desktop run also checked its actual loaded modules before and after the scenario, with no coreclr, hostfxr or hostpolicy. All runs closed gracefully. The package contains one executable and optional audit metadata; client profiles/journals remain data, not managed runtime dependencies.

| Gate | Result / evidence |
| --- | --- |
| Complete native functional regression | **231 passed, zero failed**, 68 test binaries; `.artifacts/standalone-final-full-verification.log` |
| Isolated NATS/C# integration | **52 passed, zero failed**; repeated successfully after the desktop fixture additions in `.artifacts/standalone-final-strict-fixture-recheck.log` |
| Additional portfolio/market rendering and transfer checks | **6 passed, zero failed**; `.artifacts/standalone-auxiliary-render-verification-fixed.log` |
| Opt-in visual/theme suite | **8 passed, zero failed**, including two additional feature-gated image checks; `.artifacts/standalone-final-visual-feature-tests-fixed.log` |
| Financial journal compatibility | The unchanged managed stores reopened **195 native operation journals** in the final strict recheck; both posting and configuration formats verified |
| Actual paired desktop checks | **19 scenarios / 60 paired FlaUI captures passed**, plus **one standalone-only independent-date correctness case**; `.artifacts/standalone-final-acceptance.json` |
| Comparison-tool rejection checks | **4 passed**: changed values, missing captures, and matching empty displays cannot satisfy required business values |
| Contract evidence | **50 typed cross-language fixtures**, **2,684 compiled schemas**; these counts are not claims that every public backend method is a frontend feature |
| Source ownership | **297 managed source files** mapped by responsibility; no source-census drift; **79 original Rust hashes unchanged** |
| Frame diagnostics | Debug and optimized diagnostics passed for 100/1,000/10,000 plan rows. Optimized p95 frame time **2.8–3.7 ms**; 10,000-row one-time preprocessing median **20.5 ms**. `.artifacts/standalone-release-frame-benchmark.json`; not a live market throughput or end-to-end latency qualification |
| Clean Windows without .NET | **Blocked by environment:** this workstation has .NET; Windows Sandbox is absent, and Hyper-V VM enumeration denies this account access. The user confirmed on 2026-10-25 that no clean Windows VM is available; this specific gate remains unverified |
| Live market/broker/recovery | **Not claimed by these isolated runs**. User plans the existing hybrid's live verification first, then standalone comparison and a later cutover decision |

The nineteen paired scenarios cover Reference, Portfolio, Portfolio draft editors, System/backup/scheduler, recovery notification, Market Data editors, order workspace, Broker Trade/account qualification/fills, working orders, Actor Health, Operations Health, financial reads, posting drafts, parameter inspection, Iron Condor lifecycle, JSON-only configuration, populated Market Outlook, strategy Updates/Details/Summary, and populated Iron Condor quote/plan updates. Portfolio's twenty dialog layouts also have the additional rendered-layout checks.

The populated Iron Condor case requires actual numeric values in both clients: **−$50 at 2.78%** and **−$100 at 5.56%** of the **$1,800 maximum loss**, then **+$50 at 7.14%** of the **$700 maximum profit**. Entry credit is 14 points, quantity one and multiplier 50; marks produce combined prices −15, −16 and −13 points respectively, before fees. The fixture uses the existing backend hot cache for valuation/summary and the backend entry-credit risk function for forward-loss scaling. Projected prices and quoted Greeks are synthetic display inputs, not a pricing-calibration or historical-market claim.

Documented baseline differences:

- **Risk Policy (user approved):** standalone uses the correct policy version and aggregate revision fields for activation and retirement. The hybrid's swapped request fields remain unchanged.
- **API date authority (user required):** defaults, lookup audit timestamps, business ranges and calendar decoration use API context. The hybrid retains some wall-clock behavior. Reports record the specific date/time exceptions instead of changing financial values.
- **Independent date pickers (user-approved exception, 2026-10-25):** standalone fixes shared widget/popup identities. Selecting October 20 and October 21 retains both dates. The unchanged hybrid's baseline instead changed the first date to October 21 and left the second at October 24; its failing capture is retained in `financial-distinct-dates-ad6d9b77656f42db84d24ccab3fe3727`.
- **Flattened UIA neighbors:** monitor/health reports identify the two horizontally clipped neighboring strategy labels excluded from those scoped comparisons. Screenshots retain their pixels; required fields inside the tested view are compared.

The strategy desktop scenario places identical synthetic observations inside both clients' visible history windows and supplies its operational clock through the fake API. This does not repair or endorse the hybrid's workstation-clock range selection. Terminal recovery events are injected to check presentation; no real 120-second backend recovery, API restart, order, posting, backup or live feed is triggered.

The optional details golden initially failed because the copied image lacked the borders and caption color already present in the unchanged hybrid's current renderer. Expected/actual/diff images and the source review are retained under `.artifacts/standalone-golden-review`. The standalone-owned expected image was refreshed after visual inspection and renderer/font-source comparison; no production renderer or original hybrid file was changed. Its rerun and the dark-theme render passed. Earlier failed harness runs remain retained and are not counted as passing acceptance evidence.

Fund report formulas remain **LegacyLedgerPostings/v1**: realized postings relative to period opening balances/deposits, sample Sharpe annualized by √252 **per posting**, and drawdown including deposits, withdrawals, commissions and realized P&L without cash-flow adjustment. Rust displays the backend's report and calculation basis; it does not redefine them.

The implementation retains the original hybrid source, dependencies, build and default launch selection. New VS Code tasks and `scripts/UI/Rust/Start-StandaloneRust.ps1` launch the standalone explicitly with an existing JSON client profile. No cutover, legacy removal, commit or push has been performed as part of this acceptance work.

## Current target and authority (2026-10-23)

This revision plans a complete standalone Windows frontend: **one Rust UI executable and one UI process**, with egui rendering, Rust presentation models, Rust view-state controllers, Rust API clients and Rust event consumers. The shipped UI must run without hostfxr, a CLR, managed UI/bridge assemblies, a local pricing DLL, or a helper UI process. Backend API, MarketData Host/workers, NATS, databases and broker services remain separate existing services.

The existing `TomasAI.IFM.UI.Rust` is a hybrid implementation: `src/app/hosting.rs` loads hostfxr, and `TomasAI.IFM.UI.Rust.Bridge.Net` supplies managed sessions and view-model lifetimes. Its rendered screens are reusable reference implementations; its managed ownership is **not** the final architecture. Creating or renaming an executable alone does not complete this migration.

The new folder will contain the Rust Cargo package, application state, transport, controllers, tests and embedded resources. Use a Rust-compatible package/library name such as `ifm-ui-egui` / `ifm_ui_egui`; Windows packaging assigns the requested executable filename. Assets and localized strings should be embedded where practical. External configuration, credentials, logs and user journals remain data files. OS-provided graphics/accessibility libraries are normal Windows dependencies. No application-specific .NET or pricing runtime is part of the final frontend package.

This section and the refreshed inventory supersede the older hybrid ownership, source counts and migration estimates below. The older entries remain a historical record of WinForms/native control work, not evidence that the standalone migration is complete. The [standalone migration design](IFM-Standalone-Rust-UI-Migration-Plan-v1.0.md) remains the detailed protocol reference; the executable/project naming and inventory in this revision take precedence.

### Refreshed inventory and evidence

- [Complete model/view-model inventory and source counts](IFM-Windows-Rust-UI-Model-Inventory-2026-10-23.md).
- [Machine-readable references and target disposition for every model/view-model source file](IFM-Windows-Rust-UI-Model-Inventory-2026-10-23.csv).
- [Supporting services, consumers, API clients and bridge model/controller inventory](IFM-Windows-Rust-UI-Dependency-Inventory-2026-10-23.csv).
- [Backend boundary audit and its 150-row source checklist](IFM-Rust-UI-Backend-Boundary-Audit-2026-10-23.md), with the [per-file backend/client decisions](IFM-Rust-UI-Backend-Boundary-Inventory-2026-10-23.csv). This audit supersedes earlier narrow claims about local financial calculations.
- Reproduce with `python scripts/UI/Rust/Update-RustUiModelInventory.py --date 2026-10-23`.

The inventory scans tracked source rather than leftover build directories. It separates direct Rust bridge references, WinForms references, indirect dependencies, registration-only references, and rows with no production caller found. These are conservative source-reference classifications, not runtime execution counts. Reflection, same-name types, dependency injection and method-level reachability need explicit review; a missing direct bridge reference is not permission to delete a model. Every row needs a final disposition and verification evidence.

The standalone behavior baseline is the current Rust UI. WinForms-only source rows are reachability/exclusion evidence; unused WinForms calculations do not require new backend equivalents. Preserve the current hybrid while building its standalone replacement.

### Independent source ownership (user confirmed 2026-10-23)

Copy the current Rust screens, display models, catalog and rendering tests into `TomasAI.IFM.UI.Rust.eGui`. Keep `TomasAI.IFM.UI.Rust` source, dependencies, build and runtime unchanged. Do not move its screens or introduce a shared rendering crate or dependency on the standalone project. The standalone copy replaces managed hosting with native Rust backend clients and controllers. Record copied-source hashes in `migration/screen-baseline.json`; screen fixes may need applying independently to both applications until the user approves cutover. Neither application replaces the other in ServerManager by default.

### Native implementation checkpoint (2026-10-23)

This is a development implementation, **not a completed standalone migration or a release acceptance claim**. The current hybrid remains the usable comparison application and the default launch target.

#### Additional native work (2026-10-24)

**Further desktop qualification (still in progress):** paired FlaUI comparisons passed Broker Trade, Order Fills, and account qualification (`broker-workspace-48f802e702034bb7a916d41be04426f8`), plus operations-health stages, counters, and dataset recovery details (`operations-health-79b6e3750baa4ea7a4abe9686d132a51`). Evidence lives under `.artifacts/standalone-desktop-parity/`. The health scenario supplies a moving clock through the fake API and fresh HTTP observations, since the unchanged hybrid validates freshness against its workstation clock. Comparison validates those operational observation timestamps against each test process start and records the exception; counters, identities, states, eligibility, and geometry remain exact comparisons. These tests use isolated synthetic responders, not real recovery or live market acceptance.

The standalone calendar popup now uses the API session date for its today marker and respects `DateOnly` years 1 through 9999. There is no workstation-date fallback. Its adapted egui MIT notice is embedded in the single executable and verified by the PE package audit. The API-calendar responsiveness integration case passed; the rebuilt calendar package passed dependency/license audits. A further audit found lookup creation stamping workstation `CreatedOn`; that stamp has moved to the native service's API clock, with a stale-draft timestamp regression added and verification underway.

The stopped Iron Condor desktop comparison passed after accounting explicitly for the two horizontally clipped neighboring strategy labels exposed by egui's flattened accessibility tree. The live/update comparison found one remaining display mismatch: native success text replaced the trade status with “Live feed ON,” while hybrid retained “Open.” Correct that native publication and repeat the full stopped/live/update comparison before accepting the monitor. Missing required captures now fail the comparison tool; its three regression checks pass.

**Current verification checkpoint:** native controllers, services and event consumers are wired, including opening/closing submission, EOD, account qualification, saved-position risk enrichment, market-open eligibility refresh and preview-lease cleanup. The latest isolated controller run passed **52/52 NATS cases** and the focused native checks; the managed stores reopened **127 native financial journals** (`.artifacts/standalone-controller-acceptance-final.log`). An earlier full native run passed **219 tests** but found two controller failures; both were corrected and the full 52-case controller run was repeated successfully. Do not describe the earlier combined command as passing. Later paragraphs that list those controllers as future implementation are chronological checkpoints, superseded by this entry.

Desktop acceptance is now in progress through `Test-StandaloneDesktopParity.ps1`. It starts an isolated loopback NATS broker and in-memory C# responder, launches only the explicitly selected test executable, retains FlaUI trees and owned-window screenshots, and checks graceful exit. The hybrid shell and initial Reference navigation passed; complete paired workflow acceptance remains pending. The first fixture runs exposed missing hybrid-only startup/readiness/lookup-name reads, which were added only in opt-in desktop mode; strict native protocol assertions remain enabled in normal tests. No live feed, real order, financial posting or production service was invoked.

Paired FlaUI checkpoints now passed for Reference (four captures: lookup editor, cancelled lookup draft, populated catalog and parameter sets), Portfolio (three captures: policies, versions and funds), Portfolio draft cancellation (two captures), System (five captures with the API-clock default exception below), and the recovery-failure dialog (one capture, including caption/message/OK geometry). Evidence folders are respectively `reference-430d9d341e1945a58f93673d2627b280`, `portfolio-d58ed2d63fd5458088ad8c43badca130`, `portfolio-editors-8336ea7fdd0d490bb9af6db9fabc642a`, `system-236aa83c38b5428fa863be30a31a8a46` and `recovery-notification-89acd5841e0949abaeb4671abc5d08e3`, beneath `.artifacts/standalone-desktop-parity`. Reports distinguish exact matches from approved clock differences. The recovery scenario injects a terminal event; it does not measure a real 120-second backend recovery. Complete feature coverage and a final single-artifact pass remain required.

The oracle now exports **50** typed fixtures. Focused checks passed seven codec tests, four notification tests, the pending-calendar recovery-notification integration case and both workspace scope/publication integration cases. One initial workspace filter selected zero tests; that invocation is not acceptance evidence. The verification script now rejects zero executed tests and compiles the integration binary before starting its finite-lived responder. A desktop responder exception or unhandled request also fails its scenario even when controls rendered.

The untouched hybrid still selects some chart/calendar timestamps from wall time. The standalone must retain the user's API-only business-clock requirement; these differences require explicit evidence rather than weakening standalone date ownership. The desktop fixture accepts the hybrid's bounded chart window without rewriting its payload. This is not proof that the hybrid's clock source is correct.

Latest controller checkpoint: native opening submission passed normal completion, lost Portfolio reply, lost lifecycle reply, journal reopen and exact-identity reconciliation (`.artifacts/order-submission-integration-final.log`). Reduce-only closing now checks the current backend position, exact opposite remaining quantities, original leg IDs and accepted target position; its isolated C# hash/mapper/lifecycle and lost-reply journal case passed (`.artifacts/closing-order-integration.log`). Monitor queries use the existing MarketData Host NATS contract and API dates. Monitor subscriptions are independent of the root editor lock; EOD updates, stale/wrong-scope rejection, interrupted-open cleanup and interrupted-live-toggle cleanup passed (`.artifacts/monitor-controller-integration.log`, two cases, plus ten codec/cache checks). The desktop binary type-check passes with this wiring (`.artifacts/eod-runtime-check.log`). EOD dialog verification, the final reachable-workflow audit, complete regression runs, FlaUI comparisons and packaging remain outstanding. These are isolated synthetic protocol checks, not live-trading or desktop acceptance.

The EOD dialog now uses an additive `TradeOrderQuery.GetTradeEndOfDayDisplay` query for exact decimal display totals. It preserves `TradePnl = RealizedPnl + UnrealizedPnl` and `FundBalance = AvailableCash + TradePnl`. It does not authorize an EOD transition or replace ledger state; the existing strategy-position actor seals the position. Success, malformed returned position evidence and lost acknowledgement with authoritative reload passed (`.artifacts/eod-controller-integration.log`); the Trade domain build passed with zero warnings/errors. The hybrid EOD implementation remains unchanged. The closing workspace confirmation/submission/reopen case also passed (`.artifacts/closing-workspace-integration.log`).


**Approved parity exception — Portfolio Risk Policy:** the standalone sends `PolicyVersion`, `ExpectedPolicyRevision`, and `ExpectedPortfolioRevision` according to the backend's named contracts. It also sends the retirement policy version and aggregate revision in their correct fields. The hybrid bridge currently reverses version and aggregate revision in activation and retirement calls. The user approved correcting the standalone and documenting this exception while leaving the hybrid unchanged. Equality with the hybrid's malformed requests is not an acceptance requirement; typed backend validation of the corrected requests is required.

Portfolio progress: native selection, backend performance reports, policy editing and policy commands are implemented. The isolated policy command test passed creation, versioning, activation with distinct version/revision values, stale-generation rejection, failed validation retaining a draft, lost acknowledgement without automatic resubmission, authoritative reload, and confirmed-save/failed-refresh handling (`.artifacts/portfolio-policy-command-integration.log`). Full editor-document parity, the remaining administration/financial/trade controllers, and desktop acceptance are still in progress. Typed pending financial journals have passed unchanged managed-store validation; retained command rehydration is being verified separately. These are development checks, not FlaUI acceptance or a completion claim.

The subsequent Portfolio gate passed **five isolated NATS cases**, plus seven codec tests, the policy-contract check and the typed financial-journal test (`.artifacts/portfolio-financial-and-fund-integration.log`). Full policy and Fund editor documents match the unchanged managed session. Administration creation/version/state/delete commands preserve server-allocated identities and revisions. Fund allocation, envelope, assignment and version commands pass typed backend checks. A failed or ambiguously acknowledged Fund attachment retains a separate request and retries attachment without recreating the mandate. Financial panels match the managed balances/transactions/reservations/journal document and reject cross-scope cursors or changed revision cuts. Rehydrated posting/configuration requests match every serialized C# field. Posting/ledger-control orchestration, trade owners and consumers, full regressions, packaging, and FlaUI comparisons remain unqualified.

The financial-operation gate subsequently passed commit, lost acknowledgement, acknowledgement without commit, unavailable receipt service, expiry, and counterfeit-receipt cases (`.artifacts/financial-operation-integration.log`). Posting preparation passed typed backend rule, authority, fingerprint and exact-request checks (`.artifacts/portfolio-posting-integration.log`). The ledger gate passed eleven configuration actions against the unchanged managed preparation methods, nine managed form/confirmation comparisons, and invalid-input checks with zero command dispatch (`.artifacts/portfolio-ledger-integration.log`). Windows date display, backend DateOnly limits, policy field separation and managed journal compatibility passed. The authority review's timestamp precision discrepancy was corrected and its comparison rerun successfully. These are isolated service/controller checks, not desktop or live-trading acceptance. Saved-operation reopening, Fund subtabs, remaining trade owners/consumers, full regressions, packaging and FlaUI acceptance still require verification.

The next Portfolio gate passed **ten isolated integration cases**, including saved posting reopening without another dispatch, modal policy/discard forms, inline Fund financials/risk history and the embedded risk evidence document (`.artifacts/portfolio-completion-integration.log`). Embedded risk JSON also matches transported managed observations with and without a completed calculation; its metadata preserves serializer property order, conditional omissions, exact decimal scale and time formatting. Scheduler optional bounds now use API time, and the scheduler CRUD/history/output/notification/unknown-acknowledgement regression passed (`.artifacts/standalone-clock-and-risk-verification.log`). Trade workspace reads passed scope, duplicate/cursor, failed-refresh and latest-mandate checks. The initial native workspace publication matches the unchanged hybrid snapshot formatter, with correlated request replay and stale-selection rejection (`.artifacts/trade-workspace-controller-integration.log`).

Manual order creation, backend trade-ID allocation, trade addition/removal and order deletion passed **three isolated integration cases** (`.artifacts/manual-trade-integration.log`). Lost acknowledgements and unavailable reconciliation reads preserve the original operation without republishing. More than 256 blocked retry attempts cannot exhaust retained receipts or prevent reconciliation. Deletion requires explicit typed NotFound evidence, rather than an unavailable read. Exact option selection also passed against the unchanged managed helper, including frozen/stale quotes, UTF-16 ordinal ties, full decimal range and 28-digit fractional strike distances (`.artifacts/option-selection-verification.log`). These are service/controller checks, not desktop acceptance.

The Blotter market services now cover owner-scoped option-chain acquisition/release, exact selection, retained display snapshots and correlated backend previews. The chain integration gate passed lost-reply, failed cleanup, duplicate/wrong-scope and preview-revision cases (`.artifacts/option-chain-integration.log`). Six-strategy manual role selection and applied startup-default selection passed offline comparisons (`.artifacts/composer-market-verification.log`). Account/order/execution reads and parallel draft initialization passed scope, missing-record, EOD fallback and shared-deadline cases (`.artifacts/broker-read-integration.log`, `.artifacts/broker-initialization-integration.log`). Market/selected-leg wire documents and preview labels match the unchanged hybrid formatters, including exact decimal strings and seven-digit offset timestamps (`.artifacts/broker-display-wire-verification.log`). The native futures Blotter publication also matches the unchanged hybrid `SnapshotAsync`, with price-edit, invalid-input, stale-incarnation and close checks (`.artifacts/native-composer-integration.log`). These checks use isolated in-memory API responders and have not submitted a real order.

A protocol audit identified that actor command replies contain `GuidResult`, while several early responders had incorrectly emitted the already-unwrapped client API GUID. The native receipt adapter and affected Portfolio/manual responders were corrected. The accumulated offline run passed **215 native tests**; its protocol phase exposed one shared-fixture catalog leak, which was isolated. The protocol rerun passed **43/43 cases**, including all affected command workflows and the new four-leg Iron Condor publication comparison (`.artifacts/standalone-protocol-regression-20261024.log`, `.artifacts/standalone-protocol-regression-final-20261024.log`). The managed stores also reopened 76 native operation journals successfully. Desktop quote ages use published API instants, following the explicit API-clock requirement; the original hybrid renderer remains unchanged.

Further Trade Blotter progress: account qualification commands retain uncertain operations across close/reopen and reconcile their original receipts; report preview/hash loading is implemented (`.artifacts/broker-account-controller-integration.log`, two isolated cases). Working-order evidence is compared directly with unchanged hybrid `EvidenceRows`, including captions, identity getters, ordered properties, exact decimal limits and fill rows. Repricing/cancellation retain their event watches across view disposal, reject off-tick prices before publishing, and resolve only correlated replies/events. Backend order preparation executes unchanged `TradeOrderPreparationCompute` over synthetic fixtures; native confirmation displays those returned amounts/fees, freezes the reviewed draft, and cancellation publishes no execution. These two cases passed in `.artifacts/order-controls-preparation-integration.log`. The native submission journal/lifecycle/reconciliation implementation is being tested separately; it is not yet claimed accepted. Original hybrid source remains unchanged. Established-trade monitoring, the remaining inventory review, side-by-side FlaUI acceptance and single-executable packaging are still required.


The native options Blotter now loads applied backend defaults, acquires the chain under its own owner, publishes source-matching market metrics/legs/preview, switches frozen/live intent, preserves manual clearing and releases its owner on close. Its full initial publication matches the unchanged hybrid snapshot, with invalid-width and lifecycle checks. Qualification services cover all six actions, known rejection, wrong receipts/events, event-before-reply and late committed-event reconciliation. Account operations survive Blotter close/reopen in their workspace owner. Working-order controls, confirmation/submission, established-trade monitoring and event consumers, final regressions, packaging and desktop parity remain incomplete.

- Actor Health now reads the existing HTTP endpoint and sends all eight supported Supervisor operations through native typed NATS requests. Controls require the configured requester, current backend authority and the selected mailbox generation. Stale selections, rejected commands and unknown acknowledgements are tested. Native display rows match the unchanged hybrid `ActorHealthSession` oracle; this is not yet FlaUI acceptance.
- The main-screen calendar now has native API-owned dates, countries, five period selections and refresh events. Calendar publications update only their own fields. Status-console notifications retain their event timestamps and appear as console rows rather than modal errors.
- Shell feed controls use the API session and existing Start/Stop commands, subscribe before submission, and reconcile missing completion events against runtime status within one deadline. No command is automatically retried. These controls are distinct from the still-pending per-trade live-feed controller.
- Native ES/VX bar and Daily Bollinger history readers use the API clock for the six-hour window and backend-computed indicator values. The display projection matches the unchanged hybrid for empty/single histories, the DST transition, validity filters, duplicate observations, warming bands and the 40-day bound. Failed refreshes retain prior points with a visible stale/error status.
- The full offline checkpoint passed **182 tests, zero failures**, including **ten isolated NATS integration tests** and typed round trips for **24 fixtures**. Seven graphics/export tests remain skipped. The notification responsiveness case confirms that pending calendar hydration cannot delay a recovery-error publication. The existing `TomasAI.IFM.UI.Rust` source remains unchanged. Evidence: `.artifacts/standalone-full-verification-20261024.log` (development artifact, not desktop acceptance).
- Subsequent Market Outlook work adds independent five-second API refreshes, exact decimal display rounding, API-clock freshness, current-contract/date checks, and `TimeOnly` projection with 100 ns precision. Three outlook/format/contract tests pass against the unchanged hybrid presenter, and typed round trips now cover **25 fixtures**. Dashboard pipeline-health hydration also uses the API clock; stale/failed HTTP diagnostics clear green while preserving independently confirmed feed-running state. All **twelve** isolated NATS cases pass, including complete outlook hydration/rejection and HTTP diagnostic freshness/failure. Evidence: `.artifacts/standalone-dashboard-verification.log`. The remaining feature controllers/consumers and desktop qualification are still pending.
- The futures-contract editor now has native load/add/change/remove/convention review, typed field editing that preserves provider evidence and unknown trailing slots, UTC/Windows/IANA input checks, explicit catalog reload and correlated command completions, and request-generation/correlation guards. Four model/edit/display tests and all **thirteen** isolated NATS cases pass; the new case checks immutable provider identity, stale selection, rejected qualification, CRUD, unknown outcomes, confirmed-save/failed-refresh distinction and external edits. Typed round trips cover **28 fixtures**. Evidence: `.artifacts/standalone-futures-editor-verification.log`. The option-contract/provider workflows and other remaining feature groups are not declared complete.

- The option-contract editor now has native symbol-scoped 200-row paging, one bounded cursor restart, typed CRUD/convention review, immutable provider evidence and exact strike identities. The targeted gate passed seven codec tests, four model tests, two identity tests, two option-display/edit tests, six publication-ownership tests, and **all fourteen isolated NATS cases**. It includes stale selections, expired/repeated/cross-symbol cursors, qualification rejection, missing completions, and confirmed-save/failed-refresh handling. Typed round trips cover **31 fixtures**. Evidence: `.artifacts/standalone-option-integration-verification.log`.
- Reference catalogs follow the hybrid's explicit reload behavior. Transient command listeners subscribe and flush before submission; catalog refreshes do not add a new background workflow. Independently, per-view publication caching retains undelivered command outcomes across newer data publications until the view renders them. A headless test draws the actual copied editor to verify that its pending operation clears.

- Provider definitions now have a native bounded stored-page search, pinned snapshot/cursor checks, preview-to-publish correlation, single-use previews, target/underlying rereads, and typed add/change commands. Mapping and full preview documents match the unchanged hybrid output, including exchange-local expiry dates, exact decimal scales, original timestamp offsets, derived identities and retained conventions on legacy reimport. Four provider tests and all **fifteen isolated NATS cases** pass; typed round trips cover **39 fixtures**. The provider case verifies happy-path future/option imports, stale selections, changed targets, expired evidence, oversized/mismatched/repeated pages and unknown completion without resubmission. Evidence: `.artifacts/standalone-provider-integration-verification.log`. This is protocol/projection evidence, not desktop acceptance.

- Scheduled tasks now have native dashboard/catalog projection, previews, schedule CRUD/enablement, manual run submission, uncertain-run resolution, bounded schedule-scoped histories/output, and notification/reconnect refresh. Native run receipts and initial screen documents match the unchanged hybrid oracle. Two projection/input tests and **all sixteen isolated NATS cases** pass; typed round trips cover **42 fixtures**. The scheduler integration checks revisions, API-clock run/resolution timestamps, disabled-before-removal, missing output, exhausted/repeated cursors, lost/malformed acknowledgements, and accepted-save/failed-refresh outcomes. Administrative tests never launch a real task. Evidence: `.artifacts/standalone-scheduler-integration-verification.log`. Full feature coverage and FlaUI comparisons remain incomplete.

- Consolidated offline verification passed **208 tests, zero failures**, including all **16 isolated NATS integration cases** and typed C# round trips for **42 fixtures**. Seven graphics/export tests remain skipped; the initial ignored NATS cases are subsequently executed by the isolated harness. Evidence: `.artifacts/standalone-full-verification-20261024-controllers.log`.
- Subsequent scheduler protocol checks compare omitted command fields against the real C# type initializers, preserve the default query page size, and require reload after an unknown acknowledgement. All 16 NATS cases plus focused scheduler/model/codec checks pass: `.artifacts/standalone-scheduler-defaults-verification.log`. A final focused gate passed seven codec checks and six model/boundary checks, including malformed offsets that previously could overflow before validation: `.artifacts/standalone-reference-boundary-verification.log`. The 79 original UI source hashes still match the recorded baseline. No desktop acceptance or complete migration is claimed.

- Database backup administration now has native configured-set/dashboard/history projection, bounded output, backup/cancel/restore-drill/policy commands, source-scoped history and notification/reconnect refresh. C# operation/policy/configuration comparisons, two backup checks, the eight-variant parameter-field comparison and seven codec checks pass. All **17 isolated NATS integration cases** pass, including backup acceptance versus completion, uncertain/malformed acknowledgements, no automatic write retry, output/cursor bounds, cancellation, restore drills, revision-matched policy saves and accepted-write/failed-refresh behavior. Typed round trips cover **46 fixtures**. Evidence: `.artifacts/standalone-backup-parameter-verification.log`. The responder never runs real backups. This is not FlaUI acceptance.
- The parameter field adapter matches the unchanged hybrid field list, display and scalar edits for all eight typed parameter variants. It retains untouched raw slots and rejects duplicate/identity edits. Parameter controller/service implementation is in progress; this field-adapter evidence does not certify the complete parameter workflow.

- Native parameter controllers now cover detached typed drafts, backend preview/validation, lifecycle writes, assignments, startup reports, monitoring and legacy import paging. The initial integration gate passed all **18 cases** (`.artifacts/standalone-parameter-integration-verification.log`). A subsequent focused parameter case also passes audit-based assignment reconciliation after lost/malformed acknowledgements, request-generation checks, legacy import and authoritative commit recovery (`.artifacts/parameter-edge-verification.log`). These are isolated protocol tests, not desktop acceptance. Receipt space is reserved before writes; uncertain requests retain their original operation IDs.
- Strategy history/ITI ownership now hydrates complete workflow payloads, subscribes before reading, preserves API date/time scope, and handles duplicate/stale notifications, revision conflicts and reconnect repair. The focused strategy integration case passes (`.artifacts/strategy-native-focused.log`); the basic row and complete details tree match the unchanged hybrid fixture. Observation/risk controllers compile and their history/evidence/identity/failure-retention integration case passed in the expanded run, which separately exposed a parameter test's stale-generation input (subsequently corrected and re-run). Full evidence-text comparisons are still being extended; this is not a claim that all strategy result variants or UI workflows are accepted.

**Next native owners:** finish parameter sets, strategy/ITI details and history, portfolio/financial workflows, order composition/execution, and established-trade monitoring/history/live-feed workflows with their event consumers. Complete these before hybrid-versus-standalone FlaUI acceptance, final packaging and user-controlled cutover.

Code comments and the standalone README document API clock ownership, plot-only floating-point conversion, independent publication owners, generation checks, cancellation and unknown command outcomes. Remaining controllers and consumers must be completed before the requested hybrid-versus-standalone FlaUI workflow comparisons begin.

| Implemented area | Native source/evidence | Remaining qualification |
| --- | --- | --- |
| Independent screen copy | `TomasAI.IFM.UI.Rust.eGui/src`, embedded catalog and copied rendering tests; original-source hashes in `migration/screen-baseline.json` | All feature data/action owners must be native before parity is complete |
| Native executable/runtime | Separate `ifm-ui-egui` Cargo package; background Tokio runtime, bounded UI queues, explicit configuration, responsive shutdown, visible runtime errors; no hosting module or CLR loader | Complete controllers, scoped late-result fencing, reconnect hydration and clean-Windows package acceptance |
| Backend protocol | `backend/codec.rs`, `contracts.rs`, `schema.rs`, `transport.rs`; MessagePack-CSharp 3.1.9/LZ4 fixtures and isolated C# NATS responder | Golden samples do not certify every exported contract; close the reachable operation registry |
| Exact identity foundation | Decimal validation, .NET GUID byte-order adapters, canonical financial JSON/hashes and generic atomic journal storage | Integrate typed financial request preparation, validation, both existing journal formats and reconciliation into native financial controllers |
| API date/session and feed status | `services/business_context.rs`, `feed_status.rs`, `notifications.rs`; API-only ValueDate, bounded initialization, independent status polling, subscription/transport errors and recovery-failure dialog publications | Complete shell/market/trade hydration, live feed controls, EOD/rollover and desktop recovery acceptance |
| Lookup editor | `state/lookup.rs`; native load/add/change/remove, event-before-reply correlation, duplicate request checks and unknown-outcome preservation | Actual configured API/desktop comparison in addition to the isolated protocol/controller tests |
| Strategy Catalog | `state/strategy_catalog.rs`; drafts, immutable identity/version checks, save/publish/retire and authoritative verification | Full configured-backend/desktop comparison, restart/reconnect and working-copy scope acceptance |
| Yield curves | `state/yield_curve.rs`; API-date filters, duplicate-date checks, native mutations/import completion | Complete integrated/desktop acceptance including real backend import outcomes |
| Economic-calendar editor | `state/calendar.rs`; published countries, Eastern-day filters, UTC event precision, CRUD and backend import; terminal events subscribed before submission | Configured-backend/desktop comparison and complete sidebar/calendar-consumer hydration |
| Operations health | `backend/health_http.rs`, `state/operations_health.rs`; bounded existing HTTP protocol, API-clock freshness, existing display rows and visible failures; single-flight refresh while visible | Actor supervision controls and configured endpoint/desktop comparison |
| Publication ownership | `state/publications.rs`; independent per-view generations and cached rows; late replies cannot replace another screen | Full per-dialog incarnation/cancellation and reconnect ownership for remaining controllers |

The development-only `tools/ContractFixtures` exporter uses the installed C# serializer and compiled types. It also records public API client methods and source routing expressions in `tests/fixtures/backend/operations.json`. That registry includes methods outside the active Rust UI: a pending row requires reachability review or a native port, rather than automatic migration of unused APIs. Reflected schemas are metadata evidence, not a claim that every method/controller has been ported.

Reproduce offline verification with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/UI/Rust/Verify-StandaloneRust.ps1
```

This builds the development-only fixture tool, runs copied/native Rust tests, verifies Rust output through typed C# deserialization, and runs native controller tests against an isolated loopback NATS broker and C# responder. The responder uses in-memory records; it does not run production domain actors or establish live-screen acceptance. The script cleans up its own helper and broker. It does not restart the Development stack, start market feeds, or send real trading/ledger requests.

`scripts/UI/Rust/Build-StandaloneRust.ps1` builds only the new package, statically links the Visual C++ runtime and assigns the requested Windows executable filename. The development-only PE audit rejects CLR headers, managed runtime imports and dynamic Visual C++ runtime imports, and records an artifact SHA-256/import report. This static audit does not replace clean-Windows or desktop qualification. Configuration is external (`IFM_UI_NATS_URL`, optional NATS credentials/token, request timeout; `IFM_UI_OPERATIONS_HEALTH_URL` for that existing HTTP endpoint). Unsupported standalone actions currently reject visibly and send no backend command. Do not interpret screen availability or a successful build as full feature completion.

Controller evidence now includes isolated calendar import/add/change/remove, exact fractional event timestamps, stale-generation rejection, missing completion and malformed acknowledgement handling. Unknown writes do not become ordinary failures or automatically receive replacement IDs. Catalog retries retain the original effective instant and operation ID until authoritative verification; different changes cannot overwrite an uncertain operation. These editor histories are currently in memory. Durable financial request journals and restart reconciliation still require their feature-specific adapters and acceptance.

The 2026-10-23 offline verification pass completed with **171 tests passed, zero failed**, including all five isolated NATS/controller tests. Seven graphics/export tests remain skipped; no live-screen parity or trading acceptance is implied. The fixture tool also verified typed Rust-to-C# round trips for all 22 exported wire fixtures. All 79 original screen/library/test files still matched their recorded source hashes, and the existing `TomasAI.IFM.UI.Rust` working tree had no changes. This is evidence for the implemented subset, not completion of the feature matrix below.

Remaining feature groups include market/ITI/chart hydration and consumers; contract/calendar/provider/parameter editors; actor controls, scheduled tasks and backups; portfolio and financial administration; order browsing/composition/execution; established trades, positions, plans, six-hour chart history and live monitoring; complete scope/reconnect/journal ownership; and desktop/package acceptance. Preserve the approved formulas and backend ownership while implementing those groups. Do not change the default ServerManager UI or retire the current UI until the user approves final cutover after both versions are verified.

| Refreshed model/view-model scope | Files |
| --- | ---: |
| `UI.Net.ViewModels` | 81 |
| `UI.Net.Models` | 17 |
| **Total model/view-model source files** | **98** |
| Direct Rust bridge reference | 58 |
| WinForms reference without a direct bridge reference | 19 |
| Indirect production reference requiring entry-path review | 16 |
| Test references only | 3 |
| No external type/method identifier reference found | 1 |
| C# global import infrastructure | 1 |

The census classifies `PortfolioConcurrencyRefreshModel.cs`, `PortfolioNavigationModel.cs` and `PositionEntryWindow.cs` as test-only references. The native eligibility responsibility formerly represented by the last helper now uses API context. `Trade/IronCondor/IronCondorTradeInfoViewModel.cs` declares `IronCondorTradeInfoReadModel` and has no external identifier reference in this scan. `GlobalUsings.cs` is import infrastructure. These classifications do not authorize deletion. Extension-method call identifiers are included so helpers such as `ObservableUiServiceExtensions` are not mistaken for unused classes.

The supplemental checklist covers **199 files**: 52 UI service files, 39 UI event-consumer files, 1 composition file, 53 NATS API client files and 54 managed bridge files. It includes publication and session models outside the primary model projects. The visual baseline contains 134 WinForms view-project source files and 49 Rust source files. These counts include helpers and partial/designer files and are not counts of distinct screens or wire operations.

### Ownership rules

| Responsibility | Final owner |
| --- | --- |
| Rendering, focus, tabs, selection, window geometry, colors, formatting and editable drafts | Rust egui thread and typed view state |
| Queries, commands, function requests, subscriptions, reconnect, correlation and client journals | Rust background runtime and scoped controllers |
| Authoritative valuation, Greeks, payoff/maximum loss, trade plans, risk decisions, financial posting validation and persistence | Existing backend actors/services; add backend operations for uncovered client calculations |
| ValueDate and market-session dates | API server exclusively; no local value-date provider or machine-date fallback |
| UTC instants, display-zone conversion and monotonic client durations | Preserve wire timestamps; convert for display; use monotonic time for client timing. Do not substitute UI time for server business dates |
| Option feed ownership, lease reconciliation, two-minute recovery and one-time API restart | Existing backend/ServerManager workflow; Rust presents state and errors and reconciles after reconnect |

All financial and identity values retain exact representations. Floats are permitted for plot coordinates and geometry, not authoritative currency, quantities, strike prices or canonical hashes. UI enable/disable rules can mirror server state for usability; backend authorization and business validation remain mandatory.

### Feature and behavior migration matrix

Supported current-Rust feature rows are **planned standalone Rust work**, even where the hybrid already renders the screen. Explicitly excluded WinForms-only rows are scope evidence.

| Area | Managed behavior to account for | Rust target / required gate |
| --- | --- | --- |
| Shell, navigation and startup | `IFMAppViewModel`, `IAppRoot`, navigator/dispatcher/interaction contracts, `RustShellViewModelSession`, dashboard/sidebar sessions | `app` and `state/shell`; API-owned ValueDate, confirmed feed state, recovery/initialization error dialogs, settings and shutdown |
| Market outlook, ITI and charts | EOD/trade-signal display models, `MarketDataFeedHealthMonitor`, `MarketChartSession`, `MarketOutlookPresentation`, graph-window models | `state/market`; preserve source timestamps, provisional indicators, baseline/stale status, graph gaps and colors |
| Economic calendar | Both calendar view models, editor/readonly views and calendar session | `state/reference/calendar`; same filters, CRUD, timestamp identity and cancellation |
| Contracts, provider mapping and yield curves | Contract editor/view models, instrument selector, convention editor, provider mappings and draft review | `state/market/reference`; qualified selections, paging, explicit Draft import confirmation and server dates |
| Reference manager | `ReferenceViewModel`, lookup editor, parameter-set editors and comparisons, strategy catalog session | `state/reference`; preserve Reference as the WinForms manager flow, without adding menu items underneath the Reference menu |
| Strategy/workflow details and Risk History | Strategy operations/details/presentation, assessment/risk presenters, inspection backend and sessions | `state/strategy`; stable identities, cached trees, exact server decisions and current evaluation-date flow |
| Portfolio/fund administration | Administration and metrics view models, navigation/identity/allocation/fund editor models, portfolio session partials | `state/portfolio`; all create/edit/allocation/risk-envelope/template-assignment paths and approved dialog layouts |
| Financial ledger and posting | Financial view models, drafts, control preparation, operation state and both pending-operation stores | `state/portfolio/financial` plus journal adapters; exact request identity/hash, concurrency revisions, uncertain outcomes and restart reconciliation |
| Order browser and fund orders | `TradeOrderEditorViewModel`, `FundOrderEditorViewModel`, order editor models and workspace session | `state/trading/orders`; server paging, selection, date filters, lifecycle and acknowledged actions |
| Broker/manual composition | `BrokerManualTradeOrderViewModel`, initialization/preview/leg-selection/mutation helpers, confirmation, `TradeComposerSession` | `state/trading/composer`; complete preview, account qualification, component identity, backend-calculated risk, broker acknowledgement and outcome reconciliation |
| WinForms-only Iron Condor entry (excluded) | `IronCondorTradeOrderViewModel`, legacy entry view, trade-info models and local Greeks calls | Current Rust uses `BrokerManualTradeOrderViewModel`; no backend clone or standalone port of the unused WinForms entry model is required |
| Established trades, positions and plans | `IronCondorViewModel`, trade-info/spread-bar models, monitor row/cache/update helpers and `TradeWorkspaceSession` | `state/trading/monitor`; API ValueDate, saved live-feed intent, P&L bars using maximum gain/loss, valid-value preservation, EOD refresh and error presentation |
| Spread history | Range query/client service and chart merge in the workspace | Seed six hours across trading-value-date boundaries using API time, preserve baseline on failure, merge buffered live updates, retain once-per-minute history sampling |
| Actor and market-data operations health | Actor control/health snapshots, operations-health view model/session and HTTP health adapters | `state/operations/health`; preserve permissions, generation checks, lifecycle actions and visible failures |
| Scheduled tasks, backups and System | Scheduled-task and backup view models/models/sessions, `SystemAdminViewModel` | `state/operations`; complete setup, scheduling, preview, execution confirmation, logs/history paging, progress and cancellation |
| Status console and notifications | Status-console model/services, terminal correlation, all UI event consumers and initialization/recovery notices | `services/notifications`; deduplicate per operation/outcome and scope, retain visible terminal failures, do not confuse indicator failure with feed shutdown |
| Shared lifecycle/presentation helpers | Observable/base-editor/async-operation classes, presentation errors/colors, lookup extensions, field editors and time conversions | Rust ownership, enums, state transitions and formatting; retire C# infrastructure only after each dependent feature is qualified |

### Concrete gaps found in the refreshed review

Execute the [current Rust UI backend processing implementation plan](IFM-Current-Rust-UI-Backend-Processing-Implementation-Plan.md) before standalone client cutover. It maps B1-B7 to existing actors and requires current API/managed-client/desktop verification.

1. **Close the seven reviewed backend boundary groups.** The full audit identifies B1 order candidate risk/deployment/tick preparation, B2 confirmation amounts/fees, B3 fund performance metrics, B4 realtime cache financial summaries, B5 saved-position/history valuations, B6 composer financial summaries, and B7 server-owned business clock/session/range policy. `SelectedOrderPriceRisk.MaximumOptionLoss` is one part of B1, not the complete list. Reuse existing backend preview and realtime calculations. The WinForms-only `OptionPricingService.CalculateGreeks` path does not require a new backend equivalent for this migration.
2. **Port financial request helpers to Rust.** `FinancialControlPreparation` and `FinancialPostingPreparation` assemble requests, prechecks, expected versions and canonical hashes; these can remain client-side. Existing backend ledger validation and persistence remain authoritative. Preserve accepted contracts, exact decimals and hashes; no new financial preparation endpoint is needed solely to replace these helpers.
3. **Pending journals require compatibility.** `PendingFinancialOperationStore` and `PendingFinancialConfigurationStore` retain exact requests and uncertain operation IDs under the user profile. Port loading, serialization, atomic replacement, locks, size bounds and reconciliation. Never generate a replacement operation ID because the UI restarted or transport timed out.
4. **Managed orchestration exists outside view models.** Bridge sessions directly access APIs, format publications, cache rows, apply update generations and dispatch commands. `UiServiceCatalog`, client composition, API clients and UI event consumers are part of the migration boundary. Porting only the model projects would leave a managed dependency.
5. **Preserve recent runtime fixes.** Include API-only ValueDate, durable live-feed restoration, two-minute option/API recovery messages, EOD updates, six-hour chart range/merge, latest-value queue behavior and daily indicator baseline/stale/error reporting in the acceptance registry.
6. **An old rendering port is not standalone qualification.** Most native views consume managed MessagePack publications today. Reuse their geometry/styles/widgets while replacing their provider and action owner. Fixture rendering does not verify broker actions, field validation, backend queries or subscription cleanup.

### Implementation phases

| Phase | Deliverable | Completion gate |
| --- | --- | --- |
| 0 — Freeze scope and contracts | Reconcile every inventory row against menu routes, construction/registration and bridge dispatch; record each command/query/function/event, endpoint, payload, identity, hash and owner | No unclassified active model, helper, bridge action or event path; ambiguous/dead-code candidates documented separately |
| 1 — Create the single Rust application | Create `TomasAI.IFM.UI.Rust.eGui`; one UI binary/event loop, embedded assets/catalog, typed feature modules, background task supervision and configuration | Starts and closes on Windows without loading hostfxr, CLR or any managed UI assembly; old hybrid stays available for comparison |
| 2 — Implement direct backend compatibility | Rust request/reply and subscriptions over existing NATS, MessagePack-CSharp/LZ4 compatibility, exact IDs/decimals/dates/enums and existing HTTP health adapters | Cross-language golden payloads and actual isolated NATS round trips, compression boundaries, cancellation, errors, tracing headers and reconnect |
| 3 — Close backend preparation gaps | Complete B1-B7 in the backend boundary audit; reuse existing preview/realtime engines and exclude unused WinForms pricing paths | UI consumes server-prepared financial values and business context; integrated tests cover risk/fees, metrics, saved/live projections and server-controlled dates; financial request helpers remain portable client code |
| 4 — Port shared client state and persistence | Scoped view lifetimes, operation correlation, unknown-outcome reconciliation, compatible journals, caches and queue policies | Deterministic restart/reconnect tests; latest market snapshots can coalesce while commands, financial outcomes and terminal errors remain reliable |
| 5 — Port feature groups | Shell/market/reference; strategy/inspection; operations; portfolio/financial; order composition; established monitoring, following the matrix | Each inventory row has a Rust owner or an approved replacement, and each supported view/action has source-linked parity evidence |
| 6 — Full integration and desktop qualification | Real API query/event workflows, isolated/emulator order and ledger tests, both recovery branches, startup/close/open/reconnect and all supported forms/dialogs | Existing API + Rust client integration passes; FlaUI/AccessKit UIA checks and Rust widget/visual checks cover geometry, controls, actions, accessibility and failures |
| 7 — Package and cut over | Produce `TomasAI.IFM.UI.Rust.eGui.exe`; update VS Code tasks, build/package scripts, ServerManager process paths and diagnostics | Package starts without .NET installed and has no managed/helper UI process; secrets remain external; full acceptance evidence recorded before legacy retirement approval |

Phases are dependency ordered. Feature groups can be implemented incrementally, but the final executable cannot call back into the old managed bridge. Copy tested Rust view code without requiring the old hybrid executable or modifying its source/build. Do not remove WinForms or the hybrid.

### Testing, acceptance and inventory closure

- **Protocol:** both directions for each reachable wire contract, MessagePack numeric/map keys, extension values, LZ4 framing, null/default fields, UTC/DateOnly/time spans, enum/union values, decimals and canonical hashes. Offline C# fixture generators may remain test tools; they are not runtime dependencies.
- **State:** pending/loading/success/failure/cancelled/unknown outcomes; request identity across retry; late results after closing/reopening a view; stable selections; subscriptions detached exactly once; completed requests removed from bounded history.
- **Market/monitor integration:** API-provided ValueDate at rollover and historical dates; six-hour database history before/alongside buffered live updates; no clearing a valid baseline on failed refresh; valid values survive N/A updates; independent dataset recovery; indicator baseline and error notices.
- **Trading/financial:** account/environment qualification, emulator isolation, immutable component IDs, broker acknowledgements, no blind retry after an uncertain submission, both journal formats and server rejection handling. Do not use real orders or financial writes merely to establish UI parity.
- **Desktop:** inventory all reachable forms and runtime-generated controls; preserve approved Rust color conventions, layout/dimensions, labels, selection, keyboard/focus, enabled states, scrolling and border containment. FlaUI is an external Windows acceptance runner; Rust widget tests and screenshots supplement controls that UIA cannot individually address. Missing accessibility evidence remains an open gate.
- **Packaging:** inspect imports and loaded modules; test on a clean Windows environment without .NET; verify exactly one UI process and no hostfxr/bridge load, no local pricing engine and no connection from the UI to a database. Configuration/journal/log files do not count as extra executable processes.
- **Performance:** keep I/O and expensive projection off the egui thread; measure allocation rate, resident memory, queue depth, frame time and end-to-end update latency. Bound grids by viewport and server paging. Use latest-value replacement only for disposable market display updates, with per-entity identity; never evict financial commands or terminal notifications as if they were quotes.
- **Closure:** every CSV row needs a final Rust module/backend owner or a justified inactive/merged disposition, its operation mapping and test evidence. Repeat the inventory before cutover to detect newly added models or routes. Static counts alone cannot certify full migration.

### Feasibility conclusion

**A complete migration to one Rust executable is feasible.** Existing native egui views provide a substantial rendering base, and the backend already supplies command/query/event boundaries. The work is a client orchestration, contract and state-lifetime migration as well as a rendering migration. The remaining local financial helpers and request/journal compatibility are concrete implementation dependencies, not evidence that a second UI process or a CLR must remain. Full standalone parity is not yet verified; closure requires the per-row and per-feature gates above.

## Historical WinForms/hybrid migration record

Date: 2026-10-13. Windows first; WinForms remains supported. Full migration implementation and testing authorized by the user on 2026-10-13. WinForms remains available during migration.

## Pilot architecture and ownership

The Rust executable hosts .NET 10 through hostfxr. C# owns service calls, workflow query hydration, notifications, reconciliation and view-model lifetimes. Rust owns immutable display snapshots, cached navigation, grid selection and rendering. StrategyOperationsComposition is shared by the WinForms Startup and live Rust bridge. The existing StrategyOperationsViewModel is reused; initialization now clears a previous backend error to allow reconnection status to recover.

The bridge owner publishes completed snapshots. Live selection uses stable per-workflow action tokens, rather than a row's transient index. Render functions never perform queries. Grid datasets transfer as one bounded publication (16 MiB maximum); future datasets exceeding that size need chunked, versioned transfer before adoption.

One canonical catalog generates C# and Rust keys. Financial display values retain invariant exact decimal strings; display sorting does not mutate them. Dates are ISO UTC strings. Microsoft Sans Serif 10pt is loaded from Windows, without redistributing the font.

## Dependency and lifecycle inventory

| Component | Reuse | Owner / shutdown |
| --- | --- | --- |
| StrategyOperationsViewModel | Existing source unchanged | Managed session; dispose subscriptions and reconciliation |
| StrategyOperationsService | Existing service through shared composition | Managed client lifetime |
| MarketDataAnalyticsQueryApi / IntrinsicTimeStrategyWorkflowQueryApi | Existing C# NATS clients | Managed transport lifetime |
| FuturesItiSignalUIEventConsumer / IntrinsicTimeStrategyWorkflowUIEventConsumer | Existing notification fanout | View scope; StopAsync before disposal |
| NatsActorProducer / NatsConnectionManager | Existing C# transport | Stop local producer, then dispose; no backend stop command |
| StrategyWorkflowPresentation | Existing immutable semantics | Adapted to typed display snapshots |
| StrategyWorkflowDetailsAccordion | WinForms-specific control | Retained; used by linked regression test |
| DarkTradingTheme | Existing WinForms implementation | Rust typography matches its font settings |
| hostfxr function table | New native boundary | Opaque handles; caller-owned buffers; contained errors |

C# owner preparation is bounded to 10,000 tree nodes, depth 12 and 200 collection entries per node, with visible truncation markers. Broad application navigation and editing are not exposed by the pilot ABI. They require view-specific action envelopes and acknowledged outcomes before those screens migrate.

## Control mapping

The source-linked [control occurrence inventory](IFM-WinForms-Control-Inventory.csv) enumerates constructors, declarations and uses throughout the view project. Occurrences are not a count of distinct runtime controls. Target-typed new expressions and custom controls must be reconciled as each screen migrates; custom rendering cannot be ported by mechanical replacement.

| Current control / behavior | Rust candidate | Acceptance |
| --- | --- | --- |
| TreeView | egui CollapsingHeader / stable node IDs | Cached branches, selection, expand/collapse and accessibility |
| PropertyGrid | Typed egui Grid; read-only pilot, explicit editors later | Exact values, units, null and read-only semantics |
| ListView / DataGridView | ScrollArea::show_rows or egui_extras TableBuilder | 100k addressable rows, viewport rendering, selection identity |
| Chart / custom plots | egui_plot | Same series, axes, UTC windows, cursor and data gaps |
| TabControl / SplitContainer | Selectable tabs / resizable panels; egui_dock when needed | Layout, resize, selected-state persistence |
| TextBox / RichTextBox | TextEdit | Validation, multiline behavior, keyboard and focus |
| ComboBox / DateTimePicker / NumericUpDown | ComboBox / egui_extras datepicker / DragValue | Typed values, limits and effective dates |
| Status circles / progress / log tree | Painter shapes / ProgressBar / tree | Exact state colors, percentage and scoped log selection |
| Modal forms / file dialogs | Explicit view states / native dialog crate | Cancellation, confirmation and owner lifetime |
| Buttons / menus / toolbar | egui widgets | Exactly one accepted intent per activation |

Compatibility metadata was checked against the crates.io registry: egui_extras 0.32.3 requires egui ^0.32.3; egui_plot 0.33.0 and egui_dock 0.17.0 require egui ^0.32. The pilot itself only needs egui/eframe 0.32.3 and egui_kittest 0.32.3. egui_plot 0.33.0 is now installed and tested by the native Strategy workspace. egui_extras 0.32.3 is now installed and tested for virtual tables and date selection. egui_dock remains a candidate. Sources: [egui_extras manifest](https://docs.rs/crate/egui_extras/0.32.3/source/Cargo.toml), [egui_plot manifest](https://docs.rs/crate/egui_plot/0.33.0/source/Cargo.toml), [egui_dock manifest](https://docs.rs/crate/egui_dock/0.17.0/source/Cargo.toml).

## Migration waves

| Wave | Scope | Main dependency / gate | Planning estimate |
| --- | --- | --- | --- |
| 0 | Shared shell, typography, catalog and host bridge | Runtime, owner thread, packaging and ABI tests | Pilot implemented |
| 1 | Strategy Details, workflow list and summaries | Real live backend / reconnect qualification | 1–2 weeks after live gate |
| 2 | Market selection, option-chain lists, ITI and chart panels | Series snapshots, volatility units, selected contract identity | 2–4 weeks |
| 3 | Reference and configuration editors | Validation and acknowledged save/cancel outcomes | 2–3 weeks |
| 4 | Trade entry/order submission, trade plans, fund and portfolio | Broker confirmations and financial action contracts | 4–6 weeks; highest-risk wave |
| 5 | Scheduled Tasks, Local/AWS Backups and System Admin | Progress/log streams and safe command outcomes | 2–4 weeks |
| 6 | Main navigation and remaining diagnostics | Cross-view lifecycle and feature parity | 1–2 weeks |

Estimates are planning ranges, not measured implementation commitments. Each wave must inventory runtime-generated controls, map bindings/handlers, define snapshots and actions, keep both clients operational and pass deterministic and desktop tests before review. Do not retire WinForms until all migrated screens have parity evidence and the user approves retirement.

## Screen allocation

The table lists view classes discovered from source inheritance. Shared base controls are included as foundations. The linked control inventory supplies the source expressions for concrete controls.

| Wave | View class | Source | Base |
| --- | --- | --- | --- |
| 6 | ActorHealthForm | [TomasAI.IFM.UI.Net.Views/App/ActorHealthForm.cs](../../TomasAI.IFM.UI.Net.Views/App/ActorHealthForm.cs) | DarkTradingForm, IForm<ActorHealthForm> |
| 6 | DarkTabControl | [TomasAI.IFM.UI.Net.Views/App/DarkTabControl.cs](../../TomasAI.IFM.UI.Net.Views/App/DarkTabControl.cs) | TabControl |
| 6 | IFMAppView | [TomasAI.IFM.UI.Net.Views/App/IFMAppView.cs](../../TomasAI.IFM.UI.Net.Views/App/IFMAppView.cs) | DarkTradingForm, IForm<IFMAppView>, IFormControl, IIFMAppLiveViewAdapter |
| 6 | MarketDataOperationsHealthForm | [TomasAI.IFM.UI.Net.Views/App/MarketDataOperationsHealthForm.cs](../../TomasAI.IFM.UI.Net.Views/App/MarketDataOperationsHealthForm.cs) | DarkTradingForm, IForm<MarketDataOperationsHealthForm> |
| 6 | MarketDataView | [TomasAI.IFM.UI.Net.Views/App/MarketDataView.cs](../../TomasAI.IFM.UI.Net.Views/App/MarketDataView.cs) | DarkTradingView |
| 6 | MarketEconomicCalendarView | [TomasAI.IFM.UI.Net.Views/App/MarketEconomicCalendarView.cs](../../TomasAI.IFM.UI.Net.Views/App/MarketEconomicCalendarView.cs) | DarkTradingView, IAsyncFormControl |
| 6 | MarketOutlookView | [TomasAI.IFM.UI.Net.Views/App/MarketOutlookView.cs](../../TomasAI.IFM.UI.Net.Views/App/MarketOutlookView.cs) | DarkTradingView |
| 6 | OperationsView | [TomasAI.IFM.UI.Net.Views/App/OperationsView.cs](../../TomasAI.IFM.UI.Net.Views/App/OperationsView.cs) | DarkTradingView |
| 6 | StatusConsoleView | [TomasAI.IFM.UI.Net.Views/App/StatusConsoleView.cs](../../TomasAI.IFM.UI.Net.Views/App/StatusConsoleView.cs) | DarkTradingView |
| 2 | FuturesContractEditorControl | [TomasAI.IFM.UI.Net.Views/MarketData/FuturesContractEditorControl.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/FuturesContractEditorControl.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 2 | FuturesOptionContractEditorControl | [TomasAI.IFM.UI.Net.Views/MarketData/FuturesOptionContractEditorControl.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/FuturesOptionContractEditorControl.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 2 | InstrumentDefinitionSelectorForm | [TomasAI.IFM.UI.Net.Views/MarketData/InstrumentDefinitionSelectorForm.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/InstrumentDefinitionSelectorForm.cs) | DarkTradingForm |
| 2 | MarketDataForm | [TomasAI.IFM.UI.Net.Views/MarketData/MarketDataForm.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/MarketDataForm.cs) | DarkTradingForm, IForm<MarketDataForm>, IFormControl |
| 2 | YieldCurveRateEditForm | [TomasAI.IFM.UI.Net.Views/MarketData/YieldCurveRateEditForm.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/YieldCurveRateEditForm.cs) | DarkTradingForm, IForm<YieldCurveRateEditForm>, IFormControl |
| 2 | YieldCurveRateEditorControl | [TomasAI.IFM.UI.Net.Views/MarketData/YieldCurveRateEditorControl.cs](../../TomasAI.IFM.UI.Net.Views/MarketData/YieldCurveRateEditorControl.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 4 | FundFinancialForm | [TomasAI.IFM.UI.Net.Views/Portfolio/FundFinancialForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/FundFinancialForm.cs) | DarkTradingForm |
| 4 | FundFinancialPostingForm | [TomasAI.IFM.UI.Net.Views/Portfolio/FundFinancialPostingForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/FundFinancialPostingForm.cs) | DarkTradingForm |
| 4 | FundMandateEditorForm | [TomasAI.IFM.UI.Net.Views/Portfolio/FundMandateEditorForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/FundMandateEditorForm.cs) | DarkTradingForm |
| 4 | PortfolioAdministrationForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioAdministrationForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioAdministrationForm.cs) | DarkTradingForm, IForm<PortfolioAdministrationForm> |
| 4 | DeleteDraftPortfolioDialog | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioAdministrationForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioAdministrationForm.cs) | DarkTradingForm |
| 4 | PortfolioEditorForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioEditorForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioEditorForm.cs) | DarkTradingForm |
| 4 | PortfolioFinancialAuthorityForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioFinancialAuthorityForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioFinancialAuthorityForm.cs) | DarkTradingForm |
| 4 | PortfolioLedgerControlForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerControlForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerControlForm.cs) | DarkTradingForm |
| 4 | PortfolioLedgerSetupForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerSetupForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerSetupForm.cs) | DarkTradingForm |
| 4 | PortfolioLedgerVersionForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerVersionForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioLedgerVersionForm.cs) | DarkTradingForm |
| 4 | PortfolioRiskPolicyForm | [TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioRiskPolicyForm.cs](../../TomasAI.IFM.UI.Net.Views/Portfolio/PortfolioRiskPolicyForm.cs) | DarkTradingForm |
| 0 | CheckedDropdown | [TomasAI.IFM.UI.Net.Views/Presentation/CheckedDropdown.cs](../../TomasAI.IFM.UI.Net.Views/Presentation/CheckedDropdown.cs) | DarkTradingView |
| 0 | DarkHeaderSeparator | [TomasAI.IFM.UI.Net.Views/Presentation/DarkHeaderSeparator.cs](../../TomasAI.IFM.UI.Net.Views/Presentation/DarkHeaderSeparator.cs) | Control |
| 0 | DarkTradingForm | [TomasAI.IFM.UI.Net.Views/Presentation/DarkTradingForm.cs](../../TomasAI.IFM.UI.Net.Views/Presentation/DarkTradingForm.cs) | Form |
| 0 | DarkTradingView | [TomasAI.IFM.UI.Net.Views/Presentation/DarkTradingView.cs](../../TomasAI.IFM.UI.Net.Views/Presentation/DarkTradingView.cs) | UserControl |
| 3 | EconomicCalendarEditorView | [TomasAI.IFM.UI.Net.Views/Reference/EconomicCalendarEditorView.cs](../../TomasAI.IFM.UI.Net.Views/Reference/EconomicCalendarEditorView.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 3 | LookupTypeEditorView | [TomasAI.IFM.UI.Net.Views/Reference/LookupTypeEditorView.cs](../../TomasAI.IFM.UI.Net.Views/Reference/LookupTypeEditorView.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 3 | ParameterSetsReferenceView | [TomasAI.IFM.UI.Net.Views/Reference/ParameterSets/ParameterSetsReferenceView.cs](../../TomasAI.IFM.UI.Net.Views/Reference/ParameterSets/ParameterSetsReferenceView.cs) | DarkTradingView, IControlCommand |
| 3 | ReferenceForm | [TomasAI.IFM.UI.Net.Views/Reference/ReferenceForm.cs](../../TomasAI.IFM.UI.Net.Views/Reference/ReferenceForm.cs) | DarkTradingForm, IForm<ReferenceForm>, IFormControl |
| 3 | StrategyCatalogDefinitionEditor | [TomasAI.IFM.UI.Net.Views/Reference/StrategyCatalogDefinitionEditor.cs](../../TomasAI.IFM.UI.Net.Views/Reference/StrategyCatalogDefinitionEditor.cs) | DarkTradingView |
| 3 | StrategyCatalogReferenceView | [TomasAI.IFM.UI.Net.Views/Reference/StrategyCatalogReferenceView.cs](../../TomasAI.IFM.UI.Net.Views/Reference/StrategyCatalogReferenceView.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 3 | TradeStrategyFamilyEditorForm | [TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyEditorForm.cs](../../TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyEditorForm.cs) | DarkTradingForm |
| 3 | TradeStrategyFamilyEditorControl | [TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyEditorForm.cs](../../TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyEditorForm.cs) | DarkTradingView |
| 3 | TradeStrategyFamilyReferenceView | [TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyReferenceView.cs](../../TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyReferenceView.cs) | DarkTradingView, IControlCommand, IAsyncFormControl |
| 1 | RiskHistoryForm | [TomasAI.IFM.UI.Net.Views/Strategy/RiskHistoryForm.cs](../../TomasAI.IFM.UI.Net.Views/Strategy/RiskHistoryForm.cs) | DarkTradingForm |
| 1 | StrategyObservationForm | [TomasAI.IFM.UI.Net.Views/Strategy/StrategyObservationForm.cs](../../TomasAI.IFM.UI.Net.Views/Strategy/StrategyObservationForm.cs) | DarkTradingForm, IForm<StrategyObservationForm> |
| 1 | StrategyWorkflowDetailsAccordion | [TomasAI.IFM.UI.Net.Views/Strategy/StrategyWorkflowDetailsAccordion.cs](../../TomasAI.IFM.UI.Net.Views/Strategy/StrategyWorkflowDetailsAccordion.cs) | DarkTradingView |
| 5 | BackupDatabasesView | [TomasAI.IFM.UI.Net.Views/SystemAdmin/BackupDatabasesView.cs](../../TomasAI.IFM.UI.Net.Views/SystemAdmin/BackupDatabasesView.cs) | DarkTradingView, IAsyncFormControl |
| 5 | ScheduledTasksView | [TomasAI.IFM.UI.Net.Views/SystemAdmin/ScheduledTasksView.cs](../../TomasAI.IFM.UI.Net.Views/SystemAdmin/ScheduledTasksView.cs) | DarkTradingView, IAsyncFormControl |
| 5 | SystemAdminForm | [TomasAI.IFM.UI.Net.Views/SystemAdmin/SystemAdminForm.cs](../../TomasAI.IFM.UI.Net.Views/SystemAdmin/SystemAdminForm.cs) | DarkTradingForm, IForm<SystemAdminForm>, IFormControl |
| 5 | SystemErrorView | [TomasAI.IFM.UI.Net.Views/SystemInfo/SystemErrorView.cs](../../TomasAI.IFM.UI.Net.Views/SystemInfo/SystemErrorView.cs) | DarkTradingForm |
| 5 | SystemWaitView | [TomasAI.IFM.UI.Net.Views/SystemInfo/SystemWaitView.cs](../../TomasAI.IFM.UI.Net.Views/SystemInfo/SystemWaitView.cs) | DarkTradingForm |
| 4 | BrokerAccountQualificationDialog | [TomasAI.IFM.UI.Net.Views/Trade/BrokerAccountQualificationDialog.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerAccountQualificationDialog.cs) | DarkTradingForm |
| 4 | BrokerExecutionEvidenceControl | [TomasAI.IFM.UI.Net.Views/Trade/BrokerExecutionEvidenceControl.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerExecutionEvidenceControl.cs) | DarkTradingView |
| 4 | BrokerManualTradeOrderView | [TomasAI.IFM.UI.Net.Views/Trade/BrokerManualTradeOrderView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerManualTradeOrderView.cs) | DarkTradingView, ITradeOrderControl, ITradeExecutionSelectionControl, ITradeOrderPriceSelectionControl, ITradeOptionLegSelectionControl, ITradeQuantitySelectionControl, IFormControl |
| 4 | BrokerOrderFillsPreviewControl | [TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs) | DarkTradingView |
| 4 | BrokerTradePreviewControl | [TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs) | BrokerOrderFillsPreviewControl |
| 4 | OrderFillsPreviewControl | [TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerOrderFillsPreviewControl.cs) | BrokerOrderFillsPreviewControl |
| 4 | EsTradeBlotterControl | [TomasAI.IFM.UI.Net.Views/Trade/BrokerTradeBlotterView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerTradeBlotterView.cs) | DarkTradingView, ITradeOrderControl, IAsyncFormControl |
| 4 | BrokerTradeBlotterView | [TomasAI.IFM.UI.Net.Views/Trade/BrokerTradeBlotterView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/BrokerTradeBlotterView.cs) | EsTradeBlotterControl |
| 4 | CreateFundOrderForm | [TomasAI.IFM.UI.Net.Views/Trade/CreateFundOrderForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/CreateFundOrderForm.cs) | DarkTradingForm, IForm<CreateFundOrderForm>, IFormControl |
| 4 | CreateFundOrderTradeForm | [TomasAI.IFM.UI.Net.Views/Trade/CreateFundOrderTradeForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/CreateFundOrderTradeForm.cs) | DarkTradingForm, IForm<CreateFundOrderTradeForm>, IFormControl |
| 4 | DeleteFundOrderForm | [TomasAI.IFM.UI.Net.Views/Trade/DeleteFundOrderForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/DeleteFundOrderForm.cs) | DarkTradingForm |
| 4 | EstablishedTradeView | [TomasAI.IFM.UI.Net.Views/Trade/EstablishedTradeView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/EstablishedTradeView.cs) | DarkTradingView |
| 4 | IronCondorTradeOrderView | [TomasAI.IFM.UI.Net.Views/Trade/IronCondor/IronCondorTradeOrderView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/IronCondor/IronCondorTradeOrderView.cs) | DarkTradingView, IAsyncFormControl, ITradeOrderControl, ITradeExecutionSelectionControl, ITradeOrderPriceSelectionControl, ITradeOptionLegSelectionControl, ITradeQuantitySelectionControl |
| 4 | IronCondorTradeView | [TomasAI.IFM.UI.Net.Views/Trade/IronCondor/IronCondorTradeView.cs](../../TomasAI.IFM.UI.Net.Views/Trade/IronCondor/IronCondorTradeView.cs) | DarkTradingView, IAsyncFormControl |
| 4 | SetLocalSymbolForm | [TomasAI.IFM.UI.Net.Views/Trade/SetLocalSymbolForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/SetLocalSymbolForm.cs) | DarkTradingForm |
| 4 | TradeEndOfDayForm | [TomasAI.IFM.UI.Net.Views/Trade/TradeEndOfDayForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/TradeEndOfDayForm.cs) | DarkTradingForm, IFormControl |
| 4 | TradeOrderConfirmationForm | [TomasAI.IFM.UI.Net.Views/Trade/TradeOrderConfirmationForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/TradeOrderConfirmationForm.cs) | DarkTradingForm |
| 4 | TradeOrderEditorForm | [TomasAI.IFM.UI.Net.Views/Trade/TradeOrderEditorForm.cs](../../TomasAI.IFM.UI.Net.Views/Trade/TradeOrderEditorForm.cs) | DarkTradingForm, IForm<TradeOrderEditorForm>, IFormControl |
| 4 | VolatilityContextHistoryControl | [TomasAI.IFM.UI.Net.Views/Trade/VolatilityContextHistoryControl.cs](../../TomasAI.IFM.UI.Net.Views/Trade/VolatilityContextHistoryControl.cs) | UserControl |

## Testing and performance gates

- egui_kittest: all supported states, keyboard/mouse input, selection, revisions, stale snapshots and reviewed images.
- FlaUI UIA3 / AccessKit: real host, runtime, controls, observed data, resize, close and local lifetime. Current header controls support Invoke; tests do not assume ExpandCollapse or Grid patterns.
- Existing WinForms: shared view-model / event consumer regressions and the linked StrategyDetailsFlaUiTests remain mandatory.
- 100k grid: first/last row, viewport-sized draw count, background sorting/filtering, exact values, stable selection and repeated navigation.
- Performance: report native CPU layout separately from GPU presentation and input-to-visible latency. A harness CPU timing is not an end-to-end benchmark. Record build profile, viewport, GPU, data count and process memory.
- Live: verify real history hydration, updates, disconnect/reconnect, stale state and no backend shutdown. No responders is a blocked qualification, not a passed empty-data test.

Current remaining qualification gates are recorded in the implementation plan. A complete roadmap does not imply the full UI has been migrated.

### Backend-first pilot evidence update (2026-10-13)

Persisted history through the ready Development API, real C# bridge and native Rust UI is now verified, including Daily/Weekly/Monthly switching, workflow selection, property inspection and client reconnect/reload. The backend-first test entry point passed and client shutdown left backend readiness healthy. The earlier missing API was a test setup omission. The owned backend interruption/restart gate has subsequently passed. Newly persisted business workflow delivery has now passed. Mixed-debugger breakpoints and controlled comparative input-to-visible timing remain separate gates. See implementation-plan section 16 for evidence and commands.

## Current implementation progress

Full migration started on 2026-10-13. The Strategy workflow list, ITI plot and Summary have been implemented alongside Details. A shared client composition DLL now serves both hosts. The first mutable native screen, Lookup Definitions, uses the existing Reference service and lookup view model with acknowledged ABI v2 actions. Development create/change/remove qualification passed and cleaned up its disposable lookup category. See implementation plan section 17 for evidence and remaining gates. Unimplemented screen allocations above remain outstanding; the roadmap itself is not implementation evidence.


### Current port and qualification ledger

Strategy workspace/Details, Lookup Definitions, Strategy Observation and Risk History have native ports with the recorded behavior and backend evidence. Futures/Option Contract manual editors and Yield Curve Rates are implemented; provider Draft mapping/confirmation and convention review are implemented. Native/backend preview and cancellation passed; isolated import-save parity remains open. Live contract loading, option paging and native selection/removal-cancel checks have passed. Economic Calendar CRUD/filtering and native cancel checks have passed, with the timestamp identity regression covered in isolated PostgreSQL. Yield backend and native selection/cancellation tests passed. The 100,000-row fixtures are rendering tests, not fabricated business history.

Reference restoration is explicitly authorized for Development. The full Databento catalog refresh timed out without publishing a partial catalog; bounded ES parent-symbol restoration and exact multiplier verification are documented in implementation-plan section 18. Draft reference imports must not be represented as reviewed, trading-ready contracts.

All later wave allocations remain outstanding until their controls, existing actions, validation, lifecycle behavior and parity tests are implemented. This ledger supersedes older pilot-only status text; it does not imply full migration completion.


### Infrastructure pause in live qualification

The authorized Databento ES restoration completed and contract loading/paging plus native selection checks passed. Calendar CRUD/filtering and isolated fractional-timestamp regression passed. Provider browser and convention review are implemented with offline and recorded native-browser evidence. On 2026-10-13 Docker failed because WSL became unresponsive; Windows service permissions prevent recovery from the agent process. See implementation-plan section 19 for exact diagnostics, recovery attempts and remaining verification. No full-migration completion is claimed.


### Recovery and provider mapping continuation

The 2026-10-13 WSL/Docker outage is resolved after the user rebooted Windows. PostgreSQL, ScyllaDB and NATS responded, the API rebuilt cleanly, and live contract/provider/convention tests passed. Provider mapping now has a bridge-owned preview and explicit Draft import confirmation through existing view models. Native/backend preview and cancellation passed without Development reference writes. Full-screen wave allocations and remaining acceptance gates are still open; see implementation-plan section 20.


### Verified recovered package and business-data qualification gap

Release packaging, its 132-file SHA256 manifest and two FlaUI tests using the actual packaged executable passed after Docker recovery. Corrected native Summary and provider mapping tests passed (2/2); Rust widget tests passed (26/26), and isolated PostgreSQL VX checkpoint persistence/replay passed. See implementation-plan section 20 for exact evidence and first-run failures.

Live new-workflow delivery is verified. The captured Development workflow has known Regime Discovery input/schema errors (missing VIX/VX and intraday EMA/RSI metrics, and ATR14 schema unsupported). The user explicitly accepted that shared Development defect on 2026-10-13: it is not a UI migration blocker. Both UIs must display the same real result; a port must not fabricate successful pipeline output. Later-wave controls/actions and the remaining acceptance gates are still outstanding.

### Administrative ports and paired desktop checks (2026-10-13)

Scheduled Tasks and Market Data Operations Health are native ports through the existing C# view models. Scheduled Tasks includes Logs/Setup, cached task/year/month/day/run navigation, exact run output with continuation, Quartz daily/time/day helper, schedule editing/preview/enable/disable and confirmation of on-demand execution/removal/uncertain-run resolution. Operations Health includes the stage and dataset-worker panels, complete immutable metric values and automatic refresh/failure clearing.

Paired live FlaUI tests passed for both screens (2/2, `live-admin-routing-fixed.trx`). The current Development/development scheduler has no schedules: the live test verifies that genuine state, and fixture tests verify populated hierarchy/output/status behavior. No scheduled job was created or run during the live read-only checks. Native widget checks passed 7/7 and the initial shared/WinForms scheduler suite passed 11/11. Early failures exposed and fixed a double CentralPanel draw and the Rust health endpoint; those failures are retained in the evidence folder.

Actor Health has a native port and a shared `ActorHealthControlModel` used by WinForms and the Rust bridge for authority, operational reason and exact observed generation checks. Shared model/WinForms rendering tests passed 4/4 and native cached-tree/cancel tests passed 2/2. Live actor-tree qualification is tracked separately in the implementation plan; lifecycle commands are tested with isolated service fixtures, without pausing or restarting Development actors.

The existing 66-view inventory remains the completion checklist. These additions do not mark unported trading, portfolio, market selection, parameter/catalog, backup, shell and remaining dialogs as complete. The user has authorized implementation of all remaining views and paired tests.

For ongoing synchronization, business rules and command/query/service contracts remain shared C#. Every changed supported screen requires a matching native renderer and tests for both hosts, including enabled state, validation, cancellation, stable selection and lifecycle behavior. The known backend defects must remain visibly consistent across hosts.


### Parameter and catalog ports (2026-10-13)

Parameter Sets and Strategy Catalog now have native working-copy editors through the existing C# domain/service contracts. Paired live FlaUI rendering and native catalog editing/discard checks passed. Shared/WinForms/bridge tests cover lossless parameter editing, exact saved-version verification, immutable catalog identities, failed-save retry, lifecycle hashes and cancellation. See implementation record section 22 for the passing and failed-run evidence and the action-specific gates still open. These ports do not imply completion of the remaining trading, financial, backup or application-shell views.


## Required control equivalence (2026-10-13)

Every migrated form, view, and control must use the closest native egui equivalent to its actual WinForms control. Matching data alone is insufficient. Inspect runtime-generated controls as well as designer files; preserve placement, physical dimensions, font, colors, selection semantics, enabled states, and command behavior. Do not declare parity from a generic property list or a screenshot with only the same labels.

| WinForms control | Native Rust equivalent | Required behavior |
| --- | --- | --- |
| ListView, Details, FullRowSelect | egui_extras TableBuilder report list | One selected row across all cells and unused row width; original headers and column widths; viewport rows only; horizontal overflow scrolls inside the list |
| DataGridView | egui_extras TableBuilder | Match the source cell/row selection mode, formatting, column sizing, editing and validation; virtualize large datasets |
| DateTimePicker | egui_extras DatePickerButton | Calendar dropdown; selected date and format; same dependent-date behavior and date-range queries |
| ComboBox, DropDownList | egui ComboBox | Selection from the source choices; no arbitrary text entry |
| NumericUpDown | egui DragValue | Match precision, increment and min/max; retain exact managed decimal values at the bridge |
| TextBox | egui TextEdit | Single/multiline, read-only state and validation match the source |
| Button | egui Button | Same caption, placement, enabled state, confirmation and acknowledged application action |
| CheckBox | egui Checkbox | Same checked/enabled state and change handler |
| TreeView | egui CollapsingHeader hierarchy | Same branches, selected object, expand/collapse behavior and cached data; no service calls during rendering |
| PropertyGrid | Read-only egui Grid with scroll area | Selected object's named properties and formatting; editing only where WinForms permits it |
| TabControl | Native tab selector and exclusive content region | Same tab order and one active page; hidden source tabs stay hidden |
| SplitContainer | Resizable egui panels/split regions | Same initial orientation, proportions, minimum sizes and splitter behavior |
| MenuStrip / StatusStrip | Top/bottom egui panels | Same commands, ordering and status publication |
| Graph/chart | Native plotted series | Source series, scales, time zone, legend, annotations and empty-state behavior |
| Modal form | Modal native egui window | Parent-centered bounds, focus, Save/Cancel and pending/error behavior |

The report-list implementation is shared in `TomasAI.IFM.UI.Rust/src/control_parity.rs`. Orders and Trades use it; existing specialized data-grid ports require their own source-specific selection rules. Trading dialogs and nested blotter views remain subject to per-control desktop verification before full trading parity can be claimed.


### Current trading-control evidence

- Orders and Trades use the shared virtual report list with one continuous selected row, WinForms headers/column widths, non-clickable headers, internal horizontal scrolling, and no data-row cap.
- Native tests cover clicking empty row space, wide-column containment, 100,000-row viewport rendering, and calendar selection (From updates To to month end and sends the range intent).
- Main editor control bounds were compared to the runtime WinForms capture: all twelve selectors, dates, lists and action buttons differ by less than two physical pixels. The outer editor is within one pixel after accounting for CenterParent placement when parent screen height changes from 1408 to 1458 pixels.
- Final desktop checks: `ActualTradeOrderGeometryTests` and `TradeOrderNativeControlParityTests` passed; the live bridge load and generation/deduplication checks passed separately. Rust native test suite passed.
- Windows was locked during final screenshot acquisition. `trade-control-parity.png` captures the lock screen and is **not** valid visual parity evidence. UIA bounds and calendar invocation remain valid interaction evidence. Reacquire an unlocked desktop screenshot before claiming visual equivalence.
- These results qualify the main editor controls; they do not establish complete visual or action parity for every nested trading view, qualification dialog, EOD view, or established-trade monitor. Continue their per-control comparisons using the mappings above. No live trade was submitted, cancelled or repriced during qualification.


### Trade Order editor adjustments ? 2026-10-14

The Rust editor now draws the horizontal divider below Fund and above the workspace tabs, suppresses the generic Completed labels, and allows report-list columns to resize by dragging their dividers. Preserve initial WinForms widths during a new window's sizing pass; retain resized widths during subsequent frames. Keep actionable error messages visible.

Both clients use these presentation rules: Create Order is always enabled; Delete Order is enabled when the visible order list has a row; Load Trade requires a selected trade whose state is not NewTrade; Add Trade requires a selected order; Remove Trade requires a nonempty trade list. Command execution still reports missing context or rejected business operations through the existing validation paths. Source filtering must be accounted for when deriving visible-list button states.

Regression coverage includes the shared view-model states, empty editor buttons, calendar date ranges, 100,000-row virtualization, divider dragging/width retention, and initial widths in a newly sized window.


### Trade Blotter control parity ? 2026-10-14

The native Trade Blotter now follows the runtime WinForms control layout rather than a generic editor. Iron Condor has Market Selection, Broker Trade and Order Fills; the other supported blotters retain their source staging/evidence tabs. Parent trade/date/action/live controls, identity fields, grid columns, numeric settings, selection colors, execution fields, financial rows, qualification controls and order/fill inspector use the source placement and dimensions. Preserve the existing shared dark theme and regular tab typography.

Financial preview calculations are centralized in `BrokerTradePreviewPresentation`, used by both WinForms and the managed Rust bridge. Quotes, balances, limits and eligibility come from the existing managed queries and models. Missing quotes or fees remain unavailable. Manual leg roles follow strategy and strike order through `BrokerTradeLegSelection`, independent of click order; automatic selection retains the shared delta and minimum-wing-width rules.

The bridge wires expiry/live changes, manual leg selection/removal, quantity and execution choices, staging, confirmations, submission, execution refresh, broker selection, bounded repricing, cancellation and qualification actions to the existing managed services. Established trades retain their submitted contracts and execution identities. Loading an established trade opens its monitor separately from the Blotter. Chain owners are released on subscription changes and disposal. The Blotter tab waits for selected-trade loading to finish before allowing composer initialization.

Working-order repricing and cancellation were explicitly authorized by the owner. `BrokerOrderMutationRules` limits these to selected Working/PartiallyFilled orders with valid bounds/ticks, no terminal execution and no pending acknowledgement. A rejected mutation clears its pending marker; an accepted request waits for its matching broker operation acknowledgement. These checks remain enforced in the managed bridge even when UI controls are disabled.

A live parity check exposed an existing broker-account PostgreSQL adapter still passing CQL-style anonymous parameters. `BrokerAccountReadStore` now uses typed PostgreSQL positional parameters, qualified tables and revision-ordered reads. Its regression test creates and removes a uniquely named isolated database; application account data is preserved.

Verification:

- Rust full suite: 74 passed; final Blotter suite after dialog/startup corrections: 11 passed.
- Shared financial preview, strike/role selection and mutation eligibility tests: 26 passed.
- Broker-account tests, including isolated PostgreSQL replay/latest-revision checks: 9 passed.
- WinForms source capture tests cover the original Blotter, real editor parent and qualification dialog.
- Final desktop checks: main editor/calendar parity 1 passed; Blotter native FlaUI plus bridge tests 5 passed. The native comparison covers header fields, Market Selection, 41 Broker Trade controls, Order Fills navigation and qualification dimensions/controls. Asserted geometry tolerance is 3 pixels (3.01 for fractional DPI rounding).
- Final managed builds and native Release build succeeded. The rebuilt Rust executable and managed bridge/view-model assemblies were deployed to the Development launch directory and the live API was verified ready.

Evidence is under `.artifacts/rust-ui/tests/`: `blotter-winforms-*`, `blotter-rust-{Market-Selection,Broker-Trade,Order-Fills}.png`, `blotter-*-control-parity.json`, `blotter-qualification-control-parity.json` and `blotter-live-*.json`. Final test records are `TomasAI.IFM.UI.Rust.SystemTests/TestResults/blotter-parent-final.trx` and `blotter-final.trx`.

Limits: Development's selected trade 1201 has an October 9 effective/requested date and no eligible expiries in the source WinForms query range on October 14. The native client preserves that source behavior; it does not invent chain data. Successful live option selection/submission/fill is therefore not established by these runs. Financial mutation tests use simulated actions; no live order was submitted, cancelled or repriced. These checks qualify the Blotter controls and shared behavior, not every established-trade monitor or every screen in the broader migration.


### Frozen emulator strategy selection ? 2026-10-14

A shared selector regression rejected all frozen preview legs because the frozen data correctly retains `IsStale=true`. Both WinForms and Rust now explicitly opt into frozen selection only when the client is in emulator/Development mode, Live Feed is off and the snapshot identifies itself as a frozen emulator preview. Live selection retains its original stale-data rejection. Quotes remain marked frozen/stale; finite qualified deltas, the delta ceiling and minimum wing width remain required. The original four-argument selector contract is preserved for existing callers.

Live backend checks now return a complete four-leg frozen Iron Condor for the current Development trade; the earlier empty-expiry observation above is no longer the current qualification result. WinForms FlaUI loads the frozen November 20 chain and confirms four selected rows with Live Feed off. Rust FlaUI confirms four selected legs with its checkbox unchecked. Bridge assertions verify frozen provenance, delta ceilings and wing widths. Seven selector regression cases and eleven native Blotter tests pass. No order was submitted or changed by these checks. The selected-leg strip also uses full chain-column widths rather than header text widths.


### Stable live Market Selection updates ? 2026-10-14

One-second quote/execution reads no longer dim Blotter controls or show a transient Pending caption. User edits made while a read is in progress retain their drafts and wait for its acknowledgement before being submitted with the current generation; mutation serialization and validation remain in force. Closing the editor cancels deferred input. Read polling outcomes have bounded retention so the gateway does not exhaust its 256-request capacity after a few minutes; mutation deduplication remains intact. Read timeouts preserve the displayed view and allow a subsequent refresh.

The managed bridge merges display cells by contract ID, overwrites non-null incoming values (including numeric zero), and retains rows and cells absent from a partial snapshot. Eligibility and submission use the unmerged raw snapshot. Missing rows remain visibly cached and stale. Explicit underlying/expiry/frozen-live changes reset the display cache. Selected contract identities remain fixed once complete, quote updates retain their row markers, and the chain scrolls to a new selection once instead of jumping during polling. Selected-leg values use the same display data and formatting as the chain.

Verification: twelve Rust Blotter tests, two display-cache tests and four gateway/live-bridge tests passed. A live FlaUI check enabled Live Feed, observed active background polling over 79 samples with zero disabled-control samples, confirmed all four selected legs and duplicate role markers in the chain and selected-leg grids, and passed. Evidence: `.artifacts/rust-ui/tests/blotter-live-refresh-observation.json`, `blotter-live-nonflashing.png`, and `TomasAI.IFM.UI.Rust.SystemTests/TestResults/live-nonflashing.trx`. No trade order was submitted or modified.


### Underlying Last and full-row price marker ? 2026-10-14

`Last` now reads the exact underlying contract's latest accepted futures trade from `IMarketDataApi.TryGetLastTickPrice`. `EvaluatedOptionChainReadModel` appends nullable `LastUnderlyingPrice` and `LastUnderlyingTradeAtUtc` at MessagePack keys 13/14. The pricing/window `UnderlyingPrice` remains separate and retains its established freshness and stored-input fallback rules. Both clients display the explicit last trade, calculate Daily Price Change from it, and locate the yellow strike row using it. No underlying trade produces N/A. A quote-only cache, a different contract, cancelled/cleared trades and nonpositive prices cannot populate Last.

The Rust marker now paints the full row yellow, including gaps, with black text. Six source tests, twelve Rust Blotter tests and two live API/FlaUI checks passed. A live observation confirmed pricing input 7849.50 versus actual last trade 7798.00; the displayed Last was 7798.00. The Development API and Rust client were redeployed. Evidence: `underlying-last-price-ui.trx`, `underlying-last-trade.trx`, and `.artifacts/rust-ui/tests/blotter-live-nonflashing.png`.
