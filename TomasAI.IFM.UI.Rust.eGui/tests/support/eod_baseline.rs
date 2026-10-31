//! Frozen original EOD document constructor for before/after timing.
use ifm_ui_egui::services::trade_monitor_queries::Scope;
use serde_json::{Value as Json,json};
pub struct Session{pub scope:Scope,pub date:chrono::NaiveDate,pub reference:String,pub display:Json,pub pending:bool,pub completed:bool}
impl Session{
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
}
