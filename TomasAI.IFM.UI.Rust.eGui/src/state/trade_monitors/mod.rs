//! Per-trade owners publish immutable monitor results independently of Blotter
//! commands. Locks protect short in-memory changes only; no lock crosses an await.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    contract_editor::{ContractResult, ContractRow},
    services::{
        business_context::BusinessContext,
        trade_feed_recovery::{Action, Client},
        trade_monitor_queries::{self as api, Scope},
    },
};
use serde_json::{Value as Json, json};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};
mod background;
mod commands;
mod data;
mod events;

/// Open Request used by the mod component.
#[derive(Clone)]
pub struct OpenRequest {
    /// Identity and routing scope against which requests and incoming results are checked.
    pub scope: Scope,
    /// Strategy associated with this open request.
    pub strategy: String,
    /// Trade type associated with this open request.
    pub trade_type: String,
    /// Trade action associated with this open request.
    pub trade_action: String,
    /// Fund name associated with this open request.
    pub fund_name: String,
}

/// Publication used by the mod component.
#[derive(Clone, Default)]
pub struct Publication {
    /// Revision associated with this publication.
    pub revision: u64,
    /// Result associated with this publication.
    pub result: ContractResult,
}

/// Owner used by the mod component.
struct Owner {
    /// Feed associated with this owner.
    feed: Client,
    /// Data associated with this owner.
    data: Arc<Mutex<data::Monitor>>,
    /// Registry associated with this owner.
    registry: Arc<Registry>,
    /// Keyed background retained by Owner.
    background: BTreeMap<&'static str, tokio::task::JoinHandle<Result<(), ()>>>,
    /// Ordered jobs retained by Owner.
    jobs: Vec<tokio::task::JoinHandle<()>>,
}
impl Drop for Owner {
    /// Releases resources owned by this value when its lifetime ends.
    fn drop(&mut self) {
        for job in &self.jobs {
            job.abort();
        }
        for job in self.background.values() {
            job.abort();
        }
    }
}

/// State used by the mod component.
#[derive(Default)]
struct State {
    /// Keyed owners retained by State.
    owners: BTreeMap<String, Owner>,
    /// Ordered order retained by State.
    order: Vec<String>,
    /// Optional active; `None` represents an absent value.
    active: Option<String>,
    /// Revision associated with this state.
    revision: u64,
}

/// Hub used by the mod component.
#[derive(Clone)]
pub struct Hub {
    /// Host scope associated with this hub.
    host_scope: Arc<str>,
    /// State associated with this hub.
    state: Arc<Mutex<State>>,
    /// Updates associated with this hub.
    updates: tokio::sync::watch::Sender<Arc<Publication>>,
}
impl Default for Hub {
    /// Constructs the initial state used when no explicit value is supplied.
    fn default() -> Self {
        let (updates, _) = tokio::sync::watch::channel(Arc::new(Publication::default()));
        // A production client must not silently address the Development Host.
        let fallback = if std::env::var("DOTNET_ENVIRONMENT").unwrap_or("Development".into())
            == "Development"
        {
            "development"
        } else {
            ""
        };
        let scope = crate::backend::configuration::value("IFM_UI_MARKETDATA_SCOPE")
            .ok()
            .flatten()
            .unwrap_or(fallback.into());
        Self {
            host_scope: scope.into(),
            state: Default::default(),
            updates,
        }
    }
}


/// Typed native records bypass the JSON compatibility adapter entirely.
fn typed_row<T: serde::Serialize>(kind: &str, key: &str, value: &T) -> Result<ContractRow, String> {
    Ok(ContractRow {
        id: format!("{kind}:{key}"),
        fields: BTreeMap::from([("kind".into(), kind.into())]),
        data: Some(crate::wire::encode(value)?.into()),
    })
}
impl Hub {
    /// Provides with host scope for the hub implementation.
    pub fn with_host_scope(scope: &str) -> Result<Self, String> {
        if scope.is_empty()
            || !scope
                .bytes()
                .all(|b| b.is_ascii_alphanumeric() || b == b'-')
        {
            return Err("Invalid MarketData Host deployment scope".into());
        }
        Ok(Self {
            host_scope: scope.into(),
            ..Default::default()
        })
    }


    /// Provides feed for the hub implementation.
    fn feed(&self, key: &str) -> Result<Client, String> {
        self.state
            .lock()
            .map_err(|_| "Monitor lifetime lock failed")?
            .owners
            .get(key)
            .map(|owner| owner.feed.clone())
            .ok_or("The trade tab is no longer open".into())
    }


    /// Provides watch for the hub implementation.
    pub fn watch(&self) -> tokio::sync::watch::Receiver<Arc<Publication>> {
        self.updates.subscribe()
    }


    /// Provides current for the hub implementation.
    pub fn current(&self) -> Arc<Publication> {
        self.updates.borrow().clone()
    }


    /// Provides owner for the hub implementation.
    fn owner(&self, key: &str) -> Result<Arc<Mutex<data::Monitor>>, String> {
        self.state
            .lock()
            .map_err(|_| "Monitor lifetime lock failed")?
            .owners
            .get(key)
            .map(|owner| owner.data.clone())
            .ok_or("The trade tab is no longer open".into())
    }


