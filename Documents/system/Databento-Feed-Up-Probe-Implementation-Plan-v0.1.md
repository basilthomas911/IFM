# Databento Feed Up Probe Implementation Plan v0.1

| Item | Value |
| --- | --- |
| Plan ID | `DBUP` |
| Status | Implemented and qualified for the selected C++ runtime |
| Date | 2026-09-01 |
| Scope | One synchronous, non-mutating Databento runtime up/down call |
| Out of scope | Reset, retry, persistence, watchdog scheduling, UI navigation policy and data freshness |

## Objective

Expose one application-level call that returns `true` only when every configured native Databento
feed and its managed aggregation worker are running. The call must be safe to invoke at any time,
must not require a market record, and must return `false` for expected operational unavailability.

```csharp
bool IsDatabentoFeedUp(TimeSpan? timeout = null);
```

The default timeout is one second. The existing atomic `dbf_feed_get_stats` operation remains the
native source for `FeedState` and terminal status in both the C++ and Rust implementations.

## Gates

| Gate | Deliverable | Exit evidence | Status |
| --- | --- | --- | --- |
| DBUP-01 | Public contract and binding semantics | Contract approval test | Complete |
| DBUP-02 | Native-to-aggregation-to-epoch health propagation | Framework/application compilation and focused tests | Complete |
| DBUP-03 | Boolean API, default timeout and fail-closed behavior | Running/stopped/faulted/timeout tests | Complete |
| DBUP-04 | API readiness integration | `databentoFeedUp` field and readiness dependency | Complete |
| DBUP-05 | Unit, integration and verification coverage | Focused and full affected suites | Complete |
| DBUP-06 | Regression, documentation and completion evidence | Clean builds, diff check and recorded results | Complete |

## Binding behavior

- No active epoch returns `false`.
- Native state other than `Running` returns `false`.
- Native terminal status other than `Ok` returns `false`.
- Any completed/stopping managed aggregation worker returns `false`.
- Any configured dataset feed being down makes the single global result `false`.
- A running feed without recent records returns `true`.
- A non-positive or exceeded timeout returns `false`.
- Expected health-enumeration failures return `false` and do not initiate recovery.
- The method never starts, stops, resets, replaces or rolls a Databento epoch.

## Qualification matrix

- Unit: absent epoch, running epoch, stopped aggregation, invalid timeout and stopped epoch.
- Verification: multi-dataset partial failure, native fault, `ConnectionHung`, health-read failure and
  elapsed timeout.
- Integration: real `TickAggregationService` lifecycle with a connected quiet feed, native fault,
  terminal status and managed stop.
- Native qualification: selected C++ `dbf_feed_get_stats` lifecycle suite; the existing Rust ABI 2
  baseline remains incompatible with the managed ABI 3 wrapper and is recorded separately below.
- Regression: full Databento framework, Application MarketData and MarketData Feed integration suites,
  plus API Server build.

## Completion record

Completed on 2026-09-01 with the following evidence:

- Application MarketData unit and verification suite: 93 passed, 0 failed.
- Databento C++ framework suite: 132 passed, 0 failed.
- Quiet-feed lifecycle integration scenario: 1 passed, 0 failed.
- API Server isolated-output build: succeeded with 0 warnings and 0 errors while the development
  API process remained running.
- `git diff --check`: passed; only repository line-ending notices were emitted.

The broad MarketData Feed integration run reported 42 passed, 4 skipped and 6 failures in legacy
shared-fixture/start-event scenarios. The new probe integration scenario passes independently; none
of the six failures executes or asserts the new probe. The Rust targeted lifecycle run reported 24
passed and 3 failures because the existing Rust library advertises ABI version 2 while the managed
wrapper requires ABI version 3. No native ABI was changed by DBUP, and the selected C++ implementation
is fully green. Rust ABI 3 parity remains an explicit follow-up before selecting Rust for runtime use.
