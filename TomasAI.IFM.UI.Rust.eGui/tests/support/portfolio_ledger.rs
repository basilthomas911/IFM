use super::*;
use ifm_ui_egui::{backend::schema::Registry, state::portfolio::PortfolioController};
use serde_json::{Value as Json, json};

async fn mode(backend: &Backend, owner: &str, value: &str) {
    service_result(
        &backend
            .request(
                &ActorSubject::query("NativeCompatibility", owner, "none"),
                &value.into(),
                None,
                None,
            )
            .await
            .unwrap(),
    )
    .unwrap();
}
async fn act(
    state: &mut PortfolioController,
    backend: &Backend,
    registry: &Registry,
    generation: u64,
    intent: &str,
    fields: Json,
) -> ifm_ui_egui::contract_editor::ContractPublication {
    state.execute(backend, registry, &json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"portfolio","intent":intent,"expectedGeneration":generation,"originalId":null,"fields":fields})).await.unwrap()
}
fn document(p: &ifm_ui_egui::contract_editor::ContractPublication) -> Json {
    ifm_ui_egui::wire::decode(&p.result.documents["portfolio"]).unwrap()
}
fn controller() -> PortfolioController {
    PortfolioController::with_journal_root(
        std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("target/cross-language/ledger-ui")
            .join(uuid::Uuid::new_v4().to_string()),
    )
}
async fn dispatches(backend: &Backend) -> i64 {
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
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn portfolio_ledger_invalid_controls_never_dispatch() {
    let backend = backend().await;
    let registry = Registry::compiled().unwrap();
    mode(&backend, "PortfolioMode", "Reset").await;
    mode(&backend, "FinancialMode", "Normal").await;
    mode(&backend, "FinancialOperationMode", "Controls").await;
    let mut state = controller();
    let mut p = act(&mut state, &backend, &registry, 0, "load", json!({})).await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "portfolio-open",
        json!({"view":"ledger"}),
    )
    .await;
    let count = dispatches(&backend).await;
    for input in [
        json!({"action":"Apply","control":"Reconcile","reason":" "}),
        json!({"action":"Apply","control":"OpenPeriod","start":"2026-01-01","end":"2026-12-31","reason":"Native ledger control"}),
        json!({"action":"Apply","control":"OpenPeriod","start":"0000-01-01","end":"0000-12-31","reason":"Native ledger control"}),
        json!({"action":"Apply","control":"RetireAccount","account":"999","reason":"Native ledger control"}),
        json!({"action":"Apply","control":"ReopenPeriod","period":"230bcb5c-dea4-46cf-9a29-1cd0693b6cd0","reason":"Native ledger control"}),
    ] {
        p = act(
            &mut state,
            &backend,
            &registry,
            p.generation,
            "portfolio-financial",
            input,
        )
        .await;
        assert_eq!(p.outcomes[0].state, "Failed", "{}", p.status);
        assert_eq!(dispatches(&backend).await, count);
    }
    mode(&backend, "FinancialOperationMode", "Normal").await;
}
async fn backend() -> Backend {
    Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap()
}

