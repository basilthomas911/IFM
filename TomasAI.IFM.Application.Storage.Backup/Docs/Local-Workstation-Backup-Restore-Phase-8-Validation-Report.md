# Local Workstation Database Backup and Restore Phase 8 Validation Report

**Gate:** 8 - Vault, offline media, manifest, catalog, retention, and drills

**Status:** Passed

**Date:** 2026-08-12

## Scope completed

- Added destination-neutral application contracts for publication, restore-source preparation, online vaults,
  offline media, restore workspaces, signed manifests, catalog access, checksums, signatures, capacity, path policy,
  retention, drill evidence, and break-glass recovery records.
- Implemented an allowlisted path policy that accepts only normalized relative identities under configured roots,
  rejects traversal and alternate-stream syntax, detects symbolic links, junctions, and reparse points before
  descending into a tree, and prevents signing-key placement inside a vault or restore workspace.
- Implemented bounded SHA-256 artifact inventory and verification. Artifact, catalog-entry, and manifest-size limits
  fail closed before unbounded publication or catalog processing.
- Implemented explicit online-vault and offline-media enrollment. The signed immutable enrollment record binds the
  environment, replica, MediaId, rotation slot, signing key, and SHA-256 digest of a public-key trust bundle copied
  to the medium. A wrong configured or attached media identity fails before publication or restore.
- Implemented ECDSA P-256/SHA-256 detached signatures using separate PEM private/public key references. The private
  key is never copied to backup media; every enrolled replica receives the independently verifiable public trust
  bundle.
- Implemented no-overwrite artifact publication through unique incoming and final identities, `CreateNew` file
  creation, write-through streams, `Flush(true)`, read-back SHA-256 verification, same-volume directory promotion,
  signed manifest publication, signed commit-last evidence, signed append-only catalog entries, and post-publication
  resolution.
- Made multi-replica publication restart-safe. A retry verifies any already catalog-visible replica and publishes
  only missing replicas from the identical signed manifest; conflicting immutable identities fail closed.
- Added signed offline media seals with MediaId, rotation slot/revision, restore point, manifest, bytes, file count,
  dependency completeness, and verification result. Offline catalog eligibility requires a valid seal.
- Made catalog visibility depend on enrollment, detached manifest and commit signatures, commit-to-manifest digest,
  catalog-to-manifest identity, exact artifact length/digest, and an acyclic dependency graph wholly present on the
  selected replica. Restore never silently mixes replicas.
- Implemented explicit restore-source preference with verified online-first/offline fallback, capacity admission,
  immutable isolated staging, durable copy read-back, and materialization into the engine-owned native restore-point
  root without modifying the source vault.
- Integrated publication behind native verification and before terminal backup completion. The host now emits the
  common `DatabaseBackupArtifactReplicaUpdatedEvent`; restore and restore-drill execution resolve signed catalog
  evidence before invoking PostgreSQL or Scylla, and a successful drill writes immutable signed RPO/RTO evidence
  before `DatabaseRestoreDrillCompletedEvent`.
- Implemented retention as two independent operations. Evaluation writes an immutable signed, revision-bound plan
  containing exact relative file identities; dependency closure, caller-protected/held/active points, and the newest
  rollback reserve are excluded. Execution requires the exact plan revision and approval reference, revalidates the
  current dependency graph, and deletes only the plan's exact files—never a wildcard tree.
- Implemented immutable signed break-glass recovery records on the selected replica and Core-independent signature,
  catalog, dependency, and artifact validation during reconciliation.
- Added disabled/dry-run-safe host configuration for `OnlineVault`, `OfflineMedia`, `RestoreWorkspace`, `Manifest`,
  and `Limits`, plus real DI composition for all Phase 8 adapters.

The durable file boundary uses APIs whose documented semantics match the implementation: `.NET` `FileMode.CreateNew`
throws when an identity already exists, and `FileStream.Flush(true)` requests that intermediate file buffers be
flushed to disk. See the official [.NET `FileMode` documentation](https://learn.microsoft.com/dotnet/api/system.io.filemode?view=net-10.0)
and [`FileStream.Flush(Boolean)` documentation](https://learn.microsoft.com/dotnet/api/system.io.filestream.flush?view=net-10.0).
Link and junction rejection uses the documented `FileSystemInfo.LinkTarget` and reparse-point attributes; see
[`FileSystemInfo`](https://learn.microsoft.com/dotnet/api/system.io.filesysteminfo?view=net-10.0).

## Validation evidence

### Gate 8 fault and recovery integration

```text
dotnet test TomasAI.IFM.Framework.Storage.IntegrationTests/
  TomasAI.IFM.Framework.Storage.IntegratedTests.csproj --no-restore \
  --filter "Category=Gate8Integration"

Passed: 7
Failed: 0
Skipped: 0
```

The suite proves:

1. a published artifact identity cannot be overwritten;
2. the wrong offline MediaId fails closed;
3. signed-manifest and artifact-content tampering are rejected;
4. insufficient destination capacity fails before copy;
5. retention preserves the dependency of a retained point and deletes only an independent exact plan;
6. a corrupt online replica falls back to one verified offline replica, reconstructs a lost native restore point
   through an isolated workspace, and produces immutable drill evidence; and
7. one signed offline break-glass record reconciles without Core or NATS.

### Host, storage, and native regression

```text
Storage integration tests (non-native): 24 passed, 0 failed, 8 skipped
Host/NATS restart end-to-end tests:        2 passed, 0 failed, 0 skipped
Native PostgreSQL restore tests:           1 passed, 0 failed, 0 skipped
Native Scylla restore tests:               1 passed, 0 failed, 0 skipped
```

The two native tests used uniquely named disposable Docker databases and completed successfully. The persistent
workstation database containers were not reused or modified by the tests. Phase 8 changes do not alter either native
format; they add the verified durable publication and retrieval boundary around those formats.

### SystemAdmin regression

```text
SystemAdmin unit tests:        93 passed, 0 failed, 0 skipped
SystemAdmin BDD tests:          3 passed, 0 failed, 0 skipped
SystemAdmin integration tests:  4 passed, 0 failed, 0 skipped
```

### Dependency advisory audit

```text
dotnet list TomasAI.IFM.sln package --vulnerable --include-transitive

No vulnerable packages found for any solution project.
```

### Full solution

```text
dotnet build TomasAI.IFM.sln --no-restore

Build succeeded.
0 Warning(s)
0 Error(s)
```

## Gate result

Gate 8 passed because a restore point becomes visible only through a valid signed enrollment, manifest, commit,
catalog entry, optional offline seal, complete dependency graph, and exact artifact digests; storage identities cannot
be overwritten by the service; the wrong medium, tampering, and capacity shortage fail closed; restore reads a single
verified replica through a separate workspace; retention is signed, exact, revision-bound, dependency-safe, and
separate from execution; drills and break-glass recovery retain immutable independently verifiable evidence; host,
native database, SystemAdmin, solution-build, and package-advisory regressions all pass.

This gate establishes the software publication and recovery semantics. It does not claim that a mounted workstation
vault is administrator-proof. BitLocker/LUKS state, physical-volume identity, qualified filesystem durability, ACL
deployment, safe device removal, and cross-workstation production media qualification remain environment controls
for the Ubuntu/runtime qualification and production-readiness work in Phase 10.

Phase 9 (Console and WinForms migration) is the next pending implementation phase.
