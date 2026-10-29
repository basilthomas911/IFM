//! Preferred-expiry cache capture choices. A missing candidate in one complete
//! expiry may fall back to the next; expiry universes are never mixed in ranking.
use super::preparation::ReviewedScope;
use crate::dataset::options_owner::CacheScope;
use std::collections::{BTreeMap, BTreeSet};
/// Returns complete memberships in the existing background planner order.
///
/// # Errors
/// Missing metadata, invalid ranking/date or incomplete membership.
pub fn choices(
    cache: &CacheScope,
    reviewed: &ReviewedScope,
    date: time::Date,
) -> Result<Vec<BTreeSet<String>>, String> {
    let Some(ranking) = &cache.ranking else {
        return Ok(vec![cache.contracts.clone()]);
    };
    let bias = ranking.bias()?;
    if ranking.date != date {
        return Err("Cache ranking ValueDate changed".into());
    }
    let mut groups = BTreeMap::<(time::Date, Vec<u16>), BTreeSet<String>>::new();
    for contract in &cache.contracts {
        let definition = reviewed
            .definitions
            .iter()
            .find(|d| d.blueprint.contract.contract() == contract)
            .ok_or("Native cache complete definition unavailable")?;
        let expiry = definition
            .blueprint
            .calendar
            .calendar_date(definition.blueprint.contract.expiration())
            .map_err(|_| "Native cache expiration date unavailable")?;
        let dte = (expiry - date).whole_days();
        if dte < i64::from(bias.minimum) || dte > i64::from(bias.maximum) {
            continue;
        }
        groups
            .entry((
                expiry,
                definition
                    .blueprint
                    .contract
                    .underlying()
                    .encode_utf16()
                    .collect(),
            ))
            .or_default()
            .insert(contract.clone());
    }
    let mut groups = groups.into_iter().collect::<Vec<_>>();
    groups.sort_by_key(|((expiry, underlying), _)| {
        (
            ranking
                .parameters
                .biases
                .iter()
                .filter(|b| b.enabled)
                .map(|b| (((*expiry - date).whole_days()) - i64::from(b.preferred)).abs())
                .min()
                .unwrap_or(i64::MAX),
            *expiry,
            underlying.clone(),
        )
    });
    Ok(groups.into_iter().map(|(_, contracts)| contracts).collect())
}
