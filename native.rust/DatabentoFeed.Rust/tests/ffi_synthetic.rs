#![cfg(windows)]

use std::ptr;

use databento_feed_native::*;

fn config(record_count: u32, ring_records: u64) -> FeedConfigV1 {
    FeedConfigV1 {
        struct_size: size_of::<FeedConfigV1>() as u32,
        abi_version: ABI_VERSION,
        data_source: DATA_SOURCE_SYNTHETIC,
        feed_kind: FEED_TICKER,
        ring_memory_bytes: ring_records * size_of::<MarketRecord64>() as u64,
        spin_iterations: 100,
        ring_full_timeout_us: 2_000,
        synthetic_record_count: record_count,
        synthetic_instrument_count: 2,
        producer_logical_processor: UNPINNED_PROCESSOR,
        drain_logical_processor: UNPINNED_PROCESSOR,
        producer_alternate_logical_processor: UNPINNED_PROCESSOR,
        drain_alternate_logical_processor: UNPINNED_PROCESSOR,
        numa_node: UNPINNED_PROCESSOR,
        ..FeedConfigV1::default()
    }
}

unsafe fn create_subscribed(
    record_count: u32,
    ring_records: u64,
) -> *mut databento_feed_native::engine::Feed {
    let mut feed = ptr::null_mut();
    let config = config(record_count, ring_records);
    assert_eq!(
        unsafe { dbf_feed_create(&config, ptr::null(), 0, &mut feed) },
        OK
    );
    let blob = b"ESM6VXM6";
    let subscriptions = [
        TickerSubscriptionV1 {
            struct_size: size_of::<TickerSubscriptionV1>() as u32,
            abi_version: ABI_VERSION,
            symbol_offset: 0,
            symbol_length: 4,
            input_symbology: 1,
            data_kinds: MARKET_DATA_QUOTE
                | MARKET_DATA_TRADE
                | MARKET_DATA_MBO
                | MARKET_DATA_STATISTICS,
            reserved: 0,
        },
        TickerSubscriptionV1 {
            struct_size: size_of::<TickerSubscriptionV1>() as u32,
            abi_version: ABI_VERSION,
            symbol_offset: 4,
            symbol_length: 4,
            input_symbology: 1,
            data_kinds: MARKET_DATA_QUOTE | MARKET_DATA_TRADE,
            reserved: 0,
        },
    ];
    assert_eq!(
        unsafe {
            dbf_feed_subscribe_tickers(
                feed,
                subscriptions.as_ptr(),
                subscriptions.len() as u32,
                blob.as_ptr(),
                blob.len() as u32,
                2_000,
            )
        },
        OK
    );
    feed
}

#[test]
fn layouts_and_version_match_the_c_header() {
    assert_eq!(dbf_get_abi_version(), 3);
    assert_eq!(size_of::<RecordHeader32>(), 32);
    assert_eq!(size_of::<StatisticsRecord64>(), 64);
    assert_eq!(size_of::<MarketRecord64>(), 64);
    assert_eq!(size_of::<FeedConfigV1>(), 128);
    assert_eq!(size_of::<StatsV1>(), 128);
    assert_eq!(size_of::<WatchdogSnapshotV1>(), 64);
    assert_eq!(size_of::<WatchdogFeedStatusV1>(), 320);
    assert_eq!(size_of::<ContractDetailV1>(), 192);
    assert_eq!(size_of::<LatestPriceRequestV1>(), 88);
    assert_eq!(size_of::<HistoricalRequestV1>(), 64);
    assert_eq!(size_of::<HistoricalEstimateV1>(), 32);
    assert_eq!(size_of::<HistoricalRecord120>(), 120);
    assert_eq!(size_of::<HistoricalBatchV1>(), 24);
}

