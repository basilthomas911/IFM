use eframe::egui;
use egui_kittest::kittest::NodeT;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::{contract_editor::ContractPublication, trade_workspace::TradeWorkspace};


/// Collect the painted baseline of each text run inside the requested field.
fn text_baselines(shapes: &[egui::epaint::ClippedShape], bounds: egui::Rect) -> Vec<(String, f32)> {
    /// Provides visit for the trade blotter implementation.
    fn visit(shape: &egui::Shape, bounds: egui::Rect, values: &mut Vec<(String, f32)>) {
        match shape {
            egui::Shape::Vec(shapes) => {
                for shape in shapes {
                    visit(shape, bounds, values);
                }
            }
            egui::Shape::Text(text) if bounds.expand(0.5).contains(text.pos) => {
                if let Some(row) = text.galley.rows.first() {
                    if let Some(glyph) = row.glyphs.first() {
                        values.push((
                            text.galley.text().into(),
                            text.pos.y + row.pos.y + glyph.pos.y,
                        ));
                    }
                }
            }
            _ => {}
        }
    }
    let mut values = Vec::new();
    for shape in shapes {
        visit(&shape.shape, bounds, &mut values);
    }
    values
}


/// Captions retain a twenty-pixel gap and share the painted baseline of each field.
#[test]
fn editor_caption_gaps_and_font_baselines_follow_display_scale() {
    for dpi in [1., 1.5, 2.] {
        let mut h = harness();
        h.state_mut()
            .snapshot
            .result
            .context
            .insert("effectiveDate".into(), "2026-10-14".into());
        for key in ["portfolioIndex", "fundIndex"] {
            h.state_mut()
                .snapshot
                .result
                .context
                .insert(key.into(), "0".into());
        }
        h.input_mut()
            .viewports
            .get_mut(&egui::ViewportId::ROOT)
            .unwrap()
            .native_pixels_per_point = Some(dpi);
        h.run();
        for (caption_id, control_id) in [
            ("portfolio", "ddlPortfolio"),
            ("fund", "ddlFund"),
            ("source", "ddlCompositionSource"),
            ("Trade Id:", "Trade Id:"),
            ("Effective Date:", "Effective Date:"),
            ("Trade Date:", "dtpTradeDate"),
            ("Order Action:", "parentOrderAction"),
            ("expiry", "expiry"),
            ("callDelta", "callDelta"),
            ("liquiditySelector", "liquiditySelector"),
        ] {
            let controls = &h.state().view.controls;
            let caption = controls[&format!("caption:{caption_id}")];
            let control = controls[control_id];
            assert!(
                ((control.left() - caption.right()) * dpi - 20.).abs() < 1.,
                "gap {caption_id} at {dpi}"
            );
            let labels = text_baselines(&h.output().shapes, caption);
            let values = text_baselines(&h.output().shapes, control);
            assert!(!labels.is_empty(), "missing caption {caption_id}");
            assert!(!values.is_empty(), "missing field {control_id}");
            assert!(
                ((labels[0].1 - values[0].1) * dpi).abs() <= 1.,
                "baseline {caption_id} at {dpi}: {labels:?} {values:?}"
            );
        }
        let reference =
            text_baselines(&h.output().shapes, h.state().view.controls["Trade Id:"])[0].1;
        assert!(h.state().view.controls["dtpTradeDate"].top() > h.state().view.controls["Effective Date:"].bottom());
        for id in ["Effective Date:", "parentOrderAction"] {
            let baseline = text_baselines(&h.output().shapes, h.state().view.controls[id])[0].1;
            assert!(
                ((reference - baseline) * dpi).abs() <= 1.,
                "header row baseline {id} at {dpi}"
            );
        }
        let live_baseline = text_baselines(&h.output().shapes, h.ctx.screen_rect())
            .into_iter()
            .find(|(text, _)| text == "Live Feed")
            .unwrap()
            .1;
        assert!(
            ((reference - live_baseline) * dpi).abs() <= 1.,
            "live-feed baseline at {dpi}"
        );
        if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
            h.render()
                .unwrap()
                .save(std::path::Path::new(&directory).join(format!("market-{dpi}.png")))
                .unwrap();
        }
        // Accessibility bounds are physical pixels; send logical pointer coordinates at non-unit DPI.
        let tab = h.state().view.controls["tab:Broker Trade"].center();
        h.input_mut().events.push(egui::Event::PointerMoved(tab));
        h.input_mut().events.push(egui::Event::PointerButton {
            pos: tab,
            button: egui::PointerButton::Primary,
            pressed: true,
            modifiers: egui::Modifiers::NONE,
        });
        h.run();
        h.input_mut().events.push(egui::Event::PointerButton {
            pos: tab,
            button: egui::PointerButton::Primary,
            pressed: false,
            modifiers: egui::Modifiers::NONE,
        });
        h.run();
        if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
            h.render()
                .unwrap()
                .save(std::path::Path::new(&directory).join(format!("broker-{dpi}.png")))
                .unwrap();
        }
        for caption in [
            "Contracts",
            "Order type",
            "Time in force",
            "Action",
            "Directed venue",
            "OrderPrice",
        ] {
            let id = format!("broker-field-{caption}");
            let control = *h.state().view.controls.get(&id).unwrap_or_else(|| {
                panic!(
                    "missing {id} at {dpi}: {:?}",
                    h.state().view.controls.keys()
                )
            });
            let label = h.state().view.controls[&format!("caption:{id}")];
            assert!(((control.left() - label.right()) * dpi - 20.).abs() < 1.);
            let labels = text_baselines(&h.output().shapes, label);
            let values = text_baselines(&h.output().shapes, control);
            assert!(!values.is_empty(), "missing broker field {caption}");
            assert!(
                ((labels[0].1 - values[0].1) * dpi).abs() <= 1.,
                "broker baseline {caption} at {dpi}: {labels:?} {values:?}"
            );
        }
    }
}


/// A terminal reason remains visible on the Blotter without its own modal.
#[test]
fn terminal_submission_rejection_stays_inline_across_refreshes() {
    let mut h = harness();
    let reason = "Broker rejected the accepted order: EM.EXPIRED: Approval expired.";
    h.state_mut().snapshot.result.context.insert("submissionRejection".into(), reason.into());
    let inline = format!("Rejected · {reason}");
    for _ in 0..3 {
        h.run();
        assert!(h.query_by_label(&inline).is_some());
        assert!(h.query_by_label("Order Submission Error").is_none());
    }
}

/// Broker captions wrap with their controls instead of overlapping neighboring fields.
#[test]
fn broker_field_groups_fit_a_narrow_editor() {
    let mut h = harness();
    h.set_size(egui::vec2(1200., 800.));
    h.run();
    h.get_by_label("Broker Trade").click();
    h.run();
    let controls = &h.state().view.controls;
    let client = controls["blotter-client"];
    for (id, field) in controls
        .iter()
        .filter(|(id, _)| id.starts_with("broker-field-"))
    {
        let caption = controls[&format!("caption:{id}")];
        assert!(caption.left() >= client.left(), "left overflow {id}");
        assert!(field.right() <= client.right(), "right overflow {id}");
        assert!((field.left() - caption.right() - 20.).abs() < 1.);
    }
    assert!(
        controls["broker-field-Call spread"].top() > controls["broker-field-Available funds"].top(),
        "long rows should wrap"
    );
}


/// Broker fields share compact widths and column starts while retaining the twenty-pixel caption gap.
#[test]
fn broker_fields_use_uniform_responsive_columns() {
    for size in [
        egui::vec2(1200., 800.),
        egui::vec2(1500., 1000.),
        egui::vec2(2100., 1300.),
    ] {
        for dpi in [1., 1.5, 2.] {
            let mut h = harness();
            h.set_size(size);
            h.input_mut()
                .viewports
                .get_mut(&egui::ViewportId::ROOT)
                .unwrap()
                .native_pixels_per_point = Some(dpi);
            h.run();
            let point = h.state().view.controls["tab:Broker Trade"].center();
            for pressed in [true, false] {
                h.input_mut().events.extend([
                    egui::Event::PointerMoved(point),
                    egui::Event::PointerButton {
                        pos: point,
                        button: egui::PointerButton::Primary,
                        pressed,
                        modifiers: egui::Modifiers::NONE,
                    },
                ]);
                h.run();
            }
            let controls = &h.state().view.controls;
            let viewport = controls["broker-fields-viewport"];
            let fields: Vec<_> = controls
                .iter()
                .filter(|(id, _)| id.starts_with("broker-field-"))
                .collect();
            assert_eq!(fields.len(), 38);
            let width = fields[0].1.width();
            assert!(width <= 180. + 1. / dpi, "controls should remain compact");
            let mut columns: Vec<f32> = Vec::new();
            for (id, field) in &fields {
                let caption = controls[&format!("caption:{id}")];
                assert!(
                    ((field.width() - width) * dpi).abs() <= 1.,
                    "width {id} at {size:?}, {dpi}"
                );
                assert!(
                    ((field.left() - caption.right()) * dpi - 20.).abs() <= 1.,
                    "caption gap {id}"
                );
                assert!(
                    caption.left() >= viewport.left() - 1. / dpi
                        && field.right() <= viewport.right() + 1. / dpi,
                    "horizontal overflow {id} at {size:?}, {dpi}"
                );
                if !columns
                    .iter()
                    .any(|left| ((field.left() - left) * dpi).abs() <= 1.)
                {
                    columns.push(field.left());
                }
            }
            assert!(
                (2..=5).contains(&columns.len()),
                "column count at {size:?}, {dpi}: {columns:?}"
            );
            for (_, field) in &fields {
                for (other_id, other) in &fields {
                    if ((field.top() - other.top()) * dpi).abs() <= 1.
                        && field.left() < other.left()
                    {
                        let caption = controls[&format!("caption:{other_id}")];
                        assert!(
                            caption.left() > field.right(),
                            "adjacent field groups overlap"
                        );
                    }
                }
            }
            if dpi == 1. && size.x != 1500. {
                if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
                    h.render()
                        .unwrap()
                        .save(
                            std::path::Path::new(&directory)
                                .join(format!("aligned-broker-{}.png", size.x)),
                        )
                        .unwrap();
                }
            }
            if size.x == 1200. && dpi == 1. {
                h.input_mut().events.extend([
                    egui::Event::PointerMoved(viewport.center()),
                    egui::Event::MouseWheel {
                        unit: egui::MouseWheelUnit::Point,
                        delta: egui::vec2(0., -2000.),
                        modifiers: egui::Modifiers::NONE,
                    },
                ]);
                // Let the smooth-scroll animation settle before checking the final visible row.
                h.run_steps(30);
                let last = h.state().view.controls["broker-field-Risk approval"];
                let visible = h.state().view.controls["broker-fields-viewport"];
                assert!(
                    last.top() >= visible.top() && last.bottom() <= visible.bottom() + 1.,
                    "last fields must remain reachable by scrolling"
                );
                assert!(
                    visible.bottom() < h.state().view.controls["brokerPreviewPlaceOrder"].top(),
                    "field scrolling must leave the action buttons accessible"
                );
            }
        }
    }
}


