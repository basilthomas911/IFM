//! Paged copied-reference I/O protocol. The native dataset owns the request,
//! deadline, socket and cancellation. An external database helper receives bytes
//! only; a stalled read/write can never retain this dataset's resources.
use super::{
    codec,
    publisher::{BrokerSettings, Publisher},
};
use crate::runtime::{Deadline, owner::Child};
use rmpv::Value;
use sha2::{Digest, Sha256};
use std::{
    io::{Cursor, Read},
    sync::Arc,
};
use tokio_util::sync::CancellationToken;
/// Maximum complete detached raw-reference response, independent of page size.
pub const MAXIMUM: usize = 4 * 1024 * 1024;
/// A response page fits the independently bounded broker inbound frame.
pub const PAGE: usize = 4096;
pub(crate) const BROKER_UNAVAILABLE: &str = "Native raw-reference broker unavailable:";
/// Copied response page. It is not a native provider/resource handle.
pub struct Page {
    /// Request identity; responses to retired requests cannot satisfy this one.
    pub id: u64,
    /// Complete immutable response byte length.
    pub total: u32,
    /// Exact page byte offset.
    pub offset: u32,
    /// SHA256 of the complete response, stable across every page.
    pub digest: String,
    /// At most 4096 copied bytes.
    pub bytes: Vec<u8>,
    /// Explicit external-storage failure, absent on a successful page.
    pub error: Option<String>,
}
/// Fixed four-key request, copied directly onto the native NATS connection.
///
/// # Errors
/// Invalid kind, identity, argument capacity or unaligned response offset.
pub fn request(id: u64, kind: u8, args: &[u8], offset: u32) -> Result<Vec<u8>, &'static str> {
    if id == 0
        || !(1..=10).contains(&kind)
        || args.len() > 16 * 1024
        || offset as usize >= MAXIMUM
        || !(offset as usize).is_multiple_of(PAGE)
    {
        return Err("Invalid native raw-reference request");
    }
    codec::encode(&Value::Array(vec![
        id.into(),
        kind.into(),
        Value::Binary(args.to_vec()),
        offset.into(),
    ]))
}
fn text(reader: &mut Cursor<&[u8]>, maximum: usize) -> Result<String, &'static str> {
    let count = rmp::decode::read_str_len(reader).map_err(|_| "Malformed reference text")? as usize;
    if count > maximum
        || count
            > reader
                .get_ref()
                .len()
                .saturating_sub(reader.position() as usize)
    {
        return Err("Reference text capacity exceeded");
    }
    let mut bytes = vec![0; count];
    reader
        .read_exact(&mut bytes)
        .map_err(|_| "Truncated reference text")?;
    String::from_utf8(bytes).map_err(|_| "Invalid reference UTF-8")
}
/// Decodes only fixed-schema bounded fields. Wire lengths are checked before
/// allocation; malformed large arrays/strings/binaries cannot grow the buffer.
///
/// # Errors
/// Every truncation, invalid page bound, trailing field and malformed error.
pub fn decode(bytes: &[u8]) -> Result<Page, &'static str> {
    if bytes.len() > 8192 {
        return Err("Reference page capacity exceeded");
    }
    let mut reader = Cursor::new(bytes);
    if rmp::decode::read_array_len(&mut reader).map_err(|_| "Malformed reference page")? != 6 {
        return Err("Malformed reference page");
    }
    let id: u64 = rmp::decode::read_int(&mut reader).map_err(|_| "Malformed reference identity")?;
    let total: u32 =
        rmp::decode::read_int(&mut reader).map_err(|_| "Malformed reference length")?;
    let offset: u32 =
        rmp::decode::read_int(&mut reader).map_err(|_| "Malformed reference offset")?;
    let digest = text(&mut reader, 64)?;
    let count =
        rmp::decode::read_bin_len(&mut reader).map_err(|_| "Malformed reference bytes")? as usize;
    if id == 0
        || total as usize > MAXIMUM
        || offset > total
        || !(offset as usize).is_multiple_of(PAGE)
        || count > PAGE
        || count
            > reader
                .get_ref()
                .len()
                .saturating_sub(reader.position() as usize)
        || count > (total - offset) as usize
    {
        return Err("Invalid reference page bounds");
    }
    let mut content = vec![0; count];
    reader
        .read_exact(&mut content)
        .map_err(|_| "Truncated reference bytes")?;
    let error = if reader.get_ref().get(reader.position() as usize) == Some(&0xc0) {
        reader.set_position(reader.position() + 1);
        None
    } else {
        Some(text(&mut reader, 4096)?)
    };
    if reader.position() != bytes.len() as u64 {
        return Err("Trailing reference page data");
    }
    if error.is_some() {
        if total != 0 || offset != 0 || count != 0 || !digest.is_empty() {
            return Err("Conflicting reference failure fields");
        }
    } else if digest.len() != 64
        || !digest.bytes().all(|b| b.is_ascii_hexdigit())
        || count != (total as usize - offset as usize).min(PAGE)
    {
        return Err("Invalid complete reference page");
    }
    Ok(Page {
        id,
        total,
        offset,
        digest,
        bytes: content,
        error,
    })
}
/// One sequential reference exchange owned by a bounded operation.
pub(crate) struct Reader {
    broker: BrokerSettings,
    scope: String,
    parent: Arc<Child>,
    exchange: Option<(Publisher, tokio::sync::mpsc::Receiver<Vec<u8>>)>,
}

