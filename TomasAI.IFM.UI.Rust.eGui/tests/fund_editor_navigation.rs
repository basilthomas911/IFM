//! Tests the same native navigation and shared error ownership used by the running app.
use eframe::egui;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::{
    contract_editor::{ContractPublication, ContractResult},
    lookup_editor::EditorOutcome,
    portfolio::PortfolioManager,
    ui::errors::ErrorDialogs,
};
use serde_json::{Value, json};
use std::collections::BTreeMap;

/// Read-only publications, pending UI requests and the application's shared error queue.
struct State {
    /// Portfolio Administration under test.
    manager: PortfolioManager,
    /// Current correlated controller publication.
    publication: ContractPublication,
    /// Published document, edited only to emulate controller responses.
    document: Value,
    /// Financials panel returned when the Funds view requests it.
    financials: Value,
    /// Ordered UI intents captured from the real renderer.
    requests: Vec<Value>,
    /// Single shared owner of application error dialogs.
    errors: ErrorDialogs,
    /// Injects a rejected editor-open receipt without creating another dialog owner.
    reject: bool,
}

/// Navigating through an embedded Fund panel must open Base Contracts, or show exactly one error on rejection.
#[test]
fn funds_to_fund_details_to_base_contracts_uses_one_editor_and_one_error_owner() {
    for reject in [false, true] {
        let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");
        let mut document: Value = ifm_ui_egui::wire::decode(
            &std::fs::read(root.join("portfolio-session-fund-financials.msgpack")).unwrap(),
        )
        .unwrap();
        let financials = document["fundView"].take();
        document["dialogs"] = json!([]);
        document["enabled"]["Base Contracts..."] = true.into();
        let publication = ContractPublication {
            generation: 1,
            result: ContractResult {
                view: "portfolio".into(),
                documents: BTreeMap::from([(
                    "portfolio".into(),
                    ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
                )]),
                ..Default::default()
            },
            ..Default::default()
        };
        let mut manager = PortfolioManager::default();
        manager.open();
        let mut h=Harness::builder().with_max_steps(20).with_size(egui::vec2(1800.,1200.)).build_state(|ctx,state:&mut State| {
            // Matches main.rs: incoming outcomes go to the shared queue before the Portfolio renderer.
            state.errors.outcomes("portfolio",&state.publication.outcomes);
            if let Some(payload)=state.manager.draw(ctx,&state.publication,true) {
                let request:Value=serde_json::from_str(&payload).unwrap();
                let failed=state.reject && request["intent"]=="portfolio-open";
                if request["intent"]=="portfolio-fund-view" {
                    state.document["fundView"]=state.financials.clone();
                }
                if request["intent"]=="portfolio-open" && !failed {
                    state.document["dialogs"]=json!([{"key":"base-contracts","title":"Fund Base Contracts","width":780,"height":560,
                        "fields":[{"key":"saved","value":"None saved"},{"key":"BaseAsset","value":"Futures"}],"buttons":["Save","Cancel"]}]);
                }
                state.publication.generation+=1;
                state.publication.result.documents.insert("portfolio".into(),ifm_ui_egui::wire::encode_value(&state.document).unwrap().into());
                state.publication.outcomes=vec![EditorOutcome {request_id:request["requestId"].as_str().unwrap().into(),
                    state:if failed {"Failed"} else {"Completed"}.into(),
                    message:if failed {"Close the current editor before opening another"} else {""}.into()}];
                state.requests.push(request);
                ctx.request_repaint();
            }
            if let Some(message)=state.errors.message().map(str::to_string) {
                egui::Modal::new(egui::Id::new("standalone-runtime-error")).show(ctx,|ui| {
                    if ifm_ui_egui::status_dialog::body(ui,&message,ifm_ui_egui::status_dialog::Severity::Error).acknowledged {
                        state.errors.acknowledge();
                    }
                });
            }
        },State {manager,publication,document,financials,requests:vec![],errors:ErrorDialogs::default(),reject});
        h.run();
        h.get_by_label("Funds").click();
        h.run();
        assert!(
            h.state().requests.iter().any(
                |r| r["intent"] == "portfolio-fund-view" && r["fields"]["view"] == "financials"
            )
        );
        h.get_by_label("Fund Details").click();
        h.run();
        h.get_by_label("Fund Detail Actions").click();
        h.run();
        h.get_by_label("Base Contracts...").click();
        h.run();
        assert_eq!(
            h.state().requests.last().unwrap()["intent"],
            "portfolio-open"
        );
        assert_eq!(
            h.state().requests.last().unwrap()["fields"]["view"],
            "base-contracts"
        );
        assert!(h.query_by_label("Portfolio Error").is_none());
        if reject {
            assert_eq!(
                h.state().errors.message(),
                Some("Close the current editor before opening another")
            );
            h.get_by_label("OK").click();
            h.run();
            h.run_steps(5);
            assert!(
                h.state().errors.message().is_none(),
                "Replayed failed outcomes must not queue another error"
            );
            assert!(h.query_by_label("OK").is_none());
            assert!(h.query_by_label("Portfolio Error").is_none());
        } else {
            assert!(h.query_by_label("Fund Base Contracts").is_some());
            assert!(h.query_by_label("Symbol filter:").is_some());
            assert!(h.state().errors.message().is_none());
        }
    }
}
