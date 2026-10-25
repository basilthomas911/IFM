use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    services::inspection_display,
};
#[test]
fn risk_observation_text_matches_current_managed_presenter() {
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");
    for name in ["risk-observation", "risk-completed"] {
        let wire =
            codec::decode(&std::fs::read(root.join(format!("{name}.msgpack"))).unwrap()).unwrap();
        let expected = std::fs::read_to_string(root.join(format!("{name}-display.txt"))).unwrap();
        let actual = inspection_display::risk(&Registry::compiled().unwrap(), &wire).unwrap();
        assert_eq!(actual, expected, "{name}");
        let evidence=ifm_ui_egui::services::strategy_display::evidence(&Registry::compiled().unwrap(),"TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskObservation",&wire).unwrap();
        let expected = std::fs::read_to_string(root.join(format!("{name}-evidence.txt"))).unwrap();
        if evidence != expected {
            let at = evidence
                .chars()
                .zip(expected.chars())
                .position(|(a, b)| a != b)
                .unwrap_or(evidence.chars().count().min(expected.chars().count()));
            panic!(
                "{name} evidence differs at character {at}: native {:?}, managed {:?}",
                evidence
                    .chars()
                    .skip(at.saturating_sub(65))
                    .take(210)
                    .collect::<String>(),
                expected
                    .chars()
                    .skip(at.saturating_sub(65))
                    .take(210)
                    .collect::<String>()
            );
        }
    }
}
