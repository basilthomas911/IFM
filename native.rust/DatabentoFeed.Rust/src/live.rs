use std::{
    num::NonZeroU64,
    path::{Path, PathBuf},
    time::Duration,
};

use databento::{
    HistoricalClient, LiveClient,
    dbn::{
        BboMsg, Compression, Encoding, ErrorCode, ErrorMsg, InstrumentDefMsg, MboMsg, Mbp1Msg,
        OhlcvMsg, Record, RecordHeader, RecordRef, SType, Schema, StatMsg, SymbolMappingMsg,
        SystemCode, SystemMsg, TradeMsg, UNDEF_PRICE, UNDEF_TIMESTAMP, VersionUpgradePolicy,
        decode::{DbnMetadata, DecodeRecordRef, DynDecoder},
    },
    historical::{
        batch::{Delivery, DownloadParams, JobState, SplitDuration, SubmitJobParams},
        metadata::GetQueryParams,
        timeseries::GetRangeParams,
    },
    live::{SlowReaderBehavior, Subscription, TimeoutConf},
};

use crate::abi::*;
use crate::engine::{Feed, Mapping};
use time::OffsetDateTime;

#[derive(Debug)]
struct LiveFailure {
    status: Status,
    message: String,
}

fn keeps_live_session_open(code: SystemCode) -> bool {
    matches!(code, SystemCode::SlowReaderWarning)
}

pub(crate) struct ContractData {
    pub detail: ContractDetailV1,
    pub strings: [Vec<u8>; 9],
}

impl From<databento::Error> for LiveFailure {
    fn from(error: databento::Error) -> Self {
        let status = if matches!(error, databento::Error::HeartbeatTimeout(_)) {
            CONNECTION_HUNG
        } else {
            DATABENTO_ERROR
        };
        Self {
            status,
            message: error.to_string(),
        }
    }
}

pub(crate) fn run_feed(feed: &Feed) {
    let runtime = match tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()
    {
        Ok(runtime) => runtime,
        Err(error) => {
            feed.fail_live(OS_ERROR, error.to_string());
            return;
        }
    };
    if let Err(error) = runtime.block_on(run_feed_async(feed)) {
        feed.fail_live(error.status, error.message);
    }
}

async fn run_feed_async(feed: &Feed) -> Result<(), LiveFailure> {
    let remaining = feed.remaining_start_milliseconds();
    if remaining == 0 {
        return Err(failure(
            TIMEOUT,
            "Databento start deadline expired before connect",
        ));
    }
    let timeout_seconds = u64::from(remaining.div_ceil(1_000).max(1));
    let dataset = String::from_utf8(feed.dataset.clone())
        .map_err(|error| failure(INVALID_ARGUMENT, error.to_string()))?;
    let timeout = time::Duration::seconds(timeout_seconds as i64);
    let mut client = LiveClient::builder()
        .key_from_env()
        .map_err(LiveFailure::from)?
        .dataset(dataset)
        .heartbeat_interval(time::Duration::milliseconds(i64::from(
            feed.config.heartbeat_interval_ms,
        )))
        .slow_reader_behavior(SlowReaderBehavior::Warn)
        .timeout_conf(TimeoutConf {
            connect: Some(timeout),
            auth: Some(timeout),
        })
        .build()
        .await
        .map_err(LiveFailure::from)?;

    let mappings = feed.mappings_snapshot();
    let mut expected_acknowledgements = 0u32;
    let mut statistics_replay_pending = feed.config.statistics_replay_start_ns != 0
        && mappings
            .iter()
            .any(|mapping| mapping.data_kinds & MARKET_DATA_STATISTICS != 0);
    let mut trade_replay_pending = feed.config.trade_replay_start_ns != 0
        && mappings
            .iter()
            .any(|mapping| mapping.data_kinds & MARKET_DATA_SESSION_VOLUME != 0);
    for input_symbology in [1u32, 2u32] {
        subscribe_group(
            &mut client,
            &mappings,
            input_symbology,
            MARKET_DATA_QUOTE,
            Schema::Mbp1,
            0,
            0,
        )
        .await?;
        expected_acknowledgements += u32::from(group_exists(
            &mappings,
            input_symbology,
            MARKET_DATA_QUOTE,
            0,
        ));
        subscribe_group(
            &mut client,
            &mappings,
            input_symbology,
            MARKET_DATA_TRADE,
            Schema::Trades,
            0,
            MARKET_DATA_SESSION_VOLUME,
        )
        .await?;
        expected_acknowledgements += u32::from(group_exists(
            &mappings,
            input_symbology,
            MARKET_DATA_TRADE,
            MARKET_DATA_SESSION_VOLUME,
        ));
        subscribe_group(
            &mut client,
            &mappings,
            input_symbology,
            MARKET_DATA_SESSION_VOLUME,
            Schema::Trades,
            feed.config.trade_replay_start_ns,
            0,
        )
        .await?;
        expected_acknowledgements += u32::from(group_exists(
            &mappings,
            input_symbology,
            MARKET_DATA_SESSION_VOLUME,
            0,
        ));
        subscribe_group(
            &mut client,
            &mappings,
            input_symbology,
            MARKET_DATA_MBO,
            Schema::Mbo,
            0,
            0,
        )
        .await?;
        expected_acknowledgements +=
            u32::from(group_exists(&mappings, input_symbology, MARKET_DATA_MBO, 0));
        subscribe_group(
            &mut client,
            &mappings,
            input_symbology,
            MARKET_DATA_STATISTICS,
            Schema::Statistics,
            feed.config.statistics_replay_start_ns,
            0,
        )
        .await?;
        expected_acknowledgements += u32::from(group_exists(
            &mappings,
            input_symbology,
            MARKET_DATA_STATISTICS,
            0,
        ));
    }
    let metadata = client.start().await.map_err(LiveFailure::from)?;
    if !metadata.not_found.is_empty() || !metadata.partial.is_empty() {
        let _ = client.close().await;
        return Err(failure(
            SYMBOL_RESOLUTION_FAILED,
            "Databento could not resolve one or more ticker symbols",
        ));
    }

    let mut acknowledgements = 0u32;
    while !feed.stop_requested()
        && (!feed.all_mappings_resolved() || acknowledgements < expected_acknowledgements)
    {
        let remaining = feed.remaining_start_milliseconds();
        if remaining == 0 {
            let _ = client.close().await;
            return Err(failure(
                TIMEOUT,
                "Databento ticker mappings or acknowledgements timed out",
            ));
        }
        if let Some(record) = next_with_poll(&mut client, remaining.min(250)).await? {
            process_record(
                feed,
                record,
                true,
                &mut acknowledgements,
                &mut statistics_replay_pending,
                &mut trade_replay_pending,
            )?;
        }
    }

    feed.enter_consumer_setup();
    feed.wait_for_consumer();
    while !feed.stop_requested()
        && feed.state.load(std::sync::atomic::Ordering::Acquire) != STATE_FAULTED
    {
        if let Some(record) = next_with_poll(&mut client, 250).await? {
            process_record(
                feed,
                record,
                false,
                &mut acknowledgements,
                &mut statistics_replay_pending,
                &mut trade_replay_pending,
            )?;
        }
    }
    let _ = client.close().await;
    Ok(())
}

