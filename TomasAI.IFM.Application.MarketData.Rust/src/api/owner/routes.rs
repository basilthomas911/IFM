//! Bounded transient stream-owner identities, independent of physical datasets.
//! Reset preserves demand; explicit session stop clears it. No route can retain
//! a provider task, lock guard across await, lease or callback into managed code.
use super::*;
use std::collections::BTreeSet;

#[derive(Default)]
pub(super) struct Routes {
    owners: BTreeMap<(bool, String), BTreeSet<u128>>,
    count: usize,
}
impl Routes {
    fn change(
        &mut self,
        owner: u128,
        contract: &str,
        option: bool,
        acquire: bool,
    ) -> Result<bool, String> {
        if owner == 0 || contract.trim().is_empty() || contract.len() > 1024 {
            return Err("Invalid native stream owner".into());
        }
        let key = (option, contract.to_owned());
        let present = self
            .owners
            .get(&key)
            .is_some_and(|owners| owners.contains(&owner));
        if acquire {
            if present {
                return Ok(false);
            }
            if self.count == 4096
                || (!self.owners.contains_key(&key) && self.owners.len() == 2048)
                || self
                    .owners
                    .get(&key)
                    .is_some_and(|owners| owners.len() == 256)
            {
                return Err("Native stream owner capacity exceeded".into());
            }
            self.owners.entry(key).or_default().insert(owner);
            self.count += 1;
            Ok(true)
        } else {
            if !present {
                return Ok(false);
            }
            if let Some(owners) = self.owners.get_mut(&key) {
                owners.remove(&owner);
            }
            self.count -= 1;
            if self.owners.get(&key).is_some_and(BTreeSet::is_empty) {
                self.owners.remove(&key);
            }
            Ok(true)
        }
    }
}
impl MarketData {
    /// Registers/releases one transient owner without changing runtime-owned core
    /// publications. Option physical demand is separately commanded atomically
    /// through the native monitored-scope owner, not through a managed route map.
    ///
    /// # Errors
    /// Unmapped acquisition, invalid identity, bounded capacity or interrupted state.
    pub fn change_stream_route(
        &self,
        owner: u128,
        contract: &str,
        option: bool,
        acquire: bool,
    ) -> Result<bool, String> {
        let pending = option
            && self.options.control.intent().is_some_and(|intent| {
                intent.background.as_ref().is_some_and(|background| {
                    background
                        .monitors()
                        .iter()
                        .any(|monitor| monitor.owner == owner && monitor.contract == contract)
                })
            });
        if acquire && !pending && self.contract_binding(contract, option)?.is_nil() {
            return Err("Native stream contract is not configured".into());
        }
        self.routes
            .lock()
            .map_err(|_| "Native stream ownership interrupted")?
            .change(owner, contract, option, acquire)
    }
    pub(super) fn clear_stream_routes(&self) -> Result<(), String> {
        *self
            .routes
            .lock()
            .map_err(|_| "Native stream ownership interrupted")? = Routes::default();
        Ok(())
    }
    /// Runtime-owned futures and individually monitored options remain active
    /// only while their current native resource generation is fully healthy.
    /// Background option candidates do not imply an individual actor tick route.
    ///
    /// # Errors
    /// Interrupted copied ownership state is explicit.
    pub fn stream_active(&self, contract: &str) -> Result<bool, String> {
        let future = self.contract_binding(contract, false)?;
        if let Some(fields) = future.as_array() {
            return Ok(fields[4].as_bool() == Some(true) && fields[5].as_bool() == Some(true));
        }
        let option = self.contract_binding(contract, true)?;
        let Some(fields) = option.as_array() else {
            return Ok(false);
        };
        if fields[4].as_bool() != Some(true) || fields[5].as_bool() != Some(true) {
            return Ok(false);
        }
        let instrument = u32::try_from(fields[2].as_u64().ok_or("Malformed native binding")?)
            .map_err(|_| "Malformed native binding")?;
        Ok(self
            .options_current()
            .is_some_and(|view| view.monitored.contains(&instrument)))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn ownership_is_idempotent_shared_bounded_and_release_never_needs_capacity() {
        let mut routes = Routes::default();
        assert!(routes.change(1, "ES", false, true).unwrap());
        assert!(!routes.change(1, "ES", false, true).unwrap());
        assert!(routes.change(2, "ES", false, true).unwrap());
        assert!(routes.change(1, "ES", false, false).unwrap());
        assert!(!routes.change(1, "ES", false, false).unwrap());
        for owner in 3..=257 {
            routes.change(owner, "ES", false, true).unwrap();
        }
        assert_eq!(routes.count, 256);
        assert!(routes.change(258, "ES", false, true).is_err());
        assert_eq!(routes.count, 256);
        assert!(routes.change(2, "ES", false, false).unwrap());
        assert!(routes.change(258, "ES", false, true).unwrap());
        assert!(routes.change(258, "ES", true, true).unwrap());
        assert!(routes.change(0, "ES", false, true).is_err());
        assert!(routes.change(1, " ", false, true).is_err());
    }
}
