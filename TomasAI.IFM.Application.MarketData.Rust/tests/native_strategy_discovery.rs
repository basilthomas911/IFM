//! Complete raw source validation before native background planning.
use ifm_market_data_native::option_chain::discovery;
#[test]
fn published_policy_range_is_native_and_source_identity_is_strict() {
    let fixtures: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/strategy-policy.json"
    ))
    .unwrap();
    for fixture in fixtures.as_array().unwrap() {
        let command = &fixture["command"]["parameters"];
        let environment = command[6].as_str().unwrap();
        let bytes = serde_json::to_vec(&serde_json::json!([command])).unwrap();
        let policies = discovery::policies(&bytes, environment).unwrap();
        assert_eq!(policies.len(), 1);
        let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
        let range = discovery::range(&policies[0], date).unwrap();
        if let Some((from, until)) = range {
            assert!(from > date);
            assert!(until >= from);
            assert!((until - date).whole_days() <= 730);
        }
        let duplicate = serde_json::to_vec(&serde_json::json!([command, command])).unwrap();
        assert!(discovery::policies(&duplicate, environment).is_err());
        assert!(discovery::policies(&bytes, "Unconfigured").is_err());
    }
    assert!(discovery::policies(b"{}", "Development").is_err());
    assert!(discovery::policies(&vec![b' '; 4 * 1024 * 1024 + 1], "Development").is_err());
}
#[test]
fn definitions_are_complete_bounded_copied_models_without_local_preparation() {
    let fixtures: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/reviewed-options.json"
    ))
    .unwrap();
    let raw = &fixtures[0]["command"];
    let bytes = serde_json::to_vec(&serde_json::json!([raw])).unwrap();
    assert_eq!(discovery::definitions(&bytes).unwrap().len(), 1);
    assert!(
        discovery::definitions(&serde_json::to_vec(&serde_json::json!([raw, raw])).unwrap())
            .is_err()
    );
    assert!(discovery::definitions(&serde_json::to_vec(&vec![raw; 2049]).unwrap()).is_err());
    let mut second = raw.clone();
    second[0] = serde_json::json!("second-option");
    assert!(
        discovery::definitions(&serde_json::to_vec(&serde_json::json!([raw, second])).unwrap())
            .is_err(),
        "One physical option cannot map to different canonical contracts"
    );
    second[16] = serde_json::json!(raw[16].as_u64().unwrap() + 1);
    assert_eq!(
        discovery::definitions(&serde_json::to_vec(&serde_json::json!([raw, second])).unwrap())
            .unwrap()
            .len(),
        2,
        "Two options may share the exact same underlying binding"
    );
    second[33] = serde_json::json!("conflicting-underlying");
    assert!(
        discovery::definitions(&serde_json::to_vec(&serde_json::json!([raw, second])).unwrap())
            .is_err(),
        "A shared physical underlying cannot change its canonical contract"
    );
    assert!(discovery::definitions(b"[[1]]").is_err());
    assert!(discovery::definitions(b"null").is_err());
    assert!(discovery::definitions(&vec![0; 4 * 1024 * 1024 + 1]).is_err());
    assert!(discovery::definitions(b"[]").unwrap().is_empty());
}