fn group_exists(
    mappings: &[Mapping],
    input_symbology: u32,
    data_kind: u32,
    excluded_data_kind: u32,
) -> bool {
    mappings.iter().any(|mapping| {
        mapping.input_symbology == input_symbology
            && mapping.data_kinds & data_kind != 0
            && mapping.data_kinds & excluded_data_kind == 0
    })
}

async fn subscribe_group(
    client: &mut LiveClient,
    mappings: &[Mapping],
    input_symbology: u32,
    data_kind: u32,
    schema: Schema,
    replay_start_ns: u64,
    excluded_data_kind: u32,
) -> Result<(), LiveFailure> {
    let symbols: Vec<String> = mappings
        .iter()
        .filter(|mapping| {
            mapping.input_symbology == input_symbology
                && mapping.data_kinds & data_kind != 0
                && mapping.data_kinds & excluded_data_kind == 0
        })
        .map(|mapping| {
            String::from_utf8(mapping.requested_symbol.clone())
                .map_err(|error| failure(INVALID_ARGUMENT, error.to_string()))
        })
        .collect::<Result<_, _>>()?;
    if !symbols.is_empty() {
        let subscription = if replay_start_ns == 0 {
            Subscription::builder()
                .symbols(symbols)
                .schema(schema)
                .stype_in(to_stype(input_symbology))
                .build()
        } else {
            let start = OffsetDateTime::from_unix_timestamp_nanos(i128::from(replay_start_ns))
                .map_err(|error| failure(INVALID_ARGUMENT, error.to_string()))?;
            Subscription::builder()
                .symbols(symbols)
                .schema(schema)
                .stype_in(to_stype(input_symbology))
                .start(start)
                .build()
        };
        client
            .subscribe(subscription)
            .await
            .map_err(LiveFailure::from)?;
    }
    Ok(())
}

async fn next_with_poll<'a>(
    client: &'a mut LiveClient,
    timeout_ms: u32,
) -> Result<Option<RecordRef<'a>>, LiveFailure> {
    match tokio::time::timeout(
        Duration::from_millis(u64::from(timeout_ms)),
        client.next_record(),
    )
    .await
    {
        Err(_) => Ok(None),
        Ok(Ok(record)) => Ok(record),
        Ok(Err(error)) => Err(LiveFailure::from(error)),
    }
}

fn process_record(
    feed: &Feed,
    source: RecordRef<'_>,
    initial_mapping: bool,
    acknowledgements: &mut u32,
    statistics_replay_pending: &mut bool,
    trade_replay_pending: &mut bool,
) -> Result<(), LiveFailure> {
    if let Some(error) = source.get::<ErrorMsg>() {
        return Err(failure(
            classify_gateway_error(error.code().unwrap_or(ErrorCode::Unset)),
            error.err().unwrap_or("Databento gateway error"),
        ));
    }
    if let Some(system) = source.get::<SystemMsg>() {
        match system.code().unwrap_or(SystemCode::Unset) {
            SystemCode::SubscriptionAck => *acknowledgements = acknowledgements.saturating_add(1),
            SystemCode::ReplayCompleted => {
                match classify_replay_schema(system.msg().unwrap_or_default()) {
                    Some(Schema::Statistics) if *statistics_replay_pending => {
                        *statistics_replay_pending = false;
                        publish_statistics_replay_complete(feed)?;
                    }
                    Some(Schema::Trades) if *trade_replay_pending => {
                        *trade_replay_pending = false;
                        publish_trade_replay_complete(feed)?;
                    }
                    _ => {}
                }
            }
            SystemCode::SlowReaderWarning => {
                // Advisory only. Keep draining the live transport; health and
                // watchdog telemetry may report the warning independently.
                if !keeps_live_session_open(SystemCode::SlowReaderWarning) {
                    return Err(failure(
                        DATABENTO_ERROR,
                        "Databento reported a terminal system message",
                    ));
                }
            }
            _ => {}
        }
        return Ok(());
    }
    if let Some(mapping) = source.get::<SymbolMappingMsg>() {
        let requested = mapping
            .stype_in_symbol()
            .map_err(|error| failure(DATABENTO_ERROR, error.to_string()))?;
        return feed
            .resolve_mapping(
                requested.as_bytes(),
                mapping.hd.instrument_id,
                mapping.hd.publisher_id,
                initial_mapping,
            )
            .map_err(|(status, message)| failure(status, String::from_utf8_lossy(message)));
    }
    if initial_mapping {
        let header = source.header();
        feed.resolve_mapping_publisher(header.instrument_id, header.publisher_id)
            .map_err(|(status, message)| failure(status, String::from_utf8_lossy(message)))?;
    }
    let trade_replay =
        *trade_replay_pending && feed.is_session_volume_instrument(source.header().instrument_id);
    if let Some(record) = normalize(source, *statistics_replay_pending, trade_replay)
        && !feed.publish_live(record)
    {
        return Err(failure(
            feed.terminal_status(),
            "Native ring publication failed",
        ));
    }
    Ok(())
}

fn classify_replay_schema(message: &str) -> Option<Schema> {
    if message.contains("trades") {
        Some(Schema::Trades)
    } else if message.contains("statistics") {
        Some(Schema::Statistics)
    } else {
        None
    }
}

fn publish_statistics_replay_complete(feed: &Feed) -> Result<(), LiveFailure> {
    for mapping in feed.mappings_snapshot().into_iter().filter(|mapping| {
        mapping.data_kinds & MARKET_DATA_STATISTICS != 0
            && mapping.instrument_id != 0
            && mapping.publisher_id != 0
    }) {
        let record = MarketRecord64 {
            header: RecordHeader32 {
                instrument_id: mapping.instrument_id,
                publisher_id: mapping.publisher_id,
                record_kind: RECORD_STATISTICS_REPLAY_COMPLETE,
                source_schema: Schema::Statistics as u16,
                ..RecordHeader32::default()
            },
        };
        if !feed.publish_live(record) {
            return Err(failure(
                feed.terminal_status(),
                "Native ring publication failed",
            ));
        }
    }
    Ok(())
}

