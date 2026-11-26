# Production-handler historical replay

This is an explicit offline Test mode. It uses the real API, NATS dispatch, PostgreSQL/Scylla stores and file-emulator owners. It does not reuse EventLogQualification or substitute a synthetic financial owner. Ordinary startup and live activation remain unchanged and disabled for the new mailbox backend.

## Original native boundary

Enable `IFM_SESSION_CAPTURE_BOUNDARY_ENABLED=1` before loading the native library in the capture process, with required source capture configured. The existing authenticated native forwarding hold must already be held. The opt-in Rust host requires `SessionCapture:Enabled`, protected `SessionCapture:AccessToken`, and an existing absolute `SessionCapture:CheckpointDirectory`.

`POST /internal/session-capture/native/checkpoint/export/{hexLease}/{12hexBoundary}` takes the common native mutation fence, freezes the real original registrations, private books, mapper, publication buffers, pricing/evidence state, source and raw sequence floors, and writes:

- `native.checkpoint`: versioned, checksummed, bounded binary state;
- `backend-snapshot.bin`: the original binary managed read snapshot;
- `source-prefix.json`: exact immutable source and raw prefix lengths under the same native fence;
- `checkpoint.json`: SHA-256 and lengths binding those originals.

The common mutation fence is released afterward; the caller's original forwarding hold stays held. No route retires a provider or claims a cross-owner financial snapshot. The certificate explicitly remains `Qualified:false`, `CrossOwnerSnapshot:false`.

Copy the fixed prefixes with `session_tool.py copy-prefix` while that forwarding lease remains held. The reviewed plan must name both `nativeSourceRoot` and `nativeRawRoot`. The copy tool hashes every complete fixed prefix, copies raw `.wal`/`.owner`/sequence files too, rejects a different raw root or inconsistent shared floors, and binds the result to the exact `source-prefix.json` hash. Keep the checkpoint directory as an owner-file root in the separately fenced API/PG/Scylla snapshot. A scalar market snapshot alone is insufficient.

Native import is idle-only and validates the original graph. Imported factories expose the original public book/catalog before starting a source. Explicit import-only mode never starts a provider or claims healthy live-source proofs. Copied raw/source directories must be inside a separately configured owned restore root and cannot overlap the original directories.

## Isolated API host

`dotnet <Api.Server.dll> --historical-session-replay=<absolute-host-config.json>` requires `DOTNET_ENVIRONMENT=Test` and `ASPNETCORE_ENVIRONMENT=Test`. Supply `IFM_REPLAY_CONTROL_TOKEN` through the process environment; do not put tokens in JSON. Test database credentials use the existing protected Test credential variables, not inline base connection strings.

The host configuration is version1 and contains `runId`, `restoreCertificatePath`, `restoreCertificateSha256`, `natsUrl`, `httpUrl`, `scope`, `initialUtc`, `nativeCheckpointCertificatePath`, `nativeCheckpointCertificateSha256`, and a flat string-valued `configuration` dictionary. That dictionary explicitly maps the canonical database connections and every `HistoricalSessionReplay.OwnerPathKeys` filesystem owner under the restored root. It must include a real restored emulator ledger listed in the owner certificate. The existing finite integration fixture demonstrates all keys.

`session_restore.py` certifies the actual owned containers, image IDs, stores, owner-file bytes and published loopback endpoint mappings. An optional `targets.nats` object binds the owned replay broker. The API rejects other ports/databases/keyspaces, changed owner files, provider credentials and live filesystem paths. Restored original scope is allowed only when bound to its native certificate on the separate broker. Without a native certificate, a finite component fixture must use `replay-<runId>`.

The shared business clock holds the original boundary time during bootstrap, starts on the first authenticated recorded admission and then advances continuously without rewinding. Startup duration cannot make the first original signal falsely stale. Monotonic deadlines, cancellation and elapsed durations use the real clock. Authenticated loopback endpoints expose actual readiness/drain state, advance contiguous original ordinals, and request graceful stop. This mode does not relax `/health/ready` or fabricate missing inputs.

## Original-input runner

The benchmark executable has an explicit `--replay-original-session <absolute-run-config.json>` command, separate from BenchmarkDotNet. Its version1 configuration pins the SHA-256 of the host config, API assembly, native library, native certificate, `ArchiveCopyManifest` and `NativePrefixCopyManifest`. It also specifies a new owned `WorkingArchiveDirectory`, `ArchiveSessionId`, new `OutputDirectory`, bounded record/time budgets, `InitialArchiveBoundary` plus its SHA, and explicit `AccountObservationMode`. `RootRealtimeVerbs` must contain exactly `Realtime.FuturesTradeSessionBarSignal.Barrier`, `Realtime.FuturesTradePositionRealtime.EvaluateFuturesExitClock`, and `Realtime.IronCondorTradePositionRealtime.IronCondorMonitoringInputsChanged`; these producers are suppressed only in historical mode while their actual handlers remain active. Zero observed events is permitted.