#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn portfolio_ledger_dialogs_match_managed_forms() {
    let backend = backend().await;
    let registry = Registry::compiled().unwrap();
    mode(&backend, "PortfolioMode", "Reset").await;
    mode(&backend, "FinancialMode", "Normal").await;
    for view in [
        "ledger",
        "account-version",
        "rule-version",
        "setup",
        "setup-prepared",
        "authority",
        "authority-prepared",
        "risk-policy",
        "discard-policy",
    ] {
        let mut state = controller();
        let mut p = act(&mut state, &backend, &registry, 0, "load", json!({})).await;
        let root = if view.contains("version") {
            "ledger"
        } else if view.ends_with("policy") {
            "risk-policy"
        } else {
            view.split('-').next().unwrap()
        };
        p = act(
            &mut state,
            &backend,
            &registry,
            p.generation,
            "portfolio-open",
            json!({"view":root}),
        )
        .await;
        if view == "account-version" || view == "rule-version" {
            p=act(&mut state,&backend,&registry,p.generation,"portfolio-financial",json!({"action":"Apply","control":if view=="account-version" {"AddAccountVersion"} else {"AddPostingRuleVersion"},"account":"101","rule":"43da81b8-a8f2-45b1-aa21-5ad8cb080287"})).await;
        }
        if view == "setup-prepared" {
            p=act(&mut state,&backend,&registry,p.generation,"portfolio-financial",json!({"action":"Prepare","account":"paper:fixture","start":"2026-01-01","end":"2026-12-31"})).await;
        }
        if view == "authority-prepared" {
            p = act(
                &mut state,
                &backend,
                &registry,
                p.generation,
                "portfolio-financial",
                json!({"action":"Prepare","permit":"true"}),
            )
            .await;
        }
        if view == "discard-policy" {
            p = act(
                &mut state,
                &backend,
                &registry,
                p.generation,
                "portfolio-policy",
                json!({"action":"New Policy"}),
            )
            .await;
            p = act(
                &mut state,
                &backend,
                &registry,
                p.generation,
                "portfolio-cancel",
                json!({"dirty":"true"}),
            )
            .await;
        }
        assert_eq!(p.outcomes[0].state, "Completed", "{view}: {}", p.status);
        let native = document(&p);
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
            same_display_document(
                native["dialogs"].as_array().unwrap().last().unwrap(),
                managed["dialogs"].as_array().unwrap().last().unwrap()
            ),
            "{view}: native={} managed={}",
            native["dialogs"],
            managed["dialogs"]
        );
    }
}

#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn portfolio_ledger_controls_match_managed_preparation() {
    let backend = backend().await;
    let registry = Registry::compiled().unwrap();
    mode(&backend, "PortfolioMode", "Reset").await;
    mode(&backend, "FinancialOperationMode", "Controls").await;
    // Each operation starts from an independent authoritative snapshot and a
    // separate journal. The C# responder compares every body and revision with
    // the unchanged managed preparation code before issuing a commit receipt.
    for control in [
        "Reconcile",
        "OpenPeriod",
        "ClosePeriod",
        "ReopenPeriod",
        "RetireAccount",
        "RetirePostingRule",
        "QualifyDevelopmentBook",
        "AddAccountVersion",
        "AddPostingRuleVersion",
        "CreateBook",
        "RefreshAuthority",
    ] {
        mode(
            &backend,
            "FinancialMode",
            if control == "ReopenPeriod" {
                "LedgerClosed"
            } else {
                "Normal"
            },
        )
        .await;
        let mut state = controller();
        let mut p = act(&mut state, &backend, &registry, 0, "load", json!({})).await;
        p=act(&mut state,&backend,&registry,p.generation,"portfolio-open",json!({"view":if control=="CreateBook" {"setup"} else if control=="RefreshAuthority" {"authority"} else {"ledger"}})).await;
        if control == "CreateBook" || control == "RefreshAuthority" {
            p=act(&mut state,&backend,&registry,p.generation,"portfolio-financial",json!({"action":"Prepare","account":"paper:fixture","start":"2026-01-01","end":"2026-12-31","permit":"true"})).await;
        } else {
            p=act(&mut state,&backend,&registry,p.generation,"portfolio-financial",json!({"action":"Apply","control":control,"period":"230bcb5c-dea4-46cf-9a29-1cd0693b6cd0","start":"2027-01-01","end":"2027-12-31","account":"101","rule":"43da81b8-a8f2-45b1-aa21-5ad8cb080287","reason":"Native ledger control"})).await;
        }
        assert_eq!(p.outcomes[0].state, "Completed", "{control}: {}", p.status);
        if matches!(
            control,
            "AddAccountVersion" | "AddPostingRuleVersion" | "CreateBook" | "RefreshAuthority"
        ) {
            p=act(&mut state,&backend,&registry,p.generation,"portfolio-financial",json!({"action":"Save","normal":"Credit","dimension":"False","debit":"102@1","credit":"101@1","asset":"","pnl":"","confirmed":"False","effective":"2026-10-26","reason":"Native ledger control"})).await;
            assert_eq!(p.outcomes[0].state, "Completed", "{control}: {}", p.status);
        }
        let doc = document(&p);
        assert!(
            doc["dialogs"].to_string().contains("Saved")
                || doc["dialogs"].to_string().contains("Committed"),
            "{control}: {doc}"
        );
    }
    mode(&backend, "FinancialMode", "Normal").await;
    mode(&backend, "FinancialOperationMode", "Normal").await;
}
