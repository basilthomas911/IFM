# Regime Discovery Market Signal Interface MDSI-2 Historical Provider

Databento Historical Provider and Application API v1.0

| Item | Value |
| --- | --- |
| Gate | `MDSI-2 - Databento Historical provider/application API` |
| Status | Complete |
| Date | 2026-08-25 |
| Live qualification | Estimate-only preflight passed; no batch was submitted or downloaded |
| Design authority | `Regime-Discovery-Market-Signal-Interface-Design-v1.0.md` |
| Implementation authority | `Regime-Discovery-Market-Signal-Interface-Implementation-v1.0.md` |

## 1. Gate conclusion

MDSI-2 provides a provider-neutral historical API above a Databento adapter.
Domain projects depend only on historical request, estimate, batch, file,
decode, and normalized-record contracts owned by Framework and Application.
DBN schema values, provider job payloads, native handles, compression, and
Databento credentials remain inside the adapter.

## 2. Native and managed boundary

The native ABI supports estimate, direct range, batch submit/status/file list,
download, DBN file decode, and deterministic synthetic execution. Returned
native results use an explicit release export and the managed adapter owns them
through `SafeHistoricalResultHandle`.

The live implementation uses continuous symbology, DBN/Zstandard batch output,
symbol mapping, bounded waits, file size/hash metadata, and native DBN decoding.
The synthetic implementation exercises the same managed lifetime and decode
flow without a network or credential.

## 3. Security and dependency injection

Options contain behavior and timeout settings only. The API key is read by the
provider at execution time and is never copied into domain messages, manifests,
logs, or persisted options. Synthetic and live providers are selected through
the Framework registration extension; the Application registration exposes
only `IMarketDataHistoricalApi` and the provider-neutral orchestration services.

## 4. Accepted qualification

| Check | Result |
| --- | --- |
| Native debug build and CTest | 1 passed |
| Native live-enabled build | Passed |
| Databento Framework unit | 123 passed |
| Databento integration | 7 passed |
| Opt-in live estimate smoke test | 1 passed |
| API Server build | 0 warnings, 0 errors |

The live smoke test performs an estimate below the configured ceiling. It does
not start a feed, submit a batch, or download historical data.

## 5. Exit decision

Offline fixtures pass, managed/native handles are deterministically released,
the live native adapter compiles, and the opt-in live preflight succeeds. The
gate is complete.
