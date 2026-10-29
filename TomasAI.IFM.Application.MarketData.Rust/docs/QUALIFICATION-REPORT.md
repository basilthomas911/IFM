# Complete native market-data owner: qualification report

Date: 2026-10-28. The earlier full-owner offline baseline is recorded below.
Live findings reopened availability/recovery qualification; the correction and
its current evidence are recorded at the end of this report.
**Real-store/API actor/UI live acceptance remains open.**
This report does not grant production acceptance or change backend selection.

## Implemented boundary

```
IMarketDataApi
  -> typed C# command/query adapter
    -> Rust market-data owner
      -> independently owned dataset/role resources
Databento -> native processing/MessagePack -> direct native NATS publication
```

The opt-in Host selects the complete owner, feature mask **0x1ffff**. Rust owns
physical subscriptions, desired membership, lease/view expiry, renewal, metadata
and reference preparation, pricing, statistics, tasks, queues, cancellation,
health, publication and the original two-minute dataset recovery episode.
The selected graph registers no managed live epoch, discovery/renewal coordinator,
pricing-context preparer, admission registry or receiving live cache owner.
C# persistence ports exchange copied business/reference facts only. Keeping those
ports or a caller blocked cannot retain the native dataset's physical resources.

All 71 command/query routes and both properties are explicit. All 40 required
Host capability members and 32 application operations/disposal resolve in the
complete constructor graph. The old C# backend and normal launch selection have
not been changed by this continuation. Historical prototype helpers in this new
project remain unselected compatibility/qualification code.

## Accepted offline evidence

Logs are under `.artifacts/rust-marketdata-offline/`.

| Gate | Result | Evidence |
|---|---|---|
| Full Rust regression, 105 test executables/examples | **385 passed, 0 failed** | `Native-complete-full-regression-r5.log`; `Native-complete-final-regression-summary.json` |
| Strict Rust Clippy, all targets | Passed with `-D warnings` | `Native-complete-clippy-final.log` |
| Explicit SDK/DLL and 120-second metadata/pricing/reference/broker recovery gates | **9 passed, 0 failed** | `Native-complete-production-deadlines-and-clr.log` |
| Production-duration DNS, storage failure, futures bootstrap, silent SDK | **4 passed, 0 failed** | `Native-complete-deadline-*.log` |
| Actual release DLL through public C# discovery/browser/preparation adapters | **3 passed, 0 failed** | `Native-complete-release-clr-sdk.log` |
| Actual development DLL through the same C# discovery/browser/preparation adapters | **3 passed, 0 failed** | `Native-complete-development-clr-sdk.log` |
| Native empty-scope bounds, one-shot consumption, no source replacement and expiry | Passed | `Native-complete-empty-owner-boundaries-r2.log` |
| Actual C# complete-empty immutable preparation/replay; blocked C# commit cannot pin reset | Passed; exactly one explicit physical recovery | `Native-complete-empty-preparation-clr-sdk-r3.log` |
| Real 30-second browser metadata maintenance, unchanged physical recipe | Passed without a new SDK connection | `Native-complete-browser-metadata-maintenance.log` |
| Exact native catalog DTO/mapping/chain results versus existing C# implementation | Passed | `Native-complete-release-catalog-parity.log` |
| All explicit facade routes and complete Host constructor graph | Passed | `Native-complete-facade-contract.log`; `Native-complete-final-host-graph.log` |
| Actual packaged release Host startup, HTTP/NATS readiness/status, no children/old provider DLL, joined shutdown | Passed | `Native-complete-release-host-executable.log` |
| Repeated underlying quote startup/cancellation | **100 passed, 0 failed** | `Native-complete-underlying-stress.json` |

The normal full suite reports 14 ignored cases. Thirteen are explicit-duration or
actual-DLL gates run separately above; the remaining case is an explicit golden
fixture regeneration command, not a skipped product acceptance test. Counts of
repeated release/component checks are not added to the 385 regular tests.

Actual deadline observations included DNS at 120.009 s, silent SDK at 120.011 s,
metadata at 120.012 s and pricing at 120.035 s. Small scheduler/cleanup overhead
is reported rather than claiming hard real-time process scheduling. No retry,
source replacement or retirement received a second recovery allowance.

Financial/source parity includes the frozen existing C# contract corpora, 1,600
bit-identical ATM-IV cases, 1,200 window/fallback cases and 81 ownership hash
cases. Explained differences are in `PARITY-DIFFERENCES.md`; none authorize an
unexplained financial mismatch.

## Defects found and corrected during final qualification

- Business-watermark-only reconciliation no longer resets unchanged SDK sources.
  A changed physical recipe is acknowledged only after the replacement qualifies;
  old runs cannot acknowledge a newer recipe. Known conditional-write races retry
  in place; unknown failures enter supervised recovery.
