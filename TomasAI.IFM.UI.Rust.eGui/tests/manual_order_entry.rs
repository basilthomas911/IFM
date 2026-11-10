use chrono::NaiveDate;
use ifm_ui_egui::{backend::schema::Registry, services::{business_context::BusinessContext, manual_orders, parameters}};
use serde_json::json;

/// Manual Add Trade preserves a NewTrade setup and reverses futures actions for a close.
#[test]
fn manual_add_trade_payloads_cover_all_three_families_and_both_futures_directions() {
    let registry = Registry::compiled().unwrap();
    let date = NaiveDate::from_ymd_opt(2026, 11, 10).unwrap();
    let context = BusinessContext { value_date: date, active_value_date: Some(date),
        as_of_utc: "2026-11-10T14:00:00Z".parse().unwrap(), revision: 1, market_state: 2,
        allow_development_emulator: true };
    let order = json!({"PortfolioId":1,"FundId":2,"OrderId":3,"AggregateVersion":4});
    let contract = json!({"ContractId":"ESZ6","Symbol":"ES"});
    for (kind, primary, opening, expected) in [
        ("ShortIronCondor", true, None, "Sell"),
        ("LongIronCondor", false, Some("Sell"), "Buy"),
        ("CallDebitSpread", true, None, "Buy"),
        ("CallCreditSpread", false, Some("Buy"), "Sell"),
        ("FuturesOutright", true, None, "Buy"),
        ("FuturesOutright", false, Some("Buy"), "Sell"),
        ("FuturesOutright", false, Some("Sell"), "Buy"),
    ] {
        let maturity = (kind != "FuturesOutright").then_some(date.succ_opt().unwrap());
        let operation = manual_orders::add(&registry, &context, &order, 10, primary, kind, opening,
            &contract, date, maturity, "Manual lifecycle", uuid::Uuid::new_v4()).unwrap();
        let command = parameters::document(&registry,
            "TomasAI.IFM.Domain.Portfolio.Shared.Commands.AddManualFundOrderTradeCommand",
            &operation.prepared.body, true).unwrap();
        let request = &command["Request"];
        assert_eq!(request["TradeAction"], expected);
        assert_eq!(request["PrimaryTrade"], primary);
        assert_eq!(request["TradeState"], "NewTrade");
        assert_eq!(request["TradeType"], kind);
        assert_eq!(request["BaseContractId"], "ESZ6");
        assert_eq!(request["ExpectedOrderVersion"], 4);
        assert!(request["TradeDate"].is_null());
    }
    assert!(manual_orders::add(&registry, &context, &order, 10, false, "FuturesOutright", None,
        &contract, date, None, "Close", uuid::Uuid::new_v4()).is_err());
}