#[test]
fn process_wide_watchdog_snapshot_is_complete() {
    unsafe {
        let feed = create_subscribed(8, 128);
        let mut snapshot = WatchdogSnapshotV1 {
            struct_size: size_of::<WatchdogSnapshotV1>() as u32,
            abi_version: ABI_VERSION,
            ..WatchdogSnapshotV1::default()
        };
        assert_eq!(dbf_get_watchdog_snapshot_v1(&mut snapshot, ptr::null_mut(), 0), BUFFER_TOO_SMALL);
        assert!(snapshot.required_count >= 1);
        let mut entries = Vec::new();
        loop {
            entries.resize(snapshot.required_count as usize, WatchdogFeedStatusV1::default());
            for entry in &mut entries {
                entry.struct_size = size_of::<WatchdogFeedStatusV1>() as u32;
                entry.abi_version = ABI_VERSION;
            }
            if dbf_get_watchdog_snapshot_v1(&mut snapshot, entries.as_mut_ptr(), entries.len() as u32) == OK {
                break;
            }
        }
        assert_eq!(snapshot.entry_count, snapshot.required_count);
        assert!(entries.iter().any(|entry| entry.feed_instance_id != 0
            && entry.expected_subscriptions == 2 && entry.major_status == MAJOR_DOWN));
        assert_eq!(dbf_feed_destroy(feed.cast()), OK);
    }
}

#[test]
fn synthetic_historical_abi_matches_cpp_results() {
    unsafe {
        let blob = b"GLBX.MDP3ES.c.0";
        let request = HistoricalRequestV1 {
            struct_size: size_of::<HistoricalRequestV1>() as u32,
            abi_version: ABI_VERSION,
            schema: HISTORICAL_OHLCV_1D,
            input_symbology: 2,
            flags: HISTORICAL_SYNTHETIC,
            symbol_count: 1,
            dataset: Utf8SliceV1 {
                offset: 0,
                length: 9,
            },
            start_ts_ns: 1_770_000_000_000_000_000,
            end_ts_ns: 1_770_086_400_000_000_000,
            record_limit: 10,
            timeout_ms: 1_000,
            ..HistoricalRequestV1::default()
        };
        let symbol = Utf8SliceV1 {
            offset: 9,
            length: 6,
        };
        let mut estimate = HistoricalEstimateV1 {
            struct_size: size_of::<HistoricalEstimateV1>() as u32,
            abi_version: ABI_VERSION,
            ..HistoricalEstimateV1::default()
        };
        assert_eq!(
            dbf_historical_estimate(
                &request,
                &symbol,
                blob.as_ptr(),
                blob.len() as u32,
                &mut estimate,
            ),
            OK
        );
        assert!(estimate.estimated_records > 0);
        assert_eq!(
            estimate.estimated_bytes,
            estimate.estimated_records * size_of::<HistoricalRecord120>() as u64
        );
        assert_eq!(estimate.estimated_cost_usd, 0.0);

        let mut result = ptr::null_mut();
        assert_eq!(
            dbf_historical_range_open(
                &request,
                &symbol,
                blob.as_ptr(),
                blob.len() as u32,
                &mut result,
            ),
            OK
        );
        let mut records = [HistoricalRecord120::default(); 2];
        let mut batch = HistoricalBatchV1 {
            struct_size: size_of::<HistoricalBatchV1>() as u32,
            abi_version: ABI_VERSION,
            ..HistoricalBatchV1::default()
        };
        assert_eq!(
            dbf_historical_result_get_next_batch(
                result,
                records.as_mut_ptr(),
                records.len() as u32,
                &mut batch,
            ),
            OK
        );
        assert_eq!(batch.records_read, 2);
        assert_eq!(batch.more_available, 0);
        assert_eq!(records[0].abi_version, ABI_VERSION);
        assert_eq!(records[0].record_kind, HISTORICAL_RECORD_OHLCV);
        assert_eq!(records[0].instrument_id, 1000);
        assert_eq!(&records[0].symbol[..5], b"SYNTH");
        assert_eq!(records[1].source_sequence, 2);
        assert_eq!(dbf_historical_result_destroy(result), OK);
    }
}

