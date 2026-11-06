//! Immutable query retention, stale-close isolation and ABI atomicity.
use ifm_market_data_native::catalog::{
    Definition,
    queries::{Error, Queries, Query},
    repository::Repository,
};
use std::sync::Arc;
fn source() -> String {
    let corpus: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
    corpus[0]["json"].as_str().unwrap().to_owned()
}
#[test]
fn pinned_rows_survive_refresh_and_query_capacity_is_released_without_stale_aliases() {
    let source = source();
    let mut repository = Repository::new(2, source.len() * 2).unwrap();
    let refresh = repository.begin().unwrap();
    repository.append(refresh, "GLBX.MDP3", &source).unwrap();
    repository.commit(refresh).unwrap();
    let mut queries = Queries::default();
    let (first, count) = queries.open(repository.snapshot(), Query::All).unwrap();
    assert_eq!(count, 1);
    let refresh = repository.begin().unwrap();
    repository.commit(refresh).unwrap();
    assert_eq!(queries.row(first, 0).unwrap().json.as_ref(), source);
    let row = Arc::new(Definition::parse("GLBX.MDP3", &source).unwrap());
    let mut tokens = vec![first];
    for _ in 0..3 {
        tokens.push(queries.open(vec![row.clone()], Query::All).unwrap().0);
    }
    assert_eq!(
        queries.open(vec![row.clone()], Query::All),
        Err(Error::Capacity)
    );
    for token in tokens {
        queries.close(token).unwrap();
    }
    for _ in 0..1000 {
        let (token, _) = queries.open(vec![row.clone()], Query::All).unwrap();
        assert!(token > first);
        assert_eq!(queries.close(first), Err(Error::Invalid));
        assert_eq!(queries.row(token, 0).unwrap(), row);
        assert_eq!(queries.row(token, 1), Err(Error::Invalid));
        queries.close(token).unwrap();
    }
}
#[test]
fn query_abi_failure_preserves_buffers_and_invalidates_only_closed_identities() {
    use ifm_market_data_native::ffi::{catalog::*, catalog_query::*, runtime::*};
    assert_eq!(std::mem::size_of::<NativeCatalogQuery>(), 24);
    let json = source();
    let mut handle = 0;
    let mut token = 0;
    let mut revision = 0;
    let request = br#"{"kind":"all","dataset":"GLBX.MDP3"}"#;
    // SAFETY: All accepted slices/outputs are live, bounded, aligned and exclusive.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 1, &mut handle), 0);
        assert_eq!(ifm_market_data_catalog_configure_v1(handle, 2, 131_072), 0);
        assert_eq!(ifm_market_data_catalog_begin_v1(handle, &mut token), 0);
        assert_eq!(
            ifm_market_data_catalog_append_v1(
                handle,
                token,
                b"GLBX.MDP3".as_ptr(),
                9,
                json.as_ptr(),
                json.len() as u32
            ),
            0
        );
        assert_eq!(
            ifm_market_data_catalog_finish_v1(handle, token, 1, &mut revision),
            0
        );
        let mut result = NativeCatalogQuery {
            token: 999,
            ..Default::default()
        };
        for len in 0..request.len() {
            assert_ne!(
                ifm_market_data_catalog_query_v1(
                    handle,
                    revision,
                    request.as_ptr(),
                    len as u32,
                    &mut result
                ),
                0
            );
            assert_eq!(result.token, 999);
        }
        assert_eq!(
            ifm_market_data_catalog_query_v1(
                handle,
                revision,
                request.as_ptr(),
                request.len() as u32,
                &mut result
            ),
            0
        );
        assert_eq!((result.size, result.count), (24, 1));
        let mut bytes = vec![77; 262_144];
        let mut length = 999;
        assert_eq!(
            ifm_market_data_catalog_query_row_v1(
                handle,
                result.token,
                1,
                0,
                bytes.as_mut_ptr(),
                262_144,
                &mut length
            ),
            1
        );
        assert_eq!((bytes[0], length), (77, 999));
        assert_eq!(
            ifm_market_data_catalog_query_row_v1(
                handle,
                result.token,
                0,
                0,
                bytes.as_mut_ptr(),
                1,
                &mut length
            ),
            4
        );
        assert_eq!((bytes[0], length), (77, 999));
        assert_eq!(
            ifm_market_data_catalog_query_row_v1(
                handle,
                result.token,
                0,
                0,
                bytes.as_mut_ptr(),
                262_144,
                &mut length
            ),
            0
        );
        assert_eq!(&bytes[..length as usize], json.as_bytes());
        assert_eq!(
            ifm_market_data_catalog_query_row_v1(
                handle,
                result.token,
                0,
                1,
                bytes.as_mut_ptr(),
                262_144,
                &mut length
            ),
            0
        );
        let projection: serde_json::Value =
            serde_json::from_slice(&bytes[..length as usize]).unwrap();
        assert_eq!(projection["Instrument"], 42);
        assert_eq!(projection["Strike"], serde_json::Value::Null);
        assert_eq!(projection["CanonicalId"], "ES20261225");
        bytes.fill(77);
        length = 999;
        let mut rows = 999;
        assert_eq!(
            ifm_market_data_catalog_query_page_v1(
                handle,
                result.token,
                0,
                65,
                0,
                bytes.as_mut_ptr(),
                262_144,
                &mut length,
                &mut rows,
            ),
            1
        );
        assert_eq!((bytes[0], length, rows), (77, 999, 999));
        assert_eq!(
            ifm_market_data_catalog_query_page_v1(
                handle,
                result.token,
                0,
                1,
                0,
                bytes.as_mut_ptr(),
                4,
                &mut length,
                &mut rows,
            ),
            4
        );
        assert_eq!((bytes[0], length, rows), (77, 999, 999));
        assert_eq!(
            ifm_market_data_catalog_query_page_v1(
                handle,
                result.token,
                0,
                64,
                0,
                bytes.as_mut_ptr(),
                262_144,
                &mut length,
                &mut rows,
            ),
            0
        );
        assert_eq!(rows, 1);
        assert_eq!(length as usize, json.len() + 4);
        assert_eq!(
            u32::from_le_bytes(bytes[..4].try_into().unwrap()) as usize,
            json.len()
        );
        assert_eq!(&bytes[4..length as usize], json.as_bytes());
        assert_eq!(
            ifm_market_data_catalog_query_close_v1(handle, result.token),
            0
        );
        assert_eq!(
            ifm_market_data_catalog_query_page_v1(
                handle,
                result.token,
                0,
                1,
                0,
                bytes.as_mut_ptr(),
                262_144,
                &mut length,
                &mut rows,
            ),
            1
        );
        assert_eq!(
            ifm_market_data_catalog_query_close_v1(handle, result.token),
            1
        );
        assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
    }
}

