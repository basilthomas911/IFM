# NATS JetStream Development Container

This composition reproduces the running `ifm-nats-server` service with NATS 2.12.0 and reuses the existing external
`natsjetstream_nats_data` volume. Port 4222 remains available to workstation applications. The monitoring endpoint is
limited to loopback on port 8222.

```powershell
docker compose -f Docker/NatsJetstream/docker-compose.yml config --quiet
docker compose -f Docker/NatsJetstream/docker-compose.yml up -d --wait
```

Do not use `down --volumes`; the JetStream volume contains durable actor messages and consumers.
