use super::*;
use ifm_ui_egui::{backend::schema::Registry, services::option_chain::*};
async fn control(b: &Backend, verb: &str, value: Value) -> Value {
    service_result(
        &b.request(
            &ActorSubject::query("NativeCompatibility", verb, "none"),
            &value,
            None,
            None,
        )
        .await
        .unwrap(),
    )
    .unwrap()
    .clone()
}
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn option_chain_preview_uses_exact_draft_and_backend_economics() {
    use ifm_ui_egui::services::broker_preview::{Leg, Preview};
    let b = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap();
    let r = Registry::compiled().unwrap();
    let dec = |s: &str| Some(ExactDecimal::parse(s).unwrap());
    let legs = vec![Leg {
        contract: "ES-C".into(),
        side: -1,
        strike: dec("6050.0000000000000000000000001"),
        is_call: true,
        is_future: false,
        bid: dec("1.25"),
        ask: dec("1.5"),
        delta: Some(0.16),
        vega: Some(0.3),
        multiplier: dec("50"),
    }];
    let price = dec("-0.1234567890123456789012345678");
    let reference = dec("6050.25");
    let mut preview = Preview::default();
    control(&b, "OptionChainMode", "Normal".into()).await;
    let result = preview
        .load(
            &b,
            &r,
            &legs,
            3,
            price.as_ref(),
            reference.as_ref(),
            budget(),
        )
        .await
        .unwrap();
    let result=ifm_ui_egui::models::backend_document::request_document(&result).unwrap();
    assert_eq!(
        result["MaximumLoss"].as_str(),
        Some("987.6543210123456789012345678")
    );
    assert!(result["MaximumProfit"].is_null());
    assert_eq!(result["ResolvedTickIncrement"].as_str(), Some("0.05"));
    assert_eq!(result["NetDelta"], 0.125);
    for mode in ["WrongRevision", "Unavailable"] {
        control(&b, "OptionChainMode", mode.into()).await;
        assert!(
            preview
                .load(
                    &b,
                    &r,
                    &legs,
                    3,
                    price.as_ref(),
                    reference.as_ref(),
                    budget()
                )
                .await
                .is_err()
        );
    }
    let before = preview.revision();
    assert!(
        preview
            .load(
                &b,
                &r,
                &legs,
                0,
                price.as_ref(),
                reference.as_ref(),
                budget()
            )
            .await
            .is_err()
    );
    assert!(
        preview
            .load(
                &b,
                &r,
                &[legs[0].clone(), legs[0].clone()],
                3,
                price.as_ref(),
                reference.as_ref(),
                budget()
            )
            .await
            .is_err()
    );
    assert_eq!(preview.revision(), before);
    control(&b, "OptionChainMode", "Normal".into()).await;
}
fn spec() -> Specification {
    Specification {
        contract: "ESZ6".into(),
        symbol: "ES".into(),
        expiry: chrono::NaiveDate::from_ymd_opt(2026, 10, 28).unwrap(),
        roots: vec!["EW".into()],
        required: vec![],
        wing_width: ExactDecimal::parse("50.000000000000000000000000001").unwrap(),
        frozen: false,
    }
}
fn budget() -> tokio::time::Instant {
    tokio::time::Instant::now() + Duration::from_millis(400)
}
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn option_chain_scope_precision_and_subscription_ownership_survive_failures() {
    let b = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap();
    let r = Registry::compiled().unwrap();
    control(&b, "OptionChainMode", "Normal".into()).await;
    let date = chrono::NaiveDate::from_ymd_opt(2026, 10, 26).unwrap();
    let expiries = expirations(
        &b,
        &r,
        "ES",
        date,
        date + chrono::Duration::days(4),
        date,
        budget(),
    )
    .await
    .unwrap();
    assert_eq!(expiries.len(), 1);
    let mut a = Lease::default();
    let mut other = Lease::default();
    assert_ne!(a.owner(), other.owner());
    let snapshot = a.load(&b, &r, spec(), budget()).await.unwrap();
    let snapshot=ifm_ui_egui::models::backend_document::request_document(&snapshot).unwrap();
    assert_eq!(snapshot["Contracts"][0]["ContractId"], "ES-C");
    other.load(&b, &r, spec(), budget()).await.unwrap();
    assert_eq!(
        control(&b, "OptionChainCount", Value::Nil)
            .await
            .as_array()
            .unwrap()[0],
        2.into()
    );
    a.release(&b, &r, budget()).await.unwrap();
    assert!(!a.may_be_active());
    assert!(other.may_be_active());
    assert_eq!(
        control(&b, "OptionChainCount", Value::Nil)
            .await
            .as_array()
            .unwrap()[0],
        1.into()
    );
    for mode in ["Unavailable", "LostReply", "WrongContract", "Duplicate"] {
        control(&b, "OptionChainMode", mode.into()).await;
        assert!(a.load(&b, &r, spec(), budget()).await.is_err(), "{mode}");
        assert!(a.may_be_active());
        control(&b, "OptionChainMode", "ReleaseUnavailable".into()).await;
        assert!(a.release(&b, &r, budget()).await.is_err());
        assert!(a.may_be_active());
        control(&b, "OptionChainMode", "Normal".into()).await;
        a.release(&b, &r, budget()).await.unwrap();
        assert!(!a.may_be_active());
    }
    let mut frozen = spec();
    frozen.frozen = true;
    a.load(&b, &r, frozen, budget()).await.unwrap();
    // Switching frozen/live first releases only this owner's prior lease.
    a.load(&b, &r, spec(), budget()).await.unwrap();
    a.release(&b, &r, budget()).await.unwrap();
    other.release(&b, &r, budget()).await.unwrap();
    assert_eq!(
        control(&b, "OptionChainCount", Value::Nil)
            .await
            .as_array()
            .unwrap()[0],
        0.into()
    );
    control(&b, "OptionChainMode", "WrongExpirySymbol".into()).await;
    assert!(
        expirations(
            &b,
            &r,
            "ES",
            date,
            date + chrono::Duration::days(4),
            date,
            budget()
        )
        .await
        .is_err()
    );
    control(&b, "OptionChainMode", "Normal".into()).await;
}