/// Draft windows fit their content once, keep their actions centered, and left-align the order identity values.
#[test]
fn order_draft_is_compact_with_left_aligned_values_and_centered_actions() {
    for order in [true, false] {
        for dpi in [1., 1.5, 2.] {
            let mut h = harness();
            h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({"id":"order-draft:42","fields":{"kind":"order-draft","data":serde_json::json!({"orderId":42,"orderDate":"2026-10-31","orderStatus":"Open"}).to_string()}})).unwrap());
            h.state_mut()
                .snapshot
                .result
                .context
                .insert("preparedTradeId".into(), "84".into());
            h.state_mut().snapshot.invalidate_render_cache();
            h.input_mut()
                .viewports
                .get_mut(&egui::ViewportId::ROOT)
                .unwrap()
                .native_pixels_per_point = Some(dpi);
            h.run();
            let point = h.get_by_label("Trade Orders").rect().center() / dpi;
            for pressed in [true, false] {
                h.input_mut().events.extend([
                    egui::Event::PointerMoved(point),
                    egui::Event::PointerButton {
                        pos: point,
                        button: egui::PointerButton::Primary,
                        pressed,
                        modifiers: egui::Modifiers::NONE,
                    },
                ]);
                h.run();
            }
            let point = h.state().view.controls[if order {
                "btnCreateOrder"
            } else {
                "btnAddTrade"
            }]
            .center();
            for pressed in [true, false] {
                h.input_mut().events.extend([
                    egui::Event::PointerMoved(point),
                    egui::Event::PointerButton {
                        pos: point,
                        button: egui::PointerButton::Primary,
                        pressed,
                        modifiers: egui::Modifiers::NONE,
                    },
                ]);
                h.run();
            }
            let controls = &h.state().view.controls;
            let dialog = controls["draft-dialog"];
            let client = controls["draft-client"];
            let save = controls["draft-save"];
            let cancel = controls["draft-cancel"];
            assert!(
                ((save.center().x + cancel.center().x) * 0.5 - client.center().x).abs() * dpi <= 1.,
                "footer center at {dpi}"
            );
            assert!((save.width() - cancel.width()).abs() * dpi <= 1.);
            assert!(save.bottom() <= client.bottom() && cancel.bottom() <= client.bottom());
            assert!(
                dialog.height() <= client.height() + 60.,
                "content must not reserve a second block below its controls"
            );
            if order {
                assert!(
                    client.height() * dpi < 340.,
                    "compact content height at {dpi}: {client:?}"
                );
                let reference = controls["draft-reference"];
                assert!(
                    (12. ..=24.).contains(&((save.top() - reference.bottom()) * dpi)),
                    "reference/footer gap at {dpi}"
                );
                for (i, value) in ["42", "2026-10-31", "Open"].iter().enumerate() {
                    let field = controls[&format!("draft-field-{i}")];
                    let label = controls[&format!("caption:draft-field-{i}")];
                    assert!(((field.left() - label.right()) * dpi - 20.).abs() <= 1.);
                    let painted = h
                        .output()
                        .shapes
                        .iter()
                        .find_map(|shape| match &shape.shape {
                            egui::Shape::Text(text)
                                if text.galley.text() == *value && field.contains(text.pos) =>
                            {
                                Some(text.pos.x + text.galley.rect.left())
                            }
                            _ => None,
                        })
                        .unwrap_or_else(|| panic!("missing value {value} at {dpi}"));
                    assert!(
                        ((painted - field.left()) * dpi - 4.).abs() <= 1.,
                        "left-aligned value {value} at {dpi}"
                    );
                }
                if dpi == 1. {
                    if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
                        h.render()
                            .unwrap()
                            .save(std::path::Path::new(&directory).join("add-fund-order.png"))
                            .unwrap();
                    }
                }
            } else {
                for (index, value) in [(0, "84"), (5, "NewTrade"), (6, "Sell")] {
                    let field = controls[&format!("draft-field-{index}")];
                    let painted = h.output().shapes.iter().find_map(|shape| match &shape.shape {
                        egui::Shape::Text(text) if text.galley.text() == value && field.contains(text.pos) =>
                            Some(text.pos.x + text.galley.rect.left()),
                        _ => None,
                    }).unwrap_or_else(|| panic!("missing value {value} at {dpi}"));
                    assert!(((painted - field.left()) * dpi - 4.).abs() <= 1.,
                        "left-aligned trade value {value} at {dpi}");
                }
            }
            let point = cancel.center();
            for pressed in [true, false] {
                h.input_mut().events.extend([
                    egui::Event::PointerMoved(point),
                    egui::Event::PointerButton {
                        pos: point,
                        button: egui::PointerButton::Primary,
                        pressed,
                        modifiers: egui::Modifiers::NONE,
                    },
                ]);
                h.run();
            }
            assert!(
                h.state()
                    .requests
                    .iter()
                    .any(|request| request["intent"] == "cancel-draft")
            );
            assert!(
                !h.state()
                    .requests
                    .iter()
                    .any(|request| request["intent"] == "create-order"
                        || request["intent"] == "create-trade")
            );
        }
    }
}


/// Date fields and list headers share their caption baseline in the order editor.
#[test]
fn order_dates_and_lists_align_with_their_captions() {
    let mut h = harness();
    h.get_by_label("Trade Orders").click();
    h.run();
    for (caption_id, control_id, header) in [
        ("dtpFrom", "dtpFrom", ""),
        ("dtpTo", "dtpTo", ""),
        ("order", "lstTradeOrders", "Order Id"),
        ("trade", "lstTrades", "Trade Id"),
    ] {
        let control = h.state().view.controls[control_id];
        let label = h.state().view.controls[&format!("caption:{caption_id}")];
        assert!((control.left() - label.right() - 20.).abs() < 1.);
        let caption = text_baselines(&h.output().shapes, label)[0].1;
        let texts = text_baselines(&h.output().shapes, control);
        let value = if header.is_empty() {
            texts[0].1
        } else {
            texts.iter().find(|(text, _)| text == header).unwrap().1
        };
        assert!(
            (caption - value).abs() <= 1.,
            "baseline {caption_id}: {caption} {value}"
        );
    }
}


/// Grid heights fill the tab in a 2:1 ratio, with their captions and action groups anchored to each grid.
#[test]
fn order_editor_grid_split_and_actions_follow_available_height() {
    for size in [egui::vec2(1500., 1000.), egui::vec2(1200., 800.)] {
        let mut h = harness();
        h.set_size(size);
        h.run();
        h.get_by_label("Trade Orders").click();
        h.run();
        h.state_mut().snapshot.status = "Editor status bar must be removed".into();
        h.run();
        assert!(
            h.query_by_label("Editor status bar must be removed")
                .is_none()
        );
        let controls = &h.state().view.controls;
        let orders = controls["lstTradeOrders"];
        let trades = controls["lstTrades"];
        assert!((orders.height() - 2. * trades.height()).abs() < 1.);
        assert!((trades.top() - orders.bottom() - 20.).abs() < 1.);
        for (grid, ids) in [
            (orders, &["btnCreateOrder", "btnDeleteOrder"][..]),
            (
                trades,
                &["btnLoadOrder", "btnAddTrade", "btnChangeTrade", "btnRemoveTrade"][..],
            ),
        ] {
            assert!((controls[ids[0]].top() - grid.top()).abs() < 1.);
            for id in ids {
                assert!(
                    controls[*id].bottom() <= grid.bottom(),
                    "{id} below its grid at {size:?}"
                );
            }
        }
        assert!(trades.bottom() <= h.state().view.bounds.unwrap().bottom());
        if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
            h.render()
                .unwrap()
                .save(std::path::Path::new(&directory).join(format!("orders-{}.png", size.y)))
                .unwrap();
        }
    }
}

/// State used by the trade blotter test fixtures.
struct State {
    /// View associated with this state.
    view: TradeWorkspace,
    /// Snapshot associated with this state.
    snapshot: ContractPublication,
    /// Ordered requests retained by State.
    requests: Vec<serde_json::Value>,
    /// Whether fail load is enabled or currently true.
    fail_load: bool,
}


/// Provides snapshot for the trade blotter implementation.
fn snapshot() -> ContractPublication {
    let mut rows = vec![
        serde_json::json!({"id":"order:27","fields":{"kind":"order","data":"{\"orderId\":27}"}}),
    ];
    rows.push(serde_json::json!({"id":"trade:1","fields":{"kind":"trade","data":"{\"index\":0,\"tradeId\":1,\"tradeState\":\"NewTrade\"}"}}));
    rows.push(serde_json::json!({"id":"market:0","fields":{"kind":"market","data":serde_json::json!({"windowMethod":"Frozen emulator preview","underlyingPrice":"100","contracts":[{"contractId":"SC","isCall":true,"strike":"110","bid":"2","ask":"2.4","delta":0.16}]}).to_string()}}));
    serde_json::from_value(serde_json::json!({"schema":1,"generation":3,"busy":false,"status":"Completed","outcomes":[],"result":{"view":"trade-orders","rows":rows,"choices":{"portfolio":["Development"],"fund":["Test Fund"],"expiry":["2026-11-20","2026-12-18"]},"hasMore":false,"context":{"orderId":"27","tradeId":"1","fundId":"701","composerSessionId":"fixture-session","tradeState":"NewTrade","valueDate":"2026-10-14","expiry":"2026-11-20","editable":"True","compositionType":"ShortIronCondor","brokerMode":"Emulator","canSelectChainSource":"True","quantity":"1","price":"-3.20","previewKey":"SC","canPlaceOrder":"True"}}})).unwrap()
}


/// Provides harness for the trade blotter implementation.
fn harness() -> Harness<'static, State> {
    harness_with_market(true)
}


