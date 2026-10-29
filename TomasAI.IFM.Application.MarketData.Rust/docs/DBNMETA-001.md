> Update: addressed at the shared live/historical/file metadata boundary by the fixed-buffer parser and scoped
> SDK hook. See [qualification and limits](METADATA-STREAMING-QUALIFICATION.md).
> The original source audit/reproduction below remains valid for the unchanged
> upstream decoder. Public historical and file entry points are covered by the shared dependency patch.

# DBNMETA-001: pinned decoder resource-bound qualification blocker

Date: 2026-10-26

Status: **Open. Stop full-port acceptance at this dependency boundary.** The new
backend is not registered in Host. The current backend, its native bridge and
running feeds have not been changed.

## What was verified

The new transport now uses the real pinned Databento SDK for three isolated logical
lanes. Network cancellation and ordinary decoder-error retirement work in local
protocol fixtures. During the remaining malformed-input/resource audit, the DBN
decoder's metadata allocation path failed the bounded-resource requirement.

Pinned versions are Databento `0.58.0` and DBN `0.66.0`.

`dbn-0.66.0/src/decode/dbn/fsm.rs`, `DbnFsm::decode_prelude`:

1. Reads the metadata length as a wire-supplied `u32`.
2. Checks the minimum metadata size and supported version.
3. Calls `buffer.grow(length + METADATA_PRELUDE_LEN)` without a maximum length.

`decode/dbn/aligned_buffer.rs`, `AlignedBuffer::grow` allocates a zero-initialized
boxed buffer synchronously. The requested size comes from the prelude, before the
metadata body has arrived. The SDK client's `buffer_size` configures an initial
size; it is not an upper bound and cannot fix this behavior.

There is a second source-audited allocation gap in
`DbnFsm::decode_metadata_symbol_mappings`: the wire mapping count is used for
`Vec::with_capacity(count)` before validating that the metadata buffer can contain
that many minimum-size mapping entries. Metadata symbol width also needs explicit
validation before count-based allocation/iteration. No huge-count/OOM test was run.

## Bounded reproduction

`tests/dependency_limits.rs` feeds only this eight-byte prelude to the actual
pinned DBN finite-state decoder:

```text
DBN version 3; announced metadata length 1,048,576 bytes
```

The decoder is constructed with a 65,536-byte initial buffer. After processing only
the prelude, its writable buffer space is **1,048,576 bytes**, although no metadata
body has arrived. The reproduction deliberately stays at 1 MiB. It does not send
multi-gigabyte lengths or attempt to exhaust the workstation's memory.

Command:

```powershell
cargo test --offline --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --test dependency_limits -- --nocapture
```

Evidence: `.artifacts/rust-marketdata-lanes-tests.log`.

## Verification of the current increment

The default regression run completed **44 tests with no test failures**: the
previous 33 checks, eight real-SDK lane/transport fixture checks, two owned-record
boundary checks and the one bounded dependency-gap reproduction. The latter proves
the missing limit; it does not satisfy the memory-bound acceptance criterion.

Rustfmt, all-target Clippy with warnings denied, and Rustdoc with warnings denied
pass. The unchanged-backend hash test still verifies all 729 frozen inputs. The
full log is `.artifacts/rust-marketdata-lanes-regression.log`. Earlier explicit
two-minute DNS/SDK timer qualifications remain recorded separately; they were not
rerun or represented as new tests in this increment because the shared deadline
primitive and those dependency versions did not change.

This passing test confirms the gap; it is **not** a passing bounded-memory acceptance
test. The larger-allocation risk follows from the inspected source, not from a
reproduced production incident or an intentional host crash.

## Why cancellation alone does not resolve it

The allocation happens synchronously inside polling the decoder future. Tokio
`select!` cannot preempt that synchronous operation. If allocation exhausts memory
and aborts the process, `catch_unwind` cannot turn it into an ordinary recoverable
provider failure. The new design puts all lanes in the Host process, so this
dependency path cannot be accepted as bounded merely because socket cancellation
and the two-minute timer tests pass.

No public SDK builder option installs a metadata/count limit or a replacement
reader/decoder in the existing `LiveClient`. An outer timeout, a smaller initial
buffer, another admission flag or detaching a task would not enforce the bound.

## Required resolution

The smallest recommended change is an **isolated hardened decoder dependency for
the new backend only**, retaining the current backend and old native crate exactly:

1. Keep an auditable copy of DBN 0.66.0 with upstream provenance, license notices,
   source hashes and a small documented patch set. Do not edit the Cargo registry
   cache or globally patch the legacy bridge.
2. Reject oversized metadata before buffer growth. Establish limits from the
   supported live/historical workloads; do not silently reject legitimate baseline
   metadata using an invented blanket size limit.
3. Validate symbol widths, mapping counts and interval counts against encoded
   remaining bytes before allocating. Use checked arithmetic and fallible
   reservations that return typed decoder errors.
4. Return malformed/capacity errors through the lane's existing recovery episode,
   without renewing its deadline or losing terminal failure notification.
5. Compare valid metadata/record outputs exactly against the unchanged decoder,
   across every currently supported DBN version and representative large chains.
6. Add oversized-length/count, zero-width, truncated and allocation-failure tests;
   verify memory ceilings, cancellation, cleanup, retries and the real 120-second
   deadline. Requalify the patched dependency before enabling full-port work.

An alternative is to replace the SDK's private decoding boundary with an owned
transport and bounded decoder. That carries more protocol/compatibility work.
Neither option introduces a production worker process or changes actors/API
consumers. Both revise the previously qualified, unmodified dependency boundary;
the existing DNS qualification remains useful but does not resolve this gap.

## Completed work alongside the finding

- Owned quote/trade/order/statistic normalization retaining integer precision,
  replay/snapshot/undefined flags and no borrow into the SDK buffer.
- Actual SDK setup/read transport with explicit DNS resolution and one setup
  budget, full consumed error/system codes, redacted diagnostics and client
  retirement on interrupted/failed reads.
- Three lane owners, lane-specific reset/stop, retained terminal status, original
  episode budgets, readiness proof and processing-error/panic recovery.
- Local fixture coverage of healthy reads, warning/error delivery, replacement,
  missing acknowledgement/proof, deadline expiry, reset during stalled auth and
  options reset with both futures lanes continuing.

These components are not a complete IMarketDataApi implementation. Durable leases,
full data/financial kernels, provider/reference/history closure, complete facade,
Host selection, performance qualification and live actor/UI acceptance remain
unfinished and are not marked accepted.
