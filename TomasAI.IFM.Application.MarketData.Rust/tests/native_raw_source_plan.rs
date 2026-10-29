//! Raw native source construction preserves reviewed physical identities.
use databento::dbn::{SType, Schema};
use ifm_market_data_native::{
    dataset::raw_options::Plan, latest::Asset, reference::option_contract::OptionContract,
};
fn raw() -> serde_json::Value {
    let fixtures: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/reviewed-options.json"
    ))
    .unwrap();
    fixtures[0]["command"].clone()
}
#[test]
fn complete_source_preserves_order_and_deduplicates_shared_underlyings() {
    let first = raw();
    let mut second = first.clone();
    second[0] = serde_json::json!("ES20261110P6000");
    second[16] = serde_json::json!(first[16].as_u64().unwrap() + 1);
    second[17] = serde_json::json!("put");
    let models = [
        OptionContract::from_command(&first).unwrap(),
        OptionContract::from_command(&second).unwrap(),
    ];
    let plan = Plan::from_models(&models).unwrap();
    assert_eq!(plan.registrations.len(), 3);
    assert_eq!(
        plan.registrations
            .iter()
            .filter(|r| r.asset == Asset::Futures)
            .count(),
        1
    );
    assert_eq!(plan.session.subscriptions.len(), 2);
    assert_eq!(plan.session.subscriptions[0].schema, Schema::Trades);
    assert_eq!(plan.session.subscriptions[1].schema, Schema::Mbp1);
    for group in &plan.session.subscriptions {
        assert_eq!(group.symbology, SType::InstrumentId);
        assert_eq!(group.symbols.len(), 3);
    }
}
#[test]
fn complete_source_rejects_unreviewed_duplicate_conflicting_and_oversized_input() {
    let first = raw();
    let model = OptionContract::from_command(&first).unwrap();
    assert!(Plan::from_models(&[]).is_err());
    assert!(Plan::from_models(&[model.clone(), model.clone()]).is_err());
    assert!(Plan::from_models(&vec![model; 2049]).is_err());
    for index in [0, 14, 17, 19, 20, 23, 28, 29, 30, 33] {
        let mut invalid = first.clone();
        invalid[index] = serde_json::json!("");
        let parsed = OptionContract::from_command(&invalid).unwrap();
        assert!(Plan::from_models(&[parsed]).is_err(), "field {index}");
    }
    let mut conflict = first.clone();
    conflict[0] = serde_json::json!("other");
    assert!(
        Plan::from_models(&[
            OptionContract::from_command(&first).unwrap(),
            OptionContract::from_command(&conflict).unwrap()
        ])
        .is_err()
    );
}