/// Provides harness with market for the trade blotter implementation.
fn harness_with_market(managed_decimal_text: bool) -> Harness<'static, State> {
    let mut initial = snapshot();
    if !managed_decimal_text {
        // Keep the frozen before executable's inactive market setup identical
        // for the established-plan frame comparison.
        let row = initial
            .result
            .rows
            .iter_mut()
            .find(|r| r.id == "market:0")
            .unwrap();
        row.fields.insert("data".into(),serde_json::json!({"windowMethod":"Frozen emulator preview","underlyingPrice":100,"contracts":[{"contractId":"SC","isCall":true,"strike":110,"bid":2,"ask":2.4,"delta":0.16}]}).to_string());
    }
    let mut view = TradeWorkspace::default();
    view.open = true;
    let mut h = Harness::new_state(
        |ctx, state: &mut State| {
            // Encode authored row documents exactly as the managed binary bridge does.
            let mut changed = false;
            for row in &mut state.snapshot.result.rows {
                if let Some(document) = row.fields.remove("data") {
                    changed = true;
                    let value: serde_json::Value = serde_json::from_str(&document).unwrap();
                    row.data = Some(ifm_ui_egui::wire::encode_value(&value).unwrap().into());
                }
            }
            if changed {
                state.snapshot.invalidate_render_cache();
            }
            if let Some(request) = state.view.draw(ctx, &state.snapshot) {
                let v: serde_json::Value = serde_json::from_str(&request).unwrap();
                let failed = state.fail_load && v["intent"] == "load-trade";
                state.snapshot.outcomes=serde_json::from_value(serde_json::json!([{"requestId":v["requestId"],"state":if failed {"Failed"} else {"Completed"},"message":if failed {"Persisted trade limits were not found."} else {""},"generation":3}])).unwrap();
                if v["intent"] == "close-trade" {
                    let key = v["fields"]["tradeTabId"].as_str().unwrap();
                    state.snapshot.result.rows.retain(|r| {
                        r.fields.get("kind").is_none_or(|k| {
                            k != "established"
                                && !(k == "workspace-tab" && r.id == format!("workspace-tab:{key}"))
                        })
                    });
                }
                if v["intent"] == "activate-trade" || v["intent"] == "close-trade" {
                    let key = v["fields"]["tradeTabId"].as_str().unwrap();
                    for row in &mut state.snapshot.result.rows {
                        if row.fields.get("kind").is_some_and(|k| k == "workspace-tab") {
                            let mut value: serde_json::Value =
                                ifm_ui_egui::wire::decode(row.data.as_ref().unwrap()).unwrap();
                            value["active"] = serde_json::json!(if v["intent"] == "close-trade" {
                                true
                            } else {
                                value["key"] == key
                            });
                            row.data =
                                Some(ifm_ui_egui::wire::encode_value(&value).unwrap().into());
                        }
                    }
                }
                if v["intent"] == "activate-trade" || v["intent"] == "close-trade" {
                    let active = state
                        .snapshot
                        .result
                        .rows
                        .iter()
                        .filter(|r| r.fields.get("kind").is_some_and(|k| k == "workspace-tab"))
                        .map(|r| {
                            ifm_ui_egui::wire::decode::<serde_json::Value>(r.data.as_ref().unwrap())
                                .unwrap()
                        })
                        .find(|v| v["active"] == true);
                    if let Some(active) = active {
                        let id = active["key"]
                            .as_str()
                            .unwrap()
                            .split('.')
                            .filter_map(|v| v.parse::<i32>().ok())
                            .collect::<Vec<_>>();
                        if id.len() == 4 {
                            state.snapshot.result.rows.retain(|r| {
                                r.fields.get("kind").is_none_or(|k| k != "established")
                            });
                            let value = serde_json::json!({"id":{"portfolioId":id[0],"fundId":id[1],"orderId":id[2],"tradeId":id[3]}});
                            let mut row:ifm_ui_egui::contract_editor::ContractRow=serde_json::from_value(serde_json::json!({"id":"established:active","fields":{"kind":"established"}})).unwrap();
                            row.data =
                                Some(ifm_ui_egui::wire::encode_value(&value).unwrap().into());
                            state.snapshot.result.rows.push(row);
                        }
                    }
                }
                state.requests.push(v);
            }
        },
        State {
            view,
            snapshot: initial,
            requests: vec![],
            fail_load: false,
        },
    );
    h.set_size(eframe::egui::vec2(2100., 1300.));
    h.run();
    h.get_by_label("Trade Blotter").click();
    h.run();
    h.run();
    h
}


/// Verifies that iron condor tabs and broker fields match the source inventory.
#[test]
fn iron_condor_tabs_and_broker_fields_match_the_source_inventory() {
    let mut h = harness();
    for label in [
        "Market Selection",
        "Broker Trade",
        "Order Fills",
        "SHORT CALL DELTA:",
        "SHORT PUT DELTA:",
        "LIQUIDITY:",
        "Live Feed",
    ] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
    assert!(h.query_by_label("Leg Staging").is_none());
    assert!(h.query_by_label("Load Chain").is_none());
    assert!(h.query_by_label("Refresh Trade").is_none());
    h.get_by_label("Broker Trade").click();
    h.run();
    for label in [
        "Contracts:",
        "Order type:",
        "Time in force:",
        "Action:",
        "OrderPrice:",
        "Net bid:",
        "Net mid:",
        "Net ask:",
        "IFM algorithm:",
        "Pace:",
        "Fund Economics and Limits:",
        "Available funds:",
        "Risk margin:",
        "Put spread:",
        "Call spread:",
        "Place Order",
        "Manage qualification...",
    ] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
}

/// OrderPrice uses the execution highlight only when the published preview permits placement.
#[test]
fn broker_order_price_highlight_follows_placement_eligibility() {
    fn yellow_background(shape: &egui::Shape, bounds: egui::Rect) -> bool {
        match shape {
            egui::Shape::Vec(shapes) => shapes.iter().any(|shape| yellow_background(shape, bounds)),
            egui::Shape::Rect(rect) => rect.fill == egui::Color32::YELLOW
                && bounds.contains(rect.rect.center()),
            _ => false,
        }
    }
    for (editable, can_place, expected) in [(true, true, true), (true, false, false), (false, true, false)] {
        let mut h = harness();
        let context = &mut h.state_mut().snapshot.result.context;
        context.insert("editable".into(), if editable { "True" } else { "False" }.into());
        context.insert("canPlaceOrder".into(), if can_place { "True" } else { "False" }.into());
        h.state_mut().snapshot.invalidate_render_cache();
        h.get_by_label("Broker Trade").click();
        h.run();
        let bounds = h.state().view.controls["brokerPreviewNetLimitTicks"];
        assert_eq!(h.output().shapes.iter().any(|shape| yellow_background(&shape.shape, bounds)), expected);
    }
}


#[test]
fn broker_trade_draws_four_prices_and_indicative_mid_with_an_empty_order_price() {
    let mut h = harness();
    let result = &mut h.state_mut().snapshot.result;
    result.context.insert("price".into(), String::new());
    result.context.insert("canPlaceOrder".into(), "False".into());
    let mut push = |kind: &str, index: usize, value: serde_json::Value| {
        result.rows.push(serde_json::from_value(serde_json::json!({
            "id": format!("{kind}:{index}"),
            "fields": {"kind": kind},
            "data": ifm_ui_egui::wire::encode_value(&value).unwrap(),
        })).unwrap());
    };
    for (index, (role, bid, ask)) in [
        ("+LC", "0.50", "0.75"),
        ("-SC", "0.70", "0.95"),
        ("-SP", "125.00", "126.00"),
        ("+LP", "108.50", "110.00"),
    ].into_iter().enumerate() {
        push("leg", index, serde_json::json!({
            "contractId": format!("ES-LEG-{index}"), "role": role, "bid": bid, "ask": ask,
            "quoteAtUtc": "2026-11-06T11:15:00Z", "stale": index < 2,
        }));
    }
    push("broker-preview", 0, serde_json::json!({"fields": {"Indicative net mid": "-16.45"}}));
    h.state_mut().snapshot.result.context.insert("asOfUtc".into(), "2026-11-06T11:17:00Z".into());
    h.state_mut().snapshot.invalidate_render_cache();
    h.get_by_label("Broker Trade").click();
    h.run();
    for label in ["ES-LEG-0", "ES-LEG-1", "ES-LEG-2", "ES-LEG-3", "0.50", "0.95",
        "Indicative net mid:"] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
    let grid = h.state().view.controls["brokerPreviewLegGrid"];
    let mid = h.state().view.controls["broker-field-Indicative net mid"];
    let price = h.state().view.controls["brokerPreviewNetLimitTicks"];
    assert_eq!(text_baselines(&h.output().shapes, grid).iter()
        .filter(|(text, _)| text == "120s (stale)").count(), 2);
    assert!(text_baselines(&h.output().shapes, mid).iter().any(|(text, _)| text == "-16.45"));
    assert!(text_baselines(&h.output().shapes, price).is_empty());
    if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
        h.render().unwrap()
            .save(std::path::Path::new(&directory).join("broker-indicative-net-mid.png")).unwrap();
    }
}


/// Verifies that expiry selection refreshes without a load button.
#[test]
fn expiry_selection_refreshes_without_a_load_button() {
    let mut h = harness();
    h.get_by_label("expiry: 2026-11-20").click();
    h.run();
    h.get_by_label("2026-12-18").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "chain" && v["fields"]["expiry"] == "2026-12-18")
    );
}


/// Verifies that switching to fills requests durable evidence and empty actions are disabled.
#[test]
fn switching_to_fills_requests_durable_evidence_and_empty_actions_are_disabled() {
    let mut h = harness();
    h.get_by_label("Order Fills").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "refresh-execution")
    );
    assert!(h.query_by_label("Orders").is_some());
    h.get_by_label("Cancel Unfilled").click();
    h.run();
    assert!(
        !h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "cancel-unfilled")
    );
}


/// Verifies that live checkbox uses the same chain intent.
#[test]
fn live_checkbox_uses_the_same_chain_intent() {
    let mut h = harness();
    h.get_by_label("Live Feed").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "chain" && v["fields"]["live"] == "true")
    );
}


#[test]
fn live_quote_timeouts_keep_feed_checked_and_schedule_the_next_read() {
    let mut h = harness();
    h.get_by_label("Live Feed").click();
    h.run();
    let mut request = h.state().requests.last().unwrap().clone();
    assert_eq!(request["fields"]["live"], "true");
    for _ in 0..5 {
        h.state_mut().snapshot.outcomes = serde_json::from_value(serde_json::json!([
            {"requestId":request["requestId"],"state":"Completed",
             "message":"Live option quotes timed out. Waiting up to another 30 seconds."}
        ])).unwrap();
        let mut errors = ifm_ui_egui::ui::errors::ErrorDialogs::default();
        errors.outcomes("trade-orders", &h.state().snapshot.outcomes);
        assert_eq!(errors.message(), None);
        let before = h.state().requests.len();
        let now = h.ctx.input(|input| input.time);
        h.input_mut().time = Some(now + 2.);
        h.run();
        assert_eq!(h.state().requests.len(), before + 1);
        request = h.state().requests[before].clone();
        assert_eq!(request["intent"], "refresh-chain");
        assert_eq!(request["fields"]["live"], "true");
    }
}


/// A failed live read must stop timed polling before dismissal, retain its error,
/// and let the operator explicitly re-enable the unchecked checkbox afterward.
#[test]
fn failed_live_feed_read_stops_polling_until_explicit_retry() {
    const ERROR: &str = "Backend error 503: Native browser source is unavailable";
    for action in ["chain", "chain-source", "refresh-chain"] {
        let mut h = harness();
        h.get_by_label("Live Feed").click();
        h.run();
        let original = h.state().requests.last().unwrap().clone();
        assert_eq!(original["fields"]["live"], "true");
        let publication = snapshot();
        let request: serde_json::Value =
            serde_json::from_str(&h.state_mut().view.request(action, &publication)).unwrap();
        h.state_mut().snapshot.outcomes = serde_json::from_value(serde_json::json!([
            {"requestId":request["requestId"],"state":"Failed","message":ERROR}
        ]))
        .unwrap();
        // A failed backend may still publish its previous live flag. It must
        // not re-check the local control on a later paint of this same session.
        h.state_mut()
            .snapshot
            .result
            .context
            .insert("live".into(), "True".into());
        let mut errors = ifm_ui_egui::ui::errors::ErrorDialogs::default();
        errors.outcomes("trade-orders", &h.state().snapshot.outcomes);
        let before = h.state().requests.len();
        h.run();
        assert_eq!(errors.message(), Some(ERROR));
        errors.acknowledge();
        let now = h.ctx.input(|input| input.time);
        for elapsed in [2., 4., 6.] {
            h.input_mut().time = Some(now + elapsed);
            h.run();
        }
        assert_eq!(h.state().requests.len(), before, "Retried {action}");
        assert_eq!(errors.message(), None);
        h.get_by_label("Live Feed").click();
        h.run();
        // Polling may resume after the explicit retry completes; inspect the
        // first new request rather than any subsequent healthy quote refresh.
        let retry = &h.state().requests[before];
        assert_eq!(retry["intent"], "chain");
        assert_eq!(retry["fields"]["live"], "true");
        assert_eq!(retry["fields"]["expiry"], original["fields"]["expiry"]);
        assert_eq!(retry["fields"]["price"], original["fields"]["price"]);
    }
}


