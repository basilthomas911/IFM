//! Committed business facts are validated before any live desired-state update.
//! These tests do not start a broker, provider session or managed lease owner.
use ifm_market_data_native::option_chain::committed::Book;
use serde_json::{Value, json};
fn digest() -> String {
    "a".repeat(64)
}
fn source(index: usize) -> Value {
    json!({"id":format!("workflow-{index}"),"version":1,"event_id":format!("{:032x}",index+1),"digest":digest(),"status":1,"reason":"Complete","workflow_type":"IntrinsicTimeWorkflow","workflow_id":format!("workflow-{index}"),"leg_id":"position","leases":[]})
}
fn empty() -> Value {
    json!({"schema":1,"authority":"IFM","dataset":"GLBX.MDP3","revision":0,"digest":digest(),"authorities":[],"plans":[]})
}
fn parse(value: &Value) -> Result<Book, String> {
    Book::parse(&serde_json::to_vec(value).unwrap(), "IFM")
}
fn future() -> Value {
    let mut book = empty();
    let id = digest();
    let mut owner = source(0);
    owner["status"] = json!(0);
    owner["leases"] = json!([[
        "00112233445566778899aabbccddeeff",
        1,
        4,
        "Databento",
        "GLBX.MDP3",
        "ESZ6",
        "mbp-1",
        0,
        null,
        id
    ]]);
    book["authorities"] = json!([owner]);
    book["plans"] = json!([{"id":digest(),"schema":1,"dataset":"GLBX.MDP3","maturity":0,"candidates":[],"futures":[["ESZ6","ES","GLBX.MDP3","XCME","USD",18000000000000000i64,0,"50","0.25",digest()]],"routes":[["ESZ6","ESZ6",1,"ES","GLBX.MDP3",false,false]],"separate":false,"independent":false}]);
    book
}
#[test]
fn empty_terminal_unknown_and_exact_future_facts() {
    assert!(parse(&empty()).unwrap().plans.is_empty());
    let mut value = future();
    let book = parse(&value).unwrap();
    let lease = &book.sources["workflow-0"].leases[0];
    assert_eq!(
        lease.id,
        [
            0x33, 0x22, 0x11, 0, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee,
            0xff
        ]
    );
    assert_eq!(lease.provider, "Databento");
    assert_eq!(lease.schema, "mbp-1");
    assert!(!lease.option);
    let plan = &book.plans[&digest()];
    assert_eq!(plan.routes[0].symbol, "ESZ6");
    assert_eq!(plan.routes[0].root, "ES");
    value["authorities"][0]["status"] = json!(2);
    assert_eq!(parse(&value).unwrap().sources["workflow-0"].leases.len(), 1);
    value["authorities"][0]["status"] = json!(1);
    assert!(parse(&value).is_err());
    value["authorities"][0]["leases"] = json!([]);
    value["plans"] = json!([]);
    assert!(parse(&value).is_ok());
}
#[test]
fn option_exact_membership_and_underlying_are_not_inferred() {
    let mut value = future();
    let plan = &mut value["plans"][0];
    plan["maturity"] = json!(739920);
    plan["futures"] = json!([]);
    plan["candidates"] = json!([[
        "ES.OPT",
        "reviewed-v1",
        digest(),
        "GLBX.MDP3",
        1,
        100,
        "ESZ6 C6000",
        "ESZ6",
        1,
        "6000",
        1800000000000000000u64
    ]]);
    let references: Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    value["plans"][0]["references"] = references[0]["raw"].clone();
    let lease = &mut value["authorities"][0]["leases"][0];
    lease[5] = json!("ES.OPT");
    lease[7] = json!(1);
    lease[8] = json!("ESZ6");
    let book = parse(&value).unwrap();
    assert_eq!(book.plans[&digest()].candidates[0].instrument, 100);
    value["authorities"][0]["leases"][0][8] = json!("ESH7");
    assert!(parse(&value).is_err());
    value["authorities"][0]["leases"][0][8] = json!("ESZ6");
    value["plans"][0]["routes"][0][0] = json!("ESH7");
    assert!(parse(&value).is_err());
}
#[test]
fn malformed_source_never_produces_partial_ownership() {
    let original = future();
    let edits = [
        ("schema", json!(2)),
        ("authority", json!("OTHER")),
        ("dataset", json!("XCBF.PITCH")),
        ("revision", json!(-1)),
        ("digest", json!("bad")),
        ("plans", json!(null)),
        ("authorities", json!(null)),
    ];
    for (key, value) in edits {
        let mut book = original.clone();
        book[key] = value;
        assert!(parse(&book).is_err(), "{key}");
    }
    for status in [json!(-1), json!(3), json!(null)] {
        let mut book = original.clone();
        book["authorities"][0]["status"] = status;
        assert!(parse(&book).is_err());
    }
    for index in [0usize, 1, 2, 3, 4, 5, 6, 7, 8, 9] {
        let mut book = original.clone();
        book["authorities"][0]["leases"][0][index] = json!({});
        assert!(parse(&book).is_err(), "lease {index}");
    }
    let bytes = serde_json::to_vec(&original).unwrap();
    for end in 0..bytes.len() {
        assert!(Book::parse(&bytes[..end], "IFM").is_err());
    }
    let mut invalid = bytes;
    invalid[0] = 255;
    assert!(Book::parse(&invalid, "IFM").is_err());
}
#[test]
fn full_ticker_identity_is_preserved_and_duplicates_rejected() {
    let mut value = future();
    let original = value["authorities"][0]["leases"][0].clone();
    let mut second = original.clone();
    second[0] = json!("112233445566778899aabbccddeeff00");
    value["authorities"][0]["leases"]
        .as_array_mut()
        .unwrap()
        .push(second.clone());
    assert!(parse(&value).is_err());
    second[3] = json!("OtherProvider");
    value["authorities"][0]["leases"][1] = second.clone();
    assert!(parse(&value).is_ok());
    second[3] = original[3].clone();
    second[6] = json!("trades");
    value["authorities"][0]["leases"][1] = second;
    assert!(parse(&value).is_ok());
    value["authorities"][0]["leases"][1][0] = original[0].clone();
    assert!(parse(&value).is_err());
    let mut other = source(1);
    other["workflow_id"] = json!("workflow-0");
    value["authorities"][0]["leases"] = json!([original]);
    value["authorities"].as_array_mut().unwrap().push(other);
    assert!(parse(&value).is_err());
}
#[test]
fn maximum_authority_and_plan_capacities_are_explicit() {
    let mut value = empty();
    value["authorities"] = Value::Array((0..10000).map(source).collect());
    let book = parse(&value).unwrap();
    assert_eq!(book.sources.len(), 10000);
    value["authorities"]
        .as_array_mut()
        .unwrap()
        .push(source(10000));
    assert!(parse(&value).is_err());
    let mut value = future();
    let recipe = value["plans"][0].clone();
    value["plans"] = Value::Array(vec![recipe; 9]);
    assert!(parse(&value).is_err());
    let mut value = future();
    let row = value["authorities"][0]["leases"][0].clone();
    value["authorities"][0]["leases"] = Value::Array(vec![row; 129]);
    assert!(parse(&value).is_err());
    let bytes = vec![b' '; 32 * 1024 * 1024 + 1];
    assert!(Book::parse(&bytes, "IFM").is_err());
}
#[test]
fn monotonic_journal_and_individual_authority_watermarks() {
    let value = future();
    let prior = parse(&value).unwrap();
    assert!(prior.validate_successor(&prior).is_ok());
    let mut next = value.clone();
    next["revision"] = json!(1);
    assert!(parse(&next).unwrap().validate_successor(&prior).is_ok());
    let mut conflict = value.clone();
    conflict["digest"] = json!("b".repeat(64));
    assert!(
        parse(&conflict)
            .unwrap()
            .validate_successor(&prior)
            .is_err()
    );
    let newest = parse(&next).unwrap();
    assert!(prior.validate_successor(&newest).is_err());
    for field in ["event_id", "digest", "reason", "workflow_id"] {
        let mut changed = next.clone();
        changed["authorities"][0][field] = match field {
            "event_id" => json!("112233445566778899aabbccddeeff00"),
            "digest" => json!("b".repeat(64)),
            _ => json!("changed"),
        };
        assert!(
            parse(&changed).unwrap().validate_successor(&prior).is_err(),
            "{field}"
        );
    }
    let mut removed = empty();
    removed["revision"] = json!(1);
    assert!(parse(&removed).unwrap().validate_successor(&prior).is_err());
    let mut changed = next;
    changed["authorities"][0]["version"] = json!(2);
    changed["authorities"][0]["status"] = json!(1);
    changed["authorities"][0]["leases"] = json!([]);
    changed["plans"] = json!([]);
    assert!(parse(&changed).unwrap().validate_successor(&prior).is_ok());
}

