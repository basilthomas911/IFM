#[test]
fn backend_dates_use_the_same_windows_display_culture_as_managed_forms() {
    let rows: serde_json::Value = serde_json::from_slice(
        &std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend/financial-short-dates.json"),
        )
        .unwrap(),
    )
    .unwrap();
    for row in rows.as_array().unwrap() {
        let day =
            chrono::NaiveDate::parse_from_str(row["Date"].as_str().unwrap(), "%Y-%m-%d").unwrap();
        assert_eq!(
            ifm_ui_egui::backend::date_display::short(day).unwrap(),
            row["Display"].as_str().unwrap()
        );
    }
}
