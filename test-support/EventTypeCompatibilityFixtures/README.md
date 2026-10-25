# Frozen event identity compatibility corpus

`manifest.json` contains six frozen event-log payloads: Iron Condor position,
ITI signal, and fund mandate events, each plain and LZ4. Bytes were generated
with the **pre-resolver codec from the Git commit recorded in the manifest**,
not the new resolver implementation. The original codec source hash and each
payload SHA-256 are recorded. Payloads are base64 to keep fixtures portable text.

All data is synthetic and fixed. Expected objects were decoded by that old
codec with storage EventId 42. Only the computed UserName display property is
excluded from JSON comparisons; the persisted principal remains covered.

Acceptance tests verify hashes, replay through both read models and the codec,
stable IDs, legacy CLR names, missing-assembly aliases, outbox delivery, metadata
restoration, and byte-for-byte re-encoding. Do not regenerate these fixtures with
the current codec to make a compatibility failure pass.