#[tokio::test(start_paused = true)]
async fn accepted_facts_rebuild_without_polling_stalled_storage() {
    use ifm_market_data_native::{option_chain::committed::Directory, runtime::Deadline};
    use tokio_util::sync::CancellationToken;
    let directory = Directory::new("IFM").unwrap();
    let stop = CancellationToken::new();
    let budget = Deadline::recovery();
    let first = directory
        .load(|| async { parse(&future()) }, &stop, budget)
        .await
        .unwrap();
    assert!(directory.current().unwrap());
    let cached = directory
        .load(
            || async { panic!("accepted physical recovery must not poll storage") },
            &stop,
            budget,
        )
        .await
        .unwrap();
    assert!(std::sync::Arc::ptr_eq(&first, &cached));
    assert_eq!(directory.refresh().unwrap(), 2);
    assert_eq!(directory.refresh().unwrap(), 3);
    assert!(!directory.current().unwrap());
    let read = directory.load(std::future::pending::<Result<Book, String>>, &stop, budget);
    tokio::pin!(read);
    assert!(futures_util::poll!(read.as_mut()).is_pending());
    assert!(std::sync::Arc::ptr_eq(
        &first,
        &directory.accepted().unwrap().unwrap()
    ));
    tokio::time::advance(std::time::Duration::from_secs(120)).await;
    assert!(read.await.is_err());
    assert!(!directory.current().unwrap());
    assert!(directory.accepted().unwrap().is_some());
}
#[tokio::test(start_paused = true)]
async fn cancellation_releases_capture_admission_and_late_reply_cannot_resurrect() {
    use ifm_market_data_native::{option_chain::committed::Directory, runtime::Deadline};
    use tokio_util::sync::CancellationToken;
    let directory = Directory::new("IFM").unwrap();
    let stop = CancellationToken::new();
    let budget = Deadline::recovery();
    let read = directory.load(std::future::pending::<Result<Book, String>>, &stop, budget);
    tokio::pin!(read);
    assert!(futures_util::poll!(read.as_mut()).is_pending());
    stop.cancel();
    assert!(read.await.is_err());
    let fresh = CancellationToken::new();
    assert!(
        directory
            .load(|| async { parse(&future()) }, &fresh, budget)
            .await
            .is_ok()
    );
    directory.refresh().unwrap();
    let (tx, rx) = tokio::sync::oneshot::channel();
    let read = directory.load(|| async { rx.await.unwrap() }, &fresh, budget);
    tokio::pin!(read);
    assert!(futures_util::poll!(read.as_mut()).is_pending());
    directory.close().unwrap();
    let _ = tx.send(parse(&future()));
    assert!(read.await.is_err());
    assert!(directory.accepted().unwrap().is_none());
    assert!(!directory.current().unwrap());
    assert!(directory.refresh().is_err());
}
#[tokio::test(start_paused = true)]
async fn duplicate_callers_join_one_capture_and_dropping_waiter_does_not_cancel_owner() {
    use ifm_market_data_native::{option_chain::committed::Directory, runtime::Deadline};
    use tokio_util::sync::CancellationToken;
    let directory = Directory::new("IFM").unwrap();
    let stop = CancellationToken::new();
    let budget = Deadline::recovery();
    let (tx, rx) = tokio::sync::oneshot::channel();
    let owner = directory.load(|| async { rx.await.unwrap() }, &stop, budget);
    tokio::pin!(owner);
    assert!(futures_util::poll!(owner.as_mut()).is_pending());
    {
        let waiter = directory.load(
            || async { panic!("duplicate source future must never be polled") },
            &stop,
            budget,
        );
        tokio::pin!(waiter);
        assert!(futures_util::poll!(waiter.as_mut()).is_pending());
    }
    let waiter = directory.load(
        || async { panic!("joined source must not be polled") },
        &stop,
        budget,
    );
    tokio::pin!(waiter);
    assert!(futures_util::poll!(waiter.as_mut()).is_pending());
    tx.send(parse(&future())).ok().unwrap();
    let accepted = owner.await.unwrap();
    assert!(std::sync::Arc::ptr_eq(&accepted, &waiter.await.unwrap()));
}
#[tokio::test(start_paused = true)]
async fn dropping_reader_releases_admission_and_zero_budget_never_runs_source() {
    use ifm_market_data_native::{option_chain::committed::Directory, runtime::Deadline};
    use tokio_util::sync::CancellationToken;
    let directory = Directory::new("IFM").unwrap();
    let stop = CancellationToken::new();
    let budget = Deadline::recovery();
    {
        let read = directory.load(std::future::pending::<Result<Book, String>>, &stop, budget);
        tokio::pin!(read);
        assert!(futures_util::poll!(read.as_mut()).is_pending());
    }
    assert!(
        directory
            .load(
                || async { panic!("expired source cannot run") },
                &stop,
                Deadline::after(std::time::Duration::ZERO)
            )
            .await
            .is_err()
    );
    assert!(
        directory
            .load(|| async { parse(&future()) }, &stop, budget)
            .await
            .is_ok()
    );
}
#[tokio::test]
async fn conflicting_refresh_keeps_last_accepted_book() {
    use ifm_market_data_native::{option_chain::committed::Directory, runtime::Deadline};
    use tokio_util::sync::CancellationToken;
    let directory = Directory::new("IFM").unwrap();
    let stop = CancellationToken::new();
    let budget = Deadline::recovery();
    let accepted = directory
        .load(|| async { parse(&future()) }, &stop, budget)
        .await
        .unwrap();
    directory.refresh().unwrap();
    let mut changed = future();
    changed["digest"] = json!("b".repeat(64));
    assert!(
        directory
            .load(|| async { parse(&changed) }, &stop, budget)
            .await
            .is_err()
    );
    assert!(!directory.current().unwrap());
    assert!(std::sync::Arc::ptr_eq(
        &accepted,
        &directory.accepted().unwrap().unwrap()
    ));
    changed["revision"] = json!(1);
    assert!(
        directory
            .load(|| async { parse(&changed) }, &stop, budget)
            .await
            .is_ok()
    );
    assert!(directory.current().unwrap());
    assert_eq!(directory.refresh().unwrap(), 3);
}

