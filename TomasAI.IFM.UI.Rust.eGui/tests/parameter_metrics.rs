use ifm_ui_egui::{backend::schema::Registry, services::parameter_metrics};
use serde_json::Value;
fn read(name: &str) -> Value {
    serde_json::from_slice(
        &std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend")
                .join(format!("parameter-metrics-{name}.json")),
        )
        .unwrap(),
    )
    .unwrap()
}
#[test]
fn metric_selection_preparation_matches_hybrid_legacy_projection_and_identifiers() {
    let registry = Registry::compiled().unwrap();
    let set = "f256f7a9-b41c-4fb9-9ec3-eab3497bc86a";
    let legacy = read("legacy");
    let (signals, observations) =
        parameter_metrics::from_legacy(&registry, set, legacy.as_array().unwrap()).unwrap();
    assert_eq!(serde_json::json!(signals), read("signals"));
    assert_eq!(serde_json::json!(observations), read("observations"));
    let converted =
        parameter_metrics::legacy(&registry, set, &signals, &observations, &read("horizon"))
            .unwrap();
    assert_eq!(serde_json::json!(converted), read("converted"));
    let mut invalid = signals;
    invalid[0]["PeriodLength"] = serde_json::json!(3);
    assert!(
        parameter_metrics::legacy(&registry, set, &invalid, &observations, &read("horizon"))
            .is_err()
    );
    assert!(
        parameter_metrics::legacy(
            &registry,
            set,
            &[],
            &observations,
            &serde_json::json!({"TimeFrames":[]})
        )
        .is_err()
    );
}
