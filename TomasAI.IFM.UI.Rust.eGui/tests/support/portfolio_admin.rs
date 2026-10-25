use super::*;
use ifm_ui_egui::{backend::schema::Registry, state::portfolio::PortfolioController};
use serde_json::{Value as Json, json};
async fn mode(backend: &Backend, name: &str) {
    service_result(
        &backend
            .request(
                &ActorSubject::query("NativeCompatibility", "PortfolioMode", "none"),
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
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn portfolio_admin_drafts_use_server_ids_clock_and_exact_command_revisions() {
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = PortfolioController::default();
    mode(&backend, "Reset").await;
    mode(&backend, "AdminCommands").await;
    let mut p = act(&mut state, &backend, &registry, 0, "load", json!({})).await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-open",
        json!({"view":"new-portfolio"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let draft: Json = ifm_ui_egui::wire::decode(&p.result.documents["portfolio"]).unwrap();
    let fields = draft["dialogs"][0]["fields"].as_array().unwrap();
    assert_eq!(
        fields.iter().find(|f| f["key"] == "PortfolioId").unwrap()["value"],
        "200"
    );
    assert_eq!(
        fields
            .iter()
            .find(|f| f["key"] == "EffectiveFromUtc")
            .unwrap()["value"],
        "2026-10-26 08:30"
    );
    let selected = state.data.portfolio.clone();
    let save = json!({"Name":" Native created portfolio ","BaseCurrency":" usd ","OperatingState":"Draft","BrokerAccountRefs":" paper:a, paper:b ","EffectiveFromUtc":"2026-10-26 08:30","PortfolioId":"999","PortfolioVersion":"999"});
    mode(&backend, "AdminReject").await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-save",
        save.clone(),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Failed");
    assert_eq!(state.data.portfolio, selected);
    mode(&backend, "AdminCommands").await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-save",
        save,
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let selected = registry
        .project(
            "TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioReadModel",
            state.data.portfolio.as_ref().unwrap(),
        )
        .unwrap();
    assert_eq!(selected["PortfolioId"], 200);
    assert_eq!(selected["PortfolioVersion"], 1);
    assert_eq!(state.data.portfolio_revision, 1);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-open",
        json!({"view":"version-portfolio"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-save",
        json!({"Name":"Native version portfolio","PortfolioId":"999","PortfolioVersion":"999"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(state.data.portfolio_revision, 2);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-open",
        json!({"view":"portfolio-state"}),
    )
    .await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-save",
        json!({"state":"Paused","reason":""}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Failed");
    assert_eq!(state.data.portfolio_revision, 2);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-save",
        json!({"state":"Paused","reason":"Native fixture"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(state.data.portfolio_revision, 3);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-open",
        json!({"view":"portfolio-state"}),
    )
    .await;
    mode(&backend, "AdminLostAck").await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-save",
        json!({"state":"Draft","reason":"Native fixture"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Unknown", "{}", p.status);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "load",
        json!({"filter":"Draft"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(state.data.portfolio_revision, 4);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-open",
        json!({"view":"delete-portfolio"}),
    )
    .await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-save",
        json!({"confirmation":"200","reason":"Delete fixture"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(state.data.portfolio_revision, 0);
    mode(&backend, "Reset").await;
}
