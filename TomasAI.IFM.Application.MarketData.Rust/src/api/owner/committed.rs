//! Exact committed future dependencies are realized by their own native futures
//! dataset. The options authority never owns that dataset's resources or waits
//! while holding a control lock; all setup consumes its original episode budget.
use super::*;
use crate::option_chain::committed::{Book, Route};
use tokio_util::sync::CancellationToken;
fn routes(book: &Book) -> Result<Vec<Route>, String> {
    let mut rows = BTreeMap::new();
    for route in book
        .plans
        .values()
        .filter(|plan| !plan.futures.is_empty())
        .flat_map(|plan| &plan.routes)
    {
        if rows
            .insert(route.contract.clone(), route.clone())
            .is_some_and(|old| old != *route)
        {
            return Err("Committed plans conflict on a fixed futures identity".into());
        }
    }
    if rows.len() > 128 {
        return Err("Committed fixed futures union exceeds capacity".into());
    }
    Ok(rows.into_values().collect())
}
impl MarketData {
    /// Rebuilds fixed future dependencies within native control. Ordinary front/
    /// term demand is preserved. This never requires C# to hold a lease or renew it.
    ///
    /// # Errors
    /// Missing native core configuration, conflicting routes, source qualification,
    /// original cancellation/deadline or actual joined dataset replacement failure.
    pub async fn realize_committed_futures(
        &self,
        book: &Book,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        let wanted = routes(book)?;
        let future = self
            .futures
            .get(&Lane::FuturesGlbx)
            .ok_or("Native GLBX futures control absent")?;
        {
            let _commands = self
                .futures_commands
                .lock()
                .map_err(|_| "Native futures command state interrupted")?;
            if let Some(intent) = future.control.intent() {
                let bootstrap = intent.bootstrap.as_ref();
                if wanted.is_empty() && bootstrap.is_none_or(|bootstrap| bootstrap.fixed.is_empty())
                {
                    return Ok(());
                }
                let bootstrap = bootstrap
                    .ok_or("Exact committed futures require native definition discovery")?;
                if bootstrap.fixed != wanted {
                    let mut discovery = bootstrap.as_ref().clone();
                    discovery.fixed = wanted.clone();
                    discovery.validate(Lane::FuturesGlbx)?;
                    let mut next = intent.as_ref().clone();
                    next.bootstrap = Some(Arc::new(discovery));
                    future
                        .control
                        .apply_before(
                            Some(Arc::new(next)),
                            Requirements::new(&[Proof::Transport, Proof::Publication]),
                            deadline,
                        )
                        .map_err(|error| {
                            format!("Native fixed futures command failed: {error:?}")
                        })?;
                }
            } else if wanted.is_empty() {
                return Ok(());
            } else {
                return Err("Native configured futures demand is unavailable".into());
            }
        }
        deadline
            .run(stop, self.wait_ready(Lane::FuturesGlbx, true))
            .await
            .map_err(|_| "Committed futures original deadline/cancellation")??;
        if !self.committed_futures_ready(book)? {
            return Err("Committed fixed futures source changed during realization".into());
        }
        Ok(())
    }
    pub(super) fn committed_futures_ready(&self, book: &Book) -> Result<bool, String> {
        let wanted = routes(book)?;
        if wanted.is_empty() {
            return Ok(true);
        }
        let source = self
            .futures
            .get(&Lane::FuturesGlbx)
            .ok_or("Native GLBX futures control absent")?;
        if source.control.recovery().status().phase != Phase::Running
            || !source.control.has_intent()
        {
            return Ok(false);
        }
        let Some(bindings) = source.factory.registrations() else {
            return Ok(false);
        };
        Ok(wanted.iter().all(|route| {
            bindings.iter().any(|binding| {
                binding.contract == route.contract
                    && binding.source == route.symbol
                    && binding.asset == crate::latest::Asset::Futures
            })
        }))
    }
}