fn publish_trade_replay_complete(feed: &Feed) -> Result<(), LiveFailure> {
    for mapping in feed.mappings_snapshot().into_iter().filter(|mapping| {
        mapping.data_kinds & MARKET_DATA_SESSION_VOLUME != 0
            && mapping.instrument_id != 0
            && mapping.publisher_id != 0
    }) {
        let record = MarketRecord64 {
            header: RecordHeader32 {
                instrument_id: mapping.instrument_id,
                publisher_id: mapping.publisher_id,
                record_kind: RECORD_TRADE_REPLAY_COMPLETE,
                source_schema: Schema::Trades as u16,
                ..RecordHeader32::default()
            },
        };
        if !feed.publish_live(record) {
            return Err(failure(
                feed.terminal_status(),
                "Native ring publication failed",
            ));
        }
    }
    Ok(())
}

fn classify_gateway_error(code: ErrorCode) -> Status {
    match code {
        ErrorCode::ConnectionLimitExceeded => CONNECTION_LIMIT,
        ErrorCode::SymbolResolutionFailed => SYMBOL_RESOLUTION_FAILED,
        _ => DATABENTO_ERROR,
    }
}
fn to_stype(value: u32) -> SType {
    if value == 1 {
        SType::RawSymbol
    } else {
        SType::InstrumentId
    }
}
fn failure(status: Status, message: impl ToString) -> LiveFailure {
    LiveFailure {
        status,
        message: message.to_string(),
    }
}

fn fill_header(
    source: &RecordHeader,
    kind: u8,
    ts_recv: u64,
    sequence: u32,
    schema: Schema,
) -> RecordHeader32 {
    RecordHeader32 {
        instrument_id: source.instrument_id,
        publisher_id: source.publisher_id,
        record_kind: kind,
        flags: 0,
        ts_event_ns: source.ts_event as i64,
        ts_recv_ns: ts_recv as i64,
        sequence,
        source_schema: schema as u16,
        reserved: 0,
    }
}
fn price_or_zero(value: i64, flags: &mut u8) -> i64 {
    if value == UNDEF_PRICE {
        *flags |= 4;
        0
    } else {
        value
    }
}

pub(crate) fn normalize(
    source: RecordRef<'_>,
    statistics_replay: bool,
    trade_replay: bool,
) -> Option<MarketRecord64> {
    if let Some(message) = source.get::<Mbp1Msg>() {
        let mut header = fill_header(
            &message.hd,
            RECORD_QUOTE,
            message.ts_recv,
            message.sequence,
            Schema::Mbp1,
        );
        if message.flags.is_snapshot() {
            header.flags |= 1;
        }
        let level = &message.levels[0];
        return Some(MarketRecord64 {
            quote: QuoteRecord64 {
                bid_price: price_or_zero(level.bid_px, &mut header.flags),
                ask_price: price_or_zero(level.ask_px, &mut header.flags),
                header,
                bid_size: level.bid_sz,
                ask_size: level.ask_sz,
                bid_count: level.bid_ct,
                ask_count: level.ask_ct,
            },
        });
    }
    if let Some(message) = source.get::<TradeMsg>() {
        let mut header = fill_header(
            &message.hd,
            RECORD_TRADE,
            message.ts_recv,
            message.sequence,
            Schema::Trades,
        );
        if message.flags.is_snapshot() {
            header.flags |= 1;
        }
        if trade_replay {
            header.flags |= 2;
        }
        return Some(MarketRecord64 {
            trade: TradeRecord64 {
                price: price_or_zero(message.price, &mut header.flags),
                header,
                size: message.size,
                action: message.action as u8,
                side: message.side as u8,
                dbn_flags: message.flags.raw(),
                depth: message.depth,
                ts_in_delta_ns: message.ts_in_delta,
                channel_id: 0,
                reserved8: [0; 3],
                ts_out_ns: 0,
            },
        });
    }
    if let Some(message) = source.get::<MboMsg>() {
        let mut header = fill_header(
            &message.hd,
            RECORD_MBO,
            message.ts_recv,
            message.sequence,
            Schema::Mbo,
        );
        if message.flags.is_snapshot() {
            header.flags |= 1;
        }
        return Some(MarketRecord64 {
            mbo: MboRecord64 {
                order_id: message.order_id,
                price: price_or_zero(message.price, &mut header.flags),
                header,
                size: message.size,
                ts_in_delta_ns: message.ts_in_delta,
                action: message.action as u8,
                side: message.side as u8,
                dbn_flags: message.flags.raw(),
                channel_id: message.channel_id,
                reserved32: 0,
            },
        });
    }
    if let Some(message) = source.get::<StatMsg>() {
        let mut header = fill_header(
            &message.hd,
            RECORD_STATISTICS,
            message.ts_recv,
            message.sequence,
            Schema::Statistics,
        );
        if statistics_replay {
            header.flags |= 2;
        }
        return Some(MarketRecord64 {
            statistics: StatisticsRecord64 {
                price: price_or_zero(message.price, &mut header.flags),
                header,
                quantity: message.quantity,
                ts_ref_ns: message.ts_ref as i64,
                stat_type: message.stat_type,
                channel_id: message.channel_id,
                update_action: message.update_action,
                stat_flags: message.stat_flags,
                reserved16: 0,
            },
        });
    }
    None
}

pub(crate) fn query_contracts(
    query_kind: u32,
    dataset: String,
    requested: Vec<String>,
    timeout_ms: u32,
) -> Result<Vec<ContractData>, (Status, String)> {
    let runtime = tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()
        .map_err(|error| (OS_ERROR, error.to_string()))?;
    runtime.block_on(async {
        match tokio::time::timeout(
            Duration::from_millis(u64::from(timeout_ms)),
            query_contracts_async(query_kind, dataset, requested),
        )
        .await
        {
            Err(_) => Err((
                TIMEOUT,
                "Databento contract-detail query timed out".to_string(),
            )),
            Ok(Err(error)) => Err((error.status, error.message)),
            Ok(Ok(entries)) => Ok(entries),
        }
    })
}

