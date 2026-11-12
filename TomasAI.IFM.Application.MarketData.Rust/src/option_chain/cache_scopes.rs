//! Preferred-expiry cache capture choices. A missing candidate in one complete
//! expiry may fall back to the next; expiry universes are never mixed in ranking.
use super::preparation::ReviewedScope;
use crate::dataset::options_owner::CacheScope;
use std::collections::{BTreeMap, BTreeSet};

/// Selects exactly the requested members from the worker's reviewed universe.
pub(crate) fn members<'a, T>(
    contracts: &BTreeSet<String>,
    definitions: &BTreeMap<&str, &'a T>,
) -> Result<Vec<&'a T>, String> {
    if contracts.is_empty() || contracts.len() > super::MAX_LIVE_OPTIONS {
        return Err(format!("invalid cache membership size {}; maximum {}", contracts.len(), super::MAX_LIVE_OPTIONS));
    }
    contracts.iter().map(|id| definitions.get(id.as_str()).copied()
        .ok_or_else(|| format!("cache contract {id:?} is absent from reviewed definitions")))
        .collect()
}
/// Returns complete memberships in the existing background planner order.
///
/// # Errors
/// Missing metadata, invalid ranking/date or incomplete membership.
pub fn choices(
    cache: &CacheScope,
    reviewed: &ReviewedScope,
    date: time::Date,
) -> Result<Vec<BTreeSet<String>>, String> {
    if cache.scope.trim().is_empty() || cache.scope.encode_utf16().count() > 128
        || !matches!(cache.horizon.as_str(), "Daily" | "Weekly" | "Monthly")
    {
        return Err("invalid cache scope or horizon".into());
    }
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn scopes_resolve_independently_in_a_worker_over_the_former_limit() {
        let ids = (0..3_000).map(|id| format!("option-{id}")).collect::<Vec<_>>();
        let definitions = ids.iter().map(|id| (id.as_str(), id)).collect::<BTreeMap<_, _>>();
        let first = ids[..2_500].iter().cloned().collect();
        let invalid = [ids[2_500].clone(), "absent-option".into()].into_iter().collect();
        let last = ids[2_500..].iter().cloned().collect();

        assert_eq!(members(&first, &definitions).unwrap().len(), 2_500);
        assert!(members(&invalid, &definitions).unwrap_err().contains("absent-option"));
        assert_eq!(members(&last, &definitions).unwrap().len(), 500);
    }

    #[test]
    fn a_scope_cannot_exceed_the_shared_feed_capacity() {
        let ids = (0..=super::super::MAX_LIVE_OPTIONS)
            .map(|id| format!("option-{id}")).collect::<BTreeSet<_>>();
        let definitions = ids.iter().map(|id| (id.as_str(), id)).collect();
        assert!(members(&ids, &definitions).unwrap_err().contains("maximum 4096"));
        let full = ids.iter().take(super::super::MAX_LIVE_OPTIONS).cloned().collect();
        assert_eq!(members(&full, &definitions).unwrap().len(), super::super::MAX_LIVE_OPTIONS);
    }
}
