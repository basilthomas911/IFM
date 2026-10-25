//! Run against the isolated C# responder using Verify-StandaloneRust.ps1.
use ifm_ui_egui::backend::{codec, contracts::*, transport::*};
use rmpv::Value;
use std::time::Duration;
#[path = "support/financial_operations.rs"]
mod financial_operations;
#[path = "support/manual_trades.rs"]
mod manual_trades;
#[path = "support/option_chain.rs"]
mod option_chain;
#[path = "support/broker_reads.rs"]
mod broker_reads;
#[path = "support/broker_initialization.rs"]
mod broker_initialization;
#[path = "support/composer.rs"]
mod composer;
#[path = "support/composer_options.rs"]
mod composer_options;
#[path = "support/broker_account_commands.rs"]
mod broker_account_commands;
#[path = "support/portfolio_admin.rs"]
mod portfolio_admin;
#[path = "support/portfolio_financial.rs"]
mod portfolio_financial;
#[path = "support/portfolio_fund.rs"]
mod portfolio_fund;
#[path = "support/portfolio_ledger.rs"]
mod portfolio_ledger;
#[path = "support/portfolio_posting.rs"]
mod portfolio_posting;
#[path = "support/portfolio_subtabs.rs"]
mod portfolio_subtabs;
#[path = "support/trade_data.rs"]
mod trade_data;
fn same_display_document(a: &serde_json::Value, b: &serde_json::Value) -> bool {
    if a == b {
        return true;
    }
    match (a, b) {
        (serde_json::Value::Number(a), serde_json::Value::Number(b)) => a.as_f64() == b.as_f64(),
        (serde_json::Value::Array(a), serde_json::Value::Array(b)) => {
            a.len() == b.len() && a.iter().zip(b).all(|(a, b)| same_display_document(a, b))
        }
        (serde_json::Value::Object(a), serde_json::Value::Object(b)) => {
            a.len() == b.len()
                && a.iter()
                    .all(|(k, a)| b.get(k).is_some_and(|b| same_display_document(a, b)))
        }
        _ => false,
    }
}