#[tokio::test]
async fn background_facts_use_native_paged_io_and_cancel_without_a_managed_owner() {
    use ifm_market_data_native::{
        messaging::{codec, publisher::BrokerSettings, raw_io},
        runtime::{Deadline, owner::Owner},
    };
    use sha2::{Digest, Sha256};
    use std::{sync::Arc, time::Duration};
    use tokio::{
        io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
        net::TcpListener,
    };
    use tokio_util::sync::CancellationToken;
    for mode in 0..3 {
        let policies: serde_json::Value = serde_json::from_str(include_str!(
            "../qualification/ContextWireBridge/strategy-policy.json"
        ))
        .unwrap();
        let raw: serde_json::Value = serde_json::from_str(include_str!(
            "../qualification/ContextWireBridge/reviewed-options.json"
        ))
        .unwrap();
        let policy = policies[0]["command"]["parameters"].clone();
        let environment = policy[6].as_str().unwrap().to_owned();
        let expected_environment = environment.clone();
        let policy_bytes = serde_json::to_vec(&serde_json::json!([policy])).unwrap();
        let definition_bytes = serde_json::to_vec(&serde_json::json!([raw[0]["command"]])).unwrap();
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let endpoint = format!("nats://{}", listener.local_addr().unwrap());
        let (arrived, accepted) = tokio::sync::oneshot::channel();
        let peer = tokio::spawn(async move {
            let mut arrived = Some(arrived);
            for expected_kind in 4..=if mode == 0 { 5 } else { 4 } {
                let (socket, _) = listener.accept().await.unwrap();
                let (read, mut write) = socket.into_split();
                let mut reader = BufReader::new(read);
                write.write_all(b"INFO {\"server_id\":\"native-background-facts\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.unwrap();
                let mut line = String::new();
                loop {
                    line.clear();
                    if reader.read_line(&mut line).await.unwrap_or(0) == 0 {
                        break;
                    }
                    if line.starts_with("PING") {
                        if write.write_all(b"PONG\r\n").await.is_err() {
                            break;
                        }
                    } else if line.starts_with("PUB ") {
                        let fields = line.split_whitespace().collect::<Vec<_>>();
                        let length = fields[3].parse::<usize>().unwrap();
                        let mut bytes = vec![0; length + 2];
                        reader.read_exact(&mut bytes).await.unwrap();
                        let request =
                            rmpv::decode::read_value(&mut bytes[..length].as_ref()).unwrap();
                        let request = request.as_array().unwrap();
                        assert_eq!(request[1].as_u64(), Some(expected_kind));
                        let args: serde_json::Value =
                            serde_json::from_slice(request[2].as_slice().unwrap()).unwrap();
                        if expected_kind == 4 {
                            assert_eq!(args["environment"], expected_environment);
                        } else {
                            assert_eq!(args["root"], "ES");
                            assert!(
                                args["until"].as_i64().unwrap() > args["from"].as_i64().unwrap()
                            );
                        }
                        if let Some(arrived) = arrived.take() {
                            let _ = arrived.send(());
                        }
                        if mode == 1 {
                            continue;
                        }
                        let payload = if expected_kind == 4 {
                            &policy_bytes
                        } else {
                            &definition_bytes
                        };
                        let offset = request[3].as_u64().unwrap() as usize;
                        let count = (payload.len() - offset).min(raw_io::PAGE);
                        let response = codec::encode(&rmpv::Value::Array(vec![
                            request[0].clone(),
                            (if mode == 2 { 0 } else { payload.len() } as u32).into(),
                            (if mode == 2 { 0 } else { offset } as u32).into(),
                            if mode == 2 {
                                "".into()
                            } else {
                                format!("{:X}", Sha256::digest(payload)).into()
                            },
                            rmpv::Value::Binary(if mode == 2 {
                                vec![]
                            } else {
                                payload[offset..offset + count].to_vec()
                            }),
                            if mode == 2 {
                                "Storage unavailable".into()
                            } else {
                                rmpv::Value::Nil
                            },
                        ]))
                        .unwrap();
                        if write
                            .write_all(
                                format!("MSG {} 1 {}\r\n", fields[2], response.len()).as_bytes(),
                            )
                            .await
                            .is_err()
                            || write.write_all(&response).await.is_err()
                            || write.write_all(b"\r\n").await.is_err()
                        {
                            break;
                        }
                    }
                }
            }
        });
        let root = Arc::new(Owner::default());
        let parent = Arc::new(root.child().unwrap());
        let sibling = Arc::new(root.child().unwrap());
        let stop = CancellationToken::new();
        let stopping = stop.clone();
        let child = parent.clone();
        let source = discovery::Discovery {
            broker: BrokerSettings::new(endpoint, vec!["127.0.0.1:1".parse().unwrap()]).unwrap(),
            scope: "development".into(),
            environment,
        };
        let task = tokio::spawn(async move {
            let deadline = Deadline::after(Duration::from_secs(5));
            let policies = source.policies(child.clone(), &stopping, deadline).await?;
            source
                .universe(
                    policies[0].clone(),
                    time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap(),
                    child,
                    &stopping,
                    deadline,
                )
                .await
        });
        tokio::time::timeout(Duration::from_secs(2), accepted)
            .await
            .unwrap()
            .unwrap();
        if mode == 1 {
            stop.cancel();
        }
        let result = tokio::time::timeout(Duration::from_secs(5), task)
            .await
            .unwrap()
            .unwrap();
        if mode == 0 {
            assert_eq!(result.unwrap().contracts.len(), 1);
        } else {
            assert!(result.is_err());
        }
        assert!(!sibling.cancellation().is_cancelled());
        drop(parent);
        drop(sibling);
        root.stop().unwrap();
        root.wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
        assert!(root.disposable().unwrap());
        tokio::time::timeout(Duration::from_secs(2), peer)
            .await
            .unwrap()
            .unwrap();
    }
}

#[test]
fn reviewed_raw_source_preserves_exact_fields_for_native_planning() {
    use ifm_market_data_native::{
        option_chain::coverage::Planner,
        option_chain::discovery::Universe,
        reference::{decimal, option_contract::OptionContract},
        runtime::Deadline,
    };
    use std::{collections::BTreeMap, sync::Arc, time::Duration};
    use tokio_util::sync::CancellationToken;
    let fixtures: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/reviewed-options.json"
    ))
    .unwrap();
    let raw = &fixtures[0]["command"];
    let model = OptionContract::from_command(raw).unwrap();
    let definition = model.coverage_definition().unwrap();
    assert_eq!(definition.contract, raw[0].as_str().unwrap());
    assert_eq!(definition.root, raw[2].as_str().unwrap());
    assert_eq!(definition.underlying, raw[33].as_str().unwrap());
    assert_eq!(
        definition.strike.compare(model.exact_strike().unwrap()),
        Some(std::cmp::Ordering::Equal)
    );
    let policies: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/strategy-policy.json"
    ))
    .unwrap();
    let parameters = ifm_market_data_native::option_chain::strategy::from_command(
        &policies[0]["command"]["parameters"],
    )
    .unwrap();
    let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
    let input = Universe {
        parameters: Arc::new(parameters),
        contracts: vec![model],
    };
    let stop = CancellationToken::new();
    stop.cancel();
    let result = input.plan(
        &Planner::default(),
        date,
        decimal::parse("6000").unwrap(),
        0.20,
        ifm_market_data_native::reference::calendar::Zone::new("Eastern Standard Time").unwrap(),
        &BTreeMap::new(),
        &stop,
        Deadline::after(Duration::from_secs(1)),
    );
    assert!(
        result.is_err(),
        "Cancellation must stop coverage before accepting a universe"
    );
}
