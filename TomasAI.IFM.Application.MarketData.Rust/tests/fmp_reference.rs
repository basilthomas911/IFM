//! Localhost reference parity and failure qualification; no provider credential
//! or market connection is used. Socket lifetime is owned by every test fixture.
use ifm_market_data_native::{
    dns::Resolver,
    reference::{
        decimal::parse_json,
        fmp::{Fmp, Options},
    },
    runtime::{Deadline, clock::Clock},
};
use std::{
    collections::VecDeque,
    sync::{
        Arc, Mutex,
        atomic::{AtomicUsize, Ordering},
    },
    time::Duration,
};
use time::{Date, Month};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::TcpListener,
    task::JoinSet,
};
use tokio_util::sync::CancellationToken;
const DAY: i64 = 17_929_728_000_000_000;
fn day(n: u8) -> Date {
    Date::from_calendar_date(2026, Month::October, n).unwrap()
}
fn budget() -> Deadline {
    Deadline::after(Duration::from_secs(3))
}
fn treasury(date: &str, rate: &str) -> String {
    format!(
        r#"{{"date":"{date}","month1":{rate},"month2":{rate},"month3":{rate},"month6":{rate},"year1":{rate},"year2":{rate},"year3":{rate},"year5":{rate},"year7":{rate},"year10":{rate},"year20":{rate},"year30":{rate}}}"#
    )
}
struct Response {
    status: u16,
    body: String,
    headers: String,
    head_delay: Duration,
    body_delay: Duration,
}
impl Response {
    fn json(body: impl Into<String>) -> Self {
        Self {
            status: 200,
            body: body.into(),
            headers: String::new(),
            head_delay: Duration::ZERO,
            body_delay: Duration::ZERO,
        }
    }
}
struct Server {
    url: String,
    requests: Arc<Mutex<Vec<String>>>,
    active: Arc<AtomicUsize>,
    stop: CancellationToken,
    task: tokio::task::JoinHandle<()>,
}
struct Active(Arc<AtomicUsize>);
impl Drop for Active {
    fn drop(&mut self) {
        self.0.fetch_sub(1, Ordering::SeqCst);
    }
}
impl Server {
    async fn new(responses: Vec<Response>) -> Self {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let url = format!("http://{}/stable/", listener.local_addr().unwrap());
        let requests = Arc::new(Mutex::new(Vec::new()));
        let active = Arc::new(AtomicUsize::new(0));
        let stop = CancellationToken::new();
        let token = stop.clone();
        let seen = requests.clone();
        let count = active.clone();
        let responses = Arc::new(Mutex::new(VecDeque::from(responses)));
        let task = tokio::spawn(async move {
            let mut peers = JoinSet::new();
            loop {
                tokio::select! {biased;()=token.cancelled()=>break,accepted=listener.accept()=>{
                    let (mut socket,_)=accepted.unwrap();let seen=seen.clone();let count=count.clone();let responses=responses.clone();
                    peers.spawn(async move {count.fetch_add(1,Ordering::SeqCst);let _active=Active(count);let mut request=Vec::new();let mut bytes=[0u8;1024];
                        loop {let n=socket.read(&mut bytes).await.unwrap_or(0);if n==0{return;}request.extend_from_slice(&bytes[..n]);if request.windows(4).any(|w|w==b"\r\n\r\n"){break;}assert!(request.len()<16384);}
                        let request=String::from_utf8(request).unwrap();assert!(request.to_ascii_lowercase().contains("apikey: fixture-secret"));assert!(!request.to_ascii_lowercase().contains("authorization:"));assert!(!request.lines().next().unwrap().contains("fixture-secret"));seen.lock().unwrap().push(request);
                        let response=responses.lock().unwrap().pop_front().unwrap_or_else(||Response::json("[]"));
                        tokio::time::sleep(response.head_delay).await;
                        let header=format!("HTTP/1.1 {} Test\r\nContent-Length: {}\r\nConnection: close\r\n{}\r\n",response.status,response.body.len(),response.headers);
                        if socket.write_all(header.as_bytes()).await.is_err(){return;}
                        tokio::time::sleep(response.body_delay).await;let _=socket.write_all(response.body.as_bytes()).await;
                    });
                },Some(result)=peers.join_next(),if !peers.is_empty()=>{result.unwrap();}}
            }
            peers.abort_all();
            while peers.join_next().await.is_some() {}
        });
        Self {
            url,
            requests,
            active,
            stop,
            task,
        }
    }
    fn client(&self, configure: impl FnOnce(&mut Options)) -> (Fmp, Arc<Clock>) {
        let mut options = Options {
            base: self.url.clone(),
            initial_delay: Duration::ZERO,
            maximum_delay: Duration::ZERO,
            ..Options::default()
        };
        configure(&mut options);
        let clock = Arc::new(Clock::new(DAY, false).unwrap());
        let dns = Arc::new(Resolver::new(vec!["127.0.0.1:9".parse().unwrap()]).unwrap());
        (
            Fmp::new(options, "fixture-secret".into(), dns, clock.clone()).unwrap(),
            clock,
        )
    }
    async fn finish(self) {
        self.stop.cancel();
        self.task.await.unwrap();
        assert_eq!(self.active.load(Ordering::SeqCst), 0);
    }
}
#[test]
fn decimal_exponents_do_not_allocate_from_wire_exponent() {
    for (input, plain) in [
        ("4.250e0", "4.250"),
        ("425e-2", "4.25"),
        ("4.25e2", "425"),
        ("-0e-100000000000000000000", "-0"),
        ("1e-100000000000000000000", "0.0000000000000000000000000000"),
        (
            "79228162514264337593543950335e0",
            "79228162514264337593543950335",
        ),
    ] {
        assert_eq!(
            parse_json(input),
            if plain.contains('e') {
                parse_json(plain)
            } else {
                ifm_market_data_native::reference::decimal::parse(plain)
            },
            "{input}"
        );
    }
    for input in [
        "1e100000000000000000000",
        "+1",
        "01",
        "1.",
        "NaN",
        "null",
        "1e",
        "1e+",
        " 1",
        "1 ",
        r#""4""#,
    ] {
        assert!(parse_json(input).is_none(), "{input}");
    }
}
#[tokio::test]
async fn treasury_sort_decimal_duplicate_and_case_insensitive_last_property() {
    let a = treasury("2026-10-25", "4.250e0");
    let b = treasury("2026-10-26", "5").replace("\"month1\":5", "\"MONTH1\":9,\"month1\":5");
    let server = Server::new(vec![Response::json(format!(
        "[{b},{a},{}]",
        treasury("2026-10-25", "4.25")
    ))])
    .await;
    let (client, _) = server.client(|_| {});
    let result = client
        .treasury_range(day(25), day(26), &CancellationToken::new(), budget())
        .await
        .unwrap();
    assert_eq!(result.len(), 2);
    assert_eq!(result[0].date, day(25));
    assert_eq!(result[0].retrieved, DAY);
    assert_eq!(result[0].rates[0].percentage.bits, [4250, 0, 0, 3 << 16]);
    assert_eq!(result[1].rates[0].percentage.bits, [5, 0, 0, 0]);
    server.finish().await;
}
#[tokio::test]
async fn treasury_chunks_use_inclusive_ranges_and_latest_lookback() {
    let server = Server::new(vec![
        Response::json("[]"),
        Response::json(format!("[{}]", treasury("2026-10-26", "1"))),
    ])
    .await;
    let (client, _) = server.client(|o| {
        o.window_days = 2;
        o.lookback_days = 3;
    });
    let result = client
        .treasury_latest(day(26), &CancellationToken::new(), budget())
        .await
        .unwrap()
        .unwrap();
    assert_eq!(result.date, day(26));
    let requests = server.requests.lock().unwrap().clone();
    assert!(requests[0].starts_with("GET /stable/treasury-rates?from=2026-10-24&to=2026-10-25 "));
    assert!(requests[1].starts_with("GET /stable/treasury-rates?from=2026-10-26&to=2026-10-26 "));
    server.finish().await;
}
#[tokio::test]
async fn treasury_invalid_rows_never_return_partial_data() {
    for bad in [
        "null".to_owned(),
        "{}".into(),
        "[null]".into(),
        "[{\"date\":\"2026-10-26\"}]".into(),
        format!("[{}]", treasury("2026-10-27", "1")),
        format!(
            "[{},{}]",
            treasury("2026-10-26", "1"),
            treasury("2026-10-26", "2")
        ),
        format!("[{}]", treasury("2026-10-26", "null")),
        format!("[{}]", treasury("2026-10-26", "1e100000000")),
        "[] garbage".into(),
    ] {
        let server = Server::new(vec![Response::json(bad.clone())]).await;
        let (client, _) = server.client(|_| {});
        let error = client
            .treasury_range(day(26), day(26), &CancellationToken::new(), budget())
            .await
            .unwrap_err();
        assert_eq!(error.code, "Contract", "{bad}");
        server.finish().await;
    }
}
#[tokio::test]
async fn calendar_preserves_scalar_lexemes_and_utc_order_and_filters() {
    let body = r#"[{"date":"2026-10-26 13:30:00","country":" us ","event":" GDP ","actual":1.230e+2,"estimate":true,"previous":" 1.0 ","impact":null,"changePercentage":-0.00},{"date":"2026-10-26T08:00:00-04:00","country":"CA","event":"CPI"},{"date":"2026-10-26T00:00:00Z","country":"GB","event":"Rate"}]"#;
    let server = Server::new(vec![Response::json(body)]).await;
    let (client, _) = server.client(|_| {});
    let result = client
        .calendar(
            day(26),
            day(26),
            &[" us ".into(), "CA".into()],
            &CancellationToken::new(),
            budget(),
        )
        .await
        .unwrap();
    assert_eq!(result.len(), 2);
    assert_eq!(result[0].country, "CA");
    let row = &result[1];
    assert_eq!(row.name, "GDP");
    assert_eq!(
        row.values,
        [
            Some("1.230e+2".into()),
            Some("true".into()),
            Some(" 1.0 ".into()),
            None,
            None,
            None,
            Some("-0.00".into())
        ]
    );
    assert_eq!(row.retrieved, DAY);
    server.finish().await;
}
#[tokio::test]
async fn calendar_conflicts_complex_scalars_and_out_of_range_filtered_rows_are_errors() {
    for body in [
        r#"[{"date":"2026-10-26","country":"US","event":"X","actual":{}}]"#,
        r#"[{"date":"2026-10-26","country":"US","event":"X","actual":[]}]"#,
        r#"[{"date":"2026-10-27","country":"GB","event":"X"}]"#,
        r#"[{"date":"2026-10-26","country":"US","event":"X"},{"date":"2026-10-26","country":"US","event":"X","actual":0}]"#,
        r#"[{"date":"invalid","country":"US","event":"X"}]"#,
    ] {
        let server = Server::new(vec![Response::json(body)]).await;
        let (client, _) = server.client(|_| {});
        assert_eq!(
            client
                .calendar(
                    day(26),
                    day(26),
                    &["US".into()],
                    &CancellationToken::new(),
                    budget()
                )
                .await
                .unwrap_err()
                .code,
            "Contract"
        );
        server.finish().await;
    }
}
#[tokio::test]
async fn validation_happens_before_network() {
    let server = Server::new(vec![]).await;
    let (client, _) = server.client(|_| {});
    assert_eq!(
        client
            .treasury_range(day(26), day(25), &CancellationToken::new(), budget())
            .await
            .unwrap_err()
            .code,
        "Validation"
    );
    assert_eq!(
        client
            .calendar(
                day(26),
                day(26),
                &["US1".into()],
                &CancellationToken::new(),
                budget()
            )
            .await
            .unwrap_err()
            .code,
        "Validation"
    );
    assert!(server.requests.lock().unwrap().is_empty());
    server.finish().await;
}
#[tokio::test]
async fn http_classes_and_bounded_retries() {
    for status in [401, 403, 404, 408, 429, 500, 502, 503, 504] {
        let mut responses = Vec::new();
        for _ in 0..3 {
            let mut response = Response::json("ignored");
            response.status = status;
            responses.push(response);
        }
        let server = Server::new(responses).await;
        let (client, _) = server.client(|_| {});
        let error = client
            .treasury_range(day(26), day(26), &CancellationToken::new(), budget())
            .await
            .unwrap_err();
        assert_eq!(error.status, Some(status));
        assert_eq!(
            error.code,
            match status {
                401 | 403 => "Authentication",
                429 => "RateLimit",
                404 => "Response",
                _ => "Unavailable",
            }
        );
        assert_eq!(
            server.requests.lock().unwrap().len(),
            if matches!(status, 401 | 403 | 404) {
                1
            } else {
                3
            }
        );
        server.finish().await;
    }
}
#[tokio::test]
async fn transient_retry_then_success() {
    let mut failure = Response::json("");
    failure.status = 503;
    let server = Server::new(vec![failure, Response::json("[]")]).await;
    let (client, _) = server.client(|_| {});
    assert!(
        client
            .treasury_range(day(26), day(26), &CancellationToken::new(), budget())
            .await
            .unwrap()
            .is_empty()
    );
    assert_eq!(server.requests.lock().unwrap().len(), 2);
    server.finish().await;
}
#[tokio::test]
async fn circuit_opens_on_final_failure_and_api_clock_controls_reentry() {
    let mut failure = Response::json("");
    failure.status = 503;
    let server = Server::new(vec![failure, Response::json("[]")]).await;
    let (client, clock) = server.client(|o| {
        o.retries = 0;
        o.circuit_failures = 1;
    });
    assert_eq!(
        client
            .treasury_range(day(26), day(26), &CancellationToken::new(), budget())
            .await
            .unwrap_err()
            .code,
        "Unavailable"
    );
    assert_eq!(
        client
            .treasury_range(day(26), day(26), &CancellationToken::new(), budget())
            .await
            .unwrap_err()
            .message,
        "FMP circuit is open"
    );
    assert_eq!(server.requests.lock().unwrap().len(), 1);
    clock.update(DAY + 30 * 10_000_000, false).unwrap();
    assert!(
        client
            .treasury_range(day(26), day(26), &CancellationToken::new(), budget())
            .await
            .is_ok()
    );
    server.finish().await;
}
#[tokio::test]
async fn header_timeout_retries_but_received_body_keeps_overall_budget() {
    let mut slow = Response::json("[]");
    slow.head_delay = Duration::from_millis(80);
    let mut body = Response::json("[]");
    body.body_delay = Duration::from_millis(80);
    let server = Server::new(vec![slow, body]).await;
    let (client, _) = server.client(|o| o.request_timeout = Duration::from_millis(20));
    assert!(
        client
            .treasury_range(day(26), day(26), &CancellationToken::new(), budget())
            .await
            .is_ok()
    );
    assert_eq!(server.requests.lock().unwrap().len(), 2);
    server.finish().await;
}
#[tokio::test]
async fn body_and_normalized_row_caps_are_explicit() {
    for (body, bytes, rows, code) in [
        ("[{}]".into(), 2, 10, "ResponseTooLarge"),
        (
            format!(
                "[{},{}]",
                treasury("2026-10-25", "1"),
                treasury("2026-10-26", "1")
            ),
            4096,
            1,
            "Response",
        ),
    ] {
        let server = Server::new(vec![Response::json(body)]).await;
        let (client, _) = server.client(|o| {
            o.response_bytes = bytes;
            o.rows = rows;
        });
        assert_eq!(
            client
                .treasury_range(day(25), day(26), &CancellationToken::new(), budget())
                .await
                .unwrap_err()
                .code,
            code
        );
        server.finish().await;
    }
}
#[tokio::test]
async fn cancellation_and_original_deadline_do_not_retry() {
    for cancel in [false, true] {
        let mut slow = Response::json("[]");
        slow.head_delay = Duration::from_secs(10);
        let server = Server::new(vec![slow]).await;
        let (client, _) = server.client(|_| {});
        let token = CancellationToken::new();
        let signal = token.clone();
        let timer = tokio::spawn(async move {
            if cancel {
                tokio::time::sleep(Duration::from_millis(30)).await;
                signal.cancel();
            }
        });
        let error = client
            .treasury_range(
                day(26),
                day(26),
                &token,
                Deadline::after(Duration::from_millis(60)),
            )
            .await
            .unwrap_err();
        assert_eq!(error.code, if cancel { "Cancelled" } else { "Timeout" });
        assert_eq!(server.requests.lock().unwrap().len(), 1);
        timer.await.unwrap();
        server.finish().await;
    }
}
