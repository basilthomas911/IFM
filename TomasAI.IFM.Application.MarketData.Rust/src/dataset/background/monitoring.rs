//! Canonical monitored demand is accepted before raw database acquisition.
//! Resolution and reconstruction are owned by the options dataset, so a stalled
//! C# fact responder cannot extend the original deadline or pin its resources.
use super::*;
use crate::reference::option_contract::OptionContract;

/// Copied stable business intent, independent of provider IDs and generations.
#[derive(Clone, PartialEq, Eq)]
pub struct Monitor {
    /// Nonzero native hash of the caller's workflow/leg identity.
    pub owner: u128,
    /// Stable opaque contract scope identity.
    pub scope: String,
    /// Requested canonical option contract.
    pub contract: String,
}
#[derive(Clone, Default)]
pub(super) struct Monitoring {
    members: BTreeMap<u128, Monitor>,
    models: Arc<Mutex<BTreeMap<String, OptionContract>>>,
}
impl Monitoring {
    pub(super) fn merged(&self, previous: &Self) -> Result<Self, String> {
        let mut next = previous.clone();
        for (owner, monitor) in &self.members {
            if next.members.get(owner).is_some_and(|old| old != monitor) {
                return Err("Conflicting native canonical monitor identity".into());
            }
            next.members.insert(*owner, monitor.clone());
        }
        next.validate()?;
        Ok(next)
    }
    fn validate(&self) -> Result<(), String> {
        if self.members.len() > 4096
            || self
                .members
                .values()
                .map(|monitor| &monitor.scope)
                .collect::<BTreeSet<_>>()
                .len()
                > 64
        {
            return Err("Native canonical monitor capacity exceeded".into());
        }
        Ok(())
    }
}
impl Configuration {
    /// Configures native monitored resolution without enabling policy discovery.
    pub fn monitored_only(mut self) -> Self {
        self.enabled = false;
        self
    }
    /// Adds one immutable desired owner before any provider/database await.
    ///
    /// # Errors
    /// Invalid identity, conflict or bounded owner/scope capacity.
    pub fn with_monitor(mut self, monitor: Monitor) -> Result<Self, String> {
        if monitor.owner == 0
            || monitor.scope.trim().is_empty()
            || monitor.scope.len() > 128
            || monitor.contract.trim().is_empty()
            || monitor.contract.len() > 1024
        {
            return Err("Invalid native canonical monitor".into());
        }
        if self
            .monitoring
            .members
            .get(&monitor.owner)
            .is_some_and(|old| old != &monitor)
        {
            return Err("Conflicting native canonical monitor".into());
        }
        self.monitoring.members.insert(monitor.owner, monitor);
        self.monitoring.validate()?;
        Ok(self)
    }
    /// Removes a logical owner without needing a native provider token.
    pub fn without_monitor(&self, owner: u128) -> Option<Self> {
        if !self.monitoring.members.contains_key(&owner)
            && !self.scopes.demands.contains_key(&owner)
        {
            return None;
        }
        let mut next = self.clone();
        next.monitoring.members.remove(&owner);
        next.scopes.demands.remove(&owner);
        Some(next)
    }
    /// Whether this copied configuration still has any desired native workload.
    pub fn has_work(&self) -> bool {
        self.enabled
            || self.browser_has_work()
            || self.committed.is_some()
            || !self.monitoring.members.is_empty()
            || !self.scopes.demands.is_empty()
    }
    /// Complete copied owner/scope identities for native recovery evidence.
    pub fn monitors(&self) -> Vec<Monitor> {
        self.monitoring.members.values().cloned().collect()
    }
    pub(super) async fn monitored_models(
        &self,
        base: &Intent,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(Vec<OptionContract>, BTreeSet<u32>), String> {
        let wanted = self
            .monitoring
            .members
            .values()
            .map(|monitor| monitor.contract.as_str())
            .collect::<BTreeSet<_>>();
        self.monitoring
            .models
            .lock()
            .map_err(|_| "Native monitored facts interrupted")?
            .retain(|contract, _| wanted.contains(contract.as_str()));
        let mut models = BTreeMap::new();
        let mut instruments = BTreeSet::new();
        for monitor in self.monitoring.members.values() {
            if models.contains_key(&monitor.contract) {
                continue;
            }
            let mut known = base
                .reference_bootstrap
                .as_ref()
                .map(|bootstrap| bootstrap.option_model(&monitor.contract))
                .transpose()?
                .flatten();
            if known.is_none() {
                let retained = self
                    .reference_cache
                    .lock()
                    .map_err(|_| "Native reference cache interrupted")?
                    .clone();
                known = retained
                    .as_ref()
                    .map(|bootstrap| bootstrap.option_model(&monitor.contract))
                    .transpose()?
                    .flatten();
            }
            if known.is_none() {
                known = self
                    .monitoring
                    .models
                    .lock()
                    .map_err(|_| "Native monitored facts interrupted")?
                    .get(&monitor.contract)
                    .cloned();
            }
            let model = if let Some(model) = known {
                model
            } else {
                let args = serde_json::to_vec(&serde_json::json!({"contract":monitor.contract}))
                    .map_err(|_| "Invalid native contract lookup")?;
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
                let value = serde_json::from_slice(&bytes)
                    .map_err(|_| "Malformed native contract lookup")?;
                OptionContract::from_command(&value)?
            };
            let convention = model.convention()?;
            if convention.contract() != monitor.contract {
                return Err("Native contract lookup returned another identity".into());
            }
            instruments.insert(convention.instrument());
            {
                let mut cached = self
                    .monitoring
                    .models
                    .lock()
                    .map_err(|_| "Native monitored facts interrupted")?;
                if cached.len() == 2048 && !cached.contains_key(&monitor.contract) {
                    return Err("Native monitored fact capacity exceeded".into());
                }
                cached.insert(monitor.contract.clone(), model.clone());
            }
            models.insert(monitor.contract.clone(), model);
        }
        Ok((models.into_values().collect(), instruments))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn canonical_monitor_owners_are_shared_conflict_checked_and_bounded() {
        let mut state = Monitoring::default();
        for id in 1..=4096u128 {
            state.members.insert(
                id,
                Monitor {
                    owner: id,
                    scope: "shared".into(),
                    contract: "ES20261110C6000".into(),
                },
            );
        }
        assert!(state.validate().is_ok());
        state.members.insert(
            4097,
            Monitor {
                owner: 4097,
                scope: "shared".into(),
                contract: "ES20261110C6000".into(),
            },
        );
        assert!(state.validate().is_err());
        state.members.remove(&4097);
        let mut other = Monitoring::default();
        other.members.insert(
            1,
            Monitor {
                owner: 1,
                scope: "changed".into(),
                contract: "ES20261110P6000".into(),
            },
        );
        assert!(other.merged(&state).is_err());
        other.members.clear();
        assert_eq!(other.merged(&state).unwrap().members.len(), 4096);
        for id in 1..=65u128 {
            other.members.insert(
                id,
                Monitor {
                    owner: id,
                    scope: format!("scope-{id}"),
                    contract: format!("contract-{id}"),
                },
            );
        }
        assert!(other.validate().is_err());
        other.members.remove(&65);
        assert!(other.validate().is_ok());
    }
}