- A complete, authoritative empty scope no longer adds/removes physical demand.
  Its bounded one-shot evidence cannot keep a provider task alive.
- Host shutdown and subsequent DI/API alias disposal observe one native join
  result rather than submitting a command through an already freed FFI handle.
- Option lookup uses the native active catalog, matching the current API, rather
  than treating a securities-database row as configured session membership.
- Publish now rejects an output directory used by a running process before
  writing any package files. A separate output directory is supported.

One underlying-bootstrap fixture failed in an earlier heavily parallel run.
It did not reproduce in isolation, 100 repeated runs, the subsequent 58-test
parallel transport run or the final complete regression. Its original assertion
was hidden by the deliberately nonblocking production panic hook. The test now
copies retained panic evidence if it fails again. No production timeout was
extended or assertion removed. This remains recorded test history, not a claim
that a demonstrated production defect was fixed without reproduction.

## Qualified packages and reproducibility

Use the freshly isolated package, **not the older running native package**:

- Release: `.artifacts/rust-marketdata-qualification-package/Release/`
- Development: `.artifacts/rust-marketdata-qualification-package/Debug/` (profile/DLL checksum verified).

The release native DLL is 13,478,912 bytes; SHA-256:
`3647c344e2ab297366398414a7e1e620690fcec780110343b40f7bb07fd74146`.
Each package has a `native-package.json` with profile, DLL checksum and
`productionAccepted=false`.

The first standard Release publish encountered loaded-file locks in the older
`.artifacts/rust-marketdata-package/Release` directory. It did not complete and
that directory must not be treated as a qualified new package. The running Host
was not stopped. Republish that directory only after stopping its Host, or use
the fresh output above. The new guard prevents repeating a partial overwrite.

```powershell
# Local SDK/broker/storage fixtures; no real provider credentials or orders.
./TomasAI.IFM.Application.MarketData.Rust/scripts/Test-NativeOwnership.ps1 `
  -NativeProfile release -IncludeProductionDeadlines

# Build separately while an older package remains in use.
./TomasAI.IFM.Application.MarketData.Rust/scripts/Build-NativeHost.ps1 `
  -Configuration Release `
  -OutputDirectory .artifacts/rust-marketdata-qualification-package/Release
```

The optional `-BrokerUrl` qualification argument additionally checks the actual
Host executable against a supplied test broker, using unique subjects/fabricated
storage identities. Production-duration checks take two minutes per gate.

## Actual remaining blocker

At verification time, local PostgreSQL (5432), Redis (6379) and NATS (4222) were
all unreachable. An older native Host (PID 66156, started 2026-10-27) still uses
the former native package. Final real-store/API actor/UI acceptance needs those
services restored and a coordinated switch from that older native Host to the
qualified package. Do not run two Hosts on the same development RPC subjects.

The user has been asked to start the local services. No existing process was
stopped/reset, no live fault injected and no live trade/order changed in this
continuation. Offline success cannot replace verification that the actual UI
receives native terminal failures and that the loaded trade resumes with fresh
quotes/Greeks/plans after recovery. Until that gate runs, overall acceptance
remains **99% / blocked**, not complete.


After the older acceptance Manager/Host is stopped and local infrastructure is
ready, the existing opt-in preparation script can select the fresh package:

```powershell
./TomasAI.IFM.Application.MarketData.Rust/scripts/Prepare-NativeAcceptance.ps1 `
  -PackageDirectory .artifacts/rust-marketdata-qualification-package/Release
```

This prepares a separate Manager configuration. Use its explicit `-Start` only
when the prior API/Host/UI session is stopped; it never takes over those processes.


## Live baseline reopened (2026-10-28)

After machine restart PostgreSQL, Redis and NATS became reachable. A controlled
native session started with all eight startup activities reporting success. UI
accessibility confirmed current ValueDate, ES/session prices, analytics values
and current market-outlook console messages, but absent ITI observations and
VX/ITI degradation mean baseline acceptance has NOT passed. The user requires a
working startup/futures EOD/analytics/UI baseline before final recovery tests.
No development fault was injected in this session.

Live background-chain discovery exposed duplicate canonical contracts across
calendar partitions. The current C# universe builder keeps the first occurrence
using a range-wide dictionary; the passive compact source omitted this step.
NativeDefinitionFacts now preserves the same ordinal first-row authority before
freezing pages. Rust's duplicate wire-snapshot rejection remains intact. The
actual C# paging regression covers overlapping partitions with conflicting later
rows and verifies identical complete page bytes and SHA-256 digest, plus existing
100000-row, expiry, cancellation, malformed-filter and capacity cases. Result:
PASS (`Native-live-compact-overlap-regression.log`); Release build/publish pass
(`Native-live-overlap-host-package.log`). Native DLL contents are unchanged.