async fn query_contracts_async(
    query_kind: u32,
    dataset: String,
    requested: Vec<String>,
) -> Result<Vec<ContractData>, LiveFailure> {
    let mut client = HistoricalClient::builder()
        .key_from_env()
        .map_err(LiveFailure::from)?
        .build()
        .map_err(LiveFailure::from)?;
    let dataset_range = client
        .metadata()
        .get_dataset_range(&dataset)
        .await
        .map_err(LiveFailure::from)?;
    let definition_end = dataset_range
        .range_by_schema
        .get(&Schema::Definition)
        .map_or(dataset_range.end, |range| range.end);
    let previous = definition_end
        .date()
        .previous_day()
        .ok_or_else(|| {
            failure(
                DATABENTO_ERROR,
                "Databento returned an invalid definition range",
            )
        })?
        .midnight()
        .assume_utc();
    let input_symbols = if query_kind == CONTRACT_QUERY_TICKER {
        let ticker = &requested[0];
        if ticker.ends_with(".FUT") || ticker.ends_with(".OPT") {
            vec![ticker.clone()]
        } else {
            vec![format!("{ticker}.FUT"), format!("{ticker}.OPT")]
        }
    } else {
        requested.clone()
    };
    let stype = match query_kind {
        CONTRACT_QUERY_TICKER => SType::Parent,
        CONTRACT_QUERY_INSTRUMENT_ID => SType::InstrumentId,
        _ => SType::RawSymbol,
    };
    let mut decoder = client
        .timeseries()
        .get_range(
            &GetRangeParams::builder()
                .dataset(&dataset)
                .date_time_range(previous..definition_end)
                .symbols(input_symbols)
                .schema(Schema::Definition)
                .stype_in(stype)
                .stype_out(SType::InstrumentId)
                .build(),
        )
        .await
        .map_err(LiveFailure::from)?;
    let mut entries = Vec::<ContractData>::new();
    let mut positions = std::collections::HashMap::<Vec<u8>, usize>::new();
    while let Some(definition) = decoder
        .decode_record::<InstrumentDefMsg>()
        .await
        .map_err(|error| failure(DATABENTO_ERROR, error.to_string()))?
    {
        let Some(entry) = contract_data(definition)? else {
            continue;
        };
        let symbol = entry.strings[0].clone();
        if let Some(&position) = positions.get(&symbol) {
            entries[position] = entry;
        } else {
            positions.insert(symbol, entries.len());
            entries.push(entry);
        }
    }
    if query_kind == CONTRACT_QUERY_EXACT {
        let mut by_symbol: std::collections::HashMap<Vec<u8>, ContractData> = entries
            .into_iter()
            .map(|entry| (entry.strings[0].clone(), entry))
            .collect();
        Ok(requested
            .into_iter()
            .map(|symbol| {
                by_symbol
                    .remove(symbol.as_bytes())
                    .unwrap_or_else(missing_contract)
            })
            .collect())
    } else {
        entries.sort_by(|left, right| {
            let left_expiration = if left.detail.flags & 8 != 0 {
                left.detail.expiration_ts_ns
            } else {
                u64::MAX
            };
            let right_expiration = if right.detail.flags & 8 != 0 {
                right.detail.expiration_ts_ns
            } else {
                u64::MAX
            };
            left_expiration
                .cmp(&right_expiration)
                .then(left.detail.contract_kind.cmp(&right.detail.contract_kind))
                .then(left.detail.strike_price.cmp(&right.detail.strike_price))
                .then(left.strings[0].cmp(&right.strings[0]))
        });
        Ok(entries)
    }
}

fn contract_data(source: &InstrumentDefMsg) -> Result<Option<ContractData>, LiveFailure> {
    let contract_kind = match source.instrument_class as u8 {
        b'F' => 1,
        b'C' => 2,
        b'P' => 3,
        _ => return Ok(None),
    };
    let mut detail = ContractDetailV1 {
        struct_size: size_of::<ContractDetailV1>() as u32,
        abi_version: ABI_VERSION,
        flags: 1,
        instrument_id: source.hd.instrument_id,
        publisher_id: source.hd.publisher_id,
        contract_kind,
        maturity_month: source.maturity_month,
        maturity_day: source.maturity_day,
        maturity_week: source.maturity_week,
        maturity_year: source.maturity_year,
        underlying_id: source.underlying_id,
        raw_instrument_id: source.raw_instrument_id,
        ..ContractDetailV1::default()
    };
    if source.contract_multiplier != i32::MAX {
        detail.flags |= 64;
        detail.contract_multiplier = source.contract_multiplier;
    }
    if source.strike_price != UNDEF_PRICE {
        detail.flags |= 2;
        detail.strike_price = source.strike_price;
    }
    if source.min_price_increment != UNDEF_PRICE {
        detail.flags |= 4;
        detail.min_price_increment = source.min_price_increment;
    }
    if source.min_price_increment_amount != UNDEF_PRICE {
        detail.flags |= 128;
        detail.min_price_increment_amount = source.min_price_increment_amount;
    }
    if source.expiration != UNDEF_TIMESTAMP {
        detail.flags |= 8;
        detail.expiration_ts_ns = source.expiration;
    }
    if source.activation != UNDEF_TIMESTAMP {
        detail.flags |= 16;
        detail.activation_ts_ns = source.activation;
    }
    if time::Month::try_from(source.maturity_month)
        .ok()
        .and_then(|month| {
            time::Date::from_calendar_date(
                i32::from(source.maturity_year),
                month,
                source.maturity_day,
            )
            .ok()
        })
        .is_some()
    {
        detail.flags |= 32;
    }
    if source.maturity_week != u8::MAX {
        detail.flags |= 256;
    }
    let convert = |value: databento::dbn::Result<&str>| {
        value
            .map(|text| text.as_bytes().to_vec())
            .map_err(|error| failure(DATABENTO_ERROR, error.to_string()))
    };
    let strings = [
        convert(source.raw_symbol())?,
        convert(source.asset())?,
        convert(source.underlying())?,
        convert(source.currency())?,
        convert(source.settl_currency())?,
        convert(source.exchange())?,
        convert(source.security_type())?,
        convert(source.cfi())?,
        convert(source.unit_of_measure())?,
    ];
    Ok(Some(ContractData { detail, strings }))
}

fn missing_contract() -> ContractData {
    ContractData {
        detail: ContractDetailV1 {
            struct_size: size_of::<ContractDetailV1>() as u32,
            abi_version: ABI_VERSION,
            ..ContractDetailV1::default()
        },
        strings: std::array::from_fn(|_| Vec::new()),
    }
}

pub(crate) fn latest_price(
    request: &LatestPriceRequestV1,
    dataset: String,
    symbol: String,
    timeout_ms: u32,
) -> Result<LatestPriceResult64, Status> {
    let runtime = tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()
        .map_err(|_| OS_ERROR)?;
    runtime.block_on(async {
        match tokio::time::timeout(
            Duration::from_millis(u64::from(timeout_ms)),
            latest_price_async(request, dataset, symbol, timeout_ms),
        )
        .await
        {
            Err(_) => Err(TIMEOUT),
            Ok(Err(error)) => Err(error.status),
            Ok(Ok(result)) => Ok(result),
        }
    })
}

