//! Isolated broker checks: no live feed or production actor is touched.
use futures_util::StreamExt;
use ifm_ui_egui::backend::{
    codec,
    contracts::ActorSubject,
    transport::{Backend, Settings},
};
use rmpv::Value;
use std::time::Duration;

#[tokio::test]
#[ignore = "Set IFM_STANDALONE_TEST_NATS_URL to an isolated Core NATS broker"]
async fn failures_are_durable_correlated_and_success_still_preserves_headers() {
    let url = std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();
    let backend = Backend::connect(Settings {
        url: url.clone(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(5),
    })
    .await
    .unwrap();
    let actor = format!("ErrorAudit{}", uuid::Uuid::new_v4().simple());
    let subject = ActorSubject::query(&actor, "Read", "test");
    let route = subject.route().unwrap();
    let absent = backend
        .request(&subject, &Value::Nil, None, None)
        .await
        .unwrap_err();
    assert!(absent.contains(&route));
    assert!(absent.contains("operationId="));
    let raw = async_nats::connect(url).await.unwrap();
    let mut subscription = raw.subscribe(route.clone()).await.unwrap();
    raw.flush().await.unwrap();
    // Flush writes locally; a same-connection round trip proves that the broker
    // has processed SUB before another connection starts the success request.
    raw.publish(route.clone(),"subscription-ready".into()).await.unwrap();
    assert_eq!(subscription.next().await.unwrap().payload.as_ref(),b"subscription-ready");
    let responder = tokio::spawn(async move {
        let request = subscription.next().await.unwrap();
        let headers = request.headers.unwrap();
        let operation = headers.get("IFM-UI-Operation-Id").unwrap().as_str();
        uuid::Uuid::parse_str(operation).unwrap();
        assert_eq!(
            headers.get("traceparent").unwrap().as_str(),
            "00-test-trace"
        );
        raw.publish(
            request.reply.unwrap(),
            codec::encode(&Value::from("ok")).unwrap().into(),
        )
        .await
        .unwrap();
        raw.flush().await.unwrap();
        // A malformed reply must also log the failed route and correlation.
        let request = subscription.next().await.unwrap();
        raw.publish(request.reply.unwrap(), vec![0x91].into())
            .await
            .unwrap();
        raw.flush().await.unwrap();
        let request = subscription.next().await.unwrap();
        raw.publish(
            request.reply.unwrap(),
            codec::encode(&Value::Array(vec![
                Value::from(false),
                Value::from(503),
                Value::from("feed unavailable"),
                Value::Nil,
            ]))
            .unwrap()
            .into(),
        )
        .await
        .unwrap();
        raw.flush().await.unwrap();
        // Keep the subscriber alive; this forces the caller deadline, not NoResponders.
        let _ = subscription.next().await;
        tokio::time::sleep(Duration::from_secs(1)).await;
    });
    assert_eq!(
        backend
            .request(&subject, &Value::Nil, None, Some(("00-test-trace", "")))
            .await
            .unwrap(),
        Value::from("ok")
    );
    let invalid = backend
        .request(&subject, &Value::Nil, None, None)
        .await
        .unwrap_err();
    assert!(invalid.contains(&route));
    let rejected = backend
        .request(&subject, &Value::Nil, None, None)
        .await
        .unwrap();
    assert!(
        ifm_ui_egui::backend::contracts::service_result(&rejected)
            .unwrap_err()
            .contains("feed unavailable")
    );
    let expired = backend
        .request(&subject, &Value::Nil, Some(Duration::from_millis(50)), None)
        .await
        .unwrap_err();
    assert!(expired.contains("deadline expired"));
    responder.abort();
    let path = std::path::PathBuf::from(std::env::var_os("LOCALAPPDATA").unwrap())
        .join("TomasAI/IFM/UI.Rust.eGui/Logs/ui-errors.jsonl");
    let records: Vec<serde_json::Value> = std::fs::read_to_string(path)
        .unwrap()
        .lines()
        .map(|line| serde_json::from_str(line).unwrap())
        .collect();
    let failures: Vec<_> = records
        .iter()
        .filter(|v| v["event"] == "backend.request_failed" && v["requestSubject"] == route)
        .collect();
    assert_eq!(
        failures.len(),
        3,
        "successful requests must not create error records"
    );
    for record in &failures {
        chrono::DateTime::parse_from_rfc3339(record["timestampUtc"].as_str().unwrap()).unwrap();
        uuid::Uuid::parse_str(record["operationId"].as_str().unwrap()).unwrap();
        assert!(record["details"].as_str().is_some_and(|v| !v.is_empty()));
    }
    assert!(failures.iter().any(|record| {
        record["details"]
            .as_str()
            .unwrap()
            .to_lowercase()
            .contains("no responders")
    }));
    assert_ne!(failures[0]["operationId"], failures[1]["operationId"]);
    assert!(records.iter().any(|record| {
        record["event"] == "backend.request_rejected"
            && record["requestSubject"] == route
            && record["details"]
                .as_str()
                .unwrap()
                .contains("feed unavailable")
    }));
}