The previous actual UI click failed with unavailable geometry; refreshed capture
failed with FrameArrived timeout. Per computer-use skill, further input was
stopped. Accessibility/log observations alone do not prove visible chart parity.


### NATS address investigation before baseline (2026-10-28, 09:07-09:09 Eastern)

No IPv6 endpoint override was applied and no Windows/WSL/Docker settings were
changed. The interrupted launcher command did not replace the Manager config;
its native Host arguments still contain no NatsUrl override. Normal C# settings
have used nats://localhost:4222 since commit 098142f6dd (2026-10-22); the native
Host fallback remains explicit nats://127.0.0.1:4222. Windows currently resolves
localhost to ::1 then 127.0.0.1. There is no evidence of an application change that
switched its preference after reboot.

Repeated protocol probes found Windows 127.0.0.1:4222 TCP connect succeeds but
no INFO arrives, including when PING is sent first. Three repeated IPv6/localhost
probes returned INFO and PONG from the same NATS server immediately. Windows
127.0.0.2, LAN address, WSL guest address and host WSL adapter address all receive
the same broker greeting; Ubuntu's own IPv4 loopback also works. Windows IPv4
Redis PING and PostgreSQL SSL negotiation work. This isolates the observed fault
to the Windows 127.0.0.1:4222 forwarding path, not all IPv4 or an unhealthy broker.

Windows exact loopback listeners on 4222 belong to wslrelay.exe PID 17216;
Docker backend PID 20304 owns the wildcard listener. WSL's documented NAT path
relays bound guest ports through wslrelay.exe. Docker's port publication is owned
by com.docker.backend.exe. The evidence supports a faulty/shadowing specific
loopback relay; its precise internal failure/start trigger is not yet proven.

NATS container is healthy, has no slow consumers, and established application
connections still exchange messages. The first post-reboot native status query
succeeded around 08:56:40; fresh raw-reference exchanges also occurred around
09:03:28. Therefore saying reboot itself made IPv4 universally fail would exceed
the evidence. The fault is established for later NEW IPv4-loopback connections;
its onset is bounded, not known to an exact instant. No packet trace from before
reboot exists to compare address-selection behavior.

Evidence: Native-live-nats-address-probe.json. Before final baseline, restore and
verify NATS INFO/PING/PONG over the configured IPv4 endpoint, not just TCP connect
or container health. Restarting WSL/Docker affects all local stores and should be
coordinated with the user rather than silently replacing the endpoint with IPv6.


## Post-reboot baseline and natural failure (2026-10-28)

After the second reboot/Docker update, repeated IPv4 and IPv6 NATS INFO/PING/PONG
checks passed against the same broker. No endpoint override was applied. The
qualified Host started at 09:24 Eastern; all eight API startup activities passed.
Host PID 45260 and API PID 23004 were recorded. Both futures datasets and options
subsequently reported Healthy. New Daily and Monthly ITI events persisted at
09:25:23, VWAP advanced, EOD price/session updates arrived and market outlook
reported complete with no missing inputs. Actual eGui accessibility observations
confirmed changing ITI extreme (7772.00 to 7775.25), new ITI event, VWAP and newer
console messages. The user confirmed chart visibility. Screenshot capture failed
with FrameArrived timeout, then refreshed-window capture timeout; no further UI
input was attempted. Native-baseline-ui-observations-20261028.json retains the
actual successive accessibility observations.

This is NOT full baseline acceptance. ES trade-session bars did not advance and
MACD remained at the 09:19:59 startup seed after the 09:30 boundary. The existing
C# bar accumulator permanently rejects an epoch after a missing accepted-trade
ordinal. Both current C# and native market-price publishers suppress unchanged
prices while source ordinals advance on all accepted trades. A complete source
for volume/bar analytics must therefore be distinguished from price-change-only
display updates; weakening gap validation would fabricate partial OHLCV. The six
existing accumulator boundary/gap tests pass; this establishes intended safety
semantics, not an accepted live baseline. No recovery fault tests may begin while
this baseline issue remains unresolved.

A natural option incident began 09:30:26 (operation
19ddf513-a027-4227-a9bd-aa5aa712fa21), detected stale required leg progress. API
recovery expired at 09:32:26, issued the UI failure, and Server Manager replaced
API PID 23004 with PID 20976. Both futures generations remained unchanged. The
replacement API entered another incident at 09:33:04. This was not injected and
is not a passing recovery test.

Two implementation defects were identified:

- Native reconnect OptionObservations incorrectly included kind-6 durable trade
  evidence alongside kind-7 quotes. The C# contract LatestOptionObservations
  accepts only kind 7; API catch-up's quote decoder failed on the evidence DTO.
  Rust now copies only kind 7 under the same complete-result bounds, preserving
  evidence separately. Five current-publication tests pass, including mixed
  evidence/quote retention, exact byte-bound failure and detached retirement.