#[tokio::test(start_paused = true)]
async fn later_committed_handoff_fences_an_older_inflight_read_without_rearming_budget() {
    use ifm_market_data_native::{option_chain::committed::Directory, runtime::Deadline};
    use tokio_util::sync::CancellationToken;
    let directory = Directory::new("IFM").unwrap();
    let stop = CancellationToken::new();
    let deadline = Deadline::recovery();
    let (tx, rx) = tokio::sync::oneshot::channel();
    let read = directory.load(|| async { rx.await.unwrap() }, &stop, deadline);
    tokio::pin!(read);
    assert!(futures_util::poll!(read.as_mut()).is_pending());
    tokio::time::advance(std::time::Duration::from_secs(119)).await;
    assert_eq!(directory.refresh().unwrap(), 2);
    assert!(read.await.is_err());
    assert!(tx.is_closed());
    assert!(directory.accepted().unwrap().is_none());
    assert!(!directory.current().unwrap());
    let replacement = directory.load(
        std::future::pending::<Result<Book, String>>,
        &stop,
        deadline,
    );
    tokio::pin!(replacement);
    assert!(futures_util::poll!(replacement.as_mut()).is_pending());
    tokio::time::advance(std::time::Duration::from_secs(1)).await;
    assert!(replacement.await.is_err());
    assert!(!directory.current().unwrap());
}

