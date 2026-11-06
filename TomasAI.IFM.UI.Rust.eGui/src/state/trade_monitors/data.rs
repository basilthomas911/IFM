use super::*;
use crate::models::{
    trade_contracts as typed,
    trade_presentation::{DisplayRecord, LimitDisplayRow, ObservationRow},
    wire_types::DecimalText,
};
use crate::state::monitor_cache::{History, Realtime};

/// Owns one trade monitor's authoritative snapshots, read generations and subscription state.
pub(super) struct Monitor {
    /// Unique owner lifetime used to reject results from an earlier open/close cycle.
    pub incarnation: uuid::Uuid,
    /// Identity and routing scope against which requests and incoming results are checked.
    pub scope: Scope,
    /// Established trade definition that anchors this monitor's identity and original composition.
    pub trade: typed::EstablishedTradeDefinition,
    /// Caption derived once from the saved trade composition.
    pub tab_title: String,
    /// Published strategy type used when selecting monitor and history representations.
    pub trade_type: String,
    /// Published opening/closing action associated with the selected trade.
    pub trade_action: String,
    /// Fund caption retained for monitor publications.
    pub fund_name: String,
    /// Context required to interpret the current operation or publication.
    pub context: BusinessContext,
    /// Retained historical data used by this component.
    pub history: History,
    /// Current accepted realtime state, kept separate from historical snapshots.
    pub realtime: Realtime,
    /// Saved position selected for history display and optional detail hydration.
    pub selected: Option<typed::StrategyPositionSnapshot>,
    /// Accepted option observations keyed by their contract identity.
    pub observations: BTreeMap<String, typed::FuturesOptionTickDataV2ReadModel>,
    /// Published trade limits; this UI does not calculate replacements.
    pub limits: Option<typed::TradeLimitReadModel>,
    /// Published balance retained as exact decimal text.
    pub balance: Option<DecimalText>,
    /// Whether live monitoring is enabled for this owner.
    pub live: bool,
    /// Whether the monitored feed is currently collecting updates.
    pub collecting: bool,
    /// Whether live monitoring is awaiting its startup result.
    pub starting: bool,
    /// Whether the market-data feed has reported a stopped state.
    pub market_stopped: bool,
    /// Whether subscription cleanup still requires authoritative confirmation.
    pub cleanup_unconfirmed: bool,
    /// Current status text or state exposed by this component.
    pub status: String,
    /// Timestamp of the last accepted feed update.
    pub last_feed_at: Option<chrono::DateTime<chrono::Utc>>,
    /// Revision captured by detail reads so late results cannot replace newer selections.
    pub detail_generation: u64,
    /// Revision captured by chart reads so late results cannot replace a newer request.
    pub chart_generation: u64,
    /// Whether a chart read is outstanding for the current chart generation.
    pub chart_loading: bool,
    /// Whether optional observation hydration is still outstanding.
    pub observations_loading: bool,
    /// Most recent observation-hydration error exposed to the operator.
    pub observations_error: String,
    /// Backend value date used when requesting plan history.
    pub plan_date: chrono::NaiveDate,
    /// Accepted page of saved plan history, including its backend paging metadata.
    pub page: Option<typed::StrategyTradePlanHistoryPage>,
    /// Live plan snapshots keyed by backend plan identity.
    pub live_plans: BTreeMap<(i64, i64), typed::StrategyTradePlanSnapshot>,
    /// Accepted chart series kept separate from live position and plan state.
    chart: Option<crate::models::trade_chart::TradeChartSeries>,
    /// Content revision used to invalidate the encoded chart cache.
    chart_version: u64,
    /// Weak encoded-chart cache paired with the chart content revision.
    chart_row: std::cell::RefCell<Option<(u64, crate::models::row_data::WeakRowData)>>,
    /// Weak references to reusable immutable row payloads; inactive publications are not retained.
    row_payloads: std::cell::RefCell<BTreeMap<String, crate::models::row_data::WeakRowData>>,
    /// Most recent chart-read error exposed alongside retained chart data.
    pub chart_error: String,
    /// Most recent plan-history error exposed alongside retained history.
    pub plan_error: String,
    /// Weak references and display stamps used to reuse unchanged compact plan payloads.
    plan_rows: std::cell::RefCell<
        Vec<(
            std::sync::Weak<typed::StrategyTradePlanSnapshot>,
            crate::models::row_data::WeakRowData,
            String,
        )>,
    >,
}


/// Formats a monitor flag using the canonical True/False context text.
fn flag(value: bool) -> String {
    if value { "True" } else { "False" }.into()
}
impl Monitor {
    /// Constructs Monitor from the supplied inputs.
    pub fn new(
        request: OpenRequest,
        trade: typed::EstablishedTradeDefinition,
        context: BusinessContext,
    ) -> Result<Self, String> {
        let realtime = Realtime::new(request.scope, context.value_date, &trade)?;
        let history = History::new(trade.clone(), request.trade_type.clone());
        let tab_title = crate::models::trade_presentation::loaded_trade_tab_title(
            request.scope.order,
            request.scope.trade,
            &request.trade_type,
            &trade,
        );
        Ok(Self {
            incarnation: uuid::Uuid::new_v4(),
            scope: request.scope,
            trade,
            tab_title,
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
            chart_version: 0,
            chart_row: Default::default(),
            row_payloads: Default::default(),
            chart_error: String::new(),
            plan_error: String::new(),
            plan_rows: Default::default(),
        })
    }


