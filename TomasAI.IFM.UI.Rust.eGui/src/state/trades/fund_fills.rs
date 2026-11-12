use super::{Json, TradeController, field};
use crate::{
    backend::{
        contracts::{ActorSubject, date_only_wire, into_service_result},
        schema::Registry,
        transport::Backend,
    },
    contract_editor::{ContractPublication, ContractResult},
    models::{trade_contracts::ExecutionFillEvidence, wire_types::FromBackend},
    services::{broker_order_ui, broker_reads, business_context::BusinessContext},
};
use chrono::NaiveDate;
use rmpv::Value;
use serde::{Deserialize, Serialize};
use tokio::time::{Duration, Instant};

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct Fund {
    portfolio: i64,
    fund: i64,
}

/// Committed orders and fills shared by both Rust eGui views.
#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct Order {
    pub order_id: i64,
    pub trade_id: Option<i64>,
    pub attempt: String,
    pub status: String,
    pub order_time: String,
    #[serde(default)]
    pub composition: String,
    pub order_quantity: Option<i32>,
    pub filled_quantity: Option<i32>,
    pub last_fill: String,
    pub rejection: String,
    pub fills: Vec<Fill>,
}

/// Display fields from one committed execution fill.
#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct Fill {
    pub id: String,
    pub contract: String,
    pub quantity: i32,
    pub price: String,
    pub commission: String,
    pub time: String,
}

impl From<ExecutionFillEvidence> for Fill {
    fn from(value: ExecutionFillEvidence) -> Self {
        Self {
            id: value.execution_fill_id.unwrap_or_default(),
            contract: value.contract_id.or(value.trade_leg_id).unwrap_or_default(),
            quantity: value.signed_quantity.unwrap_or_default(),
            price: value
                .price
                .map(|price| price.to_string())
                .unwrap_or_default(),
            commission: value
                .commission
                .map(|commission| commission.to_string())
                .unwrap_or_default(),
            time: value
                .filled_at_utc
                .map(|time| time.to_string())
                .unwrap_or_default(),
        }
    }
}

/// Broker component and revision-based capabilities for the selected order.
#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct BrokerControl {
    pub id: String,
    pub caption: String,
    pub price: String,
    pub mutation: crate::models::broker_presentation::BrokerMutation,
}

/// The current Fund order fills view.
#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct Snapshot {
    #[serde(skip)]
    fund: Option<Fund>,
    pub date: String,
    pub orders: Vec<Order>,
    pub selected: Option<(i64, String)>,
    pub controls: Vec<BrokerControl>,
}

impl Snapshot {
    fn select(&mut self, previous: Option<&Self>) {
        let previous = previous
            .filter(|previous| previous.fund == self.fund && previous.date == self.date)
            .and_then(|previous| previous.selected.as_ref());
        let row = previous
            .and_then(|key| {
                self.orders
                    .iter()
                    .find(|row| row.order_id == key.0 && row.attempt == key.1)
                    .or_else(|| self.orders.iter().find(|row| row.order_id == key.0))
            })
            .or_else(|| self.orders.first());
        self.selected = row.map(|row| (row.order_id, row.attempt.clone()));
    }

    async fn broker_evidence(
        &self,
        backend: &Backend,
        registry: &Registry,
        deadline: Instant,
    ) -> Result<broker_reads::Evidence, String> {
        let (Some(fund), Some((order, attempt))) = (self.fund, self.selected.as_ref()) else {
            return Ok(broker_reads::Evidence::default());
        };
        let attempt_id = uuid::Uuid::parse_str(attempt).map_err(|_| "Invalid execution attempt")?;
        if attempt_id.is_nil() {
            return Ok(broker_reads::Evidence::default());
        }
        let scope = broker_reads::Scope {
            portfolio: i32::try_from(fund.portfolio).map_err(|_| "Invalid Portfolio")?,
            fund: i32::try_from(fund.fund).map_err(|_| "Invalid Fund")?,
            order: i32::try_from(*order).map_err(|_| "Invalid order")?,
        };
        let mut orders = broker_reads::orders(backend, registry, scope, deadline).await?;
        orders.retain(|order| {
            order
                .id
                .as_ref()
                .and_then(|id| id.execution.as_ref())
                .and_then(|id| id.execution_attempt_id.as_deref())
                .and_then(|id| uuid::Uuid::parse_str(id).ok())
                == Some(attempt_id)
        });
        let execution = if orders.is_empty() {
            None
        } else {
            broker_reads::execution(backend, registry, scope, attempt_id, deadline).await?
        };
        Ok(broker_reads::Evidence {
            orders,
            executions: execution.into_iter().collect(),
        })
    }

