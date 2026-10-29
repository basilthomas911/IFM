//! Native discovery scope demand and exact immutable-version qualification.
//! Only copied business identifiers/facts survive physical dataset replacement.
use super::*;
use crate::{
    messaging::option_evidence::Failure,
    option_chain::universe::{self, Candidate},
    reference::{decimal, option_contract::OptionContract},
};
/// Complete desired discovery scope, retained by the native owner until release.
#[derive(Clone)]
pub struct Demand {
    /// Stable business owner in CLR Guid byte order interpreted little endian.
    pub owner: u128,
    /// Native stable opaque scope identity.
    pub scope: String,
    /// Explicit maturity supplied by the application contract.
    pub maturity: time::Date,
    /// Complete normalized provider facts and exact requested mapping versions.
    pub candidates: Vec<Candidate>,
    /// Complete-scope assertion, validated natively before any provider allocation.
    pub complete: bool,
    /// Preserved public connection descriptor; resources remain native-owned.
    pub separate: bool,
    /// Preserved independent-chain business descriptor.
    pub independent: bool,
    signature: [u8; 32],
    origin: Option<String>,
    references: Option<Arc<crate::reference::inputs::Inputs>>,
    expires: Option<tokio::time::Instant>,
}
/// Lightweight copied scope identity for queries/health; contains no candidates.
#[derive(Clone)]
pub struct Descriptor {
    /// Stable business owner.
    pub owner: u128,
    /// Stable opaque scope.
    pub scope: String,
    /// Explicit maturity.
    pub maturity: time::Date,
    /// Preserved connection descriptor.
    pub separate: bool,
    /// Preserved independent-chain descriptor.
    pub independent: bool,
}
impl Demand {
    /// Constructs native demand from a validated immutable business plan. The
    /// owner is namespaced from the sealed plan; no managed lease/token survives.
    pub(super) fn committed(plan: &crate::option_chain::committed::Plan) -> Self {
        let signature: [u8; 32] = Sha256::digest(format!("committed-plan/v1:{}", plan.id)).into();
        let owner = u128::from_le_bytes(signature[..16].try_into().expect("SHA-256 prefix")).max(1);
        Self {
            owner,
            scope: plan.id.clone(),
            maturity: time::Date::from_julian_day(plan.maturity + 1721426)
                .expect("validated plan date"),
            candidates: plan.candidates.clone(),
            complete: true,
            separate: plan.separate,
            independent: plan.independent,
            signature,
            origin: None,
            references: plan.references.clone(),
            expires: None,
        }
    }
    pub(super) fn browser(
        scope: String,
        maturity: time::Date,
        models: &[OptionContract],
    ) -> Result<Self, String> {
        let signature: [u8; 32] = Sha256::digest(scope.as_bytes()).into();
        let owner = u128::from_le_bytes(signature[..16].try_into().expect("SHA-256 prefix")).max(1);
        Ok(Self {
            owner,
            scope,
            maturity,
            candidates: models
                .iter()
                .map(OptionContract::scope_candidate)
                .collect::<Result<_, _>>()?,
            complete: true,
            separate: false,
            independent: false,
            signature,
            origin: None,
            references: None,
            expires: None,
        })
    }
    /// Arms only provisional workflow demand from its explicit caller deadline.
    /// The native lifetime is capped at the published 120-second temporary lease
    /// bound. It is not a timeout inside dataset recovery and never expires a
    /// committed or live-trade owner. Repeated commands retain the first expiry.
    ///
    /// # Errors
    /// Missing/non-UTC/expired provisional deadline or invalid API time delta.
    pub fn arm_preparation_lifetime(
        mut self,
        value: &serde_json::Value,
        api_now: i64,
        now: tokio::time::Instant,
    ) -> Result<Self, String> {
        if self.origin.is_some() {
            let origin = &value["origin"];
            if origin["until_offset"].as_i64() != Some(0) {
                return Err("Invalid native preparation deadline offset".into());
            }
            let ticks = origin["until"]
                .as_i64()
                .and_then(|until| until.checked_sub(api_now))
                .filter(|ticks| *ticks > 0)
                .ok_or("Native preparation deadline expired")?;
            let ticks = u64::try_from(ticks)
                .map_err(|_| "Invalid native preparation deadline")?
                .min(1_200_000_000);
            self.expires = Some(now + Duration::from_nanos(ticks * 100));
        }
        Ok(self)
    }
    fn descriptor(&self) -> Descriptor {
        Descriptor {
            owner: self.owner,
            scope: self.scope.clone(),
            maturity: self.maturity,
            separate: self.separate,
            independent: self.independent,
        }
    }
}
/// Detached business admission result, independent of provider resources.
#[derive(Clone)]
pub struct Admission {
    /// Known supported business exclusions in canonical order.
    pub exclusions: Vec<Failure>,
    /// Complete-scope rejection, if any.
    pub failure: Option<Failure>,
    /// A valid scope with no accepted contracts.
    pub complete_empty: bool,
}
/// Detached qualification result; never a provider lease or pricing context.
#[derive(Clone, Default)]
pub(super) struct ResultState {
    pub contracts: BTreeSet<String>,
    pub exclusions: Vec<Failure>,
    pub failure: Option<Failure>,
}
#[derive(Clone, Default)]
pub(super) struct Scopes {
    pub demands: BTreeMap<u128, Arc<Demand>>,
    pub results: Arc<Mutex<BTreeMap<u128, ResultState>>>,
    aliases: Arc<Mutex<BTreeMap<u128, String>>>,
    pub(super) models: Arc<Mutex<BTreeMap<(String, String), OptionContract>>>,
}
fn failed(code: &str) -> ResultState {
    ResultState {
        failure: Some(Failure {
            code: code.into(),
            input: "Definitions".into(),
            contract: String::new(),
            detail: "Complete qualified option scope is unavailable.".into(),
            retryable: false,
        }),
        ..Default::default()
    }
}
impl Demand {
    /// Parses raw command fields; no managed qualification result is trusted.
    ///
    /// # Errors
    /// Invalid identity, bounded shape or normalized provider scalar fields.
    pub fn from_command(value: &serde_json::Value) -> Result<Self, String> {
        let owner = value["owner"]
            .as_array()
            .filter(|a| a.len() == 2)
            .ok_or("Invalid scope business identity")?;
        let owner = u128::from(owner[0].as_u64().ok_or("Invalid scope business identity")?)
            | (u128::from(owner[1].as_u64().ok_or("Invalid scope business identity")?) << 64);
        if owner == 0 {
            return Err("Invalid scope business identity".into());
        }
        let maturity = value["maturity"]
            .as_i64()
            .filter(|n| (1..=3652058).contains(n))
            .ok_or("Invalid scope maturity")?;
        let complete = value["complete"]
            .as_bool()
            .ok_or("Missing complete scope assertion")?;
        let rows = value["candidates"]
            .as_array()
            .filter(|a| a.len() <= 2048)
            .ok_or("Native scope candidate capacity exceeded")?;
        let mut candidates = Vec::new();
        for row in rows {
            let a = row
                .as_array()
                .filter(|a| a.len() == 11)
                .ok_or("Invalid normalized scope candidate")?;
            let text = |i: usize| {
                a[i].as_str()
                    .filter(|s| s.len() <= 1024)
                    .map(str::to_owned)
                    .ok_or("Invalid normalized candidate text")
            };
            candidates.push(Candidate {
                contract: text(0)?,
                mapping_version: text(1)?,
                definition_digest: text(2)?,
                dataset: text(3)?,
                publisher: u16::try_from(a[4].as_u64().ok_or("Invalid candidate publisher")?)
                    .map_err(|_| "Invalid candidate publisher")?,
                instrument: u32::try_from(a[5].as_u64().ok_or("Invalid candidate instrument")?)
                    .map_err(|_| "Invalid candidate instrument")?,
                raw_symbol: text(6)?,
                underlying: text(7)?,
                right: u8::try_from(a[8].as_u64().ok_or("Invalid candidate right")?)
                    .map_err(|_| "Invalid candidate right")?,
                strike: decimal::parse(&text(9)?).ok_or("Invalid candidate strike")?,
                expiration_ns: if a[10].is_null() {
                    None
                } else {
                    Some(a[10].as_u64().ok_or("Invalid candidate expiration")?)
                },
                mapping: None,
            });
        }
        let separate = value["separate"]
            .as_bool()
            .ok_or("Invalid native scope connection descriptor")?;
        let independent = value["independent"]
            .as_bool()
            .ok_or("Invalid native scope connection descriptor")?;
        if separate && (candidates.len() != 1 || independent)
            || independent && candidates.is_empty()
        {
            return Err("Invalid native scope connection descriptor".into());
        }
        let mut identity = value.clone();
        if let Some(origin) = identity
            .get_mut("origin")
            .and_then(serde_json::Value::as_object_mut)
        {
            origin.remove("until");
            origin.remove("until_offset");
        }
        let signature =
            Sha256::digest(serde_json::to_vec(&identity).map_err(|_| "Invalid scope signature")?)
                .into();
        let origin = match value.get("origin") {
            None | Some(serde_json::Value::Null) => None,
            Some(origin) => {
                let workflow = origin["workflow"]
                    .as_str()
                    .filter(|id| {
                        id.len() == 36
                            && id.bytes().enumerate().all(|(at, b)| {
                                if [8, 13, 18, 23].contains(&at) {
                                    b == b'-'
                                } else {
                                    b.is_ascii_hexdigit()
                                }
                            })
                    })
                    .ok_or("Invalid native preparation workflow")?;
                origin["revision"]
                    .as_i64()
                    .filter(|version| *version > 0)
                    .ok_or("Invalid native preparation revision")?;
                origin["input"]
                    .as_str()
                    .filter(|hash| hash.len() == 64 && hash.bytes().all(|b| b.is_ascii_hexdigit()))
                    .ok_or("Invalid native preparation input identity")?;
                Some(workflow.to_owned())
            }
        };
        let references = if value["references"].is_null() {
            None
        } else {
            Some(Arc::new(crate::reference::inputs::Inputs::from_value(
                &value["references"],
            )?))
        };
        Ok(Self {
            owner,
            scope: format!("native-scope-{owner:032x}"),
            maturity: time::Date::from_julian_day(maturity as i32 + 1721426)
                .map_err(|_| "Invalid scope maturity")?,
            candidates,
            complete,
            separate,
            independent,
            signature,
            origin,
            references,
            expires: None,
        })
    }
}
impl Scopes {
    pub(super) fn merge(&self, previous: &Self) -> Result<Self, String> {
        let mut result = previous.clone();
        for (owner, demand) in &self.demands {
            if result
                .demands
                .get(owner)
                .is_some_and(|old| old.signature != demand.signature)
            {
                return Err("Active native scope demand conflicts with its owner".into());
            }
            result
                .demands
                .entry(*owner)
                .or_insert_with(|| demand.clone());
        }
        result.validate()?;
        Ok(result)
    }
    fn validate(&self) -> Result<(), String> {
        if self.demands.len() > 64
            || self
                .demands
                .values()
                .flat_map(|d| d.candidates.iter().map(|c| c.contract.as_str()))
                .collect::<BTreeSet<_>>()
                .len()
                > 2048
        {
            return Err("Native discovery scope capacity exceeded".into());
        }
        let mut facts = BTreeMap::<
            &str,
            (
                &u128,
                &Candidate,
                Option<&Arc<crate::reference::inputs::Inputs>>,
            ),
        >::new();
        for (owner, demand) in &self.demands {
            for candidate in &demand.candidates {
                if let Some((previous, old, references)) = facts.get(candidate.contract.as_str()) {
                    let old: &Candidate = old;
                    if *previous != owner
                        && (old.mapping_version != candidate.mapping_version
                            || old.definition_digest != candidate.definition_digest
                            || old.dataset != candidate.dataset
                            || old.publisher != candidate.publisher
                            || old.instrument != candidate.instrument
                            || old.raw_symbol != candidate.raw_symbol
                            || old.underlying != candidate.underlying
                            || old.right != candidate.right
                            || old.expiration_ns != candidate.expiration_ns
                            || old.strike.bits != candidate.strike.bits
                            || demand
                                .references
                                .as_ref()
                                .zip(*references)
                                .is_some_and(|(new, old)| new.signature != old.signature))
                    {
                        return Err(
                            "Conflicting immutable native scope mapping/reference inputs".into(),
                        );
                    }
                } else {
                    facts.insert(
                        candidate.contract.as_str(),
                        (owner, candidate, demand.references.as_ref()),
                    );
                }
            }
        }
        Ok(())
    }
}
impl Configuration {
    /// Exact next native lifetime event. No C# renewal heartbeat is required.
    pub fn next_scope_expiration(&self) -> Option<tokio::time::Instant> {
        self.scopes
            .demands
            .values()
            .filter_map(|demand| demand.expires)
            .min()
    }
    /// Retires elapsed provisional intent as one native control mutation.
    /// Committed plan scopes and durable live-trade monitors are never touched.
    pub fn expire_scopes(&self, now: tokio::time::Instant) -> Result<Option<Self>, String> {
        if self.next_scope_expiration().is_none_or(|at| at > now) {
            return Ok(None);
        }
        let provisional_expired = self
            .scopes
            .demands
            .values()
            .any(|demand| demand.expires.is_some_and(|until| until <= now));
        if !provisional_expired {
            return Ok(None);
        }
        let mut next = self.clone();
        next.scopes
            .demands
            .retain(|_, demand| demand.expires.is_none_or(|at| at > now));
        Ok(Some(next))
    }
    /// Enables native-owned committed business reconstruction. Initial loading,
    /// all physical resources and subsequent replacements share dataset control.
    ///
    /// # Errors
    /// Invalid authority configuration.
    pub fn with_committed(mut self) -> Result<Self, String> {
        self.committed = Some(Arc::new(crate::option_chain::committed::Directory::new(
            "IFM",
        )?));
        Ok(self)
    }
    /// Requests one native journal refresh, fencing any earlier handoff read.
    ///
    /// # Errors
    /// Journal support absent, closed directory or interrupted copied state.
    pub fn refresh_committed(&self) -> Result<u64, String> {
        self.committed
            .as_ref()
            .ok_or("Native committed ownership is not configured")?
            .refresh()
    }
    /// Event-driven committed readiness, without a managed observer/lease registry.
    ///
    /// # Errors
    /// The committed source is not configured.
    pub fn committed_changes(&self) -> Result<tokio::sync::watch::Receiver<u64>, String> {
        Ok(self
            .committed
            .as_ref()
            .ok_or("Native committed ownership is not configured")?
            .changes())
    }
    /// Returns immutable accepted journal facts for readiness/ownership evidence.
    ///
    /// # Errors
    /// Interrupted copied state.
    pub fn committed_book(
        &self,
    ) -> Result<Option<Arc<crate::option_chain::committed::Book>>, String> {
        self.committed
            .as_ref()
            .map_or(Ok(None), |directory| directory.accepted())
    }
    /// Latest requested journal revision must be accepted in addition to physical
    /// dataset readiness before a caller can acknowledge committed realization.
    ///
    /// # Errors
    /// Journal absent or interrupted copied state.
    pub fn committed_current(&self) -> Result<bool, String> {
        let directory = self
            .committed
            .as_ref()
            .ok_or("Native committed ownership is not configured")?;
        if !directory.current()? {
            return Ok(false);
        }
        directory
            .accepted()?
            .map_or(Ok(false), |book| directory.acknowledged(&book))
    }
    /// Applies conditional durable delivery only after the concrete native
    /// source has all required readiness proofs. No managed lease is involved.
    ///
    /// # Errors
    /// Raw business persistence failure, changed journal or original deadline.
    pub async fn acknowledge_committed(
        &self,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
        physical: Option<[u8; 32]>,
    ) -> Result<bool, String> {
        let Some(directory) = &self.committed else {
            return Ok(true);
        };
        if !directory.current()? {
            return Ok(false);
        }
        match directory
            .deliver_for_physical(&self.discovery, parent, stop, deadline, physical)
            .await
        {
            Ok(accepted) => Ok(accepted && directory.current()?),
            Err(error)
                if super::committed_refresh::superseded(&error) && !directory.current()? =>
            {
                Ok(false)
            }
            Err(error) => Err(error),
        }
    }
    pub(super) fn provisional_scopes(&self) -> Result<Scopes, String> {
        match self.committed_book()? {
            Some(book) => self.scopes.surviving_preparations(&book),
            None => {
                let mut scopes = self.scopes.clone();
                scopes.demands.retain(|_, demand| {
                    demand
                        .expires
                        .is_none_or(|at| at > tokio::time::Instant::now())
                });
                Ok(scopes)
            }
        }
    }
    pub(super) fn effective_scopes(&self) -> Result<Scopes, String> {
        let mut scopes = self.provisional_scopes()?;
        if let Some(book) = self.committed_book()? {
            for plan in book
                .plans
                .values()
                .filter(|plan| !plan.candidates.is_empty())
            {
                let demand = Demand::committed(plan);
                if scopes
                    .demands
                    .insert(demand.owner, Arc::new(demand))
                    .is_some()
                {
                    return Err("Committed/discovery owner identity collision".into());
                }
            }
        }
        for demand in self.browser_demands()? {
            if scopes
                .demands
                .insert(demand.owner, Arc::new(demand))
                .is_some()
            {
                return Err("Native browser scope identity collision".into());
            }
        }
        scopes.validate()?;
        Ok(scopes)
    }
    /// Applies each accepted scope's own frozen reference recipe, never a
    /// different current global calendar or Treasury policy. Shared instruments
    /// require identical scoped recipes; ambiguous policies are explicit errors.
    pub(super) fn apply_scope_references(
        &self,
        definitions: &mut [crate::option_chain::preparation::Definition],
    ) -> Result<BTreeSet<String>, String> {
        let scopes = self.effective_scopes()?;
        let results = scopes
            .results
            .lock()
            .map_err(|_| "Native scope references interrupted")?;
        let mut inputs = BTreeMap::<String, Arc<crate::reference::inputs::Inputs>>::new();
        for (owner, demand) in &scopes.demands {
            let Some(reference) = &demand.references else {
                continue;
            };
            let Some(result) = results.get(owner).filter(|result| result.failure.is_none()) else {
                continue;
            };
            for contract in &result.contracts {
                if inputs
                    .insert(contract.clone(), reference.clone())
                    .is_some_and(|prior| prior.signature != reference.signature)
                {
                    return Err(
                        "Conflicting immutable reference recipes for a shared option".into(),
                    );
                }
            }
        }
        let mut pinned = BTreeSet::new();
        for definition in definitions {
            if let Some(reference) = inputs.get(definition.blueprint.contract.contract()) {
                let calendar = reference.calendar.value();
                if calendar
                    .as_array()
                    .and_then(|fields| fields.first())
                    .and_then(rmpv::Value::as_str)
                    != Some(definition.blueprint.contract.calendar_version())
                {
                    return Err(
                        "Frozen option calendar version differs from the reviewed convention"
                            .into(),
                    );
                }
                definition.blueprint.calendar = reference.calendar.clone();
                definition.blueprint.publications = reference.policy.publications.clone();
                definition.blueprint.conversion = reference.policy.conversion.clone();
                // Clock-lead allowance is deployment policy, not a stored rate or
                // calendar field. Preserve its currently reviewed native limit.
                definition.blueprint.maximum_lead = self.policy.maximum_lead;
                pinned.insert(definition.blueprint.contract.contract().to_owned());
            }
        }
        Ok(pinned)
    }
    /// Adds copied complete scope demand before any database/provider await.
    ///
    /// # Errors
    /// Conflicting stable identity or bounded aggregate scope capacity.
    pub fn with_scope(mut self, demand: Demand) -> Result<Self, String> {
        self.scopes.demands.insert(demand.owner, Arc::new(demand));
        self.scopes.validate()?;
        Ok(self)
    }
    /// Copies current desired business scope descriptors without resource handles.
    ///
    /// # Errors
    /// Interrupted copied business alias directory.
    pub fn scope_demands(&self) -> Result<Vec<Descriptor>, String> {
        let scopes = self.effective_scopes()?;
        let aliases = scopes
            .aliases
            .lock()
            .map_err(|_| "Native scope alias directory interrupted")?;
        Ok(scopes
            .demands
            .values()
            .map(|demand| {
                let mut descriptor = demand.descriptor();
                if let Some(alias) = aliases.get(&demand.owner) {
                    descriptor.scope = alias.clone();
                }
                descriptor
            })
            .collect())
    }
    /// Checks a copied public or pre-alias name for an exact logical owner.
    /// This permits cleanup through physical generation replacement without
    /// allowing a mismatched scope name to withdraw another demand.
    ///
    /// # Errors
    /// Interrupted immutable-name directory.
    pub fn owns_scope_name(&self, owner: u128, scope: &str) -> Result<bool, String> {
        Ok(self
            .provisional_scopes()?
            .demands
            .get(&owner)
            .is_some_and(|demand| demand.scope == scope)
            || self
                .scope_demands()?
                .iter()
                .any(|demand| demand.owner == owner && demand.scope == scope))
    }
    /// Binds immutable business reconstruction identity to already qualified
    /// native ownership. This changes copied names only: no resource is renewed,
    /// cancelled, retained, joined or reconfigured by the business store.
    ///
    /// # Errors
    /// Unknown owner, conflicting expected name, unsafe identity collision or
    /// unqualified/empty scope. Repeating the exact binding is idempotent.
    pub fn alias_scope(
        &self,
        owner: u128,
        expected: &str,
        replacement: &str,
    ) -> Result<(), String> {
        self.provisional_scopes()?
            .alias_scope(owner, expected, replacement)
    }
    fn store_scope_result(&self, owner: u128, state: ResultState) -> Result<(), String> {
        let mut results = self
            .scopes
            .results
            .lock()
            .map_err(|_| "Native scope result interrupted")?;
        results.retain(|owner, _| self.scopes.demands.contains_key(owner));
        if results.len() == 64 && !results.contains_key(&owner) {
            return Err("Native scope result capacity exceeded".into());
        }
        results.insert(owner, state);
        Ok(())
    }
    pub(super) async fn scope_models(
        &self,
        parent: Arc<Child>,
        clock: &Clock,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<OptionContract>, String> {
        let scoped = Self {
            scopes: self.effective_scopes()?,
            ..self.clone()
        };
        scoped
            .read_scope_models(parent, clock, stop, deadline)
            .await
    }
    async fn read_scope_models(
        &self,
        parent: Arc<Child>,
        clock: &Clock,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<OptionContract>, String> {
        let wanted = self
            .scopes
            .demands
            .values()
            .flat_map(|demand| {
                demand.candidates.iter().map(|candidate| {
                    (
                        candidate.contract.clone(),
                        candidate.mapping_version.clone(),
                    )
                })
            })
            .collect::<BTreeSet<_>>();
        self.scopes
            .models
            .lock()
            .map_err(|_| "Native scope facts interrupted")?
            .retain(|key, _| wanted.contains(key));
        let mut models = BTreeMap::new();
        for demand in self.scopes.demands.values() {
            // Known business rejections do not condemn another healthy scope.
            let initial = universe::qualify(
                &demand.candidates,
                demand.complete,
                clock
                    .utc_ticks()
                    .map_err(|_| "Native scope clock unavailable")?,
                0,
                stop,
                deadline,
            )?;
            if !demand.complete
                || demand.candidates.is_empty()
                || initial
                    .failure
                    .as_ref()
                    .is_some_and(|failure| failure.code == "ConflictingDefinition")
            {
                self.store_scope_result(
                    demand.owner,
                    ResultState {
                        contracts: Default::default(),
                        exclusions: initial.exclusions,
                        failure: initial.failure,
                    },
                )?;
                continue;
            }
            let mut candidates = demand.candidates.clone();
            let mut raw = BTreeMap::new();
            let mut missing = false;
            for candidate in &mut candidates {
                if stop.is_cancelled() || deadline.remaining().is_zero() {
                    return Err(
                        "Native scope qualification cancelled/original deadline expired".into(),
                    );
                }
                let key = (
                    candidate.contract.clone(),
                    candidate.mapping_version.clone(),
                );
                let known = self
                    .scopes
                    .models
                    .lock()
                    .map_err(|_| "Native scope facts interrupted")?
                    .get(&key)
                    .cloned();
                let model = if let Some(model) = known {
                    Some(model)
                } else {
                    let args=serde_json::to_vec(&serde_json::json!({"contract":candidate.contract,"version":candidate.mapping_version})).map_err(|_|"Invalid exact source request")?;
                    let bytes = crate::messaging::raw_io::read(
                        self.discovery.broker.clone(),
                        self.discovery.scope.clone(),
                        parent.clone(),
                        6,
                        &args,
                        stop,
                        deadline,
                    )
                    .await?;
                    let value: serde_json::Value = serde_json::from_slice(&bytes)
                        .map_err(|_| "Malformed exact raw scope source")?;
                    if value.is_null() {
                        None
                    } else {
                        Some(OptionContract::from_command(&value)?)
                    }
                };
                let Some(model) = model else {
                    missing = true;
                    break;
                };
                let Ok(mapping) = model.convention() else {
                    missing = true;
                    break;
                };
                candidate.mapping = Some(mapping);
                {
                    let mut cache = self
                        .scopes
                        .models
                        .lock()
                        .map_err(|_| "Native scope facts interrupted")?;
                    if cache.len() == 2048 && !cache.contains_key(&key) {
                        return Err("Native exact scope fact capacity exceeded".into());
                    }
                    cache.insert(key, model.clone());
                }
                raw.insert(candidate.contract.clone(), model);
            }
            if missing {
                self.store_scope_result(demand.owner, failed("ContractMetadataUnavailable"))?;
                continue;
            }
            let outcome = universe::qualify(
                &candidates,
                demand.complete,
                clock
                    .utc_ticks()
                    .map_err(|_| "Native scope clock unavailable")?,
                0,
                stop,
                deadline,
            )?;
            let state = ResultState {
                contracts: outcome
                    .definitions
                    .iter()
                    .map(|definition| definition.convention.contract().to_owned())
                    .collect(),
                exclusions: outcome.exclusions,
                failure: outcome.failure,
            };
            if state.failure.is_none() {
                for contract in &state.contracts {
                    let model = raw
                        .remove(contract)
                        .ok_or("Qualified native scope lacks raw source")?;
                    if models
                        .insert(contract.clone(), model.clone())
                        .is_some_and(|old: OptionContract| old.value() != model.value())
                    {
                        return Err("Native scopes conflict on raw reference version".into());
                    }
                }
            }
            self.store_scope_result(demand.owner, state)?;
        }
        Ok(models.into_values().collect())
    }
    /// Copies a qualified desired scope for capture/receipt queries.
    ///
    /// # Errors
    /// Interrupted detached result directory.
    pub fn resolved_scope(
        &self,
        id: &str,
        owner: Option<u128>,
    ) -> Result<Option<(Descriptor, BTreeSet<String>)>, String> {
        let Some(demand) = self
            .scope_demands()?
            .into_iter()
            .find(|d| d.scope == id && owner.is_none_or(|owner| owner == d.owner))
        else {
            return Ok(None);
        };
        let state = self
            .scopes
            .results
            .lock()
            .map_err(|_| "Native scope result interrupted")?;
        Ok(state
            .get(&demand.owner)
            .filter(|result| result.failure.is_none())
            .map(|result| (demand.clone(), result.contracts.clone())))
    }
    /// Returns detached known business rejections/exclusions for a scope query.
    ///
    /// # Errors
    /// Interrupted result directory.
    pub fn discovery_result(&self, owner: u128) -> Result<Option<Admission>, String> {
        if !self.provisional_scopes()?.demands.contains_key(&owner) {
            return Ok(None);
        }
        Ok(self
            .scopes
            .results
            .lock()
            .map_err(|_| "Native scope result interrupted")?
            .get(&owner)
            .map(|result| Admission {
                exclusions: result.exclusions.clone(),
                failure: result.failure.clone(),
                complete_empty: result.contracts.is_empty() && result.failure.is_none(),
            }))
    }
}

impl Scopes {
    fn surviving_preparations(
        &self,
        book: &crate::option_chain::committed::Book,
    ) -> Result<Self, String> {
        let mut scopes = self.clone();
        scopes.demands.retain(|_, demand| {
            demand
                .expires
                .is_none_or(|at| at > tokio::time::Instant::now())
        });
        let aliases = scopes
            .aliases
            .lock()
            .map_err(|_| "Native preparation alias directory interrupted")?;
        scopes.demands.retain(|owner, demand| {
            let Some(workflow) = demand.origin.as_ref() else {
                return true;
            };
            let Some(source) = book
                .sources
                .get(&format!("IntrinsicTimeWorkflow:{workflow}"))
            else {
                return true;
            };
            let scope = aliases.get(owner).unwrap_or(&demand.scope);
            !(source.status == 1 || source.leases.iter().any(|lease| &lease.plan == scope))
        });
        drop(aliases);
        Ok(scopes)
    }
    fn alias_scope(&self, owner: u128, expected: &str, replacement: &str) -> Result<(), String> {
        if replacement.len() != 64 || !replacement.bytes().all(|b| b.is_ascii_hexdigit()) {
            return Err("Invalid native business plan identity".into());
        }
        let demand = self.demands.get(&owner).ok_or("Native scope owner ended")?;
        let results = self
            .results
            .lock()
            .map_err(|_| "Native scope result interrupted")?;
        let state = results
            .get(&owner)
            .filter(|state| state.failure.is_none() && !state.contracts.is_empty())
            .ok_or("Native scope is not qualified")?;
        let mut aliases = self
            .aliases
            .lock()
            .map_err(|_| "Native scope alias directory interrupted")?;
        aliases.retain(|owner, _| self.demands.contains_key(owner));
        let current = aliases.get(&owner).unwrap_or(&demand.scope);
        if current == replacement {
            return Ok(());
        }
        if current != expected {
            return Err("Native scope name changed".into());
        }
        // Several business owners may share one immutable plan. Index accepted
        // membership once so a maximum-size shared plan cannot create quadratic
        // comparisons while holding a copied-state lock. Excluded definitions
        // are not part of the sealed reconstruction plan.
        let wanted = demand
            .candidates
            .iter()
            .filter(|candidate| state.contracts.contains(&candidate.contract))
            .map(|candidate| (candidate.contract.as_str(), candidate))
            .collect::<BTreeMap<_, _>>();
        for (other, _) in aliases
            .iter()
            .filter(|(other, alias)| **other != owner && alias.as_str() == replacement)
        {
            let prior = self.demands.get(other).ok_or("Native alias owner ended")?;
            let prior_state = results
                .get(other)
                .ok_or("Native alias result unavailable")?;
            let candidates = prior
                .candidates
                .iter()
                .filter(|candidate| prior_state.contracts.contains(&candidate.contract))
                .map(|candidate| (candidate.contract.as_str(), candidate))
                .collect::<BTreeMap<_, _>>();
            if prior_state.contracts != state.contracts
                || candidates.len() != wanted.len()
                || wanted.iter().any(|(contract, candidate)| {
                    candidates.get(contract).is_none_or(|old| {
                        old.mapping_version != candidate.mapping_version
                            || old.definition_digest != candidate.definition_digest
                            || old.dataset != candidate.dataset
                            || old.publisher != candidate.publisher
                            || old.instrument != candidate.instrument
                            || old.raw_symbol != candidate.raw_symbol
                            || old.underlying != candidate.underlying
                            || old.right != candidate.right
                            || old.expiration_ns != candidate.expiration_ns
                            || old.strike.compare(candidate.strike)
                                != Some(std::cmp::Ordering::Equal)
                    })
                })
                || prior.maturity != demand.maturity
                || prior.separate != demand.separate
                || prior.independent != demand.independent
            {
                return Err("Native business plan identity conceals different membership".into());
            }
        }
        if aliases.len() == 64 && !aliases.contains_key(&owner) {
            return Err("Native business alias capacity exceeded".into());
        }
        aliases.insert(owner, replacement.into());
        Ok(())
    }
}

#[cfg(test)]
mod alias_tests {
    use super::*;
    fn candidate(name: &str) -> Candidate {
        Candidate {
            contract: name.into(),
            mapping_version: "reviewed/v1".into(),
            definition_digest: "a".repeat(64),
            dataset: "GLBX.MDP3".into(),
            publisher: 3,
            instrument: 43,
            raw_symbol: "source-option".into(),
            underlying: "ES20261218".into(),
            right: 1,
            strike: decimal::parse("6000").unwrap(),
            expiration_ns: Some(1794326400000000000),
            mapping: None,
        }
    }
    fn add(scopes: &mut Scopes, owner: u128, extra: bool) {
        let mut demand=Demand::from_command(&serde_json::json!({"owner":[owner as u64,0],"maturity":739929,"candidates":[],"complete":true,"separate":false,"independent":false})).unwrap();
        demand.candidates.push(candidate("accepted"));
        if extra {
            let mut excluded = candidate("excluded");
            excluded.instrument = 44;
            demand.candidates.push(excluded);
        }
        scopes.demands.insert(owner, Arc::new(demand));
        scopes.results.lock().unwrap().insert(
            owner,
            ResultState {
                contracts: ["accepted".to_owned()].into_iter().collect(),
                ..Default::default()
            },
        );
    }
    #[test]
    fn same_plan_shares_accepted_membership_and_released_owner_cannot_poison_reuse() {
        let mut scopes = Scopes::default();
        add(&mut scopes, 1, true);
        add(&mut scopes, 2, false);
        let alias = "a".repeat(64);
        scopes
            .alias_scope(1, &scopes.demands[&1].scope, &alias)
            .unwrap();
        scopes
            .alias_scope(2, &scopes.demands[&2].scope, &alias)
            .unwrap();
        scopes.demands.remove(&1);
        scopes.demands.remove(&2);
        add(&mut scopes, 3, false);
        scopes
            .alias_scope(3, &scopes.demands[&3].scope, &alias)
            .unwrap();
        assert_eq!(scopes.aliases.lock().unwrap().len(), 1);
    }
    #[test]
    fn different_mapping_under_same_plan_is_rejected_without_mutating_prior_alias() {
        let mut scopes = Scopes::default();
        add(&mut scopes, 1, false);
        add(&mut scopes, 2, false);
        Arc::make_mut(scopes.demands.get_mut(&2).unwrap()).candidates[0].mapping_version =
            "different/v2".into();
        let alias = "a".repeat(64);
        scopes
            .alias_scope(1, &scopes.demands[&1].scope, &alias)
            .unwrap();
        assert!(
            scopes
                .alias_scope(2, &scopes.demands[&2].scope, &alias)
                .is_err()
        );
        assert_eq!(scopes.aliases.lock().unwrap().get(&1), Some(&alias));
        assert!(!scopes.aliases.lock().unwrap().contains_key(&2));
        assert!(scopes.alias_scope(1, "stale", &"b".repeat(64)).is_err());
        scopes.alias_scope(1, "stale", &alias).unwrap(); // exact idempotent retry
    }
    #[test]
    fn alias_capacity_reclaims_released_copied_entries_during_long_owner_churn() {
        let mut scopes = Scopes::default();
        for owner in 1..=4096u128 {
            add(&mut scopes, owner, false);
            let alias = format!("{owner:064x}");
            scopes
                .alias_scope(owner, &scopes.demands[&owner].scope, &alias)
                .unwrap();
            assert_eq!(scopes.aliases.lock().unwrap().len(), 1);
            scopes.demands.remove(&owner);
            scopes.results.lock().unwrap().remove(&owner);
        }
    }
    #[test]
    fn preparation_origin_is_strict_optional_and_separate_from_generation() {
        let base = serde_json::json!({"owner":[1,0],"maturity":739929,"candidates":[],"complete":true,"separate":false,"independent":false});
        assert!(Demand::from_command(&base).unwrap().origin.is_none());
        let mut value = base.clone();
        value["origin"] = serde_json::json!({"workflow":"00112233-4455-6677-8899-aabbccddeeff","revision":1,"input":"a".repeat(64)});
        let demand = Demand::from_command(&value).unwrap();
        assert_eq!(
            demand.origin.as_deref(),
            Some("00112233-4455-6677-8899-aabbccddeeff")
        );
        for key in ["workflow", "revision", "input"] {
            let mut invalid = value.clone();
            invalid["origin"][key] = serde_json::Value::Null;
            assert!(Demand::from_command(&invalid).is_err());
        }
        value["origin"]["workflow"] = serde_json::json!("00112233445566778899aabbccddeeff");
        assert!(Demand::from_command(&value).is_err());
    }
    #[tokio::test(start_paused = true)]
    async fn preparation_lifetime_is_native_bounded_and_duplicate_commands_cannot_extend_it() {
        let mut value = serde_json::json!({"owner":[1,0],"maturity":739929,"candidates":[],"complete":true,"separate":false,"independent":false,
            "origin":{"workflow":"00112233-4455-6677-8899-aabbccddeeff","revision":1,"input":"a".repeat(64),"until":2_400_000_000i64,"until_offset":0}});
        let now = tokio::time::Instant::now();
        let demand = Demand::from_command(&value)
            .unwrap()
            .arm_preparation_lifetime(&value, 0, now)
            .unwrap();
        assert_eq!(demand.expires.unwrap() - now, Duration::from_secs(120));
        let mut original = Scopes::default();
        original.demands.insert(1, Arc::new(demand));
        tokio::time::advance(Duration::from_secs(119)).await;
        value["origin"]["until"] = serde_json::json!(3_600_000_000i64);
        let renewed = Demand::from_command(&value)
            .unwrap()
            .arm_preparation_lifetime(&value, 1_190_000_000, tokio::time::Instant::now())
            .unwrap();
        assert_eq!(renewed.signature, original.demands[&1].signature);
        let mut incoming = Scopes::default();
        incoming.demands.insert(1, Arc::new(renewed));
        let merged = incoming.merge(&original).unwrap();
        assert_eq!(
            merged.demands[&1].expires.unwrap() - tokio::time::Instant::now(),
            Duration::from_secs(1)
        );
        tokio::time::advance(Duration::from_secs(1)).await;
        assert_eq!(
            merged.demands[&1].expires.unwrap(),
            tokio::time::Instant::now()
        );
        // Generic and committed demand has no workflow deadline and remains durable.
        value.as_object_mut().unwrap().remove("origin");
        assert!(
            Demand::from_command(&value)
                .unwrap()
                .arm_preparation_lifetime(&value, i64::MAX, now)
                .unwrap()
                .expires
                .is_none()
        );
    }
    #[test]
    fn preparation_lifetime_rejects_invalid_deadlines_and_preserves_shorter_expiry() {
        let base = serde_json::json!({"owner":[1,0],"maturity":739929,"candidates":[],"complete":true,"separate":false,"independent":false,
            "origin":{"workflow":"00112233-4455-6677-8899-aabbccddeeff","revision":1,"input":"a".repeat(64),"until":100i64,"until_offset":0}});
        let now = tokio::time::Instant::now();
        assert_eq!(
            Demand::from_command(&base)
                .unwrap()
                .arm_preparation_lifetime(&base, 0, now)
                .unwrap()
                .expires
                .unwrap()
                - now,
            Duration::from_nanos(10_000)
        );
        for (until, offset) in [(0, 0), (-1, 0), (100, 60), (i64::MIN, 0)] {
            let mut value = base.clone();
            value["origin"]["until"] = serde_json::json!(until);
            value["origin"]["until_offset"] = serde_json::json!(offset);
            assert!(
                Demand::from_command(&value)
                    .unwrap()
                    .arm_preparation_lifetime(&value, 0, now)
                    .is_err()
            );
        }
        let mut missing = base.clone();
        missing["origin"].as_object_mut().unwrap().remove("until");
        assert!(
            Demand::from_command(&missing)
                .unwrap()
                .arm_preparation_lifetime(&missing, 0, now)
                .is_err()
        );
        assert!(
            Demand::from_command(&base)
                .unwrap()
                .arm_preparation_lifetime(&base, i64::MIN, now)
                .is_err()
        );
    }
    #[test]
    fn committed_handoff_retires_only_its_provisional_origin_even_after_generation_change() {
        use crate::option_chain::committed::{Authority, Book, Lease};
        let workflow = "00112233-4455-6677-8899-aabbccddeeff";
        let plan = "a".repeat(64);
        let mut scopes = Scopes::default();
        add(&mut scopes, 1, false);
        add(&mut scopes, 2, false);
        add(&mut scopes, 3, false);
        Arc::make_mut(scopes.demands.get_mut(&1).unwrap()).origin = Some(workflow.into());
        Arc::make_mut(scopes.demands.get_mut(&3).unwrap()).origin = Some("other-workflow".into());
        for owner in [1, 2, 3] {
            scopes
                .alias_scope(owner, &scopes.demands[&owner].scope, &plan)
                .unwrap();
        }
        let mut book = Book {
            authority: "IFM".into(),
            revision: 1,
            signature: [0; 32],
            journal_digest: "a".repeat(64),
            sources: BTreeMap::new(),
            plans: BTreeMap::new(),
        };
        assert_eq!(
            scopes.surviving_preparations(&book).unwrap().demands.len(),
            3
        );
        let id = format!("IntrinsicTimeWorkflow:{workflow}");
        book.sources.insert(
            id.clone(),
            Authority {
                id: id.clone(),
                version: 1,
                status: 0,
                reason: "CommittedActive".into(),
                event: [1; 16],
                digest: "b".repeat(64),
                workflow_type: "IntrinsicTimeWorkflow".into(),
                workflow: workflow.into(),
                leg: "Position".into(),
                leases: vec![],
            },
        );
        assert_eq!(
            scopes.surviving_preparations(&book).unwrap().demands.len(),
            3
        );
        book.sources.get_mut(&id).unwrap().leases.push(Lease {
            id: [2; 16],
            version: 1,
            purpose: 4,
            provider: "Databento".into(),
            schema: "mbp-1".into(),
            plan: plan.clone(),
            contract: "accepted".into(),
            option: true,
            underlying: Some("ES20261218".into()),
        });
        let retained = scopes.surviving_preparations(&book).unwrap();
        assert!(!retained.demands.contains_key(&1));
        assert!(retained.demands.contains_key(&2));
        assert!(retained.demands.contains_key(&3));
        book.sources.get_mut(&id).unwrap().status = 2;
        assert!(
            !scopes
                .surviving_preparations(&book)
                .unwrap()
                .demands
                .contains_key(&1)
        );
        let terminal = book.sources.get_mut(&id).unwrap();
        terminal.status = 1;
        terminal.leases.clear();
        assert!(
            !scopes
                .surviving_preparations(&book)
                .unwrap()
                .demands
                .contains_key(&1)
        );
        // Logical origin remains disposed even when a delayed old caller tries
        // to alias it. There is deliberately no physical generation comparison.
        assert!(
            scopes
                .surviving_preparations(&book)
                .unwrap()
                .alias_scope(1, &scopes.demands[&1].scope, &"c".repeat(64))
                .is_err()
        );
    }
}
