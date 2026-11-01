use ifm_ui_egui::state::monitor_cache::History;
use serde_json::json;


/// Provides typed for the history currency implementation.
fn typed<T: serde::de::DeserializeOwned>(value: serde_json::Value) -> T {
    serde_json::from_value(value).unwrap()
}


/// Provides history for the history currency implementation.
fn history() -> History {
    History::new(
        typed(json!({
            "Id":{"OrderId":1901,"TradeId":1501},
            "Legs":[{"Expiry":"2026-11-20"}],
            "OpeningHistoryValuation":{"ValueDate":"2026-10-16","NetPrice":"-11.45"},
            "OpeningPositionSnapshot":{"DailyPnl":"-2.60"}
        })),
        "ShortIronCondor".into(),
    )
}


/// Verifies that every history row uses its own persisted currency without loading a plan.
#[test]
fn every_history_row_uses_its_own_persisted_currency_without_loading_a_plan() {
    let mut history = history();
    for (date, spread, points, currency) in [
        ("2026-10-16", "-10.85", "0.60", "27.40"),
        ("2026-10-19", "-10.40", "0.45", "22.50"),
    ] {
        assert!(history.remember(typed(json!({
            "Id":{"Trade":{"OrderId":1901,"TradeId":1501}}, "Phase":"EndOfDay",
            "HistoryValuation":{"ValueDate":date,"NetPrice":spread},
            "DailyPnlPoints":points,"DailyPnl":currency
        }))));
    }
    let rows = serde_json::to_value(history.rows()).unwrap();
    assert_eq!(rows[0]["tradePnl"], "-2.60");
    assert_eq!(rows[1]["tradePnl"], "27.40");
    assert_eq!(rows[1]["netSpread"], "-10.85");
    assert_eq!(rows[2]["tradePnl"], "22.50");
    assert_eq!(rows[2]["netSpread"], "-10.40");
}


/// Verifies that missing currency is blank instead of substituting points or spread.
#[test]
fn missing_currency_is_blank_instead_of_substituting_points_or_spread() {
    let mut history = history();
    assert!(history.remember(typed(json!({
        "Id":{"Trade":{"OrderId":1901,"TradeId":1501}}, "Phase":"EndOfDay",
        "HistoryValuation":{"ValueDate":"2026-10-16","NetPrice":"-10.85"},
        "DailyPnlPoints":"0.60"
    }))));
    assert!(history.rows()[1].trade_pnl.is_none());
}
