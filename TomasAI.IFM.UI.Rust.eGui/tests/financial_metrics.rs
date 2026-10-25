use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    services::financial,
};
#[test]
fn financial_report_display_preserves_backend_metrics_and_basis() {
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");
    let cases: Vec<serde_json::Value> = serde_json::from_slice(
        &std::fs::read(root.join("financial-metrics-display.json")).unwrap(),
    )
    .unwrap();
    let registry = Registry::compiled().unwrap();
    for case in cases {
        let wire = codec::decode(
            &std::fs::read(root.join(format!("{}.msgpack", case["name"].as_str().unwrap())))
                .unwrap(),
        )
        .unwrap();
        let fields = financial::metrics_context(&registry, &wire).unwrap();
        assert_eq!(
            serde_json::to_value(fields).unwrap(),
            case["fields"],
            "{}",
            case["name"]
        );
    }
}
