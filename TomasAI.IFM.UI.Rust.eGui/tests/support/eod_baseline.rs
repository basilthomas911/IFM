//! Frozen original EOD document constructor for before/after timing.
use ifm_ui_egui::services::trade_monitor_queries::Scope;
use serde_json::{Value as Json, json};

/// Session used by the eod baseline test fixtures.
pub struct Session {
    /// Identity and routing scope against which requests and incoming results are checked.
    pub scope: Scope,
    /// Date associated with this session.
    pub date: chrono::NaiveDate,
    /// Reference associated with this session.
    pub reference: String,
    /// Display associated with this session.
    pub display: Json,
    /// Whether pending is enabled or currently true.
    pub pending: bool,
    /// Whether completed is enabled or currently true.
    pub completed: bool,
}
impl Session {
    /// Provides document for the session implementation.
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
