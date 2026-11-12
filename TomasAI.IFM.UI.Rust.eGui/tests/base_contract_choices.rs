use ifm_ui_egui::{backend::schema::Registry, state::trade_data::TradeData};
use rmpv::Value;
use std::collections::BTreeMap;
use ifm_ui_egui::{
    backend::contracts::date_only_wire,
    models::{trade_contracts::FuturesContractV3ReadModel, wire_types::FromBackend},
    services::{business_context::BusinessContext, composer_market::{Market, next_contract_expiry}},
};

#[test]
fn new_option_draft_uses_value_date_and_next_listed_cycle() {
    let registry = Registry::compiled().unwrap();
    let contract = |id: &str, symbol: &str, date: &str| {
        let wire = registry.encode(
            "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",
            &BTreeMap::from([
                ("ContractId".into(), id.into()),
                ("Symbol".into(), symbol.into()),
                ("Exchange".into(), "CME".into()),
                ("LastTradeDate".into(), date_only_wire(date.parse().unwrap())),
            ]),
        ).unwrap();
        FuturesContractV3ReadModel::from_backend(&wire).unwrap()
    };
    let base = contract("ES20261218", "ES", "2026-12-18");
    let catalog = [
        contract("ES20270617", "ES", "2027-06-17"),
        contract("MES20270101", "MES", "2027-01-01"),
        base.clone(),
        contract("ES20270319", "ES", "2027-03-19"),
    ];
    assert_eq!(next_contract_expiry(&base, &catalog).unwrap().to_string(), "2027-03-19");
    assert!(next_contract_expiry(&base, &catalog[..0]).is_err());
    let date = "2026-11-11".parse().unwrap();
    let context = BusinessContext { value_date: date, active_value_date: Some(date),
        as_of_utc: "2026-11-11T17:00:00Z".parse().unwrap(), revision: 1, market_state: 2,
        allow_development_emulator: true };
    for development in [true, false] {
        let draft = Market::new(base.clone(), "ShortIronCondor".into(), &context, true, development);
        assert_eq!(draft.trade_date, date);
        assert!(draft.expiry.is_none());
        assert!(draft.draft.legs.is_empty());
    }
}

/// New trade choices preserve the Fund query's current-first order, irrespective of identifier spelling.
#[test]
fn fund_choices_preserve_current_first_order_and_empty_configuration() {
    let registry = Registry::compiled().unwrap();
    let mut state = TradeData::default();
    assert!(state.base_contract_choices().unwrap().is_empty());
    state.fund = Some(2);
    state.contracts =
        ["ES20261218", "ES20270319"]
            .iter()
            .map(|id| {
                registry.encode(
        "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",
        &BTreeMap::from([("ContractId".into(), Value::from(*id))])).unwrap()
            })
            .collect();
    assert_eq!(
        state.base_contract_choices().unwrap(),
        ["ES20261218", "ES20270319"]
    );
}