/// Discard a deferred live selection when its outstanding quote read fails;
/// otherwise the queued selection would restart the same failing provider.
#[test]
fn failed_live_feed_read_does_not_replay_a_deferred_chain_selection() {
    let mut h = harness();
    let publication = snapshot();
    let request: serde_json::Value =
        serde_json::from_str(&h.state_mut().view.request("refresh-chain", &publication)).unwrap();
    h.state_mut().snapshot.busy = true;
    h.get_by_label("Live Feed").click();
    h.run();
    let before = h.state().requests.len();
    h.state_mut().snapshot.busy = false;
    h.state_mut().snapshot.outcomes = serde_json::from_value(serde_json::json!([
        {"requestId":request["requestId"],"state":"Failed","message":"Source unavailable"}
    ]))
    .unwrap();
    h.run();
    assert_eq!(h.state().requests.len(), before);
    h.get_by_label("Live Feed").click();
    h.run();
    assert_eq!(h.state().requests.last().unwrap()["fields"]["live"], "true");
}


/// Unrelated order failures must leave live mode enabled. A rejected feed read
/// disables it only when the rejection matches the pending request identity.
#[test]
fn only_the_matching_failed_feed_request_unchecks_live_feed() {
    for (action, reject) in [("refresh-execution", false), ("refresh-chain", true)] {
        let mut h = harness();
        h.get_by_label("Live Feed").click();
        h.run();
        let publication = snapshot();
        let request: serde_json::Value =
            serde_json::from_str(&h.state_mut().view.request(action, &publication)).unwrap();
        h.state_mut().view.submission_rejected("stale-request");
        if reject {
            h.state_mut()
                .view
                .submission_rejected(request["requestId"].as_str().unwrap());
        } else {
            h.state_mut().snapshot.outcomes = serde_json::from_value(serde_json::json!([
                {"requestId":request["requestId"],"state":"Failed","message":"Order evidence unavailable"}
            ]))
            .unwrap();
        }
        h.run();
        h.get_by_label("Live Feed").click();
        h.run();
        assert_eq!(
            h.state().requests.last().unwrap()["fields"]["live"],
            if reject { "true" } else { "false" }
        );
    }
}


/// Verifies that order type change updates preview without submitting.
#[test]
fn order_type_change_updates_preview_without_submitting() {
    let mut h = harness();
    h.get_by_label("Broker Trade").click();
    h.run();
    h.get_by_label("orderType: Limit").click();
    h.run();
    h.get_by_label("Market").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "preview-draft" && v["fields"]["orderType"] == "Market")
    );
    assert!(
        !h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "submit-order")
    );
}


/// Verifies that read only trade disables live feed and place order.
#[test]
fn read_only_trade_disables_live_feed_and_place_order() {
    let mut h = harness();
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("editable".into(), "False".into());
    h.run();
    h.get_by_label("Live Feed").click();
    h.run();
    h.get_by_label("Broker Trade").click();
    h.run();
    h.get_by_label("Place Order").click();
    h.run();
    assert!(
        !h.state()
            .requests
            .iter()
            .any(|v| matches!(v["intent"].as_str(), Some("chain" | "submit-order")))
    );
}


/// Verifies that vertical strategy has the five source tabs and staging controls.
#[test]
fn vertical_strategy_has_the_five_source_tabs_and_staging_controls() {
    let mut h = harness();
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("compositionType".into(), "PutCreditSpread".into());
    h.run();
    h.get_by_label("Leg Staging").click();
    h.run();
    for label in [
        "Broker Side",
        "Validation / Review",
        "Clear Staged Legs",
        "Recalculate / Validate",
        "Submit Opening Order",
        "Submit Closing Order",
    ] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
    h.get_by_label("Clear Staged Legs").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "clear-staged")
    );
    h.get_by_label("Orders and Fills").click();
    h.run();
    assert!(h.query_by_label("Broker orders: none submitted").is_some());
    assert!(h.query_by_label("End Of Day...").is_some());
}


/// Verifies that qualification exposes report and durable actions.
#[test]
fn qualification_exposes_report_and_durable_actions() {
    let mut h = harness();
    h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({"id":"account:0","fields":{"kind":"account","data":serde_json::json!({"qualificationStatus":"Accepted","manualHold":false}).to_string()}})).unwrap());
    h.get_by_label("Broker Trade").click();
    h.run();
    h.get_by_label("Manage qualification...").click();
    h.run();
    for label in [
        "Submit evidence",
        "Accept reviewed manifest",
        "Revoke",
        "Set hold",
        "Release hold",
        "Resynchronize",
    ] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
    h.get_by_label("Set hold").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "qualification-hold")
    );
}


fn publish_fill_controls(h: &mut Harness<'static, State>, id: &str, order: i64, attempt: &str, can_mutate: bool) {
    let snapshot = serde_json::json!({
        "date": "2026-10-14",
        "selected": [order, attempt],
        "orders": [{
            "order_id": order, "trade_id": 1, "attempt": attempt, "status": "Submitted",
            "order_time": "", "order_quantity": 1, "filled_quantity": 0,
            "last_fill": "", "rejection": "", "fills": []
        }],
        "controls": [{
            "id": id, "caption": "Broker component", "price": "-3.2",
            "mutation": {"canMutate": can_mutate, "minimum": "-10", "maximum": "0", "tick": "0.05"}
        }]
    });
    h.state_mut().snapshot.result.documents.insert(
        "blotter-fund-fills".into(), ifm_ui_egui::wire::encode_value(&snapshot).unwrap().into()
    );
}

/// Verifies that working order mutations target the selected broker identity.
#[test]
fn working_order_mutations_target_the_selected_broker_identity() {
    let mut h = harness();
    let attempt = "6fb55186-c479-73ea-b0af-65a34f98c51c";
    publish_fill_controls(&mut h, "owner", 2202, attempt, true);
    h.get_by_label("Order Fills").click();
    h.run();
    let reconcile = h.get_by_label("Reconcile").rect();
    let update = h.get_by_label("Update Order Price").rect();
    let cancel = h.get_by_label("Cancel Unfilled").rect();
    assert!(update.left() - reconcile.right() > 100.);
    assert!(cancel.left() > update.right());
    assert!((cancel.center().y - reconcile.center().y).abs() < 3.);
    h.get_by_label("Update Order Price").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "update-limit" && v["fields"]["brokerOrderId"] == "owner"
                && v["fields"]["fillsOrderId"] == "2202" && v["fields"]["fillsAttempt"] == attempt)
    );
    h.get_by_label("Cancel Unfilled").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "cancel-unfilled" && v["fields"]["brokerOrderId"] == "owner")
    );
}


#[test]
fn add_trade_reference_defaults_to_action_and_tab_label_and_preserves_edits() {
    let mut h = harness();
    let title = "1901:1501 ShortIronCondor=8100:8050|7375:7325 26-Nov-20";
    h.state_mut().snapshot.result.context.extend([
        ("preparedTradeId".into(), "2000".into()),
        ("preparedTradeLabel".into(), title.into()),
        ("closingType".into(), "LongIronCondor".into()),
    ]);
    h.get_by_label("Trade Orders").click();
    h.run();
    h.get_by_label("Add Trade...").click();
    h.run();
    h.run();
    assert!(h.query_by_label("Instruction Reference:").is_none());
    let expected = format!("Buy {title}");
    let input_font = egui::FontSelection::Default.resolve(&h.ctx.style());
    let text_width = h.ctx.fonts(|fonts| fonts.layout_no_wrap(
        expected.clone(), input_font, egui::Color32::WHITE,
    ).size().x);
    assert!(h.state().view.controls["draft-trade-reference"].width() >= text_width + 8.);
    assert!(h.output().shapes.iter().any(|shape| matches!(
        &shape.shape, egui::Shape::Text(text) if text.galley.job.text == expected
    )));
    let point = h.state().view.controls["draft-trade-reference"].center();
    for pressed in [true, false] {
        h.input_mut().events.push(egui::Event::PointerButton {
            pos: point, button: egui::PointerButton::Primary, pressed, modifiers: egui::Modifiers::NONE,
        });
        h.run();
    }
    h.input_mut().events.push(egui::Event::Key {
        key: egui::Key::End, physical_key: None, pressed: true, repeat: false,
        modifiers: egui::Modifiers::NONE,
    });
    h.run();
    h.input_mut().events.push(egui::Event::Text(" — my edited reference".into()));
    h.run();
    let request: serde_json::Value = serde_json::from_str(
        &h.state_mut().view.request("inspect-draft", &snapshot()),
    ).unwrap();
    assert_eq!(request["fields"]["tradeReference"], format!("{expected} — my edited reference"));
}


#[test]
fn change_trade_prefills_existing_draft_and_requires_a_fund_contract() {
    let mut h = harness();
    h.state_mut().snapshot.result.context.extend([
        ("canChangeTrade".into(), "True".into()),
        ("changeTradeType".into(), "ShortIronCondor".into()),
        ("changeBaseContract".into(), "ES20311219".into()),
        ("changeEffectiveDate".into(), "2026-11-09".into()),
        ("changeTradeReference".into(), "Original draft".into()),
        ("changeTradeClosing".into(), "false".into()),
    ]);
    h.state_mut().snapshot.result.choices.insert("baseContract".into(), vec!["ES20261218".into()]);
    h.get_by_label("Trade Orders").click();
    h.run();
    h.get_by_label("Change Trade...").click();
    h.run();
    let request = h.state().requests.last().unwrap().clone();
    assert_eq!(request["intent"], "prepare-change-trade");
    h.state_mut().snapshot.outcomes = serde_json::from_value(serde_json::json!([
        {"requestId":request["requestId"],"state":"Completed","message":"Trade editor opened"}
    ])).unwrap();
    h.run();
    h.run();
    assert!(h.query_by_label("Change Trade").is_some());
    assert!(h.output().shapes.iter().any(|shape| matches!(
        &shape.shape, egui::Shape::Text(text) if text.galley.job.text == "ES20311219"
    )));
    let point = h.state().view.controls["draft-field-7"].center();
    h.input_mut().events.push(egui::Event::PointerMoved(point));
    for pressed in [true, false] {
        h.input_mut().events.push(egui::Event::PointerButton {
            pos: point, button: egui::PointerButton::Primary, pressed, modifiers: egui::Modifiers::NONE,
        });
        h.run();
    }
    h.get_by_label("ES20261218").click();
    h.run();
    h.get_by_label("Save").click();
    h.run();
    let save = h.state().requests.last().unwrap();
    assert_eq!(save["intent"], "change-trade");
    assert_eq!(save["fields"]["tradeId"], request["fields"]["tradeId"]);
    assert_eq!(save["fields"]["baseContract"], "ES20261218");
    assert_eq!(save["fields"]["tradeReference"], "Original draft");
    assert_eq!(save["fields"]["effectiveDate"], "2026-11-09");
}

