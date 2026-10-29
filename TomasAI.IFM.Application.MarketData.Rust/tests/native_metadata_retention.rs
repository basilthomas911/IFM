//! Released-window metadata is finite and cannot permanently poison rebuilding.
use ifm_market_data_native::{
    messaging::publisher::BrokerSettings, option_chain::reference_bootstrap::Bootstrap,
    reference::option_contract::OptionContract,
};
use std::collections::BTreeSet;
fn cache() -> Bootstrap {
    Bootstrap::new(
        BrokerSettings::new(
            "nats://127.0.0.1:9".into(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        "development".into(),
    )
    .unwrap()
}
#[test]
fn more_than_the_total_model_capacity_can_churn_without_retaining_released_models() {
    let fixture: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/reviewed-options.json"
    ))
    .unwrap();
    let mut source = fixture[0]["command"].clone();
    let mut prior = cache();
    let mut old = String::new();
    for index in 1..=4096 {
        let id = format!("window-{index}");
        source[0] = id.clone().into();
        source[16] = (100 + index).into();
        let model = OptionContract::from_command(&source).unwrap();
        let next = cache();
        next.register_option_model(model).unwrap();
        next.inherit_selected_models(&prior, &[id.clone()].into_iter().collect())
            .unwrap();
        assert!(next.option_model(&id).unwrap().is_some());
        if !old.is_empty() {
            assert!(next.option_model(&old).unwrap().is_none());
        }
        prior = next;
        old = id;
    }
    let empty = cache();
    empty
        .inherit_selected_models(&prior, &BTreeSet::new())
        .unwrap();
    assert!(empty.option_model(&old).unwrap().is_none());
}
#[test]
fn unchanged_desired_models_remain_available_and_explicit_facts_take_precedence() {
    let fixture: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/reviewed-options.json"
    ))
    .unwrap();
    let mut source = fixture[0]["command"].clone();
    let id = source[0].as_str().unwrap().to_owned();
    let prior = cache();
    prior
        .register_option_model(OptionContract::from_command(&source).unwrap())
        .unwrap();
    let selected = [id.clone()].into_iter().collect();
    let next = cache();
    next.inherit_selected_models(&prior, &selected).unwrap();
    assert_eq!(
        next.option_model(&id).unwrap().unwrap().value(),
        prior.option_model(&id).unwrap().unwrap().value()
    );
    source[16] = 999.into();
    let replacement = cache();
    replacement
        .register_option_model(OptionContract::from_command(&source).unwrap())
        .unwrap();
    replacement
        .inherit_selected_models(&prior, &selected)
        .unwrap();
    assert_eq!(
        replacement
            .option_model(&id)
            .unwrap()
            .unwrap()
            .convention()
            .unwrap()
            .instrument(),
        999
    );
}
