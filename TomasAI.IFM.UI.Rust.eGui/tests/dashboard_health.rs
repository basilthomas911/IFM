use ifm_ui_egui::services::dashboard_health::*;
use serde_json::json;
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
