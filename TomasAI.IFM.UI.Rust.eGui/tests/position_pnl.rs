//! Currency totals are supplied by the backend, independently of daily P&L.
use ifm_ui_egui::{models::{trade_contracts::*, cells::Cells}, state::monitor_cache::History};
use serde_json::json;

/// Closed zero-quantity positions retain final currency totals rather than raw price points.
#[test]
fn closed_history_displays_backend_currency_totals_without_recalculating_daily_pnl() {
    let id = json!({"PortfolioId":101,"FundId":701,"OrderId":1901,"TradeId":1501});
    let trade: EstablishedTradeDefinition = serde_json::from_value(json!({"Id":id,
        "Legs":[{"Expiry":"2026-11-20"}],"OpeningHistoryValuation":{
            "ValueDate":"2026-10-16","Commission":"2.60"}})).unwrap();
    let position: StrategyPositionSnapshot = serde_json::from_value(json!({
        "Id":{"Trade":id,"PositionId":uuid::Uuid::new_v4().to_string()},"Phase":"Close",
        "ValueDate":"2026-11-06","PositionSequence":10,"RouteGeneration":1,
        "AsOfUtc":"2026-11-07T15:00:00Z","DailyPnl":"-99.00","RealizedPnl":"-6.95",
        "HistoryValuation":{"ValueDate":"2026-11-06","Quantity":0,"Commission":"2.60",
            "RealizedPnl":"-347.50","TotalCommission":"5.20","NetPnl":"-352.70","PeriodPnl":"-97.60"}})).unwrap();
    let mut history=History::new(trade,"ShortIronCondor".into());
    assert!(history.remember(position));
    let rows=history.rows();
    let closed=rows.last().unwrap();
    assert_eq!(closed.cell("tradePnl").text(),"-97.60");
    assert_eq!(closed.cell("realizedPnl").text(),"-347.50");
    assert_eq!(closed.cell("totalCommission").text(),"5.20");
    assert_eq!(closed.cell("netPnl").text(),"-352.70");
    assert!(rows[0].realized_pnl.is_none(),"Legacy fields stay unavailable until backend enrichment");
}
