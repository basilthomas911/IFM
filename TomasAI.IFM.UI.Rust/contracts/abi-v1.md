# Pilot ABI version 1

Windows x64 Cdecl. ApiV1 uses sequential native layout: u32 version at 0, u32 byte size at 4, then four 8-byte function pointers at offsets 8, 16, 24, 32. Total size 40 bytes. Bootstrap GetApi(version, pointer, size) negotiates version 1. All exported methods contain ordinary managed exceptions.

Create(out u64 handle); Copy(handle, u32 kind, caller buffer, i32 capacity, out i32 required); Action(handle,u32 action); Destroy(handle). Copy kind 0 is the immutable detail snapshot, kind 1 the catalog, kind 2 the complete virtual-grid fixture dataset. An empty publication means not ready yet. Capacity limit is 16 MiB. Status 0 succeeds, 1 requires a larger buffer, -1 invalid input/version, -2 managed failure, -3 stale handle, -4 full action queue. Caller buffers are borrowed only during the call. Pointers are trusted in-process inputs.

Actions 0/1 select fixture workflow, 2 refreshes it (or reloads live mode), 3 loads the grid fixture, 10/11/12 select Daily/Weekly/Monthly, and tokens >=1000 select a stable live workflow identity. Tokens are scoped to the session and never interpreted as changing row indexes. Acceptance means queued locally, not broker success. The action queue has capacity 32. These are fixture or read-only presentation actions; no trading command API is exposed.

UTF-8 JSON snapshots have schema 1, epoch, monotonically increasing generation, mode, workflow identity/revision, status, optional workflow choice/action tokens and roots. Live subscriptions are owned by the managed session; no subscription runs in Rust. Nodes have stable workflow/path IDs, label, property name/value rows and children. Decimal and date values are invariant strings; no .NET decimal binary layout crosses FFI. Maximum tree preparation is 10,000 nodes, 12 levels and 200 collection entries per node, with visible truncation markers.

One serialized managed owner prepares all nodes. Rust's worker performs bounded buffer-growth retries and adopts complete generations. UI expansion does not call FFI. Shutdown joins the managed owner with a five-second deadline and releases the DI container; it does not stop a backend.

This is the narrow read-only pilot API. Session status/error payloads and acknowledged command-result APIs for general screens remain future contract work before live trading actions are introduced.
