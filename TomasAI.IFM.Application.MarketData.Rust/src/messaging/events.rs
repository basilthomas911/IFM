//! Native construction of existing futures raw-tick and statistics event DTOs.
//! Inputs are owned provider records and immutable API identity. Publication
//! returns detached MessagePack values; it cannot call or retain managed code.

use super::{codec, wire::guid};
use crate::{
    aggregation::session::Snapshot,
    dataset::{
        live::Registration,
        records::{Quote, Trade},
        transport::Lane,
    },
    latest::Asset,
};
use rmpv::Value;
use time::Date;

/// Qualified immutable identity for one instrument's existing event contracts.
pub struct Instrument {
    registration: Registration,
    lane: Lane,
    date: Date,
    definition_date: Date,
    entity: String,
}

/// Explicit per-event identities and source processing time. Native production
/// callers generate identities; fixture callers inject the C# oracle identities.
#[derive(Clone, Copy)]
pub struct Identity {
    /// Event Guid in .NET ToByteArray order.
    pub event: [u8; 16],
    /// Command/correlation Guid in .NET ToByteArray order.
    pub command: [u8; 16],
    /// Source-time API UTC ticks since Unix epoch, not a later drain wall clock.
    pub observed: i64,
}

impl Identity {
    /// Creates fresh nonempty native correlation identities at source time.
    pub fn fresh(observed: i64) -> Self {
        fn identifier() -> [u8; 16] {
            let mut value = rand::random::<[u8; 16]>();
            // Guid.ToByteArray stores Data3 little-endian; the UUID version
            // nibble therefore lives in byte 7. Preserve RFC 4122 variant bits.
            value[7] = (value[7] & 0x0f) | 0x40;
            value[8] = (value[8] & 0x3f) | 0x80;
            value
        }
        Self {
            event: identifier(),
            command: identifier(),
            observed,
        }
    }
}

