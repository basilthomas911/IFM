//! Native selection of stored EOD/Bollinger fallback facts. The passive storage
//! responder copies every candidate; eligibility and date policy execute here.
use super::qualification::Decimal;
use crate::reference::decimal;
use serde_json::Value;
/// Existing selected fallback values; these do not qualify executable pricing.
#[derive(Debug)]
pub struct Inputs {
    /// Stored fallback price, overridden only by a separately native live mark.
    pub price: Option<Decimal>,
    /// Positive selected daily/indicator standard deviation.
    pub deviation: Option<Decimal>,
    /// Original source observation date in CLR day numbers.
    pub date: Option<i32>,
}
struct Eod {
    date: i32,
    close: Decimal,
    deviation: f64,
}
struct Band {
    price: Decimal,
    deviation: Option<Decimal>,
    valid: bool,
    date: i32,
}
fn day(value: &Value) -> Result<i32, &'static str> {
    value
        .as_i64()
        .and_then(|v| i32::try_from(v).ok())
        .filter(|v| (0..=3652058).contains(v))
        .ok_or("Invalid raw browser date")
}
fn amount(value: &Value) -> Result<Decimal, &'static str> {
    value
        .as_str()
        .and_then(decimal::parse)
        .ok_or("Invalid raw browser decimal")
}
fn eod(value: &Value) -> Result<Option<Eod>, &'static str> {
    if value.is_null() {
        return Ok(None);
    }
    let row = value
        .as_array()
        .filter(|v| v.len() == 3)
        .ok_or("Malformed raw browser EOD")?;
    let bits = row[2]
        .as_str()
        .filter(|s| s.len() == 16)
        .and_then(|s| u64::from_str_radix(s, 16).ok())
        .ok_or("Invalid raw EOD deviation bits")?;
    Ok(Some(Eod {
        date: day(&row[0])?,
        close: amount(&row[1])?,
        deviation: f64::from_bits(bits),
    }))
}
fn band(value: &Value) -> Result<Option<Band>, &'static str> {
    if value.is_null() {
        return Ok(None);
    }
    let row = value
        .as_array()
        .filter(|v| v.len() == 4)
        .ok_or("Malformed raw browser band")?;
    Ok(Some(Band {
        price: amount(&row[0])?,
        deviation: if row[1].is_null() {
            None
        } else {
            Some(amount(&row[1])?)
        },
        valid: row[2].as_bool().ok_or("Invalid raw browser validity")?,
        date: day(&row[3])?,
    }))
}
fn positive(value: Decimal) -> bool {
    value
        .compare(Decimal::default())
        .is_some_and(std::cmp::Ordering::is_gt)
}
fn deviation(value: &Eod) -> bool {
    value.deviation.is_finite() && value.deviation > 0.0
}
/// Applies the unchanged EOD -> daily band -> five-minute band policy using the
/// caller's API ValueDate. Nonfinite EOD deviations remain raw facts, not JSON
/// parser exceptions or permission to infer a different trading date.
///
/// # Errors
/// Invalid copied fact shapes, date/scalar bounds or CLR decimal overflow.
pub fn select(bytes: &[u8], date: i32) -> Result<Inputs, &'static str> {
    if bytes.len() > 65536 || !(1..=3652058).contains(&date) {
        return Err("Invalid raw browser input bounds");
    }
    let value: Value = serde_json::from_slice(bytes).map_err(|_| "Malformed raw browser inputs")?;
    let row = value
        .as_array()
        .filter(|v| v.len() == 4)
        .ok_or("Malformed raw browser input tuple")?;
    let current = eod(&row[0])?;
    let previous = eod(&row[1])?;
    let daily = band(&row[2])?;
    let intraday = band(&row[3])?;
    let selected = current
        .as_ref()
        .filter(|e| deviation(e))
        .or(previous.as_ref());
    let fallback = daily
        .as_ref()
        .map(|b| b.price)
        .or_else(|| {
            current
                .as_ref()
                .filter(|e| positive(e.close))
                .map(|e| e.close)
        })
        .or_else(|| selected.filter(|e| positive(e.close)).map(|e| e.close));
    if let Some(eod) = selected.filter(|e| deviation(e))
        && fallback.is_some_and(positive)
    {
        return Ok(Inputs {
            price: fallback,
            deviation: Some(
                decimal::from_double(eod.deviation)
                    .ok_or("Stored deviation overflows CLR Decimal")?,
            ),
            date: Some(eod.date),
        });
    }
    if let Some(band) = daily
        .as_ref()
        .filter(|b| b.valid && b.date == date && b.deviation.is_some_and(positive))
    {
        return Ok(Inputs {
            price: Some(band.price),
            deviation: band.deviation,
            date: Some(date),
        });
    }
    if let Some(band) = intraday.as_ref().filter(|b| {
        b.valid && b.date == date && positive(b.price) && b.deviation.is_some_and(positive)
    }) {
        return Ok(Inputs {
            price: Some(band.price),
            deviation: band.deviation,
            date: Some(date),
        });
    }
    Ok(Inputs {
        price: fallback,
        deviation: None,
        date: current
            .as_ref()
            .map(|e| e.date)
            .or_else(|| selected.map(|e| e.date)),
    })
}
