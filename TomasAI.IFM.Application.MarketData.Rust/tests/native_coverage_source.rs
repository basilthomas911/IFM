//! Complete compact-universe transport qualification using generated pages.
use ifm_market_data_native::{
    messaging::{codec, publisher::BrokerSettings, raw_io},
    option_chain::discovery::Discovery,
    runtime::{Deadline, owner::Owner},
};
use sha2::{Digest, Sha256};
use std::sync::{
    Arc,
    atomic::{AtomicUsize, Ordering},
};
use std::time::Duration;
use tokio::{
    io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
    net::TcpListener,
};
use tokio_util::sync::CancellationToken;
fn page(template: &serde_json::Value, count: usize, offset: usize, mode: u8) -> Vec<u8> {
    let rows = (offset..(offset + 256).min(count))
        .map(|index| {
            let mut row = template.clone();
            let index = if mode == 1 { 0 } else { index };
            row[0] = format!("ES20261110C{}", 6000 + index).into();
            row[9] = (43 + index as u64).into();
            row
        })
        .collect::<Vec<_>>();
    serde_json::to_vec(&rows).unwrap()
}
async fn run(count: usize, mode: u8) {
    let fixtures: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/reviewed-options.json"
    ))
    .unwrap();
    let model = &fixtures[0]["command"];
    let fields = [
        0, 2, 8, 9, 11, 12, 13, 14, 15, 16, 19, 21, 23, 24, 29, 33, 34, 35, 36, 37, 40, 42, 43, 44,
    ];
    let template = serde_json::Value::Array(
        fields
            .into_iter()
            .map(|index| model[index].clone())
            .collect(),
    );
    let mut hash = Sha256::new();
    for offset in (0..count).step_by(256) {
        hash.update(page(&template, count, offset, mode));
    }
    let digest = if mode == 2 {
        "0".repeat(64)
    } else {
        format!("{:X}", hash.finalize())
    };
    let header = serde_json::to_vec(
        &serde_json::json!({"id":"11111111111111111111111111111111","count":count,"digest":digest}),
    )
    .unwrap();
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = listener.local_addr().unwrap();
    let stop = CancellationToken::new();
    let stopping = stop.clone();
    let observed = Arc::new(AtomicUsize::new(0));
    let maximum = observed.clone();
    let arrivals = Arc::new(AtomicUsize::new(0));
    let arrived = arrivals.clone();
    let server = tokio::spawn(async move {
        let mut peers = tokio::task::JoinSet::new();
        loop {
            tokio::select! {biased;()=stopping.cancelled()=>break,accepted=listener.accept()=>{
                let(socket,_)=accepted.unwrap();let template=template.clone();let header=header.clone();let maximum=maximum.clone();let arrived=arrived.clone();
                peers.spawn(async move{
                    let(read,mut write)=socket.into_split();let mut read=BufReader::new(read);
                    if write.write_all(b"INFO {\"server_id\":\"compact-source\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.is_err(){return;}
                    let mut line=String::new();let mut sid=String::new();let mut response:Option<(Vec<u8>,String)>=None;
                    loop{line.clear();if read.read_line(&mut line).await.unwrap_or(0)==0{break;}
                        let fields=line.split_whitespace().collect::<Vec<_>>();
                        if fields.first()==Some(&"PING"){if write.write_all(b"PONG\r\n").await.is_err(){break;}}
                        else if fields.first()==Some(&"SUB"){sid=fields.last().unwrap().to_string();}
                        else if fields.first()==Some(&"PUB"){
                            let length=fields.last().unwrap().parse::<usize>().unwrap();assert!(length<20000);
                            let mut input=vec![0;length+2];if read.read_exact(&mut input).await.is_err(){break;}
                            let request=rmpv::decode::read_value(&mut &input[..length]).unwrap();let request=request.as_array().unwrap();assert_eq!(request[1].as_u64(),Some(7));
                            let args:serde_json::Value=serde_json::from_slice(request[2].as_slice().unwrap()).unwrap();
                            if response.is_none() {let body=if args["snapshot"].is_null(){header.clone()}else{
                                let offset=args["offset"].as_u64().unwrap() as usize;
                                arrived.fetch_add(1,Ordering::SeqCst);
                                if mode==4{continue;}
                                assert_eq!(args["snapshot"],"11111111111111111111111111111111");
                                page(&template,if mode==3{count-1}else{count},offset,mode)
                            };
                            maximum.fetch_max(body.len(),Ordering::SeqCst);
                            let digest=format!("{:X}",Sha256::digest(&body));response=Some((body,digest));}
                            let (body,digest)=response.as_ref().unwrap();
                            let offset=request[3].as_u64().unwrap() as usize;let end=(offset+raw_io::PAGE).min(body.len());
                            let receipt=codec::encode(&rmpv::Value::Array(vec![request[0].clone(),(body.len() as u32).into(),(offset as u32).into(),digest.clone().into(),rmpv::Value::Binary(body[offset..end].to_vec()),rmpv::Value::Nil])).unwrap();
                            let envelope=format!("MSG {} {} {}\r\n",fields[2],sid,receipt.len());
                            if write.write_all(envelope.as_bytes()).await.is_err()||write.write_all(&receipt).await.is_err()||write.write_all(b"\r\n").await.is_err(){break;}
                        }
                    }
                });
            },done=peers.join_next(),if !peers.is_empty()=>{done.unwrap().unwrap();}}
        }
        peers.abort_all();
        while let Some(result) = peers.join_next().await {
            assert!(result.is_ok() || result.unwrap_err().is_cancelled());
        }
    });
    let owner = Arc::new(Owner::default());
    let parent = Arc::new(owner.child().unwrap());
    let sibling = owner.child().unwrap();
    let source = Discovery {
        broker: BrokerSettings::new(
            format!("nats://{address}"),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        scope: "development".into(),
        environment: "Development".into(),
    };
    let cancel = CancellationToken::new();
    let cancellation = cancel.clone();
    let child = parent.clone();
    let task = tokio::spawn(async move {
        let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
        source
            .read_coverage(
                "ES",
                date,
                date + time::Duration::days(60),
                child,
                &cancellation,
                Deadline::recovery(),
            )
            .await
    });
    if mode == 4 {
        tokio::time::timeout(Duration::from_secs(5), async {
            while arrivals.load(Ordering::SeqCst) == 0 {
                tokio::task::yield_now().await;
            }
        })
        .await
        .unwrap();
        cancel.cancel();
    }
    let result = tokio::time::timeout(Duration::from_secs(60), task)
        .await
        .unwrap()
        .unwrap();
    if mode == 0 && count <= 100000 {
        let rows = result.unwrap();
        assert_eq!(rows.len(), count);
        assert!(observed.load(Ordering::SeqCst) < 256 * 2048);
    } else {
        assert!(
            result.is_err(),
            "Invalid or cancelled complete source was accepted"
        );
    }
    assert!(!sibling.cancellation().is_cancelled());
    drop(parent);
    drop(sibling);
    owner.stop().unwrap();
    owner
        .wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(owner.disposable().unwrap());
    stop.cancel();
    server.await.unwrap();
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn complete_100000_row_source_uses_bounded_pages_and_compact_native_models() {
    run(100000, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn compact_empty_and_partial_page_boundaries() {
    for count in [0, 1, 255, 256, 257, 4097] {
        run(count, 0).await;
    }
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn compact_invalid_count_digest_duplicate_and_incomplete_pages() {
    run(100001, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn compact_bad_digest_duplicate_and_missing_last_row() {
    for mode in [1, 2, 3] {
        run(257, mode).await;
    }
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn compact_stalled_page_cancels_without_pinning_dataset() {
    run(257, 4).await;
}
