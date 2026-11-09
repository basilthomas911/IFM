# Retired Redis Development Container

Redis and Blackboard were removed from the supported application on November 8, 2026. The old composition is retired. Development starts using typed process-local caches and authoritative PostgreSQL data.

The IFM-owned `redis` container is stopped with automatic restart disabled. Its original anonymous volume is preserved. A verified RDB copy and container configuration are retained at `E:\IFM\RedisRemoval\20261108\baseline`. Do not remove that recovery data without a separate retention decision.

The isolated historical comparison benchmark can start a disposable Redis container. Normal application and integration startup do not require this service.
