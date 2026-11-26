# Realtime mailbox matched-session capture setup

Date: 2026-11-25. Status: **Capture-only sink implemented and component verified; eligible sessions prepared, not armed**. No live profile restart, forwarding hold, mailbox activation, broker order or paid data request has occurred for this setup.

The next planned normal sessions are **Nov 30 and Dec 1, 2026**, after the Thanksgiving holiday/early-close interval. Official CME and CFE sources and their limitations are recorded in [calendar evidence](../../scripts/RealtimeMailboxSessions/calendar-evidence-20261125.json). Exchange calendars must be rechecked before arming.

| Value date | Full ES/VX normal trading window (UTC) | Observation envelope including rollover |
| --- | --- | --- |
| Nov 30 | Nov 29 23:00 → Nov 30 22:00 | Nov 29 22:50 → Nov 30 23:10 |
| Dec 1 | Nov 30 23:00 → Dec 1 22:00 | Nov 30 22:50 → Dec 1 23:10 |

The envelope includes cash open/close, EOD, maintenance and the next 6 p.m. ET rollover. Its overlap must be reconciled by original source identities, not injected twice. Contract-specific option calendars and the exact ES/VX/option catalogue must be pinned separately.

The [machine-readable plan](../../scripts/RealtimeMailboxSessions/next-normal-sessions.json) explicitly leaves unacquired owner/source/recipient/clock hashes null. Memory and File must restore **the same** initial owner snapshot once per backend, then carry session-one state into session two. They must not independently restart session two from the original pre-session-one state. Account observations, financial and workflow control, statistics, exact option evidence and all native input families are required alongside ES/VX trades and quotes.

## Implemented preparation

- Bounded Core-NATS observer preserves original subject/reply/header/body bytes, arrival ordering/time and checksummed segment chains. It has finite payload, disk and duration bounds and never masks disconnects by reconnecting. It is always diagnostic until authoritative source and recipient ledger reconciliation is supplied.
- The offline snapshot/export command performs actual PostgreSQL custom dumps, Scylla native snapshot/schema export and exact owner file copies only under a verified held owner fence. Missing providers, changed/expired leases, file mutation, corruption, export errors and exhausted bounds fail closed. Snapshot restoration and replay reconciliation remain separate gates.
- Native forwarding hold prevents new relay sends and waits for current send guards while provider capture continues. It refuses Core-only operation. The exact lease controls release. Immutable native source-journal prefix export captures fixed lengths and queued original heads without hashing gigabytes under the producer lock. It explicitly does not freeze native mapper or RequiredCaptureIntent state.
- Optional native Host control endpoints require Development, loopback, explicit enablement and a configured access token. Defaults remain disabled. Their status explicitly states it is not a cross-owner snapshot.
- Fixed-prefix copying requires the authenticated live matching native lease before, during and after copying. Matched-run planning pins source/initial-snapshot hashes; reconciliation compares original owner/recipient identities and all financial/state invariants, not only counts or P&L.

Commands and file formats are in [the tool README](../../scripts/RealtimeMailboxSessions/README.md). Checking in these scripts does not launch a recorder or claim a completed trial.

## Why eligible captures are not armed

The earlier audit found a receiver coupled to the durable mailbox adapter. That missing-code finding is **superseded**: the independent bounded original-wire source archive and actual receiver/Core/JetStream/compatibility producer wiring are implemented and component verified below. It proves archival ownership, not financial-handler settlement. Current arming blockers are the missing matching owner/source/clock baseline, unhealthy original source continuity and unqualified total capture storage capacity.

A native forwarding hold alone also does not establish the API/database/emulator boundary. The bounded safe option is to keep native original/raw capture running, hold forwarding, gracefully stop all API/background/financial writers without force, preserve their actual drain/flush evidence, snapshot PG/Scylla/files at rest, restart the capture-only receiver and resume the exact native lease. Native raw-storage publication must be accounted for separately; it cannot mutate the supposedly frozen Scylla starting snapshot silently. The existing development stop script has a forced-cleanup fallback and is not itself a clean snapshot certificate.

Alternatively, a real cooperative cross-owner fence must cover native source floor, API ingress and late requests, actor quietness, host clocks/EOD/monitoring/recovery writers, database writes, emulator/account state and every journal checkpoint. No JSON attestation or queue count substitutes for those implementations.

No two complete all-family source captures with matching authoritative initial snapshots were found in existing evidence. Saved Nov 16/17 ES historical ticks are useful reference inputs but lack complete quotes/VX/options/statistics/control/arrival/owner evidence. Component tests, the six real financial-owner cases and short transport load runs do not close this two-session gate.

## Completion evidence still required

