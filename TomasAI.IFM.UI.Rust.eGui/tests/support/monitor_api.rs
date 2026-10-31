use super::*;
use ifm_ui_egui::{
    backend::schema::Registry,
    services::{
        business_context::BusinessContext,
        trade_feed_recovery::{Action, Client},
        trade_monitor_queries as api,
    },
};
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
async fn monitor_queries_and_feed_intent_use_exact_identity_api_dates_and_existing_host_contract() {
    let b = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap();
    let r = Registry::compiled().unwrap();
    let context = BusinessContext::load(&b).await.unwrap();
    let scope = api::Scope {
        portfolio: 1,
        fund: 2,
        order: 701,
        trade: 456,
    };
    let deadline = || tokio::time::Instant::now() + Duration::from_secs(2);
    control(&b, "MonitorMode", "Normal".into()).await;
    let trade = api::established(&b, &r, scope, "IronCondor", deadline())
        .await
        .unwrap();
    assert_eq!(trade.id.as_ref().unwrap().order_id, Some(701));
    assert_eq!(
        api::positions(&b, &r, scope, &context, deadline())
            .await
            .unwrap()
            .len(),
        1
    );
    assert_eq!(
        api::limits(&b, &r, scope, deadline())
            .await
            .unwrap()
            .max_loss
            .unwrap()
            .as_str(),
        "1800"
    );
    assert_eq!(
        api::realtime(&b, &r, scope, context.value_date, deadline())
            .await
            .unwrap()
            .unwrap()
            .snapshot_revision,
        Some(10)
    );
    assert_eq!(
        api::current_plan(&b, &r, scope, context.value_date, deadline())
            .await
            .unwrap()
            .unwrap()
            .plan_revision,
        Some(7)
    );
    let page = api::plan_page(&b, &r, scope, context.value_date, 0, 100, None, deadline())
        .await
        .unwrap();
    assert_eq!(page.maximum_revision, Some(7));
    assert!(
        api::plan_page(
            &b,
            &r,
            scope,
            context.value_date,
            0,
            100,
            Some(7),
            deadline()
        )
        .await
        .is_ok()
    );
    let chart = api::chart(&b, &r, scope, context.value_date, deadline())
        .await
        .unwrap();
    assert_eq!(chart.points[0].net_spread, "-15");
    let client = Client::new(b.clone(), "nativecompat").unwrap();
    assert!(
        !client
            .invoke(
                Action::Load,
                scope,
                &context,
                uuid::Uuid::new_v4(),
                deadline()
            )
            .await
            .unwrap()
    );
    assert!(
        client
            .invoke(
                Action::Enable,
                scope,
                &context,
                uuid::Uuid::new_v4(),
                deadline()
            )
            .await
            .unwrap()
    );
    assert!(
        client
            .invoke(
                Action::Load,
                scope,
                &context,
                uuid::Uuid::new_v4(),
                deadline()
            )
            .await
            .unwrap()
    );
    assert!(
        client
            .invoke(
                Action::Disable,
                scope,
                &context,
                uuid::Uuid::new_v4(),
                deadline()
            )
            .await
            .unwrap()
    );
    assert!(
        !client
            .invoke(
                Action::Load,
                scope,
                &context,
                uuid::Uuid::new_v4(),
                deadline()
            )
            .await
            .unwrap()
    );
    control(&b, "MonitorMode", "WrongScope".into()).await;
    assert!(
        api::established(&b, &r, scope, "IronCondor", deadline())
            .await
            .is_err()
    );
    control(&b, "MonitorMode", "WrongCut".into()).await;
    assert!(
        api::plan_page(
            &b,
            &r,
            scope,
            context.value_date,
            0,
            100,
            Some(7),
            deadline()
        )
        .await
        .is_err()
    );
    control(&b, "MonitorMode", "HostReject".into()).await;
    assert!(
        client
            .invoke(
                Action::Enable,
                scope,
                &context,
                uuid::Uuid::new_v4(),
                deadline()
            )
            .await
            .unwrap_err()
            .contains("Fixture recovery rejected")
    );
    control(&b, "MonitorMode", "HostTimeout".into()).await;
    assert!(
        client
            .invoke(
                Action::Enable,
                scope,
                &context,
                uuid::Uuid::new_v4(),
                tokio::time::Instant::now() + Duration::from_millis(100)
            )
            .await
            .is_err()
    );
    assert_eq!(
        control(&b, "MonitorHostCount", Value::Nil)
            .await
            .as_array()
            .unwrap()[0],
        Value::from(1)
    );
    assert!(Client::new(b.clone(), "bad.scope").is_err());
    control(&b, "MonitorMode", "Disabled".into()).await;
}
