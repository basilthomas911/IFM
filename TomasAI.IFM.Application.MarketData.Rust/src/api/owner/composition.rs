//! Read-only composition queries over detached numeric views. Queries retain no
//! SDK connection, source child, subscription lease or acquisition service.
use super::*;
use crate::option_chain::composition::{
    capture::{self, Failure, Request},
    model::Snapshot,
    source,
};
use tokio_util::sync::CancellationToken;
fn failed(code: &str, input: &str) -> Failure {
    Failure {
        code: code.into(),
        input: input.into(),
    }
}
fn risk_scope(contract: &str) -> String {
    use sha2::{Digest, Sha256};
    format!("native-risk-{:x}", Sha256::digest(contract.as_bytes()))
}
impl MarketData {
    /// Captures a complete active option scope entirely in native code. Source
    /// copying and numerical qualification cannot pin a physical dataset owner.
    /// Retiring the dataset signals cancellation of any copied-source calculation.
    ///
    /// # Errors
    /// Existing typed source, scope, request, coherence or numerical failures.
    pub async fn capture_options(
        &self,
        mut request: Request,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Snapshot, Failure> {
        if request.at == -621355968000000000 {
            request.at = self
                .clock
                .utc_ticks()
                .map_err(|_| failed("InvalidSnapshotRequest", "Snapshot"))?;
        }
        capture::validate_request(&request, &self.clock)?;
        if !request.options {
            return Err(failed("InvalidSnapshotRequest", "Snapshot"));
        }
        let health = self.health(Lane::OptionsGlbx);
        let before = health.status();
        let claims = self
            .option_claims()
            .ok_or_else(|| failed("LeaseEnded", "ScopeId"))?;
        let view = self
            .options_current()
            .ok_or_else(|| failed("Recovering", "GenerationId"))?;
        let scope = if let Some((_, scope)) = claims
            .active_scopes()
            .map_err(|_| failed("Recovering", "ScopeId"))?
            .into_iter()
            .find(|(id, _)| *id == request.scope)
        {
            scope
        } else if let Some(definition) = view.definitions.iter().find(|definition| {
            view.monitored.contains(&definition.instrument)
                && risk_scope(definition.blueprint.contract.contract()) == request.scope
        }) {
            crate::dataset::option_claims::Scope {
                options: [definition.instrument].into_iter().collect(),
                monitored: [definition.instrument].into_iter().collect(),
                caches: vec![],
            }
        } else {
            let background = self
                .options
                .control
                .intent()
                .and_then(|intent| intent.background.clone())
                .ok_or_else(|| failed("LeaseEnded", "ScopeId"))?;
            let mut contracts = background
                .monitors()
                .into_iter()
                .filter(|monitor| monitor.scope == request.scope)
                .map(|monitor| monitor.contract)
                .collect::<std::collections::BTreeSet<_>>();
            if let Some((_, resolved)) = background
                .resolved_scope(&request.scope, None)
                .map_err(|_| failed("Recovering", "ScopeId"))?
            {
                contracts.extend(resolved);
            }
            if contracts.is_empty() {
                return Err(failed("LeaseEnded", "ScopeId"));
            }
            let options = view
                .definitions
                .iter()
                .filter(|definition| contracts.contains(definition.blueprint.contract.contract()))
                .map(|definition| definition.instrument)
                .collect::<std::collections::BTreeSet<_>>();
            if options.len() != contracts.len() {
                return Err(failed("IncompleteChain", "Snapshot"));
            }
            crate::dataset::option_claims::Scope {
                monitored: options.clone(),
                options,
                caches: vec![],
            }
        };
        if view.header.generation != request.generation {
            return Err(failed("Recovering", "GenerationId"));
        }
        let definitions = view
            .definitions
            .iter()
            .filter(|d| scope.options.contains(&d.instrument))
            .cloned()
            .collect::<Vec<_>>();
        if definitions.len() != scope.options.len() {
            return Err(failed("IncompleteChain", "Snapshot"));
        }
        let token = view
            .configuration
            .lock()
            .map_err(|_| failed("Recovering", "Snapshot"))?
            .clone();
        let page = source::read_queries(
            &view.queries,
            &view.pricing,
            source::Metadata {
                definitions: &definitions,
                policy_version: &view.policy_version,
                iv_ms: view.iv_ms,
            },
            request.generation,
            &token,
            request.at,
        )?;
        let generation = request.generation;
        let result = tokio::select! {biased;
            ()=stop.cancelled()=>Err(failed("DeadlineExpired","Snapshot")),
            ()=view.cancelled()=>Err(failed("Recovering","GenerationId")),
            result=capture::capture(request,vec![page],&self.clock,stop,deadline)=>result,
        }?;
        let after = health.status();
        if before.ticket != after.ticket
            || after.phase != Phase::Running
            || self
                .publication_identity(Lane::OptionsGlbx)
                .is_none_or(|h| h.generation != generation)
        {
            return Err(failed("Recovering", "GenerationId"));
        }
        Ok(result)
    }
}

impl MarketData {
    /// Captures outright-futures source quotes without option definitions,
    /// Treasury, IV or Greeks. All metadata/hash/source decisions remain native.
    ///
    /// # Errors
    /// Existing typed request, definition, source or quote qualification failure.
    pub async fn capture_futures(
        &self,
        mut request: Request,
        definitions: Vec<crate::option_chain::composition::model::Future>,
        non_utc_definition: bool,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Snapshot, Failure> {
        use crate::{
            messaging::option_evidence::Quote,
            option_chain::{
                coalesced::underlying_quote,
                composition::{capture::Page, model::Instrument},
            },
            reference::semantic,
        };
        if request.at == -621355968000000000 {
            request.at = self
                .clock
                .utc_ticks()
                .map_err(|_| failed("InvalidSnapshotRequest", "Snapshot"))?;
        }
        capture::validate_request(&request, &self.clock)?;
        if request.options || definitions.is_empty() || definitions.len() > request.maximum {
            return Err(failed("InvalidSnapshotRequest", "Snapshot"));
        }
        let mut instruments = Vec::new();
        let mut observed = Vec::new();
        for definition in &definitions {
            let lane = match definition.dataset.as_str() {
                "GLBX.MDP3" => Lane::FuturesGlbx,
                "XCBF.PITCH" => Lane::FuturesXcbf,
                _ => return Err(failed("ContractMetadataUnavailable", "Snapshot")),
            };
            let source = self
                .futures
                .get(&lane)
                .ok_or_else(|| failed("ContractMetadataUnavailable", "Snapshot"))?;
            let before = source.control.recovery().status();
            let identity = source
                .factory
                .identity()
                .filter(|id| id.generation == request.generation)
                .ok_or_else(|| failed("ContractMetadataUnavailable", "Snapshot"))?;
            if before.phase != Phase::Running || !source.control.has_intent() {
                return Err(failed("ContractMetadataUnavailable", "Snapshot"));
            }
            let registrations = source
                .factory
                .registrations()
                .ok_or_else(|| failed("ContractMetadataUnavailable", "Snapshot"))?;
            let registration = registrations
                .iter()
                .find(|r| r.contract == definition.contract)
                .ok_or_else(|| failed("ContractMetadataUnavailable", "Snapshot"))?;
            let queries = source
                .factory
                .queries()
                .ok_or_else(|| failed("QuoteUnavailable", "Snapshot"))?;
            let raw = queries
                .raw_prices(registration.instrument)
                .map_err(|_| failed("Recovering", "Snapshot"))?
                .and_then(|r| r.quote)
                .ok_or_else(|| failed("QuoteUnavailable", "Snapshot"))?;
            let mut quote = underlying_quote(raw, u128::from_le_bytes(identity.generation))
                .ok_or_else(|| failed("QuoteUnavailable", "Snapshot"))?;
            // The existing outright-futures source uses provider receipt time;
            // option qualification deliberately uses local ingestion time.
            quote.received = raw.receive_ticks;
            instruments.push(Instrument {
                contract: definition.contract.clone(),
                quote: Some(Quote {
                    contract: definition.contract.clone(),
                    quote,
                }),
                context: None,
                strike: None,
                call: None,
                underlying: None,
                future: Some(definition.clone()),
                selection: None,
                volume: None,
                interest: None,
                statistics_at: None,
                unavailable: None,
                last_event: None,
                last_received: None,
                volume_date: None,
                interest_date: None,
            });
            observed.push((lane, before.ticket));
        }
        if non_utc_definition {
            return Err(failed("ContractMetadataUnavailable", "Snapshot"));
        }
        let mut ordered = definitions.iter().collect::<Vec<_>>();
        ordered.sort_by_key(|f| f.contract.encode_utf16().collect::<Vec<_>>());
        let token = semantic::compute(&semantic::Value::Array(
            ordered
                .into_iter()
                .map(|d| d.semantic())
                .collect::<Result<Vec<_>, _>>()
                .map_err(|_| failed("ContractMetadataUnavailable", "Snapshot"))?,
        ))
        .map_err(|_| failed("ContractMetadataUnavailable", "Snapshot"))?;
        let page = Page {
            generation: request.generation,
            token,
            total: instruments.len(),
            instruments,
            continuation: None,
        };
        let result = capture::capture(request, vec![page], &self.clock, stop, deadline).await?;
        if observed.into_iter().any(|(lane, ticket)| {
            let status = self.health(lane).status();
            status.ticket != ticket || status.phase != Phase::Running
        }) {
            return Err(failed("Recovering", "GenerationId"));
        }
        Ok(result)
    }
}

impl MarketData {
    /// Copies a fully native-prepared scope receipt in the existing public DTO.
    /// No source resource, renewal authority or acquisition task is exported.
    ///
    /// # Errors
    /// Incomplete/expired reference evidence, conflicting identity or size bound.
    pub fn scope_receipt(
        &self,
        scope: &str,
        lease: [u8; 16],
        maturity: time::Date,
    ) -> Result<rmpv::Value, String> {
        use crate::messaging::{codec, wire::guid};
        use rmpv::Value;
        if scope.is_empty()
            || scope.encode_utf16().count() > 128
            || lease == [0; 16]
            || maturity.year() < 1
            || maturity.year() > 9999
        {
            return Err("Invalid native scope receipt identity".into());
        }
        let before = self.health(Lane::OptionsGlbx).status();
        let Some(view) = self.options_current() else {
            return Ok(Value::Nil);
        };
        let Some(claims) = self.option_claims() else {
            return Ok(Value::Nil);
        };
        let mut flags = (false, false);
        let membership = if let Some(membership) =
            claims.owned_scope(u128::from_le_bytes(lease), scope)?
        {
            membership
        } else {
            let Some(background) = self
                .options
                .control
                .intent()
                .and_then(|intent| intent.background.clone())
            else {
                return Ok(Value::Nil);
            };
            let Some((demand, contracts)) =
                background.resolved_scope(scope, Some(u128::from_le_bytes(lease)))?
            else {
                return Ok(Value::Nil);
            };
            flags = (demand.separate, demand.independent);
            let options = view
                .definitions
                .iter()
                .filter(|definition| contracts.contains(definition.blueprint.contract.contract()))
                .map(|definition| definition.instrument)
                .collect::<std::collections::BTreeSet<_>>();
            if options.len() != contracts.len() {
                return Ok(Value::Nil);
            }
            crate::dataset::option_claims::Scope {
                options,
                monitored: Default::default(),
                caches: vec![],
            }
        };
        let now = self
            .clock
            .utc_ticks()
            .map_err(|_| "Native scope clock unavailable")?;
        let mut expiry = now
            .checked_add(1_200_000_000)
            .ok_or("Native scope receipt expiry overflow")?;
        let mut definitions = view
            .definitions
            .iter()
            .filter(|definition| membership.options.contains(&definition.instrument))
            .collect::<Vec<_>>();
        if definitions.len() != membership.options.len() {
            return Err("Incomplete native scope definitions".into());
        }
        definitions.sort_by_key(|definition| definition.blueprint.contract.contract());
        let mut options = Vec::new();
        let mut bytes = 0usize;
        for definition in definitions {
            let context = view
                .pricing
                .reference_context(definition.instrument)?
                .ok_or("Native scope reference is absent")?;
            if context.generation() != view.header.generation {
                return Ok(Value::Nil);
            }
            context.validate_at(now).map_err(|failure| {
                format!("Native scope reference unavailable: {}", failure.code)
            })?;
            expiry = expiry.min(context.valid_until());
            let value = Value::Array(vec![
                context.value()?,
                codec::decimal_bits(definition.strike)?,
                definition.call.into(),
            ]);
            bytes = bytes
                .checked_add(codec::encode(&value)?.len())
                .ok_or("Native scope receipt size overflow")?;
            if bytes > 4 * 1024 * 1024 {
                return Err("Native scope receipt capacity exceeded".into());
            }
            options.push(value);
        }
        let after = self.health(Lane::OptionsGlbx).status();
        if before.ticket != after.ticket
            || after.phase != Phase::Running
            || self
                .options_current()
                .is_none_or(|next| next.header.generation != view.header.generation)
        {
            return Ok(Value::Nil);
        }
        Ok(Value::Array(vec![
            scope.into(),
            guid(lease).into(),
            guid(view.header.generation).into(),
            view.header.value_date.into(),
            (maturity.to_julian_day() - 1721426).into(),
            codec::utc_offset(expiry)?,
            Value::Array(options),
            Value::Nil,
            flags.0.into(),
            flags.1.into(),
            Value::Array(vec![]),
            false.into(),
        ]))
    }
}

impl MarketData {
    /// Builds individual-risk query policy from the native current owner and
    /// API clock. A query never creates, renews or retains a source subscription.
    ///
    /// # Errors
    /// Missing/wrong-date monitored ownership or unavailable native clock.
    pub fn monitored_risk_command(
        &self,
        contract: &str,
        date: time::Date,
    ) -> Result<super::CaptureCommand, Failure> {
        let view = self
            .options_current()
            .filter(|view| view.header.value_date == date.to_julian_day() - 1721426)
            .ok_or_else(|| failed("Recovering", "GenerationId"))?;
        let definition = view
            .definitions
            .iter()
            .find(|definition| {
                definition.blueprint.contract.contract() == contract
                    && view.monitored.contains(&definition.instrument)
            })
            .ok_or_else(|| failed("LeaseEnded", "ScopeId"))?;
        let scopes = self
            .option_claims()
            .ok_or_else(|| failed("LeaseEnded", "ScopeId"))?
            .active_scopes()
            .map_err(|_| failed("Recovering", "ScopeId"))?;
        let scope = scopes
            .into_iter()
            .find(|(_, scope)| {
                scope.options.len() == 1 && scope.monitored.contains(&definition.instrument)
            })
            .map(|(id, _)| id)
            .or_else(|| {
                self.options.control.intent().and_then(|intent| {
                    intent.background.as_ref().and_then(|background| {
                        background
                            .monitors()
                            .into_iter()
                            .find(|monitor| monitor.contract == contract)
                            .map(|monitor| monitor.scope)
                    })
                })
            })
            .unwrap_or_else(|| risk_scope(contract));
        let until = self
            .clock
            .utc_ticks()
            .ok()
            .and_then(|now| now.checked_add(1_200_000_000))
            .ok_or_else(|| failed("InvalidSnapshotRequest", "Snapshot"))?;
        Ok(super::CaptureCommand {
            wait: false,
            request: Request {
                id: rand::random::<[u8; 16]>(),
                scope,
                generation: view.header.generation,
                horizon: "Daily".into(),
                at: -621355968000000000,
                until,
                maximum: 1,
                age: 5000,
                skew: 1000,
                options: true,
                selection: false,
                allow_missing: false,
                risk: Default::default(),
            },
            futures: vec![],
            non_utc_definition: false,
        })
    }
}

impl MarketData {
    /// Copies native discovery admission/exclusions and a current scope receipt.
    /// Known business rejections are data; source/IO failures use dataset recovery.
    ///
    /// # Errors
    /// Interrupted native result state or incomplete copied contexts.
    pub fn discovery_scope_result(&self, owner: [u8; 16]) -> Result<rmpv::Value, String> {
        use rmpv::Value;
        if self.has_empty_discovery(u128::from_le_bytes(owner))? {
            return Ok(Value::Array(vec![
                Value::Nil,
                Value::Array(vec![]),
                true.into(),
                Value::Nil,
            ]));
        }
        let failure = |code: &str| {
            Value::Array(vec![
                code.into(),
                "Definitions".into(),
                "".into(),
                "Complete qualified option scope is unavailable.".into(),
                false.into(),
            ])
        };
        let Some(background) = self
            .options
            .control
            .intent()
            .and_then(|intent| intent.background.clone())
        else {
            return Ok(Value::Array(vec![
                Value::Nil,
                Value::Array(vec![]),
                false.into(),
                failure("LeaseEnded"),
            ]));
        };
        let business = u128::from_le_bytes(owner);
        let Some(demand) = background
            .scope_demands()?
            .into_iter()
            .find(|demand| demand.owner == business)
        else {
            return Ok(Value::Array(vec![
                Value::Nil,
                Value::Array(vec![]),
                false.into(),
                failure("LeaseEnded"),
            ]));
        };
        let Some(admission) = background.discovery_result(business)? else {
            return Ok(Value::Array(vec![
                Value::Nil,
                Value::Array(vec![]),
                false.into(),
                failure("Recovering"),
            ]));
        };
        let exclusions = Value::Array(
            admission
                .exclusions
                .iter()
                .map(|failure| failure.value())
                .collect::<Result<Vec<_>, _>>()?,
        );
        if let Some(rejection) = admission.failure {
            return Ok(Value::Array(vec![
                Value::Nil,
                exclusions,
                false.into(),
                rejection.value()?,
            ]));
        }
        // Known rejections remain reportable during physical recovery. Successful
        // empty or populated results still require the healthy dataset proof.
        if self.health(Lane::OptionsGlbx).status().phase != Phase::Running {
            return Ok(Value::Array(vec![
                Value::Nil,
                Value::Array(vec![]),
                false.into(),
                failure("Recovering"),
            ]));
        }
        if admission.complete_empty {
            return Ok(Value::Array(vec![
                Value::Nil,
                exclusions,
                true.into(),
                Value::Nil,
            ]));
        }
        let receipt = self.scope_receipt(&demand.scope, owner, demand.maturity)?;
        if receipt.is_nil() {
            return Ok(Value::Array(vec![
                Value::Nil,
                exclusions,
                false.into(),
                failure("Recovering"),
            ]));
        }
        Ok(Value::Array(vec![
            receipt,
            exclusions,
            false.into(),
            Value::Nil,
        ]))
    }
}

impl MarketData {
    /// Finalizes a copied known rejection/empty result. This is invoked only
    /// after a complete FFI result copy, never by a sizing or failed copy call.
    ///
    /// # Errors
    /// Interrupted native desired-state mutation.
    pub fn complete_rejected_discovery(&self, owner: [u8; 16]) -> Result<(), String> {
        let business = u128::from_le_bytes(owner);
        if self.forget_empty_discovery(business)? {
            return Ok(());
        }
        let rejected = self
            .options
            .control
            .intent()
            .and_then(|intent| intent.background.clone())
            .map(|background| background.discovery_result(business))
            .transpose()?
            .flatten()
            .is_some_and(|admission| admission.failure.is_some() || admission.complete_empty);
        if rejected && let Some(claims) = self.option_claims() {
            claims.disable(business)?;
        }
        Ok(())
    }
}

impl MarketData {
    /// Assigns an already persisted immutable business plan identity to native
    /// demand. Generation comparison rejects a stale business acceptance, while
    /// the resulting copied alias survives later physical dataset replacement.
    ///
    /// # Errors
    /// Stale source identity, ended/unqualified ownership or conflicting alias.
    pub fn alias_option_scope(
        &self,
        owner: u128,
        generation: [u8; 16],
        expected: &str,
        replacement: &str,
    ) -> Result<(), String> {
        if self.health(Lane::OptionsGlbx).status().phase != Phase::Running
            || self
                .publication_identity(Lane::OptionsGlbx)
                .is_none_or(|header| header.generation != generation)
        {
            return Err("Native scope generation changed".into());
        }
        let background = self
            .options
            .control
            .intent()
            .and_then(|intent| intent.background.clone())
            .ok_or("Native scope owner ended")?;
        background.alias_scope(owner, expected, replacement)
    }
}

impl MarketData {
    /// Accepts an authoritative empty scope as bounded copied business evidence.
    /// No provider, reference request, physical generation or task is needed.
    /// Replacing this owner's prior nonempty demand still explicitly releases it.
    ///
    /// # Errors
    /// Invalid scope/date, interrupted state, exhausted result capacity or failure
    /// to withdraw this same owner's earlier native demand.
    pub fn accept_empty_discovery(
        &self,
        demand: &crate::dataset::background::ScopeDemand,
        date: time::Date,
    ) -> Result<bool, String> {
        if !demand.complete || !demand.candidates.is_empty() {
            return Ok(false);
        }
        if self.closing.load(std::sync::atomic::Ordering::Acquire) {
            return Err("Native owner is closing".into());
        }
        self.validate_api_date(date)?;
        let now = tokio::time::Instant::now();
        let mut results = self
            .empty_discovery
            .lock()
            .map_err(|_| "Native empty result state interrupted")?;
        results.retain(|_, until| *until > now);
        if results.len() >= 64 && !results.contains_key(&demand.owner) {
            return Err("Native empty result capacity exceeded".into());
        }
        if let Some(claims) = self.option_claims() {
            claims.disable(demand.owner)?;
        }
        self.clear_catalog_memo()?;
        results
            .entry(demand.owner)
            .or_insert(now + std::time::Duration::from_secs(120));
        Ok(true)
    }
    fn has_empty_discovery(&self, owner: u128) -> Result<bool, String> {
        let mut results = self
            .empty_discovery
            .lock()
            .map_err(|_| "Native empty result state interrupted")?;
        results.retain(|_, until| *until > tokio::time::Instant::now());
        Ok(results.contains_key(&owner))
    }
    /// Retires copied empty evidence when consumed or superseded by a later
    /// accepted nonempty command. This never affects another native owner.
    ///
    /// # Errors
    /// Interrupted copied result state.
    pub fn forget_empty_discovery(&self, owner: u128) -> Result<bool, String> {
        Ok(self
            .empty_discovery
            .lock()
            .map_err(|_| "Native empty result state interrupted")?
            .remove(&owner)
            .is_some())
    }
}
