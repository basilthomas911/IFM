//! Native command/query ABI checks without a managed lifecycle coordinator.
use ifm_market_data_native::ffi::{
    live::{NativeLiveConfiguration, ifm_market_data_clock_v1, ifm_market_data_live_configure_v1},
    market_owner::*,
    runtime::*,
};
#[test]
fn native_owner_commands_are_bounded_and_mutually_exclusive_with_prototype_live_ownership() {
    assert_eq!(std::mem::size_of::<NativeOwnerHealth>(), 64);
    assert_eq!(std::mem::size_of::<NativeOwnerIdentity>(), 56);
    assert_eq!(ifm_market_data_owner_features_v1(), 0x1ffff);
    let mut handle = 0;
    // SAFETY: All inputs/outputs below are caller-owned aligned fixed records or
    // bounded readable/writable arrays that remain live for each synchronous call.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 2, &mut handle), 0);
        assert_eq!(
            ifm_market_data_owner_create_v1(handle),
            1,
            "authoritative API clock is required"
        );
        assert_eq!(ifm_market_data_clock_v1(handle, 0, 0), 0);
        assert_eq!(ifm_market_data_owner_create_v1(handle), 0);
        assert_eq!(ifm_market_data_owner_create_v1(handle), 7);
        let config = NativeLiveConfiguration {
            size: 40,
            date: 20261027,
            utc_ticks: 0,
            identity_low: 9,
            identity_high: 0,
            capacity: 2112,
            advancing: 0,
        };
        assert_eq!(
            ifm_market_data_live_configure_v1(handle, &config),
            7,
            "old and new ownership cannot overlap"
        );
        assert_eq!(std::mem::size_of::<NativeRetiredEvidence>(), 40);
        for invalid in [
            b"[]".as_slice(),
            b"{}",
            b"[null]",
            b"[{\"lane\":3}]",
            b"[{\"lane\":1},{\"lane\":1}]",
        ] {
            assert_eq!(
                ifm_market_data_owner_start_v1(handle, invalid.as_ptr(), invalid.len() as u32),
                1
            );
        }
        let query = br#"{"kind":"binding","contract":"ES20261218","options":false}"#;
        let mut query_required = 999;
        let mut changed = 0xa5u8;
        assert_ne!(
            ifm_market_data_owner_route_v1(handle, std::ptr::null(), 0, &mut changed),
            0
        );
        assert_eq!(changed, 0xa5);
        let release=br#"{"workflow_type":"fixture","workflow_id":"owner","leg_id":"leg","contract":"unknown","options":false,"acquire":false}"#;
        assert_ne!(
            ifm_market_data_owner_route_v1(
                handle,
                release.as_ptr(),
                release.len() as u32,
                std::ptr::null_mut()
            ),
            0
        );
        assert_eq!(
            ifm_market_data_owner_route_v1(
                handle,
                release.as_ptr(),
                release.len() as u32,
                &mut changed
            ),
            0
        );
        assert_eq!(changed, 0);
        let mut query_output = [0xa5; 4096];
        assert_eq!(
            ifm_market_data_owner_query_v1(
                handle,
                query.as_ptr(),
                query.len() as u32,
                std::ptr::null_mut(),
                0,
                &mut query_required
            ),
            4
        );
        assert_eq!(query_required, 1);
        assert_eq!(
            ifm_market_data_owner_query_v1(
                handle,
                query.as_ptr(),
                query.len() as u32,
                query_output.as_mut_ptr(),
                4096,
                &mut query_required
            ),
            0
        );
        assert_eq!(query_output[0], 0xc0);
        assert_eq!(query_output[1], 0xa5);
        for invalid in [
            br#"{"kind":"binding","contract":"ES","options":"false"}"#.as_slice(),
            b"{}",
            b"[]",
            b"null",
        ] {
            query_output.fill(0xa5);
            query_required = 999;
            assert_eq!(
                ifm_market_data_owner_query_v1(
                    handle,
                    invalid.as_ptr(),
                    invalid.len() as u32,
                    query_output.as_mut_ptr(),
                    4096,
                    &mut query_required
                ),
                1
            );
            assert_eq!(query_required, 999);
            assert!(query_output.iter().all(|v| *v == 0xa5));
        }
        let mut retention = NativeRetiredEvidence::default();
        assert_eq!(
            ifm_market_data_owner_retired_evidence_v1(handle, 39, &mut retention),
            3
        );
        assert_eq!(retention.size, 0);
        assert_eq!(
            ifm_market_data_owner_retired_evidence_v1(handle, 40, std::ptr::null_mut()),
            1
        );
        assert_eq!(
            ifm_market_data_owner_retired_evidence_v1(handle, 40, &mut retention),
            0
        );
        assert_eq!(
            (
                retention.size,
                retention.retained,
                retention.generations,
                retention.records,
                retention.bytes
            ),
            (40, 0, 0, 0, 0)
        );
        let mut required = 999u32;
        let mut tiny = [0xabu8; 4];
        assert_eq!(
            ifm_market_data_owner_backend_status_v1(handle, 2, tiny.as_mut_ptr(), 4, &mut required),
            1
        );
        assert_eq!(required, 999);
        assert_eq!(tiny, [0xab; 4]);
        assert_eq!(
            ifm_market_data_owner_backend_status_v1(handle, 1, tiny.as_mut_ptr(), 4, &mut required),
            4
        );
        assert!(required > 4 && required <= 65536);
        assert_eq!(tiny, [0xab; 4]);
        let mut status = vec![0u8; 65536];
        assert_eq!(
            ifm_market_data_owner_backend_status_v1(
                handle,
                1,
                status.as_mut_ptr(),
                65536,
                &mut required
            ),
            0
        );
        let copied = rmpv::decode::read_value(&mut status[..required as usize].as_ref()).unwrap();
        assert_eq!(copied.as_array().unwrap().len(), 10);
        assert!(copied.as_array().unwrap()[9].is_nil());
        assert_eq!(copied.as_array().unwrap()[2].as_array().unwrap().len(), 3);
        let mut identity = NativeOwnerIdentity::default();
        assert_eq!(
            ifm_market_data_owner_identity_v1(handle, 1, 55, &mut identity),
            3
        );
        assert_eq!(identity.size, 0);
        assert_eq!(
            ifm_market_data_owner_identity_v1(handle, 1, 56, &mut identity),
            0
        );
        assert_eq!(identity.size, 56);
        assert_eq!(identity.flags, 0);
        assert_eq!(identity.lane, 1);
        let mut health = NativeOwnerHealth::default();
        assert_eq!(
            ifm_market_data_owner_health_v1(handle, 1, 63, &mut health),
            3
        );
        assert_eq!(
            health.size, 0,
            "invalid query must not modify caller output"
        );
        assert_eq!(
            ifm_market_data_owner_health_v1(handle, 4, 64, &mut health),
            1
        );
        assert_eq!(
            ifm_market_data_owner_health_v1(handle, 1, 64, &mut health),
            0
        );
        assert_eq!(health.size, 64);
        assert_eq!(health.lane, 1);
        assert!(health.ticket.iter().all(|n| *n > 0));
        let mut state = ifm_market_data_native::ffi::publication::NativePublication::from_state(
            ifm_market_data_native::ffi::live::NativeMarketEvent::default(),
            Default::default(),
        );
        assert_eq!(
            ifm_market_data_owner_state_v1(handle, 1, 42, 3, 655, &mut state),
            3
        );
        assert_eq!(
            state.source.size, 0,
            "invalid output layout must preserve caller storage"
        );
        assert_eq!(
            ifm_market_data_owner_state_v1(handle, 1, 42, 3, 656, &mut state),
            0
        );
        assert_eq!(state.source.size, 128);
        assert_eq!(state.flags, 0);
        assert_eq!(state.source.kind, 0);

        let mut contracts_required = 99;
        for lane in [1, 2] {
            assert_eq!(
                ifm_market_data_owner_contracts_v1(
                    handle,
                    lane,
                    std::ptr::null_mut(),
                    0,
                    &mut contracts_required
                ),
                0
            );
            assert_eq!(
                contracts_required, 0,
                "idle owner has no current resolved contracts"
            );
        }
        for (lane, capacity) in [(3, 0), (4, 0), (1, 1), (1, 1048577)] {
            contracts_required = 99;
            assert_eq!(
                ifm_market_data_owner_contracts_v1(
                    handle,
                    lane,
                    std::ptr::null_mut(),
                    capacity,
                    &mut contracts_required
                ),
                1
            );
            assert_eq!(contracts_required, 99, "invalid query preserves output");
        }
        assert_eq!(
            ifm_market_data_owner_contracts_v1(
                handle,
                1,
                std::ptr::null_mut(),
                0,
                std::ptr::null_mut()
            ),
            1
        );

        let cache_query = serde_json::json!({"status":false,"scope":"trade-1","generation":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],"date":739915,"horizon":"Daily","deadline":10000000,"deadline_offset":0,"configuration":"fixture","strategy":"","strategy_version":0,"root":"ES","required":[],"fund_schema":null,"age":1000,"skew":250});
        let query = serde_json::to_vec(&cache_query).unwrap();
        let mut query_required = 99;
        assert_eq!(
            ifm_market_data_owner_prepared_cache_v1(
                handle,
                query.as_ptr(),
                query.len() as u32,
                std::ptr::null_mut(),
                0,
                &mut query_required
            ),
            4
        );
        assert!(query_required > 0 && query_required < 128);
        let mut result = [0u8; 128];
        assert_eq!(
            ifm_market_data_owner_prepared_cache_v1(
                handle,
                query.as_ptr(),
                query.len() as u32,
                result.as_mut_ptr(),
                128,
                &mut query_required
            ),
            0
        );
        let decoded = rmpv::decode::read_value(&mut &result[..query_required as usize]).unwrap();
        assert_eq!(
            decoded.as_array().unwrap()[1].as_str(),
            Some("ScopeNotReady")
        );
        for (input, length, output, capacity) in [
            (std::ptr::null(), 0, result.as_mut_ptr(), 128),
            (query.as_ptr(), query.len() as u32, std::ptr::null_mut(), 1),
            (query.as_ptr(), 65537, result.as_mut_ptr(), 128),
            (
                query.as_ptr(),
                query.len() as u32,
                result.as_mut_ptr(),
                4198401,
            ),
        ] {
            query_required = 99;
            assert_eq!(
                ifm_market_data_owner_prepared_cache_v1(
                    handle,
                    input,
                    length,
                    output,
                    capacity,
                    &mut query_required
                ),
                1
            );
            assert_eq!(query_required, 99);
        }

        let scope = b"trade-1";
        let mut cache_required = 99;
        assert_eq!(
            ifm_market_data_owner_cache_v1(
                handle,
                scope.as_ptr(),
                scope.len() as u32,
                std::ptr::null_mut(),
                0,
                &mut cache_required
            ),
            0
        );
        assert_eq!(cache_required, 0, "idle scope must not invent a cache");
        cache_required = 99;
        assert_eq!(
            ifm_market_data_owner_cache_v1(
                handle,
                scope.as_ptr(),
                scope.len() as u32,
                std::ptr::null_mut(),
                1,
                &mut cache_required
            ),
            1
        );
        assert_eq!(
            cache_required, 99,
            "invalid output must leave caller storage unchanged"
        );
        assert_eq!(
            ifm_market_data_owner_cache_v1(
                handle,
                scope.as_ptr(),
                513,
                std::ptr::null_mut(),
                0,
                &mut cache_required
            ),
            1
        );
        assert_eq!(
            ifm_market_data_owner_cache_v1(
                handle,
                [255u8].as_ptr(),
                1,
                std::ptr::null_mut(),
                0,
                &mut cache_required
            ),
            1
        );
        assert_eq!(
            ifm_market_data_owner_cache_v1(
                handle,
                b" ".as_ptr(),
                1,
                std::ptr::null_mut(),
                0,
                &mut cache_required
            ),
            1
        );

        let mut page_length = 777;
        assert_eq!(
            ifm_market_data_owner_current_v1(
                handle,
                3,
                0,
                256,
                std::ptr::null_mut(),
                0,
                &mut page_length
            ),
            0
        );
        assert_eq!(page_length, 0);
        page_length = 777;
        assert_eq!(
            ifm_market_data_owner_current_v1(
                handle,
                3,
                -1,
                256,
                std::ptr::null_mut(),
                0,
                &mut page_length
            ),
            1
        );
        assert_eq!(page_length, 777);
        assert_eq!(
            ifm_market_data_owner_current_v1(
                handle,
                3,
                0,
                257,
                std::ptr::null_mut(),
                0,
                &mut page_length
            ),
            1
        );
        assert_eq!(page_length, 777);
        assert_eq!(ifm_market_data_owner_wait_v1(handle, 991, 3, 2), 1);
        let mut greek_length = 99;
        assert_eq!(
            ifm_market_data_owner_quote_greeks_v1(
                handle,
                42,
                3,
                std::ptr::null_mut(),
                0,
                &mut greek_length
            ),
            0
        );
        assert_eq!(greek_length, 0, "idle owner must not invent quote Greeks");
        greek_length = 99;
        assert_eq!(
            ifm_market_data_owner_quote_greeks_v1(
                handle,
                0,
                3,
                std::ptr::null_mut(),
                0,
                &mut greek_length
            ),
            1
        );
        assert_eq!(greek_length, 99);
        assert_eq!(
            ifm_market_data_owner_quote_greeks_v1(
                handle,
                42,
                65536,
                std::ptr::null_mut(),
                0,
                &mut greek_length
            ),
            1
        );
        assert_eq!(
            ifm_market_data_owner_quote_greeks_v1(
                handle,
                42,
                3,
                std::ptr::null_mut(),
                16385,
                &mut greek_length
            ),
            1
        );
        greek_length = 777;
        assert_eq!(
            ifm_market_data_owner_trade_greeks_v1(
                handle,
                42,
                3,
                std::ptr::null_mut(),
                0,
                &mut greek_length
            ),
            0
        );
        assert_eq!(greek_length, 0);
        greek_length = 777;
        assert_eq!(
            ifm_market_data_owner_trade_greeks_v1(
                handle,
                0,
                3,
                std::ptr::null_mut(),
                0,
                &mut greek_length
            ),
            1
        );
        assert_eq!(greek_length, 777);
        assert_eq!(
            ifm_market_data_owner_trade_greeks_v1(
                handle,
                42,
                65536,
                std::ptr::null_mut(),
                0,
                &mut greek_length
            ),
            1
        );
        assert_eq!(
            ifm_market_data_owner_trade_greeks_v1(
                handle,
                42,
                3,
                std::ptr::null_mut(),
                16385,
                &mut greek_length
            ),
            1
        );

        let mut raw_length = 99;
        let contract = b"ES20261218";
        assert_eq!(
            ifm_market_data_owner_raw_prices_v1(
                handle,
                1,
                contract.as_ptr(),
                contract.len() as u32,
                739915,
                std::ptr::null_mut(),
                0,
                &mut raw_length
            ),
            0
        );
        assert_eq!(raw_length, 0, "idle owner must not invent raw prices");
        raw_length = 99;
        assert_eq!(
            ifm_market_data_owner_raw_prices_v1(
                handle,
                1,
                contract.as_ptr(),
                contract.len() as u32,
                3652059,
                std::ptr::null_mut(),
                0,
                &mut raw_length
            ),
            1
        );
        assert_eq!(raw_length, 99);

        for input in [
            b"{}".as_slice(),
            br#"{"command":"unknown"}"#.as_slice(),
            br#"{"command":"enable","owner":[0,0],"scopes":[]}"#.as_slice(),
        ] {
            assert_eq!(
                ifm_market_data_owner_command_v1(handle, input.as_ptr(), input.len() as u32),
                1
            );
        }
        let oversized = vec![b' '; 2 * 1024 * 1024 + 1];
        assert_eq!(
            ifm_market_data_owner_command_v1(handle, oversized.as_ptr(), oversized.len() as u32),
            1
        );
        assert_eq!(
            ifm_market_data_owner_command_v1(handle, std::ptr::null(), 0),
            1
        );
        let stop = br#"{"command":"stop"}"#;
        assert_eq!(
            ifm_market_data_owner_command_v1(handle, stop.as_ptr(), stop.len() as u32),
            0
        );
        let mut needed = 0;
        assert_eq!(
            ifm_market_data_owner_failures_v1(handle, std::ptr::null_mut(), 0, &mut needed),
            4
        );
        assert_eq!(needed, 2);
        let mut bytes = [0u8; 64];
        assert_eq!(
            ifm_market_data_owner_failures_v1(
                handle,
                bytes.as_mut_ptr(),
                bytes.len() as u32,
                &mut needed
            ),
            0
        );
        assert_eq!(&bytes[..needed as usize], b"[]");
        assert_eq!(ifm_market_data_owner_failure_ack_v1(handle, 3, 1), 1);
        // Create the persistent notification subtree before root stop. A
        // retained command-directory child must not form a wait/release cycle.
        let futures=br#"{"date":20261027,"definition":20261027,"scope":"development","revision":1,"source":{"key":"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","host":"127.0.0.1","port":9,"dns":["127.0.0.1:1"],"evidence":"control","subscriptions":[{"symbols":["42"],"schema":"mbp-1","symbology":"instrument_id"}],"registrations":[{"instrument":42,"publisher":3,"contract":"ES20261218","source":"42","asset":"futures","tick":250000000,"vwap":false,"required":true}]},"broker":{"endpoint":"nats://127.0.0.1:9","dns":["127.0.0.1:1"]}}"#;
        assert_eq!(
            ifm_market_data_owner_futures_v1(handle, 1, futures.as_ptr(), futures.len() as u32),
            0
        );
        assert_eq!(
            ifm_market_data_owner_identity_v1(handle, 1, 56, &mut identity),
            0
        );
        assert_eq!(
            identity.flags & 2,
            2,
            "accepted demand is not inferred from idle health"
        );
        assert_eq!(
            ifm_market_data_owner_command_v1(handle, stop.as_ptr(), stop.len() as u32),
            0
        );
        assert_eq!(
            ifm_market_data_owner_identity_v1(handle, 1, 56, &mut identity),
            0
        );
        assert_eq!(
            identity.flags & 2,
            0,
            "retained publication identity must not keep Live Feed enabled"
        );

        assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
        let (finished, completion) = std::sync::mpsc::channel();
        let waiter = std::thread::spawn(move || {
            let mut available = 1;
            // SAFETY: The runtime remains alive; output is exclusive thread storage.
            let status = ifm_market_data_runtime_wait_v1(handle, &mut available);
            finished.send((status, available)).unwrap();
        });
        let (status, available) = completion
            .recv_timeout(std::time::Duration::from_secs(3))
            .expect("root stop must retire persistent notification ownership before release");
        waiter.join().unwrap();
        assert_eq!(status, 0);
        assert_eq!(
            available, 0,
            "native wait proves service exit before runtime release"
        );
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
        assert_eq!(ifm_market_data_owner_create_v1(handle), 6);
    }
}
