#[test]
fn embedded_risk_evidence_matches_managed_json() {
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");
    let registry = ifm_ui_egui::backend::schema::Registry::compiled().unwrap();
    for name in ["risk-observation", "risk-completed"] {
        let wire = ifm_ui_egui::backend::codec::decode(
            &std::fs::read(root.join(format!("{name}.msgpack"))).unwrap(),
        )
        .unwrap();
        let text=ifm_ui_egui::services::inspection_json::render(&registry,"TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskObservation",&wire).unwrap();
        let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join(format!("target/cross-language/{name}-native.json"));
        std::fs::write(path, &text).unwrap();
        let expected =
            std::fs::read_to_string(root.join(format!("{name}-embedded-json.txt"))).unwrap();
        if text != expected {
            let first = text
                .bytes()
                .zip(expected.bytes())
                .position(|(a, b)| a != b)
                .unwrap_or(text.len().min(expected.len()));
            panic!(
                "{name}: Embedded JSON differs at byte {first}; compare target/cross-language/{name}-native.json with {name}-embedded-json.txt"
            );
        }
    }
}
