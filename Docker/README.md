# IFM Development Containers

Repository-owned development compositions are organized by service:

1. `NatsJetstream`
2. `Postgres`
3. `ScyllaDb`
4. `ScyllaManager`
5. `DatabaseBackup`

Redis was retired on November 8, 2026. The application uses typed local caches and PostgreSQL; the former Redis data is retained separately for recovery. See `Redis/README.md`.

`OpenWebUI` and `vLLM` remain separate optional AI tooling and were not running as part of the captured IFM application
stack. Each database composition documents its external volume and must be stopped without `--volumes`.

The current workstation primarily runs application executables on Windows against published localhost ports. The
future production design runs on Linux and mounts physical SATA backup storage directly; the E-drive bind mounts in
development are temporary compatibility paths, not a production storage recommendation.