- Internal cache capture froze its one-second result validity before awaited
  source work and earlier cache calculations. A delayed capture can therefore
  reject its own request as expired (InvalidSnapshotRequest), despite valid
  intent. Each capture now freezes API time immediately before capture/ranking.
  No source timestamp, quote freshness threshold or recovery deadline is extended.
  Diagnostic failure detail includes request age, horizon and scope length. Four
  capture tests pass, including delayed validity versus unchanged recovery budget.

Evidence: Native-live-cache-boundary-regression.log and
Native-live-catchup-kind-regression.log. These changes are not yet live accepted;
the failing acceptance session was asked to shut down gracefully before a new
package is deployed. Actual API catch-up, sustained native cache stability and
the futures-bar baseline still require verification. No fault was armed.

## Unified availability/recovery correction: offline accepted (2026-10-28)

Design authority: the final sections of
`Documents/system/MarketData-Rust-FFI-Lifecycle-Design-v0.1.md` and
`Documents/system/MarketData-Rust-FFI-Implementation-Plan-v0.1.md`.

Implemented in the new Rust backend only:

- Per-workload 30-second monotonic availability, covering required risk legs,
  browser risk, discovery pricing, background cache results and publication
  progress. Unrelated traffic cannot renew a missing workload's deadline.
- Initial observation and temporary underlying quote acquisition have the same
  allowance. Acknowledgements/heartbeats/trades cannot substitute for a required
  underlying quote. Control-only workloads retain their explicit quiet policy.
- Capture uses the remaining original operation budget, not a one-second result
  overlay. Source/context validity and explicit caller/strategy financial policies
  remain authoritative. The former internal 1-second age/250-ms skew restriction
  is replaced by the existing native context qualification bounds.
- Browser demand survives UI inactivity until explicit release or runtime/date
  teardown; provisional claims remain distinct from committed trading intent.
- Processing faults are published to supervision before cleanup. A healthy report
  clears the completed setup deadline in the same iteration. Health-channel
  closure after a long idle period starts a fresh incident at detection.
- Maintenance keeps an absolute scheduled wakeup across health notifications.
- Fully valued scopes with no candidates publish explicit empty ranked results.
  Unknown, missing, expired or unqualified inputs cannot become empty success.
  Later expiry choices are still inspected; empty fallback validity is bounded
  by all evaluated source/context/policy expiries. This explained parity change
  replaces missing-scope output with `NoEligibleCandidate` for a known empty result.
- One original 120-second recovery authority, joined replacement, bounded retained
  terminal notification and independent notification transport remain in force.

### Final passing evidence

All logs below are in `.artifacts/rust-marketdata-offline/`.

| Check | Result | Evidence |
|---|---|---|
| Full Rust regression | **394 passed, 0 failed** | `Unified-full-regression-accepted.log` |
| Strict all-target Clippy | **Passed** | `Unified-clippy-final-accepted.log` |
| Actual-duration SDK/owner/publication/reference/metadata/pricing gates | **7 passed, 0 failed** | `Unified-production-deadlines-verified.log` |
| Corrected quote-wait cancellation fixture | **1 passed, 0 failed** | `Unified-release-ffi.log` |
| Actual release DLL through C# discovery/browser/preparation, SDK and broker fixtures | **3 passed, 0 failed** | `Unified-release-ffi.log` |
| Managed facade routes and complete Host constructor graph | **Passed; builds 0 errors** | `Unified-managed-bridges.log` |
| Packaged executable startup/readiness/NATS status/clean shutdown | **Passed** | `Unified-release-host.log` |

The seven duration gates include actual acknowledgement/heartbeat-only quote
absence at 30 seconds, plus six actual two-minute failure paths. Metadata expired
at 120.006 seconds and pricing at 120.042 seconds in the final run. There were no
competing recovery windows. The regular suite includes exact-boundary virtual-time
tests for absence, unrelated traffic, recurrence, capacity, replacement without
overlap, permanent failure, retained notification and health closure after hours
idle. Explicit production/interop checks are counted separately, not added to the
394 regular-test total. Other previously qualified untouched duration gates were
not rerun as part of this correction; fixture regeneration remains manual.

During investigation, the new watchdog correctly exposed two old behaviors:
health events postponed maintenance, and absent executable candidates were silently
represented as missing scopes. Both were corrected and their integration tests
pass. Earlier exploratory runs also exposed a test-provider handshake mismatch;
the quote-only-availability fixture now explicitly acknowledges both trades and
BBO subscriptions while emitting only heartbeats. The final duration run is green.
An earlier build encountered a Windows test-executable lock from concurrent
qualification; final qualification used the completed current executable.

### Built artifact and remaining acceptance blocker

