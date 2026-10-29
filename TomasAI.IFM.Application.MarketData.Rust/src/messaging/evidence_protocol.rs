//! Fixed-schema bounded external evidence IO protocol. Reply parsing never
//! allocates from an untrusted MessagePack array, string or binary length.
use super::{
    codec,
    evidence_outbox::{Outbox, Record},
};
use rmpv::Value;
use std::io::{Cursor, Read};

fn hash(value: &str) -> bool {
    value.len() == 64 && value.bytes().all(|b| b.is_ascii_hexdigit())
}
/// Encodes copied evidence for the external storage endpoint. No native resource
/// identity or callback appears in the request.
///
/// # Errors
/// Malformed receipts/hashes or oversize copied evidence fail before enqueueing.
pub fn request(record: &Record) -> Result<Vec<u8>, &'static str> {
    if record.receipt == 0
        || !hash(&record.identity)
        || !hash(&record.digest)
        || record.bytes.is_empty()
        || record.bytes.len() > 131072
    {
        return Err("Invalid native evidence handoff");
    }
    codec::encode(&Value::Array(vec![
        record.receipt.into(),
        record.identity.clone().into(),
        record.digest.clone().into(),
        Value::Binary(record.bytes.to_vec()),
    ]))
}
fn text(reader: &mut Cursor<&[u8]>, maximum: usize) -> Result<String, &'static str> {
    let length =
        rmp::decode::read_str_len(reader).map_err(|_| "Invalid evidence receipt string")? as usize;
    if length > maximum
        || length
            > reader
                .get_ref()
                .len()
                .saturating_sub(reader.position() as usize)
    {
        return Err("Evidence receipt string capacity exceeded");
    }
    let mut bytes = vec![0; length];
    reader
        .read_exact(&mut bytes)
        .map_err(|_| "Truncated evidence receipt")?;
    String::from_utf8(bytes).map_err(|_| "Invalid evidence receipt UTF-8")
}
/// Applies only a fully validated durable acknowledgment. A transport PONG,
/// no-responder status, malformed body or negative reply cannot authorize trading.
///
/// # Errors
/// Invalid/truncated/trailing data, storage failure and stale receipt are explicit
/// native recovery inputs. No failure consumes or replaces the original attempt.
pub fn acknowledge(bytes: &[u8], outbox: &Outbox) -> Result<(), &'static str> {
    let receipt = decode_receipt(bytes)?;
    if !receipt.persisted {
        return Err("External durable evidence persistence failed");
    }
    outbox.acknowledge(&receipt.identity, &receipt.digest, receipt.receipt)
}
/// Validated copied storage receipt, with no resource ownership.
#[derive(Debug, PartialEq, Eq)]
pub struct Receipt {
    /// Monotonic detached handoff identity.
    pub receipt: u64,
    /// Existing source identity.
    pub identity: String,
    /// Existing immutable economic content digest.
    pub digest: String,
    /// True only after the external durable writer completed.
    pub persisted: bool,
    /// Bounded external failure description, never native cancellation authority.
    pub error: Option<String>,
}
/// Parses the fixed five-key receipt without trusting any allocation length.
///
/// # Errors
/// Invalid schema, bounds, identity, UTF-8 or trailing bytes fail closed.
pub fn decode_receipt(bytes: &[u8]) -> Result<Receipt, &'static str> {
    if bytes.is_empty() || bytes.len() > 8192 {
        return Err("Evidence receipt byte capacity exceeded");
    }
    let mut reader = Cursor::new(bytes);
    if rmp::decode::read_array_len(&mut reader).map_err(|_| "Invalid evidence receipt array")? != 5
    {
        return Err("Invalid evidence receipt field count");
    }
    let receipt: u64 =
        rmp::decode::read_int(&mut reader).map_err(|_| "Invalid evidence receipt identity")?;
    let identity = text(&mut reader, 64)?;
    let digest = text(&mut reader, 64)?;
    let persisted =
        rmp::decode::read_bool(&mut reader).map_err(|_| "Invalid evidence receipt outcome")?;
    let position = reader.position() as usize;
    let error = if bytes.get(position) == Some(&0xc0) {
        reader.set_position(reader.position() + 1);
        None
    } else {
        Some(text(&mut reader, 4096)?)
    };
    if reader.position() != bytes.len() as u64
        || receipt == 0
        || !hash(&identity)
        || !hash(&digest)
        || persisted && error.is_some()
    {
        return Err("Malformed or trailing evidence receipt data");
    }
    Ok(Receipt {
        receipt,
        identity,
        digest,
        persisted,
        error,
    })
}
