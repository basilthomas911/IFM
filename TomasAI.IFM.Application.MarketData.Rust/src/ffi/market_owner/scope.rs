//! Copied complete scope demand. Provider identities and membership are derived
//! from native-reviewed definitions; the managed adapter supplies no lease handle.
use super::super::runtime::Status;
use crate::{
    dataset::{option_claims::Scope, options_owner::CacheScope},
    option_chain::preparation::ReviewedScope,
    subscriptions::leases::Lifetime,
};
use serde_json::Value;
use std::{collections::BTreeSet, time::Duration};
pub(super) struct Claim {
    pub owner: u128,
    pub id: String,
    pub scope: Scope,
    pub lifetime: Lifetime,
}
pub(super) fn parse(value: &Value, reviewed: &ReviewedScope, now: i64) -> Result<Claim, Status> {
    let owner = super::business(value)?;
    let id = super::text(value, "id", 512)?.to_owned();
    if id.encode_utf16().count() > 128 {
        return Err(Status::InvalidInput);
    }
    let full_risk = value["full_risk"].as_bool().ok_or(Status::InvalidInput)?;
    let options = reviewed
        .definitions
        .iter()
        .map(|d| d.instrument)
        .collect::<BTreeSet<_>>();
    if options.is_empty() || options.len() != reviewed.definitions.len() {
        return Err(Status::InvalidInput);
    }
    let monitored = if full_risk {
        options.clone()
    } else {
        BTreeSet::new()
    };
    let mut caches = Vec::new();
    if !value["horizon"].is_null() {
        let horizon = super::text(value, "horizon", 16)?;
        if !matches!(horizon, "Daily" | "Weekly" | "Monthly") {
            return Err(Status::InvalidInput);
        }
        caches.push(CacheScope {
            scope: id.clone(),
            horizon: horizon.into(),
            ranking: None,
            contracts: reviewed
                .definitions
                .iter()
                .map(|d| d.blueprint.contract.contract().to_owned())
                .collect(),
        });
    }
    let lifetime = if value["expires_ticks"].is_null() {
        Lifetime::Durable
    } else {
        if value["expires_offset"].as_f64() != Some(0.0) {
            return Err(Status::InvalidInput);
        }
        let ticks = value["expires_ticks"]
            .as_i64()
            .and_then(|until| until.checked_sub(now))
            .filter(|ticks| *ticks > 0 && *ticks <= 1_200_000_000)
            .ok_or(Status::InvalidInput)?;
        Lifetime::Ephemeral(Duration::from_nanos(
            u64::try_from(ticks).map_err(|_| Status::InvalidInput)? * 100,
        ))
    };
    Ok(Claim {
        owner,
        id,
        scope: Scope {
            options,
            monitored,
            caches,
        },
        lifetime,
    })
}