    pub(super) fn append(&self, view: &str, result: &mut ContractResult) -> Result<(), String> {
        let bytes = rmp_serde::to_vec(self).map_err(|error| error.to_string())?;
        result
            .documents
            .insert(format!("{view}-fund-fills"), bytes.into());
        Ok(())
    }
}

impl Order {
    fn read(value: &Value) -> Result<Self, String> {
        let fields = value
            .as_array()
            .filter(|fields| fields.len() >= 5)
            .ok_or("Invalid Fund order fills row")?;
        let field = |index| fields.get(index).unwrap_or(&Value::Nil);
        let timestamp = |index| {
            Option::<crate::models::wire_types::UtcText>::from_backend(field(index))
                .map(|time| time.map(|time| time.to_string()).unwrap_or_default())
        };
        Ok(Self {
            order_id: fields[0]
                .as_i64()
                .filter(|id| *id > 0)
                .ok_or("Invalid order ID")?,
            trade_id: Option::<i64>::from_backend(&fields[1])?,
            attempt: String::from_backend(&fields[2])?,
            status: String::from_backend(&fields[3])?,
            order_time: timestamp(5)?,
            composition: Option::<
                Vec<crate::models::trade_contracts::TradeOrderComponentDefinition>,
            >::from_backend(field(10))?
            .unwrap_or_default()
            .iter()
            .map(component_caption)
            .collect::<Vec<_>>()
            .join(" / "),
            order_quantity: Option::<i32>::from_backend(field(6))?,
            filled_quantity: Option::<i32>::from_backend(field(7))?,
            last_fill: timestamp(8)?,
            rejection: Option::<String>::from_backend(field(9))?.unwrap_or_default(),
            fills: Vec::<ExecutionFillEvidence>::from_backend(&fields[4])?
                .into_iter()
                .map(Fill::from)
                .collect(),
        })
    }
}

fn component_caption(
    component: &crate::models::trade_contracts::TradeOrderComponentDefinition,
) -> String {
    use crate::models::trade_contracts::TradeStrategyKind;
    let legs = component.legs.as_deref().unwrap_or_default();
    let (strikes, maturity) = crate::models::trade_presentation::leg_caption_parts(legs);
    let [lc, sc, sp, lp] = strikes;
    let order = |left: Option<&str>, right: Option<&str>| {
        left?
            .parse::<f64>()
            .ok()?
            .partial_cmp(&right?.parse::<f64>().ok()?)
    };
    use std::cmp::Ordering::{Greater, Less};
    let trade_type = match component.strategy_kind.as_ref() {
        Some(TradeStrategyKind::IronCondor) => match (order(lc, sc), order(lp, sp)) {
            (Some(Greater), Some(Less)) => "ShortIronCondor".into(),
            (Some(Less), Some(Greater)) => "LongIronCondor".into(),
            _ => "IronCondor".into(),
        },
        Some(TradeStrategyKind::VerticalSpread) => match (order(lc, sc), order(lp, sp)) {
            (Some(Greater), None) => "CallCreditSpread".into(),
            (Some(Less), None) => "CallDebitSpread".into(),
            (None, Some(Less)) => "PutCreditSpread".into(),
            (None, Some(Greater)) => "PutDebitSpread".into(),
            _ => "VerticalSpread".into(),
        },
        Some(kind) => kind.label(),
        None => "Trade".into(),
    };
    let mut caption = trade_type.into_owned();
    if strikes.iter().any(Option::is_some) {
        let [lc, sc, sp, lp] = strikes.map(|strike| strike.unwrap_or("—"));
        caption.push_str(&format!("={lc}:{sc} - {sp}:{lp}"));
    }
    if let Some(maturity) = maturity {
        caption.push_str(&format!(" {}", maturity.format("%Y-%b-%d")));
    }
    caption
}

async fn read(
    backend: &Backend,
    fund: Fund,
    date: NaiveDate,
    deadline: Instant,
) -> Result<Snapshot, String> {
    let key = format!("{}.{}", fund.portfolio, fund.fund);
    let subject = ActorSubject::query("OrderExecutionQuery", "GetFundOrderFills", &key);
    let mut orders = Vec::new();
    let mut cursor = (0, uuid::Uuid::nil());
    loop {
        let query = Value::Array(vec![
            subject.wire(),
            Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
            fund.portfolio.into(),
            fund.fund.into(),
            date_only_wire(date),
            cursor.0.into(),
            cursor.1.to_string().into(),
        ]);
        let payload = into_service_result(
            backend
                .request_until(&subject, &query, deadline, None)
                .await?,
        )?;
        let rows = payload.as_array().ok_or("Invalid Fund order fills page")?;
        if rows.is_empty() {
            return Ok(Snapshot {
                fund: Some(fund),
                date: date.to_string(),
                orders,
                selected: None,
                controls: Vec::new(),
            });
        }
        for value in rows {
            let row = Order::read(value)?;
            let next = (
                row.order_id,
                uuid::Uuid::parse_str(&row.attempt).map_err(|error| error.to_string())?,
            );
            if next <= cursor {
                return Err("Fund order fills cursor did not advance".into());
            }
            cursor = next;
            orders.push(row);
        }
        if rows.len() < 8 {
            return Ok(Snapshot {
                fund: Some(fund),
                date: date.to_string(),
                orders,
                selected: None,
                controls: Vec::new(),
            });
        }
    }
}