#[test]
fn synthetic_feed_preserves_lifecycle_mappings_and_order() {
    unsafe {
        let feed = create_subscribed(256, 512);
        let mut buffer = ptr::null_mut();
        assert_eq!(
            dbf_feed_allocate_read_buffer64(feed.cast(), 64, &mut buffer),
            OK
        );
        assert_eq!(dbf_feed_start(feed.cast(), 2_000), OK);

        let mut mapping_count = 0;
        let mut mapping_bytes = 0;
        assert_eq!(
            dbf_feed_get_ticker_mapping_counts(feed.cast(), &mut mapping_count, &mut mapping_bytes),
            OK
        );
        assert_eq!(mapping_count, 2);
        assert_eq!(mapping_bytes, 16);
        let mut mappings = vec![TickerInstrumentMappingV1::default(); mapping_count as usize];
        let mut mapping_blob = vec![0; mapping_bytes as usize];
        assert_eq!(
            dbf_feed_copy_ticker_mappings(
                feed.cast(),
                mappings.as_mut_ptr(),
                mapping_count,
                mapping_blob.as_mut_ptr(),
                mapping_bytes
            ),
            OK
        );
        assert_eq!(mappings[0].instrument_id, 1);
        assert_eq!(mappings[1].instrument_id, 2);

        let mut pre_activation = StatsV1 {
            struct_size: size_of::<StatsV1>() as u32,
            abi_version: ABI_VERSION,
            ..StatsV1::default()
        };
        assert_eq!(dbf_feed_get_stats(feed.cast(), &mut pre_activation), OK);
        assert_eq!(pre_activation.state, STATE_CONSUMER_SETUP);
        assert_eq!(pre_activation.records_produced, 0);
        assert_eq!(pre_activation.ring_used_records, 0);
        assert_eq!(pre_activation.ring_high_water_records, 0);

        assert_eq!(dbf_feed_set_consumer_ready(feed.cast(), 2_000), OK);
        let mut expected = 1u64;
        let mut observed_statistics = [false; 3];
        loop {
            let mut wait = WaitResultV1 {
                struct_size: size_of::<WaitResultV1>() as u32,
                abi_version: ABI_VERSION,
                ..WaitResultV1::default()
            };
            assert_eq!(dbf_feed_wait(feed.cast(), 5_000, &mut wait), OK);
            if wait.available_records != 0 {
                loop {
                    let mut batch = BatchResultV1 {
                        struct_size: size_of::<BatchResultV1>() as u32,
                        abi_version: ABI_VERSION,
                        ..BatchResultV1::default()
                    };
                    assert_eq!(
                        dbf_feed_read_batch64(feed.cast(), buffer, 64, &mut batch),
                        OK
                    );
                    for index in 0..batch.records_read as usize {
                        let record = *buffer.add(index);
                        assert_eq!(record.header.sequence as u64, expected);
                        if record.header.record_kind == RECORD_STATISTICS {
                            let statistic = record.statistics;
                            match statistic.stat_type {
                                1 => observed_statistics[0] = true,
                                4 => observed_statistics[1] = true,
                                5 => observed_statistics[2] = true,
                                value => panic!("unexpected synthetic statistic type {value}"),
                            }
                        }
                        expected += 1;
                    }
                    if batch.more_available == 0 {
                        break;
                    }
                }
            }
            if wait.flags & WAIT_TERMINAL != 0 {
                break;
            }
        }
        assert_eq!(expected, 257);
        assert_eq!(observed_statistics, [true, true, true]);
        let mut stats = StatsV1 {
            struct_size: size_of::<StatsV1>() as u32,
            abi_version: ABI_VERSION,
            ..StatsV1::default()
        };
        assert_eq!(dbf_feed_get_stats(feed.cast(), &mut stats), OK);
        assert_eq!(stats.records_produced, 256);
        assert_eq!(stats.records_consumed, 256);
        assert_eq!(dbf_feed_stop(feed.cast(), 2_000), OK);
        assert_eq!(dbf_feed_free_read_buffer64(feed.cast(), buffer), OK);
        assert_eq!(dbf_feed_destroy(feed.cast()), OK);
    }
}