Updated package: `.artifacts/rust-marketdata-qualification-package/Release`.
Native SHA-256:
`a8919caa3d875dbea6b59eb122c807664466f59d6ab1727f568ddbfe3a5d317d`.
The packaged DLL matches `target/release/ifm_market_data_native.dll`. Its manifest
still says `productionAccepted: false`. Normal backend selection is unchanged.

**Live acceptance remains blocked by the outstanding futures/signals/eGui baseline.**
The earlier live investigation found ES trade-session bars not progressing after
an accepted-ordinal gap: the current price-change-only trade publication stream
does not provide the continuity the bar accumulator requires. This correction
does not redefine that stream contract. The API/UI stack is stopped; only an
isolated qualification Host was launched and cleanly joined. No live fault was
injected and no API restart acceptance is claimed. Complete the baseline before
running live recovery acceptance against this package.

## Futures execution continuity correction (2026-10-28)

The native ordered futures stream must carry every accepted execution, including
unchanged prices. The previous C# price-change-only publication behavior is an
intentional parity exception: it advances accepted trade ordinals while omitting
executions, so the bar accumulator correctly invalidates the epoch. Removing gap
validation or renumbering the surviving trades would hide lost volume.

`Market::execution_snapshot` captures the accepted execution without changing the
filtered query/display cache. `Book::publication_state` uses that snapshot for
futures execution contributions, and `Futures::process` emits each accepted trade.
Duplicate/rejected inputs remain suppressed; replay remains aggregate-only.
Existing MessagePack contracts, epoch fencing and genuine-gap validation remain.

Qualification: `futures_bar_continuity` tests the production Book and native wire
encoder. Its explicit integration gate passes those bytes through the actual C#
serializer and `FuturesTradeSessionBarAccumulator`, asserting unchanged-price
executions, exact OHLCV, duplicate suppression and genuine-gap rejection.
The focused bar/ITI/EMA/Bollinger C# regression passes 233 tests with zero failures.
Live baseline verification is required separately; offline results alone do not
certify advancing API signals or rendered UI graphs.

The first live run exposed a second gap source: `MarketDataReadMirror.Apply`
updates the latest-value cache from query snapshots, while `Accept` previously
used the same sequence watermark to filter live delivery. A snapshot overtaking
queued events therefore consumed executions without delivering them to bars.
The cache now tracks its streaming watermark separately. Both paths retain exact
identity and contract validation; older queued live frames may be delivered once
without overwriting newer cached prices. Retirement resets both watermarks.
This is a cache/delivery correction, not managed ownership of native resources.

The full regular Rust regression passes **395 tests, zero failures**, and strict
Clippy passes. The added cache test and related C# regressions pass **33 tests,
zero failures**. Release API build passes with zero warnings/errors. The expanded
Rust-to-C# bridge additionally exercises the actual snapshot-ahead read mirror.
Native release SHA-256:
`ab63fec4c45e03ac51a712597fe9c7bdf94ce2e7017d3e92d1fe99b363aeecc0`.

### Live baseline observations and remaining qualifications

The corrected API/native Host/eGui stack was observed through 17:35 UTC.
All eight startup activities passed. ES and both VX contracts produced current
15-second, 1-minute, 5-minute and 15-minute bars. ES continued across the second
five-minute boundary at 17:35; its 15-second stream reached 28 stored bars with
2,854 executions and 9,070 volume across those stored observations. EMA/BB,
MACD/RSI, EOD, VWAP and market-outlook timestamps advanced beyond startup seeds.
The API live-health observation at 17:35:26 UTC reports Healthy for Host, all
three dataset lanes and actor routing. ITI Daily commands were generated from
current ES trades; no new material ITI event was observed during this interval.
An unchanged historical ITI event is not evidence of a new signal transition.

Independent eGui accessibility observations confirmed green feed health and
changing close/EMA/market-outlook values. Console rows advanced from 13:28:47 to
13:28:57 Eastern. Bringing the current window forward refreshed values; snapshots
of an occluded window had retained earlier accessible content. Screenshot capture
still failed (`window capture timed out: timed out waiting on channel`) and a
maximize attempt failed (`coordinate input geometry is unavailable`). Pixel-level
graph acceptance therefore remains unverified; no recovery fault was injected.

**Environment qualification:** Windows `wslrelay` began accepting IPv4 NATS TCP
connections without sending INFO, despite a passing greeting check earlier in the
same session. NATS inside Docker and `::1` remained responsive. This run uses the
explicit acceptance-only endpoint `nats://[::1]:4222`; normal launch configuration
and Windows networking remain unchanged. Default IPv4 acceptance is still blocked.
One MACD cold snapshot lookup also hit Npgsql's 30-second read timeout while
computing `max(eventVersion)` for a stream/type filter. Later MACD observations
advanced, but this cold-query failure requires separate qualification; the timeout
was not increased and no event-log data or indexes were changed.

