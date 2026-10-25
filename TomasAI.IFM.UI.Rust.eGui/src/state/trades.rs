//! Native trade workspace owner. Rendering receives immutable display rows;
//! backend DTOs, selected scope and correlated operation receipts stay here.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
    services::{business_context::BusinessContext, trade_orders as api},
    state::trade_data::TradeData,
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
mod mutations;
mod composer;
mod account;
mod broker_mutations;
mod submissions;
mod eod;
#[derive(Default)]
pub struct TradeController {
    pub data: TradeData,
    pub monitors:crate::state::trade_monitors::Hub,
    eod:Option<crate::services::trade_eod::Session>,
    generation: u64,
    receipts: BTreeMap<String, (Json, EditorOutcome)>,
    message: String,
    order_draft: Option<Json>,
    prepared_trade: Option<i32>,
    pending_manual: Option<crate::services::manual_orders::Operation>,
    manual_request: Option<String>,
    confirmed_write: bool,
    reconciled: Vec<EditorOutcome>,
    composer: Option<composer::Composer>,
    submission_operations:BTreeMap<String,crate::services::order_submission::Operation>,
    submissions_loaded:bool,
    order_operations: BTreeMap<String,crate::services::broker_order_commands::Operation>,
    account_operations: BTreeMap<String,crate::services::broker_account_commands::Operation>,
}
fn field<'a>(intent: &'a Json, key: &str) -> &'a str {
    intent["fields"][key].as_str().unwrap_or_default()
}
fn text(value: &Json) -> String {
    value.as_str().map(str::to_string).unwrap_or_else(|| {
        if value.is_null() {
            String::new()
        } else {
            value.to_string()
        }
    })
}
fn model(registry: &Registry, name: &str, value: &Value) -> Result<Json, String> {
    registry.project(&api::kind(name), value)
}
fn selected(
    registry: &Registry,
    name: &str,
    rows: &[Value],
    key: &str,
    id: Option<i64>,
) -> Result<Option<Json>, String> {
    let Some(id) = id else { return Ok(None) };
    rows.iter()
        .map(|row| model(registry, name, row))
        .collect::<Result<Vec<_>, _>>()?
        .into_iter()
        .find(|row| row[key] == id)
        .map(Some)
        .ok_or("Selected trade workspace identity is no longer available".into())
}
fn row(kind: &str, id: impl std::fmt::Display, value: Json) -> Result<ContractRow, String> {
    Ok(ContractRow {
        id: format!("{kind}:{id}"),
        fields: BTreeMap::from([("kind".into(), kind.into())]),
        data: Some(crate::wire::encode_value(&value)?.into()),
    })
}
fn choice_index(intent: &Json, length: usize) -> Result<usize, String> {
    field(intent, "index")
        .parse::<usize>()
        .ok()
        .filter(|v| *v < length)
        .ok_or("Select a current workspace row".into())
}
fn closing(name: &str) -> &str {
    match name {
        "ShortIronCondor" => "LongIronCondor",
        "LongIronCondor" => "ShortIronCondor",
        "PutCreditSpread" => "PutDebitSpread",
        "PutDebitSpread" => "PutCreditSpread",
        "CallCreditSpread" => "CallDebitSpread",
        "CallDebitSpread" => "CallCreditSpread",
        "FuturesOutright" => "FuturesOutright",
        _ => "Unknown",
    }
}
impl TradeController {
    /// Release this UI's preview owner without disabling any saved trade feed.
    /// Shutdown callers supply a short operational budget; failed cleanup never
    /// discards a retained owner while the workspace remains open.
    pub async fn release_preview(&mut self,backend:&Backend,registry:&Registry,deadline:tokio::time::Instant)->Result<(),String>{
        if let Some(composer)=self.composer.as_mut(){composer.close(backend,registry,deadline).await?;}
        self.composer=None;Ok(())
    }
    async fn apply(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let action = intent["intent"].as_str().ok_or("Missing trade action")?;
        if action != "load" && intent["expectedGeneration"].as_u64() != Some(self.generation) {
            return Err("Trade workspace changed; review the current selection".into());
        }
        if matches!(action,"eod-open"|"eod-load"|"eod-run"){let result=self.eod_action(backend,registry,intent,deadline).await;if result.is_ok(){self.message.clear();}return result;}
        if matches!(action,"load-trade"|"activate-trade"|"close-trade"|"position"|"plans-page"|"live-on"|"live-off"){
            let context=BusinessContext::load(backend).await?;self.data.context=Some(context.clone());
            let result=if action=="load-trade"{
                if intent["originalId"].as_str()!=self.data.order.map(|id|id.to_string()).as_deref(){return Err("Order selection changed".into())}
                let trade=selected(registry,"FundOrderTradeProjectionReadModel",&self.data.trades,"TradeId",self.data.trade)?.ok_or("Select an established trade")?;
                if !["Open","Closed"].contains(&trade["TradeState"].as_str().unwrap_or_default()){return Err("Only an established Open or Closed trade can be loaded".into())}
                let number=|name:&str|trade[name].as_i64().and_then(|n|i32::try_from(n).ok()).filter(|n|*n>0).ok_or("The setup has no established execution identity");
                let scope=crate::services::trade_monitor_queries::Scope{portfolio:self.data.portfolio.and_then(|n|i32::try_from(n).ok()).ok_or("Missing Portfolio")?,fund:self.data.fund.and_then(|n|i32::try_from(n).ok()).ok_or("Missing Fund")?,order:number("ExecutionOrderId")?,trade:number("ExecutionTradeId")?};
                let trade_type=text(&trade["TradeType"]);let strategy=if trade_type.ends_with("IronCondor"){"IronCondor"}else if trade_type=="FuturesOutright"{"FuturesOutright"}else{"VerticalSpread"};
                let fund=selected(registry,"FundMandateReadModel",&self.data.funds,"FundId",self.data.fund)?.ok_or("Missing Fund")?;
                if let Some(composer)=self.composer.as_mut(){composer.close(backend,registry,deadline).await?;}self.composer=None;
                self.monitors.open(backend,std::sync::Arc::new(Registry::compiled()?),crate::state::trade_monitors::OpenRequest{scope,strategy:strategy.into(),trade_type,trade_action:text(&trade["TradeAction"]),fund_name:text(&fund["Name"])},context,deadline).await
            }else{self.monitors.execute(backend,registry,intent,&context,deadline).await};
            if result.is_ok(){self.message.clear();}return result;
        }
        if matches!(action,"load"|"portfolio"|"fund"|"order"|"trade"|"close") {
            if let Some(composer)=self.composer.as_mut(){composer.close(backend,registry,deadline).await?;}
            self.composer=None;
        }
        if matches!(action,"confirm-order"|"cancel-order-confirmation"|"compose"|"preview-draft"|"refresh-chain"|"clear-staged"|"chain"|"select-leg"|"remove-leg"|"account"|"qualification-report"|"qualification-submit"|"qualification-accept"|"qualification-revoke"|"qualification-hold"|"qualification-release"|"qualification-resync"|"submit-order"|"refresh-execution"|"update-limit"|"cancel-unfilled") {
            let result=self.composer_action(backend,registry,intent,deadline).await;
            if result.is_ok(){self.message.clear();}
            return result;
        }
        if matches!(
            action,
            "prepare-order"
                | "create-order"
                | "remove-order"
                | "prepare-trade"
                | "create-trade"
                | "remove-trade"
        ) {
            return self
                .manual_action(backend, registry, intent, deadline)
                .await;
        }
        let mut next = self.data.clone();
        if action != "load" {
            next.context = Some(BusinessContext::load(backend).await?);
        }
        match action {
            "load" => next.load(backend, registry, deadline).await?,
            "portfolio" => {
                let index = choice_index(intent, next.portfolios.len())?;
                let value =
                    model(registry, "PortfolioReadModel", &next.portfolios[index])?["PortfolioId"]
                        .as_i64()
                        .ok_or("Missing Portfolio identity")?;
                next.select_portfolio(backend, registry, value, deadline)
                    .await?;
            }
            "fund" => {
                let index = choice_index(intent, next.funds.len())?;
                let value = model(registry, "FundMandateReadModel", &next.funds[index])?["FundId"]
                    .as_i64()
                    .ok_or("Missing Fund identity")?;
                next.select_fund(backend, registry, value, deadline).await?;
            }
            "order" => {
                let order = intent["originalId"]
                    .as_str()
                    .and_then(|v| v.parse::<i64>().ok())
                    .filter(|v| *v > 0)
                    .ok_or("Select an order")?;
                next.select_order(backend, registry, order, deadline)
                    .await?;
            }
            "trade" => {
                let index = choice_index(intent, next.trades.len())?;
                next.trade = Some(
                    model(
                        registry,
                        "FundOrderTradeProjectionReadModel",
                        &next.trades[index],
                    )?["TradeId"]
                        .as_i64()
                        .ok_or("Missing Trade identity")?,
                );
            }
            "range" => {
                let from = crate::backend::date_input::parse(field(intent, "from"))?;
                let through = crate::backend::date_input::parse(field(intent, "to"))?;
                if through < from {
                    return Err("To date must be on or after From date".into());
                }
                next.from = Some(from);
                next.through = Some(through);
                next.load_orders(backend, registry, deadline).await?;
                if let Some(order) = next.order {
                    next.select_order(backend, registry, order, deadline)
                        .await?;
                }
            }
            "cancel-draft" => {
                self.order_draft = None;
                self.prepared_trade = None;
            }
            "close" => {}
            _ => return Err(format!("Trade action {action} is not yet implemented")),
        }
        self.data = next;
        if action == "load" {
            self.reconcile_manual(backend, registry, deadline).await?;
        }
        if matches!(action, "load" | "portfolio" | "fund" | "order" | "trade")
            && self.pending_manual.is_none()
        {
            self.order_draft = None;
            self.prepared_trade = None;
        }
        self.message.clear();
        Ok("Trade workspace loaded".into())
    }
    pub fn result(&self, registry: &Registry) -> Result<ContractResult, String> {
        let mut result = ContractResult {
            view: "trade-orders".into(),
            ..Default::default()
        };
        let data = &self.data;
        let mut choices = |key: &str,
                           name: &str,
                           rows: &[Value],
                           identity: &str,
                           label: &str|
         -> Result<(), String> {
            result.choices.insert(
                key.into(),
                rows.iter()
                    .map(|row| {
                        let row = model(registry, name, row)?;
                        Ok(format!("{} - {}", text(&row[identity]), text(&row[label])))
                    })
                    .collect::<Result<Vec<_>, String>>()?,
            );
            Ok(())
        };
        choices(
            "portfolio",
            "PortfolioReadModel",
            &data.portfolios,
            "PortfolioId",
            "Name",
        )?;
        choices(
            "fund",
            "FundMandateReadModel",
            &data.funds,
            "FundId",
            "Name",
        )?;
        result.choices.insert("baseContract".into(),data.contracts.iter().map(|wire|registry.project("TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",wire).and_then(|row|row["ContractId"].as_str().map(str::to_string).ok_or("Missing contract identity".into()))).collect::<Result<Vec<_>,_>>()?);
        result.choices.insert(
            "tradeType".into(),
            [
                "ShortIronCondor",
                "LongIronCondor",
                "PutCreditSpread",
                "PutDebitSpread",
                "CallCreditSpread",
                "CallDebitSpread",
                "FuturesOutright",
            ]
            .map(str::to_string)
            .to_vec(),
        );
        for wire in &data.orders {
            let order = model(registry, "FundOrderProjectionReadModel", wire)?;
            let at = chrono::DateTime::parse_from_rfc3339(
                order["CreatedOnUtc"]
                    .as_str()
                    .ok_or("Missing order timestamp")?,
            )
            .map_err(|_| "Invalid order timestamp")?
            .to_utc();
            let date = crate::backend::time_zone::local_date("America/New_York", at)?;
            result.rows.push(row("order",text(&order["OrderId"]),json!({"orderId":order["OrderId"],"status":order["Status"],"createdOnUtc":date.format("%Y-%b-%d").to_string(),"operatorReference":order["OperatorReference"],"source":if order["Origin"]=="ManualUi"{"Manual"}else{"Strategy Workflow"}}))?);
        }
        let mut primary = None;
        for (index, wire) in data.trades.iter().enumerate() {
            let trade = model(registry, "FundOrderTradeProjectionReadModel", wire)?;
            if trade["PrimaryTrade"] == true && primary.is_none() {
                primary = Some(trade.clone());
            }
            result.rows.push(row("trade",text(&trade["TradeId"]),json!({"index":index,"tradeId":trade["TradeId"],"tradeType":trade["TradeType"],"effectiveDate":trade["RequestedTradeDate"],"tradeDate":trade["TradeDate"],"maturityDate":trade["MaturityDate"],"tradeState":trade["TradeState"],"tradeAction":trade["TradeAction"],"baseContractId":trade["BaseContractId"]}))?);
        }
        if let Some(draft) = &self.order_draft {
            result.rows.push(row("order-draft", "0", draft.clone())?);
        }
        let order = selected(
            registry,
            "FundOrderProjectionReadModel",
            &data.orders,
            "OrderId",
            data.order,
        )?
        .unwrap_or(Json::Null);
        let trade = selected(
            registry,
            "FundOrderTradeProjectionReadModel",
            &data.trades,
            "TradeId",
            data.trade,
        )?
        .unwrap_or(Json::Null);
        let fund = selected(
            registry,
            "FundMandateReadModel",
            &data.funds,
            "FundId",
            data.fund,
        )?
        .unwrap_or(Json::Null);
        let index =
            |name: &str, rows: &[Value], key: &str, id: Option<i64>| -> Result<String, String> {
                let Some(id) = id else { return Ok("-1".into()) };
                Ok(rows
                    .iter()
                    .position(|wire| model(registry, name, wire).is_ok_and(|row| row[key] == id))
                    .map(|v| v.to_string())
                    .unwrap_or("-1".into()))
            };
        let mutable = order["Status"] == "Draft";
        let development =
            std::env::var("DOTNET_ENVIRONMENT").unwrap_or("Development".into()) == "Development";
        let can_enter = data.context.as_ref().is_some_and(|context| {
            context.market_state == 2 || (context.allow_development_emulator && development)
        });
        for (key, value) in [
            (
                "portfolioIndex",
                index(
                    "PortfolioReadModel",
                    &data.portfolios,
                    "PortfolioId",
                    data.portfolio,
                )?,
            ),
            (
                "fundIndex",
                index("FundMandateReadModel", &data.funds, "FundId", data.fund)?,
            ),
            (
                "orderId",
                data.order.map(|v| v.to_string()).unwrap_or_default(),
            ),
            (
                "tradeId",
                data.trade.map(|v| v.to_string()).unwrap_or_default(),
            ),
            ("tradeState", text(&trade["TradeState"])),
            (
                "valueDate",
                data.context
                    .as_ref()
                    .map(|v| v.value_date.to_string())
                    .unwrap_or_default(),
            ),
            (
                "asOfUtc",
                data.context
                    .as_ref()
                    .map(|v| utc_roundtrip(v.as_of_utc))
                    .unwrap_or_default(),
            ),
            ("from", data.from.map(|v| v.to_string()).unwrap_or_default()),
            (
                "to",
                data.through.map(|v| v.to_string()).unwrap_or_default(),
            ),
            (
                "fundId",
                data.fund.map(|v| v.to_string()).unwrap_or_default(),
            ),
            (
                "portfolioId",
                data.portfolio.map(|v| v.to_string()).unwrap_or_default(),
            ),
            (
                "preparedTradeId",
                self.prepared_trade
                    .map(|id| id.to_string())
                    .unwrap_or_default(),
            ),
            (
                "closingType",
                primary
                    .as_ref()
                    .map(|v| closing(v["TradeType"].as_str().unwrap_or_default()).to_string())
                    .unwrap_or_default(),
            ),
            (
                "closingContract",
                primary
                    .as_ref()
                    .map(|v| text(&v["BaseContractId"]))
                    .unwrap_or_default(),
            ),
            ("fundName", text(&fund["Name"])),
            ("compositionType", text(&trade["TradeType"])),
            ("tradeAction", text(&trade["TradeAction"])),
            ("status", self.message.clone()),
            ("canCreateOrder", "True".into()),
            ("canRemoveOrder", (!data.orders.is_empty()).to_string()),
            ("canAddTrade", (!order.is_null()).to_string()),
            ("canRemoveTrade", (!data.trades.is_empty()).to_string()),
            (
                "canLoadTrade",
                (!trade.is_null() && trade["TradeState"] != "NewTrade").to_string(),
            ),
            (
                "canSubmit",
                (mutable && trade["TradeState"] == "NewTrade" && can_enter).to_string(),
            ),
            ("canEndOfDay", (mutable && !trade.is_null()).to_string()),
        ] {
            result.context.insert(key.into(), value);
        }
        for key in [
            "canRemoveOrder",
            "canAddTrade",
            "canRemoveTrade",
            "canLoadTrade",
            "canSubmit",
            "canEndOfDay",
        ] {
            if let Some(value) = result.context.get_mut(key) {
                *value = if *value == "true" { "True" } else { "False" }.into();
            }
        }
        for (key, value) in [
            ("latestPlanRevision", "0"),
            ("planMaximumRevision", "0"),
            ("planOffset", "0"),
            ("planTotal", "1"),
            ("planLoaded", "False"),
            ("planDate", ""),
            ("chartError", ""),
            ("planError", ""),
            ("monitorStatus", ""),
            ("monitorLive", "False"),
            ("monitorCanLive", "False"),
            ("monitorCanSelectPosition", "False"),
            ("activeTradeTab", ""),
            ("canLive", "False"),
            ("live", "False"),
            ("canSelectPosition", "False"),
        ] {
            result.context.insert(key.into(), value.into());
        }
        if let Some(session)=&self.eod{result.rows.push(row("eod",session.scope.key()?,session.document())?);}
        crate::state::trade_monitors::Hub::merge(&mut result,&self.monitors.current().result);
        if let (Some(composer),Some(context))=(&self.composer,&data.context){composer.append(&mut result,context)?;
            composer.append_evidence(&mut result,&self.order_operations)?;
            if let Some(operation)=self.submission_operations.values().find(|operation|self.matches_submission(registry,operation)){
                result.context.insert("submissionUnconfirmed".into(),if operation.confirmed{"False"}else{"True"}.into());
                result.context.insert("submitted".into(),if operation.accepted.is_empty(){"False"}else{"True"}.into());
                result.context.insert("canPlaceOrder".into(),"False".into());result.context.insert("editable".into(),"False".into());
            }}
        Ok(result)
    }
    async fn composer_action(&mut self,backend:&Backend,registry:&Registry,intent:&Json,deadline:tokio::time::Instant)->Result<String,String>{
        self.reconcile_account_operations();
        self.reconcile_order_operations();
        let context=BusinessContext::load(backend).await?;
        if matches!(intent["intent"].as_str(),Some("confirm-order"|"cancel-order-confirmation")){
            let composer=self.composer.as_mut().ok_or("Open Trade Blotter first")?;
            let id=composer.confirmation_id.ok_or("No order confirmation is pending")?;
            if intent["originalId"].as_str()!=Some(id.to_string().as_str()){return Err("Order confirmation changed; review its current prepared values".into())}
            if intent["intent"]=="cancel-order-confirmation"{composer.preparation=None;composer.confirmation_id=None;return Ok("Submission cancelled".into())}
            return self.submit_confirmed(backend,registry,&context,intent,deadline).await;
        }
        let selected_order=self.data.order.map(|v|v.to_string()).unwrap_or_default();
        if intent["originalId"].as_str()!=Some(selected_order.as_str()){return Err("Order selection changed".into())}
        if intent["fields"].get("tradeId").is_some()&&field(intent,"tradeId")!=self.data.trade.map(|v|v.to_string()).unwrap_or_default(){return Err("Trade selection changed".into())}
        self.data.context=Some(context.clone());
        if matches!(intent["intent"].as_str(),Some("qualification-submit"|"qualification-accept"|"qualification-revoke"|"qualification-hold"|"qualification-release"|"qualification-resync")){
            return self.account_action(backend,registry,&context,intent,deadline).await;
        }
        if matches!(intent["intent"].as_str(),Some("update-limit"|"cancel-unfilled")){
            return self.broker_mutation(backend,registry,&context,intent,deadline).await;
        }
        self.load_submissions(registry).await?;
        if intent["intent"]=="refresh-execution"{self.reconcile_submissions(backend,registry,&context,deadline).await?;}
        if intent["intent"]=="submit-order"&&self.submission_operations.values().any(|operation|self.matches_submission(registry,operation)){
            return Err("This setup already has a retained submission; refresh execution evidence instead of submitting it again".into());
        }
        if intent["intent"]=="compose" {
            if let Some(composer)=self.composer.as_mut(){composer.close(backend,registry,deadline).await?;}
            self.composer=None;
            let mut trade=selected(registry,"FundOrderTradeProjectionReadModel",&self.data.trades,"TradeId",self.data.trade)?.ok_or("Select a trade")?;
            for(key,kind,fallback)in[("TradeAction","TradeAction","Buy"),("TradeState","TradeState","NewTrade"),("TradeType","TradeType","Unknown")]{
                let names=registry.enum_names(&format!("TomasAI.IFM.Domain.Trade.Shared.{kind}"))?;
                let input=text(&trade[key]);
                trade[key]=names.into_iter().find(|name|name.eq_ignore_ascii_case(&input)).unwrap_or_else(||if input.parse::<i32>().is_ok(){input}else{fallback.into()}).into();
            }
            let mut contract=text(&trade["BaseContractId"]);
            let definitions=self.data.contracts.iter().map(|row|registry.project("TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",row)).collect::<Result<Vec<_>,_>>()?;
            if contract.trim().is_empty(){contract=definitions.iter().find(|c|c["Symbol"].as_str().is_some_and(|s|s.eq_ignore_ascii_case(trade["BaseContractSymbol"].as_str().unwrap_or_default()))).map(|c|text(&c["ContractId"])).unwrap_or_default();}
            let matching=definitions.into_iter().filter(|c|c["ContractId"]==contract).collect::<Vec<_>>();
            if matching.len()!=1{return Err("Selected underlying contract is unavailable or ambiguous".into())}
            let order=selected(registry,"FundOrderProjectionReadModel",&self.data.orders,"OrderId",self.data.order)?.ok_or("Select an order")?;
            let integer=|id:Option<i64>|id.and_then(|v|i32::try_from(v).ok()).filter(|v|*v>0).ok_or("Invalid composition scope");
            let scope=crate::services::broker_reads::Scope{portfolio:integer(self.data.portfolio)?,fund:integer(self.data.fund)?,order:integer(self.data.order)?};
            let development=std::env::var("DOTNET_ENVIRONMENT").unwrap_or("Development".into())=="Development";
            let allowed=context.market_state==2||development&&context.allow_development_emulator;
            let editable=order["Status"]=="Draft"&&trade["TradeState"]=="NewTrade"&&allowed;
            // Install the owner before I/O: cancellation cannot discard the only
            // subscription identity capable of cleaning up a partial open.
            self.composer=Some(composer::Composer::new(scope,trade,matching.into_iter().next().unwrap(),&context,editable,development)?);
            if let Some(operation)=self.submission_operations.values().find(|operation|self.matches_submission(registry,operation)){
                let composer=self.composer.as_mut().unwrap();
                if operation.confirmed{composer.accept_submission(operation.accepted.clone(),"Previously submitted order restored");}
                else{composer.mark_submission_unknown(operation.accepted.clone(),"Submission outcome unconfirmed; refresh execution evidence");}
            }
            self.composer.as_mut().unwrap().initialize(backend,registry,&context,deadline).await?;
            return Ok("Blotter loaded".into());
        }
        let composer=self.composer.as_mut().ok_or("Open Trade Blotter first")?;
        if let Some(session)=intent["fields"]["composerSessionId"].as_str(){if session!=composer.session{return Err("Blotter incarnation changed; reload its current draft".into())}}
        if intent["intent"]=="submit-order"{return composer.prepare_confirmation(backend,registry,&context,intent,deadline).await;}
        composer.execute(backend,registry,&context,intent,deadline).await
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<ContractPublication, String> {
        if intent["schema"] != 1 || intent["view"] != "trade-orders" {
            return Err("Invalid trade workspace intent".into());
        }
        let request = uuid::Uuid::parse_str(intent["requestId"].as_str().unwrap_or_default())
            .ok()
            .filter(|id| !id.is_nil())
            .ok_or("Invalid trade request identity")?
            .to_string();
        let fields = intent["fields"]
            .as_object()
            .filter(|fields| fields.len() <= 64)
            .ok_or("Invalid trade fields")?;
        if fields.iter().any(|(key, value)| {
            key.len() > 64
                || value
                    .as_str()
                    .is_none_or(|v| v.encode_utf16().count() > 2048)
        }) || intent["originalId"].as_str().is_some_and(|v| v.len() > 512)
        {
            return Err("Trade input exceeds its bound".into());
        }
        self.reconcile_account_operations();
        self.reconcile_order_operations();
        let outcome = if let Some((original, outcome)) = self.receipts.get(&request) {
            if original != intent {
                return Err("RequestId belongs to another trade action".into());
            }
            outcome.clone()
        } else {
            if self.receipts.len() >= 256 {
                let key = self
                    .receipts
                    .iter()
                    .find(|(_, (_, outcome))| outcome.state != "Unknown")
                    .map(|(key, _)| key.clone())
                    .ok_or("Reconcile unknown trade operations before continuing")?;
                self.receipts.remove(&key);
            }
            self.confirmed_write = false;
            let deadline = tokio::time::Instant::now() + backend.default_timeout();
            let result =
                tokio::time::timeout_at(deadline, self.apply(backend, registry, intent, deadline))
                    .await
                    .map_err(|_| if self.pending_manual.as_ref().is_some_and(|operation|operation.attempted){"OutcomeUnknown: manual order deadline expired; reload to check the retained operation".into()}else if self.account_operations.get(&request).is_some_and(|operation|operation.attempted&&!operation.acknowledged){"OutcomeUnknown: broker account deadline expired; refresh Account to check its retained operation".into()}else if self.order_operations.get(&request).is_some_and(|operation|operation.attempted&&!operation.acknowledged){"OutcomeUnknown: broker order deadline expired; refresh its original evidence".into()}else if self.submission_operations.get(&request).is_some_and(|operation|operation.attempted&&!operation.confirmed){"OutcomeUnknown: order submission deadline expired; reconcile its retained identity".into()}else{"Trade workspace deadline expired".to_string()})
                    .and_then(|v| v);
            let result = if result.is_err() && self.confirmed_write {
                Ok("Saved; reload the trade workspace to refresh its projections".into())
            } else {
                result
            };
            let outcome = match result {
                Ok(message) => EditorOutcome {
                    request_id: request.clone(),
                    state: "Completed".into(),
                    message,
                },
                Err(message) => {
                    self.message = message.clone();
                    EditorOutcome {
                        request_id: request.clone(),
                        state: if message.starts_with("OutcomeUnknown:")
                            && (self.eod.as_ref().is_some_and(|v|v.pending)||self.manual_request.as_deref() == Some(request.as_str()) || self.account_operations.get(&request).is_some_and(|operation|operation.attempted&&!operation.acknowledged) || self.order_operations.get(&request).is_some_and(|operation|operation.attempted&&!operation.acknowledged) || self.submission_operations.get(&request).is_some_and(|operation|operation.attempted&&!operation.confirmed))
                        {
                            "Unknown"
                        } else {
                            "Failed"
                        }
                        .into(),
                        message,
                    }
                }
            };
            self.receipts
                .insert(request.clone(), (intent.clone(), outcome.clone()));
            outcome
        };
        self.generation += 1;
        let mut outcomes = vec![outcome];
        outcomes.append(&mut self.reconciled);
        Ok(ContractPublication {
            schema: 1,
            generation: self.generation,
            // Successful hybrid workspace publications use the standard hidden
            // Completed label; diagnostics stay visible for failed/unknown work.
            status: if outcomes[0].state=="Completed" { "Completed".into() } else { outcomes[0].message.clone() },
            result: self.result(registry)?,
            outcomes,
            ..Default::default()
        })
    }
}