    /// Checks whether the established trade uses the Iron Condor strategy.
    pub fn iron(&self) -> bool {
        self.trade.strategy_kind == Some(typed::TradeStrategyKind::IronCondor)
    }


    /// Checks can live against the current data state.
    pub fn can_live(&self) -> bool {
        self.iron()
            && !self.starting
            && !self.market_stopped
            && !self.cleanup_unconfirmed
            && self.trade.status == Some(typed::EstablishedTradeStatus::Open)
            && (self.context.market_state == 2 || self.context.allow_development_emulator)
    }


    /// Provides snapshot for the monitor implementation.
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


    /// Provides position for the monitor implementation.
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


    /// Provides plan for the monitor implementation.
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


    /// Checks has chart against the current data state.
    pub fn has_chart(&self) -> bool {
        self.chart.is_some()
    }


    /// Sets chart for monitor.
    pub fn set_chart(&mut self, chart: Option<crate::models::trade_chart::TradeChartSeries>) {
        self.chart_version = self.chart_version.wrapping_add(1);
        self.chart = chart;
    }


    /// Reuses the immutable row payload when its encoded content is unchanged.
    fn cached_row<T: serde::Serialize>(
        &self,
        kind: &str,
        key: &str,
        value: &T,
    ) -> Result<ContractRow, String> {
        let mut row = typed_row(kind, key, value)?;
        let mut cache = self.row_payloads.borrow_mut();
        if let Some(data) = row.data.take() {
            let reused = cache
                .get(&row.id)
                .and_then(|w| w.upgrade())
                .filter(|old| old.as_ref() == data.as_ref());
            if let Some(old) = reused {
                row.data = Some(old);
            } else {
                cache.insert(row.id.clone(), data.downgrade());
                row.data = Some(data);
            }
        }
        Ok(row)
    }


    /// Reuses encoded chart data until the accepted chart revision changes.
    fn chart_row(&self) -> Result<ContractRow, String> {
        let mut cache = self.chart_row.borrow_mut();
        let data = cache
            .as_ref()
            .filter(|(v, _)| *v == self.chart_version)
            .and_then(|(_, w)| w.upgrade());
        let data = if let Some(data) = data {
            data
        } else {
            let bytes = crate::wire::encode(&crate::models::trade_chart::TradeChartRow::from(
                self.chart.as_ref(),
            ))?;
            let data = crate::models::row_data::RowData::from(bytes);
            *cache = Some((self.chart_version, data.downgrade()));
            data
        };
        Ok(ContractRow {
            id: "chart:0".into(),
            fields: BTreeMap::from([("kind".into(), "chart".into())]),
            data: Some(data),
        })
    }


    /// Appends data for monitor.
    pub fn append(&self, result: &mut ContractResult) -> Result<(), String> {
        result.rows.push(self.cached_row(
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
                    result.rows.push(self.cached_row(
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
                    .push(self.cached_row("position", &index.to_string(), &value)?);
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
            let mut cached = self.plan_rows.borrow_mut();
            cached.truncate(plans.len());
            for (index, plan) in plans.iter().enumerate() {
                let retained = cached.get(index).and_then(|(raw, data, time)| {
                    let data = data.upgrade()?;
                    let raw = raw.upgrade()?;
                    raw.history_equivalent(plan).then(|| (data, time.clone()))
                });
                let (data, action_time) = if let Some(retained) = retained {
                    retained
                } else {
                    let raw = Arc::new(plan.history_view());
                    let data = crate::models::row_data::RowData::history_plan(raw.clone());
                    let time = crate::models::trade_presentation::action_time(plan)?;
                    let entry = (Arc::downgrade(&raw), data.downgrade(), time.clone());
                    if index < cached.len() {
                        cached[index] = entry;
                    } else {
                        cached.push(entry);
                    }
                    (data, time)
                };
                result.rows.push(ContractRow {
                    id: format!("plan:{index}"),
                    fields: BTreeMap::from([
                        ("kind".into(), "plan".into()),
                        ("actionDateTimeEastern".into(), action_time),
                    ]),
                    data: Some(data),
                });
            }
            result.rows.push(self.cached_row(
                "limits",
                "0",
                &LimitDisplayRow {
                    order_id: self.scope.order,
                    trade_limit: self.limits.clone(),
                    fund_balance: self.balance.clone(),
                },
            )?);
            result.rows.push(self.chart_row()?);
            let position = if self.live {
                self.realtime
                    .current
                    .as_ref()
                    .and_then(|value| value.position.as_deref())
                    .or(self.selected.as_ref())
            } else {
                self.selected.as_ref()
            };
            result.rows.push(self.cached_row(
                "position-details",
                "0",
                &DisplayRecord {
                    value: &position,
                    history: false,
                },
            )?);
            if !self.live || self.realtime.current.is_none() {
                for (index, (key, value)) in self.observations.iter().enumerate() {
                    result.rows.push(self.cached_row(
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