#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn portfolio_policy_commands_reconcile_revisions_and_unknown_outcomes() {
    use ifm_ui_egui::{backend::schema::Registry, state::portfolio::PortfolioController};
    use serde_json::{Value as Json, json};
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
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
    async fn action(
        controller: &mut PortfolioController,
        backend: &Backend,
        registry: &Registry,
        generation: u64,
        intent: &str,
        id: Option<&str>,
        fields: Json,
    ) -> ifm_ui_egui::contract_editor::ContractPublication {
        controller.execute(backend,registry,&json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"portfolio","intent":intent,"expectedGeneration":generation,"originalId":id,"fields":fields})).await.unwrap()
    }
    fn document(publication: &ifm_ui_egui::contract_editor::ContractPublication) -> Json {
        ifm_ui_egui::wire::decode(&publication.result.documents["portfolio"]).unwrap()
    }
    fn value(doc: &Json, name: &str) -> String {
        doc["policy"]["fields"]
            .as_array()
            .unwrap()
            .iter()
            .find(|f| f["key"] == name)
            .unwrap()["value"]
            .as_str()
            .unwrap()
            .into()
    }
    let inputs = json!({"action":"Save","Name":"Native edited policy","CapitalBase":"50000.00","MaximumRiskPerTrade":"1000","MaximumOpenPositions":"5.9","limit.0.Enabled":"true","limit.0.MaximumRiskPerTrade":"750"});
    mode(&backend, "Reset").await;
    mode(&backend, "Commands").await;
    let mut controller = PortfolioController::default();
    let mut p = action(
        &mut controller,
        &backend,
        &registry,
        0,
        "load",
        None,
        json!({}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    p = action(
        &mut controller,
        &backend,
        &registry,
        p.generation,
        "portfolio-policy",
        None,
        json!({"action":"New Policy"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let doc = document(&p);
    assert_eq!(value(&doc, "CapitalBase"), "0");
    let managed: Json = ifm_ui_egui::wire::decode(
        &std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend/portfolio-session-policy-new.msgpack"),
        )
        .unwrap(),
    )
    .unwrap();
    assert!(
        same_display_document(&doc["policy"], &managed["policy"]),
        "Detached policy editor must match the current managed-session document"
    );
    assert_eq!(
        doc["policy"]["grids"][1]["rows"][0]["fields"]["TradeStrategyFamilyId"],
        "Fixture ES deployment"
    );
    let stale = action(
        &mut controller,
        &backend,
        &registry,
        p.generation - 1,
        "portfolio-policy",
        None,
        inputs.clone(),
    )
    .await;
    assert_eq!(stale.outcomes[0].state, "Failed");
    p = action(
        &mut controller,
        &backend,
        &registry,
        stale.generation,
        "portfolio-policy",
        None,
        inputs.clone(),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(value(&document(&p), "MaximumOpenPositions"), "5");
    // The same intent identity returns its cached receipt without resubmission.
    let cancel = json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"portfolio","intent":"portfolio-policy","expectedGeneration":p.generation,"originalId":null,"fields":{"action":"New Version"}});
    p = controller
        .execute(&backend, &registry, &cancel)
        .await
        .unwrap();
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let again = controller
        .execute(&backend, &registry, &cancel)
        .await
        .unwrap();
    assert_eq!(again.outcomes[0].state, "Completed");
    p = action(
        &mut controller,
        &backend,
        &registry,
        again.generation,
        "portfolio-policy",
        None,
        inputs.clone(),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    p = action(
        &mut controller,
        &backend,
        &registry,
        p.generation,
        "portfolio-grid",
        Some("100@2"),
        json!({"grid":"policies"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    p = action(
        &mut controller,
        &backend,
        &registry,
        p.generation,
        "portfolio-policy",
        None,
        json!({"action":"Activate && Assign"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let parent = registry
        .project(
            "TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioReadModel",
            controller.data.portfolio.as_ref().unwrap(),
        )
        .unwrap();
    assert_eq!(parent["ActivePolicyId"], 100);
    assert_eq!(parent["ActivePolicyVersion"], 2);
    assert_eq!(controller.data.portfolio_revision, 4);
    p = action(
        &mut controller,
        &backend,
        &registry,
        p.generation,
        "portfolio-policy",
        None,
        json!({"action":"New Policy"}),
    )
    .await;
    mode(&backend, "CommandReject").await;
    p = action(
        &mut controller,
        &backend,
        &registry,
        p.generation,
        "portfolio-policy",
        None,
        inputs.clone(),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Failed");
    assert_eq!(value(&document(&p), "Name"), "Native edited policy");
    mode(&backend, "LostAck").await;
    p = action(
        &mut controller,
        &backend,
        &registry,
        p.generation,
        "portfolio-policy",
        None,
        inputs.clone(),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Unknown", "{}", p.status);
    p = action(
        &mut controller,
        &backend,
        &registry,
        p.generation,
        "portfolio-policy",
        None,
        json!({"action":"New Version"}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Failed");
    assert!(p.status.contains("unknown"));
    p = action(
        &mut controller,
        &backend,
        &registry,
        p.generation,
        "load",
        None,
        json!({}),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    let doc = document(&p);
    assert_eq!(
        doc["policy"]["grids"][0]["rows"].as_array().unwrap().len(),
        3
    );
    p = action(
        &mut controller,
        &backend,
        &registry,
        p.generation,
        "portfolio-policy",
        None,
        json!({"action":"New Policy"}),
    )
    .await;
    mode(&backend, "RefreshReject").await;
    p = action(
        &mut controller,
        &backend,
        &registry,
        p.generation,
        "portfolio-policy",
        None,
        inputs,
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert!(p.outcomes[0].message.contains("committed"));
    mode(&backend, "Reset").await;
}

#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn portfolio_native_selection_uses_api_clock_revisions_and_atomic_scope_checks() {
    use ifm_ui_egui::{backend::schema::Registry, state::portfolio_data::PortfolioData};
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(1),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = PortfolioData::default();
    mode(&backend, "Reset").await;
    let deadline = || tokio::time::Instant::now() + Duration::from_secs(1);
    state
        .refresh(&backend, &registry, "Active", deadline())
        .await
        .unwrap();
    assert_eq!(state.portfolios.len(), 1);
    assert_eq!(state.funds.len(), 1);
    assert_eq!(state.portfolio_revision, 3);
    assert_eq!(state.fund_revision, 4);
    assert_eq!(
        state.context.as_ref().unwrap().value_date.to_string(),
        "2026-10-26"
    );
    assert!(state.allocation.is_none());
    assert!(state.envelope.is_none());
    assert!(state.assignments.is_empty());
    assert!(state.is_current_fund(&registry).unwrap());
    let mut controller = ifm_ui_egui::state::portfolio::PortfolioController::default();
    let initial=controller.execute(&backend,&registry,&serde_json::json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"portfolio","intent":"load","expectedGeneration":0,"originalId":null,"fields":{}})).await.unwrap();
    assert_eq!(initial.outcomes[0].state, "Completed", "{}", initial.status);
    let native: serde_json::Value =
        ifm_ui_egui::wire::decode(&initial.result.documents["portfolio"]).unwrap();
    let managed: serde_json::Value = ifm_ui_egui::wire::decode(
        &std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend/portfolio-session-initial.msgpack"),
        )
        .unwrap(),
    )
    .unwrap();
    fn difference(path: &str, a: &serde_json::Value, b: &serde_json::Value) -> Option<String> {
        if a == b || a.is_number() && b.is_number() && a.as_f64() == b.as_f64() {
            return None;
        }
        match (a, b) {
            (serde_json::Value::Object(a), serde_json::Value::Object(b)) => {
                for key in a.keys().chain(b.keys()) {
                    if let Some(error) = difference(
                        &format!("{path}/{key}"),
                        a.get(key).unwrap_or(&serde_json::Value::Null),
                        b.get(key).unwrap_or(&serde_json::Value::Null),
                    ) {
                        return Some(error);
                    }
                }
            }
            (serde_json::Value::Array(a), serde_json::Value::Array(b)) if a.len() == b.len() => {
                for (index, (a, b)) in a.iter().zip(b).enumerate() {
                    if let Some(error) = difference(&format!("{path}/{index}"), a, b) {
                        return Some(error);
                    }
                }
            }
            _ => return Some(format!("{path}: native={a} managed={b}")),
        }
        None
    }
    assert!(
        difference("portfolio", &native, &managed).is_none(),
        "{}",
        difference("portfolio", &native, &managed).unwrap_or_default()
    );
    mode(&backend, "Policy").await;
    let populated=controller.execute(&backend,&registry,&serde_json::json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"portfolio","intent":"load","expectedGeneration":initial.generation,"originalId":null,"fields":{}})).await.unwrap();
    assert_eq!(
        populated.outcomes[0].state, "Completed",
        "{}",
        populated.status
    );
    let native: serde_json::Value =
        ifm_ui_egui::wire::decode(&populated.result.documents["portfolio"]).unwrap();
    let managed: serde_json::Value = ifm_ui_egui::wire::decode(
        &std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend/portfolio-session-policy.msgpack"),
        )
        .unwrap(),
    )
    .unwrap();
    assert!(
        difference("policy portfolio", &native, &managed).is_none(),
        "{}",
        difference("policy portfolio", &native, &managed).unwrap_or_default()
    );
    mode(&backend, "Normal").await;
    let selected = state.fund.clone();
    let portfolio = state.portfolio.clone();
    assert!(
        state
            .select_portfolio(&backend, &registry, 999, deadline())
            .await
            .unwrap_err()
            .contains("current Portfolio")
    );
    assert_eq!(state.portfolio, portfolio);
    let key = ifm_ui_egui::state::portfolio_data::fund_key(
        &registry
            .project(
                "TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundMandateReadModel",
                selected.as_ref().unwrap(),
            )
            .unwrap(),
    )
    .unwrap();
    state
        .select_fund(&backend, &registry, &key, deadline())
        .await
        .unwrap();
    assert_eq!(state.fund, selected);
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
    mode(&backend, "WrongFund").await;
    assert!(
        state
            .refresh(&backend, &registry, "Active", deadline())
            .await
            .unwrap_err()
            .contains("scope")
    );
    assert_eq!(state.fund, selected);
    assert_eq!(state.portfolio_revision, 3);
    mode(&backend, "Reject").await;
    assert!(
        state
            .refresh(&backend, &registry, "Active", deadline())
            .await
            .unwrap_err()
            .contains("unavailable")
    );
    assert_eq!(state.portfolio, portfolio);
    assert_eq!(state.fund, selected);
    mode(&backend, "Normal").await;
    state.fund_filter = Some("Draft".into());
    state
        .refresh(&backend, &registry, "Active", deadline())
        .await
        .unwrap();
    assert!(state.fund.is_none());
    assert_eq!(state.status, "No Funds match the selected state.");
    state.fund_filter = Some("Active".into());
    state
        .refresh(&backend, &registry, "Active", deadline())
        .await
        .unwrap();
    assert_eq!(state.fund, selected);
}

#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn inspection_native_history_evidence_scope_and_failed_refresh_retention() {
    use ifm_ui_egui::{
        backend::schema::Registry, state::strategy_inspection::InspectionController,
    };
    use serde_json::json;
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(1),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = InspectionController::default();
    let request = |view: &str, action: &str, fields: serde_json::Value| json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":view,"intent":action,"fields":fields});
    let workflow = "b7329333-f0e3-478e-b884-6d7ef9d47971";
    let invocation = "c154e9a4-bec4-4335-a3a6-7bdc9d370fa2";
    let fields = json!({"portfolio":"1","fund":"2","date":"2026-10-26","workflow":workflow,"invocation":invocation});
    let mut current = state
        .execute(
            &backend,
            &registry,
            &request("risk", "history", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(current.result.rows.len(), 1);
    assert_eq!(current.result.paging_state.as_deref(), Some("fixture-next"));
    assert!(
        current.result.rows[0]
            .label
            .contains("Rejected 2 Fixture risk history")
    );
    async fn mode(backend: &Backend, name: &str) {
        service_result(
            &backend
                .request(
                    &ActorSubject::query("NativeCompatibility", "InspectionMode", "none"),
                    &name.into(),
                    None,
                    None,
                )
                .await
                .unwrap(),
        )
        .unwrap();
    }
    mode(&backend, "RepeatCursor").await;
    let mut next = fields.clone();
    next["cursor"] = json!("fixture-next");
    current = state
        .execute(&backend, &registry, &request("risk", "next", next.clone()))
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("did not advance"));
    assert_eq!(current.result.rows.len(), 1);
    mode(&backend, "Normal").await;
    current = state
        .execute(&backend, &registry, &request("risk", "next", next))
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed");
    assert!(current.result.rows.is_empty());
    assert!(current.result.paging_state.is_none());
    current = state
        .execute(
            &backend,
            &registry,
            &request("risk", "history", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.result.rows.len(), 1);
    let open = request("risk", "open", fields.clone());
    current = state.execute(&backend, &registry, &open).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(current.result.rows.len(), 1);
    let expected = std::fs::read_to_string(
        std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("tests/fixtures/backend/risk-observation-display.txt"),
    )
    .unwrap();
    assert_eq!(current.result.decision, expected);
    assert!(current.result.evidence.contains("NotAuthorized"));
    mode(&backend, "WrongIdentity").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("risk", "open", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("identity"));
    assert_eq!(current.result.decision, expected);
    mode(&backend, "Reject").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("risk", "history", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert_eq!(current.result.rows.len(), 1);
    mode(&backend, "Normal").await;
    let fields = json!({"profile":"ES.Standard","horizon":"Daily","workflow":workflow});
    current = state
        .execute(
            &backend,
            &registry,
            &request("observation", "history", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert!(current.result.rows.is_empty());
    current = state
        .execute(&backend, &registry, &request("observation", "open", fields))
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(current.result.detail.as_ref().unwrap().revision, 9);
    let invalid = state
        .execute(
            &backend,
            &registry,
            &request(
                "risk",
                "history",
                json!({"portfolio":"1","fund":"2","date":"not-a-date"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(invalid.outcomes[0].state, "Failed");
    assert!(invalid.status.contains("yyyy-MM-dd"));
}

#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn strategy_native_history_hydration_notifications_selection_and_revision_fences() {
    use ifm_ui_egui::{
        backend::schema::Registry,
        services::{business_context::BusinessContext, strategy, strategy_stream},
        state::strategy::StrategyController,
    };
    use std::sync::Arc;
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(1),
    })
    .await
    .unwrap();
    let registry = Arc::new(Registry::compiled().unwrap());
    let mut state = StrategyController::default();
    state.refresh(&backend, &registry, false).await.unwrap();
    let snapshot = state.snapshot(&registry).unwrap();
    assert_eq!(snapshot.rows.len(), 1, "{}", snapshot.status);
    assert_eq!(snapshot.signals.len(), 1, "{}", snapshot.status);
    assert_eq!(snapshot.rows[0].futures_price, "6500.25");
    assert!(!snapshot.roots.is_empty());
    assert_eq!(
        snapshot.graph_end_utc - snapshot.graph_start_utc,
        8.0 * 3600.0
    );
    assert!(!snapshot.has_more);
    let token = snapshot.rows[0].action;
    state.action(11).unwrap();
    assert!(state.snapshot(&registry).unwrap().rows.is_empty());
    state.action(10).unwrap();
    state.action(token).unwrap();
    assert_eq!(
        state.snapshot(&registry).unwrap().workflow_id,
        "b7329333f0e3478eb8846d7ef9d47971"
    );
    async fn control(backend: &Backend, verb: &str, value: &str) {
        service_result(
            &backend
                .request(
                    &ActorSubject::query("NativeCompatibility", verb, "none"),
                    &value.into(),
                    None,
                    None,
                )
                .await
                .unwrap(),
        )
        .unwrap();
    }
    control(&backend, "StrategyMode", "WrongIdentity").await;
    let context = BusinessContext::load(&backend).await.unwrap();
    let (start, end) = strategy::graph_window(&context, "Daily").unwrap();
    assert!(
        strategy::workflow_page(
            &backend,
            &registry,
            "ES",
            "Daily",
            start,
            end,
            1,
            tokio::time::Instant::now() + Duration::from_secs(1)
        )
        .await
        .unwrap_err()
        .contains("identity")
    );
    control(&backend, "StrategyMode", "Normal").await;
    let (actions, rx) = tokio::sync::mpsc::channel(8);
    let (tx, mut updates) = tokio::sync::mpsc::channel(8);
    let worker = tokio::spawn(strategy_stream::run(
        backend.clone(),
        registry.clone(),
        rx,
        tx,
    ));
    let initial = tokio::time::timeout(Duration::from_secs(2), updates.recv())
        .await
        .unwrap()
        .unwrap()
        .unwrap();
    assert_eq!(initial.rows.len(), 1, "{}", initial.status);
    control(&backend, "StrategyEmit", "DuplicateSignal").await;
    match tokio::time::timeout(Duration::from_millis(80), updates.recv()).await {
        Err(_) => {}
        Ok(Some(Err(error))) => panic!("Duplicate signal produced error: {error}"),
        Ok(Some(Ok(snapshot))) => panic!(
            "Duplicate signal produced publication: {} / {} signals",
            snapshot.status,
            snapshot.signals.len()
        ),
        Ok(None) => panic!("Strategy owner stopped after duplicate signal"),
    }
    control(&backend, "StrategyEmit", "Stale").await;
    assert!(
        tokio::time::timeout(Duration::from_millis(80), updates.recv())
            .await
            .is_err()
    );
    control(&backend, "StrategyEmit", "Signal").await;
    let signal = tokio::time::timeout(Duration::from_secs(1), updates.recv())
        .await
        .unwrap()
        .unwrap()
        .unwrap();
    assert_eq!(signal.signals.len(), 2);
    assert!(signal.signals.iter().any(|p| p.price == 6510.5));
    control(&backend, "StrategyEmit", "Workflow").await;
    let completed = tokio::time::timeout(Duration::from_secs(1), updates.recv())
        .await
        .unwrap()
        .unwrap()
        .unwrap();
    assert_eq!(completed.rows[0].end_state, "Approved");
    assert_eq!(completed.revision, 10);
    control(&backend, "StrategyEmit", "Conflict").await;
    let error = tokio::time::timeout(Duration::from_secs(1), updates.recv())
        .await
        .unwrap()
        .unwrap()
        .unwrap_err();
    assert!(error.contains("Conflicting"));
    let retained = updates.recv().await.unwrap().unwrap();
    assert_eq!(retained.revision, 10);
    assert_eq!(retained.rows[0].end_state, "Approved");
    actions.send(11).await.unwrap();
    let weekly = tokio::time::timeout(Duration::from_secs(1), updates.recv())
        .await
        .unwrap()
        .unwrap()
        .unwrap();
    assert_eq!(weekly.time_frame, "Weekly");
    assert!(weekly.rows.is_empty());
    drop(actions);
    tokio::time::timeout(Duration::from_secs(1), worker)
        .await
        .unwrap()
        .unwrap();
}

#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn parameter_native_drafts_lifecycle_metrics_and_authoritative_unknown_reconciliation() {
    use ifm_ui_egui::{backend::schema::Registry, state::parameters::ParameterController};
    use serde_json::{Value as Json, json};
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(1),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = ParameterController::default();
    let request = |action: &str, generation: u64, fields: Json| json!({"schema":1,"view":"parameter-sets","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"originalId":null,"fields":fields,"documents":{}});
    async fn mode(backend: &Backend, name: &str) {
        service_result(
            &backend
                .request(
                    &ActorSubject::query("NativeCompatibility", "ParameterMode", "none"),
                    &name.into(),
                    None,
                    None,
                )
                .await
                .unwrap(),
        )
        .unwrap();
    }
    let mut current = state
        .execute(&backend, &registry, &request("load", 0, json!({})))
        .await
        .unwrap();
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    let directory = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");
    let golden: Json = serde_json::from_slice(
        &std::fs::read(directory.join("parameter-initial-display.json")).unwrap(),
    )
    .unwrap();
    assert_eq!(
        serde_json::to_value(&current.result.context).unwrap(),
        golden["context"]
    );
    assert_eq!(
        serde_json::to_value(&current.result.choices).unwrap(),
        golden["choices"]
    );
    assert_eq!(
        current.result.rows.len(),
        golden["rows"].as_array().unwrap().len()
    );
    assert_eq!(
        serde_json::to_value(&current.result.rows[0].fields).unwrap(),
        golden["rows"][0]["Fields"]
    );
    for key in [
        "components",
        "fields",
        "signals",
        "observations",
        "intervals",
    ] {
        let actual: Json = rmp_serde::from_slice(&current.result.documents[key]).unwrap();
        let expected: Json = rmp_serde::from_slice(
            &std::fs::read(directory.join(format!("parameter-initial-{key}.msgpack"))).unwrap(),
        )
        .unwrap();
        assert_eq!(actual, expected, "initial {key}");
    }
    current = state
        .execute(
            &backend,
            &registry,
            &request("new-draft", current.generation, json!({"horizon":"Daily"})),
        )
        .await
        .unwrap();
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    assert_eq!(current.result.context["editing"], "True");
    let fields: Json = rmp_serde::from_slice(&current.result.documents["fields"]).unwrap();
    let mut field = fields
        .as_array()
        .unwrap()
        .iter()
        .find(|v| v["Path"] == "/DefaultTradingDays/Daily")
        .unwrap()
        .clone();
    field["Value"] = json!("2");
    let mut intent = request(
        "stage-fields",
        current.generation,
        json!({"name":"Native draft","description":"Native fixture"}),
    );
    intent["documents"]["changes"] = json!([field]);
    current = state.execute(&backend, &registry, &intent).await.unwrap();
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    current = state
        .execute(
            &backend,
            &registry,
            &request("validate-parameter", current.generation, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    let validation: Json = rmp_serde::from_slice(&current.result.documents["validation"]).unwrap();
    assert_eq!(validation["IsValid"], true);
    let mut save = request("save-draft", current.generation, json!({}));
    save["documents"] = json!({"signals":[],"observations":[],"intervals":[]});
    current = state.execute(&backend, &registry, &save).await.unwrap();
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    assert_eq!(current.result.context["editing"], "False");
    assert_eq!(current.result.context["name"], "Native draft");
    let repeated = state.execute(&backend, &registry, &save).await.unwrap();
    assert_eq!(repeated.outcomes[0].state, "Completed");
    current = repeated;
    for (action, fields) in [
        ("schema-parameter", json!({})),
        ("payload-parameter", json!({})),
        ("compare-parameter", json!({})),
        ("audit-parameter", json!({})),
        ("publish-parameter", json!({})),
        (
            "rename-parameter",
            json!({"name":"Renamed fixture","description":"renamed"}),
        ),
        ("retire-parameter", json!({})),
        ("startup-preview", json!({})),
        ("startup-runs", json!({})),
        (
            "startup-report",
            json!({"runId":uuid::Uuid::new_v4().to_string()}),
        ),
        (
            "signal-monitoring",
            json!({"runId":uuid::Uuid::new_v4().to_string()}),
        ),
        ("legacy-parameters", json!({})),
    ] {
        current = state
            .execute(
                &backend,
                &registry,
                &request(action, current.generation, fields),
            )
            .await
            .unwrap();
        assert_eq!(
            current.outcomes[0].state, "Completed",
            "{action}: {}",
            current.outcomes[0].message
        );
    }
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "component",
                current.generation,
                json!({"component":"strategy-workflow.regime-discovery"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    assert_eq!(current.result.context["regime"], "True");
    current = state
        .execute(
            &backend,
            &registry,
            &request("edit-draft", current.generation, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    let mut metrics = request("stage-metrics", current.generation, json!({}));
    metrics["documents"]["signals"] =
        rmp_serde::from_slice::<Json>(&current.result.documents["signals"]).unwrap();
    metrics["documents"]["observations"] =
        rmp_serde::from_slice::<Json>(&current.result.documents["observations"]).unwrap();
    current = state.execute(&backend, &registry, &metrics).await.unwrap();
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    let mut intervals = request("intervals", current.generation, json!({}));
    intervals["documents"]["intervals"] =
        rmp_serde::from_slice::<Json>(&current.result.documents["intervals"]).unwrap();
    current = state
        .execute(&backend, &registry, &intervals)
        .await
        .unwrap();
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    for action in [
        "save-draft",
        "publish-parameter",
        "assignment",
        "assign-parameter",
        "disable-assignment",
    ] {
        current = state
            .execute(
                &backend,
                &registry,
                &request(action, current.generation, json!({})),
            )
            .await
            .unwrap();
        assert_eq!(
            current.outcomes[0].state, "Completed",
            "{action}: {}",
            current.outcomes[0].message
        );
    }
    for scenario in ["DropReply", "MalformedAck"] {
        mode(&backend, scenario).await;
        let assign = request("assign-parameter", current.generation, json!({}));
        current = state.execute(&backend, &registry, &assign).await.unwrap();
        assert_eq!(
            current.outcomes[0].state, "Unknown",
            "{}",
            current.outcomes[0].message
        );
        let repeated = state.execute(&backend, &registry, &assign).await.unwrap();
        assert_eq!(repeated.outcomes[0].state, "Unknown");
        mode(&backend, "Normal").await;
        current = state
            .execute(
                &backend,
                &registry,
                &request("resume-parameter", current.generation, json!({})),
            )
            .await
            .unwrap();
        assert_eq!(
            current.outcomes[0].state, "Completed",
            "{}",
            current.outcomes[0].message
        );
        let reconciled = state.execute(&backend, &registry, &assign).await.unwrap();
        assert_eq!(reconciled.outcomes[0].state, "Completed");
        // Duplicate receipts advance the view generation without resubmission.
        current = reconciled;
    }
    mode(&backend, "LegacyPages").await;
    for action in ["legacy-parameters", "legacy-next", "legacy-prev"] {
        current = state
            .execute(
                &backend,
                &registry,
                &request(action, current.generation, json!({})),
            )
            .await
            .unwrap();
        assert_eq!(
            current.outcomes[0].state, "Completed",
            "{action}: {}",
            current.outcomes[0].message
        );
    }
    mode(&backend, "Normal").await;
    let legacy = "a0125491-6a62-4c77-a9de-88aa8ad785aa:1";
    for action in ["legacy-parameters", "legacy-preview", "legacy-create"] {
        current = state
            .execute(
                &backend,
                &registry,
                &request(action, current.generation, json!({"legacyId":legacy})),
            )
            .await
            .unwrap();
        assert_eq!(
            current.outcomes[0].state, "Completed",
            "{action}: {}",
            current.outcomes[0].message
        );
    }
    let mut selection = request("select", current.generation, json!({}));
    selection["originalId"] = json!(current.result.rows[0].id);
    current = state
        .execute(&backend, &registry, &selection)
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed");
    for action in ["edit-draft", "discard"] {
        current = state
            .execute(
                &backend,
                &registry,
                &request(action, current.generation, json!({})),
            )
            .await
            .unwrap();
        assert_eq!(
            current.outcomes[0].state, "Completed",
            "{action}: {}",
            current.outcomes[0].message
        );
    }
    for scenario in ["DropReply", "MalformedAck", "SlowState"] {
        current = state
            .execute(
                &backend,
                &registry,
                &request("new-draft", current.generation, json!({"horizon":"Daily"})),
            )
            .await
            .unwrap();
        assert_eq!(
            current.outcomes[0].state, "Completed",
            "{}",
            current.outcomes[0].message
        );
        mode(&backend, scenario).await;
        let save = request(
            "save-draft",
            current.generation,
            json!({"name":"Uncertain draft"}),
        );
        current = state.execute(&backend, &registry, &save).await.unwrap();
        assert_eq!(
            current.outcomes[0].state, "Unknown",
            "{scenario}: {}",
            current.outcomes[0].message
        );
        let duplicate = state.execute(&backend, &registry, &save).await.unwrap();
        assert_eq!(duplicate.outcomes[0].state, "Unknown");
        current = duplicate;
        let blocked = state
            .execute(
                &backend,
                &registry,
                &request("edit-draft", current.generation, json!({})),
            )
            .await
            .unwrap();
        assert_eq!(blocked.outcomes[0].state, "Failed");
        assert!(blocked.outcomes[0].message.contains("uncertain"));
        mode(&backend, "Normal").await;
        current = state
            .execute(
                &backend,
                &registry,
                &request("resume-parameter", current.generation, json!({})),
            )
            .await
            .unwrap();
        assert_eq!(
            current.outcomes[0].state, "Completed",
            "{}",
            current.outcomes[0].message
        );
    }
    mode(&backend, "Reject").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("publish-parameter", current.generation, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    mode(&backend, "Normal").await;
}

#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn backup_native_admin_uses_typed_api_and_bounds_uncertain_operations() {
    use futures_util::StreamExt;
    use ifm_ui_egui::{backend::schema::Registry, state::database_backup::BackupController};
    use serde_json::{Value as Json, json};
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(1),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = BackupController::default();
    let request = |action: &str, mut fields: Json| {
        fields
            .as_object_mut()
            .unwrap()
            .insert("source".into(), json!("LocalWorkstation"));
        json!({"schema":1,"view":"database-backup","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"fields":fields})
    };
    async fn mode(backend: &Backend, name: &str) {
        service_result(
            &backend
                .request(
                    &ActorSubject::query("NativeCompatibility", "BackupMode", "none"),
                    &name.into(),
                    None,
                    None,
                )
                .await
                .unwrap(),
        )
        .unwrap();
    }
    let current = state
        .execute(&backend, &registry, &request("load", json!({})))
        .await
        .unwrap();
    assert_eq!(
        current.outcomes[0].state, "Completed",
        "{}",
        current.outcomes[0].message
    );
    let mut notices = state.take_subscription().unwrap();
    let dashboard: Json = rmp_serde::from_slice(&current.result.documents["dashboard"]).unwrap();
    assert_eq!(dashboard["protectionSets"].as_array().unwrap().len(), 3);
    assert_eq!(dashboard["recentOperations"][0]["engine"], "PostgreSql");
    let select = json!({"operationSource":"LocalWorkstation","operationId":"d256f7a9-b41c-4fb9-9ec3-eab3497bc86a"});
    let output = state
        .execute(
            &backend,
            &registry,
            &request("backup-output", select.clone()),
        )
        .await
        .unwrap();
    assert_eq!(output.outcomes[0].state, "Completed");
    assert!(output.result.context["output"].ends_with("output-1"));
    let output = state
        .execute(
            &backend,
            &registry,
            &request("backup-more-output", select.clone()),
        )
        .await
        .unwrap();
    assert_eq!(output.outcomes[0].state, "Completed");
    assert!(output.result.context["output"].ends_with("output-2"));
    let before = output.result.context["output"].clone();
    let output = state
        .execute(
            &backend,
            &registry,
            &request("backup-more-output", select.clone()),
        )
        .await
        .unwrap();
    assert_eq!(output.result.context["output"], before);
    let policy = state
        .execute(
            &backend,
            &registry,
            &request("backup-policy-load", json!({"policyId":"fixture-policy"})),
        )
        .await
        .unwrap();
    assert_eq!(policy.outcomes[0].state, "Completed");
    let save=state.execute(&backend,&registry,&request("backup-policy-save",json!({"policyId":"fixture-policy","daily":"8","weekly":"4","monthly":"12","rpoMinutes":"20","rtoMinutes":"90","maximumVerificationHours":"24"}))).await.unwrap();
    assert_eq!(
        save.outcomes[0].state, "Completed",
        "{}",
        save.outcomes[0].message
    );
    let policy = state
        .execute(
            &backend,
            &registry,
            &request("backup-policy-load", json!({"policyId":"fixture-policy"})),
        )
        .await
        .unwrap();
    let policy: Json = rmp_serde::from_slice(&policy.result.documents["policy"]).unwrap();
    assert_eq!(policy["daily"], 8);
    assert_eq!(policy["revision"], 8);
    let run = request(
        "backup-run",
        json!({"protectionSets":"postgres-main","mode":"Automatic"}),
    );
    let completed = state.execute(&backend, &registry, &run).await.unwrap();
    assert_eq!(
        completed.outcomes[0].state, "Completed",
        "{}",
        completed.outcomes[0].message
    );
    tokio::time::timeout(Duration::from_secs(1), notices.next())
        .await
        .unwrap()
        .unwrap();
    let duplicate = state.execute(&backend, &registry, &run).await.unwrap();
    assert_eq!(duplicate.outcomes[0].state, "Completed");
    let cancelled = state
        .execute(
            &backend,
            &registry,
            &request("backup-cancel", select.clone()),
        )
        .await
        .unwrap();
    assert_eq!(
        cancelled.outcomes[0].state, "Completed",
        "{}",
        cancelled.outcomes[0].message
    );
    let restore=state.execute(&backend,&registry,&request("backup-restore",json!({"protectionSet":"postgres-main","restorePoint":"fixture-point","targetProfile":"isolated-target","validationProfile":"fixture-validation"}))).await.unwrap();
    assert_eq!(
        restore.outcomes[0].state, "Completed",
        "{}",
        restore.outcomes[0].message
    );
    mode(&backend, "NoOutputAdvance").await;
    let bad = state
        .execute(
            &backend,
            &registry,
            &request("backup-output", select.clone()),
        )
        .await
        .unwrap();
    assert_eq!(bad.outcomes[0].state, "Failed");
    assert!(bad.outcomes[0].message.contains("advance"));
    mode(&backend, "Normal").await;
    for scenario in ["DropReply", "MalformedAck", "EmptyReceipt"] {
        state
            .execute(&backend, &registry, &request("load", json!({})))
            .await
            .unwrap();
        mode(&backend, scenario).await;
        let intent = request(
            "backup-run",
            json!({"protectionSets":"postgres-main","mode":"Full"}),
        );
        let unknown = state.execute(&backend, &registry, &intent).await.unwrap();
        assert_eq!(
            unknown.outcomes[0].state, "Unknown",
            "{scenario}: {}",
            unknown.outcomes[0].message
        );
        let duplicate = state.execute(&backend, &registry, &intent).await.unwrap();
        assert_eq!(duplicate.outcomes[0].state, "Unknown");
        let blocked = state
            .execute(
                &backend,
                &registry,
                &request(
                    "backup-run",
                    json!({"protectionSets":"postgres-main","mode":"Full"}),
                ),
            )
            .await
            .unwrap();
        assert_eq!(blocked.outcomes[0].state, "Failed");
        mode(&backend, "Normal").await;
    }
    state
        .execute(&backend, &registry, &request("load", json!({})))
        .await
        .unwrap();
    mode(&backend, "Reject").await;
    let rejected = state
        .execute(
            &backend,
            &registry,
            &request(
                "backup-run",
                json!({"protectionSets":"postgres-main","mode":"Full"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(rejected.outcomes[0].state, "Failed");
    mode(&backend, "Normal").await;
    state
        .execute(
            &backend,
            &registry,
            &request("backup-clear-output", json!({})),
        )
        .await
        .unwrap();
    mode(&backend, "SlowRefresh").await;
    let saved = state
        .execute(
            &backend,
            &registry,
            &request(
                "backup-run",
                json!({"protectionSets":"postgres-main","mode":"Full"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(
        saved.outcomes[0].state, "Completed",
        "{}",
        saved.outcomes[0].message
    );
    assert!(saved.outcomes[0].message.contains("Accepted"));
    mode(&backend, "Normal").await;
    mode(&backend, "RepeatHistory").await;
    state
        .execute(&backend, &registry, &request("load", json!({})))
        .await
        .unwrap();
    let repeated = state
        .execute(
            &backend,
            &registry,
            &request("backup-older", json!({"historySource":"LocalWorkstation"})),
        )
        .await
        .unwrap();
    assert_eq!(repeated.outcomes[0].state, "Failed");
    mode(&backend, "Normal").await;
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn scheduled_tasks_native_crud_history_output_notifications_and_unknown_acknowledgements() {
    use futures_util::StreamExt;
    use ifm_ui_egui::{backend::schema::Registry, state::scheduled_tasks::ScheduledController};
    use serde_json::json;
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(1),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = ScheduledController::default();
    let schedule = "a256f7a9-b41c-4fb9-9ec3-eab3497bc86a";
    let run = "b256f7a9-b41c-4fb9-9ec3-eab3497bc86a";
    let uncertain = "c256f7a9-b41c-4fb9-9ec3-eab3497bc86a";
    let request = |action: &str, generation: u64, selected: &str, mut fields: serde_json::Value| {
        let map = fields.as_object_mut().unwrap();
        map.entry("environment").or_insert(json!("Development"));
        map.entry("hostId").or_insert(json!("development"));
        json!({"schema":1,"view":"scheduled-tasks","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"originalId":selected,"fields":fields})
    };
    async fn mode(backend: &Backend, name: &str) {
        service_result(
            &backend
                .request(
                    &ActorSubject::query("NativeCompatibility", "ScheduledMode", "none"),
                    &name.into(),
                    None,
                    None,
                )
                .await
                .unwrap(),
        )
        .unwrap();
    }
    let mut current = state
        .execute(&backend, &registry, &request("load", 0, "", json!({})))
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    let mut notices = state
        .take_subscription()
        .expect("Listener must be established before hydration");
    let directory = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");
    let oracle: serde_json::Value = serde_json::from_slice(
        &std::fs::read(directory.join("scheduled-dashboard-display.json")).unwrap(),
    )
    .unwrap();
    assert_eq!(
        serde_json::to_value(&current.result.rows[0].fields).unwrap(),
        oracle["rows"][0]["Fields"]
    );
    assert_eq!(
        serde_json::to_value(&current.result.choices).unwrap(),
        oracle["choices"]
    );
    let mut context = serde_json::to_value(&current.result.context).unwrap();
    assert_eq!(
        context.as_object_mut().unwrap().remove("apiAsOfUtc"),
        Some(json!("2026-10-26T12:30:00.0000000Z"))
    );
    assert_eq!(context, oracle["context"]);
    for name in [
        "historyMore",
        "histories",
        "nextFires",
        "runs",
        "preview",
        "projects",
    ] {
        let expected: serde_json::Value = rmp_serde::from_slice(
            &std::fs::read(directory.join(format!("scheduled-document-{name}.msgpack"))).unwrap(),
        )
        .unwrap();
        assert_eq!(
            current.result.document::<serde_json::Value>(name).unwrap(),
            expected,
            "{name}"
        );
    }
    assert!(current.result.has_more);
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert!(!current.result.has_more);
    assert_eq!(
        current
            .result
            .document::<serde_json::Value>("runs")
            .unwrap()
            .as_array()
            .unwrap()
            .len(),
        2
    );
    let refreshed = state.refresh_if_loaded(&backend, &registry).await.unwrap();
    assert!(
        !refreshed.result.has_more,
        "Notification refresh must preserve an exhausted history cursor"
    );
    assert_eq!(
        refreshed
            .result
            .document::<serde_json::Value>("runs")
            .unwrap()
            .as_array()
            .unwrap()
            .len(),
        2
    );
    current = refreshed;
    current = state
        .execute(
            &backend,
            &registry,
            &request("output", current.generation, schedule, json!({"runId":run})),
        )
        .await
        .unwrap();
    assert_eq!(current.result.context["output"], "stdout-1\n");
    assert_eq!(current.result.context["outputEnd"], "False");
    mode(&backend, "NoOutputAdvance").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "more-output",
                current.generation,
                schedule,
                json!({"runId":run}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("did not advance"));
    assert_eq!(current.result.context["output"], "stdout-1\n");
    mode(&backend, "Normal").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "more-output",
                current.generation,
                schedule,
                json!({"runId":run}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed");
    assert_eq!(current.result.context["output"], "stdout-1\nstdout-2");
    assert_eq!(current.result.context["outputEnd"], "True");
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "more-output",
                current.generation,
                schedule,
                json!({"runId":run}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    mode(&backend, "MissingOutput").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("output", current.generation, schedule, json!({"runId":run})),
        )
        .await
        .unwrap();
    assert_eq!(current.result.context["output"], "tail\nerror tail");
    assert_eq!(current.result.context["outputEnd"], "True");
    mode(&backend, "Normal").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("history", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    mode(&backend, "RepeatHistory").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("did not advance"));
    mode(&backend, "Normal").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("enable", 0, schedule, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    let enable = request("enable", current.generation, schedule, json!({}));
    current = state.execute(&backend, &registry, &enable).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(current.result.rows[0].fields["enabled"], "True");
    let notice = tokio::time::timeout(Duration::from_secs(1), notices.next())
        .await
        .unwrap()
        .unwrap();
    assert_eq!(
        codec::decode(&notice.payload).unwrap().as_array().unwrap()[3].as_str(),
        enable["requestId"].as_str()
    );
    let refreshed = state.refresh_if_loaded(&backend, &registry).await.unwrap();
    assert!(refreshed.outcomes.is_empty());
    assert_eq!(refreshed.result.rows[0].fields["enabled"], "True");
    current = refreshed;
    current = state
        .execute(
            &backend,
            &registry,
            &request("remove", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("Disable"));
    current = state
        .execute(
            &backend,
            &registry,
            &request("disable", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    current = state
        .execute(
            &backend,
            &registry,
            &request("run", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    current = state
        .execute(
            &backend,
            &registry,
            &request("history", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "resolve",
                current.generation,
                schedule,
                json!({"runId":uncertain,"reason":"Reviewed offline interruption"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    let fields = json!({"taskKey":"test-task","name":"Native task","oneTime":"False","expression":"0 1 17 ? * MON-FRI","timeZoneId":"America/New_York","startsAtUtc":"","endsAtUtc":"","maximumRuntimeSeconds":"1800","dispatchToleranceSeconds":"60","description":"Native fixture"});
    let mut invalid = fields.clone();
    invalid["expression"] = json!("invalid");
    current = state
        .execute(
            &backend,
            &registry,
            &request("preview", current.generation, "", invalid),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed");
    assert_eq!(
        current
            .result
            .document::<serde_json::Value>("preview")
            .unwrap()["valid"],
        false
    );
    current = state
        .execute(
            &backend,
            &registry,
            &request("add", current.generation, "", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    let added = current
        .result
        .rows
        .iter()
        .find(|r| r.id != schedule)
        .unwrap()
        .id
        .clone();
    current = state
        .execute(
            &backend,
            &registry,
            &request("change", current.generation, &added, fields),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    current = state
        .execute(
            &backend,
            &registry,
            &request("remove", current.generation, &added, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert!(!current.result.rows.iter().any(|r| r.id == added));
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "enable",
                current.generation,
                schedule,
                json!({"hostId":"other-host"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("Reload"));
    mode(&backend, "BadHost").await;
    current = state
        .execute(&backend, &registry, &request("load", 0, "", json!({})))
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("requested host"));
    mode(&backend, "Normal").await;
    current = state
        .execute(&backend, &registry, &request("load", 0, "", json!({})))
        .await
        .unwrap();
    mode(&backend, "Reject").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("enable", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    mode(&backend, "DropReply").await;
    let unknown = request("enable", current.generation, schedule, json!({}));
    current = state.execute(&backend, &registry, &unknown).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Unknown", "{}", current.status);
    current = state.execute(&backend, &registry, &unknown).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Unknown");
    mode(&backend, "Normal").await;
    current = state
        .execute(&backend, &registry, &request("load", 0, "", json!({})))
        .await
        .unwrap();
    assert_eq!(current.result.rows[0].fields["enabled"], "True");
    mode(&backend, "SlowRefresh").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("disable", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    mode(&backend, "Normal").await;
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert!(current.status.starts_with("Saved; reload scheduled tasks"));
    for fault in ["MalformedAck", "EmptyReceipt"] {
        mode(&backend, "Normal").await;
        current = state
            .execute(&backend, &registry, &request("load", 0, "", json!({})))
            .await
            .unwrap();
        mode(&backend, fault).await;
        let operation = request("enable", current.generation, schedule, json!({}));
        current = state
            .execute(&backend, &registry, &operation)
            .await
            .unwrap();
        assert_eq!(
            current.outcomes[0].state, "Unknown",
            "{fault}: {}",
            current.status
        );
        current = state
            .execute(&backend, &registry, &operation)
            .await
            .unwrap();
        assert_eq!(current.outcomes[0].state, "Unknown");
    }
    let blocked = state
        .execute(
            &backend,
            &registry,
            &request("run", current.generation, schedule, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(blocked.outcomes[0].state, "Failed");
    assert!(
        blocked.status.contains("Reload"),
        "An uncertain write requires authoritative reload before another mutation"
    );
    mode(&backend, "Normal").await;
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn provider_browser_pinned_pages_preview_publish_and_identity_fences() {
    use ifm_ui_egui::{backend::schema::Registry, state::provider_definitions::ProviderController};
    use serde_json::json;
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(1),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = ProviderController::default();
    let request = |action: &str, generation: u64, selected: &str, fields: serde_json::Value| json!({"schema":1,"view":"provider-definitions","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"originalId":selected,"fields":fields});
    async fn mode(backend: &Backend, name: &str) {
        service_result(
            &backend
                .request(
                    &ActorSubject::query("NativeCompatibility", "ProviderMode", "none"),
                    &name.into(),
                    None,
                    None,
                )
                .await
                .unwrap(),
        )
        .unwrap();
    }
    let filters = json!({"dataset":"GLBX.MDP3","root":"ES","options":"false","right":"Unknown","includeHistory":"false"});
    let mut current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", filters.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(current.result.rows.len(), 1);
    assert!(current.result.has_more);
    let first = current.result.rows[0].id.clone();
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", filters.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert!(!current.result.has_more);
    assert_ne!(first, current.result.rows[0].id);
    // Pinning failures retain the useful previous page and never import it under
    // a different snapshot. A repeated cursor is an explicit failure, not a loop.
    current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", filters.clone()),
        )
        .await
        .unwrap();
    mode(&backend, "WrongSnapshot").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", filters.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert_eq!(current.result.rows[0].id, first);
    mode(&backend, "Normal").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", filters.clone()),
        )
        .await
        .unwrap();
    mode(&backend, "RepeatCursor").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", filters.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("did not advance"));
    mode(&backend, "Oversized").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", filters.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    mode(&backend, "Normal").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", filters.clone()),
        )
        .await
        .unwrap();
    let selected = current.result.rows[0].id.clone();
    let mapping = json!({"exchangeTimeZoneId":"America/Chicago"});
    current = state
        .execute(
            &backend,
            &registry,
            &request("preview", 0, &selected, mapping.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    current = state
        .execute(
            &backend,
            &registry,
            &request("preview", current.generation, &selected, mapping.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(current.result.context["previewMode"], "Add");
    let preview: serde_json::Value = current.result.document("previewContract").unwrap();
    assert_eq!(preview["contractId"], "ES20261217");
    assert_eq!(preview["multiplier"], "50.00");
    let token = current.result.context["previewToken"].clone();
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "publish",
                current.generation,
                &selected,
                json!({"previewToken":"wrong"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "publish",
                current.generation,
                &selected,
                json!({"previewToken":token}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert!(!current.result.context.contains_key("previewToken"));
    // Consumed previews cannot be submitted again with a new request identity.
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "publish",
                current.generation,
                &selected,
                json!({"previewToken":token}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    current=state.execute(&backend,&registry,&request("preview",current.generation,&selected,json!({"exchangeTimeZoneId":"Central Standard Time","existingContractId":"ES20261217"}))).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    let token = current.result.context["previewToken"].clone();
    mode(&backend, "ChangedOriginal").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "publish",
                current.generation,
                &selected,
                json!({"previewToken":token}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("existing contract changed"));
    mode(&backend, "Normal").await;
    let option_filters =
        json!({"dataset":"GLBX.MDP3","root":"ES","options":"true","right":"Unknown"});
    current = state
        .execute(&backend, &registry, &request("load", 0, "", option_filters))
        .await
        .unwrap();
    let selected = current.result.rows[0].id.clone();
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "preview",
                current.generation,
                &selected,
                json!({"exchangeTimeZoneId":"America/Chicago","underlyingContractId":"ES20261217"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    let preview: serde_json::Value = current.result.document("previewContract").unwrap();
    assert_eq!(preview["strikePriceDecimal"], "6500.2500");
    assert_eq!(preview["underlyingContractId"], "ES20261217");
    let token = current.result.context["previewToken"].clone();
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "publish",
                current.generation,
                &selected,
                json!({"previewToken":token}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    mode(&backend, "Expired").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", filters.clone()),
        )
        .await
        .unwrap();
    let selected = current.result.rows[0].id.clone();
    current = state
        .execute(
            &backend,
            &registry,
            &request("preview", current.generation, &selected, mapping.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("current, complete"));
    mode(&backend, "NoTerminal").await;
    current = state
        .execute(&backend, &registry, &request("load", 0, "", filters))
        .await
        .unwrap();
    let selected = current.result.rows[0].id.clone();
    current = state
        .execute(
            &backend,
            &registry,
            &request("preview", current.generation, &selected, mapping),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    let token = current.result.context["previewToken"].clone();
    let unknown = request(
        "publish",
        current.generation,
        &selected,
        json!({"previewToken":token}),
    );
    current = state.execute(&backend, &registry, &unknown).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Unknown", "{}", current.status);
    current = state.execute(&backend, &registry, &unknown).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Unknown");
    mode(&backend, "Normal").await;
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn option_editor_paging_crud_review_and_uncertain_outcomes() {
    use ifm_ui_egui::{
        backend::schema::Registry, state::option_contract::OptionContractController,
    };
    use serde_json::json;
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(1),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = OptionContractController::default();
    let request = |action: &str, generation: u64, original: &str, fields: serde_json::Value| json!({"schema":1,"view":"option-contract","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"originalId":original,"fields":fields});
    async fn mode(backend: &Backend, name: &str) {
        service_result(
            &backend
                .request(
                    &ActorSubject::query("NativeCompatibility", "OptionPageMode", "none"),
                    &name.into(),
                    None,
                    None,
                )
                .await
                .unwrap(),
        )
        .unwrap();
    }
    mode(&backend, "Normal").await;
    let mut current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", json!({"symbol":"ES"})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(current.result.rows.len(), 200);
    assert!(current.result.has_more);
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(current.result.rows.len(), 301);
    assert!(!current.result.has_more);
    let provider = current
        .result
        .rows
        .iter()
        .find(|r| r.fields["provider"] == "true")
        .unwrap()
        .id
        .clone();
    // Cursor faults get one bounded restart; they never append duplicate rows.
    for fault in ["ExpiredCursor", "RepeatCursor", "WrongSymbol"] {
        mode(&backend, "Normal").await;
        current = state
            .execute(
                &backend,
                &registry,
                &request("load", 0, "", json!({"symbol":"ES"})),
            )
            .await
            .unwrap();
        mode(&backend, fault).await;
        current = state
            .execute(
                &backend,
                &registry,
                &request("next", current.generation, "", json!({})),
            )
            .await
            .unwrap();
        assert_eq!(current.result.rows.len(), 200, "{fault}");
        if fault == "RepeatCursor" {
            assert_eq!(current.outcomes[0].state, "Failed");
            assert!(current.status.contains("did not advance"));
        } else {
            assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
            current = state
                .execute(
                    &backend,
                    &registry,
                    &request("next", current.generation, "", json!({})),
                )
                .await
                .unwrap();
            assert_eq!(
                current.outcomes[0].state, "Failed",
                "{fault} must not restart indefinitely"
            );
        }
    }
    mode(&backend, "Normal").await;
    current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", json!({"symbol":"MES"})),
        )
        .await
        .unwrap();
    assert_eq!(current.result.rows.len(), 1);
    assert!(!current.result.has_more);
    assert_eq!(current.result.rows[0].fields["symbol"], "MES");
    current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", json!({"symbol":"ES"})),
        )
        .await
        .unwrap();
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", json!({})),
        )
        .await
        .unwrap();
    current = state
        .execute(
            &backend,
            &registry,
            &request("change", 0, &provider, json!({"description":"stale"})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    current = state
        .execute(
            &backend,
            &registry,
            &request(
                "change",
                current.generation,
                &provider,
                json!({"description":"Native option edit","symbol":"EVIL","strikePrice":"1"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    // Saved mutations refresh the first page. Fetch the remainder for review.
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", json!({})),
        )
        .await
        .unwrap();
    let row = current
        .result
        .rows
        .iter()
        .find(|r| r.id == provider)
        .unwrap();
    assert_eq!(row.fields["symbol"], "ES");
    assert_eq!(row.fields["strikePrice"], "6500.2500");
    let mut review = serde_json::to_value(&row.fields).unwrap();
    review["reviewState"] = json!("Draft");
    review["mappingVersion"] = json!("Native-option-map");
    current = state
        .execute(
            &backend,
            &registry,
            &request("review", current.generation, &provider, review.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", json!({})),
        )
        .await
        .unwrap();
    review["reviewState"] = json!("Reviewed");
    review["evidenceId"] = json!("");
    current = state
        .execute(
            &backend,
            &registry,
            &request("review", current.generation, &provider, review.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed", "{}", current.status);
    review["reviewState"] = json!("Draft");
    review["effectiveFromUtc"] = json!("2026-10-26T08:30:00-04:00");
    current = state
        .execute(
            &backend,
            &registry,
            &request("review", current.generation, &provider, review),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("UTC timestamp"));
    let mut fields = json!({"symbol":"ES","localSymbol":"NativeOption","securityType":"FOP","currency":"USD","exchange":"CME","multiplier":"50","contractMonth":"2027-06-18","optionType":"Put","strikePrice":"7000.1250","description":"Native legacy option"});
    let legacy = "ES20270618P7000.125";
    current = state
        .execute(
            &backend,
            &registry,
            &request("add", current.generation, "", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", json!({})),
        )
        .await
        .unwrap();
    assert!(current.result.rows.iter().any(|r| r.id == legacy));
    fields["strikePrice"] = json!("7001");
    current = state
        .execute(
            &backend,
            &registry,
            &request("change", current.generation, legacy, fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("identity cannot be changed"));
    fields["strikePrice"] = json!("7000.1250");
    fields["description"] = json!("Reject");
    current = state
        .execute(
            &backend,
            &registry,
            &request("change", current.generation, legacy, fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("rejected"));
    fields["description"] = json!("NoTerminal");
    let unknown = request("change", current.generation, legacy, fields.clone());
    current = state.execute(&backend, &registry, &unknown).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Unknown");
    current = state.execute(&backend, &registry, &unknown).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Unknown");
    current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", json!({"symbol":"ES"})),
        )
        .await
        .unwrap();
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", json!({})),
        )
        .await
        .unwrap();
    assert_eq!(
        current
            .result
            .rows
            .iter()
            .find(|r| r.id == legacy)
            .unwrap()
            .fields["description"],
        "NoTerminal"
    );
    fields["description"] = json!("SlowRefresh");
    current = state
        .execute(
            &backend,
            &registry,
            &request("change", current.generation, legacy, fields),
        )
        .await
        .unwrap();
    mode(&backend, "Normal").await;
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert!(current.status.starts_with("Saved; reload option contracts"));
    current = state
        .execute(
            &backend,
            &registry,
            &request("load", 0, "", json!({"symbol":"ES"})),
        )
        .await
        .unwrap();
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", json!({})),
        )
        .await
        .unwrap();
    current = state
        .execute(
            &backend,
            &registry,
            &request("remove", current.generation, legacy, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    current = state
        .execute(
            &backend,
            &registry,
            &request("next", current.generation, "", json!({})),
        )
        .await
        .unwrap();
    assert!(!current.result.rows.iter().any(|r| r.id == legacy));
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn futures_editor_crud_review_immutable_identity_explicit_reload_and_unknown_outcomes() {
    use ifm_ui_egui::{
        backend::schema::Registry, state::futures_contract::FuturesContractController,
    };
    use serde_json::json;
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(1),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = FuturesContractController::default();
    let request = |action: &str, generation: u64, original: &str, fields: serde_json::Value| json!({"schema":1,"view":"futures-contract","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"originalId":original,"fields":fields});
    let mut current = state
        .execute(&backend, &registry, &request("load", 0, "", json!({})))
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert!(current.result.choices["symbol"].contains(&"ES".to_string()));
    let provider = "ESZ6.ES.20261218";
    current = state
        .execute(
            &backend,
            &registry,
            &request("change", 0, provider, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("Contracts changed"));
    current=state.execute(&backend,&registry,&request("change",current.generation,provider,json!({"description":"Native provider edit","onTheRun":"true","symbol":"EVIL","multiplier":"999"}))).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(current.result.rows[0].fields["symbol"], "ES");
    assert_eq!(current.result.rows[0].fields["multiplier"], "50");
    let mut review = serde_json::to_value(&current.result.rows[0].fields).unwrap();
    review["reviewState"] = json!("Draft");
    review["mappingVersion"] = json!("Native-map");
    review["evidenceId"] = json!("Reviewed evidence");
    current = state
        .execute(
            &backend,
            &registry,
            &request("review", current.generation, provider, review.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(
        current.result.rows[0].fields["mappingVersion"],
        "Native-map"
    );
    review["reviewState"] = json!("Reviewed");
    current = state
        .execute(
            &backend,
            &registry,
            &request("review", current.generation, provider, review.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("Reference is not qualified"));
    review["reviewState"] = json!("Draft");
    review["effectiveFromUtc"] = json!("2026-10-26T08:30:00-04:00");
    current = state
        .execute(
            &backend,
            &registry,
            &request("review", current.generation, provider, review),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    assert!(current.status.contains("UTC timestamp"));
    let mut fields = json!({"symbol":"ES","localSymbol":"ES-Test","securityType":"FUT","currency":"USD","exchange":"CME","multiplier":"50","lastTradeDate":"2027-03-19","onTheRun":"false","rollover":"false","description":"Native legacy"});
    current = state
        .execute(
            &backend,
            &registry,
            &request("add", current.generation, "", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    let legacy = "ES20270319.ES.20270319";
    assert!(current.result.rows.iter().any(|r| r.id == legacy));
    fields["description"] = json!("NoTerminal");
    let unknown = request("change", current.generation, legacy, fields.clone());
    current = state.execute(&backend, &registry, &unknown).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Unknown");
    current = state.execute(&backend, &registry, &unknown).await.unwrap();
    assert_eq!(current.outcomes[0].state, "Unknown");
    current = state
        .execute(&backend, &registry, &request("load", 0, "", json!({})))
        .await
        .unwrap();
    assert_eq!(
        current
            .result
            .rows
            .iter()
            .find(|r| r.id == legacy)
            .unwrap()
            .fields["description"],
        "NoTerminal"
    );
    fields["description"] = json!("Reject");
    current = state
        .execute(
            &backend,
            &registry,
            &request("change", current.generation, legacy, fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Failed");
    fields["description"] = json!("SlowRefresh");
    current = state
        .execute(
            &backend,
            &registry,
            &request("change", current.generation, legacy, fields),
        )
        .await
        .unwrap();
    let release = ActorSubject::query("NativeCompatibility", "ReleaseFuturesReads", "none");
    service_result(
        &backend
            .request(&release, &Value::Nil, None, None)
            .await
            .unwrap(),
    )
    .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert!(current.status.starts_with("Saved; reload contracts"));
    current = state
        .execute(&backend, &registry, &request("load", 0, "", json!({})))
        .await
        .unwrap();
    current = state
        .execute(
            &backend,
            &registry,
            &request("remove", current.generation, legacy, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed");
    assert!(!current.result.rows.iter().any(|r| r.id == legacy));
    let mut other = FuturesContractController::default();
    let loaded = other
        .execute(&backend, &registry, &request("load", 0, "", json!({})))
        .await
        .unwrap();
    let external = other
        .execute(
            &backend,
            &registry,
            &request(
                "change",
                loaded.generation,
                provider,
                json!({"description":"Another client","onTheRun":"false"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(external.outcomes[0].state, "Completed");
    // The hybrid reloads catalog rows explicitly; another client's command
    // does not replace this editor's current snapshot in the background.
    let refreshed = state
        .execute(&backend, &registry, &request("load", 0, "", json!({})))
        .await
        .unwrap();
    assert_eq!(refreshed.outcomes[0].state, "Completed");
    assert_eq!(
        refreshed
            .result
            .rows
            .iter()
            .find(|r| r.id == provider)
            .unwrap()
            .fields["description"],
        "Another client"
    );
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn dashboard_health_composes_api_clock_and_http_without_retaining_stale_green() {
    use std::{
        io::{Read, Write},
        net::TcpListener,
        sync::{
            Arc,
            atomic::{AtomicBool, Ordering},
        },
    };
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(5),
    })
    .await
    .unwrap();
    let listener = TcpListener::bind("127.0.0.1:0").unwrap();
    listener.set_nonblocking(true).unwrap();
    let endpoint = format!("http://{}/live-health", listener.local_addr().unwrap());
    let stop = Arc::new(AtomicBool::new(false));
    let signal = stop.clone();
    let thread = std::thread::spawn(move || {
        let mut count = 0;
        while !signal.load(Ordering::Acquire) {
            match listener.accept() {
                Ok((mut stream, _)) => {
                    stream.set_nonblocking(false).unwrap();
                    stream
                        .set_read_timeout(Some(Duration::from_secs(1)))
                        .unwrap();
                    let mut request = [0; 4096];
                    if stream.read(&mut request).is_err() {
                        continue;
                    }
                    let observed = if count == 0 {
                        "2026-10-26T12:27:00Z"
                    } else {
                        "2026-10-26T12:29:00Z"
                    };
                    let body=serde_json::json!({"observedUtc":observed,"status":"Healthy","checks":[{"required":true,"status":"Healthy","reason":"Current"}]}).to_string();
                    let status = if count < 2 {
                        "200 OK"
                    } else {
                        "500 Internal Server Error"
                    };
                    count += 1;
                    let response = format!(
                        "HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}",
                        body.len()
                    );
                    let _ = stream.write_all(response.as_bytes());
                }
                Err(error) if error.kind() == std::io::ErrorKind::WouldBlock => {
                    std::thread::sleep(Duration::from_millis(5))
                }
                Err(error) => panic!("Dashboard HTTP fixture: {error}"),
            }
        }
    });
    struct Stop(Arc<AtomicBool>, Option<std::thread::JoinHandle<()>>);
    impl Drop for Stop {
        fn drop(&mut self) {
            self.0.store(true, Ordering::Release);
            let result = self.1.take().unwrap().join();
            if !std::thread::panicking() {
                result.unwrap();
            }
        }
    }
    let _guard = Stop(stop, Some(thread));
    let client =
        ifm_ui_egui::services::dashboard_health::DashboardHealth::new(Some(endpoint)).unwrap();
    let stale = client.load(&backend).await.unwrap();
    assert_eq!(stale.health, "Unknown");
    assert_eq!(stale.running, Some(true));
    let current = client.load(&backend).await.unwrap();
    assert_eq!(current.health, "Healthy");
    let failed = client.load(&backend).await.unwrap();
    assert_eq!(failed.health, "Unknown");
    assert_eq!(failed.running, Some(true));
    assert!(failed.reason.contains("HTTP 500"));
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn outlook_uses_current_contract_api_time_and_published_analytics() {
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(5),
    })
    .await
    .unwrap();
    let registry = ifm_ui_egui::backend::schema::Registry::compiled().unwrap();
    let scenario = ActorSubject::query("NativeCompatibility", "OutlookAvailable", "none");
    service_result(
        &backend
            .request(&scenario, &Value::Boolean(true), None, None)
            .await
            .unwrap(),
    )
    .unwrap();
    let result = ifm_ui_egui::services::outlook::load(&backend, &registry).await;
    service_result(
        &backend
            .request(&scenario, &Value::Boolean(false), None, None)
            .await
            .unwrap(),
    )
    .unwrap();
    let outlook = result.unwrap();
    assert_eq!(outlook.cells["closePrice"].value, "6055.45");
    assert_eq!(outlook.cells["trend"].value, "DownTrending");
    assert!(outlook.status.contains(" | live | complete"));
    assert_eq!(outlook.summary, "ESZ6 | 2026-10-26 | LiveTrading");
    assert!(
        ifm_ui_egui::services::outlook::load(&backend, &registry)
            .await
            .err()
            .unwrap()
            .contains("Optional current preview unavailable")
    );
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn market_history_uses_api_clock_and_current_contracts_without_local_indicators() {
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(5),
    })
    .await
    .unwrap();
    let charts = ifm_ui_egui::services::market_history::load(
        &backend,
        &ifm_ui_egui::backend::schema::Registry::compiled().unwrap(),
    )
    .await
    .unwrap();
    for key in ["ES", "VX", "ES-BB"] {
        assert!(
            charts[key].status.is_empty(),
            "{key}: {}",
            charts[key].status
        );
        assert!(!charts[key].series[0].points.is_empty());
    }
    assert_eq!(charts["ES"].series[0].points[0][1], 6050.125);
    assert_eq!(charts["ES"].series.len(), 3);
    assert_eq!(charts["ES-BB"].series[1].points[0][1], 6040.0);
    assert_eq!(charts["VX"].series[0].points.len(), 2); // same single-bar continuity as hybrid
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn pending_calendar_hydration_does_not_delay_the_recovery_error_publication() {
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(5),
    })
    .await
    .unwrap();
    let scenario = ActorSubject::query("NativeCompatibility", "CalendarHold", "none");
    service_result(
        &backend
            .request(&scenario, &Value::Boolean(true), None, None)
            .await
            .unwrap(),
    )
    .unwrap();
    let registry = std::sync::Arc::new(ifm_ui_egui::backend::schema::Registry::compiled().unwrap());
    let (send, mut receive) = tokio::sync::mpsc::channel(32);
    let (_actions, actions) = tokio::sync::mpsc::channel(32);
    let task = tokio::spawn(ifm_ui_egui::services::notifications::monitor(
        backend.clone(),
        registry,
        send,
        actions,
    ));
    let result = tokio::time::timeout(Duration::from_secs(2), async {
        while let Some(state) = receive.recv().await {
            if state.error.contains("Option pipeline recovery failed") {
                return state;
            }
        }
        panic!("Notification service stopped before reporting recovery failure");
    })
    .await;
    task.abort();
    let _ = task.await;
    // Reset even if the acceptance assertion fails, so later cases stay isolated.
    service_result(
        &backend
            .request(&scenario, &Value::Boolean(false), None, None)
            .await
            .unwrap(),
    )
    .unwrap();
    let state = result.expect("Pending calendar query blocked the feed failure dialog");
    assert!(state.busy, "Calendar hydration should still be pending");
    assert_eq!(state.api_today,"2026-10-26","Calendar reference must arrive from the API even while calendar data is blocked");
    assert!(
        state.error.contains("Option pipeline recovery failed"),
        "{}",
        state.error
    );
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn csharp_request_reply_compression_errors_and_deadlines() {
    let url = std::env::var("IFM_STANDALONE_TEST_NATS_URL").expect("isolated broker URL");
    let parsed = url.rsplit(':').next().unwrap();
    assert_ne!(
        parsed, "4222",
        "Never use the Development broker for compatibility tests"
    );
    let backend = Backend::connect(Settings {
        url,
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(10),
    })
    .await
    .unwrap();
    let subject = ActorSubject::query("MarketDataQuery", "GetValueDate", "valueDate");
    let reply = backend
        .request(
            &subject,
            &Value::Array(vec![subject.wire(), Value::Nil]),
            None,
            None,
        )
        .await
        .unwrap();
    assert_eq!(
        date_only(&service_result(&reply).unwrap().as_array().unwrap()[0])
            .unwrap()
            .to_string(),
        "2026-10-26"
    );
    let session = ifm_ui_egui::services::business_context::BusinessContext::await_ready(
        &backend,
        tokio::time::Instant::now() + Duration::from_secs(3),
    )
    .await
    .unwrap();
    assert_eq!(session.value_date.to_string(), "2026-10-26");
    assert_eq!(session.revision, 42);
    let status = ifm_ui_egui::services::feed_status::FeedStatus::load(&backend)
        .await
        .unwrap();
    assert!(status.running);
    assert!(
        status
            .terminal_error
            .unwrap()
            .contains("two-minute recovery deadline")
    );
    let registry = ifm_ui_egui::backend::schema::Registry::compiled().unwrap();
    let rows = ifm_ui_egui::services::reference::load_lookups(&backend, &registry)
        .await
        .unwrap();
    assert_eq!(rows[0].short_code, "USD");
    let large = codec::decode(include_bytes!(
        "fixtures/backend/compressed-large.raw.msgpack"
    ))
    .unwrap();
    let reply = backend
        .request(
            &ActorSubject::query("NativeCompatibility", "Echo", "none"),
            &large,
            None,
            None,
        )
        .await
        .unwrap();
    assert_eq!(reply, large);
    let reply = backend
        .request(
            &ActorSubject::query("NativeCompatibility", "Error", "none"),
            &Value::Nil,
            None,
            None,
        )
        .await
        .unwrap();
    assert!(
        service_result(&reply)
            .unwrap_err()
            .contains("Server clock unavailable")
    );
    // A subscriber exists but intentionally never replies. The caller's one deadline wins.
    let started = std::time::Instant::now();
    let error = backend
        .request(
            &ActorSubject::query("NativeCompatibility", "Hang", "none"),
            &Value::Nil,
            Some(Duration::from_millis(100)),
            None,
        )
        .await
        .err()
        .unwrap();
    assert!(error.contains("deadline"), "{error}");
    assert!(started.elapsed() < Duration::from_secs(2));
    let subscription = backend
        .subscribe("Event", "NativeCompatibility")
        .await
        .unwrap();
    backend.flush().await.unwrap();
    drop(subscription);
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn catalog_controller_load_edit_save_publish_retire_and_rejection_paths() {
    use ifm_ui_egui::{backend::schema::Registry, state::strategy_catalog::CatalogController};
    use serde_json::json;
    let url = std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();
    assert!(!url.ends_with(":4222"));
    let backend = Backend::connect(Settings {
        url,
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(10),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = CatalogController::default();
    let intent = |action: &str, generation: u64, selected: &str, fields: serde_json::Value| json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"strategy-catalog","intent":action,"expectedGeneration":generation,"originalId":selected,"fields":fields});
    let loaded = state
        .execute(&backend, &registry, &intent("load", 0, "", json!({})))
        .await
        .unwrap();
    assert_eq!(loaded.result.rows.len(), 3);
    assert_eq!(loaded.result.rows[0].fields["name"], "Futures");
    let selected = loaded.result.rows[0].id.clone();
    let publication = state
        .execute(
            &backend,
            &registry,
            &intent("catalog-select", loaded.generation, &selected, json!({})),
        )
        .await
        .unwrap();
    let stale = intent("catalog-edit", 0, &selected, json!({}));
    assert!(
        state
            .execute(&backend, &registry, &stale)
            .await
            .err()
            .unwrap()
            .contains("Catalog changed")
    );
    let publication = state
        .execute(
            &backend,
            &registry,
            &intent("catalog-edit", publication.generation, &selected, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(publication.result.context["editing"], "True");
    assert!(
        state
            .execute(
                &backend,
                &registry,
                &intent("load", publication.generation, "", json!({}))
            )
            .await
            .err()
            .unwrap()
            .contains("Save or discard")
    );
    let mut draft: serde_json::Value =
        serde_json::from_str(&publication.result.context["definition"]).unwrap();
    assert_eq!(draft["Key"]["Version"], 2);
    let mut forged = draft.clone();
    forged["Key"]["Id"] = uuid::Uuid::new_v4().to_string().into();
    assert!(
        state
            .execute(
                &backend,
                &registry,
                &intent(
                    "catalog-save",
                    publication.generation,
                    &selected,
                    json!({"definition":forged.to_string()})
                )
            )
            .await
            .err()
            .unwrap()
            .contains("identity")
    );
    draft["Name"] = "Futures Native Integration".into();
    let saved = state
        .execute(
            &backend,
            &registry,
            &intent(
                "catalog-save",
                publication.generation,
                &selected,
                json!({"definition":draft.to_string()}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(saved.result.context["editing"], "False");
    let selected = saved.result.context["selectedId"].clone();
    assert!(saved.result.rows.iter().any(|row| row.id == selected
        && row.fields["name"] == "Futures Native Integration"
        && row.fields["status"] == "Draft"));
    let published = state
        .execute(
            &backend,
            &registry,
            &intent("catalog-publish", saved.generation, &selected, json!({})),
        )
        .await
        .unwrap();
    assert!(
        published
            .result
            .rows
            .iter()
            .any(|row| row.id == selected && row.fields["status"] == "Published")
    );
    let retired = state
        .execute(
            &backend,
            &registry,
            &intent("catalog-retire", published.generation, &selected, json!({})),
        )
        .await
        .unwrap();
    assert!(
        retired
            .result
            .rows
            .iter()
            .any(|row| row.id == selected && row.fields["status"] == "Retired")
    );
    assert!(
        state
            .execute(
                &backend,
                &registry,
                &intent("catalog-publish", retired.generation, &selected, json!({}))
            )
            .await
            .err()
            .unwrap()
            .contains("lifecycle state")
    );
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn lookup_mutations_correlate_early_events_and_preserve_unknown_outcomes() {
    use ifm_ui_egui::{backend::schema::Registry, state::lookup::LookupController};
    use serde_json::json;
    let url = std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();
    assert!(!url.ends_with(":4222"));
    let backend = Backend::connect(Settings {
        url: url.clone(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(10),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = LookupController::default();
    let intent = |action: &str, generation: u64, row: serde_json::Value| json!({"schema":1,"view":"lookup","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"lookup":row});
    let loaded = state
        .execute(&backend, &registry, &intent("load", 0, json!(null)))
        .await
        .unwrap();
    assert!(loaded.rows.iter().any(|r| r.short_code == "USD"));
    let mut row = json!({"lookupTypeName":"StandaloneFixture","shortCode":"NATIVE","orderId":9911,"description":"Native add","createdOn":"2000-01-01T00:00:00Z","createdBy":"Fixture"});
    let added = state
        .execute(
            &backend,
            &registry,
            &intent("add", loaded.generation, row.clone()),
        )
        .await
        .unwrap();
    assert_eq!(added.outcomes[0].state, "Completed");
    assert!(
        added
            .rows
            .iter()
            .any(|r| r.lookup_type_name == "StandaloneFixture")
    );
    let saved = added.rows.iter().find(|r|r.lookup_type_name=="StandaloneFixture").unwrap();
    assert_eq!(chrono::DateTime::parse_from_rfc3339(&saved.created_on).unwrap().timestamp(), chrono::DateTime::parse_from_rfc3339("2026-10-26T12:30:00Z").unwrap().timestamp());
    row["createdOn"] = saved.created_on.clone().into();
    row["description"] = "Native change".into();
    let changed = state
        .execute(
            &backend,
            &registry,
            &intent("change", added.generation, row.clone()),
        )
        .await
        .unwrap();
    assert_eq!(changed.outcomes[0].state, "Completed");
    assert!(
        changed
            .rows
            .iter()
            .any(|r| r.description == "Native change")
    );
    let removed = state
        .execute(
            &backend,
            &registry,
            &intent("remove", changed.generation, row.clone()),
        )
        .await
        .unwrap();
    assert_eq!(removed.outcomes[0].state, "Completed");
    assert!(
        !removed
            .rows
            .iter()
            .any(|r| r.lookup_type_name == "StandaloneFixture")
    );
    row["shortCode"] = "REJECT".into();
    let rejected = state
        .execute(
            &backend,
            &registry,
            &intent("add", removed.generation, row.clone()),
        )
        .await
        .unwrap();
    assert_eq!(rejected.outcomes[0].state, "Failed");
    assert!(rejected.outcomes[0].message.contains("Fixture rejected"));
    let short = Backend::connect(Settings {
        url,
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_millis(500),
    })
    .await
    .unwrap();
    row["shortCode"] = "NATIVE".into();
    row["description"] = "NoTerminal".into();
    let request = intent("add", rejected.generation, row);
    let started = std::time::Instant::now();
    let unknown = state.execute(&short, &registry, &request).await.unwrap();
    assert_eq!(unknown.outcomes[0].state, "Unknown");
    assert!(started.elapsed() < Duration::from_secs(2));
    let duplicate = state.execute(&backend, &registry, &request).await.unwrap();
    assert_eq!(
        duplicate.outcomes[0].state, "Unknown",
        "An uncertain request must not be automatically resubmitted"
    );
    let refreshed = state
        .execute(
            &backend,
            &registry,
            &intent("load", duplicate.generation, json!(null)),
        )
        .await
        .unwrap();
    assert!(
        refreshed
            .rows
            .iter()
            .any(|r| r.lookup_type_name == "StandaloneFixture"),
        "Authoritative reload must reveal the committed outcome"
    );
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn yield_controller_import_add_change_remove_and_date_validation_use_backend_contracts() {
    use ifm_ui_egui::{backend::schema::Registry, state::yield_curve::YieldController};
    use serde_json::json;
    let url = std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();
    assert!(!url.ends_with(":4222"));
    let backend = Backend::connect(Settings {
        url,
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(10),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = YieldController::default();
    let intent = |action: &str, generation: u64, original: &str, fields: serde_json::Value| json!({"schema":1,"view":"yield-curve","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"originalId":original,"fields":fields});
    let loaded = state
        .execute(&backend, &registry, &intent("load", 0, "", json!({})))
        .await
        .unwrap();
    assert_eq!(loaded.result.context["valueDate"], "2026-10-26");
    assert_eq!(loaded.result.context["rangeStart"], "2026-10-01");
    let imported = state
        .execute(
            &backend,
            &registry,
            &intent(
                "import",
                loaded.generation,
                "",
                json!({"importDate":"2026-10-23"}),
            ),
        )
        .await
        .unwrap();
    assert_eq!(
        imported.outcomes[0].state, "Completed",
        "{}",
        imported.status
    );
    assert_eq!(imported.result.rows[0].fields["thirtyYear"], "2.2");
    let fields = json!({"valueDate":"2026-10-22","oneMonth":"1.1","twoMonth":"1.2","threeMonth":"1.3","sixMonth":"1.4","oneYear":"1.5","twoYear":"1.6","threeYear":"1.7","fiveYear":"1.8","sevenYear":"1.9","tenYear":"2.0","twentyYear":"2.1","thirtyYear":"2.2"});
    let added = state
        .execute(
            &backend,
            &registry,
            &intent("add", imported.generation, "", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(added.outcomes[0].state, "Completed", "{}", added.status);
    let duplicate = state
        .execute(
            &backend,
            &registry,
            &intent("add", added.generation, "", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(duplicate.outcomes[0].state, "Failed");
    assert!(duplicate.status.contains("already exists"));
    let mut changed_fields = fields;
    changed_fields["oneMonth"] = "3.125".into();
    let changed = state
        .execute(
            &backend,
            &registry,
            &intent(
                "change",
                duplicate.generation,
                "2026-10-22",
                changed_fields.clone(),
            ),
        )
        .await
        .unwrap();
    assert_eq!(changed.outcomes[0].state, "Completed", "{}", changed.status);
    assert!(
        changed
            .result
            .rows
            .iter()
            .any(|r| r.id == "2026-10-22" && r.fields["oneMonth"] == "3.125")
    );
    changed_fields["oneMonth"] = "NaN".into();
    let invalid = state
        .execute(
            &backend,
            &registry,
            &intent("change", changed.generation, "2026-10-22", changed_fields),
        )
        .await
        .unwrap();
    assert_eq!(invalid.outcomes[0].state, "Failed");
    let removed = state
        .execute(
            &backend,
            &registry,
            &intent("remove", invalid.generation, "2026-10-22", json!({})),
        )
        .await
        .unwrap();
    assert_eq!(removed.outcomes[0].state, "Completed", "{}", removed.status);
    assert!(!removed.result.rows.iter().any(|r| r.id == "2026-10-22"));
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn calendar_native_crud_import_dates_rejection_and_unknown_outcomes() {
    use ifm_ui_egui::{backend::schema::Registry, state::calendar::CalendarController};
    use serde_json::json;
    let url = std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();
    assert_ne!(url.rsplit(':').next().unwrap(), "4222");
    let backend = Backend::connect(Settings {
        url,
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_millis(400),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = CalendarController::default();
    let request = |action: &str, generation: u64, original: &str, fields: serde_json::Value| json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"economic-calendar","intent":action,"expectedGeneration":generation,"originalId":original,"fields":fields});
    let loaded = state
        .execute(&backend, &registry, &request("load", 0, "", json!({})))
        .await
        .unwrap();
    assert_eq!(loaded.outcomes[0].state, "Completed", "{}", loaded.status);
    assert_eq!(loaded.result.context["filterDate"], "2026-10-26");
    assert_eq!(
        loaded.result.context["newEventUtc"],
        "2026-10-26T12:30:00.0000000Z"
    );
    let fields = json!({"eventDate":"2026-10-26T12:30:00.1234567Z","countryCode":"US","eventName":"Native Calendar","actual":"2.5","forecast":"2.0","impact":"High"});
    let added = state
        .execute(
            &backend,
            &registry,
            &request("add", loaded.generation, "", fields.clone()),
        )
        .await
        .unwrap();
    assert_eq!(added.outcomes[0].state, "Completed", "{}", added.status);
    let row = added
        .result
        .rows
        .iter()
        .find(|r| r.fields["eventName"] == "Native Calendar")
        .unwrap();
    assert!(row.fields["eventDate"].starts_with("2026-10-26T12:30:00.1234567"));
    let selected = row.id.clone();
    let mut changed_fields = fields.clone();
    changed_fields["actual"] = "3.1".into();
    let changed = state
        .execute(
            &backend,
            &registry,
            &request("change", added.generation, &selected, changed_fields),
        )
        .await
        .unwrap();
    assert_eq!(changed.outcomes[0].state, "Completed", "{}", changed.status);
    assert_eq!(
        changed
            .result
            .rows
            .iter()
            .find(|r| r.id == selected)
            .unwrap()
            .fields["actual"],
        "3.1"
    );
    let removed = state
        .execute(
            &backend,
            &registry,
            &request("remove", changed.generation, &selected, json!({})),
        )
        .await
        .unwrap();
    assert_eq!(removed.outcomes[0].state, "Completed", "{}", removed.status);
    assert!(!removed.result.rows.iter().any(|r| r.id == selected));
    let imported = state
        .execute(
            &backend,
            &registry,
            &request("import", removed.generation, "", json!({})),
        )
        .await
        .unwrap();
    assert_eq!(
        imported.outcomes[0].state, "Completed",
        "{}",
        imported.status
    );
    assert!(
        imported
            .result
            .rows
            .iter()
            .any(|r| r.fields["eventName"] == "Offline Import")
    );
    let mut rejected_fields = fields.clone();
    rejected_fields["eventName"] = "Reject".into();
    let rejected = state
        .execute(
            &backend,
            &registry,
            &request("add", imported.generation, "", rejected_fields),
        )
        .await
        .unwrap();
    assert_eq!(rejected.outcomes[0].state, "Failed");
    assert!(rejected.status.contains("Fixture calendar rejection"));
    let mut malformed_fields = fields.clone();
    malformed_fields["eventName"] = "MalformedAck".into();
    let malformed = state
        .execute(
            &backend,
            &registry,
            &request("add", rejected.generation, "", malformed_fields),
        )
        .await
        .unwrap();
    assert_eq!(
        malformed.outcomes[0].state, "Unknown",
        "{}",
        malformed.status
    );
    assert!(malformed.status.contains("acknowledgement"));
    let mut uncertain_fields = fields;
    uncertain_fields["eventName"] = "NoTerminal".into();
    let uncertain_request = request("add", malformed.generation, "", uncertain_fields);
    let unknown = state
        .execute(&backend, &registry, &uncertain_request)
        .await
        .unwrap();
    assert_eq!(unknown.outcomes[0].state, "Unknown", "{}", unknown.status);
    let duplicate = state
        .execute(&backend, &registry, &uncertain_request)
        .await
        .unwrap();
    assert_eq!(duplicate.outcomes[0].state, "Unknown");
    let reloaded = state
        .execute(
            &backend,
            &registry,
            &request("load", duplicate.generation, "", json!({})),
        )
        .await
        .unwrap();
    assert!(
        reloaded
            .result
            .rows
            .iter()
            .any(|r| r.fields["eventName"] == "NoTerminal")
    );
    assert!(
        state
            .execute(
                &backend,
                &registry,
                &request("remove", 0, "forged", json!({}))
            )
            .await
            .is_err()
    );
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn actor_health_http_and_supervisor_commands_preserve_permissions_generation_and_unknown_outcome()
 {
    use ifm_ui_egui::{
        backend::schema::Registry,
        state::actor_health::{ActorHealthController, ControlOptions, OPERATIONS},
    };
    use serde_json::json;
    use std::{
        io::{Read, Write},
        net::TcpListener,
        sync::{
            Arc,
            atomic::{AtomicBool, Ordering},
        },
    };
    let url = std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();
    assert!(!url.ends_with(":4222"));
    let backend = Backend::connect(Settings {
        url,
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(5),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let listener = TcpListener::bind("127.0.0.1:0").unwrap();
    listener.set_nonblocking(true).unwrap();
    let endpoint = format!("http://{}/api/actor-health", listener.local_addr().unwrap());
    let stopping = Arc::new(AtomicBool::new(false));
    let signal = stopping.clone();
    let worker = std::thread::spawn(move || {
        let body = include_str!("fixtures/backend/actor-health-http.json");
        while !signal.load(Ordering::Acquire) {
            match listener.accept() {
                Ok((mut stream, _)) => {
                    // Windows accepted sockets can inherit the listener's mode.
                    // The fixture reads complete requests on this dedicated thread.
                    stream.set_nonblocking(false).unwrap();
                    stream
                        .set_read_timeout(Some(Duration::from_secs(2)))
                        .unwrap();
                    let mut bytes = [0; 8192];
                    let length = stream.read(&mut bytes).unwrap();
                    let request = String::from_utf8_lossy(&bytes[..length]);
                    assert!(!request.contains("toUtc="),"API must own the current health-window end: {request}");
                    if request.contains("fromUtc="){assert!(request.contains("2026-10-26T"),"API observation must own the initial From time: {request}");}
                    let response = format!(
                        "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}",
                        body.len()
                    );
                    stream.write_all(response.as_bytes()).unwrap();
                }
                Err(error) if error.kind() == std::io::ErrorKind::WouldBlock => {
                    std::thread::sleep(Duration::from_millis(5))
                }
                Err(error) => panic!("HTTP fixture: {error}"),
            }
        }
    });
    // Always stop the fixture even if a parity assertion fails.
    struct Stop(Arc<AtomicBool>, Option<std::thread::JoinHandle<()>>);
    impl Drop for Stop {
        fn drop(&mut self) {
            self.0.store(true, Ordering::Release);
            if let Some(worker) = self.1.take() {
                // Do not panic again while unwinding a failed assertion.
                let outcome = worker.join();
                if !std::thread::panicking() {
                    outcome.unwrap();
                }
            }
        }
    }
    let _stop = Stop(stopping, Some(worker));
    let request = |action: &str, generation: u64, operation: &str, reason: &str| json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"actor-health","intent":action,"expectedGeneration":generation,"originalId":"mailbox:Realtime:IronCondorPosition:101.701.1901.1501","fields":{"operation":operation,"reason":reason}});
    let options = ControlOptions {
        enabled: true,
        requester: "Fixture operator".into(),
        timeout: Duration::from_millis(500),
    };
    let mut state = ActorHealthController::new(Some(endpoint.clone()), options).unwrap();
    let mut current = state
        .execute(&backend, &registry, &request("load", 0, "", ""))
        .await
        .unwrap();
    assert_eq!(current.outcomes[0].state, "Completed", "{}", current.status);
    assert_eq!(current.result.context["controls"], "true");
    for operation in OPERATIONS {
        current = state
            .execute(
                &backend,
                &registry,
                &request(
                    "supervise",
                    current.generation,
                    operation,
                    "Offline native parity",
                ),
            )
            .await
            .unwrap();
        assert_eq!(
            current.outcomes[0].state, "Completed",
            "{operation}: {}",
            current.status
        );
        assert!(current.result.context["outcome"].contains("accepted; refresh health"));
    }
    let stale = state
        .execute(
            &backend,
            &registry,
            &request("supervise", 0, "Restart", "Offline"),
        )
        .await
        .unwrap();
    assert_eq!(stale.outcomes[0].state, "Failed");
    assert!(stale.status.contains("generation changed"));
    let rejected = state
        .execute(
            &backend,
            &registry,
            &request("supervise", stale.generation, "Restart", "Reject"),
        )
        .await
        .unwrap();
    assert_eq!(rejected.outcomes[0].state, "Failed");
    assert!(rejected.status.contains("fixture rejected"));
    let uncertain = request("supervise", rejected.generation, "Restart", "NoAck");
    let unknown = state
        .execute(&backend, &registry, &uncertain)
        .await
        .unwrap();
    assert_eq!(unknown.outcomes[0].state, "Unknown");
    let repeated = state
        .execute(&backend, &registry, &uncertain)
        .await
        .unwrap();
    assert_eq!(repeated.outcomes[0].state, "Unknown");
    let mut observer =
        ActorHealthController::new(Some(endpoint), ControlOptions::default()).unwrap();
    let observed = observer
        .execute(&backend, &registry, &request("load", 0, "", ""))
        .await
        .unwrap();
    assert_eq!(observed.result.context["controls"], "false");
    let denied = observer
        .execute(
            &backend,
            &registry,
            &request("supervise", observed.generation, "Restart", "Offline"),
        )
        .await
        .unwrap();
    assert_eq!(denied.outcomes[0].state, "Failed");
    assert!(denied.status.contains("unavailable"));
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn feed_controls_use_api_date_terminal_events_and_authoritative_reconciliation() {
    use ifm_ui_egui::{
        backend::schema::Registry,
        services::feed_control::{FeedObservation, set_running},
    };
    let url = std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();
    assert!(!url.ends_with(":4222"));
    let backend = Backend::connect(Settings {
        url,
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_millis(900),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let configure = |scenario: &str| {
        let backend = backend.clone();
        let scenario = scenario.to_string();
        async move {
            backend
                .request(
                    &ActorSubject::query("NativeCompatibility", "FeedScenario", "none"),
                    &Value::from(scenario),
                    None,
                    None,
                )
                .await
                .unwrap();
        }
    };
    let initial = FeedObservation::load(&backend).await.unwrap();
    assert!(initial.status.running);
    assert!(initial.control(false).can_toggle);
    assert!(!initial.control(true).can_toggle);
    let stopped = set_running(&backend, &registry, false, uuid::Uuid::new_v4())
        .await
        .unwrap();
    assert!(!stopped.status.running);
    let started = set_running(&backend, &registry, true, uuid::Uuid::new_v4())
        .await
        .unwrap();
    assert!(started.status.running);
    assert_eq!(
        started.status.active_date,
        Some("2026-10-26".parse().unwrap())
    );
    configure("LostTerminal").await;
    let reconciled = set_running(&backend, &registry, false, uuid::Uuid::new_v4())
        .await
        .unwrap();
    assert!(!reconciled.status.running);
    configure("Normal").await;
    set_running(&backend, &registry, true, uuid::Uuid::new_v4())
        .await
        .unwrap();
    configure("Reject").await;
    let rejected = set_running(&backend, &registry, false, uuid::Uuid::new_v4())
        .await
        .err()
        .unwrap();
    assert!(rejected.contains("Fixture rejected"), "{rejected}");
    configure("Freeze").await;
    let began = std::time::Instant::now();
    let failed = set_running(&backend, &registry, false, uuid::Uuid::new_v4())
        .await
        .err()
        .unwrap();
    assert!(failed.contains("OutcomeUnknown"), "{failed}");
    assert!(began.elapsed() < Duration::from_secs(2));
    configure("Normal").await;
    assert!(
        FeedObservation::load(&backend)
            .await
            .unwrap()
            .status
            .running
    );
}

#[tokio::test]
#[ignore = "Requires an isolated NATS broker and the C# compatibility responder"]
async fn sidebar_calendar_queries_backend_periods_and_keeps_other_publication_owners() {
    use ifm_ui_egui::{
        backend::schema::Registry,
        sidebar::{SidebarPublication, StatusRow},
        state::sidebar_calendar::SidebarCalendar,
    };
    let url = std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap();
    assert!(!url.ends_with(":4222"));
    let backend = Backend::connect(Settings {
        url,
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(3),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = SidebarCalendar::default()
        .refresh(&backend, &registry)
        .await
        .unwrap();
    let mut publication = SidebarPublication::default();
    publication.error = "Existing error".into();
    publication.error_sequence = 9;
    publication.feed_control.active = true;
    publication.status_rows.push(StatusRow {
        message: "Existing log".into(),
        ..Default::default()
    });
    state.publish(&mut publication);
    assert_eq!(publication.country, "US");
    assert_eq!(publication.date, "Monday, October 26, 2026");
    assert!(state.action(0x90000001).unwrap());
    state = state.refresh(&backend, &registry).await.unwrap();
    state.publish(&mut publication);
    assert_eq!(publication.country, "CA");
    assert!(
        publication
            .calendar_rows
            .iter()
            .any(|r| r.event == "Sidebar Fixture")
    );
    assert!(!publication.selected_id.is_empty());
    assert_eq!(
        publication
            .calendar_rows
            .iter()
            .find(|row| row.event == "Sidebar Fixture")
            .unwrap()
            .time,
        "8:30 AM"
    );
    assert!(!state.action(0xa0000000).unwrap());
    state.publish(&mut publication);
    assert_eq!(publication.date, "Monday, October 26, 2026");
    for index in 0..5 {
        assert!(state.action(0x80000000 + index).unwrap());
        state = state.refresh(&backend, &registry).await.unwrap();
        state.publish(&mut publication);
        assert!(!publication.date.is_empty());
    }
    assert_eq!(publication.period, "Next Week");
    assert_eq!(publication.error, "Existing error");
    assert_eq!(publication.error_sequence, 9);
    assert!(publication.feed_control.active);
    assert_eq!(publication.status_rows[0].message, "Existing log");
    assert!(state.action(0x90000fff).is_err());
    assert!(state.action(0x8000000f).is_err());
}

#[path="support/broker_order_commands.rs"]
mod broker_order_commands;

#[path="support/order_preparation.rs"]
mod order_preparation;

#[path="support/order_submission.rs"]
mod order_submission;

#[path="support/monitor_api.rs"]
mod monitor_api;

#[path="support/monitor_controller.rs"]
mod monitor_controller;

#[path="support/order_close.rs"]
mod order_close;

#[path="support/trade_eod.rs"]
mod trade_eod;

#[path="support/closing_workspace.rs"]
mod closing_workspace;


#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn operational_health_closed_market_uses_independent_api_clock(){
 use std::{io::{Read,Write},net::TcpListener};
 let listener=TcpListener::bind("127.0.0.1:0").unwrap();let endpoint=format!("http://{}/health",listener.local_addr().unwrap());
 let worker=std::thread::spawn(move||{let(mut stream,_)=listener.accept().unwrap();let mut buffer=[0u8;4096];stream.read(&mut buffer).unwrap();
 let body=r#"{"schemaVersion":1,"revision":1,"observedOnUtc":"2000-01-03T12:30:00Z","overallStatus":"Inactive","sessionState":"Closed","valueDate":null,"stages":[{"stage":"Actor routing/API","status":"Green","required":true,"reasonCode":"Healthy","reason":"API remains available"}],"datasets":[]}"#;
 write!(stream,"HTTP/1.1 200 OK\r\nX-IFM-Server-Utc: 2000-01-03T12:30:00Z\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}",body.len()).unwrap();});
 let backend=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();
 // The compatibility MarketSession is in 2026, intentionally unrelated to this
 // health API clock. A market-session dependency would reject this valid read.
 let mut health=ifm_ui_egui::state::operations_health::OperationsHealthController::new(Some(endpoint)).unwrap();
 let publication=health.execute(&backend,&serde_json::json!({"schema":1,"view":"operations-health","intent":"load","requestId":uuid::Uuid::new_v4().to_string()})).await.unwrap();
 assert_eq!(publication.outcomes[0].state,"Completed");assert_eq!(publication.result.context["status"],"Inactive");assert_eq!(publication.result.rows[0].fields["Status"],"Green");worker.join().unwrap();
}