    /// Provides active for the hub implementation.
    fn active(&self) -> Result<String, String> {
        self.state
            .lock()
            .map_err(|_| "Monitor lifetime lock failed")?
            .active
            .clone()
            .ok_or("Load a trade first".into())
    }


    /// Publishes mod for hub.
    fn publish(&self) -> Result<(), String> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Monitor lifetime lock failed")?;
        let mut result = ContractResult {
            view: "trade-orders".into(),
            ..Default::default()
        };
        for key in &state.order {
            if let Some(owner) = state.owners.get(key) {
                let owner = owner.data.lock().map_err(|_| "Monitor state lock failed")?;
                result.rows.push(typed_row(
                    "workspace-tab",
                    key,
                    &crate::models::trade_presentation::WorkspaceTabRow {
                        key: key.clone(),
                        title: owner.tab_title.clone(),
                        kind: "trade".into(),
                        active: state.active.as_ref() == Some(key),
                    },
                )?);
            }
        }
        if let Some(key) = &state.active {
            if let Some(owner) = state.owners.get(key) {
                owner
                    .data
                    .lock()
                    .map_err(|_| "Monitor state lock failed")?
                    .append(&mut result)?;
            }
        }
        state.revision += 1;
        let revision = state.revision;
        self.updates
            .send_replace(Arc::new(Publication { revision, result }));
        Ok(())
    }


    /// Closes mod for hub.
    pub fn close(&self, key: &str) -> Result<(), String> {
        {
            let mut state = self
                .state
                .lock()
                .map_err(|_| "Monitor lifetime lock failed")?;
            state.owners.remove(key);
            state.order.retain(|old| old != key);
            if state.active.as_deref() == Some(key) {
                state.active = state.order.last().cloned();
            }
        }
        // Closing a view drops its event consumers. Saved API feed intent belongs to
        // the trading session and is removed only by an explicit Live Feed OFF.
        self.publish()
    }


    /// Provides activate for the hub implementation.
    pub fn activate(&self, key: &str) -> Result<(), String> {
        {
            let mut state = self
                .state
                .lock()
                .map_err(|_| "Monitor lifetime lock failed")?;
            if !state.owners.contains_key(key) {
                return Err("The trade tab is no longer open".into());
            }
            state.active = Some(key.into());
        }
        self.publish()
    }


    /// Merges mod for hub.
    pub fn merge(result: &mut ContractResult, monitor: &ContractResult) {
        const KINDS: &[&str] = &[
            "workspace-tab",
            "established",
            "realtime",
            "position",
            "plan",
            "limits",
            "chart",
            "position-details",
            "observation",
        ];
        result.rows.retain(|row| {
            !row.fields
                .get("kind")
                .is_some_and(|kind| KINDS.contains(&kind.as_str()))
        });
        result.rows.extend(monitor.rows.iter().cloned());
        let composer = result.context.contains_key("composerSessionId");
        for (key, value) in &monitor.context {
            if composer
                && ["live", "fundName", "compositionType", "tradeAction"].contains(&key.as_str())
            {
                continue;
            }
            result.context.insert(key.clone(), value.clone());
        }
        if !monitor.context.contains_key("activeTradeTab") {
            for (key, value) in [
                ("activeTradeTab", ""),
                ("monitorLive", "False"),
                ("monitorCanLive", "False"),
                ("monitorCanSelectPosition", "False"),
                ("live", "False"),
                ("canLive", "False"),
                ("canSelectPosition", "False"),
            ] {
                if !composer || key != "live" {
                    result.context.insert(key.into(), value.into());
                }
            }
        }
    }
}

/// Cancellation can drop a controller future at any await. Clear only the
/// operation's own incarnation, and publish the resulting finite UI state.
struct StartGuard {
    /// Hub associated with this start guard.
    hub: Hub,
    /// Data associated with this start guard.
    data: Arc<Mutex<data::Monitor>>,
    /// Unique owner lifetime used to reject results from an earlier open/close cycle.
    incarnation: uuid::Uuid,
}
impl Drop for StartGuard {
    /// Releases resources owned by this value when its lifetime ends.
    fn drop(&mut self) {
        let changed = if let Ok(mut state) = self.data.lock() {
            if state.incarnation == self.incarnation && state.starting {
                state.starting = false;
                state.collecting = false;
                state.status =
                    "Live-feed change was interrupted; saved API intent requires reconciliation"
                        .into();
                true
            } else {
                false
            }
        } else {
            false
        };
        if changed {
            let _ = self.hub.publish();
        }
    }
}

/// Open Guard used by the mod component.
struct OpenGuard {
    /// Hub associated with this open guard.
    hub: Hub,
    /// Key associated with this open guard.
    key: String,
    /// Unique owner lifetime used to reject results from an earlier open/close cycle.
    incarnation: uuid::Uuid,
    /// Whether armed is enabled or currently true.
    armed: bool,
}
impl Drop for OpenGuard {
    /// Releases resources owned by this value when its lifetime ends.
    fn drop(&mut self) {
        if !self.armed {
            return;
        }
        let same = self
            .hub
            .owner(&self.key)
            .ok()
            .and_then(|data| {
                data.lock()
                    .ok()
                    .map(|data| data.incarnation == self.incarnation)
            })
            .unwrap_or(false);
        if same {
            let _ = self.hub.close(&self.key);
        }
    }
}

#[cfg(test)]
mod two_phase_tests;
