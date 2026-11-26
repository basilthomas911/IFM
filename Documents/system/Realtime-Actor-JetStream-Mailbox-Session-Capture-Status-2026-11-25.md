# Full-session capture operational preflight

The follow-up attempted the actual prepared capture preflight. Capture and Memory/File replay have not started; activation remains disabled. The normal-session windows are still Nov30 and Dec1,2026, with the original combined observation window Nov29,17:50 ET through Dec1,18:10 ET. No future session data is labelled complete.

Current evidence:

- API22543 and Rust MarketData22544 are unavailable; the development-process inventory returns no running owned profile. No current native in-memory book/mapper checkpoint was acquired.
- The latest stored account definition is revision466 with a nine-field legacy snapshot: it has no stored SourceSequence field, so it resolves to0. The default candidate emulator head is still CommitRevision0, Generation33, journal sequence224. Its SHA is unchanged from the earlier audit. It is a candidate file; an effective running-owner path is not proved. These values cannot establish the required positive source/commit join or a matching financial snapshot.
- The prepared snapshot topology still has empty store/file-root lists and no configured cooperative boundary endpoint. A source-only native hold cannot certify the financial/database owners.
- Measured physical PostgreSQL database usage is about103GB and the complete Scylla data directory about31GB. E: has about377GB free and C: about291GB free at inspection. These are not compressed dump measurements or a qualified combined budget. The configured single-snapshot500GiB maximum exceeds E:free space; replay restores, source/raw journals, working archives, final owner evidence and sorting spill also need reservations.
- The event-log row estimate alone is13.7million, above the default owner-export limit of10million. No smaller export was substituted, and no limit or qualification flag was relaxed to obtain a pass.
- No protected capture token is configured in this process. No token was placed in repository files.

Next required sequence:

1. Establish a capture-only runtime and its effective owner paths while keeping the candidate realtime mailbox backend disabled.
2. Obtain a genuine versioned emulator/PostgreSQL source binding without dummy trades or invented source revisions; verify the unchanged financial facts.
3. Bind the actual stores/file roots and acquire an authenticated stopped/drained cross-owner boundary, exact initial archive floor and original native checkpoint.
4. Measure/reserve the full storage and export capacity, then arm the original source archive for the two normal sessions.
5. After both complete sessions, restore once per Memory/File chain, preserve first-session state into the second, replay at original pacing and reconcile actual owner outcomes/resources. Activation remains disabled until all gates pass.

No process was restarted, no trading data or balance was modified, no provider data was purchased, no witness was launched and no background capture was armed in this follow-up. A successful component test or an existing stored tick history was not substituted for an authentic full-session capture.

Machine-readable evidence:

- `.artifacts/realtime-mailboxes/20261125-simple-qualification/capture-followup-preflight.json`
- `.artifacts/realtime-mailboxes/20261125-live-session-followup/capture-readiness.json`


## Development profile restarted and tested

The exact API + Rust eGui + Rust MarketData development profile has now been rebuilt and launched. API `/health/ready`, API `/health/launch-ready` and Rust MarketData `/health/ready` all returned200. The eGui window is open/responding;217/217 actors are running and raw storage is receiving live records. The earlier unavailable-runtime finding is superseded by this observation.

Startup initially blocked in the ITI resident-input recovery query. Its correlated epoch-minimum subquery was evaluated for every pending row. The replacement computes each stream/epoch minimum once in a materialized relation and keeps the original checkpoint filter/order. A disposable PostgreSQL comparison verified6,999 ordered rows and exact payloads across same/new epochs, uncheckpointed streams and an orphan checkpoint. Read-only execution on the actual1.94million-row journal returned583,378 pending rows in6.44seconds; the original recovery query was still executing after several minutes. The profile restarted successfully with the change. No inputs or checkpoint rows were discarded.

The application and feed are left running. Candidate realtime-mailbox activation remainsfalse. Full-session capture/MemoryFile qualification is not represented by these startup checks; the latest operational preflight still requires matching owner snapshot/source binding, protected capture configuration and capacity qualification.

Evidence: `.artifacts/realtime-mailboxes/20261125-development-restart/final-runtime-status.json`, `iti-recovery-query-correctness.json`, `iti-recovery-optimized-plan.txt` and `capture-preflight-after-start.json` in the same directory.
