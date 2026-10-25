use ifm_ui_egui::{backend::codec, services::portfolio};
#[test]
fn portfolio_create_identity_matches_managed_idempotent_api() {
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");
    let body = codec::decode(&std::fs::read(root.join("portfolio-create-input.msgpack")).unwrap())
        .unwrap();
    let expected: serde_json::Value = serde_json::from_slice(
        &std::fs::read(root.join("portfolio-create-identity.json")).unwrap(),
    )
    .unwrap();
    let key = uuid::Uuid::parse_str(expected["key"].as_str().unwrap()).unwrap();
    assert_eq!(
        portfolio::create_command_id(key, &body)
            .unwrap()
            .to_string(),
        expected["commandId"]
    );
    assert!(portfolio::create_command_id(uuid::Uuid::nil(), &body).is_err());
    let context = ifm_ui_egui::services::business_context::BusinessContext {
        value_date: chrono::NaiveDate::from_ymd_opt(2026, 10, 26).unwrap(),
        active_value_date: None,
        as_of_utc: chrono::DateTime::parse_from_rfc3339("2026-10-26T12:30:00Z")
            .unwrap()
            .to_utc(),
        revision: 1,
        market_state: 0,
        allow_development_emulator: false,
    };
    let registry = ifm_ui_egui::backend::schema::Registry::compiled().unwrap();
    let command_id = uuid::Uuid::parse_str(expected["commandId"].as_str().unwrap()).unwrap();
    let prepared = ifm_ui_egui::services::portfolio_commands::prepare(
        &registry,
        &context,
        "CreatePortfolioCommand",
        1,
        None,
        command_id,
        key,
        std::collections::BTreeMap::from([
            ("Portfolio".into(), body.clone()),
            ("IdempotencyKey".into(), key.to_string().into()),
        ]),
    )
    .unwrap();
    assert_eq!(
        prepared.body,
        codec::decode(&std::fs::read(root.join("portfolio-create-normalized.msgpack")).unwrap())
            .unwrap()
    );
    for (name, file, wire) in [
        ("PortfolioReadModel", "portfolio-fields-display.json", body),
        (
            "FundMandateReadModel",
            "portfolio-fund-fields-display.json",
            codec::decode(&std::fs::read(root.join("portfolio-fund.msgpack")).unwrap()).unwrap(),
        ),
    ] {
        let fields: std::collections::BTreeMap<_, _> =
            ifm_ui_egui::services::strategy_display::fields(
                &registry,
                &format!("{}.ViewModels.{name}", portfolio::ROOT),
                &wire,
            )
            .unwrap()
            .into_iter()
            .collect();
        let expected: std::collections::BTreeMap<String, String> =
            serde_json::from_slice(&std::fs::read(root.join(file)).unwrap()).unwrap();
        assert_eq!(fields, expected, "{name} fields");
    }
}
