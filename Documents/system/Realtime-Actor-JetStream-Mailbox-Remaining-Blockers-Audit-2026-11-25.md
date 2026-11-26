# Realtime mailbox — complete remaining-work audit

**Date:** 2026-11-25, America/Toronto\
**Status:** Investigation complete for the enumerated current source boundaries; no fixes or live activation performed.\
**Inventory:** [Machine-readable work list](Realtime-Actor-JetStream-Mailbox-Remaining-Work-2026-11-25.json)\
**Existing plan:** [Implementation stages](Realtime-Actor-JetStream-Mailbox-Implementation-Plan-v1.0.md)

## Answer and counting

There are **24 remaining work packages: 22 in the mailbox plan and 2 adjacent BrokerAccount safety items**.
This is not a claim of 24 independently reproduced code defects. It is the complete remaining-known-work
inventory from this audit, including conditional/legacy compatibility, explicit recovery-policy gaps,
unimplemented infrastructure and mandatory evidence gates. No unopened branch is silently counted as safe.

- **8 workflow packages** cover 33 decision, terminal, reservation, callback, preparation and financial handoff boundaries.
- **1 producer package** addresses the reproduced premature ingress watermark.
- **3 destination packages** address lifecycle publication, a supported legacy metrics-free handoff, and conditional parameter-version upgrade compatibility.
- **6 infrastructure packages** implement what the production mailbox still lacks.
- **4 qualification/rollout packages** close actual-owner, performance, two-session and cutover evidence.
- **2 adjacent account packages** were discovered following broker callbacks. They use the Event/Command account path, not one of the 25 Realtime destinations; they must not be silently folded into a dispatch-only change.

Ten work packages have focused counterexample evidence (some conditional/adjacent); two more have
code-confirmed failure paths; two concern workflow recovery-policy/proof compatibility. The other ten
are missing infrastructure or evidence gates. Some work packages contain several affected commands;
fixing one command is not completion of the package. Overlapping root causes share a package/dependency
instead of being counted as another blocker every time they appear.

## Audit coverage and evidence

The root and **three concurrent subagents** audited workflow/owners, producers/broker callbacks,
destinations, and infrastructure respectively—the maximum four active agents available in this session.
All 25 current production Realtime actor maps were reread. Coverage was refreshed for Daily flatten
clocks, bar barriers and callback event families omitted by the historical snapshot. Historical live
counters from November 24 were not relabelled as current readings.

**12 targeted audit scenarios passed in four projects**: Trade 9, Feed 1, MarketData ingress 1 and
BrokerAccount 1. They assert current undesirable behavior or a conditional incompatibility; passing
these tests does **not** mean a defect was repaired. The Trade batch includes the existing Market
Condition counterexample plus new risk/reservation/redispatch/route lifecycle/version cases. No full
regression or new BenchmarkDotNet result is claimed for this investigation. The future implementation
must convert the applicable counterexamples into desired-behavior regression assertions.

The initial same-version parameter-value hypothesis failed and was rejected: the plan fingerprint
pins Version, so value-only alteration did not create the hypothesized conflict. The final test changes
Version and is explicitly conditional on upgrade/changed context. The failed exploratory log is retained.

## Workflow work packages

| ID | Remaining item and scope | Evidence | Completion requirement |
| --- | --- | --- | --- |
| **W1** | **Freeze all time/evidence-bearing workflow outputs together**. MC completion; prepared/legacy Risk failure, preparation, entry-commit confirmation and financial advance variants | reproduced + static | Original command/result/parameter/provenance survives lost replies and startup; owner audit accepts/reconciles exact bytes without deadline relaxation |
| **W2** | **Separate Risk calculation failure from uncertain success delivery**. PreparedPortfolioRisk.EvaluateAsync currently catches CompleteRiskManagement send failure | reproduced | Accepted/lost approval never becomes a contradictory failure; original result and unknown outcome remain recoverable |
| **W3** | **Freeze legacy Portfolio request before financial evaluation**. Legacy EvaluatePortfolioAsync rebuilds RequestedAt/hash under same operation | reproduced | Exact request/op/hash is reused; real Portfolio store resolves accepted/lost response without new effects |
| **W4** | **Canonicalize reservation receipt completion**. Legacy Fund receipt returns Committed first, IdempotentReplay later | reproduced | Same immutable completion bytes retain original receipt semantics; no duplicate allocation |
| **W5** | **Durably own late reservation and stopped cleanup**. Detached late callback logs failure; pending cleanup skips failed order read | code-confirmed failure paths | Recover until authoritative reservation/expiry settled, including crash after timeout and required read failure |
| **W6** | **Define cold recovery for unaccepted local composition preparation**. Resident Lazy preparation can be lost before owner accepts it | recovery-policy gap | Persist exact preparation or intentionally interrupt/retire it with durable settlement; no recapture under old authority |
| **W7** | **Reconcile durable outputs with local proof/session lifetimes**. MC/TS/OC MatchesResult is resident; Risk approval also uses reference identity; old decisions intentionally interrupt | architecture/qualification gap | Actual owners can reconcile original acceptance or safely retire interrupted output; never bypass proof/expiry or resume stale eligibility |
| **W8** | **Accept legitimate redispatch aliases without losing input integrity**. Regime/Assessment key WF+stage+revision but hash full outer notification; explicit redispatch changes outer ID/time | reproduced | Same accepted view/dispatch aliases recover original pending/sent terminal; changed accepted content still fails closed |