Evidence under `.artifacts/rust-marketdata-offline/`: `Futures-continuity-*`,
`Futures-signals-regression.log`, `Futures-snapshot-stream-*`,
`Futures-baseline-ipv6-*`, `Futures-baseline-live-health.json` and
`Futures-baseline-ui-evidence.json`. The package remains `productionAccepted: false`.

## Follow-up: snapshot lookup and default IPv4 baseline (2026-10-28)

Docker containers restarted at approximately 17:46 UTC. PostgreSQL completed
recovery at 17:48:37 UTC. Subsequent NATS probes passed 12 IPv4 and 12 IPv6
INFO/PING/PONG checks. The acceptance Manager was prepared again without its IPv6
override; Host TCP inspection confirmed established `127.0.0.1:4222` connections.
This establishes current connectivity, not a permanent repair of Windows/WSL.

The MACD timeout has a reproduced query-plan hazard: a generic prepared
`max(eventVersion)` lookup may scan the global version index backwards until it
finds the requested stream/type. The no-snapshot case can scan unrelated history.
Adding an index alone did not eliminate that plan in the synthetic probe.

`GetMaxEventVersion` and the snapshot anchor in `GetEventLogFromSnapshotLastNRange`
now materialize filtered snapshot keys before computing max. The schema adds
`ix_event_log_stream_name_version(EventStreamId, EventNameId, EventVersion DESC)`.
The existing development index was built concurrently using
`scripts/Add-EventLogSnapshotLookupIndex.sql`; it is ready and valid. No event
rows, type aliases, payloads or replay ordering were rewritten, and no timeout
was increased. The existing-snapshot lookup measured 0.261 ms and the absent
stream 0.057 ms using the new covering index. These are observed timings, not a
hard latency guarantee or a cache-independent speedup claim.

**38 real PostgreSQL snapshot/replay tests passed**, including generic prepared
plans, 100,000 unrelated events, multiple/duplicate type IDs, empty arrays,
missing streams and missing snapshots. API/Storage Release builds passed with
zero warnings/errors. All eight default-path startup activities passed. The
18:00 UTC boundary produced current 15-minute/hourly/four-hour MACD observations,
and a new material ITI event was stored at 18:00:09 UTC without a snapshot timeout.

That live boundary also exposed a native futures metadata lifetime gap: rebuild
cleared copied canonical bindings before installing the replacement, briefly
making a known VX contract appear unconfigured. Bindings now remain copied
metadata through replacement, and newly resolved bindings are installed before
source startup. Physical queries/prices remain cleared and health-fenced.
The actual native dataset reconstruction test verifies that known IDs remain
mapped while unknown IDs remain unknown throughout replacement. No lease,
provider socket or old-price reader is retained by this correction.

Screenshot verification was retried after the environment restart, with a fresh
window selection. Both captures failed with `window capture timed out: timed out
waiting on channel`. Pixel-level UI acceptance remains blocked; accessible values
can still be inspected. No live recovery fault was injected during this follow-up.

### Final package and infrastructure recurrence

The replacement Release package is `.artifacts/rust-marketdata-baseline-package/Release`.
Its native DLL SHA256 is
`6b6bc84387d685bea5a2c4b339895211cc6c7c0b65fa179ea4a09cdf80a6825d`.
Strict Clippy passed. The full regular run passed 394 tests and exposed one
outdated source-integrity expectation for the reviewed shared continuity fix.
The original baseline manifest remains intact; the guard now pins the exact
reviewed hashes of `DatasetWorkerCurrentValues.cs` and `MarketDataReadMirror.State.cs`.
Its focused rerun passed, giving 395 passing tests across the run and corrected
guard rerun; 16 opt-in tests were excluded. This was not a second full-suite run.

At 18:09 UTC the default IPv4 package started all eight activities successfully,
but new IPv4 connections subsequently stopped receiving INFO. The loopback
listener was `wslrelay.exe`; Docker's backend also owned the wildcard listener.
The test-only Manager was restarted with the explicit IPv6 endpoint. ES 15-second
and one-minute bars, MACD/RSI and market-outlook revisions advanced again. VX and
options remained degraded, so this is **not** a successful complete baseline.
At 18:13:45–47 UTC both Windows IPv4 and IPv6 probes accepted TCP but timed out
waiting for INFO; a probe inside the NATS container immediately received INFO.
The earlier successful IPv6 probe is therefore not a stable workaround.

The live baseline remains blocked by host-to-container NATS forwarding and
pixel-level UI capture. No Docker/WSL configuration, normal launch profile or
event-log rows were changed. The separate acceptance Manager uses its explicit
IPv6 test override. No final recovery fault-injection acceptance was attempted.
Evidence: `Nats-forwarding-recurrence*.json`, `Nats-forwarding-final.json`,
`Futures-binding-*.log`, `Futures-final-*` under the offline artifact directory.

