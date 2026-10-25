use super::*;
use ifm_ui_egui::{backend::schema::Registry, state::portfolio::PortfolioController};
use serde_json::{Value as Json, json};
async fn mode(backend: &Backend, owner: &str, name: &str) {
    service_result(
        &backend
            .request(
                &ActorSubject::query("NativeCompatibility", owner, "none"),
                &name.into(),
                None,
                None,
            )
            .await
            .unwrap(),
    )
    .unwrap();
}
async fn act(
    controller: &mut PortfolioController,
    backend: &Backend,
    registry: &Registry,
    generation: u64,
    intent: &str,
    fields: Json,
) -> ifm_ui_egui::contract_editor::ContractPublication {
    controller.execute(backend,registry,&json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"portfolio","intent":intent,"expectedGeneration":generation,"originalId":null,"fields":fields})).await.unwrap()
}
fn document(p: &ifm_ui_egui::contract_editor::ContractPublication) -> Json {
    ifm_ui_egui::wire::decode(&p.result.documents["portfolio"]).unwrap()
}
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn portfolio_financial_panels_match_managed_reads_and_reject_cross_cut_pages() {
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = PortfolioController::with_journal_root(
        std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("target/empty-financial-journal-fixture")
            .join(uuid::Uuid::new_v4().to_string()),
    );
    mode(&backend, "PortfolioMode", "Reset").await;
    mode(&backend, "FinancialMode", "Normal").await;
    let mut p = act(&mut state, &backend, &registry, 0, "load", json!({})).await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-open",
        json!({"view":"financials"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let native = document(&p);
    let managed: Json = ifm_ui_egui::wire::decode(
        &std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend/portfolio-session-financials.msgpack"),
        )
        .unwrap(),
    )
    .unwrap();
    assert!(
        same_display_document(&native["dialogs"][0], &managed["dialogs"][0]),
        "Financial panel differs: native={} managed={}",
        native["dialogs"][0],
        managed["dialogs"][0]
    );
    mode(&backend, "FinancialMode", "ChangedCut").await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-financial",
        json!({"action":"Next transactions"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Failed");
    assert!(p.status.contains("cut"));
    let retained = document(&p);
    assert_eq!(
        retained["dialogs"][0]["grids"],
        native["dialogs"][0]["grids"]
    );
    mode(&backend, "FinancialMode", "Normal").await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-financial",
        json!({"action":"Next transactions"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let next = document(&p);
    assert_eq!(
        next["dialogs"][0]["grids"][1]["rows"]
            .as_array()
            .unwrap()
            .len(),
        0
    );
    assert_eq!(
        next["dialogs"][0]["buttonEnabled"]["Next transactions"],
        false
    );
    mode(&backend, "FinancialMode", "WrongCursor").await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-financial",
        json!({"action":"Refresh"}),
    )
    .await;
    let unavailable = document(&p);
    assert!(
        unavailable["dialogs"][0]["status"]
            .as_str()
            .unwrap()
            .contains("cursor")
    );
    assert_eq!(
        unavailable["dialogs"][0]["buttonEnabled"]["Transaction"],
        false
    );
    mode(&backend, "FinancialMode", "Normal").await;
    mode(&backend, "PortfolioMode", "Reset").await;
}