1. **Implemented and component verified:** independent capture-only source ownership with native/Core/JetStream/compatibility wiring while the mailbox backend remains disabled. Actual session collection is not armed.
2. Actual matching owner/clock/config/source-floor snapshot with clean participant evidence and successful isolated restoration.
3. Original normal-session tapes for both dates, full source/recipient coverage and absence of unowned gaps/refusals.
4. Isolated production owner execution for Memory and File, original pacing, same starting state and ordered session continuation; no live financial effects.
5. Per-boundary exact raw, indicator, workflow, order/fill, cash/hold, position, final plan, EOD and source-cursor reconciliation plus measured resource/backlog/recovery results.

Until these exist, the accurate status is **session capture tooling prepared; eligible capture and matched-session qualification pending**.

## Verification and observed live prerequisite

Session-tool checks passed11/11; native capture-only ingress checks passed23/23; API/DI checks passed5/5; final NATS regression including archive, prefix, exact-wire Core/account and compatibility guards passed185/185. The managed native Host graph builds with zero warnings/errors. Producer lane's final native run passed154 tests, including forwarding hold/release and immutable-prefix capture while input appends (13 infrastructure/timing tests intentionally ignored). Detailed logs and limits are in `.artifacts/realtime-mailboxes/20261125-safety/destination-v2-results.md`.

The read-only live probe found the unchanged Nov24 API on port22543 returning503 for a Daily ES20261218 ordered-source gap352759→352761. Native port22544 returned200; raw storage/indicator checks were healthy. No new code was activated and no live repair attempted. This gapped baseline must not be silently treated as eligible initial state.

All configured PG contexts actually share one physical database, so one exported PG snapshot can cover authoritative tables. A narrower baseline using exact emulator headR plus PG mirrorSourceSequenceR may avoid a blanket store freeze, but current live account evidence is legacySourceSequence0. The file store keeps only a replaced current head, not a historical headR; safe backwards slicing is unavailable. The source/journal-floor and exact-head export must be implemented/verified before using that alternative.


## Capture-only receiver and prepared-job update

The independent capture-only source sink is now implemented without activating the durable mailbox adapter. Native protocol2 immutable wires are archived before the unchanged Core mirror; managed realtime publications and actual JetStream account observations archive and publish identical serialized bytes with the original origin and frozen logical manifest. Compatibility event producers inherit the recorder and fail explicitly on recording errors. The archive owns original source evidence only; it does not acknowledge a financial actor handler. The archive is finite, separate, Development-only and disabled by default.

A token-protected loopback prefix endpoint exports committed append-only source file lengths under a short commit fence. The Python exporter copies and hashes fixed prefixes while capture may append. It makes no owner snapshot/mapper checkpoint claim.

The concrete Nov 30 / Dec 1 overlay and job in `scripts/RealtimeMailboxSessions` remain **PreparedNotArmed**, not applied. Initial owner, source-floor and clock manifest hashes are null. Read-only preflight on Nov 25 found API503 with the existing Daily ITI missing source sequence352760, native readiness200, no positive legacy emulator/PG revision binding, no acquired owner snapshot/token and unqualified capture budgets. It started no recorder, pause, profile restart, orders or eligible session trial. A clean matching baseline and healthy original source continuity remain required before arming; two full normal sessions and Memory/File reconciliation remain outstanding.


## Finite recording storage accounting

The staged source archive has separate configured limits of **64 GiB retained source frames** and **8 GiB authoritative receipt metadata**. The rebuildable derived `receipt.lookup` index is additional: **8 MiB fixed bucket heads plus 48 bytes per receipt**. It is not included in those two byte counters and is deliberately excluded from immutable-prefix exports because it rebuilds from authoritative evidence. Additional native source/raw journals, optional 32-GiB diagnostic tape, baseline snapshots/exports (up to the tool's separate 500-GiB bound), witness files and two isolated replay stores also require disk capacity. Therefore 72 GiB is not a total disk budget. The aggregate finite allocation, free-space reserve, normal-session volume and recovery-time qualification remain pending; `captureBudgetsCapacityQualified:false` remains explicit and blocks arming.


## Stress classification and observed counts

Bulk cold creation of 1,000 unknown identities/ 3,000 recipients is explicit test-only stress, separate from the two-second prepared decision-pipeline gate. Raw measurements and over-two-second flags remain unchanged. Default operation uses durable pending recovery plus on-demand consumers, with no new explicit required-consumer manifest/barrier. None of this changes the incomplete session/baseline/activation gates.

The read-only Nov 25 post-rollover sample found869 retained Realtime mailbox identities across 25 actors (zero queued/processing), not a required-consumer count or a historical peak. Native observation reported trading date Nov 26 with three futures contracts across two primary datasets. Historical startup and before/after rollover counts were not observed; no restart or synthetic substitution was used to invent them. Exact evidence is linked from the latency remediation report.

The full qualification harness still includes its cold phase when explicitly invoked. No new opt-in branch or default-off harness switch was added; the stress workload does not run in ordinary application startup.
