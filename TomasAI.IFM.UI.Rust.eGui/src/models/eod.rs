//! Backend-provided end-of-day inputs and their concrete UI publication.
use super::{
    cells::{Cell, Cells, ScalarCell},
    wire_types::DecimalText,
};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct EodInputs {
    pub open_price: Option<DecimalText>,
    pub high_price: Option<DecimalText>,
    pub low_price: Option<DecimalText>,
    pub close_price: Option<DecimalText>,
    pub volume: Option<i64>,
    pub trade_pnl: Option<DecimalText>,
    pub fund_balance: Option<DecimalText>,
    pub last_status_message: String,
}
impl Cells for EodInputs {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "openPrice" => self.open_price.scalar_cell(),
            "highPrice" => self.high_price.scalar_cell(),
            "lowPrice" => self.low_price.scalar_cell(),
            "closePrice" => self.close_price.scalar_cell(),
            "volume" => self.volume.scalar_cell(),
            "tradePnl" => self.trade_pnl.scalar_cell(),
            "fundBalance" => self.fund_balance.scalar_cell(),
            "lastStatusMessage" => self.last_status_message.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct EodDocument {
    pub portfolio_id: Option<i32>,
    pub fund_id: Option<i32>,
    pub order_id: Option<i32>,
    pub trade_id: Option<i32>,
    pub value_date: String,
    pub reference: String,
    pub can_run: bool,
    #[serde(flatten)]
    pub inputs: Option<EodInputs>,
}
impl Cells for EodDocument {
    fn cell(&self, key: &str) -> Cell<'_> {
        match key {
            "portfolioId" => self.portfolio_id.scalar_cell(),
            "fundId" => self.fund_id.scalar_cell(),
            "orderId" => self.order_id.scalar_cell(),
            "tradeId" => self.trade_id.scalar_cell(),
            "valueDate" => self.value_date.scalar_cell(),
            "reference" => self.reference.scalar_cell(),
            "canRun" => self.can_run.scalar_cell(),
            _ => self.inputs.cell(key),
        }
    }
}
