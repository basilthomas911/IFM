-- Additive production-equivalent migration for existing large event logs.
-- Run against the event-source database outside a transaction. Concurrent
-- construction keeps normal reads and writes available; no event rows change.
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_event_log_stream_name_version
ON public.event_log (EventStreamId, EventNameId, EventVersion DESC);

-- The migration is complete only when both flags are true.
SELECT indexrelid::regclass AS index_name, indisready, indisvalid
FROM pg_index
WHERE indexrelid = 'public.ix_event_log_stream_name_version'::regclass;
