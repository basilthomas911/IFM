# IFM Scheduler Host SM-S5 Unattended Operations Runbook

**Status:** Deployment tooling complete; target-machine and operator acceptance pending

**Date:** 2026-08-20

## Tooling

The Scheduler Host `Operations` directory contains installation/uninstallation, PostgreSQL backup/restore, and
acceptance-check scripts. Installation creates a delayed-auto service, recovery actions, restricted service SID,
local operator group, and restrictive deployment/output ACLs. Uninstall preserves PostgreSQL and run evidence.

Production named-pipe creation resolves configured operator groups to SIDs and grants only those groups and the
service identity. Development console mode remains current-user-only. The operational monitor probes PostgreSQL and
free disk; faults put Quartz in standby, and recovery reconciles definitions before scheduling resumes.

## Deployment sequence

1. Publish Scheduler Host and all task executables to their catalog paths.
2. Create the dedicated PostgreSQL database/login and least-privilege grants.
3. Supply credentials outside checked-in JSON.
4. Review operator groups, endpoints, hashes, environment identity, and disabled templates.
5. Back up scheduler state.
6. Run `Install-SchedulerHost.ps1` elevated without `-StartService`.
7. Run `Test-SchedulerHostAcceptance.ps1` and inspect service/filesystem ACLs.
8. Start the service and confirm `Ready` while every real schedule is disabled.
9. Exercise restart, reboot, sign-out, DST, misfire, disk pressure, API/NATS/PostgreSQL outage and recovery,
   cancellation, and backup/restore in Development/paper trading.
10. Run the agreed soak window and record resource peaks, failures, and truncation.
11. Obtain named approval for each task before enabling it.

## Recovery rules

- Disable affected definitions before maintenance when possible.
- Stop the service; bounded shutdown cancels owned processes and Job Objects prevent orphan trees.
- Never infer business rollback from a killed process; investigate `ForceTerminated` and `Abandoned` runs.
- Restore only while stopped, then rerun migrations/acceptance and compare schedule/audit counts.
- Keep schedules disabled until recovery evidence and dependency health are accepted.

Repository automation cannot claim machine reboot/sign-out, real outages, restore drill, soak time, or human approval.
Record those results in the deployment change record; SM-S5 completes only after they pass on the target machine.
