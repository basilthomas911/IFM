//! Manual order requests retain their prepared identities until an authoritative
//! projection confirms their outcome. A positive transport reply is not commit
//! evidence, and an unavailable read is never interpreted as a deleted order.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    services::{
        business_context::BusinessContext, portfolio as api, portfolio_commands as commands,
        trade_orders,
    },
};
use chrono::Datelike;
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::BTreeMap;

/// Proof used by the manual orders component.
#[derive(Clone, Copy)]
pub enum Proof {
    Created {
        /// Key associated with this proof.
        key: uuid::Uuid,
        /// Month associated with this proof.
        month: chrono::NaiveDate,
    },
    Added {
        /// Order associated with this proof.
        order: i32,
        /// Version associated with this proof.
        version: i64,
        /// Trade associated with this proof.
        trade: i32,
    },
    Removed {
        /// Order associated with this proof.
        order: i32,
        /// Version associated with this proof.
        version: i64,
        /// Trade associated with this proof.
        trade: i32,
    },
    Deleted {
        /// Order associated with this proof.
        order: i32,
    },
}

/// Operation used by the manual orders component.
#[derive(Clone)]
pub struct Operation {
    /// Prepared associated with this operation.
    pub prepared: commands::Prepared,
    /// Portfolio associated with this operation.
    pub portfolio: i32,
    /// Fund associated with this operation.
    pub fund: i32,
    /// Proof associated with this operation.
    pub proof: Proof,
    /// Whether attempted is enabled or currently true.
    pub attempted: bool,
}

/// Checks the supplied value against the required positive-value constraint.
fn positive(row: &Json, key: &str) -> Result<i32, String> {
    row[key]
        .as_i64()
        .and_then(|v| i32::try_from(v).ok())
        .filter(|v| *v > 0)
        .ok_or(format!("Invalid {key}"))
}

/// Provides revision for the manual orders implementation.
fn revision(row: &Json, key: &str) -> Result<i64, String> {
    row[key]
        .as_i64()
        .filter(|v| *v > 0)
        .ok_or(format!("Invalid {key}"))
}

/// Provides request for the manual orders implementation.
fn request(
    registry: &Registry,
    name: &str,
    fields: BTreeMap<String, Value>,
) -> Result<Value, String> {
    registry.encode(&format!("{}.Contracts.{name}", api::ROOT), &fields)
}

/// Provides operation for the manual orders implementation.
fn operation(
    registry: &Registry,
    context: &BusinessContext,
    name: &str,
    portfolio: i32,
    fund: i32,
    command: uuid::Uuid,
    correlation: uuid::Uuid,
    payload: Value,
    proof: Proof,
) -> Result<Operation, String> {
    Ok(Operation {
        prepared: commands::prepare(
            registry,
            context,
            name,
            portfolio,
            Some(fund),
            command,
            correlation,
            BTreeMap::from([("Request".into(), payload)]),
        )?,
        portfolio,
        fund,
        proof,
        attempted: false,
    })
}

/// Provides create for the manual orders implementation.
pub fn create(
    registry: &Registry,
    context: &BusinessContext,
    portfolio: &Json,
    fund: &Json,
    key: uuid::Uuid,
    reference: &str,
) -> Result<Operation, String> {
    let p = positive(portfolio, "PortfolioId")?;
    let f = positive(fund, "FundId")?;
    if fund["PortfolioId"] != p {
        return Err("The selected Fund belongs to another Portfolio".into());
    }
    let reference = reference.trim();
    if reference.encode_utf16().count() > 2048 {
        return Err("Order reference exceeds its input bound".into());
    }
    let expires = context
        .as_of_utc
        .checked_add_signed(chrono::Duration::days(1))
        .filter(|v| v.year() <= 9999)
        .ok_or("Order expiry exceeds the backend calendar")?;
    let payload = request(
        registry,
        "CreateManualFundOrderRequest",
        BTreeMap::from([
            ("PortfolioId".into(), p.into()),
            (
                "PortfolioVersion".into(),
                revision(portfolio, "PortfolioVersion")?.into(),
            ),
            ("FundId".into(), f.into()),
            (
                "FundMandateVersion".into(),
                revision(fund, "FundMandateVersion")?.into(),
            ),
            ("Reference".into(), reference.into()),
            ("IdempotencyKey".into(), key.to_string().into()),
            ("RequestedAtUtc".into(), timestamp_wire(context.as_of_utc)),
            ("ExpiresAtUtc".into(), timestamp_wire(expires)),
        ]),
    )?;
    let command = api::create_command_id(key, &payload)?;
    let month =
        chrono::NaiveDate::from_ymd_opt(context.as_of_utc.year(), context.as_of_utc.month(), 1)
            .ok_or("Invalid API order month")?;
    operation(
        registry,
        context,
        "CreateManualFundOrderCommand",
        p,
        f,
        command,
        key,
        payload,
        Proof::Created { key, month },
    )
}