async fn latest_price_async(
    request: &LatestPriceRequestV1,
    dataset: String,
    symbol: String,
    timeout_ms: u32,
) -> Result<LatestPriceResult64, LiveFailure> {
    let timeout = time::Duration::seconds(i64::from(timeout_ms.div_ceil(1_000).max(1)));
    let mut client = LiveClient::builder()
        .key_from_env()
        .map_err(LiveFailure::from)?
        .dataset(dataset)
        .heartbeat_interval(time::Duration::seconds(5))
        .slow_reader_behavior(SlowReaderBehavior::Warn)
        .timeout_conf(TimeoutConf {
            connect: Some(timeout),
            auth: Some(timeout),
        })
        .build()
        .await
        .map_err(LiveFailure::from)?;
    let schema = if request.selected_policy == LATEST_PRICE_LAST_TRADE {
        Schema::Trades
    } else {
        Schema::Bbo1S
    };
    let builder = Subscription::builder()
        .symbols(symbol)
        .schema(schema)
        .stype_in(to_stype(request.input_symbology));
    if request.freshness_policy == LATEST_PRICE_REPLAY_LOOKBACK_THEN_LIVE {
        client
            .subscribe(
                builder
                    .start(
                        time::OffsetDateTime::now_utc()
                            - time::Duration::milliseconds(i64::from(request.replay_lookback_ms)),
                    )
                    .build(),
            )
            .await
            .map_err(LiveFailure::from)?;
    } else {
        client
            .subscribe(builder.build())
            .await
            .map_err(LiveFailure::from)?;
    }
    let metadata = client.start().await.map_err(LiveFailure::from)?;
    if !metadata.not_found.is_empty() || !metadata.partial.is_empty() {
        let _ = client.close().await;
        return Err(failure(
            SYMBOL_RESOLUTION_FAILED,
            "Databento could not resolve the latest-price symbol",
        ));
    }
    let replay_requested = request.freshness_policy == LATEST_PRICE_REPLAY_LOOKBACK_THEN_LIVE;
    let mut replay_complete = !replay_requested;
    let mut replay_candidate: Option<LatestPriceResult64> = None;
    loop {
        let Some(record) = client.next_record().await.map_err(LiveFailure::from)? else {
            return Err(failure(
                TIMEOUT,
                "Databento latest-price session closed before a usable record",
            ));
        };
        if let Some(error) = record.get::<ErrorMsg>() {
            return Err(failure(
                classify_gateway_error(error.code().unwrap_or(ErrorCode::Unset)),
                error.err().unwrap_or("Databento gateway error"),
            ));
        }
        if let Some(system) = record.get::<SystemMsg>() {
            match system.code().unwrap_or(SystemCode::Unset) {
                SystemCode::SlowReaderWarning => {
                    return Err(failure(
                        DATABENTO_ERROR,
                        "Databento reported a slow-reader warning",
                    ));
                }
                SystemCode::ReplayCompleted => {
                    replay_complete = true;
                    if let Some(mut candidate) = replay_candidate {
                        candidate.flags |= 8;
                        let _ = client.close().await;
                        return Ok(candidate);
                    }
                }
                _ => {}
            }
            continue;
        }
        let mut candidate = LatestPriceResult64 {
            selected_policy: request.selected_policy as u8,
            ..LatestPriceResult64::default()
        };
        let selected = if request.selected_policy == LATEST_PRICE_LAST_TRADE {
            record
                .get::<TradeMsg>()
                .is_some_and(|trade| select_trade(trade, &mut candidate))
        } else {
            record
                .get::<BboMsg>()
                .is_some_and(|quote| select_quote(quote, request.selected_policy, &mut candidate))
        };
        if !selected {
            continue;
        }
        if !replay_complete {
            replay_candidate = Some(candidate);
            continue;
        }
        candidate.flags |= 16;
        let _ = client.close().await;
        return Ok(candidate);
    }
}

fn set_latest_header(header: &RecordHeader, ts_recv: u64, result: &mut LatestPriceResult64) {
    result.instrument_id = header.instrument_id;
    result.publisher_id = header.publisher_id;
    result.ts_event_ns = header.ts_event as i64;
    result.ts_recv_ns = ts_recv as i64;
}

fn select_trade(trade: &TradeMsg, result: &mut LatestPriceResult64) -> bool {
    if trade.price == UNDEF_PRICE || trade.size == 0 {
        return false;
    }
    set_latest_header(&trade.hd, trade.ts_recv, result);
    result.flags = 4;
    result.selected_price = trade.price;
    result.last_trade_price = trade.price;
    true
}

fn select_quote(quote: &BboMsg, policy: u32, result: &mut LatestPriceResult64) -> bool {
    let level = &quote.levels[0];
    let bid = level.bid_px != UNDEF_PRICE && level.bid_sz != 0;
    let ask = level.ask_px != UNDEF_PRICE && level.ask_sz != 0;
    let midpoint = bid && ask && level.bid_px <= level.ask_px;
    let valid = if policy == LATEST_PRICE_BID {
        bid
    } else if policy == LATEST_PRICE_ASK {
        ask
    } else {
        midpoint
    };
    if !valid {
        return false;
    }
    set_latest_header(&quote.hd, quote.ts_recv, result);
    result.flags = u8::from(bid) | (u8::from(ask) << 1);
    result.bid_price = if bid { level.bid_px } else { 0 };
    result.ask_price = if ask { level.ask_px } else { 0 };
    result.bid_size = if bid { level.bid_sz } else { 0 };
    result.ask_size = if ask { level.ask_sz } else { 0 };
    result.selected_price = if policy == LATEST_PRICE_BID {
        level.bid_px
    } else if policy == LATEST_PRICE_ASK {
        level.ask_px
    } else {
        (i128::from(level.bid_px) + (i128::from(level.ask_px) - i128::from(level.bid_px)) / 2)
            as i64
    };
    true
}

fn historical_schema(schema: u32) -> Result<Schema, Status> {
    match schema {
        HISTORICAL_DEFINITION => Ok(Schema::Definition),
        HISTORICAL_OHLCV_1M => Ok(Schema::Ohlcv1M),
        HISTORICAL_TRADES => Ok(Schema::Trades),
        HISTORICAL_STATISTICS => Ok(Schema::Statistics),
        HISTORICAL_OHLCV_1D => Ok(Schema::Ohlcv1D),
        _ => Err(INVALID_ARGUMENT),
    }
}

fn historical_stype(stype: u32) -> Result<SType, Status> {
    match stype {
        1 => Ok(SType::RawSymbol),
        2 => Ok(SType::Continuous),
        3 => Ok(SType::InstrumentId),
        _ => Err(INVALID_ARGUMENT),
    }
}