#[test]
fn rejected_trade_shows_status_and_confirms_removal_with_centered_buttons() {
    let mut h = harness();
    let reason = "EM.EXPIRED: Approved order is expired or time is not UTC.";
    let row = h.state_mut().snapshot.result.rows.iter_mut().find(|row| row.id == "trade:1").unwrap();
    row.fields.insert("data".into(), serde_json::json!({
        "index": 0, "tradeId": 1, "tradeState": "Rejected", "tradeStatus": reason
    }).to_string());
    h.get_by_label("Trade Orders").click();
    h.run();
    assert!(h.query_by_label("Rejected").is_some());
    assert!(h.query_by_label("Trade Status").is_some());
    assert!(h.query_by_label(reason).is_some());
    fn paints_red_rejection(shape: &egui::Shape) -> bool {
        match shape {
            egui::Shape::Vec(shapes) => shapes.iter().any(paints_red_rejection),
            egui::Shape::Text(text) => text.galley.job.text == "Rejected"
                && text.galley.job.sections.iter().any(|section| {
                    let color = if section.format.color == egui::Color32::PLACEHOLDER {
                        text.fallback_color
                    } else {
                        section.format.color
                    };
                    text.override_text_color.unwrap_or(color) == egui::Color32::from_rgb(255, 85, 85)
                }),
            _ => false,
        }
    }
    assert!(h.output().shapes.iter().any(|shape| paints_red_rejection(&shape.shape)));
    h.get_by_label("Remove Trade").click();
    h.run();
    assert!(h.query_by_label("Do you want to remove TradeId: 1 ?").is_some());
    let controls = &h.state().view.controls;
    assert_eq!(controls["remove-trade-icon"].size(), egui::Vec2::splat(56.0));
    let buttons = controls["remove-trade-yes"].union(controls["remove-trade-no"]);
    assert!((buttons.center().x - controls["draft-dialog"].center().x).abs() < 2.);
    h.get_by_label("No").click();
    h.run();
    assert!(!h.state().requests.iter().any(|request| request["intent"] == "remove-trade"));
    h.get_by_label("Remove Trade").click();
    h.run();
    h.get_by_label("Yes").click();
    h.run();
    h.run();
    let requests: Vec<_> = h.state().requests.iter().filter(|request| request["intent"] == "remove-trade").collect();
    assert_eq!(requests.len(), 1);
    assert_eq!(requests[0]["fields"]["removeTradeId"], "1");
    assert!(h.query_by_label("Do you want to remove TradeId: 1 ?").is_none());
}


/// Verifies that load trade closes the editor and keeps monitoring outside the blotter.
#[test]
fn load_trade_closes_the_editor_and_keeps_monitoring_outside_the_blotter() {
    let mut h = harness();
    h.state_mut()
        .snapshot
        .result
        .rows
        .iter_mut()
        .find(|r| r.id == "trade:1")
        .unwrap()
        .fields
        .insert(
            "data".into(),
            serde_json::json!({"index":0,"tradeId":1,"tradeState":"Open"}).to_string(),
        );
    h.state_mut().snapshot.result.rows.push(
        serde_json::from_value(
            serde_json::json!({"id":"established:1","fields":{"kind":"established","data":serde_json::json!({"id":{"orderId":27,"tradeId":1}}).to_string()}}),
        )
        .unwrap(),
    );
    h.get_by_label("Trade Orders").click();
    h.run();
    h.get_by_label("Load Trade").click();
    h.run();
    h.run();
    assert!(h.query_by_label("Trade Order Editor").is_none());
    assert!(h.query_by_label("ActionDateTime").is_some());
    assert!(h.query_by_label("Close trade 27:1").is_some());
    assert!(!h.state().view.open);
    let panel = h
        .state()
        .view
        .loaded_bounds
        .expect("Trade occupies the main center panel");
    assert!(panel.left() < 10. && panel.top() < 10.);
    assert!(panel.width() > 2000. && panel.height() > 1200.);
    assert!(!h.state().requests.iter().any(|v| v["intent"] == "close"));
    h.get_by_label("Close trade 27:1").click();
    h.run();
    h.run();
    assert!(h.state().view.loaded_bounds.is_none());
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "close-trade")
    );
}


/// A quote refresh for unchanged legs must update the actual order-price input.
#[test]
fn unchanged_legs_refresh_the_order_price_used_by_submission() {
    let mut h = harness();
    h.state_mut().snapshot.result.context.insert("live".into(), "True".into());
    h.state_mut().snapshot.result.context.insert("price".into(), "-3.45".into());
    h.run();
    let state = h.state_mut();
    let request: serde_json::Value = serde_json::from_str(
        &state.view.request("submit-order", &state.snapshot)).unwrap();
    assert_eq!(request["fields"]["price"], "-3.45");
    assert_eq!(request["fields"]["priceEdited"], "false");
    assert_eq!(state.snapshot.result.context["previewKey"], "SC");
}


/// Verifies that late composer snapshot replaces the initial empty draft.
#[test]
fn late_composer_snapshot_replaces_the_initial_empty_draft() {
    let mut h = harness();
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("composerSessionId".into(), "ready-session".into());
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("expiry".into(), "2026-12-18".into());
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("putDelta".into(), "20".into());
    h.run();
    assert!(h.query_by_label("expiry: 2026-12-18").is_some());
    h.get_by_label("Live Feed").click();
    h.run();
    assert!(h.state().requests.iter().any(|v| v["intent"] == "chain"
        && v["fields"]["expiry"] == "2026-12-18"
        && v["fields"]["putDelta"] == "20"));
}


/// Verifies that background quote refresh keeps controls bright and queues edits until
/// acknowledged.
#[test]
fn background_quote_refresh_keeps_controls_bright_and_queues_edits_until_acknowledged() {
    let mut h = harness();
    let snapshot = snapshot();
    let refresh: serde_json::Value =
        serde_json::from_str(&h.state_mut().view.request("refresh-chain", &snapshot)).unwrap();
    h.state_mut().snapshot.busy = true;
    h.state_mut().snapshot.status = "Pending".into();
    h.state_mut().snapshot.outcomes.clear();
    let before = h.state().requests.len();
    h.run();
    assert!(h.query_by_label("Pending").is_none());
    h.get_by_label("Live Feed").click(); // Remains enabled during a read refresh.
    h.run();
    assert_eq!(h.state().requests.len(), before); // Never submit into the busy gateway.
    h.state_mut().snapshot.busy = false;
    h.state_mut().snapshot.status = "Completed".into();
    h.state_mut().snapshot.outcomes = serde_json::from_value(serde_json::json!([
        {"requestId":refresh["requestId"],"state":"Completed","message":"","generation":4}
    ]))
    .unwrap();
    h.state_mut().snapshot.generation = 4;
    h.run();
    let request = h.state().requests.last().unwrap();
    assert_eq!(request["intent"], "chain");
    assert_eq!(request["fields"]["live"], "true");
    assert_eq!(request["expectedGeneration"], 4);
}


/// Verifies that rejected submission releases controls without an outcome or replaying a mutation.
#[test]
fn rejected_submission_releases_controls_without_an_outcome_or_replaying_a_mutation() {
    let mut h = harness();
    let request: serde_json::Value =
        serde_json::from_str(&h.state_mut().view.request("chain", &snapshot())).unwrap();
    h.state_mut().snapshot.outcomes.clear();
    h.run();
    h.state_mut()
        .view
        .submission_rejected(request["requestId"].as_str().unwrap());
    h.run();
    h.get_by_label("Live Feed").click();
    h.run();
    assert!(h.state().requests.iter().any(|r| r["intent"] == "chain"));
}


/// Verifies that physical pointer click switches the blotter tab.
#[test]
fn physical_pointer_click_switches_the_blotter_tab() {
    use eframe::egui::{Event, Modifiers, PointerButton};
    let mut h = harness();
    let pos = h.state().view.controls["tab:Order Fills"].center();
    h.input_mut().events.push(Event::PointerMoved(pos));
    h.step();
    h.input_mut().events.push(Event::PointerButton {
        pos,
        button: PointerButton::Primary,
        pressed: true,
        modifiers: Modifiers::NONE,
    });
    h.step();
    h.input_mut().events.push(Event::PointerButton {
        pos,
        button: PointerButton::Primary,
        pressed: false,
        modifiers: Modifiers::NONE,
    });
    h.run();
    assert!(h.query_by_label("Cancel Unfilled").is_some());
}

#[test]
fn reconcile_button_uses_the_restored_pending_broker_identity() {
    let mut h = harness();
    let id = "101.701.2202.6fb55186c47973eab0af65a34f98c51c.8c6aaaa142c745998c1a2caae964a793";
    publish_fill_controls(&mut h, id, 2202, "6fb55186-c479-73ea-b0af-65a34f98c51c", false);
    h.run();
    h.get_by_label("Order Fills").click();
    h.run();
    let pos = h.get_by_label("Reconcile").rect().center();
    h.input_mut().events.push(egui::Event::PointerMoved(pos));
    h.step();
    for pressed in [true, false] {
        h.input_mut().events.push(egui::Event::PointerButton {
            pos, pressed, button: egui::PointerButton::Primary, modifiers: egui::Modifiers::NONE,
        });
        h.step();
    }
    h.run();
    let request = h.state().requests.iter().find(|request| request["intent"] == "reconcile-broker-order").unwrap();
    assert_eq!(request["fields"]["brokerOrderId"], id);
}


/// Verifies that accepted submission selects fills before evidence refresh completes.
#[test]
fn accepted_submission_selects_fills_before_evidence_refresh_completes() {
    let mut h = harness();
    h.get_by_label("Broker Trade").click();
    h.run();
    let state = h.state_mut();
    state.snapshot.busy = true;
    state
        .snapshot
        .result
        .context
        .insert("submitted".into(), "True".into());
    state
        .snapshot
        .result
        .context
        .insert("submissionStatus".into(), "Broker accepted".into());
    h.run();
    assert!(h.query_by_label("Orders").is_some());
    assert!(h.query_by_label("Broker accepted").is_none());
    assert!(h.query_by_label("Place Order").is_none());
    h.state_mut().snapshot.status = "Execution evidence unavailable".into();
    h.run();
    assert!(h.query_by_label("Orders").is_some());
    assert!(h.query_by_label("Broker accepted").is_none());
}


