//! Typed identity/revision fences and retained history. No local valuation or business clock.
use crate::{
    models::{trade_contracts::*, trade_presentation::PositionHistoryRow},
    services::trade_monitor_queries::Scope,
};
use std::collections::{BTreeMap, BTreeSet, VecDeque};

pub struct Realtime {
    scope: Scope,
    date: String,
    position: StrategyPositionId,
    initial_revision: Option<i32>,
    initial_legs: Option<Vec<TradeLegDefinition>>,
    pub current: Option<TradeRealtimeSnapshot>,
    retired: BTreeSet<String>,
    retired_order: VecDeque<String>,
}
fn stamp(value: Option<&str>) -> Option<chrono::DateTime<chrono::FixedOffset>> {
    value.and_then(|value| chrono::DateTime::parse_from_rfc3339(value).ok())
}
impl Realtime {
    pub fn new(
        scope: Scope,
        date: chrono::NaiveDate,
        trade: &EstablishedTradeDefinition,
    ) -> Result<Self, String> {
        let strategy = trade
            .strategy_kind
            .as_ref()
            .ok_or("Missing strategy")?
            .label();
        let (_, position) = scope.typed_position(&strategy)?;
        Ok(Self {
            scope,
            date: date.to_string(),
            position,
            initial_revision: trade.evidence_revision,
            initial_legs: trade.legs.clone(),
            current: None,
            retired: BTreeSet::new(),
            retired_order: VecDeque::new(),
        })
    }
    pub fn accept(&mut self, snapshot: TradeRealtimeSnapshot) -> bool {
        let Some(generation) = snapshot
            .monitoring_generation
            .as_deref()
            .filter(|value| uuid::Uuid::parse_str(value).is_ok_and(|id| !id.is_nil()))
        else {
            return false;
        };
        let Some(legs) = snapshot
            .legs
            .as_ref()
            .filter(|legs| (1..=64).contains(&legs.len()))
        else {
            return false;
        };
        let reference = self.current.as_ref();
        let composition = reference
            .map(|value| value.composition_version)
            .unwrap_or(self.initial_revision);
        let mut identities = BTreeSet::new();
        if snapshot.schema_version != Some(1)
            || snapshot.trade_id.as_ref() != Some(&self.scope.typed_id())
            || snapshot.position_id.as_ref() != Some(&self.position)
            || snapshot.value_date.as_ref().map(|value| value.as_str()) != Some(self.date.as_str())
            || snapshot
                .snapshot_revision
                .is_none_or(|revision| revision < 0)
            || snapshot
                .composition_version
                .zip(composition)
                .is_none_or(|(new, old)| new < old)
            || self.retired.contains(generation)
            || legs.iter().any(|leg| {
                !identities.insert(
                    leg.definition
                        .as_ref()
                        .and_then(|definition| definition.trade_leg_id.clone()),
                )
            })
        {
            return false;
        }
        if snapshot.composition_version == composition {
            let expected = if let Some(previous) = reference {
                let Some(legs) = previous.legs.as_ref() else {
                    return false;
                };
                legs.iter()
                    .map(|leg| leg.definition.as_ref())
                    .collect::<Vec<_>>()
            } else {
                let Some(legs) = self.initial_legs.as_ref() else {
                    return false;
                };
                legs.iter().map(Some).collect::<Vec<_>>()
            };
            if legs.len() != expected.len()
                || legs.iter().any(|leg| {
                    !expected.iter().any(|old| {
                        let new = leg.definition.as_ref();
                        old.and_then(|value| value.trade_leg_id.as_ref())
                            == new.and_then(|value| value.trade_leg_id.as_ref())
                            && old.and_then(|value| value.contract_id.as_ref())
                                == new.and_then(|value| value.contract_id.as_ref())
                    })
                })
            {
                return false;
            }
        }
        if let Some(previous) = reference {
            if snapshot.monitoring_generation == previous.monitoring_generation {
                if snapshot.snapshot_revision <= previous.snapshot_revision {
                    return false;
                }
            } else {
                if stamp(
                    snapshot
                        .published_at_utc
                        .as_ref()
                        .map(|value| value.as_str()),
                )
                .zip(stamp(
                    previous
                        .published_at_utc
                        .as_ref()
                        .map(|value| value.as_str()),
                ))
                .is_none_or(|(new, old)| new <= old)
                {
                    return false;
                }
                let Some(old) = previous.monitoring_generation.as_ref() else {
                    return false;
                };
                self.retired.insert(old.clone());
                self.retired_order.push_back(old.clone());
                if self.retired_order.len() > 128 {
                    self.retired
                        .remove(&self.retired_order.pop_front().unwrap());
                }
            }
        }
        self.current = Some(snapshot);
        true
    }
}

