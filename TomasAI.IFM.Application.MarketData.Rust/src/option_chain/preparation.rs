//! Native reference acquisition and reusable scope preparation blueprints.
//! Durable desired inputs contain no managed object or callback. Each acquisition
//! borrows its dataset cancellation token and the original recovery deadline.

use super::{
    context::Context,
    convention::{Convention, Failure},
};
use crate::{
    reference::{
        calendar::Calendar,
        treasury_curve::Treasury,
        treasury_policy::{Conversion, Observation, Publications},
    },
    runtime::{Deadline, clock::Clock},
};
use std::sync::Arc;
use tokio_util::sync::CancellationToken;

/// Retained immutable inputs needed to rebuild one option's pricing references
/// after an arbitrary dataset generation change. Imported configuration is copied.
#[derive(Clone)]
pub struct Blueprint {
    /// Complete reviewed convention and physical identity.
    pub contract: Convention,
    /// Complete exchange trading calendar.
    pub calendar: Calendar,
    /// Complete reviewed Treasury publication schedule.
    pub publications: Publications,
    /// Existing source conversion policy.
    pub conversion: Conversion,
    /// Reviewed maximum permitted source clock lead.
    pub maximum_lead: i32,
}
/// Dataset-global reviewed reference policy, independent of any single option.
/// It can be retained before native background discovery returns definitions.
#[derive(Clone)]
pub struct RawPolicy {
    /// Complete reviewed Treasury publication schedule.
    pub publications: Publications,
    /// Exact reviewed source conversion policy.
    pub conversion: Conversion,
    /// Permitted observed source clock lead in milliseconds.
    pub maximum_lead: i32,
}
impl RawPolicy {
    /// Builds exact immutable reconstruction definitions entirely in Rust.
    /// No acquired numerical context or connection becomes part of this result.
    ///
    /// # Errors
    /// Missing review evidence, conflicting identity, invalid strike/calendar.
    pub fn definitions(
        &self,
        date: time::Date,
        models: &[crate::reference::option_contract::OptionContract],
    ) -> Result<Vec<Definition>, String> {
        if models.is_empty() || models.len() > 2048 {
            return Err("Invalid raw definition capacity".into());
        }
        let mut identities = std::collections::BTreeSet::new();
        models
            .iter()
            .map(|model| {
                let contract = model.convention()?;
                if !identities.insert(contract.instrument()) {
                    return Err("Conflicting raw option instrument".into());
                }
                let underlying = model.underlying_source()?.0;
                let value = contract.value();
                let fields = value.as_array().ok_or("Invalid native convention")?;
                Ok(Definition {
                    instrument: contract.instrument(),
                    underlying,
                    strike: model.exact_strike()?,
                    call: fields[28].as_i64() == Some(1),
                    required: true,
                    blueprint: Blueprint {
                        contract,
                        calendar: model.calendar(date)?,
                        publications: self.publications.clone(),
                        conversion: self.conversion.clone(),
                        maximum_lead: self.maximum_lead,
                    },
                })
            })
            .collect()
    }
}
/// Native HTTP/reference service shared by native dataset preparations. Its cache
/// contains detached observations only; cancellation never waits for a managed
/// consumer, FFI completion, database acknowledgement or market publication.
pub struct References {
    treasury: Arc<Treasury>,
    clock: Arc<Clock>,
}
/// One complete business input, retained by the Rust scope for later rebuilds.
#[derive(Clone)]
pub struct Definition {
    /// Provider option identity, verified against the physical plan.
    pub instrument: u32,
    /// Exact physical underlying quote dependency.
    pub underlying: u32,
    /// Requested strike in exact CLR decimal units.
    pub strike: super::qualification::Decimal,
    /// Requested call/put side.
    pub call: bool,
    /// Current full-risk demand, independent of provider subscriptions.
    pub required: bool,
    /// Complete native reference reconstruction inputs.
    pub blueprint: Blueprint,
}
/// Complete scope reference intent. It can survive physical dataset replacement
/// without calling C# to renew leases or rebuild pricing contexts.
#[derive(Clone)]
pub struct ReviewedScope {
    /// Runtime-wide retained evidence byte authority shared by every scope.
    pub manifests: Arc<super::manifest::Budget>,
    /// Native-owned HTTP/reference acquisition.
    pub references: Arc<References>,
    /// Bounded complete scope definitions.
    pub definitions: Vec<Definition>,
    /// Production mode supplied by immutable host configuration.
    pub production: bool,
    /// Explicit reviewed production approval, not inferred from numeric cadence.
    pub production_approved: bool,
    /// Reviewed policy evidence identity, required in production.
    pub review_evidence: Option<String>,
    /// Immutable reviewed cadence policy version.
    pub policy_version: String,
    /// Selection cadence, milliseconds.
    pub selection_ms: u32,
    /// Implied-volatility refresh cadence, milliseconds.
    pub iv_ms: u32,
    /// Full-risk cadence, milliseconds.
    pub risk_ms: u32,
    /// Maximum admitted contracts from the reviewed policy.
    pub maximum_contracts: usize,
    /// Maximum cooperative numerical batch.
    pub batch: usize,
    /// Copied persisted source statistics, not a storage callback.
    pub statistics: Vec<(u32, super::statistics::Seed)>,
}
/// One completely prepared native scope batch. No partially prepared option is
/// admitted; the caller commits numeric inputs, manifests and authority together.
pub struct PreparedBatch {
    /// Existing native scheduler configuration.
    pub configuration: super::owner::PricingConfiguration,
    /// Typed immutable calendar/rate authority for every numerical context.
    pub references: std::collections::BTreeMap<u32, Arc<Context>>,
}
impl ReviewedScope {
    /// Prepares every option with the same original cancellation/deadline. The
    /// shared source's native bounded cache prevents a network call per contract.
    ///
    /// # Errors
    /// Invalid/partial/duplicate definitions never become physical subscriptions.
    pub async fn prepare(
        &self,
        registrations: &[crate::dataset::live::Registration],
        generation: u128,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<PreparedBatch, String> {
        use std::collections::{BTreeMap, BTreeSet};
        if registrations.len() > 2112
            || generation == 0
            || self.definitions.is_empty()
            || !(1..=2048).contains(&self.maximum_contracts)
            || self.definitions.len() > self.maximum_contracts
            || !(10..=5000).contains(&self.selection_ms)
            || !(self.selection_ms..=60000).contains(&self.iv_ms)
            || !(10..=60000).contains(&self.risk_ms)
            || !(1..=64).contains(&self.batch)
            || self.policy_version.trim().is_empty()
            || self.policy_version.len() > 1024
            || self.statistics.len() > 2048
        {
            return Err("Invalid native reviewed scope bounds".into());
        }
        if self.production
            && (!self.production_approved
                || self.policy_version == "OptionRefresh/TestDefaults/v1"
                || self
                    .review_evidence
                    .as_ref()
                    .is_none_or(|v| v.trim().is_empty() || v.len() > 16384))
        {
            return Err("Option pricing refresh settings require a reviewed production policy and evidence identity".into());
        }
        let operation = async {
            let mut ids = BTreeSet::new();
            let mut references = BTreeMap::new();
            let mut options = Vec::with_capacity(self.definitions.len());
            let mut hashes = Vec::with_capacity(self.definitions.len());
            for definition in &self.definitions {
                if !ids.insert(definition.instrument) {
                    return Err("Duplicate native reviewed option".into());
                }
                let contract = definition.blueprint.contract.value();
                let fields = contract.as_array().ok_or("Invalid contract DTO")?;
                let physical = registrations.iter().any(|r| {
                    r.instrument == definition.instrument
                        && r.asset == crate::latest::Asset::Option
                        && fields[1].as_str() == Some(r.contract.as_str())
                        && fields[3].as_u64() == Some(u64::from(r.publisher))
                        && fields[4].as_u64() == Some(u64::from(r.instrument))
                });
                let underlying = registrations.iter().any(|r| {
                    r.instrument == definition.underlying
                        && r.asset == crate::latest::Asset::Futures
                        && fields[9].as_str() == Some(r.contract.as_str())
                });
                if !physical || !underlying || fields[2].as_str() != Some("GLBX.MDP3") {
                    return Err("Native reviewed definition disagrees with physical scope".into());
                }
                let reference = Arc::new(
                    Box::pin(self.references.prepare(
                        &definition.blueprint,
                        generation.to_le_bytes(),
                        stop,
                        deadline,
                    ))
                    .await
                    .map_err(|e| format!("Native reference preparation: {e:?}"))?,
                );
                let digest = reference.digest()?;
                let identity = u128::from_le_bytes(
                    crate::messaging::events::Identity::fresh(reference.prepared_at()).event,
                );
                let context = reference.bind(
                    reference.prepared_at(),
                    identity,
                    definition.underlying,
                    definition.strike,
                    definition.call,
                )?;
                let manifest = self.manifests.retain(
                    identity,
                    &reference.manifest(definition.strike, definition.call)?,
                )?;
                options.push(super::owner::PricingRegistration {
                    instrument: definition.instrument,
                    context,
                    required: definition.required,
                    manifest: Some(manifest),
                });
                references.insert(definition.instrument, reference);
                hashes.push((definition.instrument, digest));
                tokio::task::yield_now().await;
            }
            if registrations
                .iter()
                .filter(|r| r.asset == crate::latest::Asset::Option)
                .count()
                != ids.len()
            {
                return Err("Native preparation must cover every physical option".into());
            }
            hashes.sort_by_key(|(instrument, _)| *instrument);
            use crate::reference::semantic::{self, Value as J};
            let digest = semantic::compute(&J::Object(vec![
                ("Version".into(), J::Integer(1)),
                ("Policy".into(), J::String(self.policy_version.clone())),
                (
                    "Contexts".into(),
                    J::Array(
                        hashes
                            .into_iter()
                            .map(|(id, d)| J::Array(vec![J::Unsigned(u64::from(id)), J::String(d)]))
                            .collect(),
                    ),
                ),
            ]))?;
            Ok(PreparedBatch {
                configuration: super::owner::PricingConfiguration {
                    digest,
                    policy_version: self.policy_version.clone(),
                    options,
                    statistics: self.statistics.clone(),
                    selection_ms: self.selection_ms,
                    iv_ms: self.iv_ms,
                    risk_ms: self.risk_ms,
                    batch: self.batch,
                },
                references,
            })
        };
        deadline.run(stop, operation).await.map_err(|e| {
            format!("Native scope preparation original deadline/cancellation: {e:?}")
        })?
    }
}
/// Distinguishes cancellation/deadline from existing financial qualification errors.
#[derive(Debug)]
pub enum Error {
    /// The complete original deadline includes HTTP/DNS, cache wait and parsing.
    Acquisition(crate::historical::Failure),
    /// Native financial qualification produced the existing stable failure code.
    Qualification(Failure),
    /// No valid authoritative API business time is available.
    Clock,
}
impl References {
    /// Holds only Rust-owned reference acquisition and explicit API time.
    pub fn new(treasury: Arc<Treasury>, clock: Arc<Clock>) -> Self {
        Self { treasury, clock }
    }
    /// Acquires and prepares a replacement generation without any managed work.
    /// No per-lease or per-reference short timeout is introduced.
    ///
    /// # Errors
    /// Cancellation/deadline and invalid source/reference data remain observable.
    pub async fn prepare(
        &self,
        blueprint: &Blueprint,
        generation: [u8; 16],
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Context, Error> {
        let operation = async {
            let at = self.clock.utc_ticks().map_err(|_| Error::Clock)?;
            blueprint
                .contract
                .qualify(at, 0)
                .map_err(Error::Qualification)?;
            let date = blueprint.publications.valuation_date(at).map_err(|code| {
                Error::Qualification(Failure {
                    code,
                    input: "Treasury",
                })
            })?;
            let observation = Box::pin(self.treasury.latest(date, stop, deadline))
                .await
                .map_err(Error::Acquisition)?
                .map(Observation::official);
            // Acquisition may finish after the old captured instant. Use the API
            // clock after acquisition so fresh data cannot be mistaken for future
            // knowledge. Controlled backtest time remains explicitly controlled.
            let at = self.clock.utc_ticks().map_err(|_| Error::Clock)?;
            Context::prepare(
                blueprint.contract.clone(),
                blueprint.calendar.clone(),
                &blueprint.publications,
                &blueprint.conversion,
                observation.as_ref(),
                generation,
                blueprint.contract.engine(),
                at,
                blueprint.maximum_lead,
            )
            .map_err(Error::Qualification)
        };
        deadline
            .run(stop, operation)
            .await
            .map_err(|e| Error::Acquisition(crate::historical::Failure::Interrupted(e)))?
    }
}
