# Futures EOD live cache, history isolation and financial decision evidence

## Status

Development enables `MarketData:FuturesEodMinuteBatchWriter`. The running API has not been restarted; activation requires a rebuilt API and graceful restart. Verification uses isolated `bin/EodVerification` outputs because the running application owns the normal output DLLs.

## Live path

- Exact contract/value-date reads prefer the authoritative process-local ES/VX cache. Database fallback applies only on a miss; a concurrent live snapshot wins even if the fallback fails.
- Cache publication and immediate realtime notification precede history capture. Cache entries do not expire because a database write failed. ES exposes observation/persistence metadata separately, and older persistence acknowledgements cannot replace newer prices.
- ES/VX prune older cached dates on trading-date rollover. Historical range/previous-session queries retain their storage semantics.
- Cold VX computation starts from incoming tick/session statistics without querying storage. Cold ES initialization and genuinely historical dependencies can still require storage; this implementation does not promise an initialized cache after process restart without feed/warmup inputs.

## Independent history path

- Admitted observations are atomically written to the local `History/FuturesEod` spool under the application directory, configurable with `MarketData:FuturesEodHistorySpoolPath`. Each retained observation carries exact source event bytes and its calculated snapshot.
- A separate writer flushes every 60 seconds, or sooner at 512 pending records. Each batch holds at most 512 records and a bounded set of filenames, independent of total disk backlog.
- Every admitted ES trade history row is retained. Statistics corrections do not append trade history. Latest database snapshots are coalesced per contract/date. VX retries persist calculated totals, avoiding repeated additive volume.
- Sequence IDs are allocated in the background and saved to disk before submitting mutations. Unknown responses retry the same IDs; retained files survive restart. Successfully persisted files are removed even if their best-effort completion notification fails.
- Storage errors retry history only. On shutdown, a failed final flush leaves records on disk. No database retry or full history queue pauses live cache publication.
- Local disk capture remains awaited after live publication to preserve admitted history. Disk slowness can affect worker throughput. Disk failure is explicitly logged as a critical history gap while realtime cache processing continues. Disk space and process lifetime are real limits; this is not an unconditional availability guarantee.

## Historical projection repair

Uncertain writes deliberately retain historical projection guards/journals. Existing repair still requires a drained-writer cutoff before `BackfillQueryProjectionsV2Async`; those safeguards remain intact. A successful retry does not falsely reopen historical fast paths. Guard degradation affects historical storage/fallback performance, not the authoritative live cache. Repair does not clear or invalidate live snapshots.

## Financial persistence

`MarketDecisionEvidence` stores versioned immutable input bytes, encoding, capture time, source type and SHA-256. Automated composition captures the exact composition request, including its market snapshot, quote inputs and decision bindings. Manual order routes capture selected legs/quotes, quantities, order price, risk values and execution choices from the screen; they do not reread changing prices later.

Evidence follows the Portfolio candidate into approved order instructions, Trade Order definitions and their existing financial events/execution boundary. Provided evidence is integrity checked. Numeric MessagePack keys are appended; legacy nullable evidence remains readable, and null evidence is omitted from JSON to preserve legacy financial request hashing. The candidate economics hash remains independent of input ordering/audit bytes; the full financial request hash covers the captured evidence.

## Telemetry

Meter `TomasAI.IFM.MarketData.Eod` is registered with OpenTelemetry. Instruments report cache hits/misses, owned spool pending count, history capture/persistence/notification failures, batch duration and oldest batch age. Dimensions use stage only, without contract/trade identity cardinality. Batch/failure logs include method names and useful execution arguments; no successful per-tick informational logging is added.

## Verification ? 2026-10-05

- Complete solution build: zero warnings/errors.
- Feed: 569 passed. Regression suite covers live progress with blocked database writes, retained history/stable retry IDs, notification failure isolation, cold VX volume and no database reads, and concurrent live updates during failed fallback.
- Trade: 1,355 passed; Portfolio: 410 passed; UI: 375 passed. Financial suites verify exact input round trips, tamper detection, Portfolio/Trade mapping, transport keys and unchanged candidate economics hashing.
- Real isolated CQL tests: three passed. A repeated batch preserves every history row and latest snapshot; both uncertain-response tests retain guards until explicit repair cutoff.
- Detailed results: `.artifacts/eod-live-first-build.log`, `.artifacts/eod-live-first-feed-tests.log`, `.artifacts/eod-live-first-trade-tests.log`, `.artifacts/eod-live-first-portfolio-tests.log`, `.artifacts/eod-live-first-ui-tests.log`, `.artifacts/eod-live-first-storage-tests.log`.