/// Provides add for the manual orders implementation.
pub fn add(
    registry: &Registry,
    context: &BusinessContext,
    order: &Json,
    trade: i32,
    primary_trade: bool,
    trade_type: &str,
    contract: &Json,
    effective: chrono::NaiveDate,
    maturity: Option<chrono::NaiveDate>,
    reference: &str,
    correlation: uuid::Uuid,
) -> Result<Operation, String> {
    let p = positive(order, "PortfolioId")?;
    let f = positive(order, "FundId")?;
    let id = positive(order, "OrderId")?;
    let version = revision(order, "AggregateVersion")?;
    let reference = reference.trim();
    if reference.is_empty() || reference.encode_utf16().count() > 2048 {
        return Err("Enter a trade reference of at most 2048 characters".into());
    }
    if trade <= 0 || version == i64::MAX {
        return Err("Invalid reserved trade identity or order version".into());
    }
    if ![
        "ShortIronCondor",
        "LongIronCondor",
        "PutCreditSpread",
        "PutDebitSpread",
        "CallCreditSpread",
        "CallDebitSpread",
        "FuturesOutright",
    ]
    .contains(&trade_type)
    {
        return Err("Select a supported trade type".into());
    }
    let contract_id = contract["ContractId"]
        .as_str()
        .filter(|v| !v.trim().is_empty())
        .ok_or("Select a current futures contract")?;
    let symbol = contract["Symbol"]
        .as_str()
        .filter(|v| !v.is_empty())
        .ok_or("Missing contract symbol")?;
    let action = if matches!(
        trade_type,
        "ShortIronCondor" | "PutCreditSpread" | "CallCreditSpread"
    ) {
        "Sell"
    } else {
        "Buy"
    };
    let payload = request(
        registry,
        "AddManualFundOrderTradeRequest",
        BTreeMap::from([
            ("PortfolioId".into(), p.into()),
            ("FundId".into(), f.into()),
            ("OrderId".into(), id.into()),
            ("ExpectedOrderVersion".into(), version.into()),
            ("TradeId".into(), trade.into()),
            ("TradeType".into(), trade_type.into()),
            ("TradeDate".into(), Value::Nil),
            (
                "MaturityDate".into(),
                maturity.map(date_only_wire).unwrap_or(Value::Nil),
            ),
            ("TradeState".into(), "NewTrade".into()),
            ("TradeAction".into(), action.into()),
            ("Reference".into(), reference.into()),
            ("PrimaryTrade".into(), primary_trade.into()),
            ("BaseContractSymbol".into(), symbol.into()),
            ("BaseContractId".into(), contract_id.into()),
            ("RequestedAtUtc".into(), timestamp_wire(context.as_of_utc)),
            ("EffectiveDate".into(), date_only_wire(effective)),
        ]),
    )?;
    operation(
        registry,
        context,
        "AddManualFundOrderTradeCommand",
        p,
        f,
        uuid::Uuid::new_v4(),
        correlation,
        payload,
        Proof::Added {
            order: id,
            version,
            trade,
        },
    )
}

