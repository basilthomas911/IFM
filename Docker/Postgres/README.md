# PostgreSQL Development Container

This composition reproduces `ifm_db` with PostgreSQL 17.2. The existing cluster is stored in the external
`docker_postgres_data` volume and uses `PGDATA=/tmp/pgdata`. The anonymous image volume at
`/var/lib/postgresql/data` is not the active cluster location.

Set `IFM_POSTGRES_PASSWORD` without writing it to source control, then validate the existing cluster before adopting
the compose definition:

```powershell
$env:IFM_POSTGRES_PASSWORD = '<development-password>'
docker compose -f Docker/Postgres/docker-compose.yml config --quiet
```

Never run `down --volumes` against a database composition.