/// Verifies that confirmation waits for broker result and rejection closes before error.
#[test]
fn confirmation_waits_for_broker_result_and_rejection_closes_before_error() {
    let mut h = harness();
    let confirmation = serde_json::json!({"id":"test-confirmation","submitting":true,
        "order":{"orderQuantity":1},"legs":[{"side":"Sell","role":"Short Put",
        "contractId":"ES-VERY-LONG-CONTRACT-IDENTIFIER-20261120-P-6000","legCommission":"0.65"}]})
    .to_string();
    h.state_mut().snapshot.result.documents.insert(
        "confirmation".into(),
        ifm_ui_egui::wire::encode_value(
            &serde_json::from_str::<serde_json::Value>(&confirmation).unwrap(),
        )
        .unwrap()
        .into(),
    );
    h.state_mut().snapshot.invalidate_render_cache();
    h.run();
    assert!(
        h.query_by_label("Submitting ? awaiting broker acceptance?")
            .is_some()
    );
    assert!(h.query_by_label("Contract ID").is_some());
    let header_x = ["Contract ID", "Role", "Side", "Leg Commission"].map(|caption| {
        h.get_by_label(caption)
            .accesskit_node()
            .bounding_box()
            .unwrap()
            .x0
    });
    assert!(header_x.windows(2).all(|pair| pair[0] < pair[1]));
    let quantity = h
        .get_by_label("Order Quantity:")
        .accesskit_node()
        .bounding_box()
        .unwrap();
    let broker = h
        .get_by_label("Broker Type:")
        .accesskit_node()
        .bounding_box()
        .unwrap();
    assert!(
        (quantity.x1 - broker.x1).abs() < 1.,
        "Summary captions must align at their right edge before the control column"
    );
    assert!(broker.y0 > quantity.y0);
    assert!(h.query_by_label("Total Commission:").is_some());
    h.state_mut().snapshot.result.context.insert(
        "submissionError".into(),
        "BROKER.123: Order rejected".into(),
    );
    h.run();
    assert!(h.query_by_label("BROKER.123: Order rejected").is_none());
    h.state_mut()
        .snapshot
        .result
        .documents
        .remove("confirmation");
    h.state_mut().snapshot.invalidate_render_cache();
    h.run();
    assert!(
        h.query_by_label("Submitting ? awaiting broker acceptance?")
            .is_none()
    );
    assert!(h.query_by_label("BROKER.123: Order rejected").is_some());
    h.get_by_label("OK").click();
    h.run();
    assert!(h.query_by_label("BROKER.123: Order rejected").is_none());
}


/// Verifies that confirmation leg rows follow broker trade display order.
#[test]
fn confirmation_leg_rows_follow_broker_trade_display_order() {
    let mut h = harness();
    for (index, contract) in ["HIGH-CALL", "LOW-PUT"].iter().enumerate() {
        h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({
            "id":format!("leg:{index}"),"fields":{"kind":"leg","data":serde_json::json!({
                "contractId":contract,"role":"Selected","strike":if index==0 {"110"} else {"90"},"isCall":index==0
            }).to_string()}
        })).unwrap());
    }
    let confirmation = serde_json::json!({"id":"ordered-legs","order":{"orderQuantity":1},"legs":[
        {"contractId":"LOW-PUT","role":"Short Put","side":"Sell","legCommission":"0.65"},
        {"contractId":"HIGH-CALL","role":"Short Call","side":"Sell","legCommission":"0.65"}
    ]});
    h.state_mut().snapshot.result.documents.insert(
        "confirmation".into(),
        ifm_ui_egui::wire::encode_value(&confirmation)
            .unwrap()
            .into(),
    );
    h.state_mut().snapshot.invalidate_render_cache();
    h.run();
    let high = h
        .get_by_label("HIGH-CALL")
        .accesskit_node()
        .bounding_box()
        .unwrap();
    let low = h
        .get_by_label("LOW-PUT")
        .accesskit_node()
        .bounding_box()
        .unwrap();
    assert!(
        high.y0 < low.y0,
        "Confirmation must use the selected-leg display order"
    );
}


/// Verifies that broker leg delta precision and grid bounds survive value updates.
#[test]
fn broker_leg_delta_precision_and_grid_bounds_survive_value_updates() {
    let mut h = harness();
    h.state_mut().snapshot.result.rows.push(
        serde_json::from_value(serde_json::json!({
            "id":"leg:precision", "fields":{"kind":"leg"},
            "data":null
        }))
        .unwrap(),
    );
    let publish = |h: &mut Harness<'_, State>, delta: f64, contract: &str| {
        let leg = h.state_mut().snapshot.result.rows.last_mut().unwrap();
        leg.data = Some(
            ifm_ui_egui::wire::encode_value(&serde_json::json!({
                "contractId":contract,"role":"Short Call","delta":delta,"bid":"2.0","ask":"2.4"
            }))
            .unwrap()
            .into(),
        );
        h.state_mut().snapshot.invalidate_render_cache();
    };
    publish(&mut h, 0.16, "ES-CALL");
    h.get_by_label("Broker Trade").click();
    h.run();
    assert!(h.query_by_label("0.160000").is_some());
    let before = h.state().view.controls["brokerPreviewLegGrid"];
    publish(&mut h, -0.123456789, "ES-CHANGED-LONG-CONTRACT-IDENTIFIER");
    h.run();
    assert!(h.query_by_label("-0.123457").is_some());
    assert_eq!(before, h.state().view.controls["brokerPreviewLegGrid"]);
}


/// Verifies that failed load closes editor and shows a separate error.
#[test]
fn failed_load_keeps_editor_open_and_uses_the_correlated_error() {
    let mut h = harness();
    h.state_mut().fail_load = true;
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("canLoadTrade".into(), "True".into());
    h.state_mut()
        .snapshot
        .result
        .rows
        .iter_mut()
        .find(|r| r.id == "trade:1")
        .unwrap()
        .fields
        .insert(
            "data".into(),
            serde_json::json!({"index":0,"tradeId":1,"tradeState":"Open"}).to_string(),
        );
    h.get_by_label("Trade Orders").click();
    h.run();
    h.get_by_label("Load Trade").click();
    h.run();
    assert!(h.state().view.open);
    assert!(h.query_by_label("Trade Order Editor").is_some());
    assert!(h.query_by_label("Load Trade Error").is_none());
    assert_eq!(h.state().snapshot.outcomes[0].message, "Persisted trade limits were not found.");
    assert!(h.state().view.loaded_bounds.is_none());
    assert!(!h.state().requests.iter().any(|v| v["intent"] == "close"));
}


/// Verifies that established iron condor dashboard displays saved limits and leg marks.
#[test]
fn established_iron_condor_dashboard_displays_saved_limits_and_leg_marks() {
    let mut h = harness();
    h.state_mut()
        .snapshot
        .result
        .rows
        .iter_mut()
        .find(|r| r.id == "trade:1")
        .unwrap()
        .fields
        .insert(
            "data".into(),
            serde_json::json!({"index":0,"tradeId":1,"tradeState":"Open"}).to_string(),
        );
    for (kind, value) in [
        (
            "established",
            serde_json::json!({"id":{"orderId":27,"tradeId":1},"status":"Open","openingValue":"-3.2","establishedAtUtc":"2026-10-14T14:30:00Z","legs":[
            {"tradeLegId":"sp","contractId":"SAVED-PUT","putCall":2,"strike":"90","signedQuantity":-1,"cashMultiplier":"50","expiry":"2026-11-20"},
            {"tradeLegId":"lp","contractId":"LONG-PUT","putCall":2,"strike":"85","signedQuantity":1,"cashMultiplier":"50","expiry":"2026-11-20"},
            {"tradeLegId":"sc","contractId":"SAVED-CALL","putCall":1,"strike":"110","signedQuantity":-1,"cashMultiplier":"50","expiry":"2026-11-20"},
            {"tradeLegId":"lc","contractId":"LONG-CALL","putCall":1,"strike":"115","signedQuantity":1,"cashMultiplier":"50","expiry":"2026-11-20"}]}),
        ),
        (
            "position",
            serde_json::json!({"orderId":27,"tradeId":1,"tradeType":"ShortIronCondor","valueDate":"2026-10-14","tradeStatus":"Open","netSpread":"-3.2","tradePnl":"12.5"}),
        ),
        (
            "limits",
            serde_json::json!({"orderId":27,"tradeLimit":{"riskMargin":"38.55","maxProfit":"11.45","maxLoss":"-38.55"}}),
        ),
        (
            "position-details",
            serde_json::json!({"valueDate":"2026-10-14","legs":[{"tradeLegId":"sp","signedQuantity":-1,"openingPrice":"2.5","currentPrice":"2.3"},{"tradeLegId":"lp","signedQuantity":1,"openingPrice":"0.5","currentPrice":"0.4"}]}),
        ),
        (
            "plan",
            serde_json::json!({"valueDate":"2026-10-14","calculatedAtUtc":"2026-10-14T15:00:00Z","state":"Hold","action":"Monitor","explanation":"Saved plan evidence","totalPnl":"12.5"}),
        ),
    ] {
        h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({"id":format!("{kind}:0"),"fields":{"kind":kind,"data":value.to_string()}})).unwrap());
    }
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("canSelectPosition".into(), "True".into());
    h.get_by_label("Trade Orders").click();
    h.run();
    h.get_by_label("Load Trade").click();
    h.run();
    h.run();
    for label in [
        "Option Contract Ids",
        "Iron Condor Net / Forward Spread (price points)",
        "ActionDateTime",
        "SAVED-PUT",
        "38.55",
        "Saved plan evidence",
        "Spread Type",
        "PutCreditSpread",
    ] {
        assert!(
            h.query_all_by_label(label).next().is_some(),
            "Missing {label}"
        );
    }
    assert!(h.state().view.loaded_bounds.unwrap().width() > 2000.);
    let controls = &h.state().view.controls;
    assert!(controls["condor-chart"].left() > controls["condor-history"].right() - 5.);
    assert!(controls["condor-trade-info"].top() > controls["condor-history"].bottom());
    let contracts = controls["condor-contracts"];
    let fourth_contract = h
        .get_by_label("LONG-CALL")
        .accesskit_node()
        .bounding_box()
        .unwrap();
    assert!(fourth_contract.y0 >= f64::from(contracts.top()));
    assert!(fourth_contract.y1 <= f64::from(contracts.bottom() - 16.));
    assert!(controls["condor-limits"].top() >= contracts.bottom());
    assert_eq!(controls["condor-limits"].height(), 68.);
    assert!(controls["condor-plans"].top() > controls["condor-limits"].bottom());
    assert!(
        (controls["condor-plans"].bottom() - h.state().view.loaded_bounds.unwrap().bottom()).abs()
            < 1.
    );
    assert!(h.query_by_label("TradePlans").is_none());
    if let Ok(path) = std::env::var("IFM_CONDOR_SCREENSHOT") {
        h.render().unwrap().save(path).unwrap();
    }
    h.set_size(eframe::egui::vec2(1200., 800.));
    h.run();
    assert!(h.query_by_label("Close trade 27:1 ShortIronCondor=115:110|90:85 26-Nov-20").is_some());

    let result = &mut h.state_mut().snapshot.result;
    result.context.insert("canSelectPosition".into(), "False".into());
    for index in 1..=60 {
        let data = serde_json::json!({"orderId":9000 + index,"tradeId":1,"tradeType":"ShortIronCondor","valueDate":"2026-10-14"});
        result.rows.push(serde_json::from_value(serde_json::json!({
            "id": format!("position:{index}"),
            "fields": {"kind": "position", "data": data.to_string()}
        })).unwrap());
    }
    h.run_steps(2);
    let history = h.state().view.controls["condor-history"];
    assert!(h.query_by_label("9060").is_none());
    h.input_mut().events.extend([
        egui::Event::PointerMoved(history.center()),
        egui::Event::MouseWheel {
            unit: egui::MouseWheelUnit::Point,
            delta: egui::vec2(0., -3000.),
            modifiers: egui::Modifiers::NONE,
        },
    ]);
    h.run_steps(12);
    let last = h.get_by_label("9060").accesskit_node().bounding_box().unwrap();
    assert!(last.y1 <= f64::from(history.bottom() - 16.));
}


