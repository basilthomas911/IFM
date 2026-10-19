//! Sequence-aware reconstruction of ABI v5 MessagePack publications.
//!
//! Each publication kind has an independent slot containing its sequence, bytes,
//! optional patch tree, pending-delivery flag, and latency stamp. Kind values index a
//! fixed nine-slot array; callers must use a supported ABI kind in the range 0..9.
//!
//! Mode 0 replaces the complete document; mode 3 applies a patch against the exact
//! base sequence. Path kinds address map keys (0), array indexes (1), or embedded
//! binary MessagePack documents (2). Binary documents remain binary in the outer wire
//! representation. A failed patch resets sequence/tree state so the caller can request
//! a full resynchronization; callers must not consume a failed reconstruction.
//!
//! `pending` remains set until the application successfully queues the decoded update
//! and acknowledges it. The reusable scratch vector avoids allocating a transport
//! buffer for every poll. Timestamps use .NET UTC ticks, 100 ns since 0001-01-01.
//!
//! # Delivery acknowledgement
//!
//! ```
//! use ifm_ui_rust::{publication_transfer::TransferCache, wire};
//! let mut cache = TransferCache::default();
//! let bytes = wire::encode(&vec![1_u32, 2]).unwrap();
//! assert!(cache.legacy(8, bytes.clone()));
//! // Decode/queue the update before acknowledging it.
//! let rows: Vec<u32> = wire::decode(cache.bytes(8)).unwrap();
//! assert_eq!(rows, vec![1, 2]);
//! cache.acknowledge(8);
//! assert!(!cache.legacy(8, bytes)); // Unchanged content is not delivered twice.
//! ```

//! ABI v5 incremental publication transport. Binary row documents retain their MessagePack representation.
use rmpv::Value;
use serde::Deserialize;
use std::io::Cursor;

