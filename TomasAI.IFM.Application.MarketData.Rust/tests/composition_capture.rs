//! Offline coherent-scope boundary tests. C# financial/wire parity is exercised
//! separately by native_context against actual backend-generated fixtures.
use ifm_market_data_native::{
    messaging::option_evidence::Quote,
    option_chain::{
        composition::{
            capture::{self, Page, Request},
            model::{Future, Instrument},
        },
        qualification,
    },
    reference::decimal,
    runtime::{Deadline, clock::Clock},
};
use std::{collections::BTreeSet, time::Duration};
use tokio_util::sync::CancellationToken;
const AT: i64 = 17931024000000000;
fn request() -> Request {
    Request {
        id: [1; 16],
        scope: "scope".into(),
        generation: [2; 16],
        horizon: "Daily".into(),
        at: AT,
        until: AT + 10000000,
        maximum: 2048,
        age: 1000,
        skew: 250,
        options: false,
        selection: false,
        allow_missing: false,
        risk: BTreeSet::new(),
    }
}
fn item(contract: &str) -> Instrument {
    Instrument {
        contract: contract.into(),
        quote: Some(Quote {
            contract: contract.into(),
            quote: qualification::Quote {
                generation_low: u64::from_le_bytes([2; 8]),
                generation_high: u64::from_le_bytes([2; 8]),
                event: AT,
                received: AT,
                sequence: 1,
                bid: decimal::parse("6000").unwrap(),
                ask: decimal::parse("6000.25").unwrap(),
                bid_size: decimal::parse("1").unwrap(),
                ask_size: decimal::parse("1").unwrap(),
                ..Default::default()
            },
        }),
        context: None,
        strike: None,
        call: None,
        underlying: None,
        future: Some(Future {
            contract: contract.into(),
            root: "ES".into(),
            dataset: "GLBX.MDP3".into(),
            exchange: "XCME".into(),
            currency: "USD".into(),
            last_trading: AT + 864000000000,
            multiplier: decimal::parse("50").unwrap(),
            tick: decimal::parse(".25").unwrap(),
            digest: "A".repeat(64),
        }),
        selection: None,
        volume: None,
        interest: None,
        statistics_at: None,
        unavailable: None,
        last_event: None,
        last_received: None,
        volume_date: None,
        interest_date: None,
    }
}
fn page(items: Vec<Instrument>) -> Page {
    Page {
        generation: [2; 16],
        token: "token".into(),
        total: items.len(),
        instruments: items,
        continuation: None,
    }
}
async fn error(request: Request, pages: Vec<Page>) -> String {
    capture::capture(
        request,
        pages,
        &Clock::new(AT, false).unwrap(),
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .err()
    .unwrap()
    .code
}
#[tokio::test]
async fn complete_scope_is_canonical_and_explicit_empty_scope_is_valid() {
    let mut first = page(vec![item("ES-B")]);
    first.total = 2;
    first.continuation = Some("next".into());
    let mut second = page(vec![item("ES-A")]);
    second.total = 2;
    let clock = Clock::new(AT, false).unwrap();
    let stop = CancellationToken::new();
    let snapshot = capture::capture(
        request(),
        vec![first, second],
        &clock,
        &stop,
        Deadline::recovery(),
    )
    .await
    .unwrap();
    assert_eq!(snapshot.instruments[0].instrument.contract, "ES-A");
    assert_eq!(snapshot.digest.len(), 64);
    let flat = capture::capture(
        request(),
        vec![page(vec![item("ES-A"), item("ES-B")])],
        &clock,
        &stop,
        Deadline::recovery(),
    )
    .await
    .unwrap();
    assert_eq!(
        snapshot.digest, flat.digest,
        "pagination cannot change financial identity"
    );
    assert!(
        capture::capture(
            request(),
            vec![page(vec![])],
            &clock,
            &stop,
            Deadline::recovery()
        )
        .await
        .unwrap()
        .instruments
        .is_empty()
    );
}
#[tokio::test]
async fn invalid_pages_never_become_partial_success() {
    assert_eq!(error(request(), vec![]).await, "IncompleteChain");
    let mut p = page(vec![item("ES-A")]);
    p.generation = [3; 16];
    assert_eq!(error(request(), vec![p]).await, "Recovering");
    let mut p = page(vec![]);
    p.total = 2049;
    assert_eq!(error(request(), vec![p]).await, "SnapshotLimit");
    let mut p = page(vec![item("ES-A")]);
    p.total = 2;
    assert_eq!(error(request(), vec![p]).await, "IncompleteChain");
    let mut p = page(vec![item("ES-A")]);
    p.continuation = Some("next".into());
    assert_eq!(error(request(), vec![p]).await, "IncompleteChain");
    assert_eq!(
        error(request(), vec![page(vec![]), page(vec![])]).await,
        "IncompleteChain"
    );
    let mut p = page(vec![]);
    p.continuation = Some("next".into());
    assert_eq!(error(request(), vec![p]).await, "IncompleteChain");
    assert_eq!(
        error(request(), vec![page(vec![item("ES-A"), item("ES-A")])]).await,
        "ConflictingDefinition"
    );
    let mut p = page(vec![item("ES-A")]);
    p.instruments[0].quote.as_mut().unwrap().contract = "wrong".into();
    assert_eq!(error(request(), vec![p]).await, "ConflictingDefinition");
    for change in 0..3 {
        let mut p = page(vec![item("ES-A")]);
        p.total = 2;
        p.continuation = Some("next".into());
        let mut q = page(vec![item("ES-B")]);
        q.total = 2;
        match change {
            0 => q.token = "different".into(),
            1 => q.total = 3,
            _ => q.continuation = Some("next".into()),
        }
        assert_eq!(error(request(), vec![p, q]).await, "IncompleteChain");
    }
}
#[tokio::test]
async fn quote_metadata_request_and_deadline_boundaries_are_explicit() {
    for change in 0..9 {
        let mut r = request();
        match change {
            0 => r.id = [0; 16],
            1 => r.generation = [0; 16],
            2 => r.scope = " ".into(),
            3 => r.maximum = 0,
            4 => r.maximum = 2049,
            5 => r.age = 0,
            6 => r.skew = 2001,
            7 => r.at = AT + 1,
            _ => r.until = AT,
        }
        assert_eq!(error(r, vec![page(vec![])]).await, "InvalidSnapshotRequest");
    }
    for change in 0..9 {
        let mut i = item("ES-A");
        let expected = match change {
            0 => {
                i.quote = None;
                "QuoteUnavailable"
            }
            1 => {
                i.quote.as_mut().unwrap().quote.generation_low = 0;
                "Recovering"
            }
            2 => {
                i.quote.as_mut().unwrap().quote.event = AT - 10000001;
                "StaleData"
            }
            3 => {
                i.quote.as_mut().unwrap().quote.received = AT + 1;
                "InvalidQuote"
            }
            4 => {
                i.quote.as_mut().unwrap().quote.event = AT + 1;
                "InvalidQuote"
            }
            5 => {
                i.quote.as_mut().unwrap().quote.bid = decimal::parse("0").unwrap();
                "InvalidQuote"
            }
            6 => {
                i.future.as_mut().unwrap().currency = "EUR".into();
                "ContractMetadataUnavailable"
            }
            7 => {
                i.future.as_mut().unwrap().last_trading = AT;
                "ContractMetadataUnavailable"
            }
            _ => {
                i.future = None;
                "ContractMetadataUnavailable"
            }
        };
        assert_eq!(error(request(), vec![page(vec![i])]).await, expected);
    }
    let mut older = item("ES-B");
    older.quote.as_mut().unwrap().quote.event -= 2500001;
    assert_eq!(
        error(request(), vec![page(vec![item("ES-A"), older])]).await,
        "IncoherentQuotes"
    );
    let clock = Clock::new(AT, false).unwrap();
    let stop = CancellationToken::new();
    stop.cancel();
    assert_eq!(
        capture::capture(
            request(),
            vec![page(vec![])],
            &clock,
            &stop,
            Deadline::recovery()
        )
        .await
        .err()
        .unwrap()
        .code,
        "SnapshotTimeout"
    );
    assert_eq!(
        capture::capture(
            request(),
            vec![page(vec![])],
            &clock,
            &CancellationToken::new(),
            Deadline::after(Duration::ZERO)
        )
        .await
        .err()
        .unwrap()
        .code,
        "SnapshotTimeout"
    );
    let mut boundary = item("ES-A");
    boundary.quote.as_mut().unwrap().quote.event -= 10000000;
    assert_eq!(
        error(request(), vec![page(vec![boundary])]).await,
        "StaleData",
        "exact expiry cannot escape as a current snapshot"
    );
}

#[test]
fn deferred_cache_capture_must_freeze_time_after_source_work_without_extending_recovery() {
    let clock = Clock::new(AT, false).unwrap();
    let mut request = request();
    let recovery = Deadline::recovery();
    let before = recovery.remaining();
    assert!(capture::validate_request(&request, &clock).is_ok());
    // Source/storage/numerical work can outlive the one-second result validity.
    clock.update(AT + 20_000_000, false).unwrap();
    assert_eq!(
        capture::validate_request(&request, &clock)
            .unwrap_err()
            .code,
        "InvalidSnapshotRequest"
    );
    request.at = clock.utc_ticks().unwrap();
    request.until = request.at + 10_000_000;
    assert!(capture::validate_request(&request, &clock).is_ok());
    assert!(recovery.remaining() <= before);
    request.until = request.at;
    assert!(capture::validate_request(&request, &clock).is_err());
}
