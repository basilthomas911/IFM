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
        assert_eq!(
            ifm_market_data_catalog_query_close_v1(handle, result.token),
            0
        );
        assert_eq!(
            ifm_market_data_catalog_query_close_v1(handle, result.token),
            1
        );
        assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
    }
}