/// One patch traversal segment: map key (0), array index (1), or embedded MessagePack boundary (2).
#[derive(Clone, Debug, Deserialize, PartialEq, Eq)]
struct Path {
    /// Patch traversal kind: map key (0), array index (1), or embedded MessagePack document (2).
    kind: u8,
    /// Map key selected by a kind-0 path segment; unused for array and binary-boundary segments.
    #[serde(default)]
    key: String,
    /// Zero-based array index selected by a kind-1 path segment.
    #[serde(default)]
    index: usize,
}
/// One patch mutation, including its target path and replacement or array-splice data.
#[derive(Debug, Deserialize)]
struct Operation {
    /// Ordered path from the publication root to the mutation target, including embedded-document boundaries.
    path: Vec<Path>,
    /// Patch opcode: replacement (0), map-key removal (1), or array splice (2).
    action: u8,
    /// Encoded replacement MessagePack value for opcode 0; absent for operations that do not replace a value.
    #[serde(default)]
    value: Option<serde_bytes::ByteBuf>,
    /// Zero-based patch array index or insertion offset.
    #[serde(default)]
    index: usize,
    /// Number of array elements removed by the splice operation.
    #[serde(rename = "delete", default)]
    delete_count: usize,
    /// Encoded MessagePack values inserted by an array-splice operation.
    #[serde(default)]
    items: Option<Vec<serde_bytes::ByteBuf>>,
}
/// Ordered mutations tied to an exact base publication sequence.
#[derive(Debug, Deserialize)]
struct Patch {
    /// Sequence that the patch requires as its exact input document.
    #[serde(rename = "baseSequence")]
    base_sequence: u64,
    /// Published sequence/order value; transfer sequences are tracked independently for each kind.
    sequence: u64,
    /// Ordered mutations applied to the publication base.
    operations: Vec<Operation>,
}
/// Publication identity and source/production/native-receipt timestamps in .NET UTC ticks.
#[derive(Clone, Copy, Default, Debug)]
pub struct Stamp {
    /// ABI publication kind indexing one of the nine independent transfer slots.
    pub kind: u32,
    /// Published sequence/order value; transfer sequences are tracked independently for each kind.
    pub sequence: u64,
    /// Source observation/receipt time in .NET UTC ticks, as supplied by the bridge.
    pub source_ticks: i64,
    /// Managed publication timestamp in .NET UTC ticks.
    pub produced_ticks: i64,
    /// Native receipt timestamp in .NET UTC ticks.
    pub received_ticks: i64,
}
/// One publication kind's reconstructed bytes and delivery/resynchronization state.
#[derive(Default)]
struct Slot {
    /// Published sequence/order value; transfer sequences are tracked independently for each kind.
    sequence: u64,
    /// Rust-owned encoded MessagePack bytes for the latest accepted content.
    bytes: Vec<u8>,
    /// Lazily decoded MessagePack tree retained for incremental patches.
    tree: Option<Value>,
    /// Whether the reconstructed document still needs to be queued successfully to the UI.
    pending: bool,
    /// Identity and timing of the last accepted publication.
    stamp: Stamp,
}
/// Nine independent publication slots and a reusable managed-copy buffer. Supported ABI kinds must index these slots.
#[derive(Default)]
pub struct TransferCache {
    /// Independent reconstructed state for ABI publication kinds 0 through 8.
    slots: [Slot; 9],
    /// Reusable caller-owned copy buffer. The host fills/truncates it before complete reconstructs a publication.
    pub scratch: Vec<u8>,
}
impl TransferCache {
    /// Return the cached sequence for a supported ABI publication kind. Panics if the kind is outside the nine-slot array.
    pub fn sequence(&self, kind: u32) -> u64 {
        self.slots[kind as usize].sequence
    }
    /// Reset the requested kind's sequence to zero to force full resynchronization. Retained bytes are not a newly accepted publication; the kind must be valid.
    pub fn reset(&mut self, kind: u32) {
        self.slots[kind as usize].sequence = 0;
    }
    /// Report whether reconstructed bytes for this kind still await successful UI-queue delivery. The kind must be a valid cache index.
    pub fn pending(&self, kind: u32) -> bool {
        self.slots[kind as usize].pending
    }
    /// Borrow reconstructed MessagePack bytes for a supported publication kind. Consume them only after successful reconstruction.
    pub fn bytes(&self, kind: u32) -> &[u8] {
        &self.slots[kind as usize].bytes
    }
    /// Return the cached publication identity and latency timestamps for a supported kind.
    pub fn stamp(&self, kind: u32) -> Stamp {
        self.slots[kind as usize].stamp
    }
    /// Clear pending delivery after the application has successfully queued the decoded publication. The kind must be a valid cache index.
    pub fn acknowledge(&mut self, kind: u32) {
        self.slots[kind as usize].pending = false;
    }
    /// Compare legacy full-document bytes, advancing the local sequence and delivery flag only when content changes. Returns whether delivery remains pending.
    pub fn legacy(&mut self, kind: u32, bytes: Vec<u8>) -> bool {
        let slot = &mut self.slots[kind as usize];
        if slot.bytes != bytes {
            slot.bytes = bytes;
            slot.sequence += 1;
            slot.pending = true;
            slot.tree = None;
        }
        slot.pending
    }
    /// Consume the scratch payload as a full document (mode 0) or exact-base patch (mode 3), then stamp it pending. On failure resets sequence/tree state; returns the validation/decoding error.
    pub fn complete(
        &mut self,
        kind: u32,
        mode: i32,
        sequence: u64,
        produced_ticks: i64,
        source_ticks: i64,
    ) -> Result<(), String> {
        let payload = std::mem::take(&mut self.scratch);
        let slot = &mut self.slots[kind as usize];
        let result = (|| {
            if mode == 3 {
                let patch: Patch = crate::wire::decode(&payload)?;
                if patch.base_sequence != slot.sequence || patch.sequence != sequence {
                    return Err("Publication patch base mismatch".into());
                }
                let mut next = match slot.tree.take() {
                    Some(value) => value,
                    None => decode(&slot.bytes)?,
                };
                apply(&mut next, &patch.operations, 0)?;
                slot.bytes.clear();
                rmpv::encode::write_value(&mut slot.bytes, &next).map_err(|e| e.to_string())?;
                slot.tree = Some(next);
            } else if mode == 0 {
                slot.bytes.clear();
                slot.bytes.extend_from_slice(&payload);
                slot.tree = None;
            } else {
                return Err("Invalid publication transfer mode".into());
            }
            slot.sequence = sequence;
            slot.pending = true;
            slot.stamp = Stamp {
                kind,
                sequence,
                source_ticks,
                produced_ticks,
                received_ticks: utc_ticks(),
            };
            Ok(())
        })();
        self.scratch = payload;
        if result.is_err() {
            slot.sequence = 0;
            slot.tree = None;
        }
        result
    }
}
/// Return current UTC time as .NET ticks: 100-nanosecond intervals since 0001-01-01. A pre-Unix system-clock failure contributes zero Unix duration.
pub fn utc_ticks() -> i64 {
    621355968000000000
        + (std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap_or_default()
            .as_nanos()
            / 100) as i64
}
/// Decode exactly one MessagePack value, rejecting trailing bytes and malformed data.
fn decode(bytes: &[u8]) -> Result<Value, String> {
    let mut cursor = Cursor::new(bytes);
    let value = rmpv::decode::read_value(&mut cursor).map_err(|e| e.to_string())?;
    if cursor.position() != bytes.len() as u64 {
        return Err("Trailing MessagePack bytes".into());
    }
    Ok(value)
}
/// Resolve a patch path to a mutable map/array value. Reject missing keys, invalid indexes, and incompatible container kinds.
fn target<'a>(value: &'a mut Value, path: &[Path]) -> Result<&'a mut Value, String> {
    let mut current = value;
    for segment in path {
        current = match (segment.kind, current) {
            (0, Value::Map(fields)) => fields
                .iter_mut()
                .find(|(key, _)| key.as_str() == Some(&segment.key))
                .map(|(_, value)| value)
                .ok_or("Missing patch map key")?,
            (1, Value::Array(items)) => items
                .get_mut(segment.index)
                .ok_or("Invalid patch array index")?,
            _ => return Err("Invalid patch path".into()),
        };
    }
    Ok(current)
}
/// Apply ordered replacement/removal/array-splice operations, recursively decoding binary document boundaries. Reject invalid paths, kinds, and payloads.
fn apply(value: &mut Value, operations: &[Operation], skip: usize) -> Result<(), String> {
    let mut index = 0;
    while index < operations.len() {
        let operation = &operations[index];
        let path = operation.path.get(skip..).ok_or("Invalid patch prefix")?;
        if let Some(binary) = path.iter().position(|segment| segment.kind == 2) {
            let prefix = &operation.path[..skip + binary];
            let mut end = index + 1;
            while end < operations.len()
                && operations[end].path.len() > skip + binary
                && operations[end].path[..skip + binary] == *prefix
                && operations[end].path[skip + binary].kind == 2
            {
                end += 1;
            }
            let Value::Binary(bytes) = target(value, &path[..binary])? else {
                return Err("Patch document is not binary".into());
            };
            let mut inner = decode(bytes)?;
            apply(&mut inner, &operations[index..end], skip + binary + 1)?;
            bytes.clear();
            rmpv::encode::write_value(bytes, &inner).map_err(|e| e.to_string())?;
            index = end;
            continue;
        }
        match operation.action {
            0 => {
                let replacement =
                    decode(operation.value.as_ref().ok_or("Patch has no replacement")?)?;
                if path.is_empty() {
                    *value = replacement;
                } else {
                    let (last, parent) = path.split_last().unwrap();
                    match (last.kind, target(value, parent)?) {
                        (0, Value::Map(fields)) => {
                            if let Some((_, old)) = fields
                                .iter_mut()
                                .find(|(key, _)| key.as_str() == Some(&last.key))
                            {
                                *old = replacement;
                            } else {
                                fields.push((Value::from(last.key.clone()), replacement));
                            }
                        }
                        (1, Value::Array(items)) => {
                            *items
                                .get_mut(last.index)
                                .ok_or("Invalid replacement index")? = replacement
                        }
                        _ => return Err("Invalid replacement target".into()),
                    }
                }
            }
            1 => {
                let (last, parent) = path.split_last().ok_or("Cannot remove publication root")?;
                let Value::Map(fields) = target(value, parent)? else {
                    return Err("Removal needs a map".into());
                };
                let position = fields
                    .iter()
                    .position(|(key, _)| key.as_str() == Some(&last.key))
                    .ok_or("Missing removal key")?;
                fields.remove(position);
            }
            2 => {
                let Value::Array(items) = target(value, path)? else {
                    return Err("Splice needs an array".into());
                };
                let end = operation
                    .index
                    .checked_add(operation.delete_count)
                    .ok_or("Splice overflow")?;
                if operation.index > items.len() || end > items.len() {
                    return Err("Invalid splice range".into());
                }
                let inserted = operation
                    .items
                    .as_deref()
                    .unwrap_or_default()
                    .iter()
                    .map(|item| decode(item))
                    .collect::<Result<Vec<_>, _>>()?;
                items.splice(operation.index..end, inserted);
            }
            _ => return Err("Unknown patch action".into()),
        }
        index += 1;
    }
    Ok(())
}