#[test]
fn physical_reconstruction_ignores_business_watermarks_but_not_recipe_changes() {
    let value = future();
    let physical = parse(&value).unwrap().physical_signature().unwrap();
    let mut changed = value.clone();
    changed["revision"] = 2.into();
    changed["authorities"][0]["version"] = 2.into();
    changed["authorities"][0]["digest"] = "d".repeat(64).into();
    changed["authorities"][0]["event_id"] = "00112233445566778899aabbccddee00".into();
    assert_eq!(
        parse(&changed).unwrap().physical_signature().unwrap(),
        physical
    );
    for (index, value) in [
        (7, json!("100")),
        (8, json!("0.5")),
        (5, json!(18000000000000001i64)),
    ] {
        let mut next = changed.clone();
        next["plans"][0]["futures"][0][index] = value;
        assert_ne!(
            parse(&next).unwrap().physical_signature().unwrap(),
            physical
        );
    }
    changed["plans"][0]["routes"][0][1] = "ESH7".into();
    assert_ne!(
        parse(&changed).unwrap().physical_signature().unwrap(),
        physical
    );
}
#[tokio::test]
async fn obsolete_physical_recipe_cannot_acknowledge_new_authority_or_start_external_io() {
    use ifm_market_data_native::{
        messaging::publisher::BrokerSettings,
        option_chain::{committed::Directory, discovery::Discovery},
        runtime::{Deadline, owner::Owner},
    };
    use std::sync::Arc;
    use tokio_util::sync::CancellationToken;
    let value = future();
    let old = parse(&value).unwrap().physical_signature().unwrap();
    let mut next = value.clone();
    next["plans"][0]["futures"][0][7] = json!("100");
    let directory = Directory::new("IFM").unwrap();
    let stop = CancellationToken::new();
    let book = directory
        .load(|| async { parse(&next) }, &stop, Deadline::recovery())
        .await
        .unwrap();
    let discovery = Discovery {
        broker: BrokerSettings::new(
            "nats://127.0.0.1:1".into(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        scope: "qualification".into(),
        environment: "Development".into(),
    };
    let root = Arc::new(Owner::default());
    let parent = Arc::new(root.child().unwrap());
    assert!(
        !directory
            .deliver_for_physical(
                &discovery,
                parent.clone(),
                &stop,
                Deadline::recovery(),
                Some(old)
            )
            .await
            .unwrap()
    );
    assert!(!directory.acknowledged(&book).unwrap());
    drop(parent);
    root.stop().unwrap();
    root.wait_idle(&stop, Deadline::recovery()).await.unwrap();
}
#[tokio::test(start_paused = true)]
async fn pending_handoff_deadline_is_original_until_explicit_new_native_episode() {
    use ifm_market_data_native::{option_chain::committed::Directory, runtime::Deadline};
    use std::time::Duration;
    let directory = Directory::new("IFM").unwrap();
    directory.refresh().unwrap();
    tokio::time::advance(Duration::from_secs(60)).await;
    directory.refresh().unwrap();
    assert_eq!(
        directory.pending_deadline().unwrap().unwrap().remaining(),
        Duration::from_secs(60)
    );
    directory.rebase_pending(Deadline::recovery()).unwrap();
    assert_eq!(
        directory.pending_deadline().unwrap().unwrap().remaining(),
        Duration::from_secs(60)
    );
    tokio::time::advance(Duration::from_secs(60)).await;
    directory
        .rebase_pending(Deadline::after(Duration::ZERO))
        .unwrap();
    assert!(
        directory
            .pending_deadline()
            .unwrap()
            .unwrap()
            .remaining()
            .is_zero()
    );
    directory.rebase_pending(Deadline::recovery()).unwrap();
    assert_eq!(
        directory.pending_deadline().unwrap().unwrap().remaining(),
        Duration::from_secs(120)
    );
}
