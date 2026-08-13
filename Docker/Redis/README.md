# Redis Development Container

The current Redis 7.0.9 container uses an old anonymous Docker volume. This composition references that exact volume
through `IFM_REDIS_VOLUME` so adopting compose does not silently start with an empty cache. A later cleanup may issue
`SAVE`, copy `/data` into a named `ifm_redis_data` volume, validate it, and update this file. Redis is cache state and
is not part of the database backup recovery contract.
