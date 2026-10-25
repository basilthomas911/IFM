#[test]
fn sidebar_short_time_matches_hybrid_including_both_sides_of_the_dst_fold() {
    let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("tests/fixtures/backend/sidebar-time-display.json");
    let cases: serde_json::Value = serde_json::from_slice(&std::fs::read(path).unwrap()).unwrap();
    for case in cases.as_array().unwrap() {
        let time = chrono::DateTime::parse_from_rfc3339(case["time"].as_str().unwrap())
            .unwrap()
            .to_utc();
        assert_eq!(
            ifm_ui_egui::state::sidebar_calendar::display_time(time),
            case["expected"]
        );
    }
}
