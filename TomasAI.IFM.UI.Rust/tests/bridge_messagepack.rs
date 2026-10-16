#[path = "../src/app/hosting.rs"]
mod hosting;
use ifm_ui_rust::{wire, Catalog, Snapshot};
use std::time::{Duration, Instant};

// Runs the actual hostfxr/embedded .NET boundary. Explicitly opt in after building the bridge.
#[test]
#[ignore = "Requires IFM_RUST_BRIDGE_TEST_ROOT pointing to the built managed bridge"]
fn embedded_dotnet_messagepack_publications_and_acknowledgements() {
    let root = std::env::var_os("IFM_RUST_BRIDGE_TEST_ROOT").expect("Set IFM_RUST_BRIDGE_TEST_ROOT");
    assert_ne!(std::env::var("IFM_RUST_UI_MODE").unwrap_or_default(), "live", "Use fixture mode; no backend actions");
    let managed = hosting::Managed::load(std::path::Path::new(&root)).unwrap();
    let catalog: Catalog = wire::decode(&managed.copy(1).unwrap()).unwrap();
    assert_eq!(catalog.version, 1);
    assert!(catalog.strings.len() > 10);
    let deadline = Instant::now() + Duration::from_secs(15);
    let snapshot: Snapshot = loop {
        let bytes = managed.copy(0).unwrap();
        if !bytes.is_empty() { break wire::decode(&bytes).unwrap(); }
        assert!(Instant::now() < deadline, "No managed fixture snapshot");
        std::thread::sleep(Duration::from_millis(20));
    };
    assert_eq!(snapshot.schema, 1);
    assert!(!snapshot.roots.is_empty());
    let _: ifm_ui_rust::lookup_editor::EditorPublication = wire::decode(&managed.copy(3).unwrap()).unwrap();
    let _: ifm_ui_rust::strategy_inspection::InspectionPublication = wire::decode(&managed.copy(4).unwrap()).unwrap();
    for kind in [5, 8] {
        let _: ifm_ui_rust::contract_editor::ContractPublication = wire::decode(&managed.copy(kind).unwrap()).unwrap();
    }
    let _: ifm_ui_rust::shell::Dashboard = wire::decode(&managed.copy(6).unwrap()).unwrap();
    let _: ifm_ui_rust::sidebar::SidebarPublication = wire::decode(&managed.copy(7).unwrap()).unwrap();
    let mut cache=ifm_ui_rust::publication_transfer::TransferCache::default();
    assert!(managed.poll(0,&mut cache).unwrap());
    let original=cache.sequence(0);cache.acknowledge(0);
    assert!(!managed.poll(0,&mut cache).unwrap());
    managed.action(1).unwrap();
    let deadline=Instant::now()+Duration::from_secs(15);
    while !managed.poll(0,&mut cache).unwrap(){assert!(Instant::now()<deadline);std::thread::sleep(Duration::from_millis(20));}
    assert!(cache.sequence(0)>original);
    if let Some(path)=std::env::var_os("IFM_TRANSFER_FIXTURES"){
      let root=std::path::PathBuf::from(path);std::fs::create_dir_all(&root).unwrap();
      std::fs::write(root.join("host-actual.msgpack"),cache.bytes(0)).unwrap();
      std::fs::write(root.join("host-expected.msgpack"),managed.copy(0).unwrap()).unwrap();
    }
    let changed:Snapshot=wire::decode(cache.bytes(0)).unwrap();assert_ne!(changed.workflow_id,snapshot.workflow_id);
    managed.action(3).unwrap();
    let deadline = Instant::now() + Duration::from_secs(15);
    loop {
        let bytes = managed.copy(2).unwrap();
        if !bytes.is_empty() {
            let rows: Vec<ifm_ui_rust::virtual_grid::GridRow> = wire::decode(&bytes).unwrap();
            assert_eq!(rows.len(), 100_000);
            assert_eq!(rows[0].daily_pnl, "-99.90");
            assert_eq!(rows[99_999].sequence, 100_000);
            println!("100,000-row MessagePack publication: {} bytes", bytes.len());
            break;
        }
        assert!(Instant::now() < deadline, "Grid publication timed out");
        std::thread::sleep(Duration::from_millis(20));
    }
    for (index, (view, action, kind)) in [("lookup", "add", 3), ("observation", "history", 4), ("parameter-sets", "load", 5), ("trade-orders", "load", 8)].into_iter().enumerate() {
        let id = format!("01234567-89ab-cdef-0123-456789abcde{index}");
        let lookup = if view == "lookup" { r#"{"lookupTypeName":"Symbol","shortCode":"ES","orderId":42,"description":"ES / \u03b4","createdOn":"2026-10-15T12:34:56.0000000Z","createdBy":"developer"}"# } else { "null" };
        let intent = format!(r#"{{"schema":1,"requestId":"{id}","view":"{view}","intent":"{action}","expectedGeneration":1,"lookup":{lookup},"originalId":null,"fields":{{}}}}"#);
        managed.submit(&intent).unwrap();
        let deadline = Instant::now() + Duration::from_secs(15);
        loop {
            let value: serde_json::Value = wire::decode(&managed.copy(kind).unwrap()).unwrap();
            if let Some(outcome) = value["outcomes"].as_array().unwrap().iter()
                .find(|v| v["requestId"] == id && v["state"] != "Pending") {
                assert_eq!(outcome["state"], "Failed"); // Fixture mode deliberately does not call APIs.
                assert!(!outcome["message"].as_str().unwrap().is_empty());
                break;
            }
            assert!(Instant::now() < deadline, "Missing {view} binary acknowledgement");
            std::thread::sleep(Duration::from_millis(20));
        }
        managed.submit(&intent).unwrap(); // Duplicate request retains its acknowledgement.
    }
}
