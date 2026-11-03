# ScheduledTask command behavior scenarios

These are executable Given/When/Then scenarios for every command mapped by the ScheduledTask definition, Run and Catalog actors. They exercise the public command extension handlers with fixed UTC time, isolated states and serialized commands. They do not start a scheduler, process, broker or database.

| Feature | Commands | Behavior |
| --- | --- | --- |
| Definition | Create, ChangeSchedule, Enable, Disable, Remove, RecordInstallation, RecordInstallationFailure, AdmitRun, RecordRunCompletion | Desired/applied configuration, catalog evidence, revision conflicts, installation receipts, exclusive admission and release |
| Run | Request, RecordAdmission, RecordStarted, RecordStage, Complete, Fail, RecordUncertain | Stable occurrence identity, ordered receipts, confirmed outcomes, EOD sequencing, operator resolution and bounded diagnostics |
| Catalog | RegisterProject, RecordHostCapability | Artifact replacement, retained catalog evidence, host ownership, freshness and monotonic observations |

Every accepted scenario checks the acknowledgement identity, exact source event, operation identity, next revision, business outcome and reconstruction from serialized event history. Every rejection checks its classified reason, unchanged snapshot reference and serialized contents (including nested evidence), and absence of new events.

The coverage test compares scenario traits with each actor's actual supported-command manifest. Adding a mapped command without happy-path and edge-case scenarios fails the suite. These tests cover command behavior and event replay; transport, database commits, snapshot-query selection and actual host execution remain integration-test responsibilities.

Run with `dotnet test TomasAI.IFM.Domain.SystemAdmin.BDDTests --filter FullyQualifiedName~ScheduledTask`.
