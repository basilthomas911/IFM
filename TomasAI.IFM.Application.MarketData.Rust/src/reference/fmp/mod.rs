//! Financial Modeling Prep reference acquisition, using the same application
//! configuration and financial conventions as the current managed provider.
//! No operating-system DNS, detached task, local business clock or process is used.
mod json;
mod transport;
use super::treasury_curve::{Curve, Point};
use crate::{
    dns::Resolver,
    historical::History,
    runtime::{Deadline, Interrupted, clock::Clock},
};
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::{Arc, Mutex},
    time::Duration,
};
use time::{Date, OffsetDateTime, PrimitiveDateTime};
use tokio_util::sync::CancellationToken;

/// Typed provider failure. Messages never include the configured credential.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Error {
    /// Stable failure category for mapping to existing managed provider exceptions.
    pub code: &'static str,
    /// Redacted diagnostic explaining why no partial result was returned.
    pub message: String,
    /// HTTP response status, when the failure came from a provider response.
    pub status: Option<u16>,
}
impl Error {
    fn new(code: &'static str, message: impl Into<String>) -> Self {
        Self {
            code,
            message: message.into(),
            status: None,
        }
    }
    fn contract(message: impl Into<String>) -> Self {
        Self::new("Contract", message)
    }
    fn interrupted(reason: Interrupted) -> Self {
        Self::new(
            match reason {
                Interrupted::Cancelled => "Cancelled",
                Interrupted::DeadlineExpired => "Timeout",
            },
            reason.to_string(),
        )
    }
}
/// A complete qualified reference result or a typed provider failure.
pub type Result<T> = std::result::Result<T, Error>;
/// Explicit provider configuration. Limits are validated before opening a socket.
#[derive(Clone, Debug)]
pub struct Options {
    /// Provider base URI; HTTPS except numeric-loopback integration fixtures.
    pub base: String,
    /// Treasury endpoint relative to the configured origin.
    pub treasury: String,
    /// Calendar endpoint relative to the configured origin.
    pub calendar: String,
    /// Inclusive provider window size.
    pub window_days: u32,
    /// Maximum inclusive query range.
    pub range_days: u32,
    /// Latest-curve lookback size.
    pub lookback_days: u32,
    /// Per-response retained byte limit, at most eight MiB.
    pub response_bytes: usize,
    /// Maximum distinct normalized rows, at most 10,000.
    pub rows: usize,
    /// Shared HTTP admissions, at most four.
    pub concurrency: usize,
    /// Maximum retries after the first attempt, at most ten.
    pub retries: u32,
    /// Delay before the first retry.
    pub initial_delay: Duration,
    /// Maximum retry delay.
    pub maximum_delay: Duration,
    /// Consecutive final transient failures before opening the circuit.
    pub circuit_failures: u32,
    /// API-clock duration of an open circuit.
    pub circuit_break: Duration,
    /// One total operation budget, also bounded by the caller's original deadline.
    pub operation_timeout: Duration,
    /// Connect, TLS and response-header timeout; the body keeps the total budget.
    pub request_timeout: Duration,
}
impl Default for Options {
    fn default() -> Self {
        Self {
            base: "https://financialmodelingprep.com/stable/".into(),
            treasury: "treasury-rates".into(),
            calendar: "economic-calendar".into(),
            window_days: 90,
            range_days: 3660,
            lookback_days: 14,
            response_bytes: 4 * 1024 * 1024,
            rows: 10000,
            concurrency: 2,
            retries: 2,
            initial_delay: Duration::from_millis(250),
            maximum_delay: Duration::from_secs(5),
            circuit_failures: 5,
            circuit_break: Duration::from_secs(30),
            operation_timeout: Duration::from_secs(120),
            request_timeout: Duration::from_secs(20),
        }
    }
}
/// One normalized calendar event. Scalars preserve the provider's original text.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Event {
    /// UTC event time, Unix ticks, from the provider rather than the retrieval date.
    pub at: i64,
    /// Trimmed uppercase provider country code.
    pub country: String,
    /// Trimmed provider event name.
    pub name: String,
    /// Actual, estimate, previous, impact, unit, change and changePercentage.
    pub values: [Option<String>; 7],
    /// API UTC timestamp captured before calendar acquisition begins.
    pub retrieved: i64,
}
#[derive(Default)]
struct Circuit {
    failures: u32,
    until: Option<i64>,
}
/// Shared reference owner with bounded HTTP admission, retry and circuit state.
pub struct Fmp {
    options: Options,
    http: History,
    clock: Arc<Clock>,
    requests: tokio::sync::Semaphore,
    circuit: Mutex<Circuit>,
}
impl Fmp {
    /// Validates all non-secret settings and credential syntax without network I/O.
    pub fn new(
        options: Options,
        key: String,
        resolver: Arc<Resolver>,
        clock: Arc<Clock>,
    ) -> Result<Self> {
        if options.window_days == 0
            || options.range_days == 0
            || options.range_days > 3_660
            || options.lookback_days == 0
            || options.lookback_days > options.range_days
            || options.rows == 0
            || options.rows > 10_000
            || options.response_bytes == 0
            || options.response_bytes > 8 * 1024 * 1024
            || !(1..=4).contains(&options.concurrency)
            || options.retries > 10
            || options.initial_delay > options.maximum_delay
            || options.maximum_delay > Duration::from_secs(120)
            || options.operation_timeout.is_zero()
            || options.request_timeout.is_zero()
            || options.circuit_failures == 0
            || options.circuit_break.is_zero()
            || options.circuit_break > Duration::from_secs(86400)
        {
            return Err(Error::new(
                "Configuration",
                "Invalid or unbounded FMP options",
            ));
        }
        let base = reqwest::Url::parse(&options.base)
            .map_err(|_| Error::new("Configuration", "Invalid FMP base URI"))?;
        for path in [&options.treasury, &options.calendar] {
            if path.trim().is_empty()
                || path.contains(['?', '#'])
                || reqwest::Url::parse(path).is_ok()
                || !base
                    .join(path)
                    .is_ok_and(|url| url.origin() == base.origin())
            {
                return Err(Error::new("Configuration", "Invalid FMP relative endpoint"));
            }
        }
        let http = History::keyed_reference(&options.base, key, resolver, options.request_timeout)
            .map_err(|_| Error::new("Configuration", "Invalid FMP endpoint or credential"))?;
        let concurrency = options.concurrency;
        Ok(Self {
            options,
            http,
            clock,
            requests: tokio::sync::Semaphore::new(concurrency),
            circuit: Mutex::new(Circuit::default()),
        })
    }
    /// Routes provider warnings through the runtime's event-driven delivery owner.
    pub fn with_notifications(mut self, owner: Arc<crate::runtime::owner::Owner>) -> Self {
        self.http = self.http.with_notifications(owner);
        self
    }
    pub(crate) fn consume_warning<T>(
        &self,
        copy: impl FnOnce(
            &crate::historical::Warning,
        ) -> std::result::Result<T, crate::historical::Failure>,
    ) -> std::result::Result<Option<T>, crate::historical::Failure> {
        self.http.consume_warning(copy)
    }
    /// Removes retained warnings for complete application logging.
    pub fn drain_warnings(
        &self,
    ) -> std::result::Result<Vec<crate::historical::Warning>, crate::historical::Failure> {
        self.http.drain_warnings()
    }
    fn now(&self) -> Result<i64> {
        self.clock
            .utc_ticks()
            .map_err(|_| Error::new("Clock", "Invalid API reference clock"))
    }
    fn check(token: &CancellationToken, deadline: Deadline) -> Result<()> {
        if token.is_cancelled() {
            Err(Error::interrupted(Interrupted::Cancelled))
        } else if deadline.remaining().is_zero() {
            Err(Error::interrupted(Interrupted::DeadlineExpired))
        } else {
            Ok(())
        }
    }
    fn deadline(&self, caller: Deadline) -> Deadline {
        caller.earliest(Deadline::operation(self.options.operation_timeout))
    }
    fn range(&self, from: Date, to: Date) -> Result<()> {
        if from.year() < 1
            || to.year() > 9999
            || from > to
            || (to - from).whole_days() >= i64::from(self.options.range_days)
        {
            Err(Error::new("Validation", "Invalid FMP inclusive date range"))
        } else {
            Ok(())
        }
    }
    fn chunks(&self, from: Date, to: Date) -> Vec<(Date, Date)> {
        let mut result = Vec::new();
        let mut start = from;
        loop {
            let end = start
                .saturating_add(time::Duration::days(
                    i64::from(self.options.window_days) - 1,
                ))
                .min(to);
            result.push((start, end));
            if end == to {
                break;
            }
            start = end.next_day().expect("range already validated");
        }
        result
    }
    /// Retrieves complete, sorted Treasury curves with exact CLR decimal bits.
    pub async fn treasury_range(
        &self,
        from: Date,
        to: Date,
        token: &CancellationToken,
        caller: Deadline,
    ) -> Result<Vec<Curve>> {
        self.range(from, to)?;
        let deadline = self.deadline(caller);
        let mut result = BTreeMap::<Date, Curve>::new();
        for (start, end) in self.chunks(from, to) {
            let body = self
                .get(&self.options.treasury, start, end, token, deadline)
                .await?;
            let retrieved = self.now()?;
            json::rows(&body, |row| {
                Self::check(token, deadline)?;
                let curve = curve(row, retrieved)?;
                if curve.date < from || curve.date > to {
                    return Err(Error::contract(
                        "FMP Treasury curve is outside the requested date range",
                    ));
                }
                if let Some(old) = result.get(&curve.date) {
                    if !rates_equal(&old.rates, &curve.rates) {
                        return Err(Error::contract("Conflicting FMP Treasury curves"));
                    }
                } else {
                    result.insert(curve.date, curve);
                    if result.len() > self.options.rows {
                        return Err(Error::new(
                            "Response",
                            "Normalized FMP Treasury row limit exceeded",
                        ));
                    }
                }
                Ok(())
            })?;
        }
        Self::check(token, deadline)?;
        Ok(result.into_values().collect())
    }
    /// Returns the last available curve within the configured inclusive lookback.
    pub async fn treasury_latest(
        &self,
        as_of: Date,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Option<Curve>> {
        let min = Date::from_calendar_date(1, time::Month::January, 1).expect("constant date");
        let from = as_of
            .saturating_sub(time::Duration::days(
                i64::from(self.options.lookback_days) - 1,
            ))
            .max(min);
        Ok(self
            .treasury_range(from, as_of, token, deadline)
            .await?
            .pop())
    }
    /// Retrieves sorted, country-filtered events with duplicate conflict checks.
    pub async fn calendar(
        &self,
        from: Date,
        to: Date,
        countries: &[String],
        token: &CancellationToken,
        caller: Deadline,
    ) -> Result<Vec<Event>> {
        self.range(from, to)?;
        let deadline = self.deadline(caller);
        Self::check(token, deadline)?;
        let filters = countries
            .iter()
            .map(|c| {
                let c = c.trim().to_ascii_uppercase();
                if !(2..=3).contains(&c.len()) || !c.bytes().all(|b| b.is_ascii_alphabetic()) {
                    Err(Error::new(
                        "Validation",
                        "Calendar country filters require two or three ASCII letters",
                    ))
                } else {
                    Ok(c)
                }
            })
            .collect::<Result<BTreeSet<_>>>()?;
        let retrieved = self.now()?;
        let mut result = BTreeMap::<(i64, String, String), Event>::new();
        let mut retained = 0usize;
        for (start, end) in self.chunks(from, to) {
            let body = self
                .get(&self.options.calendar, start, end, token, deadline)
                .await?;
            json::rows(&body, |row| {
                Self::check(token, deadline)?;
                let event = event(row, retrieved)?;
                let date = OffsetDateTime::from_unix_timestamp_nanos(i128::from(event.at) * 100)
                    .map_err(|_| Error::contract("Invalid FMP event date"))?
                    .date();
                if date < from || date > to {
                    return Err(Error::contract(
                        "FMP calendar event is outside the requested date range",
                    ));
                }
                if !filters.is_empty() && !filters.contains(&event.country) {
                    return Ok(());
                }
                let key = (event.at, event.country.clone(), event.name.clone());
                if let Some(old) = result.get(&key) {
                    if old != &event {
                        return Err(Error::contract("Conflicting FMP calendar events"));
                    }
                } else {
                    retained = retained
                        .checked_add(
                            256 + event.country.len()
                                + event.name.len()
                                + event
                                    .values
                                    .iter()
                                    .flatten()
                                    .map(String::len)
                                    .sum::<usize>(),
                        )
                        .ok_or_else(|| {
                            Error::new("ResponseTooLarge", "FMP normalized byte size overflow")
                        })?;
                    if retained > 16 * 1024 * 1024 {
                        return Err(Error::new(
                            "ResponseTooLarge",
                            "FMP normalized byte capacity exceeded",
                        ));
                    }
                    result.insert(key, event);
                    if result.len() > self.options.rows {
                        return Err(Error::new(
                            "Response",
                            "Normalized FMP calendar row limit exceeded",
                        ));
                    }
                }
                Ok(())
            })?;
        }
        Self::check(token, deadline)?;
        let mut rows: Vec<_> = result.into_values().collect();
        // StringComparer.Ordinal compares UTF-16 code units; UTF-8 lexical order
        // differs for supplementary characters and would change event ordering.
        rows.sort_by(|a, b| {
            a.at.cmp(&b.at)
                .then_with(|| a.country.encode_utf16().cmp(b.country.encode_utf16()))
                .then_with(|| a.name.encode_utf16().cmp(b.name.encode_utf16()))
        });
        Ok(rows)
    }
}
fn date(value: &str) -> Result<Date> {
    let format = time::format_description::parse_borrowed::<2>("[year]-[month]-[day]")
        .expect("constant format");
    if value.len() != 10 {
        return Err(Error::contract("Invalid FMP Treasury date"));
    }
    Date::parse(value, &format)
        .ok()
        .filter(|d| d.year() >= 1)
        .ok_or(())
        .map_err(|_| Error::contract("Invalid FMP Treasury date"))
}
fn curve(row: json::Row, retrieved: i64) -> Result<Curve> {
    let date = date(
        &row.text("date")?
            .ok_or_else(|| Error::contract("Missing FMP Treasury date"))?,
    )?;
    let mut rates = Vec::with_capacity(12);
    for (key, months) in [
        ("month1", 1),
        ("month2", 2),
        ("month3", 3),
        ("month6", 6),
        ("year1", 12),
        ("year2", 24),
        ("year3", 36),
        ("year5", 60),
        ("year7", 84),
        ("year10", 120),
        ("year20", 240),
        ("year30", 360),
    ] {
        rates.push(Point {
            months,
            percentage: row.decimal(key)?,
        });
    }
    Ok(Curve {
        date,
        rates,
        retrieved,
    })
}
fn rates_equal(a: &[Point], b: &[Point]) -> bool {
    a.len() == b.len()
        && a.iter().zip(b).all(|(a, b)| {
            a.months == b.months
                && a.percentage.compare(b.percentage) == Some(std::cmp::Ordering::Equal)
        })
}
fn event(row: json::Row, retrieved: i64) -> Result<Event> {
    let required = |key| {
        row.text(key)?
            .map(|s| s.trim().to_owned())
            .filter(|s| !s.is_empty())
            .ok_or_else(|| Error::contract("Required FMP calendar string is absent"))
    };
    let source = required("date")?;
    let at = parse_time(&source)?;
    let country = required("country")?
        .chars()
        .map(|c| {
            let mut upper = c.to_uppercase();
            let first = upper.next().expect("uppercase is not empty");
            if upper.next().is_some() { c } else { first }
        })
        .collect();
    let name = required("event")?;
    let mut values: [Option<String>; 7] = Default::default();
    for (i, key) in [
        "actual",
        "estimate",
        "previous",
        "impact",
        "unit",
        "change",
        "changepercentage",
    ]
    .iter()
    .enumerate()
    {
        values[i] = row.scalar(key)?;
    }
    Ok(Event {
        at,
        country,
        name,
        values,
        retrieved,
    })
}
fn parse_time(value: &str) -> Result<i64> {
    if value.as_bytes().get(17..19) == Some(b"60") {
        return Err(Error::contract("Invalid FMP leap second"));
    }
    let parsed = OffsetDateTime::parse(value, &time::format_description::well_known::Rfc3339)
        .ok()
        .or_else(|| {
            [
                "[year]-[month]-[day] [hour]:[minute]:[second]",
                "[year]-[month]-[day]T[hour]:[minute]:[second]",
                "[year]-[month]-[day] [hour]:[minute]:[second].[subsecond]",
                "[year]-[month]-[day]T[hour]:[minute]:[second].[subsecond]",
            ]
            .iter()
            .find_map(|f| {
                let format = time::format_description::parse_borrowed::<2>(f).ok()?;
                PrimitiveDateTime::parse(value, &format)
                    .ok()
                    .map(|v| v.assume_utc())
            })
        })
        .or_else(|| date(value).ok().map(|d| d.midnight().assume_utc()))
        .ok_or_else(|| Error::contract("Invalid FMP calendar UTC timestamp"))?;
    if !(1..=9999).contains(&parsed.year()) {
        return Err(Error::contract("FMP timestamp is outside CLR date range"));
    }
    let nanos = parsed.unix_timestamp_nanos();
    let ticks = nanos.div_euclid(100) + i128::from(nanos.rem_euclid(100) >= 50);
    if !(-621355968000000000..=2534023007999999999).contains(&ticks) {
        return Err(Error::contract(
            "FMP timestamp rounds outside CLR date range",
        ));
    }
    i64::try_from(ticks).map_err(|_| Error::contract("Invalid FMP UTC ticks"))
}
