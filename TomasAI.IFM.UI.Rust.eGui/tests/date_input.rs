#[test]
fn date_inputs_fit_backend_dateonly_without_a_clock_fallback() {
    use ifm_ui_egui::backend::date_input::parse;
    for text in ["0001-01-01", "9999-12-31", "2024-02-29"] {
        assert_eq!(parse(text).unwrap().to_string(), text);
    }
    for text in [
        "0000-01-01",
        "+10000-01-01",
        "-0001-01-01",
        "2026-02-29",
        "",
        "today",
    ] {
        assert!(parse(text).is_err(), "{text}");
    }
}
