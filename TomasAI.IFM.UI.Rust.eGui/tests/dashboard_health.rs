use ifm_ui_egui::services::dashboard_health::*;
use serde_json::json;


/// Verifies that pipeline classification uses api clock and keeps required failure reason.
#[test]
fn pipeline_classification_uses_api_clock_and_keeps_required_failure_reason() {
    let now = chrono::DateTime::parse_from_rfc3339("2026-10-26T12:30:00Z")
        .unwrap()
        .to_utc();
    let value = json!({"observedUtc":"2026-10-26T12:29:00Z","status":"Healthy","checks":[{"required":false,"status":"Unhealthy","reason":"Optional UI"},{"required":true,"status":"Unknown","reason":"No current option quote"}]});
    assert_eq!(
        project(&value, Some(true), false, now).unwrap().health,
        "Healthy"
    );
    assert_eq!(
        project(&value, Some(true), true, now).unwrap().health,
        "OffHoursActive"
    );
    assert_eq!(
        project(&value, Some(false), false, now).unwrap().health,
        "Intermittent"
    );
    assert_eq!(
        project(&value, Some(true), false, now).unwrap().reason,
        "No current option quote"
    );
    let mut critical = value.clone();
    critical["status"] = "Unhealthy".into();
    assert_eq!(
        project(&critical, Some(true), false, now).unwrap().health,
        "Critical"
    );
    let mut stale = value.clone();
    stale["observedUtc"] = "2026-10-26T12:27:59Z".into();
    assert_eq!(
        project(&stale, Some(true), false, now).unwrap().health,
        "Unknown"
    );
    assert_eq!(
        project(&stale, Some(false), false, now).unwrap().health,
        "Inactive"
    );
}


/// Verifies that malformed health cannot be presented as green.
#[test]
fn malformed_health_cannot_be_presented_as_green() {
    let now = chrono::DateTime::parse_from_rfc3339("2026-10-26T12:30:00Z")
        .unwrap()
        .to_utc();
    for value in [
        json!({}),
        json!({"observedUtc":"invalid"}),
        json!({"observedUtc":"2026-10-26T12:30:00Z","status":"Healthy"}),
    ] {
        assert!(project(&value, Some(true), false, now).is_err());
    }
    assert_eq!(PipelineHealth::unavailable("HTTP 500").health, "Unknown");
}


/// Verifies that options require current generation and all readiness proofs.
#[test]
fn options_require_current_generation_and_all_readiness_proofs() {
    let mut v = json!({"datasets":[{"running":true,"healthy":true,"lifecycle":"Healthy","generationId":"current"}],"workload":{"generationId":"current","replacementPending":false,"nativeResourceProofsComplete":true,"scopes":["monitor-a","monitor-b"],"readyScopes":["monitor-a","monitor-b"]}});
    assert_eq!(options_health(&v).0, "Green");
    v["workload"]["replacementPending"] = true.into();
    assert_eq!(options_health(&v).0, "Yellow");
    v["workload"]["replacementPending"] = false.into();
    v["workload"]["generationId"] = "old".into();
    assert_eq!(options_health(&v).0, "Yellow");
    v["workload"]["generationId"] = "current".into();
    v["workload"]["readyScopes"] = json!(["monitor-a"]);
    assert_eq!(options_health(&v).0, "Yellow");
    v["datasets"][0]["lifecycle"] = "StoppedError".into();
    assert_eq!(options_health(&v).0, "Red");
    v["datasets"][0]["lifecycle"] = "Stopped".into();
    v["datasets"][0]["running"] = false.into();
    assert_eq!(options_health(&v).0, "Inactive");
    assert_eq!(options_health(&json!({})).0, "Yellow");
    assert_eq!(options_health(&json!({"datasets":[]})).0, "Inactive");
}


/// Verifies that health items sort red yellow green inactive.
#[test]
fn health_items_sort_red_yellow_green_inactive() {
    use ifm_ui_egui::operations::operations_health::severity_rank;
    let mut states = ["Inactive", "Green", "Yellow", "Red", "Resetting"];
    states.sort_by_key(|s| severity_rank(s));
    assert_eq!(states, ["Red", "Yellow", "Resetting", "Green", "Inactive"]);
}


/// A silent off-hours feed remains green when current subscriptions are ready.
#[test]
fn quiet_options_feed_is_green_without_quotes_or_valuations() {
    let value = json!({
        "datasets": [{"running": true, "healthy": true, "lifecycle": "Healthy", "generationId": "quiet-current"}],
        "workload": {"generationId": "quiet-current", "replacementPending": false,
            "nativeResourceProofsComplete": true, "scopes": ["monitor"], "readyScopes": ["monitor"]},
        "optionObservations": [], "optionChains": []
    });
    assert_eq!(options_health(&value).0, "Green");
}