impl Reader {
    /// Reads one complete immutable response, reusing the connection across calls.
    pub(crate) async fn read(
        &mut self, kind: u8, args: &[u8], stop: &CancellationToken, deadline: Deadline,
    ) -> Result<Vec<u8>, String> {
        let id = rand::random::<u64>().max(1);
        let origin = super::wire::control_origin_json()?;
        let mut first = Some(request(id, kind, args, 0)?);
        if self.exchange.is_none() {
            self.exchange = Some(Publisher::start_evidence_exchange(
                self.broker.clone(), self.scope.clone(), 1, 32 * 1024, deadline, &self.parent,
            )?);
        }
        let (publisher, replies) = self.exchange.as_mut().expect("reference exchange initialized");
        let mut progress = publisher.changes();
        loop {
            let state = progress.borrow_and_update().clone();
            if state.failure.is_some() || state.stopped {
                return Err(format!(
                    "{BROKER_UNAVAILABLE} {}",
                    state
                        .detail
                        .as_deref()
                        .or(state.failure)
                        .unwrap_or("transport stopped")
                ));
            }
            if state.connected {
                break;
            }
            deadline
                .run(stop, progress.changed())
                .await
                .map_err(|_| "Raw-reference original deadline expired")?
                .map_err(|_| "Raw-reference broker status closed")?;
        }
        let mut payload = Vec::new();
        let mut expected: Option<(u32, String)> = None;
        loop {
            let packet = if let Some(first) = first.take() {
                first
            } else {
                request(id, kind, args, payload.len() as u32)?
            };
            publisher.try_raw_reference(packet, &origin)?;
            let page = loop {
                let bytes = deadline
                    .run(stop, replies.recv())
                    .await
                    .map_err(|_| "Raw-reference original deadline expired")?
                    .ok_or("Raw-reference reply stream closed")?;
                let page = decode(&bytes)?;
                if page.id == id {
                    break page;
                }
            };
            if let Some(error) = page.error {
                return Err(format!("Raw-reference storage failed: {error}"));
            }
            if page.offset as usize != payload.len() {
                return Err("Raw-reference page ordering conflict".into());
            }
            if let Some((total, digest)) = &expected {
                if *total != page.total || *digest != page.digest {
                    return Err("Raw-reference snapshot changed during paging".into());
                }
            } else {
                payload.reserve_exact(page.total as usize);
                expected = Some((page.total, page.digest));
            }
            payload.extend_from_slice(&page.bytes);
            let (total, digest) = expected.as_ref().unwrap();
            if payload.len() == *total as usize {
                if format!("{:X}", Sha256::digest(&payload)) != digest.to_uppercase() {
                    return Err("Raw-reference content digest mismatch".into());
                }
                break;
            }
        }
        Ok(payload)
    }
}

/// Runs sequential raw-reference requests on one lazily opened connection.
/// Every request retains its own correlation ID and the caller's original budget.
pub(crate) async fn with_reader<T>(
    broker: BrokerSettings,
    scope: String,
    parent: Arc<Child>,
    deadline: Deadline,
    work: impl AsyncFnOnce(&mut Reader) -> Result<T, String>,
) -> Result<T, String> {
    let mut reader = Reader { broker, scope, parent, exchange: None };
    let result = work(&mut reader).await;
    // Join the connection even when a later page fails qualification.
    let joined = match reader.exchange.as_mut() {
        Some((publisher, _)) => publisher.shutdown(deadline).await,
        None => Ok(()),
    };
    match (result, joined) {
        (Ok(value), Ok(())) => Ok(value),
        (Err(error), _) => Err(error),
        (Ok(_), Err(error)) => Err(error.into()),
    }
}

/// Reads one complete immutable reference response under the original native
/// dataset deadline, joining its socket before returning copied bytes.
///
/// # Errors
/// Cancellation, source/storage error, conflict, bounds or original expiry.
pub async fn read(
    broker: BrokerSettings,
    scope: String,
    parent: Arc<Child>,
    kind: u8,
    args: &[u8],
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Vec<u8>, String> {
    with_reader(broker, scope, parent, deadline, async |reader| {
        reader.read(kind, args, stop, deadline).await
    }).await
}