/// Provides loaded tabs harness for the trade blotter implementation.
fn loaded_tabs_harness() -> Harness<'static, State> {
    let mut h = harness_with_market(false);
    h.state_mut()
        .snapshot
        .result
        .rows
        .iter_mut()
        .find(|r| r.id == "trade:1")
        .unwrap()
        .fields
        .insert(
            "data".into(),
            serde_json::json!({"index":0,"tradeId":1,"tradeState":"Open"}).to_string(),
        );
    for (kind, key, value) in [
        (
            "established",
            "first",
            serde_json::json!({"id":{"portfolioId":101,"fundId":701,"orderId":27,"tradeId":1}}),
        ),
        (
            "workspace-tab",
            "101.701.27.1",
            serde_json::json!({"key":"101.701.27.1","title":"27:1","kind":"trade","active":true}),
        ),
        (
            "workspace-tab",
            "101.701.28.2",
            serde_json::json!({"key":"101.701.28.2","title":"28:2","kind":"trade","active":false}),
        ),
    ] {
        h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({"id":format!("{kind}:{key}"),"fields":{"kind":kind,"data":value.to_string()}})).unwrap());
    }
    h.get_by_label("Trade Orders").click();
    h.run();
    h.get_by_label("Load Trade").click();
    h.run();
    h.run();
    h
}


/// Provides scrolling trade plans harness for the trade blotter implementation.
fn scrolling_trade_plans_harness() -> Harness<'static, State> {
    trade_plans_harness(200)
}


/// Provides trade plans harness for the trade blotter implementation.
fn trade_plans_harness(count: usize) -> Harness<'static, State> {
    let mut h = loaded_tabs_harness();
    ifm_ui_egui::presentation::apply(&h.ctx);
    for i in 0..count {
        let value = serde_json::json!({"calculatedAtUtc":format!("plan-row-{i:03}"),"explanation":"scroll fixture"});
        h.state_mut().snapshot.result.rows.push(
            serde_json::from_value(serde_json::json!({
                "id":format!("plan:{i}"),"fields":{"kind":"plan","data":value.to_string()}
            }))
            .unwrap(),
        );
    }
    h.set_size(eframe::egui::vec2(1200., 800.));
    h.run();
    h
}


/// Verifies that benchmark trade view mouse frames.
#[test]
#[ignore = "Explicit CPU frame benchmark; run alone in debug and release"]
fn benchmark_trade_view_mouse_frames() {
    use eframe::egui::{Event, pos2};
    let mut results = Vec::new();
    for count in [100, 1_000, 10_000] {
        let mut h = trade_plans_harness(count);
        h.ctx
            .style_mut(|style| style.interaction.tooltip_delay = f32::INFINITY);
        let mut times = Vec::new();
        for frame in 0..65 {
            h.input_mut().events.push(Event::PointerMoved(pos2(
                40. + (frame % 20) as f32 * 20.,
                590.,
            )));
            let start = std::time::Instant::now();
            h.step();
            if frame >= 5 {
                times.push(start.elapsed().as_secs_f64() * 1_000.);
            }
        }
        times.sort_by(f64::total_cmp);
        let mut preparation = Vec::new();
        let mut first_frames = Vec::new();
        for _ in 0..5 {
            h.state_mut().snapshot.invalidate_render_cache();
            let start = std::time::Instant::now();
            TradeWorkspace::prepare_publication(&h.state().snapshot);
            preparation.push(start.elapsed().as_secs_f64() * 1_000.);
            let start = std::time::Instant::now();
            h.step();
            first_frames.push(start.elapsed().as_secs_f64() * 1_000.);
        }
        preparation.sort_by(f64::total_cmp);
        first_frames.sort_by(f64::total_cmp);
        let result = serde_json::json!({"plans":count,"frames":times.len(),"p50Ms":times[30],"p95Ms":times[57],"maxMs":times[59],
            "preparationP50Ms":preparation[2],"preparedFirstFrameP50Ms":first_frames[2]});
        println!("{result}");
        results.push(result);
    }
    if let Ok(path) = std::env::var("IFM_TRADE_FRAME_BENCHMARK") {
        std::fs::write(path, serde_json::to_vec_pretty(&results).unwrap()).unwrap();
    }
}


/// Verifies that trade plan grid scrolls both axes inside its bounds.
#[test]
fn trade_plan_grid_scrolls_both_axes_inside_its_bounds() {
    use eframe::egui::{Event, Modifiers, MouseWheelUnit, vec2};
    let mut h = scrolling_trade_plans_harness();
    let bounds = h.state().view.controls["condor-plans"];
    assert!(h.query_by_label("plan-row-000").is_some());
    let header_x = h
        .get_by_label("ActionDateTime")
        .accesskit_node()
        .bounding_box()
        .unwrap()
        .x0;
    h.input_mut().events.extend([
        Event::PointerMoved(bounds.center()),
        Event::MouseWheel {
            unit: MouseWheelUnit::Point,
            delta: vec2(0., -300.),
            modifiers: Modifiers::NONE,
        },
    ]);
    h.run_steps(8);
    assert!(
        h.query_by_label("plan-row-000").is_none(),
        "Vertical wheel must advance the plan rows"
    );
    h.input_mut().events.push(Event::MouseWheel {
        unit: MouseWheelUnit::Point,
        delta: vec2(-600., 0.),
        modifiers: Modifiers::NONE,
    });
    h.run_steps(8);
    let shifted = h
        .query_by_label("ActionDateTime")
        .map(|node| node.accesskit_node().bounding_box().unwrap().x0);
    assert!(
        shifted.is_none_or(|x| x < header_x - 100.),
        "Horizontal wheel must move the columns: {shifted:?}"
    );
}


/// Verifies that trade plan scrollbars are clickable inside the grid.
#[test]
fn trade_plan_scrollbars_are_clickable_inside_the_grid() {
    use eframe::egui::{Event, Modifiers, PointerButton, pos2};
    let mut h = scrolling_trade_plans_harness();
    let bounds = h.state().view.controls["condor-plans"];
    if let Ok(path) = std::env::var("IFM_CONDOR_SCROLL_SCREENSHOT") {
        h.render().unwrap().save(path).unwrap();
    }
    for (axis, point) in [
        pos2(bounds.right() - 6., bounds.center().y),
        pos2(bounds.center().x, bounds.bottom() - 6.),
    ]
    .into_iter()
    .enumerate()
    {
        h.input_mut().events.push(Event::PointerMoved(point));
        h.run_steps(2);
        h.input_mut().events.push(Event::PointerButton {
            pos: point,
            button: PointerButton::Primary,
            pressed: true,
            modifiers: Modifiers::NONE,
        });
        h.run_steps(2);
        let dragged = point
            + if axis == 0 {
                eframe::egui::vec2(0., 40.)
            } else {
                eframe::egui::vec2(80., 0.)
            };
        h.input_mut().events.push(Event::PointerMoved(dragged));
        h.run_steps(2);
        h.input_mut().events.push(Event::PointerButton {
            pos: dragged,
            button: PointerButton::Primary,
            pressed: false,
            modifiers: Modifiers::NONE,
        });
        h.run_steps(8);
        if axis == 0 {
            assert!(
                h.query_by_label("plan-row-000").is_none(),
                "Vertical scrollbar must advance the rows before horizontal scrolling"
            );
        }
    }
    let header = h
        .query_by_label("ActionDateTime")
        .map(|node| node.accesskit_node().bounding_box().unwrap().x0);
    assert!(
        header.is_none_or(|x| x < bounds.left() as f64 - 100.),
        "Horizontal scrollbar must move the columns: {header:?}"
    );
}


/// Verifies that center tabs switch and close only the requested trade.
#[test]
fn center_tabs_switch_and_close_only_the_requested_trade() {
    let mut h = loaded_tabs_harness();
    for label in ["27:1", "28:2", "Close trade 27:1", "Close trade 28:2"] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
    let controls = &h.state().view.controls;
    let tab = controls["trade-tab:101.701.27.1"];
    let close = controls["close-trade:101.701.27.1"];
    assert!(close.left() > tab.center().x && close.right() <= tab.right());
    assert!(h.query_by_label("Close Trade").is_none());
    h.get_by_label("28:2").click();
    h.run();
    h.run();
    assert!(h.state().requests.iter().any(|r|r["intent"]=="activate-trade" && r["fields"]["tradeTabId"]=="101.701.28.2"));
    h.get_by_label("Close trade 27:1").click();
    h.run();
    h.run();
    assert!(h.query_by_label("27:1").is_none());
    assert!(h.query_by_label("28:2").is_some());
    assert!(h.state().view.loaded_bounds.is_some());
    h.get_by_label("Close trade 28:2").click();
    h.run();
    h.run();
    assert!(h.state().view.loaded_bounds.is_none());
    assert!(
        h.state()
            .requests
            .iter()
            .filter(|r| r["intent"] == "close-trade")
            .count()
            == 2
    );
}


/// Verifies that a failed additional load keeps existing center tabs open.
#[test]
fn a_failed_additional_load_keeps_existing_center_tabs_open() {
    let mut h = loaded_tabs_harness();
    h.state_mut().fail_load = true;
    h.state_mut().view.open = true;
    h.run();
    h.get_by_label("Load Trade").click();
    h.run();
    h.run();
    assert!(h.state().view.open);
    assert_eq!(h.state().snapshot.outcomes[0].message, "Persisted trade limits were not found.");
    assert!(h.query_by_label("28:2").is_some());
    assert!(h.state().view.loaded_bounds.is_some());
}


/// Verifies that live plan publications update the grid without a user refresh.
#[test]
fn live_plan_publications_update_the_grid_without_a_user_refresh() {
    let mut h = loaded_tabs_harness();
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("live".into(), "True".into());
    let requests_before = h.state().requests.len();
    for revision in [1, 2] {
        let value = serde_json::json!({"planRevision":revision,"valueDate":"2026-10-16","ironCondorTradePlanSnapshot":{"actionDateTime":"2026-10-16T15:00:00Z","sequenceId":revision,"dailyPnl":format!("{}",revision as f64*1.25),"calculationStatus":"Complete","isComplete":true,"actionType":"Monitor","actionReason":format!("Incoming live plan {revision}")}});
        let row=serde_json::from_value(serde_json::json!({"id":format!("plan:{revision}"),"fields":{"kind":"plan","data":value.to_string(),"actionDateTimeEastern":format!("2026-10-16 11:00:0{revision}.123")}})).unwrap();
        h.state_mut().snapshot.result.rows.insert(0, row);
        h.state_mut().snapshot.generation += 1;
        h.run();
        assert!(
            h.query_all_by_label(&format!("Incoming live plan {revision}"))
                .next()
                .is_some()
        );
        assert!(
            h.query_all_by_label(&format!("2026-10-16 11:00:0{revision}.123"))
                .next()
                .is_some()
        );
    }
    assert!(
        h.query_all_by_label("Incoming live plan 1")
            .next()
            .is_some()
    );
    assert!(
        h.query_all_by_label("Incoming live plan 2")
            .next()
            .is_some()
    );
    assert_eq!(h.state().requests.len(), requests_before);
}


