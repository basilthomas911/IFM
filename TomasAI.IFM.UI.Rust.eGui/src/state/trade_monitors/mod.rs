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
#[derive(Clone)]
pub struct OpenRequest {
    pub scope: Scope,
    pub strategy: String,
    pub trade_type: String,
    pub trade_action: String,
    pub fund_name: String,
}
#[derive(Clone, Default)]
pub struct Publication {
    pub revision: u64,
    pub result: ContractResult,
}
struct Owner {
    feed: Client,
    data: Arc<Mutex<data::Monitor>>,
    registry: Arc<Registry>,
    background: BTreeMap<&'static str, tokio::task::JoinHandle<Result<(), ()>>>,
    jobs: Vec<tokio::task::JoinHandle<()>>,
}
impl Drop for Owner {
    fn drop(&mut self) {
        for job in &self.jobs {
            job.abort();
        }
        for job in self.background.values() {
            job.abort();
        }
    }
}
#[derive(Default)]
struct State {
    owners: BTreeMap<String, Owner>,
    order: Vec<String>,
    active: Option<String>,
    revision: u64,
}
#[derive(Clone)]
pub struct Hub {
    host_scope: Arc<str>,
    state: Arc<Mutex<State>>,
    updates: tokio::sync::watch::Sender<Arc<Publication>>,
}
impl Default for Hub {
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
    fn feed(&self, key: &str) -> Result<Client, String> {
        self.state
            .lock()
            .map_err(|_| "Monitor lifetime lock failed")?
            .owners
            .get(key)
            .map(|owner| owner.feed.clone())
            .ok_or("The trade tab is no longer open".into())
    }
    pub fn watch(&self) -> tokio::sync::watch::Receiver<Arc<Publication>> {
        self.updates.subscribe()
    }
    pub fn current(&self) -> Arc<Publication> {
        self.updates.borrow().clone()
    }
    fn owner(&self, key: &str) -> Result<Arc<Mutex<data::Monitor>>, String> {
        self.state
            .lock()
            .map_err(|_| "Monitor lifetime lock failed")?
            .owners
            .get(key)
            .map(|owner| owner.data.clone())
            .ok_or("The trade tab is no longer open".into())
    }
    fn active(&self) -> Result<String, String> {
        self.state
            .lock()
            .map_err(|_| "Monitor lifetime lock failed")?
            .active
            .clone()
            .ok_or("Load a trade first".into())
    }
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
                        title: format!("{}:{}", owner.scope.order, owner.scope.trade),
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
    hub: Hub,
    data: Arc<Mutex<data::Monitor>>,
    incarnation: uuid::Uuid,
}
impl Drop for StartGuard {
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
struct OpenGuard {
    hub: Hub,
    key: String,
    incarnation: uuid::Uuid,
    armed: bool,
}
impl Drop for OpenGuard {
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
