//! EOD dialog ownership is independent of the live monitor. Selecting another
//! setup cannot silently replace an unconfirmed lifecycle operation.
use super::*;
impl TradeController {
    pub(super) async fn eod_action(
        &mut self,
        b: &Backend,
        r: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let context = BusinessContext::load(b).await?;
        self.data.context = Some(context.clone());
        let action = intent["intent"].as_str().ok_or("Missing EOD action")?;
        if action == "eod-open" {
            if self.eod.as_ref().is_some_and(|v| v.pending) {
                return Err(
                    "Reconcile the original EOD position before opening another EOD dialog".into(),
                );
            }
            let trade = selected(
                r,
                "FundOrderTradeProjectionReadModel",
                &self.data.trades,
                "TradeId",
                self.data.trade,
            )?
            .ok_or("Select an established trade")?;
            let number = |key: &str| {
                trade[key]
                    .as_i64()
                    .and_then(|v| i32::try_from(v).ok())
                    .filter(|v| *v > 0)
                    .ok_or("EOD requires an established trade")
            };
            let scope = crate::services::trade_monitor_queries::Scope {
                portfolio: self
                    .data
                    .portfolio
                    .and_then(|v| i32::try_from(v).ok())
                    .ok_or("Missing Portfolio")?,
                fund: self
                    .data
                    .fund
                    .and_then(|v| i32::try_from(v).ok())
                    .ok_or("Missing Fund")?,
                order: number("ExecutionOrderId")?,
                trade: number("ExecutionTradeId")?,
            };
            let kind = text(&trade["TradeType"]);
            let strategy = if kind.ends_with("IronCondor") {
                "IronCondor"
            } else if kind == "FuturesOutright" {
                "FuturesOutright"
            } else {
                "VerticalSpread"
            };
            self.eod = Some(crate::services::trade_eod::Session::new(
                scope,
                strategy.into(),
                text(&trade["BaseContractId"]),
                context.value_date,
            )?);
        }
        let session = self.eod.as_mut().ok_or("Open End of Day first")?;
        if action == "eod-run" {
            session.run(b, r, &context, deadline).await?;
            return Ok("End-of-day processing completed".into());
        }
        if action == "eod-load" {
            let date = crate::backend::date_input::parse(field(intent, "eodDate"))?;
            if session.pending && date != session.date {
                return Err(
                    "An unconfirmed EOD command must retain its original value date".into(),
                );
            }
            if date != session.date {
                session.date = date;
                session.display = None;
                session.completed = false;
            }
            session.reference = field(intent, "reference").trim().into();
        }
        session.load(b, r, &context, deadline).await?;
        Ok("End-of-day inputs loaded".into())
    }
}
