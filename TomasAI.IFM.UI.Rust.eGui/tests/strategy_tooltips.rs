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
fn every_stage_preserves_the_backend_reason_for_each_timeframe() {
    let reasons = [
        "Regime Discovery needs a warm EMA200 observation.",
        "Market Condition: No Trade. VX futures are in backwardation.",
        "Trade Selection is waiting for the armed Daily continuation distance.",
        "Order Composition has no eligible option contracts.",
        "Risk Management produced no authorized trade orders.",
    ];
    for frame in ["Daily", "Weekly", "Monthly"] {
        for (index, reason) in reasons.iter().enumerate() {
            let mut workflow = header();
            workflow.frame = frame.into();
            workflow.status = "Completed".into();
            workflow.outcome = "NoTrade".into();
            for stage in &mut workflow.stages { stage.processing = "NotStarted".into(); }
            let stage = &mut workflow.stages[index];
            stage.processing = "Completed".into();
            stage.continuation = "Stop".into();
            stage.reason_sentence = (*reason).into();
            let row = workflow.row(1).unwrap();
            assert_eq!(row.actors[0].state, "No Trade");
            assert_eq!(row.actors[0].value, *reason);
        }
    }
}

#[test]
fn error_tooltip_uses_the_backend_sentence_and_keeps_diagnostic_evidence() {
    let mut workflow = header();
    let stage = &mut workflow.stages[0];
    stage.processing = "Failed".into();
    stage.reason_sentence = "Regime Discovery failed because the RSI observation is unavailable.".into();
    stage.failure = Some(StageFailure {
        error_type: "Backend".into(), error_code: "23102".into(),
        error_message: "Required RSI observation is unavailable.\n  at internal stack trace".into(),
    });
    let actor = &workflow.row(1).unwrap().actors[0];
    assert_eq!(actor.state, "Failed");
    assert_eq!(actor.value, workflow.stages[0].reason_sentence);
    assert!(!actor.value.contains("stack trace"));
    assert_eq!(workflow.stages[0].failure.as_ref().unwrap().error_code, "23102");
}

#[test]
fn a_completed_no_trade_with_diagnostic_evidence_is_not_relabelled_as_an_error() {
    let mut workflow = header();
    workflow.status = "Completed".into(); workflow.outcome = "NoTrade".into();
    let stage = &mut workflow.stages[0];
    stage.processing = "Completed".into(); stage.continuation = "Stop".into();
    stage.reason_sentence = "No Trade: no eligible option contracts are available.".into();
    stage.failure = Some(StageFailure { error_type: "OptionChainUnavailable".into(),
        error_code: "24101".into(), error_message: "No eligible option contracts are available.".into() });
    let row = workflow.row(1).unwrap();
    assert_eq!(row.actors[0].state, "No Trade");
    assert_eq!(row.actors[0].value, workflow.stages[0].reason_sentence);
}

#[test]
fn legacy_missing_reason_does_not_invent_a_client_side_decision() {
    let mut workflow = header();
    workflow.stages[0].reason_sentence.clear();
    assert!(workflow.row(1).unwrap().actors[0].value.is_empty());
}

#[test]
fn an_unconfirmed_entry_remains_in_progress_with_its_saved_reason() {
    let mut workflow = header();
    workflow.status = "Started".into(); workflow.outcome = "None".into();
    for stage in &mut workflow.stages {
        stage.processing = "Completed".into(); stage.continuation = "Proceed".into();
    }
    workflow.stages[4].reason_sentence = "Risk approved; entry commitment is unconfirmed. The original operation is being reconciled.".into();
    let row = workflow.row(1).unwrap();
    assert_eq!(row.end_state, "In Progress");
    assert_eq!(row.actors[4].value, workflow.stages[4].reason_sentence);
    assert!(!row.end_state.contains("Approved"));
}
