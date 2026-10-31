use ifm_ui_egui::{
    models::{
        cells::Cells,
        eod::{EodDocument, EodInputs},
        wire_types::DecimalText,
    },
    services::{trade_eod::Session, trade_monitor_queries::Scope},
    wire,
};
#[path = "support/eod_baseline.rs"]
mod baseline;
#[test]
fn typed_eod_document_preserves_exact_prices_volume_presence_and_lifecycle_eligibility() {
    let scope = Scope {
        portfolio: 1,
        fund: 2,
        order: 3,
        trade: 4,
    };
    let date = chrono::NaiveDate::from_ymd_opt(2026, 10, 26).unwrap();
    let inputs = EodInputs {
        open_price: Some(DecimalText("6000.1250".into())),
        high_price: Some(DecimalText("6010.5000".into())),
        low_price: None,
        close_price: Some(DecimalText("6005.0000".into())),
        volume: Some(i64::MAX),
        trade_pnl: Some(DecimalText("-50.2500".into())),
        fund_balance: Some(DecimalText("123406.5300".into())),
        last_status_message: "Backend amounts".into(),
    };
    for (loaded, pending, completed) in [
        (false, false, false),
        (true, false, false),
        (true, true, false),
        (true, false, true),
    ] {
        let mut session = Session::new(scope, "IronCondor".into(), "ESZ6".into(), date).unwrap();
        session.display = loaded.then(|| inputs.clone());
        session.reference = "Exact reference".into();
        session.pending = pending;
        session.completed = completed;
        let before = baseline::Session {
            scope,
            date,
            reference: session.reference.clone(),
            display: if loaded {
                serde_json::to_value(&inputs).unwrap()
            } else {
                serde_json::Value::Null
            },
            pending,
            completed,
        }
        .document();
        let document = session.document();
        assert_eq!(serde_json::to_value(&document).unwrap(), before);
        let decoded: EodDocument = wire::decode(&wire::encode(&document).unwrap()).unwrap();
        assert_eq!(decoded.can_run, loaded && !pending && !completed);
        if loaded {
            assert_eq!(decoded.cell("openPrice").as_str(), Some("6000.1250"));
            assert_eq!(decoded.cell("volume").as_i64(), Some(i64::MAX));
            assert_eq!(decoded.cell("fundBalance").as_str(), Some("123406.5300"));
        } else {
            assert!(decoded.cell("openPrice").is_missing());
        }
    }
}