#[test]
fn registered_buffer_and_ring_overrun_match_cpp_behavior() {
    unsafe {
        let feed = create_subscribed(10_000, 8);
        let mut buffer = ptr::null_mut();
        assert_eq!(
            dbf_feed_allocate_read_buffer64(feed.cast(), 8, &mut buffer),
            OK
        );
        assert_eq!(dbf_feed_start(feed.cast(), 2_000), OK);
        assert_eq!(dbf_feed_set_consumer_ready(feed.cast(), 2_000), OK);
        std::thread::sleep(std::time::Duration::from_millis(20));
        let mut wait = WaitResultV1 {
            struct_size: size_of::<WaitResultV1>() as u32,
            abi_version: ABI_VERSION,
            ..WaitResultV1::default()
        };
        for _ in 0..20 {
            assert_eq!(dbf_feed_wait(feed.cast(), 1_000, &mut wait), OK);
            if wait.flags & WAIT_FAULT != 0 {
                break;
            }
        }
        let mut stats = StatsV1 {
            struct_size: size_of::<StatsV1>() as u32,
            abi_version: ABI_VERSION,
            ..StatsV1::default()
        };
        assert_eq!(dbf_feed_get_stats(feed.cast(), &mut stats), OK);
        assert_eq!(stats.terminal_status, RING_OVERRUN);
        assert_eq!(stats.ring_overruns, 1);
        assert_eq!(dbf_feed_stop(feed.cast(), 2_000), OK);
        assert_eq!(dbf_feed_free_read_buffer64(feed.cast(), buffer), OK);
        assert_eq!(dbf_feed_destroy(feed.cast()), OK);
    }
}

#[test]
#[cfg(not(feature = "live"))]
fn non_live_operations_match_cpp_status_and_error_contracts() {
    unsafe {
        let blob = b"GLBX.MDP3ESM6";
        let query = ContractQueryV1 {
            struct_size: size_of::<ContractQueryV1>() as u32,
            abi_version: ABI_VERSION,
            query_kind: CONTRACT_QUERY_EXACT,
            timeout_ms: 1_000,
            dataset_offset: 0,
            dataset_length: 9,
            symbol_count: 1,
            ..ContractQueryV1::default()
        };
        let symbol = Utf8SliceV1 {
            offset: 9,
            length: 4,
        };
        let mut result = ptr::null_mut();
        assert_eq!(
            dbf_contract_details_query(
                &query,
                &symbol,
                blob.as_ptr(),
                blob.len() as u32,
                &mut result
            ),
            NOT_SUPPORTED
        );
        let mut required = 0;
        assert_eq!(
            dbf_contract_details_result_get_error(result, ptr::null_mut(), 0, &mut required),
            BUFFER_TOO_SMALL
        );
        assert!(required > 1);
        assert_eq!(dbf_contract_details_result_destroy(result), OK);

        let request = LatestPriceRequestV1 {
            struct_size: size_of::<LatestPriceRequestV1>() as u32,
            abi_version: ABI_VERSION,
            selected_policy: LATEST_PRICE_LAST_TRADE,
            freshness_policy: LATEST_PRICE_NEXT_OBSERVED,
            input_symbology: 1,
            dataset: Utf8SliceV1 {
                offset: 0,
                length: 9,
            },
            symbol: Utf8SliceV1 {
                offset: 9,
                length: 4,
            },
            utf8_blob: blob.as_ptr(),
            utf8_blob_bytes: blob.len() as u32,
            ..LatestPriceRequestV1::default()
        };
        let mut price = LatestPriceResult64::default();
        assert_eq!(
            dbf_get_latest_price(&request, 1_000, &mut price),
            NOT_SUPPORTED
        );
    }
}
