# Rust/.NET bridge ABI version 3

This file retains its original pilot filename. The current executable negotiates ABI **3** only; versions 1 and 2 used JSON and are rejected. Rebuild and deploy Rust and the managed bridge together.

## Native layout and ownership

Windows x64 Cdecl. The 40-byte base table has u32 version and size, then Create, Copy, Action and Destroy pointers. The 48-byte ApiV3 table appends Submit at offset 40. GetApi(3, pointer, 48) returns the complete table. Existing handle ownership, caller-owned buffers, queued actions, correlated request outcomes and shutdown behavior are retained.

Copy accepts at most 16 MiB. Submit accepts at most 256 KiB. Return codes: 0 success/queued, 1 larger copy buffer required, -1 invalid input/version, -2 managed failure, -3 stale handle, -4 busy/full queue. Queuing a request is distinct from its completion or broker acceptance. Rust owns copied bytes; neither side keeps the other side's pointers.

## Wire encoding

All Copy publications and Submit requests use **uncompressed MessagePack**. BridgeWire pins MessagePackCompression.None independently of global broker/storage serializer options. Rust uses rmp-serde. There is no LZ4 or typeless .NET envelope and no JSON compatibility fallback.

Bridge records declare explicit camelCase string keys. Maps preserve dictionary keys exactly. Collections use arrays; strings, booleans, null and integers use native MessagePack values. GUIDs are canonical strings. Lookup timestamps are round-trip ISO 8601 strings. Displayed financial amounts retain their existing invariant string representations, avoiding floating point conversion of monetary text. Unknown fields remain compatible with typed deserialization.

The string catalog is read from its existing JSON resource once and published as MessagePack. Existing editable JSON documents, nested editor document fields, configuration files and JSON diagnostic artifacts retain their formats. Editors currently compose internal JSON intent documents; Rust converts those values to standard MessagePack before Submit. No JSON text envelope crosses the ABI.

| Copy kind | Publication |
|---|---|
| 0 | Strategy details, graph and workflow updates |
| 1 | String catalog |
| 2 | Complete virtual-grid fixture dataset |
| 3 | Lookup editor |
| 4 | Strategy/risk inspection |
| 5 | Contract/configuration editors |
| 6 | Dashboard and market charts |
| 7 | Economic calendar, status console and feed controls |
| 8 | Trade workspace and submission confirmation/outcomes |

An empty publication means not ready yet. Snapshot schema remains 1. Epoch/generation and stable IDs retain their existing meanings. Rendering reads Rust-owned cached snapshots and never performs managed reflection.

## Regression verification

Managed BridgeWireTests and AbiTests cover explicit compression, portable lookup values, malformed/truncated/old-format request rejection, ABI size/version negotiation, buffer capacity and stale handles. Rust tests/wire.rs covers ordinary numeric encoding with serde_json arbitrary_precision enabled, Unicode, exact monetary strings, u64 limits and invalid payloads.

The explicitly invoked tests/bridge_messagepack.rs hosts the actual .NET runtime from Rust, decodes every publication kind, verifies all 100,000 grid rows and sends read-only fixture requests through all four request gateways. Fixture failures must return the same correlated request ID, and duplicates retain their acknowledgement. It requires IFM_RUST_BRIDGE_TEST_ROOT pointing to built managed output and runs with --ignored; it never submits broker orders.
