//! Typed native option-source construction shared by commands and background
//! discovery. No JSON dispatcher, managed identity mapping or source lease is used.
use super::{
    live::{Evidence, Registration, Start},
    transport::{Lane, SessionPlan, SubscriptionSpec},
};
use crate::{latest::Asset, reference::option_contract::OptionContract};
use databento::dbn::{SType, Schema};
use std::collections::{BTreeMap, BTreeSet};
/// Complete bounded physical plan derived from exact reviewed native models.
pub struct Plan {
    /// Exact canonical/provider identities, ordered by instrument.
    pub registrations: Vec<Registration>,
    /// Trades before quotes, preserving authoritative evidence ordering.
    pub session: SessionPlan,
}
impl Plan {
    /// Validates all bindings before producing a subscription plan. Shared
    /// underlyings are deduplicated; conflicting physical identities reject all.
    ///
    /// # Errors
    /// Missing review evidence, wrong dataset or conflicting/excessive bindings.
    pub fn from_models(models: &[OptionContract]) -> Result<Self, String> {
        if models.is_empty() || models.len() > 2048 {
            return Err("Invalid native option model capacity".into());
        }
        let mut registrations = BTreeMap::new();
        let mut canonical = BTreeSet::new();
        for model in models {
            let contract = model.convention()?;
            let (underlying, publisher, id) = model.underlying_source()?;
            if contract.dataset() != Lane::OptionsGlbx.dataset()
                || contract.instrument() == underlying
                || !canonical.insert(contract.contract().to_owned())
            {
                return Err("Conflicting native option identity/dataset".into());
            }
            for row in [
                Registration {
                    instrument: contract.instrument(),
                    publisher: contract.publisher(),
                    contract: contract.contract().to_owned(),
                    source: contract.instrument().to_string(),
                    asset: Asset::Option,
                    tick: None,
                    vwap: false,
                    required: true,
                },
                Registration {
                    instrument: underlying,
                    publisher,
                    contract: id.to_owned(),
                    source: underlying.to_string(),
                    asset: Asset::Futures,
                    tick: None,
                    vwap: false,
                    required: true,
                },
            ] {
                if registrations
                    .insert(row.instrument, row.clone())
                    .is_some_and(|old| old != row)
                {
                    return Err("Conflicting native provider binding".into());
                }
            }
        }
        if registrations.len() > 2112 {
            return Err("Native option dependency capacity exceeded".into());
        }
        let symbols = registrations.keys().map(u32::to_string).collect::<Vec<_>>();
        let session = SessionPlan {
            lane: Lane::OptionsGlbx,
            subscriptions: [Schema::Trades, Schema::Mbp1]
                .into_iter()
                .map(|schema| SubscriptionSpec {
                    symbols: symbols.clone(),
                    symbology: SType::InstrumentId,
                    schema,
                    replay_start_ns: None,
                })
                .collect(),
        };
        session
            .validate()
            .map_err(|e| format!("Invalid native option session: {e:?}"))?;
        Ok(Self {
            registrations: registrations.into_values().collect(),
            session,
        })
    }
    /// Binds copied provider configuration to this native plan. All future
    /// socket/task ownership is created beneath the calling native dataset child.
    pub fn source(self, template: &Start) -> Start {
        Start {
            plan: self.session,
            registrations: self.registrations,
            evidence: Evidence::Observations,
            ..template.clone()
        }
    }
}
