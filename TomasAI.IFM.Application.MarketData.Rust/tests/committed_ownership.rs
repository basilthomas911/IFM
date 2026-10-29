//! Native physical identity matches the unchanged CLR hashing implementation.
use ifm_market_data_native::{
    dataset::background::ScopeDemand,
    option_chain::committed::{Authority, Book, Lease, Plan},
};
use serde_json::{Value, json};
fn plan(input: &Value) -> Plan {
    let demand=ScopeDemand::from_command(&json!({"owner":[1,0],"maturity":739929,"candidates":input["candidates"],"complete":true,"separate":false,"independent":false})).unwrap();
    Plan {
        id: "a".repeat(64),
        maturity: 739929,
        candidates: demand.candidates,
        futures: vec![],
        routes: vec![],
        references: None,
        non_utc: false,
        separate: false,
        independent: false,
    }
}
#[test]
fn every_physical_hash_matches_clr_decimal_utf16_and_maximum_scope_oracle() {
    let cases: Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/ownership.json"
    ))
    .unwrap();
    assert_eq!(cases.as_array().unwrap().len(), 81);
    for (index, input) in cases.as_array().unwrap().iter().enumerate() {
        assert_eq!(
            plan(input).physical_digest().unwrap(),
            input["digest"].as_str().unwrap(),
            "physical case {index}"
        );
    }
    assert_eq!(plan(&cases[80]).candidates.len(), 2048);
}
#[test]
fn committed_owner_validation_cannot_install_or_withdraw_other_owners() {
    let cases: Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/ownership.json"
    ))
    .unwrap();
    let plan = plan(&cases[0]);
    let contract = plan.candidates[0].contract.clone();
    let scope = plan.id.clone();
    let mut id = [0; 16];
    id[15] = 1;
    let source = Authority {
        id: "workflow".into(),
        version: 1,
        status: 0,
        reason: "Committed".into(),
        event: [1; 16],
        digest: "a".repeat(64),
        workflow_type: "Position".into(),
        workflow: "position".into(),
        leg: "option".into(),
        leases: vec![Lease {
            id,
            version: 1,
            purpose: 4,
            provider: "Databento".into(),
            schema: "mbp-1".into(),
            plan: scope.clone(),
            contract: contract.clone(),
            option: true,
            underlying: Some(plan.candidates[0].underlying.clone()),
        }],
    };
    let book = Book {
        authority: "IFM".into(),
        revision: 1,
        journal_digest: "b".repeat(64),
        signature: [1; 32],
        sources: [("workflow".into(), source)].into_iter().collect(),
        plans: [(scope.clone(), plan)].into_iter().collect(),
    };
    let input = json!({"authority":"IFM","revision":1,"digest":cases[0]["digest"],"owners":[{"id":"00000000000000000000000000000001","contracts":[contract]}]});
    assert!(book.validate_ownership(&scope, &input).unwrap());
    for field in ["authority", "revision", "digest", "owners"] {
        let mut wrong = input.clone();
        wrong[field] = Value::Null;
        assert!(book.validate_ownership(&scope, &wrong).is_err(), "{field}");
    }
    let mut wrong = input.clone();
    wrong["owners"][0]["contracts"] = json!(["another-contract"]);
    assert!(book.validate_ownership(&scope, &wrong).is_err());
    wrong = input.clone();
    wrong["owners"][0]["id"] = json!("00000000000000000000000000000002");
    assert!(book.validate_ownership(&scope, &wrong).is_err());
    let mut empty = input.clone();
    empty["owners"] = json!([]);
    assert!(book.validate_ownership(&scope, &empty).is_err());
    assert!(!book.validate_ownership(&"c".repeat(64), &empty).unwrap());
    let mut duplicate = input.clone();
    duplicate["owners"]
        .as_array_mut()
        .unwrap()
        .push(input["owners"][0].clone());
    assert!(book.validate_ownership(&scope, &duplicate).is_err());
    assert_eq!(
        book.sources["workflow"].leases.len(),
        1,
        "Compatibility checks never mutate committed desired state"
    );
    let mut excessive = input;
    excessive["owners"] = Value::Array(vec![excessive["owners"][0].clone(); 129]);
    assert!(book.validate_ownership(&scope, &excessive).is_err());
}
