//! Native idempotence for copied configuration. These bounded memo values hold
//! no resource, lease or callback; a cache miss only repeats native validation.
use super::*;
#[derive(Default)]
pub(super) struct Catalog {
    configuration: Option<[u8; 32]>,
    models: BTreeMap<String, [u8; 32]>,
}
impl MarketData {
    /// Rejects a conflicting API date before changing any accepted feed state.
    ///
    /// # Errors
    /// Default dates or an active dataset owned by another value date.
    pub fn validate_api_date(&self, date: time::Date) -> Result<(), String> {
        if date.to_julian_day() == 1721426 {
            return Err("Native market data requires an explicit API ValueDate".into());
        }
        for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx] {
            let current = if lane == Lane::OptionsGlbx {
                self.options.control.intent().map(|i| i.date)
            } else {
                self.futures
                    .get(&lane)
                    .and_then(|f| f.control.intent())
                    .map(|i| i.date)
            };
            if current.is_some_and(|current| current != date) {
                return Err("A different API ValueDate still owns native market data".into());
            }
        }
        Ok(())
    }
    /// Accepts a validated native-generated command fingerprint. Repeated starts
    /// join existing setup/recovery instead of replacing its source or deadline.
    /// Explicit start after terminal failure can begin a new recovery window.
    ///
    /// # Errors
    /// Conflicting date, unavailable native control or interrupted memo state.
    pub fn start_futures_once(
        &self,
        intent: Arc<FuturesIntent>,
        key: [u8; 32],
    ) -> Result<(), String> {
        self.validate_api_date(intent.date)?;
        let lane = intent.source.plan.lane;
        let mut commands = self
            .futures_commands
            .lock()
            .map_err(|_| "Native futures command memo interrupted")?;
        if commands.get(&lane) == Some(&key)
            && self.has_desired(lane)
            && !matches!(
                self.health(lane).status().phase,
                Phase::Failed | Phase::Stopped
            )
        {
            return Ok(());
        }
        self.apply_futures(intent)?;
        commands.insert(lane, key);
        Ok(())
    }
    pub(super) fn clear_catalog_memo(&self) -> Result<(), String> {
        *self
            .raw_catalog_commands
            .lock()
            .map_err(|_| "Native raw command memo interrupted")? = Catalog::default();
        Ok(())
    }
    /// Merges native-generated raw configuration/model fingerprints. A duplicate
    /// does not rebuild a healthy dataset, while failed control remains explicitly
    /// restartable. Native claim acquisition remains a separate atomic mutation.
    ///
    /// # Errors
    /// Date/metadata conflicts, closed control or interrupted bounded state.
    pub fn merge_raw_catalog_once(
        &self,
        catalog: Arc<OptionsIntent>,
        configuration: [u8; 32],
        models: Vec<(String, [u8; 32])>,
    ) -> Result<(), String> {
        self.validate_api_date(catalog.date)?;
        let mut memo = self
            .raw_catalog_commands
            .lock()
            .map_err(|_| "Native raw command memo interrupted")?;
        let canonical_accepted = if let Some(background) = catalog.background.as_ref() {
            let monitors = background.monitors();
            let scopes = background.scope_demands()?;
            if monitors.is_empty() && scopes.is_empty() {
                true
            } else if let Some(active) = self
                .options
                .control
                .intent()
                .and_then(|current| current.background.clone())
            {
                let accepted = active.monitors();
                let wanted = active.scope_demands()?;
                monitors.iter().all(|monitor| accepted.contains(monitor))
                    && scopes
                        .iter()
                        .all(|scope| wanted.iter().any(|old| old.owner == scope.owner))
            } else {
                false
            }
        } else {
            true
        };
        if memo.configuration == Some(configuration)
            && canonical_accepted
            && models
                .iter()
                .all(|(id, key)| memo.models.get(id) == Some(key))
            && self.option_claims().is_some()
            && (catalog.background.is_none() || self.has_desired(Lane::OptionsGlbx))
            && !matches!(
                self.health(Lane::OptionsGlbx).status().phase,
                Phase::Failed | Phase::Stopped
            )
        {
            return Ok(());
        }
        self.update_options_catalog(catalog, true)?;
        if memo.configuration != Some(configuration) {
            memo.models.clear();
            memo.configuration = Some(configuration);
        }
        if memo.models.len()
            + models
                .iter()
                .filter(|(id, _)| !memo.models.contains_key(id))
                .count()
            > 2048
        {
            memo.models.clear();
        }
        memo.models.extend(models);
        Ok(())
    }
}
