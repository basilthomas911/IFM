//! Refresh failure and stale-owner cases exercise the same repository used by
//! managed/native hydration, rather than a second test-only catalog model.
use ifm_market_data_native::catalog::{
    Definition,
    repository::{Error, Repository},
    selection,
};

fn source() -> String {
    let cases: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
    cases[0]["json"].as_str().unwrap().into()
}
#[test]
fn refresh_is_atomic_with_abort_capacity_invalid_and_stale_tokens() {
    let json = source();
    let id = selection::format(&Definition::parse("GLBX.MDP3", &json).unwrap()).unwrap();
    let mut repository = Repository::new(1, json.len()).unwrap();
    let first = repository.begin().unwrap();
    assert_eq!(repository.begin(), Err(Error::Conflict));
    repository.append(first, "GLBX.MDP3", &json).unwrap();
    assert!(repository.resolve(0, &id).unwrap().is_none());
    assert_eq!(repository.commit(first), Ok(1));
    let original = repository.resolve(1, &id).unwrap().unwrap();
    for invalid in ["{", "null", "[]", "{}"] {
        let token = repository.begin().unwrap();
        assert_eq!(
            repository.append(token, "GLBX.MDP3", invalid),
            Err(Error::Invalid)
        );
        assert_eq!(repository.commit(token), Err(Error::Conflict));
        assert_eq!(repository.resolve(1, &id).unwrap().unwrap(), original);
    }
    let token = repository.begin().unwrap();
    repository.append(token, "GLBX.MDP3", &json).unwrap();
    assert_eq!(
        repository.append(token, "XCBF.PITCH", &json),
        Err(Error::Capacity)
    );
    assert_eq!(repository.commit(token), Err(Error::Conflict));
    let new = repository.begin().unwrap();
    assert_eq!(repository.abort(token), Err(Error::Conflict));
    repository.append(new, "GLBX.MDP3", &json).unwrap();
    repository.abort(new).unwrap();
    assert_eq!(repository.revision(), 1);
    assert_eq!(repository.resolve(0, &id), Err(Error::Conflict));
    let empty = repository.begin().unwrap();
    assert_eq!(repository.commit(empty), Ok(2));
    assert!(repository.resolve(2, &id).unwrap().is_none());
    assert_eq!(original.json.as_ref(), json); // A prior reader remains memory-safe.
}

#[test]
fn duplicate_domain_identity_is_not_arbitrarily_resolved_and_replacements_are_last_arrival() {
    let json = source();
    let id = selection::format(&Definition::parse("GLBX.MDP3", &json).unwrap()).unwrap();
    let mut repository = Repository::new(2, json.len() * 3).unwrap();
    let token = repository.begin().unwrap();
    repository.append(token, "GLBX.MDP3", &json).unwrap();
    let changed = json.replace("ESZ6", "ESZ6-new");
    repository.append(token, "GLBX.MDP3", &changed).unwrap();
    repository.commit(token).unwrap();
    assert_eq!(
        repository.resolve(1, &id).unwrap().unwrap().symbol,
        "ESZ6-new"
    );
    let token = repository.begin().unwrap();
    repository.append(token, "GLBX.MDP3", &json).unwrap();
    repository.append(token, "XCBF.PITCH", &json).unwrap();
    repository.commit(token).unwrap();
    assert_eq!(repository.resolve(2, &id), Err(Error::Ambiguous));
}

#[test]
fn catalog_abi_checks_lengths_and_preserves_outputs_on_failure() {
    use ifm_market_data_native::ffi::{catalog::*, runtime::*};
    let json = source();
    let id = selection::format(&Definition::parse("GLBX.MDP3", &json).unwrap()).unwrap();
    let mut handle = 0;
    // SAFETY: Every accepted pointer is backed by an aligned exclusive local or
    // a live bounded slice. Oversized invalid input is rejected before access.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 1, &mut handle), 0);
        assert_eq!(
            ifm_market_data_catalog_configure_v1(handle, 1, json.len() as u32),
            0
        );
        assert_eq!(ifm_market_data_catalog_configure_v1(handle, 1, 1), 7);
        let mut token = 999;
        assert_eq!(ifm_market_data_catalog_begin_v1(handle, &mut token), 0);
        let mut other = 999;
        assert_eq!(ifm_market_data_catalog_begin_v1(handle, &mut other), 7);
        assert_eq!(other, 999);
        assert_eq!(
            ifm_market_data_catalog_append_v1(
                handle,
                token,
                b"GLBX.MDP3".as_ptr(),
                9,
                std::ptr::dangling(),
                65_537
            ),
            1
        );
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
        let mut revision = 999;
        assert_eq!(
            ifm_market_data_catalog_finish_v1(handle, token, 2, &mut revision),
            1
        );
        assert_eq!(revision, 999);
        assert_eq!(
            ifm_market_data_catalog_finish_v1(handle, token, 1, &mut revision),
            0
        );
        let mut buffer = vec![77; 65_536];
        let mut length = 999;
        assert_eq!(
            ifm_market_data_catalog_resolve_v1(
                handle,
                0,
                id.as_ptr(),
                id.len() as u32,
                buffer.as_mut_ptr(),
                65_536,
                &mut length
            ),
            7
        );
        assert_eq!((buffer[0], length), (77, 999));
        assert_eq!(
            ifm_market_data_catalog_resolve_v1(
                handle,
                revision,
                id.as_ptr(),
                id.len() as u32,
                buffer.as_mut_ptr(),
                1,
                &mut length
            ),
            4
        );
        assert_eq!((buffer[0], length), (77, 999));
        assert_eq!(
            ifm_market_data_catalog_resolve_v1(
                handle,
                revision,
                id.as_ptr(),
                id.len() as u32,
                buffer.as_mut_ptr(),
                65_536,
                &mut length
            ),
            0
        );
        assert_eq!(&buffer[..length as usize], json.as_bytes());
        assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
        assert_eq!(ifm_market_data_catalog_begin_v1(handle, &mut other), 6);
    }
}
