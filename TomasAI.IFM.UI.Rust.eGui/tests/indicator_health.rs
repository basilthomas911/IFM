use ifm_ui_egui::{contract_editor::ContractResult, state::indicator_health::{self, IndicatorAudit}};
use serde_json::json;

/// Qualified values retain their original provenance in the existing stage/detail projection.
#[test]
fn active_indicators_are_visible_with_source_time_interval_and_reason() {
    let now = chrono::DateTime::parse_from_rfc3339("2026-11-09T12:00:00Z").unwrap().to_utc();
    let audit: IndicatorAudit = serde_json::from_value(json!({"state":"Failed","auditedAtUtc":now.to_rfc3339(),"entries":[
        {"contractId":"ES20261218","timeFrame":"FourHours","indicator":"ATR14","warm":true,"valid":true,"sourceAsOfUtc":"2026-11-09T10:59:59Z","expectedAsOfUtc":"2026-11-09T07:00:00Z","reason":"","ready":true},
        {"contractId":"VX20261118","timeFrame":"FourHours","indicator":"Bollinger20","warm":false,"valid":true,"sourceAsOfUtc":"2026-11-09T06:59:59Z","expectedAsOfUtc":null,"reason":"Seed unavailable","ready":false}
    ]})).unwrap();
    let mut result = ContractResult::default();
    indicator_health::append(&mut result, audit, now).unwrap();
    assert_eq!(result.rows.len(), 2);
    assert_eq!(result.rows[0].fields["Status"], "Green");
    assert_eq!(result.rows[1].fields["Status"], "Red");
    assert_eq!(result.rows[1].fields["Reason"], "Seed unavailable");
    assert_eq!(result.rows[0].fields["MarketDataAsOfUtc"], "2026-11-09T10:59:59Z");
    assert_eq!(result.context["status"], "Red");
}


/// A stale audit cannot leave old green rows on screen, while a closed session stays inactive.
#[test]
fn stale_audit_is_unavailable_and_scheduled_stop_does_not_wait_for_market_ticks() {
    let now = chrono::DateTime::parse_from_rfc3339("2026-11-09T12:00:00Z").unwrap().to_utc();
    let stale: IndicatorAudit = serde_json::from_value(json!({"state":"Warm","auditedAtUtc":"2026-11-09T11:00:00Z","entries":[]})).unwrap();
    assert!(indicator_health::append(&mut ContractResult::default(), stale, now).is_err());
    let stopped: IndicatorAudit = serde_json::from_value(json!({"state":"ScheduledStopped","auditedAtUtc":null,"entries":[]})).unwrap();
    assert!(indicator_health::append(&mut ContractResult::default(), stopped, now).is_ok());
}


/// Explicit read-only Development acceptance uses the same typed transport and projection as Operations.
#[tokio::test]
#[ignore = "Requires IFM_INDICATOR_ACCEPTANCE_URL for an explicitly selected running API"]
async fn live_operations_matrix_preserves_all_qualified_indicator_rows() {
    use ifm_ui_egui::backend::health_http::HealthClient;
    use ifm_ui_egui::state::indicator_health::IndicatorReadiness;
    let endpoint = std::env::var("IFM_INDICATOR_ACCEPTANCE_URL").expect("Explicit API readiness URL");
    let response = HealthClient::new().unwrap().get_readiness_observation::<IndicatorReadiness>(
        &endpoint, 1024 * 1024, tokio::time::Instant::now() + std::time::Duration::from_secs(5)
    ).await.unwrap();
    let audit = response.value.entries.market_data_indicators.unwrap().data;
    assert_eq!(audit.state, "Warm");
    let expected = audit.entries.len();
    assert!(expected > 0);
    let mut result = ContractResult::default();
    indicator_health::append(&mut result, audit, response.server_utc.expect("API operational clock")).unwrap();
    assert_eq!(result.rows.len(), expected);
    assert!(result.rows.iter().all(|row| row.fields["Status"] == "Green"));
    for horizon in ["Daily", "Weekly", "Monthly"] {
        assert!(result.rows.iter().any(|row| row.fields["TimeFrame"] == horizon
            && row.fields["Indicator"] == "RegimeDiscovery inputs"));
    }
    println!("Live Operations matrix: {expected} qualified rows; Daily, Weekly and Monthly consumer snapshots ready.");
}
