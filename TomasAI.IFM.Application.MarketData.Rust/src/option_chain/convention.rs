//! Native qualification of imported, reviewed option convention DTOs.
//! The existing numeric-key MessagePack contract is configuration data, not a
//! managed callback. Rules mirror OptionPricingQualification and premium ticks.

use super::qualification::Decimal;
use crate::reference::decimal;
use rmpv::Value;
use std::cmp::Ordering;

/// Existing qualification failure identity and input, before event formatting.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Failure {
    /// Existing stable OptionPricingFailure.Code.
    pub code: &'static str,
    /// Existing stable OptionPricingFailure.Input.
    pub input: &'static str,
}
/// Exact immutable imported convention with original scalar representations.
#[derive(Clone)]
pub struct Convention {
    fields: Vec<Value>,
}
pub(crate) fn timestamp(value: &Value) -> Option<(i64, i64)> {
    let pair = value.as_array()?;
    if pair.len() != 2 {
        return None;
    }
    let offset = pair[1].as_i64()?;
    let Value::Ext(-1, bytes) = &pair[0] else {
        return None;
    };
    let (seconds, nanos) = match bytes.as_slice() {
        [a, b, c, d] => (i128::from(u32::from_be_bytes([*a, *b, *c, *d])), 0),
        [a, b, c, d, e, f, g, h] => {
            let word = u64::from_be_bytes([*a, *b, *c, *d, *e, *f, *g, *h]);
            (i128::from(word & 0x3ffffffff), i128::from(word >> 34))
        }
        [a, b, c, d, e, f, g, h, i, j, k, l] => (
            i128::from(i64::from_be_bytes([*e, *f, *g, *h, *i, *j, *k, *l])),
            i128::from(u32::from_be_bytes([*a, *b, *c, *d])),
        ),
        _ => return None,
    };
    if nanos >= 1_000_000_000 {
        return None;
    }
    let ticks = i64::try_from(seconds * 10_000_000 + nanos / 100).ok()?;
    (-621355968000000000..=2534023007999999999)
        .contains(&ticks)
        .then_some((ticks, offset))
}
impl Convention {
    /// Copies one bounded existing convention DTO. Typed wire corruption is
    /// rejected before business qualification or any provider/resource admission.
    ///
    /// # Errors
    /// Rejects malformed field types and oversized imported metadata.
    pub fn new(value: Value) -> Result<Self, &'static str> {
        let Value::Array(mut fields) = value else {
            return Err("Invalid convention DTO");
        };
        if fields.len() < 24
            || fields.len() > 29
            || fields
                .iter()
                .any(|v| v.as_str().is_some_and(|s| s.len() > 16384))
        {
            return Err("Invalid convention field capacity");
        }
        while fields.len() < 29 {
            let index = fields.len();
            fields.push(if index == 27 { Value::Nil } else { 0.into() });
        }
        for index in [0, 3, 4, 10, 11, 14, 24, 25, 26, 28] {
            if fields[index].as_i64().is_none() {
                return Err("Invalid convention integer");
            }
        }
        if !(0..=i64::from(u16::MAX)).contains(&fields[3].as_i64().unwrap_or(-1))
            || !(0..=i64::from(u32::MAX)).contains(&fields[4].as_i64().unwrap_or(-1))
        {
            return Err("Invalid convention provider identity range");
        }
        for index in [1, 2, 5, 6, 7, 8, 9, 15, 18, 19, 20, 21] {
            if !fields[index].is_nil() && fields[index].as_str().is_none() {
                return Err("Invalid convention string");
            }
        }
        for index in [12, 13, 22, 23] {
            timestamp(&fields[index]).ok_or("Invalid convention timestamp")?;
        }
        for index in [16, 17] {
            fields[index]
                .as_str()
                .and_then(decimal::parse)
                .ok_or("Invalid convention decimal")?;
        }
        if !fields[27].is_nil() {
            fields[27]
                .as_str()
                .and_then(decimal::parse)
                .ok_or("Invalid convention strike")?;
        }
        Ok(Self { fields })
    }
    fn number(&self, key: usize) -> i64 {
        self.fields[key].as_i64().unwrap_or(0)
    }
    fn text(&self, key: usize) -> &str {
        self.fields[key].as_str().unwrap_or("")
    }
    fn amount(&self, key: usize) -> Option<Decimal> {
        self.fields[key].as_str().and_then(decimal::parse)
    }
    fn positive(&self, key: usize) -> bool {
        self.amount(key)
            .is_some_and(|d| d.compare(Decimal::default()) == Some(Ordering::Greater))
    }
    fn same_amount(&self, key: usize, value: &str) -> bool {
        self.amount(key)
            .zip(decimal::parse(value))
            .is_some_and(|(a, b)| a.compare(b) == Some(Ordering::Equal))
    }
    fn instant(&self, key: usize) -> (i64, i64) {
        timestamp(&self.fields[key]).unwrap_or((0, i64::MAX))
    }
    fn valid_ticks(&self) -> bool {
        match self.number(0) {
            1 => self.number(24) == 0 && self.positive(17),
            2 | 3 => match self.number(24) {
                1 => self.positive(17) && !self.text(18).trim().is_empty(),
                2 => {
                    self.text(6) == "ES"
                        && self.text(2) == "GLBX.MDP3"
                        && self.text(7) == "XCME"
                        && self.text(8) == "USD"
                        && self.same_amount(16, "50")
                        && self.same_amount(17, ".05")
                        && self.text(18) == "CME-358A01.C-2026-09-08/v1"
                }
                _ => false,
            },
            _ => false,
        }
    }
    /// Existing reviewed premium increment. Combination legs use the pinned
    /// allocation increment; outright/net premiums use inclusive symmetric bands.
    /// Signed comparisons avoid taking abs of CLR Decimal.MinValue.
    ///
    /// # Errors
    /// Unknown/unreviewed tick rules or invalid decimal bits fail explicitly.
    pub fn premium_increment(
        &self,
        premium: Decimal,
        combination_leg: bool,
    ) -> Result<Decimal, &'static str> {
        if !self.valid_ticks() {
            return Err("TickRuleUnavailable");
        }
        if self.number(24) != 2 {
            return self.amount(17).ok_or("TickRuleUnavailable");
        }
        let value = if combination_leg {
            ".05"
        } else {
            let mut increment = ".50";
            for (low, high, tick) in [
                ("-5", "5", ".05"),
                ("-20", "20", ".10"),
                ("-100", "100", ".25"),
            ] {
                let low = decimal::parse(low).ok_or("Invalid premium band")?;
                let high = decimal::parse(high).ok_or("Invalid premium band")?;
                if premium.compare(low).ok_or("Invalid premium decimal")? != Ordering::Less
                    && premium.compare(high).ok_or("Invalid premium decimal")? != Ordering::Greater
                {
                    increment = tick;
                    break;
                }
            }
            increment
        };
        decimal::parse(value).ok_or("Invalid premium increment")
    }
    /// Canonical contract identity from the reviewed configuration.
    pub fn contract(&self) -> &str {
        self.text(1)
    }
    /// Exact provider dataset from the reviewed definition.
    pub fn dataset(&self) -> &str {
        self.text(2)
    }
    /// Exact publisher, or zero for an unqualified out-of-range definition.
    pub fn publisher(&self) -> u16 {
        self.fields[3]
            .as_u64()
            .and_then(|v| u16::try_from(v).ok())
            .unwrap_or(0)
    }
    /// Exact provider instrument, or zero for an unqualified definition.
    pub fn instrument(&self) -> u32 {
        self.fields[4]
            .as_u64()
            .and_then(|v| u32::try_from(v).ok())
            .unwrap_or(0)
    }
    /// Actual raw symbol; instrument-ID subscription input is not this symbol.
    pub fn raw_symbol(&self) -> &str {
        self.text(5)
    }
    /// Exact canonical futures dependency from the reviewed definition.
    pub fn underlying(&self) -> &str {
        self.text(9)
    }
    /// Explicit convention/calendar version; no root-derived fallback is inferred.
    pub fn calendar_version(&self) -> &str {
        self.text(15)
    }
    /// Exact UTC expiry from the reviewed convention.
    pub fn expiration(&self) -> i64 {
        self.instant(12).0
    }
    /// Earliest expiry, last trading or effective-until boundary.
    pub fn valid_until(&self) -> i64 {
        self.instant(12)
            .0
            .min(self.instant(13).0)
            .min(self.instant(23).0)
    }
    /// Performs the complete existing convention qualification in existing order.
    /// At includes its original offset, so an invalid non-UTC caller is rejected.
    ///
    /// # Errors
    /// Returns the existing failure code/input rather than usable pricing values.
    pub fn qualify(&self, at: i64, offset_minutes: i64) -> Result<(), Failure> {
        let fail = |code, input| Err(Failure { code, input });
        if !matches!(self.number(0), 1..=3)
            || !self.valid_ticks()
            || offset_minutes != 0
            || self.number(3) == 0
            || self.number(4) == 0
            || [1, 9, 5, 7, 20, 21, 18, 15]
                .iter()
                .any(|key| self.text(*key).trim().is_empty())
            || self.text(19).len() != 64
            || !self.text(19).bytes().all(|b| b.is_ascii_hexdigit())
            || [12, 13, 22, 23].iter().any(|key| self.instant(*key).1 != 0)
            || at < self.instant(22).0
            || at >= self.instant(23).0
            || self.instant(13).0 > self.instant(12).0
            || !self.positive(16)
            || !self.positive(17)
        {
            return fail("ContractMetadataUnavailable", "Definition/Mapping");
        }
        if self.number(10) == 0 {
            return fail("ContractMetadataUnavailable", "ExerciseStyle");
        }
        if self.number(0) < 3
            && (self.text(6) != "ES"
                || self.text(2) != "GLBX.MDP3"
                || self.text(8) != "USD"
                || self.number(10) != 1
                || self.number(11) != 1)
        {
            return fail("PricingModelUnsupported", "Product/Exercise/Settlement");
        }
        if self.number(0) == 3 {
            if !self.positive(27)
                || !matches!(self.number(28), 1 | 2)
                || self.number(25) == 0
                || self.number(26) == 0
                || self.text(6).trim().is_empty()
                || self.text(2).trim().is_empty()
            {
                return fail("ContractMetadataUnavailable", "EconomicTerms");
            }
            if self.text(6).eq_ignore_ascii_case("ES")
                && (self.text(7).eq_ignore_ascii_case("XCME")
                    || self.text(7).eq_ignore_ascii_case("CME"))
                && self.number(25) != 1
            {
                return fail(
                    "ContractConventionConflict",
                    "PremiumStyle/CME-ES-PremiumPaid",
                );
            }
            if self.number(26) != 1
                || self.text(8) != "USD"
                || !matches!(self.number(10), 1 | 2)
                || !matches!(self.number(25), 1 | 2)
                || !matches!(self.number(11), 1 | 2)
            {
                return fail("PricingModelUnsupported", "Product/Exercise/Settlement");
            }
        }
        if !matches!(self.number(14), 1 | 2) {
            return fail("DayCountUnsupported", "DayCount");
        }
        if at >= self.instant(12).0 || at >= self.instant(13).0 {
            return fail("ExpiredContract", "Expiration/LastTrading");
        }
        Ok(())
    }
    /// Computes the existing UTC elapsed year fraction with its reviewed denominator.
    ///
    /// # Errors
    /// Unqualified conventions cannot produce a usable year fraction.
    pub fn year_fraction(&self, at: i64) -> Result<f64, Failure> {
        self.qualify(at, 0)?;
        Ok((self.expiration() - at) as f64
            / 864_000_000_000.0
            / if self.number(14) == 1 { 365.0 } else { 360.0 })
    }
    /// Exact engine identity selected by the reviewed exercise convention.
    pub fn engine(&self) -> &'static str {
        if self.number(10) == 2 {
            "AmericanFutures.CRR.Managed/v1"
        } else {
            "Black76.Managed/v1"
        }
    }
    /// Typed canonical properties of the current convention contract. Default
    /// appended fields are omitted exactly as the existing JSON attributes require.
    ///
    /// # Errors
    /// Invalid imported decimal or non-UTC metadata is never silently normalized.
    pub fn semantic(&self) -> Result<crate::reference::semantic::Value, &'static str> {
        use crate::reference::semantic::{self, Value as J};
        const NAMES: [&str; 29] = [
            "SchemaVersion",
            "ContractId",
            "Dataset",
            "PublisherId",
            "InstrumentId",
            "RawSymbol",
            "Root",
            "Exchange",
            "Currency",
            "UnderlyingContractId",
            "ExerciseStyle",
            "SettlementStyle",
            "ExpirationUtc",
            "LastTradingUtc",
            "DayCount",
            "CalendarVersion",
            "Multiplier",
            "TickSize",
            "TickRuleVersion",
            "DefinitionDigest",
            "MappingVersion",
            "EvidenceId",
            "EffectiveFromUtc",
            "EffectiveUntilUtc",
            "PremiumTickRule",
            "PremiumStyle",
            "UnderlyingKind",
            "Strike",
            "Right",
        ];
        let mut fields = Vec::with_capacity(29);
        for (i, name) in NAMES.iter().enumerate() {
            if i >= 24 && (self.fields[i].is_nil() || i != 27 && self.number(i) == 0) {
                continue;
            }
            let value = match i {
                12 | 13 | 22 | 23 => {
                    let (ticks, offset) = self.instant(i);
                    if offset != 0 {
                        return Err("Non-UTC convention metadata");
                    }
                    semantic::utc(ticks)?
                }
                16 | 17 | 27 => J::Decimal(self.amount(i).ok_or("Invalid convention decimal")?),
                0 | 3 | 4 | 10 | 11 | 14 | 24 | 25 | 26 | 28 => J::Integer(self.number(i)),
                _ => {
                    if self.fields[i].is_nil() {
                        J::Null
                    } else {
                        J::String(self.text(i).into())
                    }
                }
            };
            fields.push(((*name).into(), value));
        }
        Ok(J::Object(fields))
    }
    /// Returns an owned original DTO for native manifest/event construction.
    pub fn value(&self) -> Value {
        Value::Array(self.fields.clone())
    }
}

