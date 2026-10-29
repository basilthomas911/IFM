# Bounded metadata: all-path implementation and qualification

Date: 2026-10-26

## Implemented scope

Bounded metadata parsing is installed at the shared DBN dependency boundary in the
new Rust backend, not deferred to future historical integration. The old backend
and old Rust bridge remain untouched. This does not complete the wider IMarketDataApi
port or enable the new backend in production.

| Entry point | Implementation |
| --- | --- |
| New owned live sessions and lifecycle probe | Incremental async parser, readiness retention |
| Standard SDK LiveClient.start | Shared bounded async metadata decoder |
| Historical timeseries.get_range | Shared bounded async decoder after streaming Zstd decompression |
| Historical get_range_to_file and reopened saved file | Same bounded async decoder on HTTP and file reads |
| AsyncMetadataDecoder / AsyncDbnDecoder / compressed async readers | Shared incremental async parser |
| MetadataDecoder / DbnDecoder / compressed sync readers | Generated synchronous equivalent of the same parser |
| DynDecoder inferred/raw/Zstd/file reads | Public synchronous decoder; no DBN whole-body buffering |
| Low-level push DbnFsm | Rejects metadata larger than its 65,536-byte capacity before growth; validates counts against declared body only |
| Legacy DBZ dynamic reader | Explicit compressed and expanded metadata caps of 65,536 bytes each; validated counts and fallible reservations |

The raw metadata scratch capacity is 65,536 bytes. Retained symbols and mappings
have a separate checked 16 MiB default budget; full-retention history reads return
all required fields or an explicit budget error. Live readiness validates but
omits arrays it does not consume. Allocator overhead, fixed structs, record/auth
buffers and decompressor working memory are separate; total process memory is not
claimed to be 65,536 bytes.

A Take reader enforces the DBN body boundary. Counts must fit remaining encoded
bytes before fallible allocation; UTF-8, enums, symbol width and interval dates
are validated. Trailing bytes accepted by upstream (including alignment) are
consumed without retention. Fixed-buffer compaction handles fragmented fields.
Async reads yield during always-ready input and processing, and retain the existing
absolute caller deadline. An interrupted metadata parse is abandoned with its
reader/client, never resumed. Sync file readers are synchronous and are not used
as cancellable network tasks.

## Explicit compatibility differences

- Valid DBN v1..v3 metadata is compared against an independently locked, unpatched
  registry DBN oracle. Record upgrade policy and metadata fields are preserved.
- Full-retention results exceeding the configured application budget fail explicitly.
- Push-only FSM metadata above 65,528 body bytes must use the streaming reader API.
- Legacy DBZ metadata above either 65,536-byte cap fails explicitly. DBZ is a legacy
  format distinct from current DBN v1..v3; it is not silently routed to an unsafe
  decoder. Streaming the full current DBN u32 range remains supported.

## Tests

- 120 exact comparisons across DBN v1..v3, valid widths 8/22/71/65,535, empty/single/
  1,001-symbol lists and fragmented reads. An independent registry oracle supplies
  SHA-256 hashes of complete decoded metadata, avoiding a self-comparison after patching.
- Every truncation prefix of a populated fixture; invalid prefix/version/length,
  enums, UTF-8, zero width, schema definitions, counts, interval dates and budgets.
- 4,000 deterministic multi-byte mutations; errors or bounded valid results only.
- Body boundaries around 65,536 and 131,072; sentinel next-record bytes preserved.
- Generated valid symbol streams up to 4,294,967,295 body bytes plus eight prelude
  bytes. No giant source Vec is constructed. Input capacity and every read request
  remain at most 65,536; large discard-mode peak extra allocations were 65,545 bytes.
- Synchronous generated stream near 4 GiB also completes with the fixed input buffer.
- Deterministic allocation failures for scratch/vector/string reservations.
- 1,000 cancellations during always-ready large streams and stalled-reader deadlines.
- Local real SDK live recovery: malformed metadata then healthy replacement; repeated
  errors/stalls use the original episode deadline and close sockets.
- Standard LiveClient.start is tested separately from the custom hook.
- Public sync/async/dynamic/compressed readers decode large metadata and the following
  records with correct v1..v3 version policy behavior.
- Actual localhost HTTP historical get_range and get_range_to_file, including
  decompression, saved-file reopening, invalid/truncated data and stalled response.
- Legacy DBZ valid fixture, huge wire length, decompression expansion and invalid counts.
- Push FSM count checks cannot consume subsequent records to satisfy malformed counts.
- Windows Job Object caps the dedicated memory test child at 256 MiB; no running
  application process is attached or terminated.

Final counts, deadline timings and process-memory results are recorded below after
executing the complete regression. No finite suite establishes universal absence
of defects; acceptance refers to the executed cases and explicit scope above.

## Reproduce

```powershell
cargo test --offline --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml
cargo test --offline --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --test lane_transport real_120_second_metadata_stall -- --ignored --nocapture
python TomasAI.IFM.Application.MarketData.Rust/qualification/Run-MetadataMemory.py <metadata_memory-test-executable>
```

The Windows harness applies the memory ceiling before resuming its suspended test
child. It introduces no production subprocess. The oracle has a separate Cargo.lock
and uses unpatched registry DBN: `qualification/MetadataOracle`.

## Final executed results

- Full default regression: **58 passed, zero failed**. Three slow tests are excluded
  by default; the metadata-specific one was separately rerun against the final code.
- Real metadata stall deadline: **120.0053485 seconds**, socket closed, test passed.
- Memory-qualified test child: **256 MiB ceiling**, **2,621,440-byte peak private
  memory**, exit zero. This includes the synchronous near-4-GiB stream test.
- DLL build and existing managed qualification harness: passed (1,107 C# numerical
  cases and 1,000 ownership/cancellation/disposal cycles in that harness).
- fmt, Clippy with warnings denied, rustdoc with warnings denied and whitespace
  checks pass. The frozen original-backend preservation test passes.

Evidence: `.artifacts/rust-marketdata-all-paths-regression.log`,
`rust-marketdata-all-paths-deadline.log`, `rust-marketdata-all-paths-memory.log`,
`rust-marketdata-all-paths-managed.log`.

No live provider connection, application restart or database mutation was used.
The new backend is still not enabled in Host; remaining full API port acceptance
is separate from this completed metadata-boundary change.
