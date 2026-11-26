//! Existing prepared option-cache transport, encoded directly by Rust. Each
//! publication is bounded before compression and split into 64 KiB chunks. These
//! bytes retain no provider resource and use a separate writer from actor IDs.
use super::{codec, wire::guid};
use crate::option_chain::composition::model::Snapshot;
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
    pub(crate) fn checkpoint(&self) -> Result<rmpv::Value, String> {
        use rmpv::Value as V;
        if self.bytes.is_empty() || self.bytes.len() > MAXIMUM {
            return Err("Original option cache bytes exceed bound".into());
        }
        Ok(V::Array(vec![
            1.into(),
            self.snapshot.checkpoint(),
            self.date.into(),
            self.configuration.clone().into(),
            V::Binary(self.generation.to_vec()),
            self.scope.clone().into(),
            self.version.into(),
            self.until.into(),
            V::Binary(self.bytes.clone()),
            self.digest.clone().into(),
        ]))
    }
    pub(crate) fn restore_checkpoint(value: &rmpv::Value) -> Result<Self, String> {
        use crate::checkpoint as c;
        use rmpv::Value as V;
        let f = c::fields(value, 10)?;
        if c::unsigned(&f[0])? != 1 {
            return Err("Option publication checkpoint version invalid".into());
        }
        let snapshot = std::sync::Arc::new(Snapshot::restore_checkpoint(&f[1])?);
        let generation = match &f[4] {
            V::Binary(bytes) if bytes.len() == 16 => bytes.as_slice().try_into().unwrap(),
            _ => return Err("Option publication UUID invalid".into()),
        };
        let bytes = match &f[8] {
            V::Binary(bytes) if !bytes.is_empty() && bytes.len() <= MAXIMUM => bytes.clone(),
            _ => return Err("Option publication bytes invalid".into()),
        };
        let result = Self {
            snapshot,
            date: c::number(&f[2])?
                .try_into()
                .map_err(|_| "Option publication date overflow")?,
            configuration: c::string(&f[3], 128)?,
            generation,
            scope: c::string(&f[5], 2048)?,
            version: c::number(&f[6])?,
            until: c::number(&f[7])?,
            bytes,
            digest: c::string(&f[9], 64)?,
        };
        if result.generation != result.snapshot.generation
            || result.until != result.snapshot.until
            || result.version <= 0
            || result.scope.trim().is_empty()
            || result.scope.encode_utf16().count() > 512
            || !(0..=3652058).contains(&result.date)
            || result.configuration.trim().is_empty()
            || format!("{:X}", Sha256::digest(&result.bytes)) != result.digest
        {
            return Err("Original option publication identity/digest differs".into());
        }
        // Verify the bytes name exactly the retained immutable snapshot/metadata; do not call new(), seal() or any pricer.
        let mut expected = Vec::new();
        rmp::encode::write_array_len(&mut expected, 5).map_err(|e| e.to_string())?;
        result
            .snapshot
            .encode_into(&mut expected)
            .map_err(str::to_owned)?;
        rmp::encode::write_sint(&mut expected, i64::from(result.date))
            .map_err(|e| e.to_string())?;
        rmp::encode::write_str(&mut expected, &result.configuration).map_err(|e| e.to_string())?;
        rmp::encode::write_sint(&mut expected, result.version).map_err(|e| e.to_string())?;
        rmp::encode::write_str(&mut expected, &result.scope).map_err(|e| e.to_string())?;
        if expected != result.bytes {
            return Err("Original option publication bytes and snapshot differ".into());
        }
        Ok(result)
    }
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
        Self::owned(snapshot.clone(), date, configuration, version, scope)
    }

    /// Transfer an already sealed snapshot into its cache publication without cloning the graph.
    ///
    /// # Errors
    /// Invalid identity or oversized complete content is rejected before publication.
    pub fn owned(
        snapshot: Snapshot,
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
        let mut content = Vec::new();
        rmp::encode::write_array_len(&mut content, 5).map_err(|_| "Event encoding failed")?;
        snapshot.encode_into(&mut content)?;
        let encoded = (|| {
            rmp::encode::write_sint(&mut content, i64::from(date))?;
            rmp::encode::write_str(&mut content, configuration)?;
            rmp::encode::write_sint(&mut content, version)?;
            rmp::encode::write_str(&mut content, scope)
        })();
        encoded.map_err(|_| "Event encoding failed")?;
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
        let generation = snapshot.generation;
        let until = snapshot.until;
        Ok(Self {
            snapshot: std::sync::Arc::new(snapshot),
            date,
            configuration: configuration.into(),
            generation,
            scope: scope.into(),
            version,
            until,
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
        let mut output = Vec::with_capacity((self.bytes.len() - offset).min(CHUNK) + 256);
        let encoded = (|| {
            rmp::encode::write_array_len(&mut output, 7)?;
            rmp::encode::write_str(&mut output, &guid(self.generation))?;
            rmp::encode::write_str(&mut output, &self.scope)?;
            rmp::encode::write_sint(&mut output, self.version)?;
            rmp::encode::write_uint(&mut output, self.bytes.len() as u64)?;
            rmp::encode::write_uint(&mut output, offset as u64)?;
            rmp::encode::write_bin(
                &mut output,
                &self.bytes[offset..(offset + CHUNK).min(self.bytes.len())],
            )?;
            rmp::encode::write_str(&mut output, &self.digest)
        })();
        encoded.map_err(|_| "Event encoding failed")?;
        codec::compress(output)
    }
}

#[cfg(test)]
mod checkpoint_tests {
    use super::*;
    use rmpv::Value as V;
    #[test]
    fn cache_checkpoint_restores_original_content_chunks_and_digest_and_rejects_crossed_metadata() {
        let at = 638_000_000_000_000_000;
        let mut snapshot = Snapshot {
            ranking: None,
            schema: 2,
            id: [7; 16],
            scope: "original-scope".into(),
            token: "original-token".into(),
            horizon: "Monthly".into(),
            generation: [8; 16],
            at,
            until: at + 1234,
            instruments: vec![],
            digest: String::new(),
        };
        snapshot.seal().unwrap();
        let publication =
            Publication::owned(snapshot, 738000, "original-config-v7", 9, &"原".repeat(256))
                .unwrap();
        let checkpoint = publication.checkpoint().unwrap();
        let restored = Publication::restore_checkpoint(&checkpoint).unwrap();
        assert_eq!(publication.content(), restored.content());
        assert_eq!(publication.digest, restored.digest);
        assert_eq!(publication.frame(0).unwrap(), restored.frame(0).unwrap());
        assert_eq!(checkpoint, restored.checkpoint().unwrap());
        let mut changed = checkpoint.clone();
        let V::Array(ref mut fields) = changed else {
            panic!()
        };
        fields[6] = 10.into();
        assert!(Publication::restore_checkpoint(&changed).is_err());
        let mut changed = checkpoint;
        let V::Array(ref mut fields) = changed else {
            panic!()
        };
        let V::Binary(bytes) = &mut fields[8] else {
            panic!()
        };
        bytes[0] ^= 1;
        assert!(Publication::restore_checkpoint(&changed).is_err());
    }
}
