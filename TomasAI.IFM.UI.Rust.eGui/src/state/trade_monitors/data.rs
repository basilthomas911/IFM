use super::*;
use crate::models::{
    trade_contracts as typed,
    trade_presentation::{DisplayRecord, LimitDisplayRow, ObservationRow},
    wire_types::DecimalText,
};
use crate::state::monitor_cache::{History, Realtime};
pub(super) struct Monitor {
    pub incarnation: uuid::Uuid,
    pub scope: Scope,
    pub trade: typed::EstablishedTradeDefinition,
    pub trade_type: String,
    pub trade_action: String,
    pub fund_name: String,
    pub context: BusinessContext,
    pub history: History,
    pub realtime: Realtime,
    pub selected: Option<typed::StrategyPositionSnapshot>,
    pub observations: BTreeMap<String, typed::FuturesOptionTickDataV2ReadModel>,
    pub limits: Option<typed::TradeLimitReadModel>,
    pub balance: Option<DecimalText>,
    pub live: bool,
    pub collecting: bool,
    pub starting: bool,
    pub market_stopped: bool,
    pub cleanup_unconfirmed: bool,
    pub status: String,
    pub last_feed_at: Option<chrono::DateTime<chrono::Utc>>,
    pub detail_generation: u64,
    pub chart_generation: u64,
    pub chart_loading: bool,
    pub observations_loading: bool,
    pub observations_error: String,
    pub plan_date: chrono::NaiveDate,
    pub page: Option<typed::StrategyTradePlanHistoryPage>,
    pub live_plans: BTreeMap<(i64, i64), typed::StrategyTradePlanSnapshot>,
    pub chart: Option<crate::models::trade_chart::TradeChartSeries>,
    pub chart_error: String,
    pub plan_error: String,
}
fn flag(value: bool) -> String {
    if value { "True" } else { "False" }.into()
}
impl Monitor {
    pub fn new(
        request: OpenRequest,
        trade: typed::EstablishedTradeDefinition,
        context: BusinessContext,
    ) -> Result<Self, String> {
        let realtime = Realtime::new(request.scope, context.value_date, &trade)?;
        let history = History::new(trade.clone(), request.trade_type.clone());
        Ok(Self {
            incarnation: uuid::Uuid::new_v4(),
            scope: request.scope,
            trade,
            trade_type: request.trade_type,
            trade_action: request.trade_action,
            fund_name: request.fund_name,
            plan_date: context.value_date,
            context,
            history,
            realtime,
            detail_generation: 0,
            chart_generation: 0,
            chart_loading: false,
            observations_loading: false,
            observations_error: String::new(),
            selected: None,
            observations: BTreeMap::new(),
            limits: None,
            balance: None,
            live: false,
            collecting: false,
            starting: false,
            market_stopped: false,
            cleanup_unconfirmed: false,
            status: String::new(),
            last_feed_at: None,
            page: None,
            live_plans: BTreeMap::new(),
            chart: None,
            chart_error: String::new(),
            plan_error: String::new(),
        })
    }
    pub fn iron(&self) -> bool {
        self.trade.strategy_kind == Some(typed::TradeStrategyKind::IronCondor)
    }
    pub fn can_live(&self) -> bool {
        self.iron()
            && !self.starting
            && !self.market_stopped
            && !self.cleanup_unconfirmed
            && self.trade.status == Some(typed::EstablishedTradeStatus::Open)
            && (self.context.market_state == 2 || self.context.allow_development_emulator)
    }
    pub fn snapshot(&mut self, value: typed::TradeRealtimeSnapshot) -> bool {
        if value
            .position
            .as_ref()
            .is_some_and(|position| position.id != value.position_id)
        {
            return false;
        }
        if !self.realtime.accept(value) {
            return false;
        }
        if self.live
            && self
                .status
                .starts_with("Live feed ON - waiting for an authoritative snapshot:")
        {
            self.status.clear();
        }
        let position = self
            .realtime
            .current
            .as_ref()
            .and_then(|value| value.position.clone());
        let plan = self
            .realtime
            .current
            .as_ref()
            .and_then(|value| value.plan.clone());
        if let Some(position) = position {
            self.history.remember(*position);
        }
        if let Some(plan) = plan {
            self.plan(*plan);
        }
        true
    }
    pub fn position(&mut self, value: typed::StrategyPositionSnapshot) -> bool {
        if self
            .scope
            .typed_position("IronCondor")
            .map_or(true, |(_, id)| value.id.as_ref() != Some(&id))
        {
            return false;
        }
        let changed = self.history.remember(value.clone());
        if changed && !self.live && value.phase == Some(typed::StrategyPositionPhase::EndOfDay) {
            self.detail_generation += 1;
            self.observations_loading = false;
            self.selected = Some(value);
        }
        changed
    }
    pub fn plan(&mut self, value: typed::StrategyTradePlanSnapshot) -> bool {
        if self
            .scope
            .typed_position("IronCondor")
            .map_or(true, |(_, id)| {
                value
                    .position
                    .as_ref()
                    .and_then(|position| position.id.as_ref())
                    != Some(&id)
            })
            || value.value_date.as_ref().map(|date| date.as_str())
                != Some(self.context.value_date.to_string().as_str())
        {
            return false;
        }
        if !(self.live || self.collecting) {
            return false;
        }
        let Some(key) = value
            .position
            .as_ref()
            .and_then(|position| position.route_generation)
            .zip(value.plan_revision)
        else {
            return false;
        };
        if self.live_plans.get(&key) == Some(&value) {
            return false;
        }
        self.live_plans.insert(key, value);
        while self.live_plans.len() > 100 {
            self.live_plans.pop_first();
        }
        true
    }
    pub fn append(&self, result: &mut ContractResult) -> Result<(), String> {
        result.rows.push(typed_row(
            "established",
            &self.scope.key()?,
            &DisplayRecord {
                value: &self.trade,
                history: false,
            },
        )?);
        if self.iron() {
            if self.live {
                if let Some(value) = &self.realtime.current {
                    result.rows.push(typed_row(
                        "realtime",
                        "0",
                        &DisplayRecord {
                            value,
                            history: false,
                        },
                    )?);
                }
            }
            for (index, value) in self.history.rows().into_iter().enumerate() {
                result
                    .rows
                    .push(typed_row("position", &index.to_string(), &value)?);
            }
            let plans = if self.live && !self.live_plans.is_empty() {
                self.live_plans.values().rev().collect::<Vec<_>>()
            } else {
                self.page
                    .as_ref()
                    .and_then(|page| page.items.as_ref())
                    .map(|rows| rows.iter().collect())
                    .unwrap_or_default()
            };
            for (index, plan) in plans.iter().enumerate() {
                let mut entry = typed_row(
                    "plan",
                    &index.to_string(),
                    &DisplayRecord {
                        value: *plan,
                        history: true,
                    },
                )?;
                entry.fields.insert(
                    "actionDateTimeEastern".into(),
                    crate::models::trade_presentation::action_time(plan)?,
                );
                result.rows.push(entry);
            }
            result.rows.push(typed_row(
                "limits",
                "0",
                &LimitDisplayRow {
                    order_id: self.scope.order,
                    trade_limit: self.limits.clone(),
                    fund_balance: self.balance.clone(),
                },
            )?);
            result.rows.push(typed_row(
                "chart",
                "0",
                &crate::models::trade_chart::TradeChartRow::from(self.chart.as_ref()),
            )?);
            let position = if self.live {
                self.realtime
                    .current
                    .as_ref()
                    .and_then(|value| value.position.as_deref())
                    .or(self.selected.as_ref())
            } else {
                self.selected.as_ref()
            };
            result.rows.push(typed_row(
                "position-details",
                "0",
                &DisplayRecord {
                    value: &position,
                    history: false,
                },
            )?);
            if !self.live || self.realtime.current.is_none() {
                for (index, (key, value)) in self.observations.iter().enumerate() {
                    result.rows.push(typed_row(
                        "observation",
                        &index.to_string(),
                        &ObservationRow {
                            key: key.clone(),
                            value: value.clone(),
                        },
                    )?);
                }
            }
        }
        let maximum = self
            .page
            .as_ref()
            .map(|page| {
                page.maximum_revision
                    .map(|value| value.to_string())
                    .unwrap_or_default()
            })
            .unwrap_or("0".into());
        let latest = self
            .live_plans
            .keys()
            .next_back()
            .map(|(_, revision)| revision.to_string())
            .unwrap_or("0".into());
        for (key, value) in [
            (
                "latestPlanRevision",
                if self.live { latest } else { "0".into() },
            ),
            ("planMaximumRevision", maximum),
            (
                "planOffset",
                self.page
                    .as_ref()
                    .map(|page| {
                        page.offset
                            .map(|value| value.to_string())
                            .unwrap_or_default()
                    })
                    .unwrap_or("0".into()),
            ),
            (
                "planTotal",
                self.page
                    .as_ref()
                    .map(|page| {
                        page.total_count
                            .map(|value| value.to_string())
                            .unwrap_or_default()
                    })
                    .unwrap_or("1".into()),
            ),
            ("planLoaded", flag(self.live || self.page.is_some())),
            ("planDate", self.plan_date.to_string()),
            ("chartError", self.chart_error.clone()),
            ("chartLoading", flag(self.chart_loading)),
            ("observationsLoading", flag(self.observations_loading)),
            ("observationsError", self.observations_error.clone()),
            ("planError", self.plan_error.clone()),
            ("monitorStatus", self.status.clone()),
            ("monitorLive", flag(self.live)),
            ("monitorCanLive", flag(self.can_live())),
            (
                "monitorCanSelectPosition",
                flag(!self.starting && !self.live),
            ),
            ("activeTradeTab", self.scope.key()?),
            ("fundName", self.fund_name.clone()),
            ("compositionType", self.trade_type.clone()),
            ("tradeAction", self.trade_action.clone()),
            ("canLive", flag(self.can_live())),
            ("live", flag(self.live)),
            ("canSelectPosition", flag(!self.starting && !self.live)),
        ] {
            result.context.insert(key.into(), value);
        }
        Ok(())
    }
}
