//! Validated copied browser commands. No UI date, local clock, provider handle
//! or managed lease is accepted. The authoritative native API date is explicit.
use crate::option_chain::qualification::Decimal;
use crate::reference::decimal;
use serde_json::Value;
use std::collections::BTreeSet;
/// Stable family is one exact underlying/expiry; a view contributes its own
/// required legs. Multiple views must not overwrite each other's demand.
#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub struct Family {
    /// Canonical underlying.
    pub contract: String,
    /// CLR expiry day number.
    pub expiry: i32,
}
/// Copied command retained below the native market-data owner.
#[derive(Clone)]
pub struct Command {
    /// Exact requested family.
    pub family: Family,
    /// Source root; no fallback or OS date inference.
    pub root: String,
    /// Canonically ordered provider-root filter.
    pub providers: Vec<String>,
    /// Stable UI/business owner (empty retains the published legacy identity).
    pub owner: String,
    /// Full-risk exceptions required by this view.
    pub required: BTreeSet<String>,
    /// Existing positive finite Bollinger multiplier.
    pub multiplier: f64,
    /// Exact subscription buffer wing.
    pub wing: Decimal,
    /// Release only this view, leaving all other family owners intact.
    pub release: bool,
}
fn text(value: &Value, maximum: usize, empty: bool) -> Result<String, String> {
    let value = value
        .as_str()
        .filter(|s| {
            (empty || !s.trim().is_empty())
                && s.encode_utf16().count() <= maximum
                && !s.chars().any(char::is_control)
        })
        .ok_or("Invalid native browser text")?;
    Ok(value.into())
}
impl Command {
    /// Validates the entire copied command before changing desired ownership.
    /// The obsolete strike-count field and unused deviation override are not
    /// consulted, preserving the baseline selector's behavior.
    ///
    /// # Errors
    /// Missing/oversized identities, malformed arrays, invalid required legs,
    /// unsupported scalar values or an invalid explicit expiry.
    pub fn parse(value: &Value) -> Result<Self, String> {
        let family = Family {
            contract: text(&value["contract"], 1024, false)?,
            expiry: value["expiry"]
                .as_i64()
                .and_then(|v| i32::try_from(v).ok())
                .filter(|v| (1..=3652058).contains(v))
                .ok_or("Invalid native browser expiry")?,
        };
        let providers = value["providers"]
            .as_array()
            .filter(|a| a.len() <= 64)
            .ok_or("Native browser provider capacity exceeded")?;
        let mut providers = providers
            .iter()
            .map(|v| text(v, 128, false))
            .collect::<Result<Vec<_>, _>>()?;
        if providers.iter().any(|provider| !provider.is_ascii()) {
            return Err("Invalid native browser provider root".into());
        }
        for provider in &mut providers {
            provider.make_ascii_uppercase();
        }
        providers.sort();
        providers.dedup_by(|a, b| a.eq_ignore_ascii_case(b));
        let rows = value["required"]
            .as_array()
            .filter(|a| a.len() <= 2048)
            .ok_or("Native browser required-leg capacity exceeded")?;
        let required = rows
            .iter()
            .map(|v| text(v, 1024, false))
            .collect::<Result<BTreeSet<_>, _>>()?;
        // Baseline Distinct handles repeated required IDs, so duplicates collapse.
        let multiplier = value["multiplier"]
            .as_f64()
            .filter(|v| v.is_finite() && *v > 0.0)
            .ok_or("Invalid native browser window multiplier")?;
        let wing = value["wing"]
            .as_str()
            .and_then(decimal::parse)
            .ok_or("Invalid native browser wing")?;
        Ok(Self {
            family,
            root: text(&value["root"], 60, false)?,
            providers,
            owner: text(&value["owner"], 1024, true)?,
            required,
            multiplier,
            wing,
            release: value["release"]
                .as_bool()
                .ok_or("Invalid native browser release flag")?,
        })
    }
}
