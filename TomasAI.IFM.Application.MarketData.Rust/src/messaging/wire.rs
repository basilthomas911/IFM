//! Exact existing eight-key MessagePack dataset envelope, constructed in Rust.
//!
//! Guid values use the existing .NET string formatter; dates use DateOnly day
//! numbers. Inner event payloads remain binary MessagePack, not JSON or pointers.

use crate::dataset::transport::Lane;
use rmpv::Value;

/// Maximum copied event payload; allocation is checked before envelope encoding.
pub const MAX_PAYLOAD_BYTES: usize = 1024 * 1024;

/// One existing DatasetPublicationEnvelope, detached from dataset resources.
#[derive(Clone)]
pub struct Frame {
    /// Physical source role and dataset.
    pub lane: Lane,
    /// Authoritative API ValueDate as .NET DateOnly.DayNumber.
    pub value_date: i32,
    /// Logical dataset owner in .NET Guid.ToByteArray byte order.
    pub worker: [u8; 16],
    /// Dataset generation in .NET Guid.ToByteArray byte order.
    pub generation: [u8; 16],
    /// Positive desired-subscription manifest revision.
    pub manifest_revision: i64,
    /// Positive monotonic publication sequence for this owner.
    pub sequence: i64,
    /// Existing byte enum: trade=1, quote=2, price=3, statistics=4,
    /// replay=5, trade evidence=6, option quote=7.
    pub kind: u8,
    /// Owned event bytes; no managed buffer or dataset handle is retained.
    pub payload: Vec<u8>,
}

/// Formats the .NET little-endian Guid fields without changing their identity.
pub fn guid(bytes: [u8; 16]) -> String {
    format!(
        "{:08x}-{:04x}-{:04x}-{:02x}{:02x}-{:02x}{:02x}{:02x}{:02x}{:02x}{:02x}",
        u32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]),
        u16::from_le_bytes([bytes[4], bytes[5]]),
        u16::from_le_bytes([bytes[6], bytes[7]]),
        bytes[8],
        bytes[9],
        bytes[10],
        bytes[11],
        bytes[12],
        bytes[13],
        bytes[14],
        bytes[15]
    )
}

impl Frame {
    /// Builds the same deployment-scoped subject as MarketDataHostConnection.
    ///
    /// # Errors
    /// Rejects wildcard, empty or multi-token deployment scope and invalid frame.
    pub fn subject(&self, scope: &str) -> Result<String, &'static str> {
        self.validate()?;
        if scope.is_empty()
            || scope.len() > 128
            || !scope
                .bytes()
                .all(|c| c.is_ascii_alphanumeric() || c == b'-')
        {
            return Err("Invalid deployment scope");
        }
        let role = if self.lane == Lane::OptionsGlbx {
            "Options"
        } else {
            "Futures"
        };
        Ok(format!(
            "ifm.{scope}.marketdata.v1.publications.{role}.{}",
            guid(self.worker).replace('-', "")
        ))
    }

    pub(crate) fn validate(&self) -> Result<(), &'static str> {
        if !(0..=3_652_058).contains(&self.value_date)
            || self.worker == [0; 16]
            || self.generation == [0; 16]
            || self.manifest_revision <= 0
            || self.sequence <= 0
            || !(1..=7).contains(&self.kind)
            || self.payload.is_empty()
            || self.payload.len() > MAX_PAYLOAD_BYTES
        {
            return Err("Invalid dataset publication");
        }
        Ok(())
    }

    /// Copies the existing eight-key dataset envelope for an explicit query.
    /// This is detached data, not a streaming FFI receiver or provider handle.
    ///
    /// # Errors
    /// Invalid identity, source kind or payload capacity.
    pub fn value(&self) -> Result<Value, &'static str> {
        self.validate()?;
        Ok(Value::Array(vec![
            self.lane.dataset().into(),
            self.value_date.into(),
            guid(self.worker).into(),
            guid(self.generation).into(),
            self.manifest_revision.into(),
            self.sequence.into(),
            self.kind.into(),
            Value::Binary(self.payload.clone()),
        ]))
    }
    /// Encodes the existing envelope without changing its compression boundary.
    ///
    /// # Errors
    /// Invalid identity, source data or encoding failure.
    pub fn content(&self) -> Result<Vec<u8>, &'static str> {
        super::codec::encode(&self.value()?)
    }

    /// Applies MessagePack-CSharp Lz4BlockArray (extension 98) transport framing.
    /// Compressed bytes may differ between conforming LZ4 encoders; decoded
    /// envelope bytes remain identical. Existing C# consumers accept this frame.
    ///
    /// # Errors
    /// Propagates invalid frame or MessagePack encoding failure.
    pub fn encode(&self) -> Result<Vec<u8>, &'static str> {
        super::codec::compress(self.content()?)
    }
}