/// Removes manual orders for manual orders.
pub fn remove(
    registry: &Registry,
    context: &BusinessContext,
    order: &Json,
    trade: Option<i32>,
    reason: &str,
    correlation: uuid::Uuid,
) -> Result<Operation, String> {
    let p = positive(order, "PortfolioId")?;
    let f = positive(order, "FundId")?;
    let id = positive(order, "OrderId")?;
    let version = revision(order, "AggregateVersion")?;
    if version == i64::MAX {
        return Err("Order revision overflow".into());
    }
    let mut fields = BTreeMap::from([
        ("PortfolioId".into(), p.into()),
        ("FundId".into(), f.into()),
        ("OrderId".into(), id.into()),
        ("ExpectedOrderVersion".into(), version.into()),
        ("RequestedAtUtc".into(), timestamp_wire(context.as_of_utc)),
    ]);
    let (name, payload, proof) = if let Some(trade) = trade {
        if trade <= 0 {
            return Err("Select a current trade".into());
        }
        fields.extend(BTreeMap::from([
            ("TradeId".into(), trade.into()),
            ("TradeState".into(), "".into()),
            ("ExecutionOrderId".into(), Value::Nil),
            ("ExecutionTradeId".into(), Value::Nil),
        ]));
        (
            "RemoveManualFundOrderTradeCommand",
            request(registry, "ManualFundOrderTradeMutationRequest", fields)?,
            Proof::Removed {
                order: id,
                version,
                trade,
            },
        )
    } else {
        fields.insert("Reason".into(), reason.into());
        (
            "DeleteManualFundOrderCommand",
            request(registry, "ManualFundOrderMutationRequest", fields)?,
            Proof::Deleted { order: id },
        )
    };
    operation(
        registry,
        context,
        name,
        p,
        f,
        uuid::Uuid::new_v4(),
        correlation,
        payload,
        proof,
    )
}
impl Operation {
    /// Observes only. It never republishes the command, including after an
    /// acknowledgement was lost or the owning editor was closed and reopened.
    pub async fn observe(
        &self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        deadline: tokio::time::Instant,
    ) -> Result<Option<i32>, String> {
        if let Proof::Created { key, month } = self.proof {
            let rows = trade_orders::page(
                backend,
                registry,
                context,
                "GetFundOrdersPageQuery",
                &format!("{}.{}", self.portfolio, self.fund),
                BTreeMap::from([
                    ("PortfolioId".into(), self.portfolio.into()),
                    ("FundId".into(), self.fund.into()),
                    ("OrderMonth".into(), date_only_wire(month)),
                ]),
                deadline,
            )
            .await?;
            let mut found = None;
            for row in rows {
                let projected =
                    registry.project(&trade_orders::kind("FundOrderProjectionReadModel"), &row)?;
                if projected["IdempotencyKey"] == key.to_string() {
                    if found.is_some() {
                        return Err("Multiple orders claim the retained idempotency key".into());
                    }
                    found = Some(positive(&projected, "OrderId")?);
                }
            }
            return Ok(found);
        }
        let order = match self.proof {
            Proof::Added { order, .. }
            | Proof::Removed { order, .. }
            | Proof::Deleted { order } => order,
            Proof::Created { .. } => unreachable!(),
        };
        let row = api::optional_order(backend, registry, context, order, deadline).await?;
        if matches!(self.proof, Proof::Deleted { .. }) {
            return Ok(if row.is_none() { Some(order) } else { None });
        }
        let Some(row) = row else { return Ok(None) };
        let projected =
            registry.project(&trade_orders::kind("FundOrderProjectionReadModel"), &row)?;
        if projected["PortfolioId"] != self.portfolio
            || projected["FundId"] != self.fund
            || projected["OrderId"] != order
        {
            return Err("Order commit evidence crossed its retained scope".into());
        }
        let (version, trade, added) = match self.proof {
            Proof::Added { version, trade, .. } => (version, trade, true),
            Proof::Removed { version, trade, .. } => (version, trade, false),
            _ => unreachable!(),
        };
        if revision(&projected, "AggregateVersion")? <= version {
            return Ok(None);
        }
        let rows = trade_orders::page(
            backend,
            registry,
            context,
            "GetFundOrderTradesPageQuery",
            &order.to_string(),
            BTreeMap::from([("OrderId".into(), order.into())]),
            deadline,
        )
        .await?;
        let mut found = false;
        for row in rows {
            let projected = registry.project(
                &trade_orders::kind("FundOrderTradeProjectionReadModel"),
                &row,
            )?;
            if projected["PortfolioId"] != self.portfolio || projected["FundId"] != self.fund {
                return Err("Trade commit evidence crossed its retained scope".into());
            }
            if projected["TradeId"] == trade {
                found = true;
            }
        }
        Ok(if found == added { Some(order) } else { None })
    }

    /// Submits manual orders for operation.
    pub async fn submit(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        deadline: tokio::time::Instant,
    ) -> Result<i32, String> {
        if !self.attempted {
            self.prepared = commands::preparation::qualify(backend, registry, context, &self.prepared, deadline).await?;
            self.attempted = true;
            let acknowledgement = commands::submit(backend, &self.prepared, deadline).await?;
            if acknowledgement != self.prepared.command_id {
                return Err(
                    "OutcomeUnknown: command acknowledgement has a different identity".into(),
                );
            }
        }
        loop {
            match self.observe(backend, registry, context, deadline).await {
                Ok(Some(order)) => return Ok(order),
                Ok(None) => {}
                Err(error) => {
                    return Err(format!(
                        "OutcomeUnknown: manual order projection is not confirmed: {error}"
                    ));
                }
            }
            if tokio::time::Instant::now() >= deadline {
                return Err("OutcomeUnknown: manual order projection deadline expired; reload to reconcile the retained operation".into());
            }
            tokio::time::sleep_until(
                (tokio::time::Instant::now() + std::time::Duration::from_millis(50)).min(deadline),
            )
            .await;
        }
    }
}