pub struct History {
    trade: EstablishedTradeDefinition,
    trade_type: String,
    positions: BTreeMap<(String, String), StrategyPositionSnapshot>,
    insertion: BTreeMap<(String, String), usize>,
}
fn history_key(position: &StrategyPositionSnapshot) -> (String, String) {
    let date = position
        .history_valuation
        .as_ref()
        .and_then(|value| value.value_date.as_ref())
        .map(ToString::to_string)
        .unwrap_or_default();
    let phase = match position.phase.as_ref() {
        Some(StrategyPositionPhase::EndOfDay | StrategyPositionPhase::MarkToMarket) => {
            "MarkToMarket".into()
        }
        Some(phase) => phase.label().into_owned(),
        None => String::new(),
    };
    (date, phase)
}
fn history_time(
    position: &StrategyPositionSnapshot,
) -> Option<chrono::DateTime<chrono::FixedOffset>> {
    let value = match position.history_as_of_utc.as_ref() {
        Some(value) if value.as_str().starts_with("0001-") => position.as_of_utc.as_ref(),
        value => value,
    };
    stamp(value.map(|value| value.as_str()))
}
impl History {
    pub fn new(trade: EstablishedTradeDefinition, trade_type: String) -> Self {
        Self {
            trade,
            trade_type,
            positions: BTreeMap::new(),
            insertion: BTreeMap::new(),
        }
    }
    pub fn remember(&mut self, position: StrategyPositionSnapshot) -> bool {
        if position.history_valuation.is_none()
            || position.id.as_ref().and_then(|id| id.trade.as_ref()) != self.trade.id.as_ref()
        {
            return false;
        }
        let Some(date) = position
            .history_valuation
            .as_ref()
            .and_then(|value| value.value_date.as_ref())
            .filter(|date| date.as_str() != "0001-01-01")
        else {
            return false;
        };
        let _ = date;
        let key = history_key(&position);
        if let Some(previous) = self.positions.get(&key) {
            if previous.phase == Some(StrategyPositionPhase::EndOfDay)
                && position.phase == Some(StrategyPositionPhase::MarkToMarket)
            {
                return false;
            }
            let rank = |value: &StrategyPositionSnapshot| {
                (
                    value.route_generation.unwrap_or(-1),
                    value.position_sequence.unwrap_or(-1),
                )
            };
            if !(position.phase == Some(StrategyPositionPhase::EndOfDay)
                && previous.phase == Some(StrategyPositionPhase::MarkToMarket))
                && rank(&position) < rank(previous)
            {
                return false;
            }
            if previous == &position {
                return false;
            }
        }
        let next = self.insertion.len();
        self.insertion.entry(key.clone()).or_insert(next);
        self.positions.insert(key, position);
        true
    }
    pub fn positions(&self) -> Vec<StrategyPositionSnapshot> {
        let mut rows = self
            .positions
            .values()
            .filter(|position| {
                position.phase != Some(StrategyPositionPhase::Open)
                    && position.history_valuation.is_some()
            })
            .cloned()
            .collect::<Vec<_>>();
        rows.sort_by(|a, b| {
            history_key(a)
                .0
                .cmp(&history_key(b).0)
                .then_with(|| history_time(a).cmp(&history_time(b)))
                .then_with(|| {
                    self.insertion
                        .get(&history_key(a))
                        .cmp(&self.insertion.get(&history_key(b)))
                })
        });
        rows
    }
    pub fn saved(&self, index: usize) -> Option<StrategyPositionSnapshot> {
        if index == 0 {
            self.trade.opening_position_snapshot.as_deref().cloned()
        } else {
            self.positions().get(index - 1).cloned()
        }
    }
    pub fn rows(&self) -> Vec<PositionHistoryRow> {
        let Some(opening) = self.trade.opening_history_valuation.as_ref() else {
            return vec![];
        };
        if !self
            .trade
            .legs
            .as_ref()
            .is_some_and(|legs| legs.iter().any(|leg| leg.expiry.is_some()))
        {
            return vec![];
        }
        let row = |value: &StrategyPositionHistoryValuation,
                   status: &str,
                   pnl: Option<crate::models::wire_types::DecimalText>| {
            let id = self.trade.id.as_ref();
            PositionHistoryRow {
                order_id: id.and_then(|id| id.order_id),
                trade_id: id.and_then(|id| id.trade_id),
                trade_type: self.trade_type.clone(),
                value_date: value.value_date.clone(),
                days_to_expiry: value.days_to_expiry,
                trade_status: status.into(),
                commission: value.commission.clone(),
                net_spread: value.net_price.clone(),
                trade_pnl: pnl,
                is_valid: true,
            }
        };
        let mut rows = vec![row(
            opening,
            "Open",
            self.trade
                .opening_position_snapshot
                .as_ref()
                .and_then(|position| position.daily_pnl.clone()),
        )];
        for position in self.positions() {
            let status = match position.phase {
                Some(StrategyPositionPhase::EndOfDay) => "EndOfDay",
                Some(StrategyPositionPhase::Close) => "Close",
                _ => "IntraDay",
            };
            if let Some(value) = position.history_valuation.as_ref() {
                rows.push(row(value, status, position.daily_pnl.clone()));
            }
        }
        rows
    }
}
