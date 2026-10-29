//! Raw monitored-option commands. C# copies forty-five existing fields; exact
//! strike translation, review qualification and convention construction are native.
use super::{calendar::Zone, decimal};
use crate::{
    messaging::codec,
    option_chain::{convention::Convention, qualification::Decimal},
};
use rmpv::Value;
/// Bounded copied existing read-model data; no socket, task or reference service.
#[derive(Clone)]
pub struct OptionContract {
    fields: Vec<Value>,
}
/// Compact owned coverage facts; full pricing metadata is deliberately absent.
#[derive(Clone, PartialEq)]
pub struct CoverageRow {
    contract: String,
    root: String,
    underlying: String,
    zone: String,
    strike: Decimal,
    expiration: Option<u64>,
    instrument: u32,
    publisher: u16,
    digest: [u8; 32],
    legacy_strike: u64,
    browser_kind: Option<String>,
    mapping_version: String,
}
impl CoverageRow {
    /// Decodes the compact 24-field projection without retaining full raw graphs.
    ///
    /// # Errors
    /// Invalid shape, source fields or exact scalar representation.
    pub fn from_command(value: &serde_json::Value) -> Result<Self, String> {
        let fields = value.as_array().ok_or("Malformed compact definition")?;
        if fields.len() == 45 {
            return Self::from_model(&OptionContract::from_command(value)?);
        }
        if !matches!(fields.len(), 24 | 25) {
            return Err("Invalid compact definition field count".into());
        }
        let mut full = vec![serde_json::Value::Null; 45];
        for (index, value) in [
            0, 2, 8, 9, 11, 12, 13, 14, 15, 16, 19, 21, 23, 24, 29, 33, 34, 35, 36, 37, 40, 42, 43,
            44,
        ]
        .into_iter()
        .zip(fields)
        {
            full[index] = value.clone();
        }
        if fields.len() == 25 {
            full[10] = fields[24].clone();
        }
        let mut result = Self::from_model(&OptionContract::from_command(
            &serde_json::Value::Array(full),
        )?)?;
        if fields.len() == 24 {
            result.browser_kind = None;
        }
        Ok(result)
    }
    /// Validates copied coverage facts without requiring review of unselected contracts.
    ///
    /// # Errors
    /// Invalid/missing planning fields, underlying source or bounded identities.
    pub fn from_model(model: &OptionContract) -> Result<Self, String> {
        let definition = model.coverage_definition()?;
        let (instrument, publisher, underlying) = model.underlying_source()?;
        if instrument == 0
            || publisher == 0
            || underlying.is_empty()
            || [
                definition.contract,
                definition.root,
                underlying,
                model.text(23),
            ]
            .iter()
            .any(|s| s.len() > 1024)
        {
            return Err("Invalid compact source identity".into());
        }
        Ok(Self {
            contract: definition.contract.into(),
            root: definition.root.into(),
            underlying: underlying.into(),
            zone: model.text(23).into(),
            strike: definition.strike,
            expiration: definition.expiration,
            instrument,
            publisher,
            digest: model.coverage_source_digest()?,
            legacy_strike: model.fields[9]
                .as_f64()
                .ok_or("Missing compact legacy strike")?
                .to_bits(),
            browser_kind: Some(model.text(10).to_owned()),
            mapping_version: model.text(29).to_owned(),
        })
    }
    /// Borrows exact legacy display fields from a browser source projection.
    ///
    /// # Errors
    /// A background-only source cannot silently substitute an inferred option type.
    pub fn browser_row(
        &self,
    ) -> Result<crate::option_chain::browser_window::Row<'_>, &'static str> {
        Ok(crate::option_chain::browser_window::Row {
            contract: &self.contract,
            strike: f64::from_bits(self.legacy_strike),
            kind: self
                .browser_kind
                .as_deref()
                .ok_or("Browser source lacks legacy option-type text")?,
        })
    }
    /// Exact source key for selected full metadata. An absent version is an
    /// explicit business qualification failure, never permission to read latest.
    pub fn mapping_version(&self) -> &str {
        &self.mapping_version
    }
    /// Exact native planner input, borrowing only compact copied strings.
    pub fn coverage_definition(
        &self,
    ) -> Result<crate::option_chain::coverage::Definition<'_>, &'static str> {
        Ok(crate::option_chain::coverage::Definition {
            contract: &self.contract,
            root: &self.root,
            underlying: &self.underlying,
            strike: self.strike,
            expiration: self.expiration,
        })
    }
    /// Exact raw exchange zone, never a local-machine fallback.
    pub fn timezone(&self) -> &str {
        &self.zone
    }
    /// Exact underlying binding for independent native quote acquisition.
    pub fn underlying_source(&self) -> Result<(u32, u16, &str), &'static str> {
        Ok((self.instrument, self.publisher, &self.underlying))
    }
    /// Opaque complete projection signature for immutable-source verification.
    pub fn digest(&self) -> [u8; 32] {
        self.digest
    }
    /// Checks that full selected metadata belongs to the captured source.
    pub fn matches(&self, model: &OptionContract) -> Result<bool, String> {
        Ok(self.digest == model.coverage_source_digest()?)
    }
}
impl OptionContract {
    /// Parses the raw typed marshaling array. No input financial/date rule runs in C#.
    ///
    /// # Errors
    /// Malformed field shapes, invalid exact scalar representation or capacity.
    pub fn from_command(value: &serde_json::Value) -> Result<Self, &'static str> {
        let input = value
            .as_array()
            .filter(|a| a.len() == 45)
            .ok_or("Invalid option read-model field count")?;
        if input
            .iter()
            .filter_map(serde_json::Value::as_str)
            .map(str::len)
            .sum::<usize>()
            > 60000
        {
            return Err("Option read-model text capacity exceeded");
        }
        let mut fields = Vec::with_capacity(45);
        for (index, value) in input.iter().enumerate() {
            let mapped = if index == 9 {
                let bits = value
                    .as_str()
                    .filter(|s| s.len() == 16)
                    .and_then(|s| u64::from_str_radix(s, 16).ok())
                    .ok_or("Invalid legacy option strike bits")?;
                Value::F64(f64::from_bits(bits))
            } else if matches!(index, 18 | 21 | 22 | 31 | 32 | 38) && !value.is_null() {
                let pair = value
                    .as_array()
                    .filter(|p| p.len() == 2)
                    .ok_or("Invalid option read-model time")?;
                let ticks = pair[0].as_i64().ok_or("Invalid option read-model time")?;
                let offset = pair[1]
                    .as_i64()
                    .filter(|n| (-840..=840).contains(n))
                    .ok_or("Invalid option read-model offset")?;
                Value::Array(vec![
                    codec::timestamp(
                        ticks
                            .checked_add(offset * 600000000)
                            .ok_or("Option read-model local time overflow")?,
                    )?,
                    offset.into(),
                ])
            } else if matches!(index, 11 | 25 | 26 | 27) && !value.is_null() {
                let amount = value
                    .as_str()
                    .filter(|s| s.len() <= 64)
                    .ok_or("Invalid option read-model decimal")?;
                decimal::parse(amount).ok_or("Invalid option read-model decimal")?;
                amount.into()
            } else if matches!(
                index,
                8 | 12 | 13 | 15 | 16 | 24 | 34 | 35 | 36 | 37 | 40 | 42 | 43 | 44
            ) && (!value.is_null() || !matches!(index, 15 | 16 | 43 | 44))
            {
                let number = value.as_i64().ok_or("Invalid option read-model integer")?;
                let valid = match index {
                    8 => (0..=3652058).contains(&number),
                    15 | 44 => (0..=65535).contains(&number),
                    16 | 43 => (0..=i64::from(u32::MAX)).contains(&number),
                    _ => i32::try_from(number).is_ok(),
                };
                if !valid {
                    return Err("Option read-model integer range exceeded");
                }
                number.into()
            } else {
                match value {
                    serde_json::Value::Null => Value::Nil,
                    serde_json::Value::String(s) if s.len() <= 16384 => s.clone().into(),
                    _ => return Err("Invalid option read-model scalar"),
                }
            };
            fields.push(mapped);
        }
        Ok(Self { fields })
    }
    /// Existing complete numeric-key DTO, preserving exact raw scalar values.
    pub fn value(&self) -> Value {
        Value::Array(self.fields.clone())
    }
    fn text(&self, key: usize) -> &str {
        self.fields[key].as_str().unwrap_or("")
    }
    fn number(&self, key: usize) -> Option<i64> {
        self.fields[key].as_i64()
    }
    fn amount(&self, key: usize) -> Option<Decimal> {
        self.fields[key].as_str().and_then(decimal::parse)
    }
    fn time(&self, key: usize) -> Option<(i64, i64)> {
        crate::option_chain::convention::timestamp(&self.fields[key]).and_then(|(local, offset)| {
            local
                .checked_sub(offset * 600000000)
                .map(|utc| (utc, offset))
        })
    }
    fn utc(&self, key: usize) -> bool {
        self.time(key).is_some_and(|(at, offset)| {
            offset == 0 && at != -621355968000000000 && at != 2534023007999999999
        })
    }
    /// Lossless option strike, independent of the fifteen-digit ranking cast.
    ///
    /// # Errors
    /// Invalid legacy/exact source representation or conflict.
    pub fn exact_strike(&self) -> Result<Decimal, &'static str> {
        decimal::exact_strike(
            self.fields[9].as_f64().ok_or("Invalid legacy strike")?,
            self.amount(11),
        )
    }
    /// Exact copied reference fields for native subscription-universe planning.
    /// Expiration is converted to provider nanoseconds with checked arithmetic;
    /// this helper never substitutes a machine date or a rounded strike.
    ///
    /// # Errors
    /// Missing canonical identities, exact strike or representable expiration.
    pub fn coverage_definition(
        &self,
    ) -> Result<crate::option_chain::coverage::Definition<'_>, &'static str> {
        if self.text(0).is_empty() || self.text(2).is_empty() || self.text(33).is_empty() {
            return Err("Missing native coverage identity");
        }
        let expiration = self
            .time(21)
            .and_then(|(ticks, _)| u64::try_from(ticks).ok())
            .and_then(|ticks| ticks.checked_mul(100));
        Ok(crate::option_chain::coverage::Definition {
            contract: self.text(0),
            root: self.text(2),
            underlying: self.text(33),
            strike: self.exact_strike()?,
            expiration,
        })
    }
    fn coverage_source_digest(&self) -> Result<[u8; 32], String> {
        use sha2::{Digest, Sha256};
        let fields = [
            0, 2, 8, 9, 11, 12, 13, 14, 15, 16, 19, 21, 23, 24, 29, 33, 34, 35, 36, 37, 40, 42, 43,
            44,
        ]
        .into_iter()
        .map(|index| self.fields[index].clone())
        .collect();
        Ok(Sha256::digest(codec::encode(&Value::Array(fields))?).into())
    }
    /// Copies the exact normalized provider candidate from this reviewed source.
    ///
    /// # Errors
    /// Missing immutable identity, invalid convention or exact strike/expiry.
    pub fn scope_candidate(&self) -> Result<crate::option_chain::universe::Candidate, String> {
        let mapping = self.convention()?;
        Ok(crate::option_chain::universe::Candidate {
            contract: self.text(0).into(),
            mapping_version: self.text(29).into(),
            definition_digest: self.text(19).into(),
            dataset: mapping.dataset().into(),
            publisher: mapping.publisher(),
            instrument: mapping.instrument(),
            raw_symbol: mapping.raw_symbol().into(),
            underlying: mapping.underlying().into(),
            right: u8::try_from(self.number(35).ok_or("Missing option right")?)
                .map_err(|_| "Invalid option right")?,
            strike: self.exact_strike()?,
            expiration_ns: self.coverage_definition()?.expiration,
            mapping: Some(mapping),
        })
    }
    /// Existing ordered raw-reference failure names. These are validation facts,
    /// not permission to reuse an unreviewed/latest provider convention.
    pub fn errors(&self) -> Vec<&'static str> {
        let mut errors = Vec::new();
        let positive = |value: Option<Decimal>| {
            value
                .is_some_and(|v| v.compare(Decimal::default()) == Some(std::cmp::Ordering::Greater))
        };
        if self.number(12) != Some(1) || self.number(13) != Some(2) {
            errors.push("ReviewRequired");
        }
        if self.text(14).trim().is_empty()
            || self.number(15).is_none_or(|v| v == 0)
            || self.number(16).is_none_or(|v| v == 0)
            || self.text(17).trim().is_empty()
        {
            errors.push("ProviderIdentity");
        }
        if !self.utc(18)
            || self.text(19).len() != 64
            || !self.text(19).bytes().all(|b| b.is_ascii_hexdigit())
            || self.text(20).trim().is_empty()
        {
            errors.push("DefinitionEvidence");
        }
        if !self.utc(21)
            || !self.utc(22)
            || self
                .time(21)
                .zip(self.time(22))
                .is_some_and(|(expiration, last)| last.0 > expiration.0)
        {
            errors.push("ExactTimes");
        }
        if !self.utc(31)
            || !self.utc(32)
            || self
                .time(31)
                .zip(self.time(32))
                .is_some_and(|(from, until)| from.0 >= until.0)
        {
            errors.push("EffectiveInterval");
        }
        let legacy = self.text(7);
        let old = if !legacy.is_empty() && legacy.bytes().all(|b| b.is_ascii_digit() || b == b'.') {
            decimal::parse(legacy)
        } else {
            None
        };
        if !positive(self.amount(25))
            || old
                .zip(self.amount(25))
                .is_none_or(|(a, b)| a.compare(b) != Some(std::cmp::Ordering::Equal))
        {
            errors.push("Multiplier");
        }
        if !positive(self.amount(26)) || !positive(self.amount(27)) {
            errors.push("PriceScale/TickSize");
        }
        if [28, 29, 30]
            .iter()
            .any(|key| self.text(*key).trim().is_empty())
        {
            errors.push("ConventionEvidence");
        }
        if Zone::new(self.text(23)).is_err() {
            errors.push("ExchangeTimeZoneId");
        }
        if self.text(4) != "FOP" {
            errors.push("SecurityType");
        }
        if self.number(34) != Some(1) || self.text(33).trim().is_empty() {
            errors.push("Underlying");
        }
        if self.number(43).is_none_or(|v| v == 0) || self.number(44).is_none_or(|v| v == 0) {
            errors.push("UnderlyingProviderIdentity");
        }
        if !matches!(self.number(36), Some(1 | 2)) {
            errors.push("ExerciseStyle");
        }
        if !matches!(self.number(24), Some(1 | 2)) {
            errors.push("SettlementStyle");
        }
        if !matches!(self.number(37), Some(1 | 2)) {
            errors.push("PremiumStyle");
        }
        if self.text(2).eq_ignore_ascii_case("ES")
            && (self.text(6).eq_ignore_ascii_case("XCME")
                || self.text(6).eq_ignore_ascii_case("CME"))
            && self.number(37) != Some(1)
        {
            errors.push("CmeEsPremiumStyleConflict");
        }
        if !matches!(self.number(35), Some(1 | 2))
            || self.text(10)
                != if self.number(35) == Some(1) {
                    "Call"
                } else {
                    "Put"
                }
        {
            errors.push("OptionRight");
        }
        if !matches!(self.number(42), Some(1 | 2)) {
            errors.push("DayCount");
        }
        if !matches!(self.number(40), Some(1 | 2)) || self.text(41).trim().is_empty() {
            errors.push("PremiumTickRule");
        }
        if self.amount(11).is_none() {
            errors.push("StrikePriceDecimal");
        }
        if self.exact_strike().is_err() {
            errors.push("StrikeConflict");
        }
        if self.time(38).is_some_and(|(at, offset)| {
            offset != 0 || self.time(21).is_some_and(|(expiration, _)| at > expiration)
        }) {
            errors.push("ExerciseCutoffUtc");
        }
        if [0, 2, 3, 4, 5, 6, 7, 10]
            .iter()
            .any(|key| self.text(*key).is_empty())
            || self.number(8).is_none_or(|v| v == 0)
            || self.fields[9]
                .as_f64()
                .is_none_or(|v| v <= 0.0 || v.is_nan())
        {
            errors.push("LegacyIdentity");
        }
        errors
    }
    /// Exact underlying source identity copied from the reviewed model.
    ///
    /// # Errors
    /// Missing or invalid physical identity; never inferred from a symbol.
    pub fn underlying_source(&self) -> Result<(u32, u16, &str), &'static str> {
        Ok((
            u32::try_from(self.number(43).ok_or("Missing underlying instrument")?)
                .map_err(|_| "Invalid underlying instrument")?,
            u16::try_from(self.number(44).ok_or("Missing underlying publisher")?)
                .map_err(|_| "Invalid underlying publisher")?,
            self.text(33),
        ))
    }
    /// Empty calendar input for native raw-fact lookup. Trading dates are loaded
    /// inside the dataset before context preparation; none are guessed here.
    ///
    /// # Errors
    /// Expired/invalid dates, unreviewed metadata or unknown exchange timezone.
    pub fn calendar(&self, date: time::Date) -> Result<super::calendar::Calendar, &'static str> {
        let convention = self.convention()?;
        let expiration = time::OffsetDateTime::from_unix_timestamp_nanos(
            i128::from(convention.expiration()) * 100,
        )
        .map_err(|_| "Invalid option expiry date")?
        .date();
        super::calendar::Calendar::new(
            convention.calendar_version().into(),
            self.text(23),
            date,
            expiration,
            648000000000,
            vec![],
        )
        .map_err(|_| "Invalid option calendar input")
    }
    /// Creates the exact derived reviewed convention only after complete raw
    /// qualification. The subsequent native exact-version lookup remains mandatory.
    ///
    /// # Errors
    /// Raw review failures or a derived pricing convention rejected by native policy.
    pub fn convention(&self) -> Result<Convention, &'static str> {
        if !self.errors().is_empty() {
            return Err("Unqualified option reference");
        }
        let mut fields = vec![Value::from(3)];
        for key in [
            0, 14, 15, 16, 17, 2, 6, 5, 33, 36, 24, 21, 22, 42, 28, 25, 27, 41, 19, 29, 30, 31, 32,
            40, 37, 34, 11, 35,
        ] {
            fields.push(self.fields[key].clone());
        }
        let convention = Convention::new(Value::Array(fields))?;
        convention
            .qualify(
                self.time(31)
                    .ok_or("Missing effective option reference time")?
                    .0,
                0,
            )
            .map_err(|failure| failure.code)?;
        Ok(convention)
    }
}
