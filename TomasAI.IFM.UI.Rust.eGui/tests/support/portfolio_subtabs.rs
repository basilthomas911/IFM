use super::*;
use ifm_ui_egui::{backend::schema::Registry, state::portfolio::PortfolioController};
use serde_json::{Value as Json, json};
async fn act(
    state: &mut PortfolioController,
    backend: &Backend,
    registry: &Registry,
    generation: u64,
    intent: &str,
    fields: Json,
    original: Option<&str>,
) -> ifm_ui_egui::contract_editor::ContractPublication {
    state.execute(backend,registry,&json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"portfolio","intent":intent,"expectedGeneration":generation,"originalId":original,"fields":fields})).await.unwrap()
}
fn doc(p: &ifm_ui_egui::contract_editor::ContractPublication) -> Json {
    ifm_ui_egui::wire::decode(&p.result.documents["portfolio"]).unwrap()
}
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn portfolio_subtabs_match_managed_financials_and_risk_evidence() {
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(3),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    for owner in ["PortfolioMode", "FinancialMode", "InspectionMode"] {
        service_result(
            &backend
                .request(
                    &ActorSubject::query("NativeCompatibility", owner, "none"),
                    &if owner == "PortfolioMode" {
                        "Reset"
                    } else {
                        "Normal"
                    }
                    .into(),
                    None,
                    None,
                )
                .await
                .unwrap(),
        )
        .unwrap();
    }
    for name in ["fund-financials", "fund-risk-history", "fund-risk-evidence"] {
        let mut state = PortfolioController::with_journal_root(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("target/cross-language/subtab-ui")
                .join(uuid::Uuid::new_v4().to_string()),
        );
        let mut p = act(&mut state, &backend, &registry, 0, "load", json!({}), None).await;
        p = act(
            &mut state,
            &backend,
            &registry,
            p.generation,
            "portfolio-fund-view",
            json!({"view":if name=="fund-financials"{"financials"}else{"risk-history"}}),
            None,
        )
        .await;
        if name == "fund-risk-evidence" {
            p = act(
                &mut state,
                &backend,
                &registry,
                p.generation,
                "portfolio-grid",
                json!({"grid":"risk-history"}),
                Some("c154e9a4-bec4-4335-a3a6-7bdc9d370fa2"),
            )
            .await;
        }
        assert_eq!(p.outcomes[0].state, "Completed", "{name}: {}", p.status);
        let native = doc(&p);
        let managed: Json = ifm_ui_egui::wire::decode(
            &std::fs::read(
                std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join(format!(
                    "tests/fixtures/backend/portfolio-session-{name}.msgpack"
                )),
            )
            .unwrap(),
        )
        .unwrap();
        assert!(
            same_display_document(&native["fundView"], &managed["fundView"]),
            "{name}: native={} managed={}",
            native["fundView"],
            managed["fundView"]
        );
        assert_eq!(native["dialogs"], json!([]));
    }
}
