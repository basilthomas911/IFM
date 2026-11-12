use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    services::strategy_display,
};
use serde_json::Value;


/// Reads strategy display for strategy display.
fn read(name: &str) -> Vec<u8> {
    std::fs::read(
        std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("tests/fixtures/backend")
            .join(name),
    )
    .unwrap()
}


/// Provides difference for the strategy display implementation.
fn difference(path: &str, a: &Value, b: &Value) -> Option<String> {
    if a == b {
        return None;
    }
    match (a, b) {
        (Value::Object(a), Value::Object(b)) => {
            for key in a.keys().chain(b.keys()) {
                if let Some(d) = difference(
                    &format!("{path}/{key}"),
                    a.get(key).unwrap_or(&Value::Null),
                    b.get(key).unwrap_or(&Value::Null),
                ) {
                    return Some(d);
                }
            }
        }
        (Value::Array(a), Value::Array(b)) => {
            if a.len() != b.len() {
                return Some(format!("{path}: lengths {} != {}", a.len(), b.len()));
            }
            for (i, (a, b)) in a.iter().zip(b).enumerate() {
                if let Some(d) = difference(&format!("{path}/{i}"), a, b) {
                    return Some(d);
                }
            }
        }
        _ => {
            return Some(format!(
                "{path}: {} != {}",
                a.to_string().chars().take(200).collect::<String>(),
                b.to_string().chars().take(200).collect::<String>()
            ));
        }
    }
    None
}


/// Preserves grid data and complete evidence; user tooltips have separate presentation tests.
#[test]
fn strategy_grid_data_and_complete_details_tree_preserve_backend_evidence() {
    let registry = Registry::compiled().unwrap();
    let wire = codec::decode(&read("strategy-view.msgpack")).unwrap();
    let mut expected: Value = serde_json::from_slice(&read("strategy-row-display.json")).unwrap();
    let mut row = serde_json::to_value(strategy_display::row(&registry, &wire, 1000).unwrap()).unwrap();
    // The legacy fixture has two active indicators and predates server-authored reasons.
    assert_eq!(row["actors"].as_array().unwrap().len(), 2);
    for document in [&mut expected, &mut row] {
        for actor in document["actors"].as_array_mut().unwrap() {
            actor.as_object_mut().unwrap().remove("value");
        }
    }
    assert!(
        difference("row", &row, &expected).is_none(),
        "{}",
        difference("row", &row, &expected).unwrap_or_default()
    );
    let expected: Value = serde_json::from_slice(&read("strategy-details-display.json")).unwrap();
    let result =
        serde_json::to_value(strategy_display::details(&registry, &wire).unwrap()).unwrap();


    /// Provides missing for the strategy display implementation.
    fn missing(value: &Value, items: &mut Vec<String>) {
        match value {
            Value::String(v) if v.contains("No native display getter") => items.push(v.clone()),
            Value::Array(v) => {
                for v in v {
                    missing(v, items)
                }
            }
            Value::Object(v) => {
                for v in v.values() {
                    missing(v, items)
                }
            }
            _ => {}
        }
    }
    let mut missing_getters = vec![];
    missing(&result, &mut missing_getters);
    assert!(missing_getters.is_empty(), "{}", missing_getters.join("\n"));
    for key in ["workflowId", "revision", "roots", "header"] {
        assert!(
            difference(key, &result[key], &expected[key]).is_none(),
            "{}",
            difference(key, &result[key], &expected[key]).unwrap_or_default()
        );
    }
    assert!(!result.to_string().contains("No native display getter"));
}