## Producer work packages

| ID | Remaining item and scope | Evidence | Completion requirement |
| --- | --- | --- | --- |
| **P1** | **Commit ingress replay watermark after durable capture**. Non-option admission advances LastSequence before publication; a failure fences exact retry | reproduced | Retain/serialize pending admission and commit progress after durable ownership; exact retry and successor order survive restart/fan-out |

## Destination work packages

| ID | Remaining item and scope | Evidence | Completion requirement |
| --- | --- | --- | --- |
| **D1** | **Resolve option lifecycle snapshot/cleanup retry ownership**. Open/close route/cache mutation can suppress failed snapshot send; separate IronCondor final close publications exist | reproduced; lifecycle/display scope | Recover required lifecycle/cleanup or prove allowed display/read-repair semantics; preserve final feed-off/control contract, no route resurrection |
| **D2** | **Make legacy market-price failure settlement metrics-independent**. IFM.RawFuturesDurableStorage=false and optional metrics absent; default durable raw mode bypasses forward | reproduced; conditional legacy mode | Required failure is not ACKed in every supported mode, or unqualified legacy configuration is explicitly disabled |
| **D3** | **Specify parameter-version compatibility for pending position plans**. VerticalSpread/pre-intent Futures contexts currently immutable; changed Version during pending replay conflicts | reproduced conditional upgrade; not active hot-reload defect | Capture original versioned plan input or explicitly retire incompatible pending work under tested upgrade policy |

## Adjacent account safety work packages

| ID | Remaining item and scope | Evidence | Completion requirement |
| --- | --- | --- | --- |
| **A1** | **Recover latest account convergence and supervise callback reader**. BrokerAccount Event bridge can lose last snapshot, remain faulted, and leave bounded synchronous channel blocking financial callers | code-confirmed failure paths; outside 25 Realtime destinations | Last failed snapshot converges without another callback; startup fault restarts or surfaces readiness; dead reader cannot stall financial progress |
| **A2** | **Fence stale same-generation account evidence**. Older account AsOf can overwrite newer NewRiskAllowed=false evidence and reopen gate | reproduced; outside Realtime transport | Monotonic authoritative evidence/order plus equal-time conflict rule; preserve manual/qualification holds |

## Unimplemented infrastructure work packages

| ID | Remaining item and scope | Evidence | Completion requirement |
| --- | --- | --- | --- |
| **INF1** | **Realtime backend/options, canonical envelope and durable mailbox registry**. ActorType/Name/EntityId is key; all verbs share one consumer. No production backend is registered | missing implementation / qualification | Realtime-only Memory/File backend behind current APIs; binary origin envelope, stable registry, cached async lifecycle; other actor categories unchanged |
| **INF2** | **Confirmed source-to-destination publication and frozen fan-out manifest**. Core sends/local enqueue/flush are not durable destination acceptance; raw trade/quote WAL excludes statistics/control families | missing implementation / qualification | Every required native/managed/domain/clock source and recipient has original identity plus confirmed capture or recoverable outbox; partial/unknown publishes reconcile |
| **INF3** | **Broker scheduling, ordered retries, ACKs and independent progress**. Current ActorThreadV2 consumes local queues; shared admission/stripes remain; a completion hook alone is not an adapter | missing implementation / qualification | Bounded byte/message pulls, one owner/mailbox, no overtaking on fail/ACK uncertainty, idle pull/self-send/cycle safety; migrated Realtime bypasses old shared rejection |
| **INF4** | **Generation, rollover, consumer eviction and restart lifecycle**. Local eviction sees local count only; source fences can settle as success; obsolete and required ownership need explicit policy | missing implementation / qualification | Retain pending consumers independent of local objects; fence old execution/ACKs and qualify contract/attachment generation plus wakeup envelope aliases |
| **INF5** | **Measured finite capacity, overflow ownership, storage guarantees and retention**. No complete numeric capacity worksheet; pending journals bounded but sent history unpruned; Memory does not survive broker loss | missing implementation / qualification | Per-subject/stream/account/fetch budgets; no TTL/DiscardOld for required work; owned refused publications; safe pruning; tested disk-full and declared Memory/File failure limits |
| **INF6** | **Broker-aware Actor Health, metrics and readiness**. Current local queue counters cannot describe authoritative broker backlog or stalled consumers | missing implementation / qualification | Show pending bytes/messages/age, in-flight/ACK/refusal/cursors and source gaps; incident counters survive eviction and health stays truthful |

