//! Existing prepared option-cache transport, encoded directly by Rust. Each
//! publication is bounded before compression and split into 64 KiB chunks. These
//! bytes retain no provider resource and use a separate writer from actor IDs.
use super::{codec, wire::guid};
use crate::option_chain::composition::model::Snapshot;
use rmpv::Value;
use sha2::{Digest, Sha256};
/// Existing maximum compressed publication length.
pub const MAXIMUM: usize = 8 * 1024 * 1024;
/// Existing cache frame chunk size, independent of DBN's metadata read buffer.
pub const CHUNK: usize = 64 * 1024;
/// Complete detached prepared-chain publication and its existing chunk protocol.
pub struct Publication {
    /// Frozen source/context values for native request-specific cache validation.
    /// Context graphs contain copied reference data, never provider resources.
    pub snapshot: std::sync::Arc<Snapshot>,
    /// Authoritative source session day number.
    pub date: i32,
    /// Exact global policy digest.
    pub configuration: String,
    /// Logical generation; receiver never confuses it with a physical SDK ID.
    pub generation: [u8; 16],
    /// Existing lookup scope (can differ from a composition request scope).
    pub scope: String,
    /// Monotonic native cache version.
    pub version: i64,
    /// Frozen authoritative expiry; queries must not serve an expired capture.
    pub until: i64,
    /// Existing plain five-key Host publication, with no resource handles.
    bytes: Vec<u8>,
    digest: String,
}
impl Publication {
    /// Encodes an already sealed complete snapshot; no financial calculation or
    /// provider I/O happens during publication framing.
    ///
    /// # Errors
    /// Unsealed snapshots, invalid identity or oversized payloads are rejected.
    pub fn new(
        snapshot: &Snapshot,
        date: i32,
        configuration: &str,
        version: i64,
        scope: &str,
    ) -> Result<Self, &'static str> {
        if snapshot.digest.len() != 64
            || !(0..=3652058).contains(&date)
            || configuration.trim().is_empty()
            || configuration.len() > 128
            || version <= 0
            || scope.trim().is_empty()
            || scope.encode_utf16().count() > 512
        {
            return Err("Invalid native option-cache publication identity");
        }
        let content = codec::encode(&Value::Array(vec![
            snapshot.value()?,
            date.into(),
            configuration.into(),
            version.into(),
            scope.into(),
        ]))?;
        // A sealed snapshot is <=4 MiB. Check before compression as a second
        // independent boundary for callers constructing an unsealed model.
        if content.len() > 4 * 1024 * 1024 + 4096 {
            return Err("Native option-cache publication exceeds content bound");
        }
        let bytes = content;
        if bytes.is_empty() || bytes.len() > MAXIMUM {
            return Err("Native option-cache publication exceeds transport bound");
        }
        let digest = format!("{:X}", Sha256::digest(&bytes));
        Ok(Self {
            snapshot: std::sync::Arc::new(snapshot.clone()),
            date,
            configuration: configuration.into(),
            generation: snapshot.generation,
            scope: scope.into(),
            version,
            until: snapshot.until,
            bytes,
            digest,
        })
    }
    /// Retained complete transport length for native cache memory accounting.
    pub fn len(&self) -> usize {
        self.bytes.len()
    }
    /// A validated publication always contains nonempty MessagePack content.
    pub fn is_empty(&self) -> bool {
        self.bytes.is_empty()
    }
    /// Exact existing five-key publication for an explicit cache query. This
    /// borrows immutable copied bytes and owns no provider or publisher resource.
    pub fn content(&self) -> &[u8] {
        &self.bytes
    }
    /// Exact number of independently bounded chunks.
    pub fn chunks(&self) -> usize {
        self.bytes.len().div_ceil(CHUNK)
    }
    /// Encodes one existing seven-key frame plus the standard NATS compression.
    /// Only one chunk is copied at a time; no complete second frame array is held.
    ///
    /// # Errors
    /// Rejects a missing chunk or scalar/encoding failure.
    pub fn frame(&self, index: usize) -> Result<Vec<u8>, &'static str> {
        let offset = index
            .checked_mul(CHUNK)
            .ok_or("Native option-cache offset overflow")?;
        if offset >= self.bytes.len() {
            return Err("Native option-cache chunk unavailable");
        }
        codec::compress(codec::encode(&Value::Array(vec![
            guid(self.generation).into(),
            self.scope.clone().into(),
            self.version.into(),
            self.bytes.len().into(),
            offset.into(),
            Value::Binary(self.bytes[offset..(offset + CHUNK).min(self.bytes.len())].to_vec()),
            self.digest.clone().into(),
        ]))?)
    }
}