fn historical_schema_id(schema: Schema) -> u32 {
    match schema {
        Schema::Definition => HISTORICAL_DEFINITION,
        Schema::Ohlcv1M => HISTORICAL_OHLCV_1M,
        Schema::Trades => HISTORICAL_TRADES,
        Schema::Statistics => HISTORICAL_STATISTICS,
        Schema::Ohlcv1D => HISTORICAL_OHLCV_1D,
        _ => 0,
    }
}

fn historical_range(
    request: &HistoricalRequestV1,
) -> Result<std::ops::Range<OffsetDateTime>, Status> {
    let start = OffsetDateTime::from_unix_timestamp_nanos(request.start_ts_ns.into())
        .map_err(|_| INVALID_ARGUMENT)?;
    let end = OffsetDateTime::from_unix_timestamp_nanos(request.end_ts_ns.into())
        .map_err(|_| INVALID_ARGUMENT)?;
    Ok(start..end)
}

fn json_escape(value: &str) -> String {
    value.chars().fold(String::new(), |mut output, character| {
        match character {
            '\\' => output.push_str("\\\\"),
            '"' => output.push_str("\\\""),
            '\n' => output.push_str("\\n"),
            '\r' => output.push_str("\\r"),
            '\t' => output.push_str("\\t"),
            other => output.push(other),
        }
        output
    })
}

fn job_state(state: JobState) -> &'static str {
    match state {
        JobState::Queued => "Queued",
        JobState::Processing => "Processing",
        JobState::Done => "Completed",
        JobState::Expired => "Expired",
    }
}

fn job_payload(job: &databento::historical::batch::BatchJob) -> Vec<u8> {
    format!(
        "{{\"providerJobId\":\"{}\",\"state\":\"{}\",\"costUsd\":{},\"recordCount\":{},\"billedBytes\":{},\"progressPercent\":{}}}",
        json_escape(&job.id),
        job_state(job.state),
        job.cost_usd.unwrap_or(0.0),
        job.record_count.unwrap_or(0),
        job.billed_size.unwrap_or(0),
        job.progress.unwrap_or(u8::from(job.state == JobState::Done) * 100),
    ).into_bytes()
}

fn run_historical<T>(
    timeout_ms: u32,
    operation: impl std::future::Future<Output = Result<T, LiveFailure>>,
) -> Result<T, (Status, String)> {
    let runtime = tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()
        .map_err(|error| (OS_ERROR, error.to_string()))?;
    runtime.block_on(async {
        match tokio::time::timeout(Duration::from_millis(u64::from(timeout_ms)), operation).await {
            Err(_) => Err((
                TIMEOUT,
                "Databento historical request timed out".to_string(),
            )),
            Ok(Err(error)) => Err((error.status, error.message)),
            Ok(Ok(value)) => Ok(value),
        }
    })
}

pub(crate) fn historical_estimate(
    request: HistoricalRequestV1,
    dataset: String,
    symbols: Vec<String>,
) -> Result<HistoricalEstimateV1, (Status, String)> {
    run_historical(request.timeout_ms, async move {
        let mut client = HistoricalClient::builder()
            .key_from_env()
            .map_err(LiveFailure::from)?
            .build()
            .map_err(LiveFailure::from)?;
        let params = GetQueryParams::builder()
            .dataset(dataset)
            .symbols(symbols)
            .schema(
                historical_schema(request.schema)
                    .map_err(|status| failure(status, "Invalid historical schema"))?,
            )
            .stype_in(
                historical_stype(request.input_symbology)
                    .map_err(|status| failure(status, "Invalid historical symbology"))?,
            )
            .date_time_range(
                historical_range(&request)
                    .map_err(|status| failure(status, "Invalid historical range"))?,
            )
            .maybe_limit(NonZeroU64::new(request.record_limit))
            .build();
        let estimated_cost_usd = client
            .metadata()
            .get_cost(&params)
            .await
            .map_err(LiveFailure::from)?;
        let estimated_bytes = client
            .metadata()
            .get_billable_size(&params)
            .await
            .map_err(LiveFailure::from)?;
        let estimated_records = client
            .metadata()
            .get_record_count(&params)
            .await
            .map_err(LiveFailure::from)?;
        Ok(HistoricalEstimateV1 {
            struct_size: std::mem::size_of::<HistoricalEstimateV1>() as u32,
            abi_version: ABI_VERSION,
            estimated_cost_usd,
            estimated_bytes,
            estimated_records,
        })
    })
}

pub(crate) fn historical_batch_submit(
    request: HistoricalRequestV1,
    dataset: String,
    symbols: Vec<String>,
) -> Result<Vec<u8>, (Status, String)> {
    run_historical(request.timeout_ms, async move {
        let mut client = HistoricalClient::builder()
            .key_from_env()
            .map_err(LiveFailure::from)?
            .build()
            .map_err(LiveFailure::from)?;
        let params = SubmitJobParams::builder()
            .dataset(dataset)
            .symbols(symbols)
            .schema(
                historical_schema(request.schema)
                    .map_err(|status| failure(status, "Invalid historical schema"))?,
            )
            .date_time_range(
                historical_range(&request)
                    .map_err(|status| failure(status, "Invalid historical range"))?,
            )
            .encoding(Encoding::Dbn)
            .compression(Compression::Zstd)
            .split_duration(SplitDuration::Month)
            .delivery(Delivery::Download)
            .stype_in(
                historical_stype(request.input_symbology)
                    .map_err(|status| failure(status, "Invalid historical symbology"))?,
            )
            .stype_out(SType::InstrumentId)
            .maybe_limit(NonZeroU64::new(request.record_limit))
            .build();
        let job = client
            .batch()
            .submit_job(&params)
            .await
            .map_err(LiveFailure::from)?;
        Ok(job_payload(&job))
    })
}

pub(crate) fn historical_batch_status(
    job_id: String,
    timeout_ms: u32,
) -> Result<Vec<u8>, (Status, String)> {
    run_historical(timeout_ms, async move {
        let mut client = HistoricalClient::builder()
            .key_from_env()
            .map_err(LiveFailure::from)?
            .build()
            .map_err(LiveFailure::from)?;
        let job = client
            .batch()
            .get_job_details(&job_id)
            .await
            .map_err(LiveFailure::from)?;
        Ok(job_payload(&job))
    })
}