/// Every indicator displays the original C# sentence byte for byte.
#[test]
fn visible_stages_keep_the_recorded_sentence_and_stop_at_a_terminal_stage() {
    let registry = Registry::compiled().unwrap();
    let mut wire = codec::decode(&read("strategy-view.msgpack")).unwrap();
    for (status, continuation, outcome, label, expected_count) in [
        (1, 0, 0, "Processing", 2),
        (2, 1, 0, "Continued", 2),
        (2, 2, 7, "No Trade", 1),
        (2, 2, 3, "Stopped", 1),
        (3, 0, 2, "Failed", 1),
        (4, 0, 4, "Timed Out", 1),
        (5, 0, 5, "Cancelled", 1),
    ] {
        let rmpv::Value::Array(fields) = &mut wire else { panic!("workflow fields") };
        fields[22] = outcome.into();
        let rmpv::Value::Array(stage) = &mut fields[13] else { panic!("stage fields") };
        stage[0] = status.into();
        stage[1] = continuation.into();
        stage.resize(17, rmpv::Value::Nil);
        let sentence = "ESZ6’s trend and VX volatility supported the Daily classification.";
        stage[16] = sentence.into();
        let row = strategy_display::row(&registry, &wire, 1000).unwrap();
        assert_eq!(row.actors.len(), expected_count);
        assert_eq!(row.actors[0].state, label);
        assert_eq!(row.actors[0].value, sentence);
    }
}

/// A No Trade decision in Trade Selection shows RD, MC and TS only.
#[test]
fn no_trade_in_trade_selection_is_the_last_visible_indicator() {
    let registry = Registry::compiled().unwrap();
    let mut wire = codec::decode(&read("strategy-view.msgpack")).unwrap();
    let rmpv::Value::Array(fields) = &mut wire else { panic!("workflow fields") };
    fields[22] = 7.into();
    let rmpv::Value::Array(market) = &mut fields[14] else { panic!("market condition fields") };
    market[0] = 2.into();
    market[1] = 1.into();
    let rmpv::Value::Array(selection) = &mut fields[15] else { panic!("trade selection fields") };
    selection.resize(17, rmpv::Value::Nil);
    selection[0] = 2.into();
    selection[1] = 2.into();
    let reason = "The dislocated ES market did not match the entry rules.";
    selection[16] = reason.into();

    let row = strategy_display::row(&registry, &wire, 1000).unwrap();
    assert_eq!(row.actors.iter().map(|actor| actor.name.as_str()).collect::<Vec<_>>(), ["RD", "MC", "TS"]);
    assert_eq!(row.actors[2].state, "No Trade");
    assert_eq!(row.actors[2].value, reason);
}

/// Reads Development's compact grid and compares it with the persisted C# action evidence.
#[tokio::test]
#[ignore = "Requires the Development API and IFM_VERIFY_NATS_URL"]
async fn development_pipeline_reasons_match_the_recorded_workflow() {
    use ifm_ui_egui::{
        backend::transport::{Backend, Settings},
        services::strategy::{workflow_detail, workflow_page},
    };
    use std::time::Duration;

    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_VERIFY_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(25),
    }).await.unwrap();
    let registry = Registry::compiled().unwrap();
    let until = chrono::Utc::now();
    let mut compared = 0;
    for frame in ["Daily", "Weekly", "Monthly"] {
        let (rows, _) = workflow_page(
            &backend, &registry, "ES", frame, until - chrono::TimeDelta::days(21), until, 1,
            tokio::time::Instant::now() + Duration::from_secs(25),
        ).await.unwrap();
        let row = rows.first().unwrap_or_else(|| panic!("No Development workflows for {frame}"));
        assert!(!row.row.actors.is_empty());
        assert!(row.row.actors.iter().all(|actor| !actor.value.trim().is_empty()));
        let view = workflow_detail(&backend, &registry, &row.row.id, row.revision).await.unwrap();
        if view.header.revision == row.revision {
            let expected = view.header.row(0).unwrap();
            assert_eq!(row.row.actors.iter().map(|actor| &actor.name).collect::<Vec<_>>(),
                expected.actors.iter().map(|actor| &actor.name).collect::<Vec<_>>());
            for (indicator, stage) in row.row.actors.iter().zip(expected.actors) {
                if !stage.value.is_empty() {
                    assert_eq!(indicator.value, stage.value);
                    compared += 1;
                }
            }
        }
        println!("{frame} {}: {:?}", row.row.id, row.row.actors.iter()
            .map(|actor| (&actor.state, &actor.value)).collect::<Vec<_>>());
    }
    println!("Compared {compared} tooltips with recorded stage evidence");
    if std::env::var_os("IFM_VERIFY_FRESH_WORKFLOW").is_some() {
        assert!(compared > 0, "Expected at least one fresh, recorded stage reason");
    }
}
