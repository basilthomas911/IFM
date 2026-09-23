# UI performance pass — 23 September 2026

This tracks the 50 static-review findings in their original priority order. “Changed” means code was updated; it does not claim a measured end-to-end improvement. “Retained” means the behavior is bounded, intentional, or unproven as a bottleneck. “Open” needs a separate API design. No live UI latency or GC trace was captured.

## Before and after

The Release `dotnet test` 80-strike projection microbenchmark uses 160 call/put contracts and 10,000 iterations. It compares the previous LINQ grouping algorithm with the indexed algorithm and asserts equal output.

| Algorithm | Time per chain | Allocated per chain |
| --- | ---: | ---: |
| Before: group/sort/scan | 0.0406 ms | 37,798 bytes |
| After: index/sort | 0.0277 ms | 19,376 bytes |

This isolates projection only; it does not include NATS, ScyllaDB, WinForms painting, or Databento. The initial pre-edit baseline run was 0.0393 ms and 37,386 bytes per chain.

The UI Presentation suite started at 359 passed / 3 failed (362 total). After the workflow-batch follow-up it is 363 passed / 2 failed (365 total); the two remaining architecture checks also failed before edits. The Market Data domain suite passes 229/229; the Trade domain suite passes 1132/1132. The UI Views and API Server projects build with zero warnings and errors.

## Findings disposition

| # | Disposition | Result / reason |
| ---: | --- | --- |
| 1 | Partial | Stable cached definitions and daily deviation are reused for 30 seconds; a current-price query and snapshot request still occur each second. |
| 2 | Changed | Reconcile option rows rather than clear/rebuild the displayed list. |
| 3 | Changed | Invalidate changed rows, or one grid repaint when more than eight rows change. |
| 4 | Changed | Index contracts by strike before sorting strike keys. |
| 5 | Changed | Collect call and put in one pass per contract. |
| 6 | Changed | Set grid row count only when its shape changes. |
| 7 | Changed | Cache provider roots by expiry. |
| 8 | Changed | Cache selected-contract IDs until selection changes. |
| 9 | Changed | Find nearest IV strike with a linear pass. |
| 10 | Changed | Average IV without a temporary array. |
| 11 | Changed | Capture latest quote time during contract indexing. |
| 12 | Partial | Most live header values use change-only text assignment; expected-move text remains assigned on refresh. |
| 13 | Changed | Preformat live delta, OI, and volume once per response. |
| 14 | Retained | Contract-key lookup and at-most-four selected-leg membership checks are cheap; no paint trace establishes a bottleneck. |
| 15 | Changed | Construct preview contract keys once per preview row. |
| 16 | Changed | Remove duplicate row invalidation after selection status refresh. |
| 17 | Changed | Cancel superseded cached-chain and evaluated-chain requests. |
| 18 | Partial | Server reuses definitions for 30 seconds; the UI still reloads when the expiry changes so newly cached contracts can appear. |
| 19 | Retained | WinForms timer intentionally coordinates painting with the UI thread; a background timer could build a backlog when painting is slow. |
| 20 | Changed | Catch expiry-load and live-refresh failures at the async event boundary. |
| 21 | Partial | Trade editor now awaits hosted blotter close; the legacy `IFormControl.Close()` void entry still cannot itself be awaited. |
| 22 | Retained | Evidence refresh occurs once on open, not per quote or tick. |
| 23 | Changed | Reuse fund-order ListView items when IDs are unchanged. |
| 24 | Changed | Index canonical orders once for the render. |
| 25 | Changed | Rebuild long order accessibility text only for a new order snapshot. |
| 26 | Changed | Scan items directly to restore the selected order. |
| 27 | Changed | Write ListView selection state only when it differs. |
| 28 | Changed | Reuse trade ListView items when IDs are unchanged. |
| 29 | Changed | Rebuild trade accessibility text only for a new trade snapshot. |
| 30 | Changed | Scan items directly to restore the selected trade. |
| 31 | Changed | Find selected trade index without copying the collection. |
| 32 | Retained | Refreshing fill evidence on selection is required for current trade state; no latency measurement justifies a stale cache. |
| 33 | Retained | Different trades require different workflow controls; the existing displayed-trade ID guard already prevents same-trade recreation. |
| 34 | Changed | Hosted controls are closed, serialized, and disposed before replacement. |
| 35 | Changed | Resolve base contract by ID/symbol in one pass. |
| 36 | Changed | Coalesce editor property notifications into one posted render. |
| 37 | Changed | Recompute buttons only for relevant property groups. |
| 38 | Retained | Reassigning unchanged WinForms `Enabled`/`Visible` values is not established as a significant cost. |
| 39 | Changed | The drawing helper no longer calls synchronous full-control `Refresh()`. |
| 40 | Changed | Redraw is restored in `finally`; trade-editor blotter replacement no longer uses that deferred helper. |
| 41 | Changed | Workflow hydration uses at most eight workers rather than one waiting task per item. |
| 42 | Changed | Workflow history now requests up to 100 detail snapshots per NATS batch, preserving revision checks and page order. A two-row test verifies one batch call and zero per-ID calls. The server still performs up to eight concurrent Scylla point reads per batch; no live latency benchmark was captured. |
| 43 | Changed | Terminal-cache eviction uses a single-pass oldest lookup rather than sorting and allocating under lock. |
| 44 | Changed | Small live signal batches are inserted in order; bulk history still sorts once. |
| 45 | Changed | Operations chart appends events when its previous sequence is unchanged. |
| 46 | Changed | Reuse one marker font for chart event points. |
| 47 | Changed | Measure a maximum fixed-format time string instead of every workflow row. |
| 48 | Changed | Use cached system brushes and pens while owner-drawing workflow actors. |
| 49 | Retained | The metrics snapshot copies at most the few selected option-leg entries; no measurement supports changing snapshot semantics. |
| 50 | Changed | Cache health-row projections and skip grid rebinding when row values are equal. |

The remaining unmeasured work is an end-to-end live UI latency/GC benchmark. The workflow batch removes UI-to-server N+1 requests, but it does not replace the server's per-ID Scylla reads with a single storage query.
