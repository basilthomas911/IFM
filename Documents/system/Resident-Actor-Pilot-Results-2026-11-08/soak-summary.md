# Burst/idle soak

Completed 1800.148 seconds (requested 1,800), 28,160 successful commands and exact source event-count checks for all 80 streams. Four bursts per cycle, 16 keys per actor, five actors, up to 16 concurrent independent streams, then 20 seconds idle. Capacity was 32 keys per actor. All actors ended at 16 entries, zero pinned entries and zero in-flight work; each recorded 5,616 hits and 16 initial misses. Failure or source-count mismatch would abort completion.

Managed live heap after forced full collection at each sample ranged 11.27–13.13 MiB, ending 13.13 MiB. Process working-set peak among samples was 205.27 MiB. The fixture retains 88 measurement records and PostgreSQL histories grow. These observations qualify this fixed-key workload, not a hard bound on state bytes or a proof of no long-term leak. Cache capacity/idle eviction and pinned fallback have separate unit/integration coverage.

The probe intentionally forces GC for retained-memory samples; it does not measure natural GC pauses during this soak. CPU, allocations and natural GC deltas are in the separate resource probe. Full raw samples remain at `E:\IFM\BenchmarkArtifacts\ResidentActors\20261108\resources.json.soak.json`.
