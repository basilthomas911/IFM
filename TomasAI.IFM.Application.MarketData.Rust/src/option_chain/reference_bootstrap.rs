//! Dataset-owned raw-reference acquisition. C# storage supplies copied calendar
//! and immutable convention facts only. Native validation, caching, context
//! construction, renewal and source recovery remain under the dataset owner.
use super::{
    convention::Convention,
    preparation::{Blueprint, Definition, ReviewedScope},
    universe::{self, Candidate},
};
use crate::{
    messaging::{codec, publisher::BrokerSettings, raw_io},
    reference::calendar::Calendar,
    runtime::{Deadline, clock::Clock, owner::Child},
};
use sha2::{Digest, Sha256};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};
use tokio_util::sync::CancellationToken;
/// Native copied-fact configuration. The cache contains immutable values only;
/// it cannot retain a provider socket, task, cancellation owner or managed lease.
pub struct Bootstrap {
    broker: BrokerSettings,
    scope: String,
    cache: Mutex<Option<(String, Vec<Definition>)>>,
    raw_models: Mutex<BTreeMap<String, Arc<crate::reference::option_contract::OptionContract>>>,
    pinned_calendars: Mutex<std::collections::BTreeSet<String>>,
}
impl Bootstrap {
    /// Creates raw fact configuration without broker/database I/O.
    ///
    /// # Errors
    /// Invalid existing deployment subject scope.
    pub fn new(broker: BrokerSettings, scope: String) -> Result<Self, &'static str> {
        if scope.is_empty()
            || scope.len() > 128
            || !scope
                .bytes()
                .all(|v| v.is_ascii_alphanumeric() || v == b'-')
        {
            return Err("Invalid raw reference scope");
        }
        Ok(Self {
            broker,
            scope,
            cache: Mutex::new(None),
            raw_models: Mutex::new(BTreeMap::new()),
            pinned_calendars: Mutex::default(),
        })
    }
    /// Marks the validated copied calendar as the exact frozen recipe for this
    /// contract. It is not replaced by a fresh database calendar during reset.
    ///
    /// # Errors
    /// Unknown raw contract, interrupted state or bounded calendar capacity.
    pub fn pin_calendar(&self, contract: &str) -> Result<(), String> {
        if !self
            .raw_models
            .lock()
            .map_err(|_| "Raw option model directory interrupted")?
            .contains_key(contract)
        {
            return Err("Frozen calendar lacks its exact raw contract".into());
        }
        let mut pinned = self
            .pinned_calendars
            .lock()
            .map_err(|_| "Frozen calendar directory interrupted")?;
        if pinned.len() == 2048 && !pinned.contains(contract) {
            return Err("Frozen calendar capacity exceeded".into());
        }
        pinned.insert(contract.into());
        Ok(())
    }
    /// Copies raw contract metadata without retaining any dataset resource.
    ///
    /// # Errors
    /// Interrupted copied-metadata directory.
    pub fn option_model(
        &self,
        contract: &str,
    ) -> Result<Option<crate::reference::option_contract::OptionContract>, String> {
        Ok(self
            .raw_models
            .lock()
            .map_err(|_| "Raw option model directory interrupted")?
            .get(contract)
            .map(|model| model.as_ref().clone()))
    }
    /// Retains a completely native-qualified copied contract for first-use
    /// immutable reference staging. It owns no external store or provider handle.
    ///
    /// # Errors
    /// Raw validation failure, conflicting identity or bounded metadata capacity.
    pub fn register_option_model(
        &self,
        model: crate::reference::option_contract::OptionContract,
    ) -> Result<(), String> {
        let convention = model.convention()?;
        let bytes = codec::encode(&model.value())?;
        // The existing raw IO request has a 16 KiB bound. Refuse an oversized
        // first-use staging input before retaining or submitting any work.
        if bytes.len() > 8192 {
            return Err("Native raw option staging input exceeds capacity".into());
        }
        let mut models = self
            .raw_models
            .lock()
            .map_err(|_| "Raw option model directory interrupted")?;
        if let Some(old) = models.get(convention.contract()) {
            if codec::encode(&old.value())? != bytes {
                return Err("Conflicting raw option reference model".into());
            }
            return Ok(());
        }
        if models.len() == 2048 {
            return Err("Raw option model directory capacity exceeded".into());
        }
        models.insert(convention.contract().into(), Arc::new(model));
        Ok(())
    }
    /// Inherits copied metadata for unchanged catalog members. New explicit
    /// models take precedence. Detached validated facts are reused only when the
    /// complete active input signature matches; physical ownership is never inherited.
    ///
    /// # Errors
    /// Interrupted metadata directory or bounded source model capacity.
    pub fn inherit_models(&self, old: &Self) -> Result<(), String> {
        self.inherit_filtered(old, None)
    }
    /// Retains immutable metadata only for the current complete desired union.
    /// Released or old-window definitions cannot accumulate until every future
    /// rebuild fails the finite metadata capacity. No provider state is inherited.
    ///
    /// # Errors
    /// Interrupted copied state or a selected union exceeding 2,048 definitions.
    pub fn inherit_selected_models(
        &self,
        old: &Self,
        wanted: &std::collections::BTreeSet<String>,
    ) -> Result<(), String> {
        self.inherit_filtered(old, Some(wanted))
    }
    fn inherit_filtered(
        &self,
        old: &Self,
        wanted: Option<&std::collections::BTreeSet<String>>,
    ) -> Result<(), String> {
        let mut previous = old
            .raw_models
            .lock()
            .map_err(|_| "Raw option model directory interrupted")?
            .clone();
        if let Some(wanted) = wanted {
            previous.retain(|id, _| wanted.contains(id));
        }
        let mut current = self
            .raw_models
            .lock()
            .map_err(|_| "Raw option model directory interrupted")?;
        let selected_current = current
            .keys()
            .filter(|id| wanted.is_none_or(|wanted| wanted.contains(*id)))
            .collect::<std::collections::BTreeSet<_>>();
        if previous
            .keys()
            .filter(|id| !selected_current.contains(id))
            .count()
            + selected_current.len()
            > 2048
        {
            return Err("Raw option model directory capacity exceeded".into());
        }
        if let Some(wanted) = wanted {
            current.retain(|id, _| wanted.contains(id));
        }
        for (id, model) in previous {
            current.entry(id).or_insert(model);
        }
        drop(current);
        let old_cache = old
            .cache
            .lock()
            .map_err(|_| "Native reference cache interrupted")?
            .clone()
            .filter(|(_, definitions)| {
                wanted.is_none_or(|wanted| {
                    definitions
                        .iter()
                        .all(|definition| wanted.contains(definition.blueprint.contract.contract()))
                })
            });
        let mut cache = self
            .cache
            .lock()
            .map_err(|_| "Native reference cache interrupted")?;
        if cache.is_none() {
            *cache = old_cache;
        }
        Ok(())
    }
    /// Loads complete raw facts before preparing a pricing context. Every request,
    /// page and validation uses the original dataset recovery budget. A successful
    /// immutable blueprint is retained across resource resets without another C# call.
    ///
    /// # Errors
    /// Missing/mismatched exact versions, incomplete calendars, cancellation or IO.
    pub async fn load(
        &self,
        template: &Arc<ReviewedScope>,
        date: time::Date,
        clock: &Clock,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Arc<ReviewedScope>, String> {
        if stop.is_cancelled() || deadline.remaining().is_zero() {
            return Err("Reference bootstrap original deadline expired".into());
        }
        if template.definitions.is_empty() || template.definitions.len() > 2048 {
            return Err("Invalid raw reference scope capacity".into());
        }
        let pinned = self
            .pinned_calendars
            .lock()
            .map_err(|_| "Frozen calendar directory interrupted")?
            .clone();
        let mut hash = Sha256::new();
        hash.update(date.to_julian_day().to_le_bytes());
        hash.update(template.policy_version.as_bytes());
        for definition in &template.definitions {
            hash.update(codec::encode(&definition.blueprint.contract.value())?);
            hash.update(codec::encode(&definition.blueprint.calendar.value())?);
            hash.update([u8::from(
                pinned.contains(definition.blueprint.contract.contract()),
            )]);
            hash.update(definition.underlying.to_le_bytes());
            for word in definition.strike.bits {
                hash.update(word.to_le_bytes());
            }
            hash.update([u8::from(definition.call)]);
        }
        let identity = format!("{:x}", hash.finalize());
        if let Some((key, reviewed)) = &*self
            .cache
            .lock()
            .map_err(|_| "Native reference cache interrupted")?
            && *key == identity
        {
            return Ok(rebase(template, reviewed.clone()));
        }
        let mut mappings = Vec::with_capacity(template.definitions.len());
        for batch in template.definitions.chunks(16) {
            let args=serde_json::to_vec(&serde_json::json!({"contracts":batch.iter().map(|d|{let v=d.blueprint.contract.value();let v=v.as_array().unwrap();serde_json::json!({"contract":d.blueprint.contract.contract(),"version":v[20].as_str().unwrap()})}).collect::<Vec<_>>()})).map_err(|_|"Native reference request encoding failed")?;
            let bytes = raw_io::read(
                self.broker.clone(),
                self.scope.clone(),
                parent.clone(),
                2,
                &args,
                stop,
                deadline,
            )
            .await?;
            let values: serde_json::Value =
                serde_json::from_slice(&bytes).map_err(|_| "Malformed raw convention reply")?;
            let values = values
                .as_array()
                .filter(|v| v.len() == batch.len())
                .ok_or("Incomplete raw convention reply")?;
            for (value, input) in values.iter().zip(batch) {
                let mapping = if value.is_null() {
                    let model = self
                        .raw_models
                        .lock()
                        .map_err(|_| "Raw option model directory interrupted")?
                        .get(input.blueprint.contract.contract())
                        .cloned();
                    let Some(model) = model else {
                        return Err(format!(
                            "Exact reviewed option mapping is unavailable: {}",
                            input.blueprint.contract.contract()
                        ));
                    };
                    let convention = model.convention()?;
                    if convention.value() != input.blueprint.contract.value() {
                        return Err(
                            "Raw staging model differs from accepted native option reference"
                                .into(),
                        );
                    }
                    use base64::Engine;
                    let expected = convention.value();
                    let fields = expected.as_array().ok_or("Invalid staging convention")?;
                    let args=serde_json::to_vec(&serde_json::json!({"contract":convention.contract(),"version":fields[20].as_str(),"model":base64::engine::general_purpose::STANDARD.encode(codec::encode(&model.value())?)})).map_err(|_|"Native reference staging request encoding failed")?;
                    let bytes = raw_io::read(
                        self.broker.clone(),
                        self.scope.clone(),
                        parent.clone(),
                        3,
                        &args,
                        stop,
                        deadline,
                    )
                    .await?;
                    serde_json::from_slice::<serde_json::Value>(&bytes)
                        .map_err(|_| "Malformed staged convention reply")?
                } else {
                    value.clone()
                };
                mappings.push(Convention::from_command(&mapping)?);
            }
        }
        let mut candidates = Vec::with_capacity(mappings.len());
        for (input, mapping) in template.definitions.iter().zip(&mappings) {
            let expected = input.blueprint.contract.value();
            let v = expected.as_array().unwrap();
            candidates.push(Candidate {
                contract: input.blueprint.contract.contract().into(),
                mapping_version: v[20].as_str().unwrap().into(),
                definition_digest: v[19].as_str().unwrap().into(),
                dataset: input.blueprint.contract.dataset().into(),
                publisher: input.blueprint.contract.publisher(),
                instrument: input.instrument,
                raw_symbol: input.blueprint.contract.raw_symbol().into(),
                underlying: input.blueprint.contract.underlying().into(),
                right: if input.call { 1 } else { 2 },
                strike: input.strike,
                expiration_ns: u64::try_from(input.blueprint.contract.expiration())
                    .ok()
                    .and_then(|t| t.checked_mul(100)),
                mapping: Some(mapping.clone()),
            });
        }
        let at = clock
            .utc_ticks()
            .map_err(|_| "Native reference clock unavailable")?;
        let qualified = universe::qualify(&candidates, true, at, 0, stop, deadline)?;
        if let Some(error) = qualified.failure {
            return Err(format!(
                "Native reference qualification failed: {}: {}",
                error.code, error.contract
            ));
        }
        if qualified.definitions.len() != template.definitions.len() {
            return Err(
                "Raw reference scope contains excluded/unavailable requested contracts".into(),
            );
        }
        let mut calendars = BTreeMap::new();
        let mut definitions = Vec::with_capacity(mappings.len());
        for (input, mapping) in template.definitions.iter().zip(mappings) {
            let original = input.blueprint.calendar.value();
            let fields = original.as_array().ok_or("Invalid raw calendar template")?;
            let timezone = fields[1].as_str().ok_or("Invalid raw calendar timezone")?;
            let expiration = time::OffsetDateTime::from_unix_timestamp_nanos(
                i128::from(mapping.expiration()) * 100,
            )
            .map_err(|_| "Invalid raw calendar maturity")?
            .date();
            let key = (
                mapping.calendar_version().to_owned(),
                timezone.to_owned(),
                expiration,
            );
            if pinned.contains(mapping.contract()) {
                if fields[0].as_str() != Some(mapping.calendar_version()) {
                    return Err("Frozen calendar version changed".into());
                }
                calendars.insert(key.clone(), input.blueprint.calendar.clone());
            } else if !calendars.contains_key(&key) {
                let args=serde_json::to_vec(&serde_json::json!({"from":date.to_julian_day()-1721426,"until":expiration.to_julian_day()-1721426})).map_err(|_|"Native calendar request encoding failed")?;
                let bytes = raw_io::read(
                    self.broker.clone(),
                    self.scope.clone(),
                    parent.clone(),
                    1,
                    &args,
                    stop,
                    deadline,
                )
                .await?;
                let value: serde_json::Value =
                    serde_json::from_slice(&bytes).map_err(|_| "Malformed raw calendar reply")?;
                let dates = value["dates"]
                    .as_array()
                    .filter(|v| v.len() <= 2000)
                    .ok_or("Raw calendar capacity exceeded")?
                    .iter()
                    .map(|n| {
                        n.as_i64()
                            .and_then(|n| n.checked_add(1721426))
                            .and_then(|n| i32::try_from(n).ok())
                            .and_then(|n| time::Date::from_julian_day(n).ok())
                            .ok_or("Invalid raw trading date")
                    })
                    .collect::<Result<Vec<_>, _>>()?;
                let calendar = Calendar::new(
                    mapping.calendar_version().into(),
                    timezone,
                    date,
                    expiration,
                    fields[4].as_u64().ok_or("Invalid raw calendar rollover")?,
                    dates,
                )
                .map_err(|_| "Invalid raw calendar coverage")?;
                calendars.insert(key.clone(), calendar);
            }
            definitions.push(Definition {
                instrument: input.instrument,
                underlying: input.underlying,
                strike: input.strike,
                call: input.call,
                required: input.required,
                blueprint: Blueprint {
                    contract: mapping,
                    calendar: calendars.get(&key).unwrap().clone(),
                    publications: input.blueprint.publications.clone(),
                    conversion: input.blueprint.conversion.clone(),
                    maximum_lead: input.blueprint.maximum_lead,
                },
            });
        }
        if stop.is_cancelled() || deadline.remaining().is_zero() {
            return Err("Reference bootstrap original deadline expired".into());
        }
        *self
            .cache
            .lock()
            .map_err(|_| "Native reference cache interrupted")? =
            Some((identity, definitions.clone()));
        Ok(rebase(template, definitions))
    }
}

fn rebase(template: &ReviewedScope, mut definitions: Vec<Definition>) -> Arc<ReviewedScope> {
    // Cached facts carry no old runtime reference service or manifest budget.
    // All current refresh/production inputs are applied on every reconstruction.
    for (definition, input) in definitions.iter_mut().zip(&template.definitions) {
        definition.required = input.required;
        definition.blueprint.publications = input.blueprint.publications.clone();
        definition.blueprint.conversion = input.blueprint.conversion.clone();
        definition.blueprint.maximum_lead = input.blueprint.maximum_lead;
    }
    Arc::new(ReviewedScope {
        manifests: template.manifests.clone(),
        references: template.references.clone(),
        definitions,
        production: template.production,
        production_approved: template.production_approved,
        review_evidence: template.review_evidence.clone(),
        policy_version: template.policy_version.clone(),
        selection_ms: template.selection_ms,
        iv_ms: template.iv_ms,
        risk_ms: template.risk_ms,
        maximum_contracts: template.maximum_contracts,
        batch: template.batch,
        statistics: template.statistics.clone(),
    })
}
