use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    models::strategy::{StageFailure, WorkflowHeader},
};

fn header() -> WorkflowHeader {
    let registry = Registry::compiled().unwrap();
    let bytes = include_bytes!("fixtures/backend/strategy-view.msgpack");
    WorkflowHeader::from_backend(&registry, &codec::decode(bytes).unwrap()).unwrap()
}

#[test]
fn every_stage_explains_no_trade_in_plain_english_for_each_timeframe() {
    let cases = [
        ("RD.VOL.HIGH", "Volatility was high."),
        (
            "MC.ASSESSMENT.INHERITED_NO_NEW_TRADE",
            "Regime Discovery blocked new trades.",
        ),
        (
            "TS.NO_COMPATIBLE_CANDIDATE",
            "No configured strategy matches the current market conditions.",
        ),
        ("No eligible construction.", "No eligible construction."),
        (
            "RM.PORTFOLIO.NO_TRADE_ORDERS",
            "Portfolio approval produced no authorized trade orders.",
        ),
    ];
    for frame in ["Daily", "Weekly", "Monthly"] {
        for (index, (code, description)) in cases.iter().enumerate() {
            let mut workflow = header();
            workflow.frame = frame.into();
            workflow.status = "Stopped".into();
            workflow.outcome = "NoTrade".into();
            for stage in &mut workflow.stages {
                stage.processing = "NotStarted".into();
            }
            let stage = &mut workflow.stages[index];
            stage.processing = "Completed".into();
            stage.continuation = "Stop".into();
            stage.failure = None;
            stage.decision_reasons = vec![code.to_string()];
            let row = workflow.row(1).unwrap();
            let actor = &row.actors[0];
            assert_eq!(actor.state, "No Trade");
            assert!(actor.value.contains(description), "{}", actor.value);
            assert_eq!(actor.value.lines().count(), 2);
            assert!(!actor.value.contains("continuation"));
        }
    }
}

#[test]
fn errors_show_a_brief_reason_and_preserve_the_recorded_failure() {
    let mut workflow = header();
    workflow.stages[0].failure = Some(StageFailure {
        error_type: "Backend".into(),
        error_code: "23102".into(),
        error_message: "Required RSI observation is unavailable.\n  at internal stack trace".into(),
    });
    let actor = &workflow.row(1).unwrap().actors[0];
    assert_eq!(
        actor.value,
        "Regime Discovery: Error\nReason: Required RSI observation is unavailable."
    );
    assert_eq!(
        workflow.stages[0].failure.as_ref().unwrap().error_code,
        "23102"
    );
}

#[test]
fn valid_no_trade_with_recorded_option_chain_failure_is_not_relabelled_as_an_error() {
    let mut workflow = header();
    workflow.status = "Completed".into();
    workflow.outcome = "NoTrade".into();
    let stage = &mut workflow.stages[0];
    stage.processing = "Completed".into();
    stage.continuation = "Stop".into();
    stage.decision_reasons.clear();
    stage.failure = Some(StageFailure {
        error_type: "OptionChainUnavailable".into(),
        error_code: "24101".into(),
        error_message: "No eligible option contracts are available.".into(),
    });
    let actor = &workflow.row(1).unwrap().actors[0];
    assert_eq!(actor.state, "No Trade");
    assert!(
        actor
            .value
            .contains("No Trade\nReason: No eligible option contracts are available.")
    );
}

#[test]
fn missing_or_unfamiliar_reasons_are_visible_without_inventing_an_explanation() {
    let mut workflow = header();
    workflow.status = "Stopped".into();
    workflow.outcome = "NoTrade".into();
    workflow.stages[0].processing = "Completed".into();
    workflow.stages[0].continuation = "Stop".into();
    workflow.stages[0].failure = None;
    workflow.stages[0].decision_reasons.clear();
    assert!(
        workflow.row(1).unwrap().actors[0]
            .value
            .contains("No decision reason was recorded")
    );
    workflow.stages[0].decision_reasons = vec!["RD.NEW_POLICY_LIMIT".into()];
    assert!(
        workflow.row(1).unwrap().actors[0]
            .value
            .contains("New policy limit.")
    );
}

#[test]
fn inherited_restrictions_are_described_once_with_the_underlying_cause() {
    let mut workflow = header();
    workflow.status = "Stopped".into();
    workflow.outcome = "NoTrade".into();
    let stage = &mut workflow.stages[0];
    stage.processing = "Completed".into();
    stage.continuation = "Stop".into();
    stage.decision_reasons = vec![
        "MC.ASSESSMENT.INHERITED_NO_NEW_TRADE".into(),
        "MC.ASSESSMENT.INHERITED_NO_NEW_TRADE".into(),
        "RD.VOL.BACKWARDATION".into(),
    ];
    let value = workflow.row(1).unwrap().actors[0].value.clone();
    assert_eq!(
        value
            .matches("Regime Discovery blocked new trades.")
            .count(),
        1
    );
    assert!(value.contains("VX futures were in backwardation."));
}

#[test]
fn unavailable_market_inputs_name_the_observation_in_user_language() {
    let mut workflow = header();
    workflow.status = "Completed".into();
    workflow.outcome = "NoTrade".into();
    let stage = &mut workflow.stages[0];
    stage.processing = "Completed".into();
    stage.continuation = "Stop".into();
    stage.decision_reasons = vec![
        "MC.ASSESSMENT.UNAVAILABLE".into(),
        "MC.ASSESSMENT.REFERENCEQUOTE.STALE".into(),
    ];
    let value = workflow.row(1).unwrap().actors[0].value.clone();
    assert!(value.contains("Required market inputs are unavailable or out of date."));
    assert!(value.contains("The futures quote is out of date."));
    assert!(!value.contains("REFERENCEQUOTE"));
}