impl TradeController {
    pub(super) async fn fills_action(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: Instant,
    ) -> Result<String, String> {
        let action = intent["intent"].as_str().unwrap_or_default();
        if action == "select-fill-order" {
            let view = field(intent, "fillsView");
            let order = field(intent, "fillsOrderId")
                .parse()
                .map_err(|_| "Invalid order ID")?;
            let attempt = field(intent, "fillsAttempt");
            let snapshot = self
                .fills_snapshot_mut(view)
                .ok_or("Open Order Fills first")?;
            if !snapshot
                .orders
                .iter()
                .any(|row| row.order_id == order && row.attempt == attempt)
            {
                return Err("Order selection changed".into());
            }
            snapshot.selected = Some((order, attempt.into()));
            snapshot.controls.clear();
            self.refresh_fill_controls(backend, registry, view, deadline)
                .await?;
            return Ok("Selected order".into());
        }
        let fund = match action {
            "close-blotter" => {
                self.blotter_fills = None;
                if let Some(composer) = self.composer.as_mut() {
                    composer.close(backend, registry, deadline).await?;
                }
                self.composer = None;
                return Ok("Closed Trade Blotter".into());
            }
            "leave-blotter-fills" => {
                self.blotter_fills = None;
                return Ok("Closed Order Fills".into());
            }
            "leave-loaded-fills" => {
                self.loaded_fills = None;
                return Ok("Closed Order Fills".into());
            }
            "refresh-loaded-fills" => {
                let monitor = self.monitors.current();
                let key = monitor
                    .result
                    .context
                    .get("activeTradeTab")
                    .ok_or("Load a trade first")?;
                let mut ids = key.split('.');
                let portfolio = ids
                    .next()
                    .and_then(|id| id.parse().ok())
                    .ok_or("Missing Portfolio")?;
                let fund = ids
                    .next()
                    .and_then(|id| id.parse().ok())
                    .ok_or("Missing Fund")?;
                Fund { portfolio, fund }
            }
            _ => Fund {
                portfolio: self.data.portfolio.ok_or("Select a Portfolio")?,
                fund: self.data.fund.ok_or("Select a Fund")?,
            },
        };
        let date = BusinessContext::load(backend).await?.value_date;
        let snapshot = read(backend, fund, date, deadline).await?;
        let view = if action == "refresh-loaded-fills" {
            "loaded"
        } else {
            "blotter"
        };
        let slot = if view == "loaded" {
            &mut self.loaded_fills
        } else {
            &mut self.blotter_fills
        };
        let mut snapshot = snapshot;
        snapshot.select(slot.as_ref());
        *slot = Some(snapshot);
        self.refresh_fill_controls_for_display(backend, registry, view, deadline)
            .await;
        self.message.clear();
        Ok("Loaded Fund orders and fills".into())
    }

    fn fills_snapshot(&self, view: &str) -> Option<&Snapshot> {
        match view {
            "loaded" => self.loaded_fills.as_ref(),
            "blotter" => self.blotter_fills.as_ref(),
            _ => None,
        }
    }

    fn fills_snapshot_mut(&mut self, view: &str) -> Option<&mut Snapshot> {
        match view {
            "loaded" => self.loaded_fills.as_mut(),
            "blotter" => self.blotter_fills.as_mut(),
            _ => None,
        }
    }

    pub(super) async fn fill_broker_order(
        &self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: Instant,
    ) -> Result<
        (
            crate::models::trade_contracts::BrokerOrderDefinition,
            Option<crate::models::trade_contracts::OrderExecutionDefinition>,
        ),
        String,
    > {
        let view = field(intent, "fillsView");
        let key = field(intent, "brokerOrderId");
        let snapshot = self.fills_snapshot(view).ok_or("Open Order Fills first")?;
        let selected = snapshot.selected.as_ref().ok_or("Select an order first")?;
        if field(intent, "fillsOrderId").parse::<i64>().ok() != Some(selected.0)
            || field(intent, "fillsAttempt") != selected.1
        {
            return Err("Order selection changed; review the current order".into());
        }
        let evidence = snapshot
            .broker_evidence(backend, registry, deadline)
            .await?;
        let order = evidence
            .orders
            .into_iter()
            .find(|order| {
                order
                    .id
                    .as_ref()
                    .and_then(|id| broker_reads::typed_identity(id).ok())
                    .as_deref()
                    == Some(key)
            })
            .ok_or("Selected broker component is no longer available")?;
        Ok((order, evidence.executions.into_iter().next()))
    }

