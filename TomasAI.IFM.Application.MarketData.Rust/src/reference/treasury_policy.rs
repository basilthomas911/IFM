//! Native reviewed Treasury publication and conversion policy.
//! Policy is imported immutable configuration. No managed lease/context timer or
//! callback participates in selecting, qualifying or encoding a reference rate.

use super::{
    calendar::Zone,
    decimal,
    semantic::{self, Value as Json},
    treasury,
    treasury_curve::{Curve, SERIES, SOURCE},
};
use crate::messaging::codec;
use rmpv::Value;
use std::collections::BTreeSet;
use time::Date;

/// One reviewed daily publication availability point, in API UTC ticks.
#[derive(Clone, Copy)]
pub struct Publication {
    /// Official observation date.
    pub date: Date,
    /// Earliest required availability, distinct from retrieval time.
    pub available: i64,
}
/// Complete bounded reviewed publication schedule and valuation timezone.
#[derive(Clone)]
pub struct Publications {
    version: String,
    from: i64,
    until: i64,
    entries: Vec<Publication>,
    zone: Zone,
}
impl Publications {
    /// Validates immutable schedule data before native feed resources are started.
    ///
    /// # Errors
    /// Rejects missing/duplicate/unordered/non-UTC coverage and unknown zones.
    pub fn new(
        version: String,
        from: i64,
        until: i64,
        entries: Vec<Publication>,
        zone: &str,
    ) -> Result<Self, &'static str> {
        if version.trim().is_empty()
            || version.len() > 512
            || from >= until
            || entries.is_empty()
            || entries.len() > 1000
            || entries.iter().any(|p| {
                p.date.to_julian_day() == 1721426 || codec::timestamp(p.available).is_err()
            })
            || entries
                .windows(2)
                .any(|p| p[0].date >= p[1].date || p[0].available >= p[1].available)
            || codec::timestamp(from).is_err()
            || codec::timestamp(until).is_err()
        {
            return Err("TreasuryPublicationCoverageUnavailable");
        }
        Ok(Self {
            version,
            from,
            until,
            entries,
            zone: Zone::new(zone).map_err(|_| "TreasuryPublicationCoverageUnavailable")?,
        })
    }
    /// Exact reviewed version included in every prepared pricing context.
    pub fn version(&self) -> &str {
        &self.version
    }
    /// Most recent required source date at the supplied API valuation instant.
    ///
    /// # Errors
    /// Uncovered dates never fall back to the computer's current date.
    pub fn required(&self, at: i64) -> Result<Date, &'static str> {
        if at < self.from || at >= self.until {
            return Err("TreasuryPublicationCoverageUnavailable");
        }
        self.entries
            .iter()
            .rev()
            .find(|p| p.available <= at)
            .map(|p| p.date)
            .ok_or("TreasuryPublicationCoverageUnavailable")
    }
    /// Current local valuation date without the exchange's futures rollover.
    ///
    /// # Errors
    /// Rejects out-of-range instants with the existing reference failure code.
    pub fn valuation_date(&self, at: i64) -> Result<Date, &'static str> {
        self.zone
            .calendar_date(at)
            .map_err(|_| "TreasuryPublicationCoverageUnavailable")
    }
    /// Next publication boundary or end of explicit schedule coverage.
    pub fn valid_until(&self, at: i64) -> i64 {
        self.entries
            .iter()
            .find(|p| p.available > at)
            .map_or(self.until, |p| p.available)
    }
}
/// Existing reviewed source/series/unit conversion contract.
#[derive(Clone, PartialEq, Eq)]
pub struct Conversion {
    /// Existing official source name.
    pub source: String,
    /// Existing source series name.
    pub series: String,
    /// Existing TreasuryRateConvention numeric value.
    pub convention: i32,
    /// Reviewed conversion version.
    pub version: String,
    /// Required public provenance/evidence identity.
    pub evidence: String,
}
impl Conversion {
    /// Exact current UsTreasuryCurve.ConversionPolicy, not a new Rust convention.
    pub fn standard() -> Self {
        Self{source:SOURCE.into(),series:SERIES.into(),convention:1,version:"USTreasury-ParCmt-Semiannual/v1".into(),
        evidence:"https://home.treasury.gov/policy-issues/financing-the-government/interest-rate-statistics/interest-rates-frequently-asked-questions".into()}
    }
    fn value(&self) -> Value {
        Value::Array(vec![
            self.source.clone().into(),
            self.series.clone().into(),
            self.convention.into(),
            self.version.clone().into(),
            self.evidence.clone().into(),
        ])
    }
    fn semantic(&self) -> Json {
        Json::Object(vec![
            ("Source".into(), Json::String(self.source.clone())),
            ("SourceSeriesId".into(), Json::String(self.series.clone())),
            (
                "Convention".into(),
                Json::Integer(i64::from(self.convention)),
            ),
            ("Version".into(), Json::String(self.version.clone())),
            ("EvidenceId".into(), Json::String(self.evidence.clone())),
        ])
    }
}
/// Detached source snapshot identity, matching the current curve service contract.
/// Official Rust XML acquisition always supplies US/USD and a zero UTC offset.
pub struct Observation {
    /// Parsed source dates/rates and API retrieval instant.
    pub curve: Curve,
    /// Stable source identity.
    pub source: String,
    /// Issuing sovereign.
    pub country: String,
    /// Source currency.
    pub currency: String,
    /// Original retrieval UTC offset, retained for current validation parity.
    pub offset_minutes: i32,
}
impl Observation {
    /// Adds the official source identity to a Rust-acquired XML curve.
    pub fn official(curve: Curve) -> Self {
        Self {
            curve,
            source: SOURCE.into(),
            country: "US".into(),
            currency: "USD".into(),
            offset_minutes: 0,
        }
    }
    /// Existing declared-order curve digest. Retrieval time does not change it.
    ///
    /// # Errors
    /// Invalid decimal data or bounded canonical encoding cannot produce a digest.
    pub fn digest(&self) -> Result<String, &'static str> {
        let mut points: Vec<_> = self.curve.rates.iter().collect();
        points.sort_by_key(|p| p.months);
        let rates = points
            .into_iter()
            .map(|point| {
                Ok(Json::OrderedObject(vec![
                    ("Tenor".into(), Json::Integer(i64::from(point.months))),
                    (
                        "Rate".into(),
                        Json::String(semantic::decimal(point.percentage)?),
                    ),
                ]))
            })
            .collect::<Result<Vec<_>, &'static str>>()?;
        semantic::compute(&Json::OrderedObject(vec![
            ("Version".into(), Json::Integer(1)),
            ("Source".into(), Json::String(self.source.clone())),
            ("CountryCode".into(), Json::String(self.country.clone())),
            ("CurrencyCode".into(), Json::String(self.currency.clone())),
            (
                "ValueDate".into(),
                Json::String(self.curve.date.to_string()),
            ),
            ("Rates".into(), Json::Array(rates)),
        ]))
    }
}
/// Fully qualified detached selected-rate inputs for native context preparation.
#[derive(Clone)]
pub struct Rate {
    /// Selected tenor, in the existing enum's maturity-month units.
    pub tenor: u16,
    /// Unchanged source percentage in exact CLR decimal units.
    pub percentage: crate::option_chain::qualification::Decimal,
    /// Current continuously compounded annual rate.
    pub annual: f64,
    /// Original observation date.
    pub date: Date,
    /// Original API retrieval UTC ticks.
    pub retrieved: i64,
    /// Source semantic digest, independent of retrieval time.
    pub digest: String,
    /// Exact reviewed conversion/provenance fields.
    pub conversion: Conversion,
}
impl Rate {
    /// Full existing curve admission and rate conversion in existing check order.
    ///
    /// # Errors
    /// Returns the same stable error family as current UsTreasuryCurve conversion.
    pub fn convert(
        observation: &Observation,
        tenor: u16,
        policy: &Conversion,
    ) -> Result<Self, &'static str> {
        if *policy != Conversion::standard() || observation.source != SOURCE {
            return Err("TreasuryConventionUnverified");
        }
        if observation.country != "US" || observation.currency != "USD" {
            return Err("RateConventionUnsupported");
        }
        let curve = &observation.curve;
        let mut seen = BTreeSet::new();
        if curve.date.to_julian_day() == 1721426
            || observation.offset_minutes != 0
            || curve.rates.is_empty()
            || curve.rates.len() > 32
            || curve.rates.iter().any(|point| {
                !matches!(
                    point.months,
                    1 | 2 | 3 | 6 | 12 | 24 | 36 | 60 | 84 | 120 | 240 | 360
                ) || !seen.insert(point.months)
            })
        {
            return Err("TreasuryInvalid");
        }
        if !matches!(tenor, 1..=3) {
            return Err("TreasuryHorizonUnsupported");
        }
        let point = curve
            .rates
            .iter()
            .find(|point| point.months == tenor)
            .ok_or("TreasuryTenorMissing")?;
        let half = decimal::to_double(
            decimal::divide_200(point.percentage).ok_or("RateConversionDomainInvalid")?,
        )
        .ok_or("RateConversionDomainInvalid")?;
        let annual = treasury::continuous_rate(half)?;
        Ok(Self {
            tenor,
            percentage: point.percentage,
            annual,
            date: curve.date,
            retrieved: curve.retrieved,
            digest: observation.digest()?,
            conversion: policy.clone(),
        })
    }
    /// Existing TreasuryContinuousRate numeric-key MessagePack DTO.
    ///
    /// # Errors
    /// Invalid imported decimal/timestamp never becomes a usable event payload.
    pub fn value(&self) -> Result<Value, &'static str> {
        Ok(Value::Array(vec![
            self.tenor.into(),
            codec::decimal_bits(self.percentage)?,
            self.annual.into(),
            (self.date.to_julian_day() - 1721426).into(),
            codec::utc_offset(self.retrieved)?,
            self.digest.clone().into(),
            self.conversion.value(),
            "FlatSelectedCmtProxy/v1".into(),
        ]))
    }
    /// Existing typed pricing hash properties for the exact selected-rate DTO.
    ///
    /// # Errors
    /// Rejects invalid UTC provenance before hashing.
    pub fn semantic(&self) -> Result<Json, &'static str> {
        Ok(Json::Object(vec![
            ("Tenor".into(), Json::Integer(i64::from(self.tenor))),
            ("RatePercent".into(), Json::Decimal(self.percentage)),
            ("AnnualContinuousRate".into(), Json::Double(self.annual)),
            ("ValueDate".into(), Json::String(self.date.to_string())),
            ("ObservedAtUtc".into(), semantic::utc(self.retrieved)?),
            ("CurveDigest".into(), Json::String(self.digest.clone())),
            ("Conversion".into(), self.conversion.semantic()),
            (
                "ModelingPolicy".into(),
                Json::String("FlatSelectedCmtProxy/v1".into()),
            ),
        ]))
    }
}