## Qualification/rollout work packages

| ID | Remaining item and scope | Evidence | Completion requirement |
| --- | --- | --- | --- |
| **VAL1** | **Actual-owner end-to-end failure/restart and non-Realtime regression**. Synthetic terminal receipts do not prove actual workflow/Fund/Portfolio/financial/projector acceptance | evidence gate; not an asserted defect | Real owners and schemas through before/after commit/ACK, fan-out, source/generation changes, pool disposal and no duplicate financial effects; deadline/proof rules intact |
| **VAL2** | **Matched complete-backend BenchmarkDotNet and burst qualification**. Existing component probes exclude parts of real transport, financial owners and NATS RAM | evidence gate; not an asserted defect | Predeclared capacity/latency gates; local vs Memory vs File under recorded rates/cardinality; p50/p95/p99/max, drain, API+NATS RAM, LOH/Gen2/CPU/IO; two-second decisions separately from queue age |
| **VAL3** | **Two complete matched exchange-session trials per backend**. No completed two-session Memory/File qualification exists | evidence gate; not an asserted defect | Same authoritative captured sessions, overnight/open/close/EOD/rollover; isolated financial effects and complete identity/result reconciliation |
| **VAL4** | **Controlled activation, migration and rollback**. Core/local live path remains active; cutover epoch/drain/double-route exclusion unimplemented | evidence gate; not an asserted defect | Retain pending ownership across both directions; no Core+JS double execution; qualify profile restart/UI/readiness, keep old backend rollback |

## Source-by-source delivery obligations

INF2 includes the complete producer matrix, not only messages already reaching Core NATS:

| Source family | Required closure |
| --- | --- |
| Native futures trade/quote capture | Preserve the repaired raw WAL/storage route; qualify independent business destination manifests. |
| Official session statistics, VWAP/replay/control outputs | Raw WAL accepts Trade/Quote only; persist these outputs or prove exact reconstruction/convergence with original timestamps and recipients. |
| Option trade evidence | Preserve storage receipts; qualify actor publication and reconstruction after native/API/generation restart. |
| Latest option quotes/greeks | Separate permitted latest-state coalescing from required exit/order/position observations; demonstrate reconnect/read repair. |
| Domain/projector notifications | Do not retire parent projector work before required Realtime destination capture; recover partial fan-out to different recipient categories. |
| Raw EOD coalescer | Prove last official statistics and cumulative extrema survive/reconstruct through close and restart; an in-memory last value is not a durable receipt. |
| Flatten clock, bar barriers, monitoring inputs | Define immutable facts versus reconstructable wakeups; clocks can repeat a stable ID with different ReceivedOn without changing business fingerprint. |
| Emulator broker order facts | Preserve repaired financial checkpoint/ordered publication cursor; prove actual downstream domain effects. |
| Broker account snapshots | Latest-state convergence/order through A1/A2; financial facts remain separately journaled. |
| Nonrecoverable broker adapters, synthetic/compatibility publishers | Qualify each enabled profile or explicitly refuse unsupported no-loss claims. Do not treat unused managed compatibility code as the active native owner. |

## Existing behavior we must preserve