    pub(super) async fn refresh_fill_controls(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        view: &str,
        deadline: Instant,
    ) -> Result<(), String> {
        let Some(snapshot) = self.fills_snapshot(view) else {
            return Ok(());
        };
        let evidence = snapshot
            .broker_evidence(backend, registry, deadline)
            .await?;
        let mut controls = Vec::new();
        for order in &evidence.orders {
            let id =
                broker_reads::typed_identity(order.id.as_ref().ok_or("Missing broker identity")?)?;
            controls.push(BrokerControl {
                caption: broker_order_ui::caption(order)?,
                price: order
                    .current_signed_net_debit_limit
                    .as_ref()
                    .map(ToString::to_string)
                    .unwrap_or_default(),
                mutation: broker_order_ui::mutation(
                    order,
                    evidence.executions.first(),
                    self.order_operations.values().any(|op| op.order_key == id),
                )?,
                id,
            });
        }
        if let Some(snapshot) = self.fills_snapshot_mut(view) {
            snapshot.controls = controls;
        }
        Ok(())
    }

    async fn refresh_fill_controls_for_display(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        view: &str,
        deadline: Instant,
    ) {
        if let Err(error) = self
            .refresh_fill_controls(backend, registry, view, deadline)
            .await
        {
            crate::diagnostics::errors::record("ui.fill_controls_unavailable", None, None, &error);
        }
    }

    /// Refresh open fills views after a committed event or reconnect.
    pub async fn refresh_fills_after_notice(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        subject: Option<&str>,
    ) -> Result<Option<ContractPublication>, String> {
        let scope = |snapshot: &Option<Snapshot>| {
            snapshot
                .as_ref()
                .and_then(|entry| entry.fund)
                .filter(|fund| matches_fund(subject, *fund))
        };
        let blotter = scope(&self.blotter_fills);
        let loaded = scope(&self.loaded_fills);
        if blotter.is_none() && loaded.is_none() {
            return Ok(None);
        }
        self.reconcile_order_operations();
        let deadline = Instant::now() + Duration::from_secs(10);
        let date = BusinessContext::load(backend).await?.value_date;
        let mut shared = None;
        if let Some(fund) = blotter {
            let mut snapshot = read(backend, fund, date, deadline).await?;
            if Some(fund) == loaded {
                shared = Some(snapshot.clone());
            }
            snapshot.select(self.blotter_fills.as_ref());
            self.blotter_fills = Some(snapshot);
            self.refresh_fill_controls_for_display(backend, registry, "blotter", deadline)
                .await;
        }
        if let Some(fund) = loaded {
            let mut snapshot = match shared {
                Some(snapshot) => snapshot,
                None => read(backend, fund, date, deadline).await?,
            };
            snapshot.select(self.loaded_fills.as_ref());
            self.loaded_fills = Some(snapshot);
            self.refresh_fill_controls_for_display(backend, registry, "loaded", deadline)
                .await;
        }
        Ok(Some(ContractPublication {
            schema: 1,
            generation: self.generation,
            status: "Completed".into(),
            result: self.result(registry)?,
            outcomes: std::mem::take(&mut self.reconciled),
            ..Default::default()
        }))
    }
}

fn matches_fund(subject: Option<&str>, fund: Fund) -> bool {
    subject.is_none_or(|subject| {
        let mut tokens = subject.split('.').skip(3);
        tokens.next().and_then(|token| token.parse::<i64>().ok()) == Some(fund.portfolio)
            && tokens.next().and_then(|token| token.parse::<i64>().ok()) == Some(fund.fund)
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::{
        trade_contracts::{TradeLegDefinition, TradeOrderComponentDefinition, TradeStrategyKind},
        wire_types::{DateText, DecimalText},
    };

    #[test]
    fn caption_uses_order_leg_roles_and_four_digit_maturity_year() {
        let leg = |put_call, signed_quantity, strike: &str| TradeLegDefinition {
            put_call: Some(put_call),
            signed_quantity: Some(signed_quantity),
            strike: Some(DecimalText(strike.into())),
            expiry: Some(DateText("2026-12-11".into())),
            ..Default::default()
        };
        let component = TradeOrderComponentDefinition {
            strategy_kind: Some(TradeStrategyKind::IronCondor),
            legs: Some(vec![
                leg(1, -1, "7900"),
                leg(2, 1, "7125"),
                leg(1, 1, "7950"),
                leg(2, -1, "7175"),
            ]),
            ..Default::default()
        };
        assert_eq!(
            component_caption(&component),
            "ShortIronCondor=7950:7900 - 7175:7125 2026-Dec-11"
        );
    }
}