The source archive now adds archive-v4 recorded admission time outside original business identities; normal realtime wire-v2 and original hashes are unchanged. Legacy archive-v3 remains readable for recovery but is refused for paced session qualification because it lacks original admission times. Do not invent timestamps for old tapes.

The runner validates every copied source/raw prefix against its full hash and common checkpoint metadata, stages the real native image, starts the actual API child, replays original physical native singles/batches, option-cache frames, raw-storage batches, exact external backend replies and explicitly selected external realtime roots over isolated NATS at recorded pacing. It verifies actual native ownership receipts and raw/durable consumer drain. Derived workflow/order/fill output must be regenerated by real owners, rather than injected a second time. `RegenerateFromFileEmulator` uses the restored emulator; `RecordedExternalInput` is only for an explicitly certified external account authority. Non-state external replies are consumed once per original exact-payload call in admission order, with bounded concurrent waiters and atomic reply/origin selection. They become available only at their recorded completion barrier. EOF, missing/extra calls, physical deadline failures or unused required replies make the run incomplete; an unlimited latest-reply cache cannot conceal divergence. Baseline snapshot/status reads are explicitly counted as derived reads of the original external state. Original transport failures are captured and explicitly refuse qualification rather than becoming synthetic successful replies.

The native graph is independently validated/restorable; managed RPC playback supplies the original external read bytes to the separate real API host. It does not run provider callbacks a second time or claim a new provider acquisition. Full session outcomes must still be exported and reconciled with `outcome_evidence.py` and `session_tool.py reconcile`.

A successful runner reports `ExecutedAwaitingOwnerReconciliation`, `Qualified:false`. Only exact complete original owner/session reconciliation can pass the Memory/File qualification gates. Two complete normal-session captures, matching financial/configuration boundaries and their carried-state comparisons remain required. Prepared future capture jobs are not automatically armed. No live profile is restarted, no orders are sent and no provider data is purchased by these offline checks.


## Exact initial archive floor and monitoring permission

The initial owner snapshot must include `initial-archive-boundary.json` as an actual hash-certified restored owner file. The snapshot tool requires a saved archive prefix bound by SHA and session ID in the held `source-outbox` owner proof. It copies the exact fixed prefixes and derives their committed ordinal from real receipts and complete frame offsets. The runner verifies those original prefixes against its full copied archive, then injects only ordinals after that floor. Events already present in the initial owner state are never replayed merely because their timestamps look suitable.

Original iron-condor monitoring inputs also carry the original actor generation. In capture mode, the actual handler writes `MonitoringGenerationAccepted` after its normal generation/position/identity checks pass, before calculation. This is infrastructure permission evidence, not a trade approval or successful calculation. Stale original notifications have no receipt and gain no override in replay. The replay permission is bound to the exact event, actor/thread, original generation, canonical payload hash, position sequence and route generation. It can hydrate a missing matching position cache; it cannot overwrite a newer position or revive a retired context.

Prepare a candidate authority file from a separate exported analysis copy laid out as `<analysisRoot>/archive_<first24-lowercase-SHA256(sessionId)>/`. The existing `copy-archive-prefix` command can write directly to that epoch directory. Keep the replay runner's actual working archive directory new and separate. Then use the explicit benchmark executable command:

```text
--export-recorded-monitoring-authority <analysisRoot> <sessionId> <archive-copy-manifest-sha256> <restored-initial-boundary.json> <boundary-sha256> <new-owned-authority-file>
```

The output has a bounded JSON header and JSON-string/base64 MessagePack rows. No prediction or financial result is supplied. Its original source-copy and initial-boundary hashes must match `recordedMonitoringAuthority:{path,sha256,sourceArchiveCopyManifestSha256,initialArchiveBoundarySha256}` in the host configuration. The file must be under the restored root. The real runner independently joins every permission with its original compressed source input, recomputes the existing canonical payload hash and rejects orphan, conflicting or modified evidence. Exporting this metadata alone does not certify a session.

Actual verification is recorded in the [historical replay result summary](../../Documents/system/Realtime-Actor-JetStream-Mailbox-Historical-Replay-Results-2026-11-25.md). Activation and future capture arming remain disabled.
