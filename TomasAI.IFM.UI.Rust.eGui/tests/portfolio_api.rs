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

#[test]
fn typed_fund_selection_preserves_utf16_order_versions_and_state_filter() {
    use ifm_ui_egui::{
        backend::schema::Registry,
        services::parameters::overlay_type,
        state::portfolio_data::{PortfolioData, kind},
    };
    use std::collections::BTreeMap;
    let registry = Registry::compiled().unwrap();
    let original =
        codec::decode(include_bytes!("fixtures/backend/portfolio-fund.msgpack")).unwrap();
    let kind = kind("FundMandateReadModel");
    let rows = [
        "\u{c4}",
        "\u{df}",
        "ss",
        "\u{1f600}",
        "\u{e000}",
        "alpha",
        "ALPHA",
    ]
    .into_iter()
    .enumerate()
    .map(|(index, name)| {
        overlay_type(
            &registry,
            &kind,
            &original,
            &BTreeMap::from([
                ("Name".into(), name.into()),
                ("FundMandateVersion".into(), ((index % 3 + 1) as i64).into()),
            ]),
        )
        .unwrap()
    })
    .collect::<Vec<_>>();
    let mut expected = rows
        .iter()
        .map(|row| (registry.project(&kind, row).unwrap(), row.clone()))
        .collect::<Vec<_>>();
    // Frozen previous comparison; managed labels use uppercase UTF-16 ordering.
    expected.sort_by(|(a, _), (b, _)| {
        a["Name"]
            .as_str()
            .unwrap()
            .to_uppercase()
            .encode_utf16()
            .cmp(b["Name"].as_str().unwrap().to_uppercase().encode_utf16())
            .then_with(|| {
                b["FundMandateVersion"]
                    .as_i64()
                    .cmp(&a["FundMandateVersion"].as_i64())
            })
    });
    let mut data = PortfolioData {
        funds: rows,
        fund: Some(original),
        ..Default::default()
    };
    assert_eq!(
        data.visible_funds(&registry).unwrap(),
        expected
            .into_iter()
            .map(|(_, wire)| wire)
            .collect::<Vec<_>>()
    );
    assert!(
        !data.is_current_fund(&registry).unwrap(),
        "a newer mandate must supersede the selected version"
    );
    data.fund_filter = Some("a different operating state".into());
    assert!(data.visible_funds(&registry).unwrap().is_empty());
}
