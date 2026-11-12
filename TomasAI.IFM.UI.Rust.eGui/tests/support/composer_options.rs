use super::*;
use ifm_ui_egui::{
    backend::schema::Registry,
    state::trades::TradeController,
    contract_editor::{ContractPublication, ContractResult},
    wire,
};
use serde_json::{Value as Json, json};


/// Provides control for the composer options implementation.
async fn control(b: &Backend, verb: &str, value: Value) -> Value {
    service_result(
        &b.request(
            &ActorSubject::query("NativeCompatibility", verb, "none"),
            &value,
            None,
            None,
        )
        .await
        .unwrap(),
    )
    .unwrap()
    .clone()
}


/// Provides act for the composer options implementation.
async fn act(
    state: &mut TradeController,
    b: &Backend,
    r: &Registry,
    generation: u64,
    action: &str,
    fields: Json,
) -> ContractPublication {
    state.execute(b,r,&json!({"schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"fields":fields,"originalId":if action=="load"{Json::Null}else{json!("123")}})).await.unwrap()
}


/// Verifies that composer options match hybrid snapshot and keep manual clearing and owner
/// lifecycle.
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn composer_options_match_hybrid_snapshot_and_keep_manual_clearing_and_owner_lifecycle() {
    let b = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap();
    let r = Registry::compiled().unwrap();
    for (verb, name) in [
        ("TradeWorkspaceMode", "ComposeOptions"),
        ("BrokerInitMode", "Normal"),
        ("BrokerReadMode", "Empty"),
        ("OptionChainMode", "ComposeOptions"),
    ] {
        control(&b, verb, name.into()).await;
    }
    let mut state = TradeController::default();
    let mut current = act(&mut state, &b, &r, 0, "load", json!({})).await;
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    current = act(
        &mut state,
        &b,
        &r,
        current.generation,
        "compose",
        json!({"tradeId":"456"}),
    )
    .await;
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    assert_eq!(current.result.context["expiry"], "");
    assert_eq!(current.result.context["tradeDate"], "2026-10-26");
    assert_eq!(current.result.choices["expiry"], ["2026-10-28", "2027-03-19 / ESH7", "2027-03-19 / ESM7"]);
    let draft_session = current.result.context["composerSessionId"].clone();
    for live in ["false", "true"] {
        current = act(&mut state, &b, &r, current.generation, "chain", json!({
            "tradeId":"456","composerSessionId":draft_session,"expiry":"",
            "putDelta":"16","callDelta":"16","putWidth":"50","callWidth":"50","live":live
        })).await;
        assert_eq!(current.outcomes[0].state, "Completed");
    }
    assert_eq!(control(&b, "OptionChainReads", Value::Nil).await.as_array().unwrap()[0], Value::from(0));
    current = act(&mut state, &b, &r, current.generation, "chain", json!({
        "tradeId":"456","composerSessionId":draft_session,"expiry":"2026-10-28",
        "putDelta":"16","callDelta":"16","putWidth":"50","callWidth":"50","live":"false"
    })).await;
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.outcomes[0].message);
    let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("tests/fixtures/backend/composer-options-session.msgpack");
    let expected: ContractResult = wire::decode(&std::fs::read(path).unwrap()).unwrap();
    for (key, value) in expected.context {
        if key != "composerSessionId" {
            assert_eq!(
                current.result.context.get(&key),
                Some(&value),
                "composer context {key}"
            );
        }
    }
    assert!(expected.choices["expiry"].iter().all(|expiry| current.result.choices["expiry"].contains(expiry)));
    let native: Vec<_> = current
        .result
        .rows
        .iter()
        .filter(|row| {
            [
                "market",
                "leg",
                "market-metrics",
                "qualification-report",
                "account",
                "broker-preview",
            ]
            .contains(&row.fields["kind"].as_str())
        })
        .collect();
    assert_eq!(native.len(), expected.rows.len());
    for (actual, expected) in native.iter().zip(&expected.rows) {
        assert_eq!(actual.id, expected.id);
        let actual: Json = wire::decode(actual.data.as_ref().unwrap()).unwrap();
        let data: Json = wire::decode(expected.data.as_ref().unwrap()).unwrap();
        assert_eq!(actual, data, "row {}", expected.id);
    }
    assert_eq!(current.result.context["price"], "-14");
    assert_eq!(current.result.context["canPlaceOrder"], "True");
    assert_eq!(
        control(&b, "OptionChainCount", Value::Nil)
            .await
            .as_array()
            .unwrap()[0],
        Value::from(0)
    );
    let session = current.result.context["composerSessionId"].clone();
    let fields = |width: &str| json!({"tradeId":"456","composerSessionId":session,"expiry":"2026-10-28","putDelta":"16","callDelta":"16","putWidth":width,"callWidth":"50","live":"true"});
    current = act(
        &mut state,
        &b,
        &r,
        current.generation,
        "chain",
        fields("50"),
    )
    .await;
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    assert_eq!(current.result.context["live"], "True");
    assert_eq!(
        control(&b, "OptionChainCount", Value::Nil).await.as_array().unwrap()[0],
        Value::from(1),
        "Selecting live liquidity must acquire exactly one subscription"
    );
    current = act(&mut state, &b, &r, current.generation, "chain", fields("0")).await;
    assert_eq!(current.outcomes[0].state, "Failed");
    assert_eq!(current.result.context["putWidth"], "50");
    assert_eq!(
        control(&b, "OptionChainCount", Value::Nil)
            .await
            .as_array()
            .unwrap()[0],
        Value::from(1)
    );
    current = act(
        &mut state,
        &b,
        &r,
        current.generation,
        "clear-staged",
        json!({"composerSessionId":session}),
    )
    .await;
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    assert_eq!(current.result.context["canPlaceOrder"], "False");
    current = act(
        &mut state,
        &b,
        &r,
        current.generation,
        "refresh-chain",
        json!({"composerSessionId":session}),
    )
    .await;
    assert_eq!(current.outcomes[0].state, "Completed");
    assert!(
        current
            .result
            .rows
            .iter()
            .all(|row| row.fields["kind"] != "leg")
    );
    let reads = control(&b, "OptionChainReads", Value::Nil).await;
    current = act(&mut state, &b, &r, current.generation, "trade-date",
        json!({"composerSessionId":session,"tradeDate":"2026-10-29"})).await;
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.outcomes[0].message);
    assert_eq!(current.result.context["tradeDate"], "2026-10-29");
    assert_eq!(current.result.context["expiry"], "");
    assert_eq!(current.result.context["price"], "");
    assert!(!current.result.choices["expiry"].contains(&"2026-10-28".into()));
    assert_eq!(control(&b, "OptionChainReads", Value::Nil).await, reads);
    assert_eq!(control(&b, "OptionChainCount", Value::Nil).await.as_array().unwrap()[0], Value::from(0));
    current = act(&mut state, &b, &r, current.generation, "close", json!({})).await;
    assert_eq!(current.outcomes[0].state, "Completed");
    assert_eq!(
        control(&b, "OptionChainCount", Value::Nil)
            .await
            .as_array()
            .unwrap()[0],
        Value::from(0)
    );
    for (verb, name) in [
        ("BrokerInitMode", "Disabled"),
        ("TradeWorkspaceMode", "Disabled"),
        ("BrokerReadMode", "Normal"),
        ("OptionChainMode", "Normal"),
    ] {
        control(&b, verb, name.into()).await;
    }
}
