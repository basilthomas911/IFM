//! EOD is an explicit operator action against one established position. All
//! money is returned by backend queries. A lost command reply is reconciled by
//! querying the same position, never by allocating another lifecycle command.
use super::{
    business_context::BusinessContext, financial, parameters, trade_monitor_queries::Scope,
};
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
const ROOT: &str = "TomasAI.IFM.Domain.Trade.Shared";
pub struct Session {
    pub scope: Scope,
    pub strategy: String,
    pub contract: String,
    pub date: chrono::NaiveDate,
    pub reference: String,
    pub display: Json,
    pub pending: bool,
    pub completed: bool,
}
impl Session {
    pub fn new(
        scope: Scope,
        strategy: String,
        contract: String,
        date: chrono::NaiveDate,
    ) -> Result<Self, String> {
        scope.key()?;
        if contract.trim().is_empty() {
            return Err("Missing EOD underlying".into());
        }
        Ok(Self {
            scope,
            strategy,
            contract,
            date,
            reference: String::new(),
            display: Json::Null,
            pending: false,
            completed: false,
        })
    }
    pub fn document(&self) -> Json {
        let mut value = self.display.clone();
        if !value.is_object() {
            value = json!({});
        }
        value["portfolioId"] = self.scope.portfolio.into();
        value["fundId"] = self.scope.fund.into();
        value["orderId"] = self.scope.order.into();
        value["tradeId"] = self.scope.trade.into();
        value["valueDate"] = self.date.to_string().into();
        value["reference"] = self.reference.clone().into();
        value["canRun"] = (!self.display.is_null() && !self.pending && !self.completed).into();
        value
    }
    pub async fn load(
        &mut self,
        b: &Backend,
        r: &Registry,
        context: &BusinessContext,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let position =
            super::order_close::current(b, r, self.scope, &self.strategy, deadline).await?;
        if self.pending {
            if position["Phase"] == "EndOfDay"
                && position["LatestSealedValueDate"] == self.date.to_string()
            {
                self.pending = false;
                self.completed = true;
            } else {
                return Err("OutcomeUnknown: EOD command is unconfirmed; reload its original position evidence".into());
            }
        }
        let key = format!("{}.{}", self.contract, self.date);
        let subject = ActorSubject::query("FuturesEodDataQuery", "GetFuturesEodData", &key);
        let body = r.encode(
            "TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetFuturesEodDataQuery",
            &BTreeMap::from([
                ("Subject".into(), subject.wire()),
                (
                    "EntityId".into(),
                    Value::Array(vec![0.into(), Value::Array(vec![key.into()])]),
                ),
                ("ContractId".into(), self.contract.clone().into()),
                ("ValueDate".into(), date_only_wire(self.date)),
            ]),
        )?;
        let reply = b.request_until(&subject, &body, deadline, None).await?;
        let market = parameters::document(
            r,
            "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesEodDataV2ReadModel",
            service_result(&reply)?,
            true,
        )?;
        if market["ContractId"] != self.contract || market["ValueDate"] != self.date.to_string() {
            return Err("EOD market data crossed its requested contract/date".into());
        }
        let balance = financial::query(
            b,
            r,
            context,
            "GetAccountBalancesQuery",
            self.scope.portfolio,
            Some(self.scope.fund),
            Value::Array(vec![]),
            financial::AccessScope::MetricsReader,
            deadline,
        )
        .await?
        .value
        .ok_or("Fund balance was not found")?;
        let balance = parameters::document(
            r,
            "TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialBalanceSnapshot",
            &balance,
            true,
        )?;
        let subject = ActorSubject::query("TradeOrderQuery", "GetTradeEndOfDayDisplay", "none");
        let body = r.encode(
            &format!("{ROOT}.Order.GetTradeEndOfDayDisplayQuery"),
            &BTreeMap::from([
                ("Subject".into(), subject.wire()),
                (
                    "EntityId".into(),
                    Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
                ),
                (
                    "Request".into(),
                    parameters::from_document(
                        r,
                        &format!("{ROOT}.Order.TradeEndOfDayDisplayRequest"),
                        &json!({"Position":position,"AvailableCash":balance["AvailableCash"]}),
                    )?,
                ),
            ]),
        )?;
        let reply = b.request_until(&subject, &body, deadline, None).await?;
        let amounts = parameters::document(
            r,
            &format!("{ROOT}.Order.TradeEndOfDayDisplay"),
            service_result(&reply)?,
            true,
        )?;
        if amounts["PositionId"] != position["Id"]
            || amounts["PositionSequence"] != position["PositionSequence"]
        {
            return Err("EOD display returned different position evidence".into());
        }
        self.completed = position["Phase"] == "EndOfDay";
        self.display = json!({"openPrice":market["OpenPrice"],"highPrice":market["HighPrice"],"lowPrice":market["LowPrice"],"closePrice":market["ClosePrice"],"volume":market["Volume"],"tradePnl":amounts["TradePnl"],"fundBalance":amounts["FundBalance"],"lastStatusMessage":format!("End-of-day inputs loaded for {} on {}.",self.scope.position(&self.strategy)?.0,self.date)});
        Ok(())
    }
    pub async fn run(
        &mut self,
        b: &Backend,
        r: &Registry,
        context: &BusinessContext,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if self.display.is_null() || self.completed || self.pending {
            return Err("Load unprocessed EOD inputs before running".into());
        }
        let (key, id) = self.scope.position(&self.strategy)?;
        let (actor, verb, namespace) = match self.strategy.as_str() {
            "IronCondor" => (
                "FuturesIronCondorTradePositionCommand",
                "EndOfDayIronCondorPosition",
                "Futures.Option.Position",
            ),
            "VerticalSpread" => (
                "FuturesVerticalSpreadTradePositionCommand",
                "EndOfDayVerticalSpreadPosition",
                "Futures.Option.Position",
            ),
            "FuturesOutright" => (
                "FuturesTradePositionCommand",
                "EndOfDayFuturesPosition",
                "Futures.Position",
            ),
            _ => return Err("Unsupported EOD strategy".into()),
        };
        // Match the managed client's deterministic business identity exactly.
        let command =
            super::order_submission::stable(&[&format!("PositionEndOfDay:{key}:{}", self.date)]);
        let effective = self
            .date
            .and_hms_nano_opt(23, 59, 59, 999999900)
            .ok_or("Invalid EOD boundary")?
            .and_utc();
        let subject = ActorSubject {
            kind: ActorKind::Command,
            name: actor.into(),
            verb: verb.into(),
            entity: key,
        };
        let body = r.encode(
            &format!("{ROOT}.{namespace}.{verb}Command"),
            &BTreeMap::from([
                ("Subject".into(), subject.wire()),
                ("CommandId".into(), command.to_string().into()),
                ("PostEvents".into(), true.into()),
                (
                    "EntityId".into(),
                    parameters::from_document(r, &format!("{ROOT}.StrategyPositionId"), &id)?,
                ),
                ("ValueDate".into(), date_only_wire(self.date)),
                ("EffectiveAtUtc".into(), timestamp_wire(effective)),
            ]),
        )?;
        self.pending = true;
        let submitted = super::portfolio_commands::submit(
            b,
            &super::portfolio_commands::Prepared {
                command_id: command,
                subject,
                body,
            },
            deadline,
        )
        .await;
        if let Err(error) = submitted {
            if !error.starts_with("OutcomeUnknown:") {
                self.pending = false;
            }
            return Err(error);
        }
        self.load(b, r, context, deadline).await?;
        if !self.completed {
            return Err(
                "OutcomeUnknown: EOD acknowledgement did not confirm a sealed position".into(),
            );
        }
        self.display["lastStatusMessage"] = "End-of-day processing completed.".into();
        Ok(())
    }
}
