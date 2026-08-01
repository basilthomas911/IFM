# Databento Phase 5 implementation

Phase 5 adds a synchronous, one-shot latest-price client that owns one temporary
Databento live session and closes it before returning.

**Status:** Code complete. Deferred credentialed runtime confirmations are
tracked below and do not block Phase 6.

## Public API

- `IDatabentoFeedFactory.CreateLatestPriceClient` creates a client fixed to one
  dataset.
- `IDatabentoLatestPriceClient.GetLatestPrice` requires one
  `LatestPriceRequest` and an explicit timeout.
- `LastTrade` subscribes to `trades`; `QuoteMidpoint`, `Bid`, and `Ask` subscribe
  to `mbp-1`. A requested policy never falls back to another observation.
- `NextObserved` accepts only a zero lookback. `ReplayLookbackThenLive` requires a
  positive, caller-bounded lookback and waits for Databento's replay-complete
  marker before returning the newest qualifying replay value. If replay has no
  qualifying value, it continues until a qualifying live record or timeout.

## Selection semantics

- Undefined prices and zero-size levels do not qualify.
- A locked quote is valid. A crossed quote cannot satisfy `QuoteMidpoint`, while
  its individually valid bid or ask can still satisfy the corresponding policy.
- Midpoint uses overflow-safe `std::midpoint`; an odd fixed-price interval rounds
  toward the bid, the first midpoint operand.
- The 64-byte result reports bid, ask, and trade presence independently and marks
  whether replay supplied the result or the final selected record was live.

## Lifecycle and admission control

- One monotonic deadline covers local admission, connection/authentication,
  subscription, replay/live observation, and native cleanup.
- A process-wide coordinator permits at most one temporary latest-price session
  per dataset and at most five connection starts in any sliding one-second
  window across datasets.
- Admission waits consume the caller's deadline. A rate or dataset permit is
  never acquired by retry-spinning.
- The managed dataset permit is held around the native call and released on
  success, timeout, or exception.
- Native `LiveBlocking` ownership is guarded immediately after construction.
  `Stop` runs explicitly on success and from the guard on every error/timeout
  path; no native worker or session escapes the synchronous call.

## Verification

- Native synthetic ABI/lifecycle tests pass, including 88-byte request and
  64-byte result layouts, invalid-policy rejection, and the no-live-build
  `DBF_NOT_SUPPORTED` path. A fake native session proves the guard calls `Stop`
  exactly once for explicit success cleanup and scope-exit error/timeout cleanup.
- The pinned Databento v0.62.1 live adapter compiles and its native tests pass.
- Managed unit tests cover all four price policies, both freshness modes,
  crossed quote rejection, invalid selector/lookback rejection, timeout cleanup,
  the single-session dataset budget, and the five-start governor.
- Integration coverage first proves a valid Databento Historical connection and
  then exercises invalid latest-price selectors.
- Opt-in smoke coverage discovers an activated, unexpired ES future at runtime
  and requests each policy with bounded replay. No fixed or expired contract is
  used.

## Deferred credentialed runtime confirmation

Run with `DATABENTO_API_KEY` and
`IFM_RUN_DATABENTO_SMOKE_TESTS=1` (or the compatibility live-test switch), using
a live-enabled native build. Record observations for all four policies during or
shortly after an active ES trading interval so the one-hour replay window contains
a qualifying record. Also rerun the integration suite with
`IFM_RUN_DATABENTO_INTEGRATION_TESTS=1`.

When all phases are code complete, rerun the complete credentialed smoke and
integration suites and record their results as final runtime acceptance evidence.
A deferred runtime confirmation is not an incomplete Phase 5 code deliverable
unless it exposes a defect.
