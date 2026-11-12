use chrono::NaiveDate;
use ifm_ui_egui::{backend::schema::Registry, services::{business_context::BusinessContext, manual_orders, parameters}};
use serde_json::json;

/// The editor can restore orders saved before setup references carried a draft revision.
#[test]
fn retained_setup_without_draft_revision_keeps_its_original_identity() {
    let registry = Registry::compiled().unwrap();
    let legacy = rmpv::Value::Array(vec![4001.into(), 4301.into(), 703.into()]);
    let kind = "TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundTradeSetupReference";
    let document = parameters::document(&registry, kind, &legacy, true).unwrap();
    assert_eq!(document, json!({"OrderId":4001,"TradeId":4301,"FundId":703,"DraftRevision":0}));
    let semantic = |wire: &rmpv::Value|
        ifm_ui_egui::services::inspection_display::semantic_document(&registry, kind, wire).unwrap();
    assert_eq!(semantic(&legacy), json!({"OrderId":4001,"TradeId":4301,"FundId":703}));
    let current = |version| rmpv::Value::Array(vec![4001.into(), 4301.into(), 703.into(), rmpv::Value::from(version)]);
    assert_eq!(semantic(&current(0)), semantic(&legacy));
    assert_eq!(semantic(&current(5))["DraftRevision"], 5);
    let truncated = rmpv::Value::Array(vec![4001.into(), 4301.into()]);
    assert!(parameters::document(&registry, kind, &truncated, true).is_err());
}

/// An edit uses its own compiled command initializer and carries both captured revisions.
#[test]
fn manual_change_trade_payload_preserves_identity_and_expected_revisions() {
    let registry = Registry::compiled().unwrap();
    let date = NaiveDate::from_ymd_opt(2026, 11, 10).unwrap();
    let context = BusinessContext { value_date: date, active_value_date: Some(date),
        as_of_utc: "2026-11-10T14:00:00Z".parse().unwrap(), revision: 1, market_state: 2,
        allow_development_emulator: true };
    let order = json!({"PortfolioId":101,"FundId":703,"OrderId":4001,"AggregateVersion":5});
    let trade = json!({"TradeId":4301,"AggregateVersion":4});
    let operation = manual_orders::change(&registry, &context, &order, &trade, json!({
        "RequestedTradeDate":"2026-11-10","TradeReference":"Updated","TradeType":"ShortIronCondor",
        "BaseContractId":"ES20261218","BaseContractSymbol":"ES",
    }), uuid::Uuid::new_v4()).unwrap();
    assert_eq!(operation.prepared.subject.verb, "ChangeManualFundOrderTrade");
    let command = parameters::document(&registry,
        "TomasAI.IFM.Domain.Portfolio.Shared.Commands.ChangeManualFundOrderTradeCommand",
        &operation.prepared.body, true).unwrap();
    assert_eq!(command["Request"]["TradeId"], 4301);
    assert_eq!(command["Request"]["ExpectedOrderVersion"], 5);
    assert_eq!(command["Request"]["ExpectedTradeVersion"], 4);
    assert_eq!(command["Request"]["BaseContractId"], "ES20261218");
}

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
