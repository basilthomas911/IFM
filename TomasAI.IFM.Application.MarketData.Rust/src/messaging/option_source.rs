//! Exact source-time option trade contract and persistence identities. This
//! module owns copied values only: evidence can outlive a dataset without keeping
//! its connection, scope, cancellation token or task alive.

use super::{codec, wire::guid};
use crate::{
    option_chain::qualification::Decimal,
    reference::semantic::{self, Value as Json},
};
use rmpv::Value;
use time::Date;

/// Existing twelve-key OptionTradeSource contract, including original provider
/// nanoseconds. Receive time and generation may change on retransmission without
/// changing the durable economic source identity.
#[derive(Clone)]
pub struct Source {
    /// Provider dataset, for example GLBX.MDP3.
    pub dataset: String,
    /// Provider publisher identity.
    pub publisher: u16,
    /// Provider instrument identity.
    pub instrument: u32,
    /// Application contract identity.
    pub contract: String,
    /// API-controlled trading value date.
    pub date: Date,
    /// Exact trade price in CLR decimal representation.
    pub price: Decimal,
    /// Original trade size.
    pub size: u32,
    /// Original source sequence.
    pub sequence: i64,
    /// Original event nanoseconds since Unix epoch.
    pub event: i64,
    /// Original receive nanoseconds since Unix epoch.
    pub received: i64,
    /// Source generation in CLR Guid byte order.
    pub generation: [u8; 16],
    /// Original provider symbol.
    pub symbol: String,
}
impl Source {
    fn bounded(&self) -> Result<(), &'static str> {
        if [&self.dataset, &self.contract, &self.symbol]
            .iter()
            .any(|s| s.len() > 4096)
            || !(1..=9999).contains(&self.date.year())
        {
            return Err("Option source identity exceeds contract bounds");
        }
        Ok(())
    }
    fn date_text(&self) -> String {
        format!(
            "{:04}-{:02}-{:02}",
            self.date.year(),
            self.date.month() as u8,
            self.date.day()
        )
    }
    /// Existing stable persistence key, with receive time and generation excluded.
    ///
    /// # Errors
    /// Rejects identities outside bounded wire and CLR date representations.
    pub fn identity(&self) -> Result<String, &'static str> {
        self.bounded()?;
        semantic::compute(&Json::Object(vec![
            ("Dataset".into(), Json::String(self.dataset.clone())),
            (
                "PublisherId".into(),
                Json::Unsigned(u64::from(self.publisher)),
            ),
            (
                "InstrumentId".into(),
                Json::Unsigned(u64::from(self.instrument)),
            ),
            ("ContractId".into(), Json::String(self.contract.clone())),
            ("ValueDate".into(), Json::String(self.date_text())),
            ("Sequence".into(), Json::Integer(self.sequence)),
            ("EventNanoseconds".into(), Json::Integer(self.event)),
        ]))
    }
    /// Existing immutable economic content check for idempotent persistence.
    ///
    /// # Errors
    /// Rejects invalid decimal bits or malformed source identity.
    pub fn source_digest(&self) -> Result<String, &'static str> {
        semantic::compute(&Json::Object(vec![
            ("Identity".into(), Json::String(self.identity()?)),
            ("Price".into(), Json::Decimal(self.price)),
            ("Size".into(), Json::Unsigned(u64::from(self.size))),
            ("RawSymbol".into(), Json::String(self.symbol.clone())),
        ]))
    }
    /// Plain inner MessagePack DTO. The enclosing publication owns compression.
    ///
    /// # Errors
    /// Rejects invalid exact decimals or oversized source fields.
    pub fn value(&self) -> Result<Value, &'static str> {
        self.bounded()?;
        Ok(Value::Array(vec![
            self.dataset.clone().into(),
            self.publisher.into(),
            self.instrument.into(),
            self.contract.clone().into(),
            (self.date.to_julian_day() - 1_721_426).into(),
            codec::decimal_bits(self.price)?,
            self.size.into(),
            self.sequence.into(),
            self.event.into(),
            self.received.into(),
            guid(self.generation).into(),
            self.symbol.clone().into(),
        ]))
    }
    /// Full typed JSON provenance, including computed source properties. The
    /// current CLR serializer includes IgnoreMember properties in JSON hashes.
    ///
    /// # Errors
    /// Rejects invalid scalar data before computing downstream evidence identity.
    pub fn semantic(&self) -> Result<Json, &'static str> {
        Ok(Json::Object(vec![
            ("Dataset".into(), Json::String(self.dataset.clone())),
            (
                "PublisherId".into(),
                Json::Unsigned(u64::from(self.publisher)),
            ),
            (
                "InstrumentId".into(),
                Json::Unsigned(u64::from(self.instrument)),
            ),
            ("ContractId".into(), Json::String(self.contract.clone())),
            ("ValueDate".into(), Json::String(self.date_text())),
            ("Price".into(), Json::Decimal(self.price)),
            ("Size".into(), Json::Unsigned(u64::from(self.size))),
            ("Sequence".into(), Json::Integer(self.sequence)),
            ("EventNanoseconds".into(), Json::Integer(self.event)),
            ("ReceiveNanoseconds".into(), Json::Integer(self.received)),
            ("GenerationId".into(), Json::String(guid(self.generation))),
            ("RawSymbol".into(), Json::String(self.symbol.clone())),
            ("Identity".into(), Json::String(self.identity()?)),
            ("SourceDigest".into(), Json::String(self.source_digest()?)),
        ]))
    }
}