/// Verifies that load trade transfer failure remains visible after editor closes.
#[test]
fn load_trade_transfer_failure_remains_visible_after_editor_closes() {
    let mut view = TradeWorkspace::default();
    view.request("load-trade", &snapshot());
    view.transport_failed("Trade history transfer failed");
    assert!(view.is_visible());
    let mut h = Harness::new_state(
        |ctx, (view, snapshot): &mut (TradeWorkspace, ContractPublication)| {
            view.draw(ctx, snapshot);
        },
        (view, snapshot()),
    );
    h.run();
    assert!(
        h.query_all_by_label("Trade history transfer failed")
            .next()
            .is_some()
    );
}


/// Verifies that virtual plan grid requests only visible rows after trade is loaded.
#[test]
fn virtual_plan_grid_requests_only_visible_rows_after_trade_is_loaded() {
    let mut publication = snapshot();
    publication
        .result
        .context
        .insert("activeTradeTab".into(), "101.701.1901.1501".into());
    publication
        .result
        .context
        .insert("planOffset".into(), "0".into());
    publication
        .result
        .context
        .insert("planTotal".into(), "10000".into());
    publication
        .result
        .context
        .insert("planDate".into(), "2026-10-16".into());
    publication.result.rows.push(
        serde_json::from_value(
            serde_json::json!({"id":"established:1", "fields":{"kind":"established"}, "data":null}),
        )
        .unwrap(),
    );
    publication.result.rows.last_mut().unwrap().data = Some(ifm_ui_egui::wire::encode_value(&serde_json::json!({"id":{"portfolioId":101,"fundId":701,"orderId":1901,"tradeId":1501},"legs":[]})).unwrap().into());
    let mut view = TradeWorkspace::default();
    let id = view.request("load-trade", &publication);
    let request: serde_json::Value = serde_json::from_str(&id).unwrap();
    publication.outcomes.push(serde_json::from_value(serde_json::json!({"requestId":request["requestId"],"state":"Completed","message":"Loaded"})).unwrap());
    let mut h = Harness::new_state(
        |ctx, state: &mut (TradeWorkspace, ContractPublication, Vec<serde_json::Value>)| {
            if let Some(request) = state.0.draw(ctx, &state.1) {
                state.2.push(serde_json::from_str(&request).unwrap());
            }
        },
        (view, publication, Vec::new()),
    );
    h.run();
    let page = h
        .state()
        .2
        .iter()
        .find(|request| request["intent"] == "plans-page")
        .expect("visible page requested");
    let size: usize = page["fields"]["pageSize"]
        .as_str()
        .unwrap()
        .parse()
        .unwrap();
    assert!(size > 0 && size <= 100);
    assert_eq!(page["fields"]["offset"], "0");
    let page_id = page["requestId"].clone();
    for index in 0..size {
        let mut row: ifm_ui_egui::contract_editor::ContractRow = serde_json::from_value(
            serde_json::json!({"id":format!("plan:{index}"),"fields":{"kind":"plan"}}),
        )
        .unwrap();
        row.data=Some(ifm_ui_egui::wire::encode_value(&serde_json::json!({"ironCondorTradePlanSnapshot":{"isComplete":true,"sequenceId":10000-index}})).unwrap().into());
        h.state_mut().1.result.rows.push(row);
    }
    h.state_mut().1.generation += 1;
    h.state_mut().1.outcomes.push(
        serde_json::from_value(
            serde_json::json!({"requestId":page_id,"state":"Completed","message":"Page loaded"}),
        )
        .unwrap(),
    );
    h.run();
    let bounds = h.state().0.controls["condor-plans"];
    h.input_mut().events.extend([
        eframe::egui::Event::PointerMoved(bounds.center()),
        eframe::egui::Event::MouseWheel {
            unit: eframe::egui::MouseWheelUnit::Point,
            delta: eframe::egui::vec2(0., -3000.),
            modifiers: eframe::egui::Modifiers::NONE,
        },
    ]);
    h.run_steps(8);
    assert!(
        h.state()
            .2
            .iter()
            .any(|request| request["intent"] == "plans-page"
                && request["fields"]["offset"]
                    .as_str()
                    .and_then(|v| v.parse::<usize>().ok())
                    .is_some_and(|offset| offset > 0)),
        "scrolling must query a new offset"
    );
}


/// Verifies that spread chart shows price limits and forward spread without forward limit.
#[test]
fn spread_chart_shows_price_limits_and_forward_spread_without_forward_limit() {
    let mut h = loaded_tabs_harness();
    for name in ["LossLimit", "WinLimit", "ForwardSpread", "NetSpread"] {
        assert!(
            h.query_all_by_label(name).next().is_some(),
            "Missing chart legend {name}"
        );
    }
    assert!(h.query_all_by_label("ForwardSpreadLimit").next().is_none());
}


/// The painted chart keeps future maturity dates visible with sparse EOD/intraday observations.
#[test]
fn spread_chart_paints_daily_lifetime_axis_through_unobserved_maturity() {
    let mut h = loaded_tabs_harness();
    let mut row: ifm_ui_egui::contract_editor::ContractRow = serde_json::from_value(
        serde_json::json!({"id":"chart:0","fields":{"kind":"chart"}})).unwrap();
    row.data = Some(ifm_ui_egui::wire::encode_value(&serde_json::json!({
        "chartStartUtc":"2026-10-18T22:00:00Z", "chartEndUtc":"2026-10-23T21:00:00Z",
        "points":[
            {"atUtc":"2026-10-19T21:00:00Z","netSpread":"-14"},
            {"atUtc":"2026-10-20T14:00:00Z","netSpread":"-12"},
            {"atUtc":"2026-10-20T14:15:00Z","netSpread":"-11"},
            {"atUtc":"2026-10-20T14:45:00Z","netSpread":"-10"}
        ]})).unwrap().into());
    h.state_mut().snapshot.result.rows.push(row);
    h.state_mut().snapshot.generation += 1;
    h.state_mut().snapshot.invalidate_render_cache();
    h.set_size(eframe::egui::vec2(1800., 1000.));
    h.run_steps(3);
    let labels = text_baselines(&h.output().shapes, h.ctx.screen_rect());
    for date in ["Oct-19", "Oct-20", "Oct-21", "Oct-22", "Oct-23"] {
        assert!(labels.iter().any(|(text, _)| text == date), "Missing daily chart tick {date}");
    }
    fn check_dates(shape: &egui::Shape, count: &mut usize) {
        match shape {
            egui::Shape::Vec(shapes) => {
                for shape in shapes { check_dates(shape, count); }
            }
            egui::Shape::Text(text) if text.galley.text().starts_with("Oct-") => {
                assert_eq!(text.angle, -std::f32::consts::FRAC_PI_2);
                *count += 1;
            }
            _ => {}
        }
    }
    let mut vertical_dates = 0;
    for shape in &h.output().shapes { check_dates(&shape.shape, &mut vertical_dates); }
    assert!(vertical_dates >= 5);
    if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
        h.render().unwrap().save(std::path::Path::new(&directory).join("condor-grid-dates.png")).unwrap();
    }
}


/// Verifies that live plan grid never requests database pages and stopped grid does.
#[test]
fn live_plan_grid_never_requests_database_pages_and_stopped_grid_does() {
    let mut publication = snapshot();
    for (key, value) in [
        ("activeTradeTab", "101.701.1901.1501"),
        ("planOffset", "0"),
        ("planTotal", "10000"),
        ("planDate", "2026-10-19"),
        ("latestPlanRevision", "101"),
        ("planMaximumRevision", "100"),
        ("live", "True"),
        ("monitorLive", "True"),
    ] {
        publication.result.context.insert(key.into(), value.into());
    }
    let mut row: ifm_ui_egui::contract_editor::ContractRow =
        serde_json::from_value(serde_json::json!({
        "id":"established:1","fields":{"kind":"established"}}))
        .unwrap();
    row.data = Some(
        ifm_ui_egui::wire::encode_value(&serde_json::json!({
        "id":{"portfolioId":101,"fundId":701,"orderId":1901,"tradeId":1501},"legs":[]}))
        .unwrap()
        .into(),
    );
    publication.result.rows.push(row);
    let mut view = TradeWorkspace::default();
    let request: serde_json::Value =
        serde_json::from_str(&view.request("load-trade", &publication)).unwrap();
    publication.outcomes.push(
        serde_json::from_value(serde_json::json!({
        "requestId":request["requestId"],"state":"Completed","message":"Loaded"}))
        .unwrap(),
    );
    let mut h = Harness::new_state(
        |ctx, state: &mut (TradeWorkspace, ContractPublication, Vec<serde_json::Value>)| {
            if let Some(request) = state.0.draw(ctx, &state.1) {
                state.2.push(serde_json::from_str(&request).unwrap());
            }
        },
        (view, publication, Vec::new()),
    );
    h.run_steps(3);
    assert!(
        !h.state()
            .2
            .iter()
            .any(|request| request["intent"] == "plans-page")
    );
    h.state_mut()
        .1
        .result
        .context
        .insert("live".into(), "False".into());
    h.state_mut()
        .1
        .result
        .context
        .insert("monitorLive".into(), "False".into());
    h.state_mut().1.generation += 1;
    h.run();
    assert!(
        h.state()
            .2
            .iter()
            .any(|request| request["intent"] == "plans-page")
    );
}


/// Verifies that emulator liquidity can be reloaded when order entry and market data are
/// unavailable.
#[test]
fn emulator_liquidity_can_be_reloaded_when_order_entry_and_market_data_are_unavailable() {
    let mut h = harness();
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("editable".into(), "False".into());
    h.state_mut()
        .snapshot
        .result
        .rows
        .retain(|row| row.id != "market:0");
    h.state_mut().snapshot.invalidate_render_cache();
    h.run();
    h.get_by_label("liquiditySelector: Emulator").click();
    h.run();
    h.query_all_by_label("Emulator").last().unwrap().click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|request| request["intent"] == "chain-source"
                && request["fields"]["liquiditySelector"] == "Emulator"
                && request["fields"]["live"] == "false")
    );
}


/// Verifies that liquidity source selection requests live quotes explicitly.
#[test]
fn liquidity_source_selection_requests_live_quotes_explicitly() {
    let mut h = harness();
    h.get_by_label("liquiditySelector: Emulator").click();
    h.run();
    h.get_by_label("Live").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|request| request["intent"] == "chain-source"
                && request["fields"]["liquiditySelector"] == "Live"
                && request["fields"]["live"] == "true")
    );
}


/// Verifies that closing blotter preserves close action and locks the inherited expiry.
#[test]
fn closing_blotter_preserves_close_action_and_locks_the_inherited_expiry() {
    let mut h = harness();
    let context = &mut h.state_mut().snapshot.result.context;
    context.insert("executionAction".into(), "Close".into());
    context.insert("closingTrade".into(), "True".into());
    context.insert("composerSessionId".into(), "closing-session".into());
    h.run();
    assert!(h.query_by_label("parentOrderAction: Close").is_some());
    let mut snapshot = snapshot();
    snapshot.result.context = h.state().snapshot.result.context.clone();
    let request: serde_json::Value =
        serde_json::from_str(&h.state_mut().view.request("preview-draft", &snapshot)).unwrap();
    assert_eq!(request["fields"]["action"], "Close");
    assert_eq!(request["fields"]["parentOrderAction"], "Close");
    assert_eq!(request["fields"]["expiry"], "2026-11-20");
}
