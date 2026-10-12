# Development AWS backup/restore acceptance, 2026-10-10

Qualification completed on 2026-10-11. `qualification-status-20261010.json` records `fullyQualified: true` and an empty remaining-work list.

The user approved Development backup and restore admission independently of Gate 14 deletion qualification. This supersedes the admission restriction in DEVELOPMENT-READINESS-20261004.md; its evidence remains historical.

## Accepted functionality

- Current full PostgreSQL and ScyllaDB backups published and verified in both AWS vaults.
- Native isolated restores verified, followed by actual replacement of the existing Development containers' database data.
- Database repair, application-provider validation, derived-cache invalidation and normal application admission verified. Twelve contexts and 242 tables passed; final API readiness was HTTP 200.
- Automatic retention completed for both engines in both vaults. Application operation queries show Completed at 100%; protected backups were retained.
- Actual deletion verified separately for the exact 16 owner-approved disposable versions. Temporary owner policies were removed; the broader legal-hold proposal remains withdrawn.
- Credential refresh verified through repeated scheduled renewals during the live qualification.
- Original volumes preserved for rollback; the next isolated Scylla restore target is healthy, empty and has a distinct node identity.

## Development operation

Use System Admin Backups to submit AWS backup/restore requests and inspect progress in Logs. Restore staging uses the configured isolated PostgreSQL or Scylla target; the recovery workflow then activates validated data in the existing database containers and restores application health. Explicit cutover approval remains part of that workflow.

The Development deployment combines `Docker/DatabaseBackup/docker-compose.development.yml` and `docker-compose.aws-development.yml`. AWS backup admission and live AWS checks are enabled. General destructive tests remain disabled. Normal automatic retention uses a 24-hour interval and 35-day age boundary, with existing protection checks.

Scope remains Development. Staging and Production admission are unchanged. The seven preexisting Local Workstation jobs with missing media configuration are a separate issue.

## Evidence

See `qualification-status-20261010.json`, `automatic-retention-host-20261010.json`, `.artifacts/aws-qualification-20261010`, and `.artifacts/database-recovery`. Current backup, activation and retention operation identifiers and the final deployed host image are listed in the Development README's final qualification section. Historical failures and reconciliation receipts are preserved.
