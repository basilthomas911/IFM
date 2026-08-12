# Local Workstation Database Backup and Restore Phase 7 Validation Report

**Gate:** 7 - Scylla LocalWorkstation capability

**Status:** Passed

**Date:** 2026-08-12

## Scope completed

- Added the real `ScyllaBackupCapability` behind the existing destination-neutral application port and added
  protection-set engine selection so PostgreSQL and Scylla intents route to the correct typed capability.
- Added an allowlisted Scylla Manager CLI adapter. It resolves only `sctool` from a fixed tool directory, uses
  `ProcessStartInfo.ArgumentList`, disables shell execution, captures bounded output, removes inherited Manager
  settings, supplies only the configured API URL and optional certificate/key file references, and kills the process
  tree on cancellation or timeout.
- Added strict configuration validation for Manager URL, client certificate/key pairing, compatibility versions,
  protection sets, cluster names, backup locations, keyspace selections, fresh targets, timeouts, and canonical
  non-overlapping backup/restore roots. Actor messages cannot provide executable names, arguments, endpoints,
  locations, credentials, CQL, or filesystem paths.
- Implemented Manager-driven cluster capture with required live-node checks, CQL/REST health, schema agreement,
  token/host coverage, Scylla and Manager versions, schema digest, native manifest digest, keyspace/table/artifact
  counts, snapshot tag, task reference, elapsed time, and bounded run statistics.
- Implemented bounded polling of Manager task progress and fail-closed handling for error, abort, timeout, missing
  snapshot tags, empty manifests, incomplete schema evidence, and insufficient topology coverage.
- Added native verification before an in-progress capture is promoted to a restore-point directory. A restarted host
  recovers the captured evidence without repeating native work; a tampered native artifact fails verification and is
  not published.
- Implemented fresh-target restore in the required order: verify source evidence, restore schema through Manager,
  restore tables through Manager, validate fresh-cluster node coverage/schema agreement, record validation revision
  and run statistics, and stop production recovery at `ReadyForCutover`.
- Kept restore independent of the original live cluster. Restore-point verification checks immutable capture evidence
  and Manager backup metadata; only the separately allowlisted target cluster must be live.
- Registered Scylla startup capability validation alongside PostgreSQL validation. The host is not ready when the
  configured Manager client, version, cluster health, or required topology evidence is unavailable. Dry-run remains
  the default and uses deterministic fake capabilities.
- Added deterministic Manager command compatibility tests, capability restart/tamper/allowlist tests, journal engine
  routing, and a real disposable Scylla native restore test.

The production path follows ScyllaDB Manager's supported cluster-wide workflow: `sctool backup` creates a managed
backup and schema evidence, `sctool progress` supplies task completion, `sctool backup files` exposes the native
manifest, and schema and table restores run as separate Manager restore tasks. See the official
[Scylla Manager backup](https://manager.docs.scylladb.com/stable/sctool/backup.html),
[restore](https://manager.docs.scylladb.com/stable/sctool/restore.html), and
[progress](https://manager.docs.scylladb.com/stable/sctool/progress.html) documentation.

The disposable native qualification uses the node REST snapshot API to obtain actual SSTables and then follows the
documented upload-directory plus `nodetool refresh` procedure on a separately provisioned node. This test-only adapter
does not replace the Manager-driven production implementation. See the official
[Scylla snapshots](https://docs.scylladb.com/manual/stable/kb/snapshots.html) and
[`nodetool refresh`](https://docs.scylladb.com/manual/stable/operating-scylla/nodetool-commands/refresh.html)
documentation.

## Validation evidence

### Real Scylla native restore

```text
dotnet test TomasAI.IFM.Framework.Storage.IntegrationTests/
  TomasAI.IFM.Framework.Storage.IntegratedTests.csproj --no-build \
  --filter "Category=Gate7NativeIntegration"

Passed: 1
Failed: 0
Skipped: 0
Duration: 28 seconds
```

The opt-in test creates a uniquely named disposable source node, inserts a synthetic CQL row, invokes Scylla's REST
snapshot operation, copies the complete SSTable component set and snapshot-generated schema to host-owned evidence,
and recreates the capability to simulate host restart without a second capture. It verifies the snapshot and digest,
removes the source, provisions a separately named fresh node, applies schema, copies the SSTables into the fresh
table's upload directory, invokes real `nodetool refresh`, and queries the restored synthetic row successfully.

The workstation already runs a persistent Compose-managed Scylla node. The test does not stop, modify, or reuse it.
It temporarily raises only the Docker VM AIO ceiling so one disposable node can coexist, runs source and target
sequentially, removes both containers, and restores the original `fs.aio-max-nr` value. Post-test inspection found no
Gate 7 containers and confirmed the original value `65536`.

### Deterministic capability, Manager, and journal integration

```text
dotnet test TomasAI.IFM.Framework.Storage.IntegrationTests/
  TomasAI.IFM.Framework.Storage.IntegratedTests.csproj --no-restore \
  --filter "Category=Gate7Integration"

Passed: 5
Failed: 0
Skipped: 0
```

The suite verifies exact allowlisted Manager backup/progress/manifest/schema-restore/table-restore arguments; native
version and topology parsing; restart-safe capture reuse; verification-before-publication; tamper rejection;
idempotent restore replay; protection-set/fresh-target rejection; Scylla run statistics; and journal routing through
the Scylla capability.

### Regression suites

```text
SystemAdmin unit tests:                   93 passed, 0 failed, 0 skipped
SystemAdmin BDD tests:                     3 passed, 0 failed, 0 skipped
SystemAdmin integration tests:             4 passed, 0 failed, 0 skipped
Storage integration tests (non-native):   17 passed, 0 failed, 8 skipped
Native Scylla restore tests:                1 passed, 0 failed, 0 skipped
```

### Dependency advisory audit

```text
dotnet list TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation/
  TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.csproj \
  package --vulnerable --include-transitive

No vulnerable packages found.
```

### Full solution

```text
dotnet build TomasAI.IFM.sln --no-restore

Build succeeded.
0 Warning(s)
0 Error(s)
```

## Gate result

Gate 7 passed because production capture and restore are constrained to typed, allowlisted Scylla Manager operations;
capture records cluster, schema, snapshot, manifest, version, dependency, and run-statistics evidence; verification
precedes publication and survives host restart without repeated capture; restore does not require the source cluster
to remain live; a real Scylla snapshot restores its complete SSTables to a separately provisioned fresh node and
reproduces synthetic application data; journal orchestration routes by engine and stops production recovery at
`ReadyForCutover`; regression and advisory checks pass; disposable infrastructure and temporary host settings are
cleaned up; and the complete solution builds with zero warnings and errors.

Phase 8 (vault/offline-media publication, shared signed manifest, catalog, retention, source selection, and restore
drills) is the next pending implementation phase.
