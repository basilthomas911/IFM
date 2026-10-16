use ifm_ui_rust::publication_transfer::TransferCache;
#[test]
#[ignore = "Requires IFM_TRANSFER_FIXTURES produced by PublicationTransferTests"]
fn csharp_incremental_documents_and_recovery() {
    let root =
        std::path::PathBuf::from(std::env::var_os("IFM_TRANSFER_FIXTURES").expect("fixture root"));
    let mut cache = TransferCache::default();
    cache.scratch = std::fs::read(root.join("full.msgpack")).unwrap();
    cache.complete(8, 0, 1, 100, 50).unwrap();
    assert!(cache.pending(8));
    cache.acknowledge(8);
    assert!(!cache.pending(8));
    cache.scratch = std::fs::read(root.join("patch.msgpack")).unwrap();
    cache.complete(8, 3, 2, 200, 150).unwrap();
    let actual: rmpv::Value =
        rmpv::decode::read_value(&mut std::io::Cursor::new(cache.bytes(8))).unwrap();
    let expected: rmpv::Value = rmpv::decode::read_value(&mut std::io::Cursor::new(
        std::fs::read(root.join("expected.msgpack")).unwrap(),
    ))
    .unwrap();
    assert_eq!(actual, expected);
    cache.scratch = std::fs::read(root.join("patch.msgpack")).unwrap();
    assert!(cache.complete(8, 3, 2, 200, 150).is_err());
    assert_eq!(cache.sequence(8), 0);
    cache.scratch = std::fs::read(root.join("expected.msgpack")).unwrap();
    cache.complete(8, 0, 2, 200, 150).unwrap();
    assert_eq!(cache.sequence(8), 2);
}

#[test]
#[ignore = "Requires IFM_TRANSFER_FIXTURES produced by MarketOutlookPresentationTests"]
fn csharp_dashboard_contract_keeps_value_colors_and_chart_series() {
    let root=std::path::PathBuf::from(std::env::var_os("IFM_TRANSFER_FIXTURES").expect("fixture root"));
    let dashboard:ifm_ui_rust::shell::Dashboard=ifm_ui_rust::wire::decode(&std::fs::read(root.join("dashboard.msgpack")).unwrap()).unwrap();
    let cell=&dashboard.outlook_cells["marketDirection"];
    assert!(!cell.value.is_empty());assert!(!cell.foreground.is_empty());assert!(!cell.background.is_empty());
    assert_eq!(dashboard.outlook_cells["closePrice"].background,"DarkSurface");
    assert_eq!(ifm_ui_rust::market_outlook::color(&dashboard.outlook_cells["closePrice"].background),eframe::egui::Color32::BLACK);
    assert_eq!(dashboard.market_charts["ES"].series[0].points.len(),2);
}
