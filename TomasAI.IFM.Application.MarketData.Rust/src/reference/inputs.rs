//! Copied complete reference inputs shared by live scope and journal import.
//! Parsing and validation are native; no prepared rate/context/lease is imported.
use super::{
    calendar::Calendar,
    treasury_policy::{Conversion, Publication, Publications},
};
use crate::option_chain::preparation::RawPolicy;
use serde_json::Value;
use sha2::{Digest, Sha256};
/// Immutable reference recipe, independent of provider resources and generation.
#[derive(Clone)]
pub struct Inputs {
    /// Exact supplied exchange calendar, including caller-controlled trading dates.
    pub calendar: Calendar,
    /// Exact reviewed publication and conversion policy, without a selected rate.
    pub policy: RawPolicy,
    /// Identity of the complete reference recipe, excluding deployment clock lead.
    pub signature: [u8; 32],
}
fn text(value: &Value) -> Result<String, String> {
    value
        .as_str()
        .filter(|s| !s.trim().is_empty() && s.len() <= 16384)
        .map(str::to_owned)
        .ok_or("Invalid raw reference text".into())
}
fn number(value: &Value) -> Result<i64, String> {
    value.as_i64().ok_or("Invalid raw reference number".into())
}
fn day(value: &Value) -> Result<time::Date, String> {
    number(value)?
        .checked_add(1721426)
        .and_then(|day| i32::try_from(day).ok())
        .and_then(|day| time::Date::from_julian_day(day).ok())
        .filter(|day| (1..=9999).contains(&day.year()))
        .ok_or("Invalid raw reference date".into())
}
/// Parses the existing complete exchange-calendar fields without normalizing
/// timezone spelling or inventing dates from the underlying symbol.
///
/// # Errors
/// Unsupported date/zone, unordered/duplicate dates or more than 2,000 dates.
pub fn calendar(value: &Value) -> Result<Calendar, String> {
    let dates = value["dates"]
        .as_array()
        .filter(|rows| rows.len() <= 2000)
        .ok_or("Invalid raw reference calendar capacity")?;
    Calendar::new(
        text(&value["version"])?,
        &text(&value["timezone"])?,
        day(&value["from"])?,
        day(&value["until"])?,
        value["rollover"]
            .as_u64()
            .ok_or("Invalid raw reference rollover")?,
        dates.iter().map(day).collect::<Result<_, _>>()?,
    )
    .map_err(|_| "Invalid raw reference calendar".into())
}
/// Parses unchanged publication/conversion configuration; Rust later retrieves
/// and qualifies the official curve under the dataset's original deadline.
///
/// # Errors
/// Malformed/non-UTC publication points, invalid bounds/zone or clock lead.
pub fn policy(value: &Value) -> Result<RawPolicy, String> {
    let publication = &value["publication"];
    if number(&publication["from_offset"])? != 0 || number(&publication["until_offset"])? != 0 {
        return Err("Non-UTC raw publication coverage".into());
    }
    let rows = publication["entries"]
        .as_array()
        .filter(|rows| !rows.is_empty() && rows.len() <= 1000)
        .ok_or("Invalid raw publication capacity")?;
    let entries = rows
        .iter()
        .map(|row| {
            if number(&row["offset"])? != 0 {
                return Err("Non-UTC raw publication point".into());
            }
            Ok(Publication {
                date: day(&row["date"])?,
                available: number(&row["available"])?,
            })
        })
        .collect::<Result<Vec<_>, String>>()?;
    let publications = Publications::new(
        text(&publication["version"])?,
        number(&publication["from"])?,
        number(&publication["until"])?,
        entries,
        &text(&publication["timezone"])?,
    )?;
    let conversion = &value["conversion"];
    let conversion = Conversion {
        source: text(&conversion["source"])?,
        series: text(&conversion["series"])?,
        convention: i32::try_from(number(&conversion["convention"])?)
            .map_err(|_| "Invalid raw rate convention")?,
        version: text(&conversion["version"])?,
        evidence: text(&conversion["evidence"])?,
    };
    let maximum_lead = i32::try_from(number(&value["maximum_lead"])?)
        .map_err(|_| "Invalid raw reference clock lead")?;
    if !(0..=2000).contains(&maximum_lead) {
        return Err("Invalid raw reference clock lead".into());
    }
    Ok(RawPolicy {
        publications,
        conversion,
        maximum_lead,
    })
}
impl Inputs {
    /// Validates a complete copied recipe. Physical context/rate acquisition remains
    /// native; this data can survive cancellation and joined dataset replacement.
    ///
    /// # Errors
    /// Invalid calendar, publication, conversion or bounded input representation.
    pub fn from_value(value: &Value) -> Result<Self, String> {
        let calendar = calendar(&value["calendar"])?;
        let policy = policy(value)?;
        let signature=Sha256::digest(serde_json::to_vec(&serde_json::json!({"calendar":value["calendar"],"publication":value["publication"],"conversion":value["conversion"]})).map_err(|_|"Invalid raw reference signature")?).into();
        Ok(Self {
            calendar,
            policy,
            signature,
        })
    }
}
