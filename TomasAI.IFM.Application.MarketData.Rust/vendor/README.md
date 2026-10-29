# Isolated SDK and DBN dependency copies

These path overrides apply only to the new Rust backend's Cargo root. The original
backend, original Databento Rust bridge and registry cache are unchanged.

## Provenance

- Databento 0.58.0 registry checksum: `534a3eb69444c181c67f9775c53facd6f559308033c01eadac97aa64cee75cdb`.
- DBN 0.66.0 registry checksum: `0cf85112fb961b839a80879b0cc46c3669654cc01e443a3cea93140e9ae6f28d`.
- Original copied files: `UPSTREAM-SHA256.json` and `DBN-UPSTREAM-SHA256.json`.
- Upstream revisions: each copy's `.cargo_vcs_info.json`.
- Both use Apache-2.0. Databento's packaged LICENSE is preserved. DBN's LICENSE
  comes from https://raw.githubusercontent.com/databento/dbn/v0.66.0/LICENSE
  because the registry source archive omitted it.

## Changes

Databento: one additive live `start_with_metadata_decoder` hook allows readiness
retention instead of retaining unused arrays. The standard `start` is also safe
because the shared DBN decoder underneath it is now bounded.

Historical HTTP helpers also bound success JSON (8 MiB), error bodies (64 KiB),
JSONL lines (64 KiB), total decoded JSONL (8 MiB) and retained JSONL results
(32 MiB). These checks cover the SDK entry points in addition to the new backend's
owned HTTP transport. They reject oversized declared lengths before reading and
use fallible growth. Response bodies are not dumped into deserialization logs.
The new backend's production historical client owns/polls its HTTP connection
driver directly, uses the qualified DNS path and explicit Rustls/WebPKI settings,
and preserves all provider warning headers through a separately bounded drain.

DBN: `decode/dbn/streaming.rs` owns incremental async parsing. `streaming_sync.rs`
is mechanically derived by `qualification/Generate-SyncMetadata.py`. Both retain
exactly 65,536 bytes of input scratch and have an independent retained-state budget.
The sync and async public metadata entry points use these implementations, covering
live, historical HTTP streams, saved files, compressed streams and dynamic readers.
The record FSM is installed only after metadata succeeds, preserving original wire
version/ts_out/upgrade policy. Cancellation requires discarding the reader/client.
The async decoder has an additive `into_parts` ownership transfer so a completed
historical request returns retained metadata without a second deep clone/allocation.

Low-level push FSM users get an explicit error before metadata buffer growth
beyond 65,536 bytes. Their count/width/body-boundary validation is hardened. Large
metadata must go through the pull streaming API, not the push FSM.

Legacy DBZ (pre-DBN) dynamic readers cap compressed metadata and expanded metadata
at 65,536 bytes each and validate counts before reservations. Oversized legacy
metadata explicitly fails; current DBN v1..v3 retain the full u32 streaming range.

## Maintenance

Audit all entry points when upgrading either dependency. The independent
`qualification/MetadataOracle` Cargo root intentionally uses the unpatched registry
DBN and generates `baseline/MetadataOracle.txt`; it has no dependency path overrides.
Do not regenerate this oracle from the patched root. Preserve all licenses and
review the small file patch set against original hashes. No vendored package is
used by the current production backend.


The isolated live protocol also bounds each greeting/challenge/auth response to
65,536 bytes including newline, with fallible allocation, strict UTF-8 and explicit
truncation/oversize errors. It consumes no following protocol/DBN byte. This closes
another network-controlled allocation path without changing the original bridge.
