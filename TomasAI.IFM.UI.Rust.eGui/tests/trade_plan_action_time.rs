use ifm_ui_egui::services::monitor_display::action_time;
use serde_json::json;

#[test]
fn action_time_matches_console_with_eastern_daylight_and_standard_time() {
    for (utc, expected) in [
        ("2026-10-30T13:38:42.123Z", "09:38:42.123 AM"),
        ("2026-12-01T18:38:42.123Z", "01:38:42.123 PM"),
        ("2026-12-01T05:00:00Z", "12:00:00.000 AM"),
        ("2026-12-01T17:00:00Z", "12:00:00.000 PM"),
    ] {
        let plan = json!({"IronCondorTradePlanSnapshot":{"ActionDateTime":utc}});
        assert_eq!(action_time(&plan).unwrap(), expected);
    }
}

#[test]
fn fallback_calculation_time_uses_the_same_time_only_format() {
    let plan = json!({"CalculatedAtUtc":"2026-10-30T17:38:42.123Z"});
    assert_eq!(action_time(&plan).unwrap(), "01:38:42.123 PM");
}

#[test]
fn absent_invalid_and_default_times_do_not_display_a_fabricated_time() {
    assert!(action_time(&json!({})).is_err());
    assert!(action_time(&json!({"CalculatedAtUtc":"invalid"})).is_err());
    assert_eq!(action_time(&json!({"CalculatedAtUtc":"0001-01-01T00:00:00Z"})).unwrap(), "");
}
