use ifm_ui_egui::state::actor_health::{ControlOptions, project};
use serde_json::Value;
fn snapshot() -> ifm_ui_egui::models::actor_health::ActorHealthSnapshot {
    serde_json::from_str(include_str!("fixtures/backend/actor-health-http.json")).unwrap()
}
#[test]
fn native_actor_explorer_matches_the_unchanged_hybrid_controller_projection() {
    let input = snapshot();
    let from = "2026-10-26T11:30:00Z".parse().unwrap();
    for (enabled, expected) in [
        (
            false,
            include_str!("fixtures/backend/actor-health-display.json"),
        ),
        (
            true,
            include_str!("fixtures/backend/actor-health-controlled-display.json"),
        ),
    ] {
        let expected: Value = serde_json::from_str(expected).unwrap();
        let result = project(
            &input,
            from,
            &ControlOptions {
                enabled,
                requester: "Fixture operator".into(),
                ..Default::default()
            },
        )
        .unwrap();
        assert_eq!(result.result.view, expected["View"]);
        assert_eq!(
            result.result.rows.len(),
            expected["Rows"].as_array().unwrap().len()
        );
        for (actual, expected) in result
            .result
            .rows
            .iter()
            .zip(expected["Rows"].as_array().unwrap())
        {
            assert_eq!(actual.id, expected["Id"]);
            assert_eq!(
                serde_json::to_value(&actual.fields).unwrap(),
                expected["Fields"],
                "row {}",
                actual.id
            );
        }
        assert_eq!(
            serde_json::to_value(&result.result.choices).unwrap(),
            expected["Choices"]
        );
        assert_eq!(
            serde_json::to_value(&result.result.context).unwrap(),
            expected["Context"]
        );
        assert_eq!(
            result.targets["mailbox:Realtime:IronCondorPosition:101.701.1901.1501"].generation,
            17
        );
    }
}
#[test]
fn unavailable_authority_and_bad_observations_cannot_expose_actor_controls() {
    let mut input = snapshot();
    let from = "2026-10-26T11:30:00Z".parse().unwrap();
    let options = ControlOptions {
        enabled: true,
        requester: "Operator".into(),
        ..Default::default()
    };
    input.supervisor_authority_state =
        ifm_ui_egui::models::health_fields::Present::Value("HostRecoveryRequired".into());
    assert_eq!(
        project(&input, from, &options).unwrap().result.context["controls"],
        "false"
    );
    input.observed_utc = ifm_ui_egui::models::health_fields::Present::Null;
    assert!(project(&input, from, &options).is_err());
}
