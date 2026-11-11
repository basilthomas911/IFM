use ifm_ui_egui::{backend::schema::Registry, state::trade_data::TradeData};
use rmpv::Value;
use std::collections::BTreeMap;

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
