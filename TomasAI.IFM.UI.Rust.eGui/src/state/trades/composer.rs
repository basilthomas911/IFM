//! One native Blotter incarnation owns its detached draft and subscription.
//! Quotes/evidence/pricing are asynchronous API reads. Rendering only projects
//! the retained result, and closing releases this view's owner exclusively.
use super::*;
use crate::models::{
    broker_presentation::QualificationReport,
    cells::{Cell, Cells},
    trade_contracts::{
        BrokerOrderDefinition, FundOrderTradeProjectionReadModel, FuturesContractV3ReadModel,
        OrderExecutionDefinition, TradeOrderDefinition, TradeOrderPreview,
    },
};
use crate::services::{
    broker_initialization::{self, Initialization},
    broker_market_display,
    broker_preview::{self, Preview},
    broker_preview_display,
    broker_reads::{self, Evidence, Scope},
    composer_market::{Defaults, Market},
    option_draft,
};
fn exact_cell(value: Cell<'_>) -> Result<ExactDecimal, String> {
    ExactDecimal::parse(&value.text())
}
pub(super) struct Composer {
    pub(super) execution: crate::services::execution_context::Context,
    pub session: String,
    pub(super) scope: Scope,
    pub(super) trade: FundOrderTradeProjectionReadModel,
    market: Market,
    initialization: Option<Initialization>,
    evidence: Evidence,
    preview: Preview,
    totals: Option<TradeOrderPreview>,
    quantity: i32,
    execution_action: String,
    order_type: String,
    algorithm: String,
    time_in_force: String,
    pace: String,
    status: String,
    report_path: String,
    report_hash: String,
    report_preview: String,
    accepted_orders: Vec<TradeOrderDefinition>,
    submission_unknown: bool,
    submission_error: String,
    pub(super) preparation: Option<crate::services::order_preparation::Prepared>,
    pub(super) confirmation_id: Option<uuid::Uuid>,
}
/// NumberStyles.Number compatibility for user draft fields. This is parsing,
/// never a conversion of a backend financial value through binary float.
pub(super) fn entered(text: &str) -> Option<ExactDecimal> {
    let mut value = text.trim().replace(',', "");
    if value.ends_with('-') || value.ends_with('+') {
        let sign = value.pop()?;
        if value.starts_with(['-', '+']) {
            return None;
        }
        if sign == '-' {
            value.insert(0, '-');
        }
    }
    if value.starts_with('+') {
        value.remove(0);
    }
    if value.starts_with('.') {
        value.insert(0, '0');
    } else if value.starts_with("-.") {
        value.insert(1, '0');
    }
    if value.ends_with('.') {
        value.push('0');
    }
    ExactDecimal::parse(&value).ok()
}
fn flag(value: bool) -> String {
    if value { "True" } else { "False" }.into()
}
impl Composer {
    pub fn new(
        scope: Scope,
        trade: FundOrderTradeProjectionReadModel,
        underlying: FuturesContractV3ReadModel,
        context: &BusinessContext,
        editable: bool,
        development: bool,
    ) -> Result<Self, String> {
        scope.key()?;
        let strategy = trade
            .cell("TradeType")
            .as_str()
            .ok_or("Missing selected strategy")?
            .to_string();
        let execution_action = if trade.primary_trade == Some(false) {
            "Close"
        } else {
            "Open"
        }
        .into();
        Ok(Self {
            execution: crate::services::execution_context::Context::from_env()?,
            session: uuid::Uuid::new_v4().simple().to_string(),
            scope,
            trade,
            market: Market::new(underlying, strategy, context, editable, development),
            initialization: None,
            evidence: Default::default(),
            preview: Default::default(),
            totals: None,
            quantity: 1,
            execution_action,
            order_type: "Limit".into(),
            algorithm: "None".into(),
            time_in_force: "Day".into(),
            pace: "Normal".into(),
            status: String::new(),
            report_path: String::new(),
            report_hash: String::new(),
            report_preview: String::new(),
            preparation: None,
            confirmation_id: None,
            accepted_orders: vec![],
            submission_unknown: false,
            submission_error: String::new(),
        })
    }
    pub async fn initialize(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let contract = self
            .market
            .underlying
            .contract_id
            .as_deref()
            .ok_or("Missing underlying identity")?
            .to_string();
        let trade_id = self
            .trade
            .cell("TradeId")
            .as_i64()
            .and_then(|v| i32::try_from(v).ok())
            .filter(|v| *v > 0)
            .ok_or("Invalid selected trade")?;
        let future = self.market.strategy == "FuturesOutright";
        let requested = crate::backend::date_input::parse(
            self.trade
                .cell("RequestedTradeDate")
                .as_str()
                .ok_or("Missing requested trade date")?,
        )?;
        let through = crate::backend::date_input::parse(
            self.trade
                .cell("RequestedMaturityDate")
                .as_str()
                .unwrap_or(&requested.to_string()),
        )?;
        let initializing = broker_initialization::load(
            backend,
            registry,
            context,
            self.scope,
            trade_id,
            &contract,
            future,
            &self.execution.account_alias,
            deadline,
        );
        let selecting = async {
            if future {
                let side = if self.trade.cell("TradeAction").text() == "Sell" {
                    -1
                } else {
                    1
                };
                self.market.draft.legs = vec![option_draft::Selected {
                    quote: broker_preview::Leg {
                        contract: contract.clone(),
                        side,
                        strike: None,
                        is_call: false,
                        is_future: true,
                        bid: None,
                        ask: None,
                        delta: None,
                        vega: None,
                        multiplier: Some(exact_cell(self.market.underlying.cell("Multiplier"))?),
                    },
                    role: if side < 0 { "-Future" } else { "+Future" }.into(),
                    quote_at: None,
                    frozen: false,
                }];
                Ok(())
            } else {
                self.market
                    .initialize(backend, registry, context, requested, through, deadline)
                    .await
            }
        };
        let (initialized, selected) = tokio::join!(initializing, selecting);
        self.initialization = Some(initialized?);
        selected?;
        self.refresh_evidence(backend, registry, deadline).await?;
        if self.trade.cell("TradeState").text() != "NewTrade" {
            let previous = self.market.expiry;
            self.restore_stored_legs()?;
            if self
                .market
                .draft
                .legs
                .iter()
                .any(|leg| !leg.quote.is_future)
            {
                if self.market.expiry != previous {
                    self.market
                        .lease
                        .release(backend, registry, deadline)
                        .await?;
                }
                // Stored contract IDs remain authoritative. Refresh their quotes without
                // auto-selecting a different trade from the current chain.
                self.market.refresh(backend, registry, deadline).await?;
            }
        }
        self.refresh_preview(backend, registry, deadline).await?;
        self.refresh_account(backend, registry, deadline).await
    }
    pub(super) async fn refresh_evidence(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let mut scopes = self
            .accepted_orders
            .iter()
            .map(|order| {
                let id = order.id.as_ref().ok_or("Invalid accepted evidence scope")?;
                Ok(Scope {
                    portfolio: id
                        .portfolio_id
                        .filter(|n| *n > 0)
                        .ok_or("Invalid accepted portfolio")?,
                    fund: id
                        .fund_id
                        .filter(|n| *n > 0)
                        .ok_or("Invalid accepted fund")?,
                    order: id
                        .order_id
                        .filter(|n| *n > 0)
                        .ok_or("Invalid accepted order")?,
                })
            })
            .collect::<Result<Vec<_>, String>>()?;
        scopes.push(self.scope);
        if let Some(order) = self
            .trade
            .cell("ExecutionOrderId")
            .as_i64()
            .and_then(|v| i32::try_from(v).ok())
            .filter(|v| *v > 0)
        {
            scopes.push(Scope {
                order,
                ..self.scope
            });
        }
        self.evidence = broker_reads::evidence(backend, registry, &scopes, deadline).await?;
        Ok(())
    }
    pub(super) fn broker_order(
        &self,
        key: &str,
    ) -> Result<(BrokerOrderDefinition, Option<OrderExecutionDefinition>), String> {
        let rows = self
            .evidence
            .orders
            .iter()
            .filter(|o| {
                o.id.as_ref()
                    .is_some_and(|id| broker_reads::typed_identity(id).is_ok_and(|id| id == key))
            })
            .collect::<Vec<_>>();
        if rows.len() != 1 {
            return Err("Select a current broker order".into());
        }
        let order = rows[0].clone();
        let id = order
            .id
            .as_ref()
            .and_then(|id| id.execution.as_ref())
            .ok_or("Missing execution identity")?;
        let execution = self
            .evidence
            .executions
            .iter()
            .find(|e| {
                e.trade_order_id == id.trade_order
                    && e.execution_attempt_id == id.execution_attempt_id
            })
            .cloned();
        Ok((order, execution))
    }
    fn restore_stored_legs(&mut self) -> Result<(), String> {
        self.market.draft.manual = true;
        self.market.draft.legs.clear();
        let id = self
            .trade
            .cell("ExecutionTradeId")
            .as_i64()
            .filter(|id| *id > 0)
            .or_else(|| self.trade.cell("TradeId").as_i64())
            .ok_or("Missing stored trade identity")?;
        let mut orders: Vec<_> = self.evidence.orders.iter().collect();
        orders.sort_by(|a, b| {
            b.changed_at_utc
                .as_ref()
                .map(|v| v.as_str())
                .cmp(&a.changed_at_utc.as_ref().map(|v| v.as_str()))
        });
        let component = orders
            .into_iter()
            .filter_map(|o| o.order.as_ref().and_then(|o| o.components.as_ref()))
            .flatten()
            .find(|c| c.reserved_trade_id.map(i64::from) == Some(id));
        let Some(component) = component else {
            return Ok(());
        };
        let legs = component
            .legs
            .as_ref()
            .ok_or("Missing stored component legs")?;
        let mut quantity = i32::MAX;
        for row in legs {
            let signed = row
                .cell("SignedQuantity")
                .as_i64()
                .and_then(|v| i32::try_from(v).ok())
                .ok_or("Invalid stored leg quantity")?;
            quantity = quantity.min(signed.checked_abs().ok_or("Stored leg quantity overflow")?);
            let future = row.cell("AssetFamily").text() == "Futures";
            let call = row.put_call == Some(1);
            let side = if signed < 0 { -1 } else { 1 };
            let role = if future {
                format!("{}Future", if side < 0 { "-" } else { "+" })
            } else {
                format!(
                    "{}{}",
                    if side < 0 { "-S" } else { "+L" },
                    if call { "C" } else { "P" }
                )
            };
            self.market.draft.legs.push(option_draft::Selected {
                quote: broker_preview::Leg {
                    contract: row.contract_id.clone().ok_or("Missing stored contract")?,
                    side,
                    strike: if row.strike.is_none() {
                        None
                    } else {
                        Some(exact_cell(row.cell("Strike"))?)
                    },
                    is_call: call,
                    is_future: future,
                    bid: None,
                    ask: None,
                    delta: None,
                    vega: None,
                    multiplier: if row.cash_multiplier.is_none() {
                        None
                    } else {
                        Some(exact_cell(row.cell("CashMultiplier"))?)
                    },
                },
                role,
                quote_at: None,
                frozen: false,
            });
        }
        if let Some(expiry) = legs
            .iter()
            .filter_map(|leg| leg.expiry.as_ref().map(|v| v.as_str()))
            .map(crate::backend::date_input::parse)
            .collect::<Result<Vec<_>, _>>()?
            .into_iter()
            .max()
        {
            self.market.expiry = expiry;
        }
        self.quantity = if quantity == i32::MAX {
            1
        } else {
            quantity.max(1)
        };
        self.market.draft.entered_price = if component.signed_net_debit_limit.is_none() {
            None
        } else {
            Some(exact_cell(component.cell("SignedNetDebitLimit"))?)
        };
        Ok(())
    }
    async fn refresh_preview(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let legs = self
            .market
            .draft
            .legs
            .iter()
            .map(|l| l.quote.clone())
            .collect::<Vec<_>>();
        let reference = self
            .initialization
            .as_ref()
            .and_then(|i| i.price.as_ref())
            .map(|p| exact_cell(p.cell("ClosePrice")))
            .transpose()?;
        self.totals = Some(
            self.preview
                .load(
                    backend,
                    registry,
                    &legs,
                    self.quantity,
                    self.market.draft.entered_price.as_ref(),
                    reference.as_ref(),
                    deadline,
                )
                .await?,
        );
        Ok(())
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        if self.preparation.is_some() && intent["intent"] == "refresh-chain" {
            return Ok("Awaiting order confirmation".into());
        }
        if self.preparation.is_some()
            && !matches!(
                intent["intent"].as_str(),
                Some("account" | "refresh-execution" | "close-blotter")
            )
        {
            return Err(
                "Review or cancel the prepared confirmation before changing its draft".into(),
            );
        }
        match intent["intent"].as_str().ok_or("Missing Blotter action")? {
            "refresh-chain" => {
                if self.market.live && self.market.draft.raw.is_some() {
                    self.market.refresh(backend, registry, deadline).await?;
                }
            }
            "refresh-execution" => {
                self.refresh_evidence(backend, registry, deadline).await?;
            }
            "clear-staged" => {
                if !self.market.editable {
                    return Err("Market selection is read-only".into());
                }
                self.market.draft.clear();
            }
            "remove-leg" => {
                if !self.market.editable {
                    return Err("Market selection is read-only".into());
                }
                self.market.draft.remove(field(intent, "contractId"));
            }
            "select-leg" => {
                let d = self
                    .market
                    .defaults
                    .as_ref()
                    .ok_or("Applied startup defaults are unavailable")?;
                let multiplier = exact_cell(self.market.underlying.cell("Multiplier"))?;
                self.market.draft.select(
                    field(intent, "contractId"),
                    field(intent, "role"),
                    &option_draft::Settings {
                        strategy: &self.market.strategy,
                        call_delta: &d.call_delta,
                        put_delta: &d.put_delta,
                        call_width: &d.call_width,
                        put_width: &d.put_width,
                        multiplier: &multiplier,
                        live: self.market.live,
                        development: self.market.development,
                        editable: self.market.editable,
                    },
                )?;
            }
            "chain" => {
                let number =
                    |key: &str| entered(field(intent, key)).ok_or(format!("Invalid {key}"));
                let expiry = crate::backend::date_input::parse(field(intent, "expiry"))?;
                let defaults = Defaults {
                    call_delta: number("callDelta")?,
                    put_delta: number("putDelta")?,
                    call_width: number("callWidth")?,
                    put_width: number("putWidth")?,
                };
                self.market
                    .change(
                        backend,
                        registry,
                        context,
                        expiry,
                        field(intent, "live") == "true",
                        defaults,
                        deadline,
                    )
                    .await?;
            }
            "preview-draft" => {
                let quantity = field(intent, "quantity")
                    .parse::<i32>()
                    .ok()
                    .filter(|v| (1..=100000).contains(v))
                    .ok_or("Quantity must be between 1 and 100000")?;
                let order_type = field(intent, "orderType");
                let algorithm = field(intent, "algorithm");
                let tif = field(intent, "timeInForce");
                let pace = field(intent, "pace");
                registry.enum_value(
                    "TomasAI.IFM.Domain.Trade.Shared.BrokerOrderType",
                    order_type,
                )?;
                registry
                    .enum_value("TomasAI.IFM.Domain.Trade.Shared.BrokerAlgorithm", algorithm)?;
                if !["Day", "GTC"].contains(&tif)
                    || !["Normal", "Patient", "Urgent"].contains(&pace)
                {
                    return Err("Invalid execution selection".into());
                }
                self.quantity = quantity;
                if let Some(price) = entered(field(intent, "price")) {
                    self.market.draft.entered_price = Some(price);
                }
                self.order_type = order_type.into();
                self.algorithm = algorithm.into();
                self.time_in_force = tif.into();
                self.pace = pace.into();
                if ["Open", "Close"].contains(&field(intent, "action")) {
                    self.execution_action = field(intent, "action").into();
                }
            }
            "account" => {
                self.report_path.clear();
                self.report_hash.clear();
                self.report_preview.clear();
                let trade_id = self
                    .trade
                    .cell("TradeId")
                    .as_i64()
                    .and_then(|v| i32::try_from(v).ok())
                    .ok_or("Invalid trade")?;
                self.initialization = Some(
                    broker_initialization::load(
                        backend,
                        registry,
                        context,
                        self.scope,
                        trade_id,
                        self.market
                            .underlying
                            .contract_id
                            .as_deref()
                            .ok_or("Missing underlying")?,
                        self.market.strategy == "FuturesOutright",
                        &self.execution.account_alias,
                        deadline,
                    )
                    .await?,
                );
            }
            "qualification-report" => {
                use sha2::Digest;
                let path = std::path::absolute(field(intent, "reportPath"))
                    .map_err(|e| format!("Invalid report path: {e}"))?;
                let maximum = crate::backend::codec::MAX_MESSAGE / 4;
                let metadata = tokio::fs::metadata(&path)
                    .await
                    .map_err(|e| format!("Cannot inspect qualification report: {e}"))?;
                if !metadata.is_file() || metadata.len() > maximum as u64 {
                    return Err(
                        "Qualification report exceeds the 4 MiB display preview bound".into(),
                    );
                }
                let bytes = tokio::fs::read(&path)
                    .await
                    .map_err(|e| format!("Cannot read qualification report: {e}"))?;
                if bytes.len() > maximum {
                    return Err("Qualification report grew beyond the display preview bound".into());
                }
                self.report_path = path.to_string_lossy().into();
                self.report_hash = format!("{:x}", sha2::Sha256::digest(&bytes));
                self.report_preview = String::from_utf8_lossy(&bytes).into();
            }
            "close-blotter" => {
                self.market.close(backend, registry, deadline).await?;
                return Ok("Blotter closed".into());
            }
            action => return Err(format!("Blotter action {action} is not yet implemented")),
        }
        self.refresh_preview(backend, registry, deadline).await?;
        self.refresh_account(backend, registry, deadline).await?;
        Ok("Blotter updated".into())
    }
    pub async fn refresh_account(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let data = self
            .initialization
            .get_or_insert_with(Initialization::default);
        match broker_reads::account(backend, registry, &self.execution.account_alias, deadline)
            .await
        {
            Ok(value) => data.account = value,
            Err(error) => {
                data.account = None;
                self.status = format!("Account evidence unavailable: {error}");
            }
        }
        Ok(())
    }
    pub fn append(
        &self,
        result: &mut ContractResult,
        context: &BusinessContext,
    ) -> Result<(), String> {
        if let Some(document) = self.confirmation_document()? {
            result.documents.insert(
                "confirmation".into(),
                crate::wire::encode(&document)?.into(),
            );
        }
        if let Some(market) = &self.market.draft.display {
            result.rows.push(typed_row(
                "market",
                0,
                &broker_market_display::market(market)?,
            )?);
        }
        let mut legs: Vec<_> = self.market.draft.legs.iter().collect();
        legs.sort_by(|a, b| {
            match (&a.quote.strike, &b.quote.strike) {
                (Some(a), Some(b)) => crate::services::option_selection::compare(b, a),
                _ => std::cmp::Ordering::Equal,
            }
            .then_with(|| b.quote.is_call.cmp(&a.quote.is_call))
        });
        for (index, leg) in legs.into_iter().enumerate() {
            result
                .rows
                .push(typed_row("leg", index, &broker_market_display::leg(leg)?)?);
        }
        if let Some(chain) = &self.market.draft.raw {
            if self.totals.is_some() {
                result.rows.push(typed_row(
                    "market-metrics",
                    0,
                    &crate::services::broker_market_metrics::document(
                        chain,
                        &self.market.underlying,
                        self.totals
                            .as_ref()
                            .and_then(|p| p.resolved_tick_increment.as_ref())
                            .map(|v| v.as_str()),
                        self.market.live,
                    )?,
                )?);
            }
        }
        if self.totals.is_some() {
            let mut preview = broker_preview_display::document(
                &self.market.draft.legs,
                self.totals.as_ref().unwrap(),
                self.initialization.as_ref(),
            )?;
            if let Some(account) = self
                .initialization
                .as_ref()
                .and_then(|i| i.account.as_ref())
            {
                result.rows.push(typed_row(
                    "qualification-report",
                    0,
                    &self.report_document(),
                )?);
                result.rows.push(typed_row(
                    "account",
                    0,
                    &broker_market_display::account(account)?,
                )?);
                preview.fields.insert(
                    "Route check".into(),
                    if account.development_qualifications_exempt == Some(true) {
                        "Development emulator - qualification exempt".into()
                    } else {
                        format!(
                            "{} / {}",
                            account.cell("QualificationStatus").text(),
                            account.cell("Gate").text()
                        )
                    },
                );
            }
            result.rows.push(typed_row("broker-preview", 0, &preview)?);
        }
        if self
            .initialization
            .as_ref()
            .and_then(|i| i.account.as_ref())
            .is_none()
            || self.totals.is_none()
        {
            result.rows.push(typed_row(
                "qualification-report",
                0,
                &self.report_document(),
            )?);
        }
        result.choices.insert(
            "expiry".into(),
            self.market
                .expirations
                .iter()
                .filter_map(|e| {
                    e.expiry_date
                        .as_ref()
                        .map(|value| value.as_str().to_string())
                })
                .collect::<std::collections::BTreeSet<_>>()
                .into_iter()
                .collect(),
        );
        let defaults = self.market.defaults.as_ref();
        let loaded = defaults.is_some() || self.market.strategy == "FuturesOutright";
        let quantity = self.market.draft.legs.len();
        let required = if self.market.strategy == "FuturesOutright" {
            1
        } else if self.market.strategy.ends_with("IronCondor") {
            4
        } else {
            2
        };
        let can_place = self.accepted_orders.is_empty()
            && !self.submission_unknown
            && self.preparation.is_none()
            && self.market.editable
            && loaded
            && self.totals.is_some()
            && quantity == required
            && self.market.draft.legs.iter().all(|l| {
                l.quote.is_future
                    || l.quote.side != 0 && l.quote.bid.is_some() && l.quote.ask.is_some()
            });
        for (key, value) in [
            ("composerSessionId", self.session.clone()),
            ("expiry", self.market.expiry.to_string()),
            ("compositionType", self.market.strategy.clone()),
            ("brokerMode", self.execution.environment.clone()),
            (
                "composerStatus",
                if self.status.is_empty() {
                    self.market.status.clone()
                } else {
                    self.status.clone()
                },
            ),
            (
                "effectiveDate",
                self.trade.cell("RequestedTradeDate").text(),
            ),
            (
                "editable",
                flag(self.market.editable && loaded && self.preparation.is_none()),
            ),
            ("submitted", flag(!self.accepted_orders.is_empty())),
            ("submissionUnconfirmed", flag(self.submission_unknown)),
            ("submissionError", self.submission_error.clone()),
            ("live", flag(self.market.live)),
            ("quantity", self.quantity.to_string()),
            ("price", self.totals.cell("OrderPrice").text()),
            (
                "previewKey",
                self.market
                    .draft
                    .legs
                    .iter()
                    .map(|l| format!("{}{}", l.quote.contract, l.role))
                    .collect::<Vec<_>>()
                    .join("|"),
            ),
            (
                "callDelta",
                defaults
                    .map(|d| d.call_delta.as_str())
                    .unwrap_or("16")
                    .into(),
            ),
            (
                "putDelta",
                defaults
                    .map(|d| d.put_delta.as_str())
                    .unwrap_or("16")
                    .into(),
            ),
            (
                "callWidth",
                defaults
                    .map(|d| d.call_width.as_str())
                    .unwrap_or("50")
                    .into(),
            ),
            (
                "putWidth",
                defaults
                    .map(|d| d.put_width.as_str())
                    .unwrap_or("50")
                    .into(),
            ),
            (
                "underlyingReferencePrice",
                self.initialization
                    .as_ref()
                    .and_then(|d| d.price.as_ref())
                    .map(|p| p.cell("ClosePrice").text())
                    .unwrap_or_default(),
            ),
            (
                "underlying",
                self.market.underlying.cell("ContractId").text(),
            ),
            ("symbol", self.market.underlying.cell("Symbol").text()),
            (
                "multiplier",
                self.market.underlying.cell("Multiplier").text(),
            ),
            ("venue", self.market.underlying.cell("Exchange").text()),
            ("valueDate", context.value_date.to_string()),
            ("executionAction", self.execution_action.clone()),
            ("canPlaceOrder", flag(can_place)),
        ] {
            result.context.insert(key.into(), value);
        }
        Ok(())
    }
    pub(super) fn accept_submission(
        &mut self,
        orders: Vec<Json>,
        message: &str,
    ) -> Result<(), String> {
        self.accepted_orders = orders
            .into_iter()
            .map(serde_json::from_value)
            .collect::<Result<_, _>>()
            .map_err(|e| e.to_string())?;
        self.submission_unknown = false;
        self.submission_error.clear();
        self.preparation = None;
        self.confirmation_id = None;
        self.status = message.into();
        Ok(())
    }
    pub(super) fn mark_submission_unknown(
        &mut self,
        orders: Vec<Json>,
        message: &str,
    ) -> Result<(), String> {
        self.accepted_orders = orders
            .into_iter()
            .map(serde_json::from_value)
            .collect::<Result<_, _>>()
            .map_err(|e| e.to_string())?;
        self.submission_unknown = true;
        self.submission_error = message.into();
        self.preparation = None;
        self.confirmation_id = None;
        self.status = message.into();
        Ok(())
    }
    pub(super) fn reject_submission(&mut self, error: &str) {
        self.preparation = None;
        self.confirmation_id = None;
        self.submission_unknown = false;
        self.submission_error = error.into();
        self.status = error.into();
    }
    pub(super) async fn prepare_confirmation(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        if self.preparation.is_some() {
            return Err("A confirmation is already pending".into());
        }
        if !self.market.editable
            || self.execution_action
                != if self.trade.primary_trade == Some(false) {
                    "Close"
                } else {
                    "Open"
                }
        {
            return Err("This New Trade screen submits opening orders only".into());
        }
        // Parse and publish the same execution controls as a preview before freezing
        // the order. Human review itself never holds this async controller lock.
        let mut preview = intent.clone();
        preview["intent"] = "preview-draft".into();
        self.execute(backend, registry, context, &preview, deadline)
            .await?;
        if !self.market.draft.legs.iter().all(|leg| {
            leg.quote.is_future
                || leg.quote.bid.as_ref().is_some_and(|v| {
                    crate::services::option_selection::compare(
                        v,
                        &ExactDecimal::parse("0").unwrap(),
                    )
                    .is_gt()
                }) && leg
                    .quote
                    .ask
                    .as_ref()
                    .zip(leg.quote.bid.as_ref())
                    .is_some_and(|(ask, bid)| {
                        crate::services::option_selection::compare(ask, bid).is_ge()
                    })
        }) {
            return Err("Complete quotes are unavailable for the selected legs".into());
        }
        let legs = self
            .market
            .draft
            .legs
            .iter()
            .map(|leg| leg.quote.clone())
            .collect::<Vec<_>>();
        let quote_inputs = broker_market_display::quote_inputs(&self.market.draft.legs)?;
        let price = entered(field(intent, "price")).ok_or("Enter an explicit order price")?;
        let draft = crate::services::order_preparation::Draft {
            scope: self.scope,
            trade: self
                .trade
                .cell("TradeId")
                .as_i64()
                .and_then(|v| i32::try_from(v).ok())
                .ok_or("Invalid setup Trade identity")?,
            underlying: self.market.underlying.cell("ContractId").text(),
            strategy: if self.market.strategy == "FuturesOutright" {
                "FuturesOutright"
            } else if self.market.strategy.ends_with("IronCondor") {
                "IronCondor"
            } else {
                "VerticalSpread"
            }
            .into(),
            expiry: self.market.expiry,
            quantity: self.quantity,
            price,
            legs,
            quote_inputs,
            order_type: self.order_type.clone(),
            algorithm: self.algorithm.clone(),
            tif: self.time_in_force.clone(),
            pace: self.pace.clone(),
            composition: uuid::Uuid::new_v4(),
            workflow: uuid::Uuid::new_v4(),
            component: uuid::Uuid::new_v4(),
        };
        let prepared = draft
            .prepare_with_context(backend, registry, &self.execution, deadline)
            .await?;
        if prepared.result["Candidate"]["ValueDate"] != context.value_date.to_string() {
            return Err("Backend preparation crossed the API ValueDate".into());
        }
        self.confirmation_id = Some(uuid::Uuid::new_v4());
        self.preparation = Some(prepared);
        Ok("Awaiting order confirmation".into())
    }
    pub(super) fn confirmation_document(
        &self,
    ) -> Result<Option<crate::models::broker_presentation::Confirmation>, String> {
        use crate::models::broker_presentation::{
            Confirmation, ConfirmationLeg, ConfirmationOrder,
        };
        let (Some(prepared), Some(id)) = (&self.preparation, self.confirmation_id) else {
            return Ok(None);
        };
        // The sealed preparation is an immutable command receipt. Convert only its
        // declared display fields at this protocol boundary; it stays authoritative.
        let amount = |value: &Json| {
            serde_json::from_value(value.clone())
                .map_err(|e| format!("Invalid prepared display amount: {e}"))
        };
        let commissions = prepared.result["LegCommissions"]
            .as_array()
            .ok_or("Missing prepared commissions")?;
        let legs = self
            .market
            .draft
            .legs
            .iter()
            .zip(commissions)
            .map(|(leg, commission)| {
                Ok(ConfirmationLeg {
                    side: if leg.quote.side > 0 { "Buy" } else { "Sell" }.into(),
                    role: leg.role.clone(),
                    contract_id: leg.quote.contract.clone(),
                    leg_commission: amount(commission)?,
                })
            })
            .collect::<Result<Vec<_>, String>>()?;
        Ok(Some(Confirmation {
            id: id.to_string(),
            submitting: false,
            order: ConfirmationOrder {
                order_quantity: self.quantity,
                order_price: amount(
                    &prepared.result["Candidate"]["Components"][0]["SignedNetDebitLimit"],
                )?,
                order_amount: amount(&prepared.result["CashAmount"])?,
                commission: amount(&prepared.result["TotalCommission"])?,
                total_amount: amount(&prepared.result["TotalAmount"])?,
            },
            legs,
        }))
    }
    pub(super) fn append_evidence(
        &self,
        result: &mut ContractResult,
        pending: &BTreeMap<String, crate::services::broker_order_commands::Operation>,
    ) -> Result<(), String> {
        use crate::services::broker_order_ui as ui;
        for (index, order) in self.accepted_orders.iter().enumerate() {
            result
                .rows
                .push(typed_row("submitted-order", index, order)?);
        }
        for order in &self.evidence.orders {
            let key =
                broker_reads::typed_identity(order.id.as_ref().ok_or("Missing broker identity")?)?;
            result.rows.push(typed_row("broker-order", &key, order)?);
        }
        let mut fills = vec![];
        for execution in &self.evidence.executions {
            let scope = execution
                .trade_order_id
                .as_ref()
                .ok_or("Missing execution scope")?;
            let key = format!(
                "{}.{}.{}.{}",
                scope.cell("PortfolioId").text(),
                scope.cell("FundId").text(),
                scope.cell("OrderId").text(),
                execution
                    .execution_attempt_id
                    .as_deref()
                    .unwrap_or_default()
                    .replace('-', "")
            );
            result.rows.push(typed_row("execution", key, execution)?);
            if let Some(rows) = execution.fills.as_ref() {
                fills.extend(rows);
            }
        }
        for (index, fill) in fills.iter().enumerate() {
            result.rows.push(typed_row("fill", index, *fill)?);
        }
        for order in &self.evidence.orders {
            let key =
                broker_reads::typed_identity(order.id.as_ref().ok_or("Missing broker identity")?)?;
            let (_, execution) = self.broker_order(&key)?;
            result.rows.push(typed_row(
                "broker-caption",
                &key,
                &crate::models::broker_presentation::Caption {
                    text: ui::caption(order)?,
                },
            )?);
            let summary = ui::summary(order, execution.as_ref())?;
            result.rows.push(typed_row(
                "broker-properties",
                &key,
                &ui::properties(&summary, ui::SUMMARY_NAMES)?,
            )?);
            result
                .rows
                .push(typed_row("broker-summary", &key, &summary)?);
            result.rows.push(typed_row(
                "broker-mutation",
                &key,
                &ui::mutation(
                    order,
                    execution.as_ref(),
                    pending.values().any(|op| op.order_key == key),
                )?,
            )?);
        }
        let mut seen = std::collections::BTreeSet::new();
        for fill in fills {
            let key = fill.cell("ExecutionFillId").text();
            if seen.insert(key.clone()) {
                result.rows.push(typed_row(
                    "fill-properties",
                    key,
                    &ui::properties(fill, ui::FILL_NAMES)?,
                )?);
            }
        }
        Ok(())
    }
    pub async fn close(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        self.market.close(backend, registry, deadline).await
    }
    fn report_document(&self) -> QualificationReport {
        QualificationReport {
            path: self.report_path.clone(),
            hash: self.report_hash.clone(),
            preview: self.report_preview.clone(),
        }
    }
}