fn legacy_projection(row: &Definition) -> Vec<u8> {
    use ifm_market_data_native::catalog::selection;

    let value = serde_json::json!({
        "Dataset": row.dataset, "Publisher": row.publisher, "Instrument": row.instrument,
        "Symbol": row.symbol, "Asset": row.asset, "Kind": row.kind, "Class":row.class, "Currency": row.currency,
        "Exchange": row.exchange, "SettlementCurrency": row.settlement_currency,
        "Underlying": row.underlying, "UnderlyingInstrument": row.underlying_instrument,
        "ReceivedNanoseconds": row.received_ns, "EventNanoseconds": row.event_ns,
        "Deleted": row.deleted, "Activation": row.activation, "Expiration": row.expiration,
        "Maturity": row.maturity.map(|v| v.to_string()), "Strike": row.strike,
        "Tick": row.tick, "TickAmount": row.tick_amount, "Multiplier": row.multiplier,
        "MaturityWeek": row.maturity_week, "CanonicalId": selection::format(row).ok(),
        "QueryMaturity": row.query_maturity.map(|v| v.to_string()),
        "QueryMaturityInvalid": row.query_maturity_invalid,
        "SecurityType": row.security_type, "Cfi": row.cfi, "UnitOfMeasure": row.unit
    });
    serde_json::to_vec(&value).unwrap()
}

fn projection_rows() -> Vec<Definition> {
    let corpus: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
    corpus
        .as_array()
        .unwrap()
        .iter()
        .filter(|row| row["valid"].as_bool().unwrap())
        .map(|row| Definition::parse("GLBX.MDP3", row["json"].as_str().unwrap()).unwrap())
        .collect()
}

#[test]
fn typed_catalog_projection_preserves_legacy_abi_bytes() {
    use ifm_market_data_native::catalog::queries::project;

    let mut rows = projection_rows();
    let mut exact = rows[0].clone();
    exact.symbol = "ES\\\"\t\nλ".into();
    exact.received_ns = u64::MAX - 1;
    exact.strike = Some(i64::MIN);
    rows.push(exact);
    for row in rows {
        assert_eq!(project(&row).unwrap(), legacy_projection(&row));
    }
    let mut oversized = projection_rows().remove(0);
    oversized.symbol = "a".repeat(262_144);
    assert!(matches!(
        project(&oversized),
        Err(ifm_market_data_native::catalog::Failure::Capacity)
    ));
}

#[test]
#[ignore = "paired catalog serializer qualification"]
fn measure_catalog_projection() {
    use ifm_market_data_native::catalog::queries::project;
    use std::{hint::black_box, time::Instant};

    const REPETITIONS: usize = 100;
    let rows = projection_rows();
    let measure = |encode: &dyn Fn(&Definition) -> Vec<u8>| {
        let start = Instant::now();
        let mut total = 0usize;
        for _ in 0..REPETITIONS {
            for row in &rows {
                total += black_box(encode(black_box(row))).len();
            }
        }
        (start.elapsed().as_secs_f64() * 1_000.0, total)
    };
    let typed = |row: &Definition| project(row).unwrap();
    measure(&legacy_projection);
    measure(&typed);
    for sample in 1..=7 {
        let (before, after) = if sample % 2 == 1 {
            (measure(&legacy_projection), measure(&typed))
        } else {
            let after = measure(&typed);
            (measure(&legacy_projection), after)
        };
        assert_eq!(before.1, after.1);
        println!(
            "CATALOG_PROJECTION_PERFORMANCE {}",
            serde_json::json!({"sample":sample,"rowsPerSample":rows.len() * REPETITIONS,
                "beforeMs":before.0,"afterMs":after.0,"encodedBytes":after.1})
        );
    }
}