### Isolated forwarding investigation, IFM stopped (18:25–18:28 UTC)

The fault was reproduced without any IFM application running. The production C#
`NatsConnectionManager` failed to connect to `127.0.0.1` (about 2.12 seconds) and
`::1`, but connected to `127.0.0.2` in about 72 ms and completed PING in 1.8 ms.
Raw TCP probes to `127.0.0.2` and the Windows Ethernet address also returned the
same NATS server identity. Thus this is not a Rust-only protocol/client failure.

Accepted-socket attribution and broker counters distinguish the paths:

| Destination | Windows acceptor | INFO | NATS total connections |
| --- | --- | --- | --- |
| `127.0.0.1:4222` | `wslrelay.exe`, PID 52180 | none within diagnostic read limit | unchanged: 3380 → 3380 |
| `127.0.0.2:4222` | `com.docker.backend.exe`, PID 39676 | 424 bytes immediately | incremented: 3380 → 3381 |

At the initial broker observation only one client was connected, CPU was zero,
and slow-consumer count was zero (configured maximum connections 65,536).
That surviving .NET connection had been open since 17:46 UTC. Existing working
connections therefore do not establish that fresh loopback connections work.
The failed loopback relay shadows the working Docker wildcard listener. The
internal reason that relay stopped forwarding has not been established.

The machine is Windows 10 Pro 22H2/build 19045, WSL 3.0.3, Docker Desktop 4.97.0.
`.wslconfig` requests mirrored mode, but `wslinfo --networking-mode` reports `nat`.
Mirrored mode requires Windows 11 22H2 or later according to Microsoft. NAT-mode
WSL automatically uses `wslrelay.exe` for localhost forwarding; Docker Desktop
independently publishes its ports through `com.docker.backend.exe`.

Both current C# Host and native Host default to `nats://127.0.0.1:4222`.
C# shares its connection at process scope; native datasets/reference exchanges
own independent connections. This changes exposure to new-connection failures,
but is not proof of a native load trigger, and no historical before/after capture
establishes when the relay first broke or which upgrade caused it.

Recommended controlled repair experiment: back up `.wslconfig`, use explicit
NAT with `localhostForwarding=false`, stop Docker/WSL cleanly, then restart Docker
and verify that its backend owns the loopback listener and that repeated C# and
native connect/PING tests pass on `127.0.0.1`. Disabling WSL localhost forwarding
also changes access to non-Docker Linux services; those need separate checking.
This experiment has not been applied. `127.0.0.2` is diagnostic evidence, not a
permanent application configuration change. IFM remains stopped.

References:
- https://github.com/microsoft/WSL/blob/master/doc/docs/technical-documentation/localhost.md
- https://learn.microsoft.com/en-us/windows/wsl/networking
- https://docs.docker.com/desktop/features/networking/

Evidence: `Nats-investigation-{varz,connz,address-matrix,relay-versus-docker}.json`
and `Nats-investigation-csharp-probe.jsonl`; the C# diagnostic source is under
`.artifacts/nats-forwarding-investigation/`.

### Authorized forwarding repair (18:30–18:32 UTC)

At the user's request `.wslconfig` was backed up to
`C:\Users\basil\.wslconfig.before-docker-forwarding-20261028-143008.bak`.
Its networking mode is now explicit `nat` with `localhostForwarding=false`;
the existing DNS setting was preserved. Docker Desktop was stopped using its
CLI, WSL was shut down, and Docker Desktop was started normally. All 12 previously
running containers returned; no containers or volumes were deleted. IFM stayed
stopped throughout.

The 4222 listener is now owned solely by `com.docker.backend.exe` (PID 54124),
with no competing WSL loopback relay. Fifteen independent connections to
`127.0.0.1:4222` over approximately 42 seconds all received INFO and PONG.
The production C# connection manager also connected and pinged successfully
using `127.0.0.1`, `127.0.0.2` and `::1`. Windows-side protocol probes passed for
PostgreSQL SSL negotiation (5432), Redis PING (6379), and Scylla CQL OPTIONS
(9042). NATS HTTP health returned `ok`; PostgreSQL and Scylla reported healthy.
Starting the Ubuntu distribution did not recreate the competing Windows listener.
The inspected Linux listeners corresponded to Docker's published services; no
additional independent WSL service was identified for application-level testing.

This validates the repair during the observed interval; a trading-load soak
and the full native acceptance baseline have not been rerun. No application
NATS URLs or normal launch settings were changed. Evidence:
`Nats-forwarding-repair-{repeated,service-ports}.json`,
`Nats-forwarding-repair-csharp.jsonl`, and the saved container/backup inventories.

### Repaired-network live baseline (18:36–18:44 UTC)

