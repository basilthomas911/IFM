//! Existing UI failure event, constructed without a managed feed coordinator.
//! Stable identities deduplicate delivery retries; broker flush does not consume
//! the retained terminal journal or claim that a user saw the error dialog.
use super::{codec, wire::guid};
use crate::{
    dataset::transport::Lane,
    runtime::recovery::{Phase, Terminal},
};
use rmpv::Value;
use sha2::{Digest, Sha256};

/// One detached UI event. It contains no dataset task, lease or cancellation owner.
pub struct Notice {
    /// Existing event subscription subject, shared by both UI implementations.
    pub subject: String,
    /// Stable operation identity in CLR Guid byte order.
    pub identity: [u8; 16],
    /// Plain existing twenty-key event DTO; transport compression is separate.
    pub bytes: Vec<u8>,
}
fn identity(runtime: [u8; 16], lane: Lane, ordinal: u64, kind: u8) -> [u8; 16] {
    let mut hash = Sha256::new();
    hash.update(runtime);
    hash.update([lane as u8, kind]);
    hash.update(ordinal.to_le_bytes());
    let mut result: [u8; 16] = hash.finalize()[..16]
        .try_into()
        .expect("fixed digest length");
    result[7] = (result[7] & 15) | 64;
    result[8] = (result[8] & 63) | 128;
    result
}
impl Notice {
    /// Adds the real audit OperationId while retaining this notice's distinct stable RecordId.
    /// The bytes are generated locally, so decoding cannot consume untrusted wire lengths.
    /// # Errors
    /// Invalid local correlation identity or encoding failure.
    pub fn correlate(
        mut self,
        operation: Option<&str>,
        record: Option<[u8; 16]>,
    ) -> Result<Self, &'static str> {
        if let Some(operation) = operation {
            if operation.len() != 36 {
                return Err("Invalid audit operation identity");
            }
            let mut value = rmpv::decode::read_value(&mut std::io::Cursor::new(&self.bytes))
                .map_err(|_| "Invalid locally generated notice")?;
            let Value::Array(fields) = &mut value else {
                return Err("Invalid locally generated notice fields");
            };
            let message = fields[7].as_str().ok_or("Invalid notice message")?.replace(
                &format!("OperationId: {}.", guid(self.identity)),
                &format!("OperationId: {operation}."),
            );
            fields[5] = operation.into();
            fields[7] = message.into();
            if let Some(record) = record {
                fields[2] = guid(record).into();
                self.identity = record;
            }
            self.bytes = codec::encode(&value)?;
        }
        Ok(self)
    }
    /// Maps an actual failed episode to the current C# UI contract. Logical
    /// owner/generation IDs are explicitly native identities, never invented PIDs.
    ///
    /// # Errors
    /// Nonterminal, malformed, oversized or out-of-range input is rejected.
    pub fn new(
        runtime: [u8; 16],
        lane: Lane,
        date: time::Date,
        terminal: &Terminal,
        at: i64,
    ) -> Result<Self, &'static str> {
        if runtime == [0; 16]
            || terminal.ordinal == 0
            || terminal.status.phase != Phase::Failed
            || !(1..=9999).contains(&date.year())
            || terminal
                .status
                .root_error
                .as_ref()
                .is_some_and(|s| s.len() > 4096)
            || terminal
                .status
                .latest_error
                .as_ref()
                .is_some_and(|s| s.len() > 4096)
        {
            return Err("Invalid native terminal notification");
        }
        let id = identity(runtime, lane, terminal.ordinal, 1);
        let ticket = terminal.status.ticket.parts();
        let date_text = format!(
            "{:04}-{:02}-{:02}",
            date.year(),
            date.month() as u8,
            date.day()
        );
        let detail = terminal
            .status
            .latest_error
            .as_deref()
            .or(terminal.status.root_error.as_deref())
            .unwrap_or("Readiness was not verified.");
        let action = if terminal.resetting {
            "Resetting this dataset now."
        } else {
            "No automatic dataset reset is pending."
        };
        let cause = terminal.status.root_error.as_deref().unwrap_or(detail);
        let message = format!(
            "{}/{}: Dataset recovery failed after 120 seconds. {} Cause: {} Last error: {} ValueDate: {}. OperationId: {}. Live monitoring is not verified.",
            lane.role(),
            lane.dataset(),
            action,
            cause,
            detail,
            date_text,
            guid(id)
        );
        let data = format!(
            "episode={};revision={};generation={};replaced={};resetting={};root={};missing={:?}",
            ticket[0],
            ticket[1],
            ticket[2],
            terminal.replaced,
            terminal.resetting,
            terminal
                .status
                .root_error
                .as_deref()
                .unwrap_or("unspecified"),
            terminal.status.missing
        );
        let subject = format!("Event.MarketDataFeedEvent.DatasetRecoveryFailed.{date_text}");
        let event = Value::Array(vec![
            Value::Array(vec![
                3.into(),
                "MarketDataFeedEvent".into(),
                "DatasetRecoveryFailed".into(),
                date_text.into(),
            ]),
            Value::Array(vec![(date.to_julian_day() - 1721426).into()]),
            guid(id).into(),
            codec::timestamp(at)?,
            0.into(),
            guid(id).into(),
            "RustMarketDataOwner".into(),
            message.into(),
            503.into(),
            6.into(),
            data.into(),
            codec::timestamp(at)?,
            "".into(),
            "".into(),
            "".into(),
            "".into(),
            lane.dataset().into(),
            lane.role().into(),
            guid(identity(runtime, lane, 0, 2)).into(),
            guid(identity(runtime, lane, ticket[2], 3)).into(),
        ]);
        let bytes = codec::encode(&event)?;
        if bytes.len() > 16 * 1024 {
            return Err("Native terminal notification exceeds bound");
        }
        Ok(Self {
            subject,
            identity: id,
            bytes,
        })
    }
}