- Regime failure/completion journals and previous bar/TDI/indicator/ITI/EOD/routing/emulator repairs remain scoped prerequisites, not undone work.
- TS/OC success mappers use result ProducedAtUtc: this audit did **not** find the same-runtime timestamp defect there. Their remaining W7 issue is cold proof/result ownership and interrupted-work settlement.
- Reservation and Portfolio financial owners already provide important operation/receipt deduplication. W3/W4 concern caller request/terminal bytes, not proven duplicate money allocation.
- Local old-session nonfinancial decisions intentionally interrupt; never resume stale trading or bypass MatchesResult/MatchesApproval/reference/deadline guards just to empty a queue.
- MarketOutlook and ordinary Notify lanes are already display/coalescing paths. Existing permission allows display drops; preserve latest catch-up without demanding a new approval. Final close/feed-off control has a separate contract.
- Three exit-start Realtime destinations are intentional notification no-ops; authoritative financial exit execution is elsewhere. Do not manufacture three missing-exit-code bugs.
- The Daily flatten clock ReceivedOn changes did not establish a current exit-intent conflict; first-authority intents and business fingerprints already handle its time fields. Include it in envelope compatibility tests.
- ACK-on-generation-fence and current contract filtering are policy/ownership gates until source reconstruction is proven, not blanket evidence of lost data.
- Memory remains the requested development trial default, with API restart recovery while NATS remains alive. Unconditional broker/power-loss persistence requires a tested persistent source/File policy.

## Proposed parallel execution structure for the next implementation plan

This audit does not authorize or start fixes. It supplies the finite backlog and dependencies.

1. **Shared protocol and recovery policy first:** root owns immutable command/result receipts, canonical source versus redispatch alias identity, proof/session retirement, codec migration and actual-owner acceptance semantics (W1/W6/W7/W8). Agree this boundary once for all stages; do not add success journals that cannot satisfy real owner guards.
2. **Four concurrent implementation lanes after that contract:** root implements Realtime infrastructure INF1–INF6; agent A handles workflow/financial settlement W1–W8; agent B handles producer/native ingress P1 and source ownership; agent C handles D1–D3 plus explicitly included A1/A2. File ownership must be separated and common interfaces integrated centrally. A1/A2 remain an explicit adjacent scope choice in the implementation plan.
3. **Apply every listed boundary in each package:** use the 33-row workflow matrix and the 25-destination/current-event map as checklists. Track individual subcases; do not mark W1 complete after only MC completion. Continue through the known backlog without stopping after every reproduced instance of the same root cause.
4. **Central integration and evidence:** run one real-owner fault matrix, full relevant regressions and matched BenchmarkDotNet, preserving original source/result hashes and all money/position/order invariants. Convert audit counterexamples to positive assertions. Throughput/pruning/capacity are part of completion, not follow-up promises.
5. **Matched sessions then cutover:** complete VAL1–VAL4 with isolated source/financial effects and a tested pending-work migration/rollback. Do not activate the broker backend from component microbenchmarks alone.

Only a genuinely new dependency, user scope decision or failed exit gate outside this inventory should
stop that execution. An implementation may reveal a new defect; this audit cannot prove an exhaustive
future runtime fault space. The point is to make all current known defects, policies and unfinished
qualification visible upfront and execute the known work in batches.

## Detailed evidence and coverage appendices

- [33-boundary workflow audit](../../.artifacts/realtime-mailboxes/20261125-safety/full-audit-workflows.md)
- [Producer/native/account audit](../../.artifacts/realtime-mailboxes/20261125-safety/full-audit-producers.md)
- [25-destination audit](../../.artifacts/realtime-mailboxes/20261125-safety/full-audit-destinations.md)
- [Refreshed actor/event map](../../.artifacts/realtime-mailboxes/20261125-safety/full-audit-current-destination-map.json)
- [Infrastructure evidence](../../.artifacts/realtime-mailboxes/20261125-safety/full-audit-infrastructure.md)
- [Trade counterexamples — 9](../../.artifacts/realtime-mailboxes/20261125-safety/full-audit-trade-reproductions.log)
- [Feed counterexample — 1](../../.artifacts/realtime-mailboxes/20261125-safety/full-audit-feed-reproductions.log)
- [Ingress counterexample — 1](../../.artifacts/realtime-mailboxes/20261125-safety/full-audit-ingress-reproductions.log)
- [Account counterexample — 1](../../.artifacts/realtime-mailboxes/20261125-safety/full-audit-account-reproductions.log)
- [Rejected initial parameter hypothesis](../../.artifacts/realtime-mailboxes/20261125-safety/full-audit-trade-rejected-parameter-hypothesis.log)

Application code was not changed, no production services/broker were restarted, no purchases were
made, and no runtime readiness check was weakened. Existing workspace changes from earlier work remain.