The native acceptance stack was started without a NATS endpoint override. All
eight application startup activities passed. ES and both VX contracts produced
new 15-second, one-minute and five-minute bars; EMA/BB, MACD and RSI timestamps
advanced. New daily/weekly ITI events were persisted, current EOD-session prices
and VWAP were updated, and market-outlook revisions advanced. The eGui accessible
tree independently showed current numerical analytics, ITI strategy rows and
advancing market-outlook console messages. Graph pixels remain unverified.
The Docker backend retained sole ownership of the Windows NATS listener during
this interval; the native Host had established IPv4 broker connections.

The saved four-leg trade did not fully restore. Three sequential contract starts
succeeded; the fourth remained unavailable, while the native options supervisor
reported `Required output unavailable for 30 seconds: option/42969123`. The
whole restore query canceled after 120 seconds, and native dataset recovery
subsequently exhausted its own original deadline at approximately 18:40:12 UTC.
The native options status became `StoppedError`; ES/VX remained healthy. The
eGui accessible tree showed `Application Error` / `The two-minute dataset
recovery deadline expired`. This is a naturally occurring baseline failure,
not an injected recovery acceptance test. The complete baseline did not pass.

The prior watchdog text omitted the actual missing calculation/source evidence.
Added fault-only bounded diagnostics include calculation failure, quote ages,
configured quote limit, context validity and whether publication was observed.
They do not change pricing qualification, readiness or recovery timers. The
focused diagnostic test passed (including extreme timestamp arithmetic), and
strict all-target Clippy passed. Live diagnosis with the updated package follows.

Evidence: `Futures-repaired-*`, `Option-availability-diagnostic-*` under the
offline artifact directory. No recovery fault was injected.

### Required-risk starvation diagnosis and correction

The diagnostic package (`5f6a736360bb87ff1c377c28b54f34e784c57c271a7cb99f7b71f222db656a01`)
reproduced the failure on a working IPv4 broker. At 18:48:29 UTC the required
instrument reported `CalculationPending`, valid reference context, 5,000 ms
quote limit and publication not yet seen. The sampled option and underlying
events were fresh (approximately -4/-509 ms relative to the owner iteration's
earlier timestamp). The positive failure evidence therefore identified pending
calculation/publication starvation, not an expired old quote.

Full-risk commit discarded a completed result whenever a newer quote arrived
during calculation. Strict reads, readiness and publication also required the
completed source sequence to equal the newest ingress sequence. A busy feed
could consequently prevent any required risk result from being published.

The correction retains the completed immutable quote/risk pair and provides a
separate completed-risk publication read. It keeps exact source sequence/time,
context identity, physical generation, existing financial age limits and expiry.
Strict current-quote queries still return `CalculationPending` for superseded
sources. Publication fences against physical/reference replacement and a newer
completed calculation; it never labels older Greeks with a newer quote. Invalid
undefined quotes also prevent publication. Readiness uses qualified completed
risk while the next quote calculates.

Focused regression passed: 13 coalesced-pricing tests and two native-context
tests, including ingress advancing after batch capture, exact original-source
event encoding, strict query behavior, age expiry and physical retirement.
Strict all-target Clippy passed. Full regression and new-package live acceptance
are being completed; this section does not mark production acceptance.

### Completed-risk correction: regression and live result (18:55 UTC onward)

The complete regular regression run passed **397 tests, zero failures**, with
16 explicitly opt-in tests excluded. This is a fresh full-suite result, not an
aggregate of reruns. Strict all-target Clippy and Release packaging passed.
Package: `.artifacts/rust-marketdata-completed-risk-package/Release`;
native DLL SHA256:
`68851777bfbe97ca5e1a3127b192ba1c16a469cb31b7434529e60694dcad17b2`.

With the repaired default IPv4 endpoint, all eight startup activities passed.
The saved four-leg feed for trade `101.701.1901.1501` restored successfully in
31,753.6 ms at 18:56:21 UTC. At 18:56:40 all three dataset roles were running and
healthy with no pending recovery deadline. HTTP live health subsequently reported
Healthy for session authority, Host, both futures datasets, options and actor
routing. ES/VX one-minute bars and new daily/weekly ITI events continued; eGui's
accessible tree showed numerical analytics and market-outlook delivery.

Subsequent observations also show background coverage/cache-triggered options
rebuilds. These returned to Healthy, retained the futures identities and did not
produce a terminal deadline error in the observed interval, but remain churn
requiring review; this result must not be presented as an uninterrupted options
soak or full production acceptance. No artificial fault has been injected.

The newly launched eGui is on its main screen. A user request to open trade
1901:1501 and enable Live Feed is pending because automated input/capture remains
unavailable. Loaded-trade UI and recovery acceptance therefore remain open;
graph pixels are not independently certified. Normal launch defaults are unchanged.
Evidence: `Option-completed-risk-*` under the offline artifact directory.