fn day(date: Date) -> Value {
    (date.to_julian_day() - 1_721_426).into()
}
fn stamp(date: Date) -> String {
    format!(
        "{:04}{:02}{:02}",
        date.year(),
        date.month() as u8,
        date.day()
    )
}
fn escape(value: &str) -> String {
    let mut output = String::new();
    for byte in value.bytes() {
        if byte.is_ascii_alphanumeric() || matches!(byte, b'-' | b'_' | b'.' | b'~') {
            output.push(char::from(byte));
        } else {
            output.push_str(&format!("%{byte:02X}"));
        }
    }
    output
}
impl Instrument {
    /// Validates canonical mapping and API dates before retaining event identity.
    ///
    /// # Errors
    /// Rejects unqualified sources and unsupported event identity/date ranges.
    pub fn new(
        registration: Registration,
        lane: Lane,
        date: Date,
        definition_date: Date,
    ) -> Result<Self, &'static str> {
        if registration.contract.trim().is_empty()
            || registration.contract.len() > 1024
            || registration.instrument == 0
            || registration.publisher == 0
            || !(1..=9999).contains(&date.year())
            || !(1..=9999).contains(&definition_date.year())
        {
            return Err("Unqualified event identity");
        }
        let asset = if registration.asset == Asset::Futures {
            1
        } else {
            2
        };
        let entity = format!("{asset}:{}:{}", stamp(date), escape(&registration.contract));
        Ok(Self {
            registration,
            lane,
            date,
            definition_date,
            entity,
        })
    }
    fn asset(&self) -> Value {
        if self.registration.asset == Asset::Futures {
            1.into()
        } else {
            2.into()
        }
    }
    fn tick_entity(&self) -> Value {
        Value::Array(vec![
            self.registration.contract.clone().into(),
            day(self.date),
            self.asset(),
        ])
    }
    fn common(
        &self,
        actor: &str,
        verb: &str,
        entity: Value,
        key: &str,
        identity: Identity,
    ) -> Result<Vec<Value>, &'static str> {
        Ok(vec![
            Value::Array(vec![7.into(), actor.into(), verb.into(), key.into()]),
            guid(identity.event).into(),
            entity,
            0.into(),
            guid(identity.command).into(),
            key.into(),
            "TickAggregationService".into(),
            codec::timestamp(identity.observed)?,
        ])
    }
    fn tick(
        &self,
        actor: &str,
        verb: &str,
        identity: Identity,
        sequence: i64,
    ) -> Result<Vec<Value>, &'static str> {
        if sequence <= 0 {
            return Err("Invalid event sequence");
        }
        let mut fields = self.common(actor, verb, self.tick_entity(), &self.entity, identity)?;
        fields.extend([
            1.into(),
            Value::Array(vec![
                self.registration.contract.clone().into(),
                day(self.date),
                sequence.into(),
                codec::timestamp(identity.observed)?,
            ]),
            self.asset(),
            self.lane.dataset().into(),
            day(self.definition_date),
            self.registration.publisher.into(),
            self.registration.instrument.into(),
        ]);
        Ok(fields)
    }
    pub(crate) fn source(&self, instrument: u32, publisher: u16) -> Result<(), &'static str> {
        if instrument != self.registration.instrument
            || !(publisher == self.registration.publisher
                || self.lane == Lane::FuturesXcbf
                    && matches!(
                        (publisher, self.registration.publisher),
                        (105, 106) | (106, 105)
                    ))
        {
            return Err("Event source identity changed");
        }
        Ok(())
    }
    /// Constructs the existing raw futures trade event, preserving actual source
    /// publisher rather than substituting the definition's publisher.
    ///
    /// # Errors
    /// Rejects wrong instrument/publisher, sequence or invalid time.
    pub fn trade(
        &self,
        source: Trade,
        identity: Identity,
        sequence: i64,
    ) -> Result<Value, &'static str> {
        if self.registration.asset != Asset::Futures {
            return Err("Option evidence requires option context");
        }
        self.source(source.header.instrument, source.header.publisher)?;
        let mut fields = self.tick(
            "TickAggregationRealtime",
            "FuturesTickTradeDataChanged",
            identity,
            sequence,
        )?;
        fields[13] = source.header.publisher.into();
        fields.extend([raw_trade(source), Value::Nil]);
        Ok(Value::Array(fields))
    }
    /// Existing individual-option quote/risk actor event. A quote is represented
    /// by the explicit QuoteMidpoint observation, never as traded volume. The
    /// snapshot is the already qualified native 33-key immutable read model.
    ///
    /// # Errors
    /// Wrong asset, source/snapshot identity or malformed timestamp is rejected.
    pub fn option_quote(
        &self,
        source: crate::option_chain::qualification::Quote,
        snapshot: Value,
        underlying: &str,
        identity: Identity,
    ) -> Result<Value, &'static str> {
        if self.registration.asset != Asset::Option
            || self.lane != Lane::OptionsGlbx
            || underlying.is_empty()
            || underlying.len() > 1024
        {
            return Err("Invalid individual option observation identity");
        }
        let value = snapshot
            .as_array()
            .ok_or("Invalid native option snapshot")?;
        if value.len() != 33
            || value[0].as_str() != Some(&self.registration.contract)
            || value[1] != day(self.date)
            || value[2].as_i64() != Some(source.sequence)
        {
            return Err("Option event and snapshot source disagree");
        }
        let mut fields = self.tick(
            "TickAggregationRealtime",
            "FuturesTickTradeDataChanged",
            identity,
            source.sequence,
        )?;
        fields[4] = guid(identity.event).into();
        fields[6] = "IndividualOptionObservationPublisher".into();
        fields[9] = Value::Array(vec![
            self.registration.contract.clone().into(),
            day(self.date),
            source.sequence.into(),
            codec::timestamp(source.event)?,
        ]);
        fields.extend([
            Value::Array(vec![
                0.into(),
                0.into(),
                0.into(),
                0.into(),
                0.into(),
                "0".into(),
                0.into(),
                0.into(),
                0.into(),
                0.into(),
            ]),
            Value::Array(vec![
                underlying.into(),
                2.into(),
                snapshot,
                source.sequence.into(),
                codec::timestamp(source.event)?,
            ]),
        ]);
        Ok(Value::Array(fields))
    }
    /// Constructs a bounded raw quote batch. The caller owns emission order and
    /// commits its sequence only after successful bounded publisher admission.
    ///
    /// # Errors
    /// Rejects empty/oversized batches, unknown reason or changed source identity.
    pub fn quotes(
        &self,
        sources: &[Quote],
        reason: u8,
        identity: Identity,
        sequence: i64,
    ) -> Result<Value, &'static str> {
        if sources.is_empty() || sources.len() > 4096 || !(1..=6).contains(&reason) {
            return Err("Invalid quote batch");
        }
        for source in sources {
            self.source(source.header.instrument, source.header.publisher)?;
        }
        let mut fields = self.tick(
            "TickAggregationRealtime",
            "FuturesTickQuoteDataChanged",
            identity,
            sequence,
        )?;
        fields.extend([
            reason.into(),
            (sources.len() as u16).into(),
            Value::Array(sources.iter().copied().map(raw_quote).collect()),
        ]);
        Ok(Value::Array(fields))
    }
    /// Constructs authoritative session statistics using their own selected date,
    /// including an official cleared-volume reference date before ValueDate.
    ///
    /// # Errors
    /// Rejects unusable/invalid-date statistics or invalid processing timestamp.
    pub fn statistics(&self, state: Snapshot, identity: Identity) -> Result<Value, &'static str> {
        if !state.has_any() || !(1..=9999).contains(&state.value_date.year()) {
            return Err("Unusable session statistics");
        }
        let entity = Value::Array(vec![
            self.registration.contract.clone().into(),
            day(state.value_date),
        ]);
        let key = format!("{}.{}", self.registration.contract, stamp(state.value_date));
        let mut fields = self.common(
            "TickAggregationRealtime",
            "SessionStatisticsObserved",
            entity,
            &key,
            identity,
        )?;
        fields.push(self.statistics_snapshot(state)?);
        Ok(Value::Array(fields))
    }
    /// Copies the native-selected session statistics without an event wrapper.
    /// Cleared-volume reference dates are retained exactly as in publication.
    ///
    /// # Errors
    /// Invalid or unusable selected statistics.
    pub fn statistics_snapshot(&self, state: Snapshot) -> Result<Value, &'static str> {
        if !state.has_any() || !(1..=9999).contains(&state.value_date.year()) {
            return Err("Unusable session statistics");
        }
        Ok(Value::Array(vec![
            self.registration.contract.clone().into(),
            day(state.value_date),
            codec::price(state.open),
            codec::price(state.high),
            codec::price(state.low),
            state.sequence.into(),
            state.event_ns.into(),
            state.volume.into(),
            (state.quality as u8).into(),
        ]))
    }
    /// Copies the exact existing public seven-field market-price snapshot.
    /// Query and streaming paths share this projection; neither a managed price
    /// scale conversion nor a managed aggregation cache is required.
    ///
    /// # Errors
    /// Inconsistent API date, timestamp or CLR decimal overflow.
    pub fn price_snapshot(
        &self,
        snapshot: Option<crate::latest::market::Snapshot>,
    ) -> Result<Value, &'static str> {
        if snapshot.is_some_and(|state| state.date != self.date) {
            return Err("Invalid market-price identity");
        }
        let market = if let Some(snapshot) = snapshot {
            let quote = snapshot
                .quote
                .map(|q| {
                    Ok::<_, &'static str>(Value::Array(vec![
                        q.value.bid.map(codec::price).unwrap_or(Value::Nil),
                        q.value.bid_size.into(),
                        q.value.ask.map(codec::price).unwrap_or(Value::Nil),
                        q.value.ask_size.into(),
                        q.value.bid_count.into(),
                        q.value.ask_count.into(),
                        q.sequence.into(),
                        codec::utc_offset(q.event_ticks)?,
                        codec::utc_offset(q.receive_ticks)?,
                    ]))
                })
                .transpose()?
                .unwrap_or(Value::Nil);
            let trade = snapshot
                .trade
                .map(|t| {
                    Ok::<_, &'static str>(Value::Array(vec![
                        codec::price(t.value.value.price),
                        t.value.value.size.into(),
                        t.sequence.into(),
                        codec::utc_offset(t.event_ticks)?,
                        codec::utc_offset(t.receive_ticks)?,
                        t.value.action.into(),
                        t.value.side.into(),
                        t.value.conditions.into(),
                        guid(t.value.epoch.to_le_bytes()).into(),
                        t.value.ordinal.into(),
                    ]))
                })
                .transpose()?
                .unwrap_or(Value::Nil);
            Value::Array(vec![
                self.registration.contract.clone().into(),
                self.registration.instrument.into(),
                self.registration.publisher.into(),
                self.asset(),
                day(snapshot.date),
                quote,
                trade,
            ])
        } else {
            Value::Array(vec![
                Value::Nil,
                0.into(),
                0.into(),
                0.into(),
                0.into(),
                Value::Nil,
                Value::Nil,
            ])
        };
        Ok(market)
    }
    /// Constructs the existing market-price/VWAP wrapper over coherent native
    /// state. A source trade is nested directly, never re-read from a hot cache.
    ///
    /// # Errors
    /// Rejects invalid source/date/timestamps and CLR decimal overflow.
    pub fn market(
        &self,
        snapshot: Option<crate::latest::market::Snapshot>,
        source: u8,
        checkpoint: Option<crate::aggregation::vwap::Checkpoint>,
        trade: Option<Value>,
        identity: Identity,
    ) -> Result<Value, &'static str> {
        if !(1..=2).contains(&source) || snapshot.is_some_and(|s| s.date != self.date) {
            return Err("Invalid market-price identity");
        }
        let actor = if checkpoint.is_some() {
            "FuturesVwapSignal"
        } else {
            "FuturesMarketPrice"
        };
        let mut fields =
            self.common(actor, "Updated", self.tick_entity(), &self.entity, identity)?;
        let market = self.price_snapshot(snapshot)?;
        let checkpoint = checkpoint
            .map(|cp| {
                Ok::<_, &'static str>(Value::Array(vec![
                    guid(cp.epoch.to_le_bytes()).into(),
                    cp.ordinal.into(),
                    cp.sequence.into(),
                    codec::utc_offset(cp.at)?,
                    codec::decimal(cp.price_volume.coefficient as i128, cp.price_volume.scale)?,
                    cp.volume.into(),
                    cp.eligible.into(),
                    cp.rejected.into(),
                    codec::decimal(cp.last_price.coefficient as i128, cp.last_price.scale)?,
                    cp.valid.into(),
                    cp.replay_complete.into(),
                    guid(cp.generation.to_le_bytes()).into(),
                    1.into(),
                    (cp.reason as u8).into(),
                ]))
            })
            .transpose()?
            .unwrap_or(Value::Nil);
        fields.extend([
            2.into(),
            market,
            source.into(),
            checkpoint,
            trade.unwrap_or(Value::Nil),
        ]);
        Ok(Value::Array(fields))
    }
}

/// Exact provider raw trade DTO, without floating point conversion.
pub fn raw_trade(source: Trade) -> Value {
    Value::Array(vec![
        source.header.sequence.into(),
        source.header.event_ns.into(),
        source.header.received_ns.into(),
        source.header.flags.into(),
        source.price.into(),
        codec::price(source.price),
        source.size.into(),
        source.action.into(),
        source.side.into(),
        source.dbn_flags.into(),
    ])
}
/// Exact raw quote DTO; the DBN undefined-price sentinel maps to nullable price.
pub fn raw_quote(source: Quote) -> Value {
    fn price(raw: i64) -> Value {
        if raw == i64::MAX {
            Value::Nil
        } else {
            codec::price(raw)
        }
    }
    Value::Array(vec![
        source.header.sequence.into(),
        source.header.event_ns.into(),
        source.header.received_ns.into(),
        source.header.flags.into(),
        source.bid.into(),
        price(source.bid),
        source.bid_size.into(),
        source.bid_count.into(),
        source.ask.into(),
        price(source.ask),
        source.ask_size.into(),
        source.ask_count.into(),
    ])
}
