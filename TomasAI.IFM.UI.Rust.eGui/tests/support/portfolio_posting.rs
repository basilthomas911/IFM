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
async fn count(backend: &Backend) -> i64 {
    service_result(
        &backend
            .request(
                &ActorSubject::query("NativeCompatibility", "FinancialOperationCount", "none"),
                &Value::Nil,
                None,
                None,
            )
            .await
            .unwrap(),
    )
    .unwrap()
    .as_array()
    .unwrap()[0]
        .as_i64()
        .unwrap()
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
async fn portfolio_posting_editor_uses_backend_rules_and_persists_one_cash_operation() {
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
            .join("target/cross-language/posting-ui")
            .join(uuid::Uuid::new_v4().to_string()),
    );
    mode(&backend, "PortfolioMode", "Reset").await;
    mode(&backend, "FinancialMode", "Normal").await;
    mode(&backend, "FinancialOperationMode", "Prepared").await;
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
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-financial",
        json!({"action":"Transaction"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let native = document(&p);
    let managed: Json = ifm_ui_egui::wire::decode(
        &std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend/portfolio-session-posting.msgpack"),
        )
        .unwrap(),
    )
    .unwrap();
    assert!(
        same_display_document(&native["dialogs"][1], &managed["dialogs"][1]),
        "Posting form differs: native={} managed={}",
        native["dialogs"][1],
        managed["dialogs"][1]
    );
    let fields = native["dialogs"][1]["fields"].as_array().unwrap();
    let rule = fields.iter().find(|f| f["key"] == "rule").unwrap()["value"].clone();
    let mut input = json!({"action":"Post","rule":rule,"amount":"-1","accountingDate":"2026-10-26","valueDate":"2026-10-26","description":"Native cash movement","reference":"fixture/confirmed","confirmed":"True"});
    let before = count(&backend).await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-financial",
        input.clone(),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Failed");
    assert_eq!(count(&backend).await, before);
    input["amount"] = "100.25".into();
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-financial",
        input,
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let committed = document(&p);
    assert_eq!(
        committed["dialogs"][1]["status"],
        "Committed. History may still be updating."
    );
    assert_eq!(count(&backend).await, before + 1);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-financial",
        json!({"action":"Close"}),
    )
    .await;
    let financials = document(&p);
    let pending = financials["dialogs"][0]["grids"]
        .as_array()
        .unwrap()
        .iter()
        .find(|g| g["key"] == "pending-postings")
        .unwrap();
    assert_eq!(pending["rows"].as_array().unwrap().len(), 1);
    let id = pending["rows"][0]["id"].as_str().unwrap();
    p=state.execute(&backend,&registry,&json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"portfolio","intent":"portfolio-grid","expectedGeneration":p.generation,"originalId":id,"fields":{"grid":"pending-postings"}})).await.unwrap();
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(
        document(&p)["dialogs"][1]["buttonEnabled"]["Check outcome"],
        false
    );
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-financial",
        json!({"action":"Check outcome"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(
        count(&backend).await,
        before + 1,
        "Reopening a committed operation must not dispatch it again"
    );
    assert_eq!(
        committed["dialogs"][1]["buttonEnabled"]["Check outcome"],
        false
    );
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-financial",
        json!({"action":"Check outcome"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(count(&backend).await, before + 1);
    mode(&backend, "FinancialOperationMode", "Normal").await;
    mode(&backend, "PortfolioMode", "Reset").await;
}
