# Scoped owned-connection adaptation

Source: crates.io async-nats 0.50.0, copied from the local Cargo registry.
Original license and upstream metadata are retained. This Cargo patch is scoped
only to the new market-data crate; current UI/backend dependencies are unchanged.

Changes: `src/lib.rs` factors connection setup into an internal tracked path and
adds `connect_owned` / `OwnedConnection`. The existing `connect_with_options`
retains its detached-task API behaviour. The new owner aborts socket/event tasks
on Drop and explicitly awaits both on shutdown. No broker drain/flush is required
to release sockets. Native callers supply an original deadline and cancellation.

The opt-in `ConnectOptions::socket_resolver` callback replaces the core
connector's OS resolver only for native callers that configure it. The native
market-data caller supplies its existing bounded asynchronous DNS resolver;
numeric endpoints need no DNS lookup. The original default remains unchanged.
When an explicit in-memory TLS ClientConfig is supplied, `config_tls` returns it
before loading OS roots or certificate files. The native publisher supplies such
a configuration, so reset does not start blocking resolver or certificate jobs.

The ordinary market-data publisher uses core publish/roundtrip. The evidence
exchange additionally owns one bounded Core NATS reply subscription. Neither path
creates JetStream consumers or services. Both core connection/event worker handles
are owned and joined on explicit shutdown.

Qualification must prove socket EOF after joined cancellation, cancellation during
connect/handshake, blackholed broker writes, bounded queued data and no surviving
workers. Abort is cooperative async cancellation, not OS-thread termination.

## Protocol proof and bounded parsing corrections

`Client::roundtrip` is a new opt-in command. Existing `flush` retains its local
socket-flush meaning. The connection handler tracks FIFO PONG observers separately
from heartbeat counts; pending explicit PINGs are bounded to 64. A server `-ERR`
fails pending proof and cannot be overwritten by a subsequent PONG. Disconnect
clears the connection-scoped failure and observers. Initial connection setup now
requires PONG, handling server PING/INFO/+OK explicitly before reporting readiness.

Inbound control lines and headers are bounded to 65,536 bytes, payloads to 4 MiB.
Declared sizes are checked before allocation/arithmetic. Malformed short errors
and invalid MSG/HMSG trailing CRLF are rejected without panics. The native tests
exercise rejected publication, oversized/malformed protocol, silent server and
joined cancellation. Original licenses and metadata are retained.

The evidence exchange uses one Core subscription marked with_connection_owner.
That new opt-in Subscriber method suppresses detached unsubscribe-on-Drop because
the caller owns and joins the complete connection. Existing Subscriber behavior
remains unchanged for ordinary upstream callers. Reply queues are bounded to 64
and native receipt bodies to 8 KiB. Real TCP tests prove joined cancellation while
the external writer remains silent. A server PONG is not a durable storage or
consumer acknowledgment.


Additional scoped inbound/error qualification: opt-in `inbound_payload_limit`
clamps MSG/HMSG declared payloads before allocation and is carried through TLS
connection replacement. Native market-data publication sets 8,192 bytes for its
small control receipts. The default client limit remains 4 MiB. Decoder IO errors
complete pending protocol-roundtrip observers and emit their bounded cause before
the ordinary disconnected notification, so recovery does not lose the root cause.
The local TCP fixture declares an 8,193-byte body without sending it and verifies
immediate bounded rejection, cause retention and joined cancellation. The existing
separate C# backend and original Databento native bridge do not use this fork.
