# AWS Cloud Backup and Restore Gate 2 Validation Report

**Gate:** 2 - Shared policy extraction and compatibility lock

**Result:** Complete

**Date:** 2026-08-21

**AWS mutation performed:** None

## Implemented result

Destination-neutral chain planning, manifest validation, and canonical JSON now live in
`TomasAI.IFM.Application.DatabaseBackup.Policies`. The local-workstation adapter delegates to these policies, and the
AWS project consumes the same types. The canonical reader rejects duplicate and unknown properties and normalizes
legacy schema-v1 full manifests while requiring explicit lineage in schema v2.

## Compatibility evidence

| Check | Result |
| --- | --- |
| Actor-contract MessagePack shape count | 120 concrete contracts |
| Golden shape SHA-256 | `a91e64c69448802ae2e453c597798f30587cb252ddaac33acb7fa84fa9001d87` |
| Manifest schema v1 read/round-trip | Passed |
| Manifest schema v2 canonical round-trip | Passed |
| Duplicate/unknown JSON properties | Rejected |
| Self dependency, duplicate artifact, non-UTC time | Rejected |
| Existing local chain-planning and publication tests | Passed unchanged |

No MessagePack key, actor field meaning, manifest field name, projection mapping, or persisted schema was changed.

**Rollback:** The local adapter shim can be redirected to its former internal implementation without a data migration;
no external schema changed.