pub(crate) fn historical_batch_files(
    job_id: String,
    timeout_ms: u32,
) -> Result<Vec<u8>, (Status, String)> {
    run_historical(timeout_ms, async move {
        let mut client = HistoricalClient::builder()
            .key_from_env()
            .map_err(LiveFailure::from)?
            .build()
            .map_err(LiveFailure::from)?;
        let job = client
            .batch()
            .get_job_details(&job_id)
            .await
            .map_err(LiveFailure::from)?;
        let schema = historical_schema_id(job.schema);
        let files = client
            .batch()
            .list_files(&job_id)
            .await
            .map_err(LiveFailure::from)?;
        let entries = files.into_iter()
            .filter(|file| file.filename.ends_with(".dbn") || file.filename.ends_with(".dbn.zst"))
            .map(|file| format!(
                "{{\"providerFileId\":\"{}\",\"fileName\":\"{}\",\"schema\":{},\"sizeBytes\":{},\"sha256\":\"{}\"}}",
                json_escape(&file.filename), json_escape(&file.filename), schema, file.size,
                json_escape(file.hash.strip_prefix("sha256:").unwrap_or(&file.hash))))
            .collect::<Vec<_>>().join(",");
        Ok(format!("{{\"files\":[{entries}]}}").into_bytes())
    })
}

pub(crate) fn historical_batch_download(
    job_id: String,
    file_name: String,
    destination: PathBuf,
    timeout_ms: u32,
) -> Result<(), (Status, String)> {
    run_historical(timeout_ms, async move {
        let parent = destination.parent().unwrap_or_else(|| Path::new("."));
        std::fs::create_dir_all(parent).map_err(|error| failure(OS_ERROR, error.to_string()))?;
        let mut client = HistoricalClient::builder()
            .key_from_env()
            .map_err(LiveFailure::from)?
            .build()
            .map_err(LiveFailure::from)?;
        let params = DownloadParams::builder()
            .output_dir(parent)
            .job_id(&job_id)
            .filename_to_download(&file_name)
            .build();
        let downloaded = client
            .batch()
            .download(&params)
            .await
            .map_err(LiveFailure::from)?;
        let source = downloaded
            .into_iter()
            .next()
            .ok_or_else(|| failure(DATABENTO_ERROR, "Databento returned no downloaded file"))?;
        if source != destination {
            if destination.exists() {
                std::fs::remove_file(&destination)
                    .map_err(|error| failure(OS_ERROR, error.to_string()))?;
            }
            std::fs::rename(source, destination)
                .map_err(|error| failure(OS_ERROR, error.to_string()))?;
        }
        Ok(())
    })
}

fn historical_record(
    source: RecordRef<'_>,
    schema: u32,
    symbol: &str,
    ordinal: u64,
) -> Option<HistoricalRecord120> {
    let mut record = HistoricalRecord120 {
        struct_size: std::mem::size_of::<HistoricalRecord120>() as u32,
        abi_version: ABI_VERSION,
        schema,
        ..HistoricalRecord120::default()
    };
    if let Some(value) = source.get::<OhlcvMsg>() {
        record.record_kind = HISTORICAL_RECORD_OHLCV;
        record.instrument_id = value.hd.instrument_id;
        record.publisher_id = value.hd.publisher_id;
        record.event_ts_ns = value.hd.ts_event as i64;
        record.source_sequence = ordinal as i64 + 1;
        record.open_price = value.open;
        record.high_price = value.high;
        record.low_price = value.low;
        record.close_or_trade_price = value.close;
        record.volume_or_size = value.volume;
    } else if let Some(value) = source.get::<TradeMsg>() {
        record.record_kind = HISTORICAL_RECORD_TRADE;
        record.instrument_id = value.hd.instrument_id;
        record.publisher_id = value.hd.publisher_id;
        record.event_ts_ns = value.hd.ts_event as i64;
        record.source_sequence = i64::from(value.sequence);
        record.close_or_trade_price = value.price;
        record.volume_or_size = u64::from(value.size);
        record.action = value.action as u8;
        record.side = value.side as u8;
    } else {
        return None;
    }
    let bytes = symbol.as_bytes();
    let length = bytes.len().min(record.symbol.len() - 1);
    record.symbol[..length].copy_from_slice(&bytes[..length]);
    Some(record)
}

pub(crate) fn historical_range_records(
    request: HistoricalRequestV1,
    dataset: String,
    symbols: Vec<String>,
) -> Result<Vec<HistoricalRecord120>, (Status, String)> {
    run_historical(request.timeout_ms, async move {
        let fallback = symbols.first().cloned().unwrap_or_default();
        let mut client = HistoricalClient::builder()
            .key_from_env()
            .map_err(LiveFailure::from)?
            .build()
            .map_err(LiveFailure::from)?;
        let params = GetRangeParams::builder()
            .dataset(dataset)
            .symbols(symbols)
            .schema(
                historical_schema(request.schema)
                    .map_err(|status| failure(status, "Invalid historical schema"))?,
            )
            .stype_in(
                historical_stype(request.input_symbology)
                    .map_err(|status| failure(status, "Invalid historical symbology"))?,
            )
            .stype_out(SType::InstrumentId)
            .date_time_range(
                historical_range(&request)
                    .map_err(|status| failure(status, "Invalid historical range"))?,
            )
            .maybe_limit(NonZeroU64::new(request.record_limit))
            .build();
        let mut decoder = client
            .timeseries()
            .get_range(&params)
            .await
            .map_err(LiveFailure::from)?;
        let mut records = Vec::new();
        while let Some(source) = decoder
            .decode_record_ref()
            .await
            .map_err(|error| failure(DATABENTO_ERROR, error.to_string()))?
        {
            if let Some(record) =
                historical_record(source, request.schema, &fallback, records.len() as u64)
            {
                records.push(record);
            }
        }
        Ok(records)
    })
}

pub(crate) fn historical_file_records(
    path: &Path,
    schema: u32,
) -> Result<Vec<HistoricalRecord120>, (Status, String)> {
    if !path.is_file() {
        return Err((
            INVALID_ARGUMENT,
            "Historical DBN file does not exist".to_string(),
        ));
    }
    let mut decoder = DynDecoder::from_file(path, VersionUpgradePolicy::AsIs)
        .map_err(|error| (DATABENTO_ERROR, error.to_string()))?;
    let fallback = decoder
        .metadata()
        .symbols
        .first()
        .cloned()
        .or_else(|| {
            path.file_stem()
                .map(|value| value.to_string_lossy().into_owned())
        })
        .unwrap_or_default();
    let mut records = Vec::new();
    while let Some(source) = decoder
        .decode_record_ref()
        .map_err(|error| (DATABENTO_ERROR, error.to_string()))?
    {
        if let Some(record) = historical_record(source, schema, &fallback, records.len() as u64) {
            records.push(record);
        }
    }
    Ok(records)
}

#[cfg(test)]
mod tests {
    use databento::dbn::{FlagSet, RecordRef, flags};

