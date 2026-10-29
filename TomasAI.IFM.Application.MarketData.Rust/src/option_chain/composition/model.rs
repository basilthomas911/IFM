//! Existing composition DTOs and semantic identities, generated natively.
use crate::{
    messaging::{
        codec,
        option_evidence::{Greeks, Quote},
        wire::guid,
    },
    option_chain::{context::Context, qualification::Decimal, selection::Selection},
    reference::semantic::{self, Value as Json},
};
use rmpv::Value;
use std::sync::Arc;
fn optional<T>(
    value: Option<&T>,
    encode: impl FnOnce(&T) -> Result<Value, &'static str>,
) -> Result<Value, &'static str> {
    value.map_or(Ok(Value::Nil), encode)
}
fn instant(value: Option<i64>) -> Result<Value, &'static str> {
    value.map_or(Ok(Value::Nil), codec::utc_offset)
}
fn json_instant(value: Option<i64>) -> Result<Json, &'static str> {
    value.map_or(Ok(Json::Null), semantic::utc)
}
/// Exact native outright-futures reference definition; options never fabricate it.
#[derive(Clone)]
pub struct Future {
    /// Canonical contract.
    pub contract: String,
    /// Product root.
    pub root: String,
    /// Provider dataset.
    pub dataset: String,
    /// Exchange.
    pub exchange: String,
    /// Currency.
    pub currency: String,
    /// UTC last-trading ticks.
    pub last_trading: i64,
    /// Exact contract multiplier.
    pub multiplier: Decimal,
    /// Exact tick size.
    pub tick: Decimal,
    /// Verified reference digest.
    pub digest: String,
}
impl Future {
    fn value(&self) -> Result<Value, &'static str> {
        Ok(Value::Array(vec![
            self.contract.clone().into(),
            self.root.clone().into(),
            self.dataset.clone().into(),
            self.exchange.clone().into(),
            self.currency.clone().into(),
            codec::utc_offset(self.last_trading)?,
            codec::decimal_bits(self.multiplier)?,
            codec::decimal_bits(self.tick)?,
            self.digest.clone().into(),
        ]))
    }
    /// Exact existing typed future-reference identity for a complete source page.
    ///
    /// # Errors
    /// An invalid CLR timestamp cannot become a semantic identity.
    pub fn semantic(&self) -> Result<Json, &'static str> {
        Ok(Json::Object(vec![
            ("ContractId".into(), Json::String(self.contract.clone())),
            ("Root".into(), Json::String(self.root.clone())),
            ("Dataset".into(), Json::String(self.dataset.clone())),
            ("Exchange".into(), Json::String(self.exchange.clone())),
            ("Currency".into(), Json::String(self.currency.clone())),
            ("LastTradingUtc".into(), semantic::utc(self.last_trading)?),
            ("Multiplier".into(), Json::Decimal(self.multiplier)),
            ("TickSize".into(), Json::Decimal(self.tick)),
            ("DefinitionDigest".into(), Json::String(self.digest.clone())),
        ]))
    }
}
/// Exact sixteen-key composition source item. Missing fields remain unavailable;
/// no absent Greek, quote, statistic or future is replaced with a numeric zero.
#[derive(Clone)]
pub struct Instrument {
    /// Canonical contract ID.
    pub contract: String,
    /// Current qualified source quote when available.
    pub quote: Option<Quote>,
    /// Full immutable option context.
    pub context: Option<Arc<Context>>,
    /// Exact option strike.
    pub strike: Option<Decimal>,
    /// Call/put side.
    pub call: Option<bool>,
    /// Exact captured underlying quote.
    pub underlying: Option<Quote>,
    /// Futures-only metadata.
    pub future: Option<Future>,
    /// Selection tier with original IV provenance.
    pub selection: Option<Selection>,
    /// Official/replay session volume.
    pub volume: Option<i64>,
    /// Official open interest.
    pub interest: Option<i64>,
    /// Statistics source UTC ticks.
    pub statistics_at: Option<i64>,
    /// Reason a browsing quote was suppressed.
    pub unavailable: Option<String>,
    /// Last rejected quote event UTC ticks.
    pub last_event: Option<i64>,
    /// Last rejected quote receive UTC ticks.
    pub last_received: Option<i64>,
    /// Source volume CLR day number.
    pub volume_date: Option<i32>,
    /// Source open-interest CLR day number.
    pub interest_date: Option<i32>,
}
impl Instrument {
    /// Existing plain numeric-key DTO.
    ///
    /// # Errors
    /// Malformed exact scalars or timestamps cannot become a snapshot.
    pub fn value(&self) -> Result<Value, &'static str> {
        Ok(Value::Array(vec![
            self.contract.clone().into(),
            optional(self.quote.as_ref(), Quote::value)?,
            optional(self.context.as_deref(), Context::value)?,
            self.strike.map_or(Ok(Value::Nil), codec::decimal_bits)?,
            self.call.map_or(Value::Nil, Value::from),
            optional(self.underlying.as_ref(), Quote::value)?,
            optional(self.future.as_ref(), Future::value)?,
            optional(self.selection.as_ref(), Selection::value)?,
            self.volume.map_or(Value::Nil, Value::from),
            self.interest.map_or(Value::Nil, Value::from),
            instant(self.statistics_at)?,
            self.unavailable.clone().map_or(Value::Nil, Value::from),
            instant(self.last_event)?,
            instant(self.last_received)?,
            self.volume_date.map_or(Value::Nil, Value::from),
            self.interest_date.map_or(Value::Nil, Value::from),
        ]))
    }
    /// Typed canonical semantics used by the existing complete-snapshot digest.
    ///
    /// # Errors
    /// Unrepresentable typed values fail before hashing.
    pub fn semantic(&self) -> Result<Json, &'static str> {
        let date = |d: Option<i32>| -> Result<Json, &'static str> {
            let Some(d) = d else {
                return Ok(Json::Null);
            };
            let d = time::Date::from_julian_day(
                d.checked_add(1721426).ok_or("Invalid statistics date")?,
            )
            .map_err(|_| "Invalid statistics date")?;
            Ok(Json::String(format!(
                "{:04}-{:02}-{:02}",
                d.year(),
                d.month() as u8,
                d.day()
            )))
        };
        let mut fields = vec![
            ("ContractId".into(), Json::String(self.contract.clone())),
            (
                "Quote".into(),
                self.quote
                    .as_ref()
                    .map_or(Ok(Json::Null), Quote::semantic)?,
            ),
            (
                "Pricing".into(),
                self.context
                    .as_ref()
                    .map_or(Ok(Json::Null), |c| c.semantic())?,
            ),
            (
                "Strike".into(),
                self.strike.map_or(Json::Null, Json::Decimal),
            ),
            ("IsCall".into(), self.call.map_or(Json::Null, Json::Bool)),
            (
                "Underlying".into(),
                self.underlying
                    .as_ref()
                    .map_or(Ok(Json::Null), Quote::semantic)?,
            ),
            (
                "FutureDefinition".into(),
                self.future
                    .as_ref()
                    .map_or(Ok(Json::Null), Future::semantic)?,
            ),
            (
                "SessionVolume".into(),
                self.volume.map_or(Json::Null, Json::Integer),
            ),
            (
                "OpenInterest".into(),
                self.interest.map_or(Json::Null, Json::Integer),
            ),
            ("StatisticsAtUtc".into(), json_instant(self.statistics_at)?),
            (
                "QuoteUnavailableReason".into(),
                self.unavailable.clone().map_or(Json::Null, Json::String),
            ),
            ("LastQuoteEventAtUtc".into(), json_instant(self.last_event)?),
            (
                "LastQuoteReceivedAtUtc".into(),
                json_instant(self.last_received)?,
            ),
            ("VolumeValueDate".into(), date(self.volume_date)?),
            ("OpenInterestValueDate".into(), date(self.interest_date)?),
        ];
        if let Some(selection) = &self.selection {
            fields.push(("Selection".into(), selection.semantic()?));
        }
        Ok(Json::Object(fields))
    }
}
/// One qualified instrument plus optional complete risk valuation.
#[derive(Clone)]
pub struct Valued {
    /// Exact prepared source inputs.
    pub instrument: Instrument,
    /// Full Greeks only when qualified; selection tier never manufactures these.
    pub valuation: Option<Greeks>,
}
impl Valued {
    fn value(&self) -> Result<Value, &'static str> {
        Ok(Value::Array(vec![
            self.instrument.value()?,
            optional(self.valuation.as_ref(), Greeks::value)?,
        ]))
    }
    fn semantic(&self) -> Result<Json, &'static str> {
        Ok(Json::Object(vec![
            ("Instrument".into(), self.instrument.semantic()?),
            (
                "Valuation".into(),
                self.valuation
                    .as_ref()
                    .map_or(Ok(Json::Null), Greeks::semantic)?,
            ),
        ]))
    }
}
/// Complete immutable snapshot before strategy-specific ranking metadata.
#[derive(Clone)]
pub struct Snapshot {
    /// Complete native strategy provenance, absent for manual/history captures.
    pub ranking: Option<super::super::strategy::Ranking>,
    /// Existing schema: 1 full risk, 2 selection-tier universe.
    pub schema: i32,
    /// Explicit request snapshot identity.
    pub id: [u8; 16],
    /// Current desired scope name.
    pub scope: String,
    /// Complete source/configuration token.
    pub token: String,
    /// Daily/Weekly/Monthly horizon.
    pub horizon: String,
    /// Native logical generation.
    pub generation: [u8; 16],
    /// One frozen authoritative API valuation time.
    pub at: i64,
    /// Earliest source/context/request expiry.
    pub until: i64,
    /// Canonically ordered complete scope.
    pub instruments: Vec<Valued>,
    /// Existing digest calculated with this field initially empty.
    pub digest: String,
}
impl Snapshot {
    /// Existing fourteen-key snapshot DTO, with absent ranking metadata explicit.
    ///
    /// # Errors
    /// Invalid scalar/timestamp or excessive snapshot bytes fail explicitly.
    pub fn value(&self) -> Result<Value, &'static str> {
        self.value_with(Value::Array(
            self.instruments
                .iter()
                .map(Valued::value)
                .collect::<Result<_, _>>()?,
        ))
    }
    fn value_with(&self, instruments: Value) -> Result<Value, &'static str> {
        Ok(Value::Array(vec![
            self.schema.into(),
            guid(self.id).into(),
            self.scope.clone().into(),
            self.token.clone().into(),
            self.horizon.clone().into(),
            guid(self.generation).into(),
            codec::utc_offset(self.at)?,
            codec::utc_offset(self.until)?,
            instruments,
            self.digest.clone().into(),
            Value::Nil,
            self.ranking
                .as_ref()
                .map(|r| r.bias().map(|b| Value::from(b.name())))
                .transpose()?
                .unwrap_or(Value::Nil),
            self.ranking.as_ref().map_or(Value::Nil, |r| {
                Value::from(r.date.to_julian_day() - 1721426)
            }),
            self.ranking
                .as_ref()
                .map_or(Value::Nil, |r| r.parameters.value()),
        ]))
    }
    /// Exact complete graph for independent C# digest comparison.
    ///
    /// # Errors
    /// Invalid typed provenance fails before hashing.
    pub fn semantic(&self) -> Result<Json, &'static str> {
        let mut fields = vec![
            (
                "SchemaVersion".into(),
                Json::Integer(i64::from(self.schema)),
            ),
            ("SnapshotId".into(), Json::String(guid(self.id))),
            ("ScopeId".into(), Json::String(self.scope.clone())),
            ("ScopeToken".into(), Json::String(self.token.clone())),
            ("Horizon".into(), Json::String(self.horizon.clone())),
            ("GenerationId".into(), Json::String(guid(self.generation))),
            ("EvaluatedAtUtc".into(), semantic::utc(self.at)?),
            ("ValidUntilUtc".into(), semantic::utc(self.until)?),
            (
                "Instruments".into(),
                Json::Array(
                    self.instruments
                        .iter()
                        .map(Valued::semantic)
                        .collect::<Result<_, _>>()?,
                ),
            ),
            ("Digest".into(), Json::String(self.digest.clone())),
            (
                "StrategyOptionChainParameters".into(),
                self.ranking
                    .as_ref()
                    .map_or(Ok(Json::Null), |r| r.parameters.semantic())?,
            ),
        ];
        if let Some(r) = &self.ranking {
            fields.push((
                "StrategyOptionChainBias".into(),
                Json::String(r.bias()?.name().into()),
            ));
            fields.push((
                "StrategyOptionChainValueDate".into(),
                Json::String(r.date.to_string()),
            ));
        }
        Ok(Json::Object(fields))
    }
    /// Seals canonical order, semantic identity and the existing 4 MiB bound.
    ///
    /// # Errors
    /// Rejects invalid identity, duplicates, contracts or a truncated scope.
    pub fn seal(&mut self) -> Result<(), &'static str> {
        if self.id == [0; 16]
            || self.generation == [0; 16]
            || !matches!(self.schema, 1 | 2)
            || self.scope.trim().is_empty()
            || self.scope.encode_utf16().count() > 128
            || self.token.trim().is_empty()
            || self.token.encode_utf16().count() > 128
            || !matches!(self.horizon.as_str(), "Daily" | "Weekly" | "Monthly")
            || self.instruments.len() > 2048
        {
            return Err("Invalid native composition snapshot identity");
        }
        self.instruments.sort_by(|a, b| {
            a.instrument
                .contract
                .encode_utf16()
                .cmp(b.instrument.contract.encode_utf16())
        });
        if self
            .instruments
            .windows(2)
            .any(|pair| pair[0].instrument.contract == pair[1].instrument.contract)
        {
            return Err("Conflicting native composition definition");
        }
        self.digest.clear();
        // Preflight one bounded instrument at a time, before cloning/encoding a
        // complete graph. Large but valid calendars cannot force a giant rejected
        // snapshot allocation merely because the scope has 2,048 contracts.
        let mut measured = codec::encode(&self.value_with(Value::Array(Vec::new()))?)?.len()
            + 65 // Empty digest becomes a 64-byte MessagePack string.
            + if self.instruments.len() >= 16 { 2 } else { 0 };
        for item in &self.instruments {
            measured = measured
                .checked_add(codec::encode(&item.value()?)?.len())
                .ok_or("Native composition size overflow")?;
            if measured > 4 * 1024 * 1024 {
                return Err("Native composition snapshot exceeds 4 MiB");
            }
        }
        self.digest = semantic::compute(&self.semantic()?)?;
        if codec::encode(&self.value()?)?.len() > 4 * 1024 * 1024 {
            return Err("Native composition snapshot exceeds 4 MiB");
        }
        Ok(())
    }
}
