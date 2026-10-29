//! Official Treasury monthly Atom/XML acquisition with explicit application
//! time. The existing source identity, date selection, null policy and bounded
//! two-month cache are preserved. HTTP/DNS/TLS are asynchronously owned and
//! share the caller's original deadline, including waiting for the cache gate.
use crate::{
    dns::Resolver,
    historical::{Failure, History},
    option_chain::qualification::Decimal,
    runtime::{Deadline, clock::Clock},
};
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::Arc,
};
use time::{Date, Month};
use tokio_util::sync::CancellationToken;

/// Existing official source identity; no FMP convention is substituted.
pub const SOURCE: &str = "USTreasury";
/// Existing daily par/CMT series identity.
pub const SERIES: &str = "daily_treasury_yield_curve";
/// Official monthly XML endpoint. HTTP is permitted only for numeric loopback fixtures.
pub const ENDPOINT: &str =
    "https://home.treasury.gov/resource-center/data-chart-center/interest-rates/pages/xml";
const ATOM: &str = "http://www.w3.org/2005/Atom";
const DATA: &str = "http://schemas.microsoft.com/ado/2007/08/dataservices";
const META: &str = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
const COLUMNS: [(&str, u16); 12] = [
    ("BC_1MONTH", 1),
    ("BC_2MONTH", 2),
    ("BC_3MONTH", 3),
    ("BC_6MONTH", 6),
    ("BC_1YEAR", 12),
    ("BC_2YEAR", 24),
    ("BC_3YEAR", 36),
    ("BC_5YEAR", 60),
    ("BC_7YEAR", 84),
    ("BC_10YEAR", 120),
    ("BC_20YEAR", 240),
    ("BC_30YEAR", 360),
];
/// Source percentage with the exact CLR decimal coefficient, sign and scale.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Point {
    /// Tenor in calendar months, in the existing source column order.
    pub months: u16,
    /// CMT annual percentage, never pre-divided or converted to binary float.
    pub percentage: Decimal,
}
/// One fully qualified date from the monthly official source.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Curve {
    /// Publication ValueDate from NEW_DATE, not retrieval or computer-local date.
    pub date: Date,
    /// Explicitly available source tenors; missing/null values stay absent.
    pub rates: Vec<Point>,
    /// Authoritative API clock at completed retrieval, Unix UTC ticks.
    pub retrieved: i64,
}
fn invalid(message: &'static str) -> Failure {
    Failure::Input(message)
}
fn field_text(node: roxmltree::Node<'_, '_>) -> String {
    node.descendants()
        .filter(|n| n.is_text())
        .filter_map(|n| n.text())
        .collect()
}
fn rate(value: &str) -> Result<Decimal, Failure> {
    let parsed = super::decimal::parse(value).ok_or_else(|| invalid("Invalid Treasury decimal"))?;
    let minimum = Decimal {
        bits: [200, 0, 0, 1 << 31],
    };
    let maximum = Decimal {
        bits: [1000, 0, 0, 0],
    };
    if parsed.compare(minimum) != Some(std::cmp::Ordering::Greater)
        || parsed.compare(maximum) == Some(std::cmp::Ordering::Greater)
    {
        return Err(invalid("Invalid Treasury rate range"));
    }
    Ok(parsed)
}
/// Parses at most one MiB / 16,384 XML nodes and 31 source entries. Namespace
/// identities are resolved by the XML library; no local-name-only matching,
/// DTD, external resolver, synthesized tenor or partial-page acceptance occurs.
pub fn parse(bytes: &[u8], year: i32, month: Month, retrieved: i64) -> Result<Vec<Curve>, Failure> {
    if bytes.len() > 1_048_576 {
        return Err(invalid("Treasury XML exceeds byte capacity"));
    }
    let text = std::str::from_utf8(bytes).map_err(|_| invalid("Invalid Treasury UTF-8"))?;
    let document = roxmltree::Document::parse_with_options(
        text,
        roxmltree::ParsingOptions {
            allow_dtd: false,
            nodes_limit: 16_384,
            entity_resolver: None,
        },
    )
    .map_err(|_| invalid("Invalid Treasury XML"))?;
    let root = document.root_element();
    if !root.has_tag_name((ATOM, "feed")) {
        return Err(invalid("Expected Treasury Atom feed"));
    }
    if root
        .children()
        .any(|n| n.has_tag_name((ATOM, "link")) && n.attribute("rel") == Some("next"))
    {
        return Err(invalid("Truncated monthly Treasury feed"));
    }
    let mut rows: BTreeMap<Date, Curve> = BTreeMap::new();
    let mut count = 0;
    for entry in root.children().filter(|n| n.has_tag_name((ATOM, "entry"))) {
        count += 1;
        if count > 31 {
            return Err(invalid("Monthly Treasury response exceeds date bound"));
        }
        let properties = entry
            .children()
            .find(|n| n.has_tag_name((ATOM, "content")))
            .and_then(|n| n.children().find(|n| n.has_tag_name((META, "properties"))))
            .ok_or_else(|| invalid("Treasury entry has no properties"))?;
        let mut fields = BTreeSet::new();
        for node in properties.children().filter(|n| n.is_element()) {
            if !fields.insert((node.tag_name().namespace(), node.tag_name().name())) {
                return Err(invalid("Duplicate Treasury field"));
            }
        }
        let date = properties
            .children()
            .find(|n| n.has_tag_name((DATA, "NEW_DATE")))
            .map(field_text)
            .ok_or_else(|| invalid("Missing Treasury date"))?;
        if !date.is_ascii()
            || date.len() != 19
            || &date[10..] != "T00:00:00"
            || date.as_bytes()[4] != b'-'
            || date.as_bytes()[7] != b'-'
            || !date.as_bytes()[..10]
                .iter()
                .enumerate()
                .all(|(i, b)| i == 4 || i == 7 || b.is_ascii_digit())
        {
            return Err(invalid("Invalid Treasury value date"));
        }
        let parsed = Date::from_calendar_date(
            date[..4]
                .parse()
                .map_err(|_| invalid("Invalid Treasury year"))?,
            Month::try_from(
                date[5..7]
                    .parse::<u8>()
                    .map_err(|_| invalid("Invalid Treasury month"))?,
            )
            .map_err(|_| invalid("Invalid Treasury month"))?,
            date[8..10]
                .parse()
                .map_err(|_| invalid("Invalid Treasury day"))?,
        )
        .map_err(|_| invalid("Invalid Treasury date"))?;
        if parsed.year() != year || parsed.month() != month {
            return Err(invalid("Treasury date outside requested month"));
        }
        let mut rates = Vec::with_capacity(12);
        for (name, months) in COLUMNS {
            let Some(node) = properties.children().find(|n| n.has_tag_name((DATA, name))) else {
                continue;
            };
            if node.attribute((META, "null")) == Some("true") {
                continue;
            }
            let text = field_text(node);
            if text.trim().is_empty() {
                continue;
            }
            rates.push(Point {
                months,
                percentage: rate(&text)?,
            });
        }
        if let Some(prior) = rows.get(&parsed)
            && (prior.rates.len() != rates.len()
                || prior.rates.iter().zip(&rates).any(|(a, b)| {
                    a.months != b.months
                        || a.percentage.compare(b.percentage) != Some(std::cmp::Ordering::Equal)
                }))
        {
            return Err(invalid("Conflicting Treasury observations"));
        }
        rows.insert(
            parsed,
            Curve {
                date: parsed,
                rates,
                retrieved,
            },
        );
    }
    Ok(rows.into_values().collect())
}
struct Cached {
    expires: i64,
    rows: Vec<Curve>,
}
/// Owned official-source client. A cache hit retains its original retrieval
/// instant; the two-month cache never grows with historical import range size.
pub struct Treasury {
    http: History,
    endpoint: reqwest::Url,
    clock: Arc<Clock>,
    cache: tokio::sync::Mutex<Vec<((i32, Month), Cached)>>,
}
impl Treasury {
    /// Connects HTTP warning availability to the runtime's event-driven drain.
    pub fn with_notifications(mut self, owner: Arc<crate::runtime::owner::Owner>) -> Self {
        self.http = self.http.with_notifications(owner);
        self
    }
    /// Drains complete provider warnings; unknown warning codes/text are never
    /// silently converted to a successful empty response.
    pub fn drain_warnings(&self) -> Result<Vec<crate::historical::Warning>, Failure> {
        self.http.drain_warnings()
    }
    pub(crate) fn consume_warning<T>(
        &self,
        copy: impl FnOnce(&crate::historical::Warning) -> Result<T, Failure>,
    ) -> Result<Option<T>, Failure> {
        self.http.consume_warning(copy)
    }
    /// Creates transport policy only; no socket or background task is started.
    pub fn new(
        endpoint: &str,
        resolver: Arc<Resolver>,
        clock: Arc<Clock>,
    ) -> Result<Self, Failure> {
        Ok(Self {
            http: History::public_reference(endpoint, resolver)?,
            endpoint: reqwest::Url::parse(endpoint)
                .map_err(|_| invalid("Invalid Treasury endpoint"))?,
            clock,
            cache: tokio::sync::Mutex::new(Vec::with_capacity(2)),
        })
    }
    /// Preserves the current 1..366-day range policy from 1990 onward. The
    /// original caller deadline covers serialization, every month and body read.
    pub async fn range(
        &self,
        from: Date,
        to: Date,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<Curve>, Failure> {
        if from.year() < 1990 || to < from || (to - from).whole_days() >= 366 {
            return Err(invalid("Invalid Treasury date range"));
        }
        let operation = async {
            let mut cache = self.cache.lock().await;
            let mut result = Vec::new();
            let mut year = from.year();
            let mut month = from.month();
            loop {
                let now = self
                    .clock
                    .utc_ticks()
                    .map_err(|_| invalid("API Treasury clock unavailable"))?;
                let key = (year, month);
                let position = cache.iter().position(|(existing, _)| *existing == key);
                if position.is_none_or(|position| cache[position].1.expires <= now) {
                    let mut target = self.endpoint.clone();
                    target
                        .query_pairs_mut()
                        .append_pair("data", SERIES)
                        .append_pair(
                            "field_tdr_date_value_month",
                            &format!("{year:04}{:02}", month as u8),
                        );
                    let bytes = self.http.reference_xml(target, token, deadline).await?;
                    let retrieved = self
                        .clock
                        .utc_ticks()
                        .map_err(|_| invalid("API Treasury clock unavailable"))?;
                    let rows = parse(&bytes, year, month, retrieved)?;
                    let expires = retrieved
                        .checked_add(600_000_000)
                        .ok_or_else(|| invalid("Treasury cache clock overflow"))?;
                    // The current two-slot Dictionary reuses the removed slot.
                    // Preserve first-slot tie breaking when expiration times match.
                    if let Some(position) = position {
                        cache[position] = (key, Cached { expires, rows });
                    } else if cache.len() < 2 {
                        cache.push((key, Cached { expires, rows }));
                    } else {
                        let oldest = cache
                            .iter()
                            .enumerate()
                            .min_by_key(|(_, (_, entry))| entry.expires)
                            .unwrap()
                            .0;
                        cache[oldest] = (key, Cached { expires, rows });
                    }
                }
                let entry = &cache
                    .iter()
                    .find(|(existing, _)| *existing == key)
                    .unwrap()
                    .1;
                result.extend(
                    entry
                        .rows
                        .iter()
                        .filter(|row| row.date >= from && row.date <= to)
                        .cloned(),
                );
                if year == to.year() && month == to.month() {
                    break;
                }
                if month == Month::December {
                    year += 1;
                    month = Month::January;
                } else {
                    month = month.next();
                }
            }
            Ok(result)
        };
        deadline
            .run(token, operation)
            .await
            .map_err(Failure::Interrupted)?
    }
    /// Latest available observation in the existing inclusive thirteen-day
    /// lookback. Missing source dates never invent a zero or adjacent tenor.
    pub async fn latest(
        &self,
        at: Date,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Option<Curve>, Failure> {
        let minimum = Date::from_calendar_date(1990, Month::January, 1)
            .map_err(|_| invalid("Invalid Treasury minimum date"))?;
        let from = (at - time::Duration::days(13)).max(minimum);
        Ok(self.range(from, at, token, deadline).await?.pop())
    }
}
