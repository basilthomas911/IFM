//! Main-screen calendar reader and selection owner. Dates come from the API;
//! period calculation and durable calendar selection remain backend queries.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    services::{actor_requests, business_context::BusinessContext},
    sidebar::{CalendarRow, SidebarPublication},
    state::calendar::eastern_midnight,
};
use chrono::{DateTime, Utc};
use std::collections::BTreeMap;
const ROOT: &str = "TomasAI.IFM.Domain.MarketData.Shared.";
/// Match the existing English sidebar's short-time display (h:mm tt). Status
/// console timestamps deliberately use a separate, zero-padded format.
pub fn display_time(time: DateTime<Utc>) -> String {
    time.with_timezone(&chrono_tz::America::New_York)
        .format("%-I:%M %p")
        .to_string()
}
const PERIODS: [(&str, &str); 5] = [
    ("Today", "Today"),
    ("Yesterday", "Yesterday"),
    ("Tomorrow", "Tomorrow"),
    ("This Week", "ThisWeek"),
    ("Next Week", "NextWeek"),
];
#[derive(Clone, Default)]
pub struct SidebarCalendar {
    period: usize,
    pub country: String,
    countries: Vec<String>,
    date: String,
    rows: Vec<CalendarRow>,
    times: Vec<DateTime<Utc>>,
    selected: String,
}
impl SidebarCalendar {
    /// Returns true when a backend refresh is needed. A row selection is local.
    pub fn action(&mut self, action: u32) -> Result<bool, String> {
        let index = (action & 0x0fff_ffff) as usize;
        match action & 0xf000_0000 {
            0x8000_0000 => {
                if index >= PERIODS.len() {
                    return Err("Invalid calendar period".into());
                }
                self.period = index;
                self.selected.clear();
                Ok(true)
            }
            0x9000_0000 => {
                self.country = self
                    .countries
                    .get(index)
                    .ok_or("Calendar country is no longer available")?
                    .clone();
                self.selected.clear();
                Ok(true)
            }
            0xa000_0000 => {
                let row = self
                    .rows
                    .get(index)
                    .ok_or("Calendar row is no longer available")?;
                self.selected = row.id.clone();
                self.date = self.times[index]
                    .with_timezone(&chrono_tz::America::New_York)
                    .format("%A, %B %d, %Y")
                    .to_string();
                Ok(false)
            }
            _ => Err("Unsupported calendar action".into()),
        }
    }
    pub async fn refresh(mut self, backend: &Backend, registry: &Registry) -> Result<Self, String> {
        let result = tokio::time::timeout(backend.default_timeout(), async {
            let session = BusinessContext::load(backend).await?;
            let day = session
                .as_of_utc
                .with_timezone(&chrono_tz::America::New_York)
                .date_naive();
            let midnight = eastern_midnight(day)?;
            let values = actor_requests::query(
                backend,
                registry,
                &format!("{ROOT}Queries.GetEconomicCalendarCountryCodesQuery"),
                "EconomicCalendarCountryCodes",
                BTreeMap::new(),
            )
            .await?;
            let named = registry.project(
                &format!("{ROOT}ViewModels.EconomicCalendarCountryCodeReadModel[]"),
                &values,
            )?;
            self.countries = named
                .as_array()
                .ok_or("Invalid calendar countries")?
                .iter()
                .map(|c| {
                    c["CountryCode"]
                        .as_str()
                        .filter(|s| !s.is_empty())
                        .map(str::to_string)
                        .ok_or("Invalid country".to_string())
                })
                .collect::<Result<Vec<_>, _>>()?;
            if !self.countries.contains(&self.country) {
                self.country = self
                    .countries
                    .iter()
                    .find(|s| s.as_str() == "US")
                    .or(self.countries.first())
                    .cloned()
                    .ok_or("No calendar countries are published")?;
            }
            let (_, period) = PERIODS[self.period];
            let query = actor_requests::query(
                backend,
                registry,
                &format!("{ROOT}Queries.GetEconomicCalendarQuery"),
                &format!(
                    "{}.{period}.{}",
                    midnight.format("%Y-%m-%d:%I-%M"),
                    self.country
                ),
                BTreeMap::from([
                    ("TodaysDate".into(), timestamp_wire(midnight)),
                    ("CalendarViewType".into(), (self.period as i64).into()),
                    ("CountryCode".into(), self.country.clone().into()),
                ]),
            )
            .await?;
            let named = registry.project(
                &format!("{ROOT}ViewModels.EconomicCalendarReadModel[]"),
                &query,
            )?;
            let mut rows = Vec::new();
            let mut times = Vec::new();
            for event in named.as_array().ok_or("Invalid calendar rows")? {
                let time = DateTime::parse_from_rfc3339(
                    event["EventDate"]
                        .as_str()
                        .ok_or("Missing calendar instant")?,
                )
                .map_err(|_| "Invalid event time")?
                .to_utc();
                let country = event["CountryCode"]
                    .as_str()
                    .ok_or("Missing event country")?
                    .to_owned();
                let name = event["EventName"]
                    .as_str()
                    .ok_or("Missing event name")?
                    .to_owned();
                let text = |key: &str| event[key].as_str().unwrap_or_default().to_owned();
                rows.push(CalendarRow {
                    id: format!(
                        "{}.{:07}Z|{country}|{name}",
                        time.format("%Y-%m-%dT%H:%M:%S"),
                        time.timestamp_subsec_nanos() / 100
                    ),
                    time: display_time(time),
                    country,
                    event: name,
                    actual: text("Actual"),
                    forecast: text("Forecast"),
                    prior: text("Prior"),
                });
                times.push(time);
            }
            let date = actor_requests::query(
                backend,
                registry,
                &format!("{ROOT}Queries.GetEconomicCalendarDateQuery"),
                &format!("{}.{period}", midnight.format("%Y-%m-%d")),
                BTreeMap::from([
                    ("TodaysDate".into(), timestamp_wire(midnight)),
                    ("CalendarViewType".into(), (self.period as i64).into()),
                ]),
            )
            .await?;
            self.date = date
                .as_str()
                .ok_or("Missing calendar date caption")?
                .to_owned();
            self.rows = rows;
            self.times = times;
            self.selected = self.rows.first().map(|r| r.id.clone()).unwrap_or_default();
            Ok::<_, String>(self)
        })
        .await
        .map_err(|_| "Calendar refresh deadline expired".to_string())?;
        result
    }
    /// Merge calendar fields only. Status logs, feed state and errors have their
    /// own owners and cannot be overwritten by a late calendar reply.
    pub fn publish(&self, publication: &mut SidebarPublication) {
        publication.period = PERIODS[self.period].0.into();
        publication.country = self.country.clone();
        publication.countries = self.countries.clone();
        publication.date = self.date.clone();
        publication.calendar_rows = self.rows.clone();
        publication.selected_id = self.selected.clone();
    }
}
