use ifm_ui_egui::{backend::health_http::HealthClient, state::operations_health::project};
use serde_json::json;
use std::{
    io::{Read, Write},
    net::TcpListener,
    time::Duration,
};

fn server(response: Vec<u8>, hold: Duration) -> (String, std::thread::JoinHandle<()>) {
    let listener = TcpListener::bind("127.0.0.1:0").unwrap();
    let endpoint = format!("http://{}/health", listener.local_addr().unwrap());
    let worker = std::thread::spawn(move || {
        let (mut stream, _) = listener.accept().unwrap();
        let mut request = [0; 4096];
        let _ = stream.read(&mut request);
        let _ = stream.write_all(&response);
        std::thread::sleep(hold);
    });
    (endpoint, worker)
}
fn response(status: &str, body: &str) -> Vec<u8> {
    format!("HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}",body.len()).into_bytes()
}

#[tokio::test]
async fn health_transport_enforces_status_size_json_and_single_deadline() {
    let client = HealthClient::new().unwrap();
    for (bytes,bound,expected) in [
        (response("200 OK","{\"revision\":42}"),1024,"success"),
        (response("503 Service Unavailable","{}"),1024,"HTTP 503"),
        (response("200 OK","not JSON"),1024,"invalid JSON"),
        (response("200 OK","{\"revision\":42}"),2,"bounded size"),
        (b"HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n10\r\n0123456789abcdef\r\n0\r\n\r\n".to_vec(),8,"bounded size"),
    ] {
        let(url,worker)=server(bytes,Duration::ZERO);let result=client.get(&url,bound,tokio::time::Instant::now()+Duration::from_secs(2)).await;
        if expected=="success"{assert_eq!(result.unwrap()["revision"],42);}else{assert!(result.err().unwrap().contains(expected));}
        worker.join().unwrap();
    }
    let (url, worker) = server(
        b"HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\n".to_vec(),
        Duration::from_millis(200),
    );
    let started = std::time::Instant::now();
    let error = client
        .get(
            &url,
            1024,
            tokio::time::Instant::now() + Duration::from_millis(50),
        )
        .await
        .err()
        .unwrap();
    assert!(error.contains("deadline"));
    assert!(started.elapsed() < Duration::from_millis(150));
    worker.join().unwrap();
}

#[test]
fn operations_projection_uses_api_time_and_reports_bad_or_stale_health_as_unknown() {
    let value = json!({"schemaVersion":1,"revision":42,"observedOnUtc":"2000-01-03T12:30:00Z","overallStatus":"Green","sessionState":"LiveTrading","valueDate":"2000-01-03","rejectedStaleGenerationPublications":0,
        "stages":[{"stage":"Options","status":"Green","reasonCode":"Healthy","reason":"Current","required":true,"pending":0,"capacity":32,"highWater":4,"received":20,"completed":20,"failed":0,"coalesced":5,"saturated":0,"marketDataAsOfUtc":"2000-01-03T12:29:59Z","p50Latency":"00:00:00.0010000","p95Latency":"00:00:00.0100000","p99Latency":"00:00:00.0200000"}],"datasets":[]});
    let now = chrono::DateTime::parse_from_rfc3339("2000-01-03T12:30:00Z")
        .unwrap()
        .to_utc();
    let result = project(&value, now).unwrap();
    assert_eq!(result.context["status"], "Green");
    assert!(result.context["summary"].contains("2000-01-03"));
    assert_eq!(result.rows[0].fields["SourceAge"], "1.0 s");
    assert_eq!(result.rows[0].fields["P50Latency"], "1.0 ms");
    assert!(
        project(&value, now + chrono::Duration::seconds(31))
            .err()
            .unwrap()
            .contains("stale")
    );
    assert!(
        project(&value, now - chrono::Duration::seconds(31))
            .err()
            .unwrap()
            .contains("clock")
    );
    let mut invalid = value;
    invalid["stages"][0]["status"] = "InventedGreen".into();
    assert!(project(&invalid, now).is_err());
}
