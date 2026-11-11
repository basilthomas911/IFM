use super::*;
use ifm_ui_egui::{backend::schema::Registry, state::portfolio::PortfolioController};
use serde_json::{Value as Json, json};


/// Provides mode for the portfolio fund implementation.
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


/// Provides act for the portfolio fund implementation.
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


/// Provides document for the portfolio fund implementation.
fn document(p: &ifm_ui_egui::contract_editor::ContractPublication) -> Json {
    ifm_ui_egui::wire::decode(&p.result.documents["portfolio"]).unwrap()
}


/// Verifies that portfolio fund configuration preserves catalog scope and partial create.
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn portfolio_fund_configuration_preserves_catalog_scope_and_partial_create() {
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
    mode(&backend, "FundCommands").await;
    for view in [
        "new-fund",
        "version-fund",
        "allocation",
        "envelope",
        "assignment",
    ] {
        let mut owner = PortfolioController::default();
        let loaded = act(&mut owner, &backend, &registry, 0, "load", json!({})).await;
        let opened = act(
            &mut owner,
            &backend,
            &registry,
            loaded.generation,
            "portfolio-open",
            json!({"view":view}),
        )
        .await;
        assert_eq!(opened.outcomes[0].state, "Completed", "{}", opened.status);
        let native = document(&opened);
        let managed: Json = ifm_ui_egui::wire::decode(
            &std::fs::read(
                std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join(format!(
                    "tests/fixtures/backend/portfolio-session-{view}.msgpack"
                )),
            )
            .unwrap(),
        )
        .unwrap();
        assert!(
            same_display_document(&native["dialogs"][0], &managed["dialogs"][0]),
            "Fund {view} editor differs: native={} managed={}",
            native["dialogs"][0],
            managed["dialogs"][0]
        );
    }
    let mut p = act(&mut state, &backend, &registry, 0, "load", json!({})).await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    for (view, fields, revision) in [
        (
            "allocation",
            json!({"AllocatedCapital":"10000","TargetWeight":"0.5","SourcePolicyVersion":"1","PortfolioId":"999"}),
            4,
        ),
        (
            "envelope",
            json!({"MaximumContracts":"5","days":"30","PortfolioId":"999"}),
            5,
        ),
    ] {
        p = act(
            &mut state,
            &backend,
            &registry,
            p.generation,
            "portfolio-open",
            json!({"view":view}),
        )
        .await;
        assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
        p = act(
            &mut state,
            &backend,
            &registry,
            p.generation,
            "portfolio-save",
            fields,
        )
        .await;
        assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
        assert_eq!(state.data.portfolio_revision, revision);
    }
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-open",
        json!({"view":"assignment"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let doc = document(&p);
    let fields = doc["dialogs"][0]["fields"].as_array().unwrap();
    let deployment = fields.iter().find(|f| f["key"] == "deployment").unwrap()["value"]
        .as_str()
        .unwrap()
        .to_string();
    assert!(!deployment.is_empty());
    assert_eq!(
        fields.iter().find(|f| f["key"] == "Enabled").unwrap()["enabled"],
        false
    );
    p=act(&mut state,&backend,&registry,p.generation,"portfolio-save",json!({"deployment":deployment,"Enabled":"False","Priority":"2","TradeTemplateId":"00000000-0000-0000-0000-000000000000"})).await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(state.data.assignments.len(), 1);
    assert_eq!(state.data.fund_revision, 5);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-open",
        json!({"view":"version-fund"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let doc = document(&p);
    let fields = doc["dialogs"][0]["fields"].as_array().unwrap();
    let families = fields.iter().find(|f| f["key"] == "families").unwrap()["value"].clone();
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-save",
        json!({"Name":"Changed native fund","families":families}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(state.data.fund_revision, 6);
    assert_eq!(state.data.funds.len(), 2);
    // Both a definitive attachment rejection and a lost attachment receipt must
    // preserve the already-created mandate rather than call Create again.
    for failure in ["FundAttachReject", "FundAttachLostAck"] {
        mode(&backend, "Reset").await;
        mode(&backend, "FundCommands").await;
        let mut state = PortfolioController::default();
        let mut p = act(&mut state, &backend, &registry, 0, "load", json!({})).await;
        p = act(
            &mut state,
            &backend,
            &registry,
            p.generation,
            "portfolio-open",
            json!({"view":"new-fund"}),
        )
        .await;
        assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
        let doc = document(&p);
        let fields = doc["dialogs"][0]["fields"].as_array().unwrap();
        let condition = fields
            .iter()
            .find(|f| f["key"] == "PermittedConditions")
            .unwrap()["choices"][0]["value"]
            .clone();
        let family =
            fields.iter().find(|f| f["key"] == "families").unwrap()["choices"][0]["value"].clone();
        mode(&backend, failure).await;
        p=act(&mut state,&backend,&registry,p.generation,"portfolio-save",json!({"Name":"Native new fund","UnderlyingUniverse":"ES","EligibleAssetTypes":"FuturesOption","PermittedDirections":"Bullish","PermittedConditions":condition,"families":family})).await;
        assert_eq!(p.outcomes[0].state, "Unknown", "{}", p.status);
        p = act(
            &mut state,
            &backend,
            &registry,
            p.generation,
            "portfolio-save",
            json!({}),
        )
        .await;
        assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
        assert_eq!(state.data.funds.len(), 2);
        assert_eq!(state.data.portfolio_revision, 4);
    }
    mode(&backend, "Reset").await;
}


/// Fund defaults persist as a new mandate version; unsupported or empty selections never write.
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn portfolio_fund_base_contracts_preserve_defaults_and_reject_equities_or_empty_selection() {
    let backend = Backend::connect(Settings { url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None, token: None, request_timeout: Duration::from_secs(2) }).await.unwrap();
    let registry = Registry::compiled().unwrap();
    mode(&backend, "Reset").await;
    mode(&backend, "FundCommands").await;
    let mut state = PortfolioController::default();
    let mut p = act(&mut state, &backend, &registry, 0, "load", json!({})).await;
    p = act(&mut state, &backend, &registry, p.generation, "portfolio-open", json!({"view":"base-contracts"})).await;
    assert_eq!(p.outcomes[0].state,"Completed","{}",p.status);
    let initial_doc = document(&p);
    let initial_fields = initial_doc["dialogs"][0]["fields"].as_array().unwrap();
    for field in initial_fields.iter().filter(|f| f["key"].as_str().is_some_and(|k| k.starts_with("selected.")) && f["value"] == "false") {
        let cycle_key = field["key"].as_str().unwrap().replace("selected.", "cycles.");
        assert_eq!(initial_fields.iter().find(|f| f["key"] == cycle_key).unwrap()["value"], "2");
    }
    let old_revision = state.data.fund_revision;
    for fields in [json!({"BaseAsset":"Equities","selected.ES":"true","cycles.ES":"1"}), json!({"BaseAsset":"Futures"})] {
        p = act(&mut state, &backend, &registry, p.generation, "portfolio-save", fields).await;
        assert_ne!(p.outcomes[0].state,"Completed");
        assert_eq!(state.data.fund_revision,old_revision);
    }
    p = act(&mut state, &backend, &registry, p.generation, "portfolio-save",
        json!({"BaseAsset":"Futures","selected.ES":"true","cycles.ES":"2","selected.VX":"true","cycles.VX":"1"})).await;
    assert_eq!(p.outcomes[0].state,"Completed","{}",p.status);
    assert_eq!(state.data.fund_revision,old_revision+1);
    assert_eq!(document(&p)["dialogs"], json!([]), "Successful Save must close the editor");
    p = act(&mut state, &backend, &registry, p.generation, "portfolio-open", json!({"view":"base-contracts"})).await;
    assert_eq!(p.outcomes[0].state,"Completed","{}",p.status);
    let doc = document(&p);
    let fields = doc["dialogs"][0]["fields"].as_array().unwrap();
    for (key,value) in [("selected.ES","true"),("cycles.ES","2"),("selected.VX","true"),("cycles.VX","1")] {
        assert_eq!(fields.iter().find(|f|f["key"]==key).unwrap()["value"],value);
    }
    assert!(fields.iter().all(|f| f["key"] != "saved"));
}
