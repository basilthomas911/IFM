//! Explicit exchange calendar qualification using pinned, in-process IANA rules.
//! API UTC ticks and the reviewed rollover determine ValueDate. No OS clock,
//! managed TimeZoneInfo callback, provider connection or file read is involved.

use chrono::{Datelike, Timelike};
use std::{collections::BTreeMap, sync::OnceLock};
use time::Date;

/// Calendar/zone coverage failure, matching the current context failure family.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Failure;
/// Pre-resolved immutable timezone. IANA data is compiled into this backend.
#[derive(Clone, Copy)]
pub struct Zone(chrono_tz::Tz);
impl Zone {
    /// Resolves existing IANA or frozen Windows alias identifiers case-insensitively.
    ///
    /// # Errors
    /// Unknown zones fail explicitly; no machine-local timezone is substituted.
    pub fn new(name: &str) -> Result<Self, Failure> {
        if name.is_empty() || name.len() > 128 {
            return Err(Failure);
        }
        static WINDOWS: OnceLock<Result<BTreeMap<String, String>, Failure>> = OnceLock::new();
        let aliases = WINDOWS
            .get_or_init(|| {
                serde_json::from_str(include_str!("data/windows-zones.json")).map_err(|_| Failure)
            })
            .as_ref()
            .map_err(|_| Failure)?;
        let canonical = aliases
            .iter()
            .find(|(key, _)| key.eq_ignore_ascii_case(name))
            .map_or(name, |(_, value)| value.as_str());
        chrono_tz::TZ_VARIANTS
            .iter()
            .find(|zone| zone.name().eq_ignore_ascii_case(canonical))
            .copied()
            .map(Self)
            .ok_or(Failure)
    }
    /// Version of the pinned timezone rules used for deterministic date conversion.
    pub fn version() -> &'static str {
        chrono_tz::IANA_TZDB_VERSION
    }
    /// Returns the exact exchange ValueDate for supplied UTC ticks and rollover.
    ///
    /// # Errors
    /// Invalid CLR time, rollover or local-date overflow is never guessed/clamped.
    pub fn value_date(self, ticks: i64, rollover: u64) -> Result<Date, Failure> {
        if rollover >= 864_000_000_000 {
            return Err(Failure);
        }
        let (date, time) = self.parts(ticks)?;
        if time >= rollover {
            date.next_day()
                .filter(|date| date.year() <= 9999)
                .ok_or(Failure)
        } else {
            Ok(date)
        }
    }
    /// Local calendar date without exchange rollover, used by Treasury source
    /// publication policy. It still uses only caller-supplied UTC business time.
    ///
    /// # Errors
    /// Rejects invalid/out-of-range instants without an OS timezone fallback.
    pub fn calendar_date(self, ticks: i64) -> Result<Date, Failure> {
        Ok(self.parts(ticks)?.0)
    }
    fn parts(self, ticks: i64) -> Result<(Date, u64), Failure> {
        if !(-621355968000000000..=2534023007999999999).contains(&ticks) {
            return Err(Failure);
        }
        let utc = chrono::DateTime::from_timestamp(
            ticks.div_euclid(10_000_000),
            (ticks.rem_euclid(10_000_000) * 100) as u32,
        )
        .ok_or(Failure)?;
        let local = utc.with_timezone(&self.0);
        if !(1..=9999).contains(&local.year()) {
            return Err(Failure);
        }
        let date = Date::from_calendar_date(
            local.year(),
            (local.month() as u8).try_into().map_err(|_| Failure)?,
            local.day() as u8,
        )
        .map_err(|_| Failure)?;
        let time = u64::from(local.num_seconds_from_midnight()) * 10_000_000
            + u64::from(local.nanosecond() / 100);
        Ok((date, time))
    }
}
/// Complete bounded reviewed calendar, separate from a Treasury publication schedule.
#[derive(Clone)]
pub struct Calendar {
    version: String,
    zone: Zone,
    timezone: String,
    from: Date,
    until: Date,
    rollover: u64,
    dates: Vec<Date>,
}
impl Calendar {
    /// Maps source UTC time to the reviewed exchange calendar date.
    ///
    /// # Errors
    /// Unsupported UTC time or exchange-zone conversion.
    pub fn calendar_date(&self, ticks: i64) -> Result<Date, Failure> {
        self.zone.calendar_date(ticks)
    }
    /// Validates version, sorted unique trading dates and complete coverage.
    ///
    /// # Errors
    /// Matches the current calendar coverage checks without retaining caller data.
    pub fn new(
        version: String,
        timezone: &str,
        from: Date,
        until: Date,
        rollover: u64,
        dates: Vec<Date>,
    ) -> Result<Self, Failure> {
        if !(1..=9999).contains(&from.year())
            || !(1..=9999).contains(&until.year())
            || version.trim().is_empty()
            || version.len() > 512
            || from > until
            || dates.len() > 2000
            || rollover >= 864_000_000_000
            || dates.iter().any(|date| *date < from || *date > until)
            || dates.windows(2).any(|pair| pair[0] >= pair[1])
        {
            return Err(Failure);
        }
        Ok(Self {
            version,
            zone: Zone::new(timezone)?,
            timezone: timezone.into(),
            from,
            until,
            rollover,
            dates,
        })
    }
    /// Existing numeric-key calendar DTO; timezone spelling is retained as imported.
    pub fn value(&self) -> rmpv::Value {
        use rmpv::Value as V;
        V::Array(vec![
            self.version.clone().into(),
            self.timezone.clone().into(),
            (self.from.to_julian_day() - 1721426).into(),
            (self.until.to_julian_day() - 1721426).into(),
            self.rollover.into(),
            V::Array(
                self.dates
                    .iter()
                    .map(|d| (d.to_julian_day() - 1721426).into())
                    .collect(),
            ),
        ])
    }
    /// Exact canonical typed calendar properties, including TimeOnly formatting.
    pub fn semantic(&self) -> super::semantic::Value {
        use super::semantic::Value as J;
        let seconds = self.rollover / 10_000_000;
        let mut rollover = format!(
            "{:02}:{:02}:{:02}",
            seconds / 3600,
            seconds / 60 % 60,
            seconds % 60
        );
        let fraction = self.rollover % 10_000_000;
        if fraction != 0 {
            rollover.push_str(&format!(".{fraction:07}"));
        }
        J::Object(vec![
            ("Version".into(), J::String(self.version.clone())),
            ("TimeZoneId".into(), J::String(self.timezone.clone())),
            ("CoverageFrom".into(), J::String(self.from.to_string())),
            ("CoverageUntil".into(), J::String(self.until.to_string())),
            ("ValueDateRollover".into(), J::String(rollover)),
            (
                "TradingDates".into(),
                J::Array(
                    self.dates
                        .iter()
                        .map(|d| J::String(d.to_string()))
                        .collect(),
                ),
            ),
        ])
    }
    /// Counts reviewed exchange dates in (valuation ValueDate, expiration ValueDate].
    /// The expiry date itself must be a covered trading date, as in current C#.
    ///
    /// # Errors
    /// Rejects version mismatch, uncovered/expired instants and missing expiry dates.
    pub fn count(&self, contract_version: &str, at: i64, expiration: i64) -> Result<i32, Failure> {
        if self.version != contract_version || at >= expiration {
            return Err(Failure);
        }
        let start = self.zone.value_date(at, self.rollover)?;
        let end = self.zone.value_date(expiration, self.rollover)?;
        if start < self.from
            || end > self.until
            || start > end
            || self.dates.binary_search(&end).is_err()
        {
            return Err(Failure);
        }
        let from = self.dates.partition_point(|date| *date <= start);
        let until = self.dates.partition_point(|date| *date <= end);
        Ok((until - from) as i32)
    }
}
