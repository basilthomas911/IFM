//! Backend-provided end-of-day inputs and their concrete UI publication.
use super::{
    cells::{Cell, Cells, ScalarCell},
    wire_types::DecimalText,
};
use serde::{Deserialize, Serialize};

/// Eod Inputs used by the eod component.
#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct EodInputs {
    /// Optional open price; `None` represents an absent value.
    pub open_price: Option<DecimalText>,
    /// Optional high price; `None` represents an absent value.
    pub high_price: Option<DecimalText>,
    /// Optional low price; `None` represents an absent value.
    pub low_price: Option<DecimalText>,
    /// Optional close price; `None` represents an absent value.
    pub close_price: Option<DecimalText>,
    /// Optional volume; `None` represents an absent value.
    pub volume: Option<i64>,
    /// Optional trade pnl; `None` represents an absent value.
    pub trade_pnl: Option<DecimalText>,
    /// Optional fund balance; `None` represents an absent value.
    pub fund_balance: Option<DecimalText>,
    /// Last status message associated with this eod inputs.
    pub last_status_message: String,
}
impl Cells for EodInputs {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Eod Document used by the eod component.
#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq)]
#[serde(default, rename_all = "camelCase")]
pub struct EodDocument {
    /// Optional portfolio id; `None` represents an absent value.
    pub portfolio_id: Option<i32>,
    /// Optional fund id; `None` represents an absent value.
    pub fund_id: Option<i32>,
    /// Optional order id; `None` represents an absent value.
    pub order_id: Option<i32>,
    /// Optional trade id; `None` represents an absent value.
    pub trade_id: Option<i32>,
    /// Value date associated with this eod document.
    pub value_date: String,
    /// Reference associated with this eod document.
    pub reference: String,
    /// Whether can run is enabled or currently true.
    pub can_run: bool,
    /// Optional inputs; `None` represents an absent value.
    #[serde(flatten)]
    pub inputs: Option<EodInputs>,
}
impl Cells for EodDocument {
    /// Resolves a named display cell; unsupported columns remain missing.
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