impl Convention {
    /// Imports copied raw convention fields from the reference-store protocol.
    /// Timestamp/decimal fields preserve existing CLR wire representations.
    ///
    /// # Errors
    /// Invalid bounded scalar/timestamp data or an unsupported convention schema.
    pub fn from_command(value: &serde_json::Value) -> Result<Self, &'static str> {
        let rows = value
            .as_array()
            .filter(|v| (24..=29).contains(&v.len()))
            .ok_or("Invalid raw convention fields")?;
        let mut fields = Vec::with_capacity(rows.len());
        for (index, row) in rows.iter().enumerate() {
            fields.push(if matches!(index, 12 | 13 | 22 | 23) {
                let time = row
                    .as_array()
                    .filter(|v| v.len() == 2)
                    .ok_or("Invalid raw convention time")?;
                rmpv::Value::Array(vec![
                    crate::messaging::codec::timestamp(
                        time[0].as_i64().ok_or("Invalid raw convention time")?,
                    )?,
                    time[1]
                        .as_i64()
                        .ok_or("Invalid raw convention offset")?
                        .into(),
                ])
            } else {
                match row {
                    serde_json::Value::Null => rmpv::Value::Nil,
                    serde_json::Value::String(v) if v.len() <= 16384 => v.clone().into(),
                    serde_json::Value::Number(v) => {
                        v.as_i64().ok_or("Invalid raw convention number")?.into()
                    }
                    _ => return Err("Invalid raw convention scalar"),
                }
            });
        }
        Self::new(rmpv::Value::Array(fields))
    }
}
