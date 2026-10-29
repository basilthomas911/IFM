//! Native browser intent beneath options dataset control. Copied view ownership
//! and immutable facts survive physical reset; every source read and numerical
//! qualification runs beneath the replaceable dataset parent, never a CLR lease.
use super::*;
use crate::option_chain::browser::{Command, Directory, Facts, Family, Selection};
#[derive(Default)]
pub(super) struct State {
    views: Directory,
    revision: u64,
    resolved: BTreeMap<Family, Arc<Resolved>>,
    pending: BTreeMap<Family, Arc<Facts>>,
    iv: BTreeMap<Family, (i64, f64)>,
    generation: Option<[u8; 16]>,
}
/// Detached browser query state contains no physical dataset resource.
#[derive(Clone)]
pub struct Resolved {
    /// Last complete compact source and fallback observations.
    pub facts: Arc<Facts>,
    /// Physical buffered membership and displayed bounds.
    pub selection: Selection,
    /// Exact native desired scope.
    pub demand: ScopeDemand,
    /// Exact selected full models.
    pub models: Vec<crate::reference::option_contract::OptionContract>,
    /// Query with all currently live view requirements united.
    pub command: Command,
}
fn family_scope(family: &Family) -> String {
    format!(
        "native-browser-{:x}",
        Sha256::digest(
            format!(
                "{}:{}:{}",
                family.contract.len(),
                family.contract,
                family.expiry
            )
            .as_bytes()
        )
    )
}
impl Configuration {
    /// Accepts native browser ownership without any provider/storage await.
    /// The returned mutation flag determines whether native source work changes.
    ///
    /// # Errors
    /// Conflicting identities, bounded view capacity or interrupted copied state.
    pub fn accept_browser(
        &self,
        command: Command,
        live: Option<crate::option_chain::qualification::Decimal>,
    ) -> Result<(Self, bool), String> {
        let mut state = self
            .browser
            .lock()
            .map_err(|_| "Native browser directory interrupted")?;
        let mut views = state.views.clone();
        let mut changed = views.apply(command.clone(), tokio::time::Instant::now())?;
        if !command.release
            && let Some(old) = state.resolved.get(&command.family)
        {
            let at = self
                .market
                .upgrade()
                .ok_or("Native browser market ended")?
                .clock_ticks()?;
            let iv = state
                .iv
                .get(&command.family)
                .filter(|(observed, _)| at.saturating_sub(*observed) < 3_000_000_000)
                .map(|(_, iv)| *iv);
            let selected = old.facts.select_with_iv(
                &command,
                live,
                iv,
                at,
                &CancellationToken::new(),
                Deadline::recovery(),
            )?;
            let physical = old
                .selection
                .buffered
                .iter()
                .copied()
                .collect::<BTreeSet<_>>();
            changed |= selected
                .window
                .rows
                .iter()
                .any(|index| !physical.contains(index));
        }
        let active = views
            .demands()
            .into_iter()
            .map(|command| command.family)
            .collect::<BTreeSet<_>>();
        state.resolved.retain(|family, _| active.contains(family));
        state.pending.retain(|family, _| active.contains(family));
        state.iv.retain(|family, _| active.contains(family));
        if !command.release
            && let Some(old) = state.resolved.get(&command.family)
        {
            let available = old
                .facts
                .rows
                .iter()
                .map(|row| {
                    row.coverage_definition()
                        .map(|definition| definition.contract)
                })
                .collect::<Result<BTreeSet<_>, _>>()?;
            if command
                .required
                .iter()
                .any(|contract| !available.contains(contract.as_str()))
            {
                state.resolved.remove(&command.family);
                state.pending.remove(&command.family);
                changed = true;
            }
        }
        state.views = views;
        if changed {
            state.revision = state
                .revision
                .checked_add(1)
                .ok_or("Native browser revision exhausted")?;
        }
        Ok((self.clone(), changed))
    }
    pub(super) fn browser_has_work(&self) -> bool {
        self.browser
            .lock()
            .unwrap_or_else(|error| error.into_inner())
            .views
            .has_work()
    }
    pub(super) fn browser_demands(&self) -> Result<Vec<ScopeDemand>, String> {
        Ok(self
            .browser
            .lock()
            .map_err(|_| "Native browser demands interrupted")?
            .resolved
            .values()
            .map(|resolved| resolved.demand.clone())
            .collect())
    }
    /// Observes detached browser facts. It cannot keep a source task alive.
    pub fn browser_result(&self, family: &Family) -> Result<Option<Arc<Resolved>>, String> {
        Ok(self
            .browser
            .lock()
            .map_err(|_| "Native browser result interrupted")?
            .resolved
            .get(family)
            .cloned())
    }
    /// Copies the current eligible native window estimate; it never changes
    /// executable quote/context qualification or dataset recovery deadlines.
    pub fn select_browser(
        &self,
        resolved: &Resolved,
        live: Option<crate::option_chain::qualification::Decimal>,
        at: i64,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Selection, String> {
        let iv = self
            .browser
            .lock()
            .map_err(|_| "Native browser IV interrupted")?
            .iv
            .get(&resolved.command.family)
            .filter(|(observed, _)| at.saturating_sub(*observed) < 3_000_000_000)
            .map(|(_, iv)| *iv);
        resolved
            .facts
            .select_with_iv(&resolved.command, live, iv, at, stop, deadline)
    }
    /// Samples at most once per thirty API-clock seconds, exactly as the current
    /// backend. Estimated IV is never a subscription or a resource handle.
    pub fn observe_browser_iv(
        &self,
        family: &Family,
        snapshot: &crate::option_chain::composition::model::Snapshot,
        forward: Option<crate::option_chain::qualification::Decimal>,
    ) -> Result<(), String> {
        let Some(forward) = forward else {
            return Ok(());
        };
        {
            let state = self
                .browser
                .lock()
                .map_err(|_| "Native browser IV interrupted")?;
            if state
                .iv
                .get(family)
                .is_some_and(|(observed, _)| snapshot.at.saturating_sub(*observed) < 300_000_000)
            {
                return Ok(());
            }
        }
        let Some(iv) = crate::option_chain::browser::atm::estimate(snapshot, forward)? else {
            return Ok(());
        };
        let mut state = self
            .browser
            .lock()
            .map_err(|_| "Native browser IV interrupted")?;
        if state.generation == Some(snapshot.generation) && state.resolved.contains_key(family) {
            state.iv.insert(family.clone(), (snapshot.at, iv));
        }
        Ok(())
    }
    /// A fault-driven replacement invalidates the old physical estimate. A
    /// healthy desired-membership update may carry its frozen window input.
    pub fn bind_browser_generation(&self, generation: [u8; 16], reset: bool) -> Result<(), String> {
        let mut state = self
            .browser
            .lock()
            .map_err(|_| "Native browser IV interrupted")?;
        if reset {
            state.iv.clear();
        }
        state.generation = Some(generation);
        Ok(())
    }
    pub(super) async fn refresh_browsers(
        &self,
        date: time::Date,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<bool, String> {
        let (revision, commands, known) = {
            let state = self
                .browser
                .lock()
                .map_err(|_| "Native browser refresh interrupted")?;
            (
                state.revision,
                state.views.demands(),
                state.resolved.clone(),
            )
        };
        let mut incoming = BTreeMap::new();
        let mut total = 0usize;
        for command in commands {
            let facts = Arc::new(
                self.discovery
                    .browser_facts(&command, date, parent.clone(), stop, deadline)
                    .await?,
            );
            total = total
                .checked_add(facts.rows.len())
                .ok_or("Native browser refresh count overflow")?;
            if total > 100000 {
                return Err("Native browser refresh source capacity exceeded".into());
            }
            incoming.insert(command.family, facts);
        }
        if stop.is_cancelled() || deadline.remaining().is_zero() {
            return Err("Native browser refresh cancelled/original deadline expired".into());
        }
        let mut state = self
            .browser
            .lock()
            .map_err(|_| "Native browser refresh commit interrupted")?;
        if state.revision != revision {
            return Ok(false);
        }
        let mut unchanged = true;
        for (family, facts) in incoming {
            if let Some(prior) = known
                .get(&family)
                .filter(|prior| prior.facts.rows == facts.rows)
            {
                let mut copied = prior.as_ref().clone();
                copied.facts = facts;
                state.resolved.insert(family, Arc::new(copied));
            } else {
                state.pending.insert(family, facts);
                unchanged = false;
            }
        }
        Ok(unchanged)
    }
    pub(super) async fn resolve_browsers(
        &self,
        date: time::Date,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<
        (
            Vec<crate::reference::option_contract::OptionContract>,
            BTreeSet<u32>,
        ),
        String,
    > {
        let (revision, commands, known, pending) = {
            let state = self
                .browser
                .lock()
                .map_err(|_| "Native browser source interrupted")?;
            (
                state.revision,
                state.views.demands(),
                state.resolved.clone(),
                state.pending.clone(),
            )
        };
        let mut resolved = BTreeMap::new();
        let mut all = Vec::new();
        let mut monitored = BTreeSet::new();
        let mut retained_rows = 0usize;
        for command in commands {
            let facts = if let Some(facts) = pending.get(&command.family) {
                facts.clone()
            } else if let Some(known) = known.get(&command.family) {
                known.facts.clone()
            } else {
                Arc::new(
                    self.discovery
                        .browser_facts(&command, date, parent.clone(), stop, deadline)
                        .await?,
                )
            };
            retained_rows = retained_rows
                .checked_add(facts.rows.len())
                .ok_or("Native browser source count overflow")?;
            if retained_rows > 100000 {
                return Err("Native complete browser source capacity exceeded".into());
            }
            let live = self
                .market
                .upgrade()
                .map(|market| market.contract_price(&command.family.contract, false, 9_000_000_000))
                .transpose()?
                .and_then(|price| price.1);
            let at = self
                .market
                .upgrade()
                .ok_or("Native browser market ended")?
                .clock_ticks()?;
            let iv = self
                .browser
                .lock()
                .map_err(|_| "Native browser IV interrupted")?
                .iv
                .get(&command.family)
                .filter(|(observed, _)| at.saturating_sub(*observed) < 3_000_000_000)
                .map(|(_, iv)| *iv);
            let selection = facts.select_with_iv(&command, live, iv, at, stop, deadline)?;
            let models = if let Some(known) = known.get(&command.family).filter(|known| {
                known.selection.buffered == selection.buffered && known.facts.rows == facts.rows
            }) {
                known.models.clone()
            } else {
                self.discovery
                    .browser_models(&facts, &selection, parent.clone(), stop, deadline)
                    .await?
            };
            let scope = family_scope(&command.family);
            let maturity = time::Date::from_julian_day(command.family.expiry + 1721426)
                .map_err(|_| "Invalid native browser maturity")?;
            let demand = ScopeDemand::browser(scope, maturity, &models)?;
            for model in &models {
                let candidate = model.scope_candidate()?;
                if command.required.contains(&candidate.contract) {
                    monitored.insert(candidate.instrument);
                }
                all.push(model.clone());
            }
            resolved.insert(
                command.family.clone(),
                Arc::new(Resolved {
                    facts,
                    selection,
                    demand,
                    models,
                    command,
                }),
            );
        }
        {
            let mut state = self
                .browser
                .lock()
                .map_err(|_| "Native browser result interrupted")?;
            if state.revision != revision {
                return Err("Native browser demand changed during source acquisition".into());
            }
            if stop.is_cancelled() || deadline.remaining().is_zero() {
                return Err("Native browser resolution cancelled/original deadline expired".into());
            }
            state.resolved = resolved;
            state.pending.clear();
        }
        {
            let wanted = self
                .effective_scopes()?
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
            let mut models = self
                .scopes
                .models
                .lock()
                .map_err(|_| "Native browser model cache interrupted")?;
            models.retain(|key, _| wanted.contains(key));
            for model in &all {
                let candidate = model.scope_candidate()?;
                models.insert(
                    (candidate.contract, candidate.mapping_version),
                    model.clone(),
                );
            }
            if models.len() > 2048 {
                return Err("Native browser model union capacity exceeded".into());
            }
        }
        Ok((all, monitored))
    }
}