    use super::*;

    #[test]
    fn normalizes_quote_trade_mbo_and_statistics_like_the_cpp_reference() {
        assert!(keeps_live_session_open(SystemCode::SlowReaderWarning));
        assert_eq!(
            classify_replay_schema("Finished trades replay"),
            Some(Schema::Trades)
        );
        assert_eq!(
            classify_replay_schema("Finished statistics replay"),
            Some(Schema::Statistics)
        );
        assert_eq!(classify_replay_schema("Finished mbp-1 replay"), None);

        let mut quote = Mbp1Msg::default();
        quote.hd.instrument_id = 42;
        quote.hd.publisher_id = 7;
        quote.hd.ts_event = 100;
        quote.ts_recv = 110;
        quote.sequence = 3;
        quote.flags = FlagSet::new(flags::SNAPSHOT);
        quote.levels[0].bid_px = 101_000_000_000;
        quote.levels[0].ask_px = 101_001_000_000;
        quote.levels[0].bid_sz = 4;
        quote.levels[0].ask_sz = 5;
        quote.levels[0].bid_ct = 2;
        quote.levels[0].ask_ct = 3;
        let normalized = normalize(RecordRef::from(&quote), false, false).expect("quote");
        let normalized = unsafe { normalized.quote };
        assert_eq!(normalized.header.record_kind, RECORD_QUOTE);
        assert_eq!(normalized.header.flags, 1);
        assert_eq!(normalized.bid_price, quote.levels[0].bid_px);
        assert_eq!(normalized.ask_count, 3);

        let mut trade = TradeMsg::default();
        trade.hd.instrument_id = 43;
        trade.price = 102_000_000_000;
        trade.size = 6;
        trade.action = b'T' as _;
        trade.side = b'B' as _;
        trade.flags = FlagSet::new(flags::LAST);
        trade.depth = 1;
        trade.ts_in_delta = -12;
        trade.sequence = 4;
        let normalized = normalize(RecordRef::from(&trade), false, false).expect("trade");
        let normalized = unsafe { normalized.trade };
        assert_eq!(normalized.header.record_kind, RECORD_TRADE);
        assert_eq!(normalized.price, trade.price);
        assert_eq!(normalized.dbn_flags, flags::LAST);
        assert_eq!(normalized.ts_in_delta_ns, -12);
        let replayed = normalize(RecordRef::from(&trade), false, true).expect("replayed trade");
        assert_eq!(unsafe { replayed.trade }.header.flags & 2, 2);

        let mut mbo = MboMsg::default();
        mbo.hd.instrument_id = 44;
        mbo.order_id = 99;
        mbo.price = UNDEF_PRICE;
        mbo.size = 8;
        mbo.action = b'A' as _;
        mbo.side = b'A' as _;
        mbo.channel_id = 2;
        mbo.sequence = 5;
        let normalized = normalize(RecordRef::from(&mbo), false, false).expect("mbo");
        let normalized = unsafe { normalized.mbo };
        assert_eq!(normalized.header.record_kind, RECORD_MBO);
        assert_eq!(normalized.header.flags & 4, 4);
        assert_eq!(normalized.price, 0);
        assert_eq!(normalized.order_id, 99);

        let mut statistics = StatMsg::default();
        statistics.hd.instrument_id = 45;
        statistics.hd.publisher_id = 7;
        statistics.hd.ts_event = 120;
        statistics.ts_recv = 130;
        statistics.ts_ref = 125;
        statistics.price = 104_000_000_000;
        statistics.quantity = 987_654_321;
        statistics.sequence = 10;
        statistics.ts_in_delta = 21;
        statistics.stat_type = 5;
        statistics.channel_id = 3;
        statistics.update_action = 1;
        statistics.stat_flags = 7;
        let normalized = normalize(RecordRef::from(&statistics), true, false).expect("statistics");
        let normalized = unsafe { normalized.statistics };
        assert_eq!(normalized.header.record_kind, RECORD_STATISTICS);
        assert_eq!(normalized.header.flags & 2, 2);
        assert_eq!(normalized.price, statistics.price);
        assert_eq!(normalized.quantity, 987_654_321);
        assert_eq!(normalized.ts_ref_ns, 125);
        assert_eq!(normalized.stat_type, 5);
        assert_eq!(normalized.update_action, 1);
        assert_eq!(normalized.stat_flags, 7);
    }

    #[test]
    fn latest_price_selection_matches_cpp_policies_and_overflow_behavior() {
        let mut quote = BboMsg::default_for_schema(Schema::Bbo1S);
        quote.hd.instrument_id = 55;
        quote.hd.publisher_id = 9;
        quote.hd.ts_event = 200;
        quote.ts_recv = 210;
        quote.levels[0].bid_px = i64::MIN + 10;
        quote.levels[0].ask_px = i64::MAX - 10;
        quote.levels[0].bid_sz = 2;
        quote.levels[0].ask_sz = 3;
        let mut result = LatestPriceResult64::default();
        assert!(select_quote(
            &quote,
            LATEST_PRICE_QUOTE_MIDPOINT,
            &mut result
        ));
        assert_eq!(result.selected_price, -1);
        assert_eq!(result.flags, 3);

        let mut trade = TradeMsg::default();
        trade.hd.instrument_id = 56;
        trade.price = 123_000_000_000;
        trade.size = 4;
        let mut result = LatestPriceResult64::default();
        assert!(select_trade(&trade, &mut result));
        assert_eq!(result.selected_price, trade.price);
        assert_eq!(result.flags, 4);
    }

    #[test]
    fn definition_mapping_preserves_contract_flags() {
        let mut definition = InstrumentDefMsg::default();
        definition.hd.instrument_id = 100;
        definition.hd.publisher_id = 2;
        definition.instrument_class = b'F' as _;
        definition.maturity_year = 2026;
        definition.maturity_month = 9;
        definition.maturity_day = 18;
        definition.maturity_week = 3;
        definition.contract_multiplier = 50;
        definition.strike_price = UNDEF_PRICE;
        definition.min_price_increment = 250_000_000;
        definition.min_price_increment_amount = UNDEF_PRICE;
        definition.expiration = 123;
        definition.activation = UNDEF_TIMESTAMP;
        let mapped = contract_data(&definition)
            .expect("valid definition")
            .expect("future definition");
        assert_eq!(mapped.detail.contract_kind, 1);
        assert_eq!(mapped.detail.instrument_id, 100);
        assert_eq!(mapped.detail.flags & 64, 64);
        assert_eq!(mapped.detail.flags & 32, 32);
        assert_eq!(mapped.detail.flags & 2, 0);
        assert_eq!(mapped.detail.flags & 8, 8);
    }
}
