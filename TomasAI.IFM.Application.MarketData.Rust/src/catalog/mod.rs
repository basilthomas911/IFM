//! Source-exact instrument definitions. Original provider JSON is retained because
//! current definition identities hash its raw UTF-8, not reserialized properties.
//! Numeric source timestamps/prices are parsed as integers, never through f64.
use std::{collections::BTreeMap, sync::Arc};
pub mod hydration;
pub mod queries;
pub mod repository;
pub mod selection;

pub mod current;
/// Exact parsed definition and current contract-detail projection.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Definition {
    /// Provider dataset scope.
    pub dataset: String,
    /// Unmodified JSON line, without its transport newline.
    pub json: Arc<str>,
    /// Publisher identity.
    pub publisher: u16,
    /// Instrument identity.
    pub instrument: u32,
    /// Provider symbol.
    pub symbol: String,
    /// Product/ticker code.
    pub asset: String,
    /// Original class (including unsupported classes retained for persistence).
    pub class: String,
    /// Contract kind: futures 1, call 2, put 3, unsupported 0.
    pub kind: u8,
    /// Currency code.
    pub currency: String,
    /// Venue code.
    pub exchange: String,
    /// Settlement currency.
    pub settlement_currency: String,
    /// Security type.
    pub security_type: String,
    /// Classification code.
    pub cfi: String,
    /// Unit of measure.
    pub unit: String,
    /// Underlying symbol.
    pub underlying: String,
    /// Exact underlying provider identity.
    pub underlying_instrument: u32,
    /// Received timestamp in unsigned Unix nanoseconds.
    pub received_ns: u64,
    /// Event timestamp in unsigned Unix nanoseconds.
    pub event_ns: u64,
    /// Delete record flag.
    pub deleted: bool,
    /// Optional activation Unix nanoseconds.
    pub activation: Option<u64>,
    /// Optional expiry Unix nanoseconds.
    pub expiration: Option<u64>,
    /// UTC maturity from expiry, otherwise valid provider year/month/day fields.
    pub maturity: Option<time::Date>,
    /// Live query projection prefers explicit maturity fields, matching the
    /// current native-detail converter. Stored summary maturity remains separate.
    pub query_maturity: Option<time::Date>,
    /// Current DBN bridge marks year-zero calendar dates present, which the CLR
    /// date converter rejects. Keep that query failure distinct from source load.
    pub query_maturity_invalid: bool,
    /// Exact strike in 1e-9 units; undefined sentinel remains absent.
    pub strike: Option<i64>,
    /// Exact tick price increment.
    pub tick: Option<i64>,
    /// Exact tick currency amount.
    pub tick_amount: Option<i64>,
    /// Optional contract multiplier.
    pub multiplier: Option<i32>,
    /// Optional maturity week.
    pub maturity_week: Option<u8>,
}
/// Explicit malformed-definition and bounded-catalog failures.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Failure {
    /// Required identity, JSON field or integer representation is invalid.
    InvalidDefinition,
    /// Configured row or raw-JSON budget would be exceeded.
    Capacity,
}
type RawFields<'a> = BTreeMap<String, &'a serde_json::value::RawValue>;
type Fields = BTreeMap<&'static str, String>;
fn project(raw: &RawFields<'_>, names: &[&'static str]) -> Result<Fields, Failure> {
    names
        .iter()
        .map(|name| {
            let value = match raw.get(*name).map(|v| v.get()) {
                None | Some("null") => String::new(),
                Some("true") => "True".into(),
                Some("false") => "False".into(),
                Some(v) if v.starts_with('"') => {
                    serde_json::from_str(v).map_err(|_| Failure::InvalidDefinition)?
                }
                Some(v) => v.into(),
            };
            Ok((*name, value))
        })
        .collect()
}
fn text(root: &Fields, name: &str) -> String {
    root.get(name).cloned().unwrap_or_default()
}
fn valid_depth(json: &str) -> bool {
    let (mut depth, mut quoted, mut escaped) = (0usize, false, false);
    for byte in json.bytes() {
        if quoted {
            if escaped {
                escaped = false
            } else if byte == b'\\' {
                escaped = true
            } else if byte == b'"' {
                quoted = false
            }
        } else {
            match byte {
                b'"' => quoted = true,
                b'{' | b'[' => {
                    depth += 1;
                    if depth > 64 {
                        return false;
                    }
                }
                b'}' | b']' => {
                    let Some(next) = depth.checked_sub(1) else {
                        return false;
                    };
                    depth = next
                }
                _ => {}
            }
        }
    }
    depth == 0 && !quoted
}
// Match CLR NumberStyles.Integer, including its negative-zero and trailing-NUL
// handling. Rust Unicode trim/unsigned parsing has different accepted inputs.
fn integer_text(value: &str) -> &str {
    value
        .trim_end_matches('\0')
        .trim_matches(|ch| ch == ' ' || ('\t'..='\r').contains(&ch))
}
fn unsigned_text(value: &str) -> Option<u64> {
    let value = integer_text(value);
    let negative = value.starts_with('-');
    let digits = value.strip_prefix(['-', '+']).unwrap_or(value);
    if digits.is_empty() || !digits.bytes().all(|b| b.is_ascii_digit()) {
        return None;
    }
    let parsed = digits.parse().ok()?;
    if negative && parsed != 0 {
        None
    } else {
        Some(parsed)
    }
}
fn signed<T: std::str::FromStr>(value: &str) -> Option<T> {
    integer_text(value).parse().ok()
}
fn unsigned(root: &Fields, name: &str) -> Result<u64, Failure> {
    unsigned_text(&text(root, name)).ok_or(Failure::InvalidDefinition)
}
fn timestamp(root: &Fields, name: &str) -> Option<u64> {
    unsigned_text(&text(root, name)).filter(|v| *v != u64::MAX)
}
fn price(root: &Fields, name: &str) -> Option<i64> {
    signed::<i64>(&text(root, name)).filter(|v| *v != i64::MAX)
}
impl Definition {
    /// Preserves the current ExactInstrumentDefinition.Parse projection and raw
    /// JSON identity. All additional fields remain available in json unchanged.
    pub fn parse(dataset: &str, json: &str) -> Result<Self, Failure> {
        if json.len() > 65_536 || dataset.is_empty() || dataset.len() > 64 {
            return Err(Failure::InvalidDefinition);
        }
        if !valid_depth(json) {
            return Err(Failure::InvalidDefinition);
        }
        // Raw values preserve JsonElement.ToString lexical identity without
        // converting unknown/large numeric fields through binary floating point.
        let source: RawFields<'_> =
            serde_json::from_str(json).map_err(|_| Failure::InvalidDefinition)?;
        let raw_header: RawFields<'_> =
            serde_json::from_str(source.get("hd").ok_or(Failure::InvalidDefinition)?.get())
                .map_err(|_| Failure::InvalidDefinition)?;
        let get = |name| {
            raw_header
                .get(name)
                .map(|v| v.get())
                .ok_or(Failure::InvalidDefinition)
        };
        if serde_json::from_str::<i32>(get("rtype")?).map_err(|_| Failure::InvalidDefinition)? != 19
        {
            return Err(Failure::InvalidDefinition);
        }
        let publisher = serde_json::from_str::<u16>(get("publisher_id")?)
            .map_err(|_| Failure::InvalidDefinition)?;
        let instrument = serde_json::from_str::<u32>(get("instrument_id")?)
            .map_err(|_| Failure::InvalidDefinition)?;
        if instrument == 0 {
            return Err(Failure::InvalidDefinition);
        }
        let header_fields = project(&raw_header, &["ts_event"])?;
        let root = project(
            &source,
            &[
                "raw_symbol",
                "asset",
                "instrument_class",
                "currency",
                "exchange",
                "settl_currency",
                "security_type",
                "cfi",
                "unit_of_measure",
                "underlying",
                "underlying_id",
                "ts_recv",
                "security_update_action",
                "activation",
                "expiration",
                "strike_price",
                "min_price_increment",
                "min_price_increment_amount",
                "contract_multiplier",
                "maturity_week",
                "maturity_year",
                "maturity_month",
                "maturity_day",
            ],
        )?;
        let symbol = text(&root, "raw_symbol");
        if symbol.trim().is_empty() {
            return Err(Failure::InvalidDefinition);
        }
        let class = text(&root, "instrument_class");
        let kind = match class.as_str() {
            "F" => 1,
            "C" => 2,
            "P" => 3,
            _ => 0,
        };
        let expiration = timestamp(&root, "expiration");
        let expiration_date = expiration
            .and_then(|ns| {
                time::OffsetDateTime::from_unix_timestamp((ns / 1_000_000_000) as i64).ok()
            })
            .map(|v| v.date());
        let explicit_maturity = (|| {
            let year = signed::<i32>(&text(&root, "maturity_year"))?;
            if !(1..=9999).contains(&year) {
                return None;
            }
            let month = signed::<u8>(&text(&root, "maturity_month"))
                .and_then(|v| time::Month::try_from(v).ok())?;
            let day = signed::<u8>(&text(&root, "maturity_day"))?;
            time::Date::from_calendar_date(year, month, day).ok()
        })();
        let maturity = expiration_date.or(explicit_maturity);
        let query_maturity = explicit_maturity.or(expiration_date);
        let query_maturity_invalid = signed::<u16>(&text(&root, "maturity_year")) == Some(0)
            && signed::<u8>(&text(&root, "maturity_month"))
                .and_then(|m| time::Month::try_from(m).ok())
                .zip(signed::<u8>(&text(&root, "maturity_day")))
                .is_some_and(|(month, day)| time::Date::from_calendar_date(0, month, day).is_ok());
        Ok(Self {
            dataset: dataset.into(),
            json: Arc::from(json),
            publisher,
            instrument,
            symbol,
            asset: text(&root, "asset"),
            class,
            kind,
            currency: text(&root, "currency"),
            exchange: text(&root, "exchange"),
            settlement_currency: text(&root, "settl_currency"),
            security_type: text(&root, "security_type"),
            cfi: text(&root, "cfi"),
            unit: text(&root, "unit_of_measure"),
            underlying: text(&root, "underlying"),
            underlying_instrument: u32::try_from(unsigned(&root, "underlying_id")?)
                .map_err(|_| Failure::InvalidDefinition)?,
            received_ns: unsigned(&root, "ts_recv")?,
            event_ns: unsigned(&header_fields, "ts_event")?,
            deleted: text(&root, "security_update_action") == "D",
            activation: timestamp(&root, "activation"),
            expiration,
            maturity,
            query_maturity,
            query_maturity_invalid,
            strike: price(&root, "strike_price"),
            tick: price(&root, "min_price_increment"),
            tick_amount: price(&root, "min_price_increment_amount"),
            multiplier: signed::<i32>(&text(&root, "contract_multiplier"))
                .filter(|v| *v != i32::MAX),
            maturity_week: unsigned_text(&text(&root, "maturity_week"))
                .and_then(|v| u8::try_from(v).ok())
                .filter(|v| *v != u8::MAX),
        })
    }
}
/// Bounded single-writer cold catalog. Dataset+instrument identifies a record;
/// arrival-order replacement matches current historical contract query behavior.
pub struct Catalog {
    rows: BTreeMap<(String, u32), Arc<Definition>>,
    capacity: usize,
    byte_capacity: usize,
    bytes: usize,
}
impl Catalog {
    /// Creates explicit row/raw-source limits. No expiration horizon is hardcoded.
    pub fn new(capacity: usize, byte_capacity: usize) -> Result<Self, Failure> {
        if capacity == 0 || byte_capacity == 0 {
            Err(Failure::Capacity)
        } else {
            Ok(Self {
                rows: BTreeMap::new(),
                capacity,
                byte_capacity,
                bytes: 0,
            })
        }
    }
    /// Inserts the next source record atomically or preserves the prior catalog on
    /// capacity failure. Raw JSON bytes are charged before retaining the record.
    pub fn insert(&mut self, value: Definition) -> Result<(), Failure> {
        let key = (value.dataset.clone(), value.instrument);
        let prior = self.rows.get(&key).map_or(0, |d| d.json.len());
        let bytes = self
            .bytes
            .checked_sub(prior)
            .and_then(|n| n.checked_add(value.json.len()))
            .ok_or(Failure::Capacity)?;
        if bytes > self.byte_capacity
            || (!self.rows.contains_key(&key) && self.rows.len() >= self.capacity)
        {
            return Err(Failure::Capacity);
        }
        self.rows.insert(key, Arc::new(value));
        self.bytes = bytes;
        Ok(())
    }
    /// Exact provider identity lookup, including delete records for persistence.
    pub fn find(&self, dataset: &str, instrument: u32) -> Option<Arc<Definition>> {
        self.rows.get(&(dataset.to_owned(), instrument)).cloned()
    }
    /// Raw source bytes currently retained (projection/allocator overhead separate).
    pub fn source_bytes(&self) -> usize {
        self.bytes
    }
    /// Enumerates the bounded catalog in provider identity order. Cloned rows
    /// retain immutable source ownership across a later atomic refresh.
    pub fn definitions(&self) -> impl Iterator<Item = Arc<Definition>> + '_ {
        self.rows.values().cloned()
    }
    /// Clears all definitions at an explicit catalog/generation boundary.
    pub fn clear(&mut self) {
        self.rows.clear();
        self.bytes = 0;
    }
}

/// Exact public option read-model projection from immutable provider definitions.
pub mod option_models;
