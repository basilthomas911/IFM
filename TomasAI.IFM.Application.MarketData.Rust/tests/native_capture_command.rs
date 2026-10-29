//! Raw command boundaries are checked before native dataset ownership is touched.
use ifm_market_data_native::api::owner::CaptureCommand;
use serde_json::{Value, json};
fn command() -> Value {
    json!({"dataset":"GLBX.MDP3","id":vec![1;16],"generation":vec![2;16],
        "scope":"trade","horizon":"Daily","at":17931024000000000_i64,
        "until":17931024010000000_i64,"at_offset":0,"until_offset":0,
        "options":false,"maximum":2048,"age":1000,"skew":250,
        "selection":false,"allow_missing":false,"risk":[],
        "futures":[["ES20261218","ES","GLBX.MDP3","XCME","USD",
            17976096000000000_i64,0,"50","0.25","digest"]]})
}
#[test]
fn copies_futures_without_rounding_and_defers_definition_offset_rejection() {
    let value = command();
    let parsed = CaptureCommand::from_value(&value).unwrap();
    assert_eq!(parsed.futures.len(), 1);
    assert_eq!(parsed.request.id, [1; 16]);
    assert!(!parsed.non_utc_definition);
    let mut value = value;
    value["futures"][0][6] = json!(60);
    assert!(
        CaptureCommand::from_value(&value)
            .unwrap()
            .non_utc_definition
    );
}
#[test]
fn malformed_scalars_shapes_and_capacity_are_typed_failures() {
    for (field, bad) in [
        ("id", json!([1, 2])),
        ("generation", json!(vec![256; 16])),
        ("at_offset", json!(60)),
        ("until_offset", json!(-60)),
        ("age", json!(-1)),
        ("skew", json!(4294967296_u64)),
        ("maximum", json!(-1)),
        ("options", json!("false")),
        ("scope", json!("x".repeat(1025))),
        ("futures", json!([[]])),
        ("risk", json!(vec!["x"; 2049])),
    ] {
        let mut value = command();
        value[field] = bad;
        let failure = CaptureCommand::from_value(&value).err().expect(field);
        assert_eq!(failure.code, "InvalidSnapshotRequest", "{field}");
    }
    for index in [7, 8] {
        let mut value = command();
        value["futures"][0][index] = json!("NaN");
        assert!(CaptureCommand::from_value(&value).is_err());
    }
    let mut value = command();
    value["futures"] = json!(vec![value["futures"][0].clone(); 2049]);
    assert!(CaptureCommand::from_value(&value).is_err());
}
#[test]
fn option_requests_do_not_parse_unused_futures_and_preserve_dataset_failure() {
    let mut value = command();
    value["options"] = json!(true);
    value["futures"] = Value::Null;
    assert!(
        CaptureCommand::from_value(&value)
            .unwrap()
            .futures
            .is_empty()
    );
    value["dataset"] = json!("XCBF.PITCH");
    let failure = CaptureCommand::from_value(&value).err().unwrap();
    assert_eq!(failure.code, "Recovering");
    assert_eq!(failure.input, "GenerationId");
}

#[test]
fn readiness_wait_is_opt_in_and_malformed_flags_are_rejected() {
    let mut input = command();
    assert!(!CaptureCommand::from_value(&input).unwrap().wait);
    input["wait"] = true.into();
    assert!(CaptureCommand::from_value(&input).unwrap().wait);
    for invalid in [Value::Null, 1.into(), "true".into(), serde_json::json!([])] {
        input["wait"] = invalid;
        assert_eq!(
            CaptureCommand::from_value(&input).err().unwrap().code,
            "InvalidSnapshotRequest"
        );
    }
}
