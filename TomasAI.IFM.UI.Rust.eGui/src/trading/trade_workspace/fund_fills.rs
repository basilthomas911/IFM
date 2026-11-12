use super::{TradeWorkspace, flag, order_layout::at};
use crate::{
    contract_editor::ContractPublication,
    control_parity::{BorderedScrollArea as _, BorderedTable as _, FilledGrid as _},
    models::cells::Cells,
    state::trades::fund_fills::{Order, Snapshot},
};
use eframe::egui::{self, Color32, RichText};

fn timestamp(value: &str) -> String {
    if value.is_empty() {
        return "—".into();
    }
    chrono::DateTime::parse_from_rfc3339(value)
        .map(|time| {
            time.with_timezone(&chrono_tz::America::New_York)
                .format("%Y-%b-%d %H:%M:%S %Z")
                .to_string()
        })
        .unwrap_or_else(|_| value.into())
}

fn status(order: &Order) -> (&str, Color32) {
    match order.status.as_str() {
        "Filled" => ("Order Filled", Color32::from_rgb(112, 222, 148)),
        "Rejected" | "Failed" | "Expired" => (
            if order.rejection.is_empty() {
                &order.status
            } else {
                &order.rejection
            },
            Color32::from_rgb(255, 136, 136),
        ),
        "PartiallyFilled" => ("Partially Filled", Color32::from_rgb(242, 204, 97)),
        "Pending" | "Submitted" | "Executing" => ("Order Filling", Color32::from_rgb(242, 204, 97)),
        _ => (&order.status, crate::presentation::BORDER),
    }
}

impl TradeWorkspace {
    /// Draw the shared Fund order tree, selected fill grid and broker controls.
    pub(super) fn draw_order_fills(
        &mut self,
        ui: &mut egui::Ui,
        publication: &ContractPublication,
        view: &str,
    ) -> Option<&'static str> {
        let snapshot = publication
            .result
            .document::<Snapshot>(&format!("{view}-fund-fills"));
        let bounds = ui.available_rect_before_wrap();
        let scale = ui.ctx().pixels_per_point();
        let mut panes = bounds;
        panes.max.y = (panes.bottom() - 40. / scale).max(panes.top());
        let key = format!("{view}FillsSplit");
        let fraction = self
            .draft
            .get(&key)
            .and_then(|value| value.parse::<f32>().ok())
            .unwrap_or(0.5);
        let gap = 8. / scale;
        let width = (panes.width() - gap).max(1.);
        let left =
            egui::Rect::from_min_size(panes.min, egui::vec2(width * fraction, panes.height()));
        let right =
            egui::Rect::from_min_max(egui::pos2(left.right() + gap, panes.top()), panes.max);
        let splitter = egui::Rect::from_min_max(
            egui::pos2(left.right(), panes.top()),
            egui::pos2(right.left(), panes.bottom()),
        );
        let handle = ui
            .interact(
                splitter,
                ui.id().with((view, "fills-splitter")),
                egui::Sense::drag(),
            )
            .on_hover_cursor(egui::CursorIcon::ResizeHorizontal);
        if handle.dragged()
            && let Some(pointer) = handle.interact_pointer_pos()
        {
            let next = ((pointer.x - panes.left() - gap / 2.) / width).clamp(0.25, 0.75);
            self.draft.insert(key, next.to_string());
        }
        ui.painter().rect_filled(
            egui::Rect::from_center_size(splitter.center(), egui::vec2(2. / scale, 30. / scale)),
            0.,
            crate::presentation::BORDER,
        );
        let mut action = None;
        let selected = snapshot.as_ref().and_then(|data| {
            data.selected.as_ref().and_then(|key| {
                data.orders
                    .iter()
                    .find(|row| row.order_id == key.0 && row.attempt == key.1)
            })
        });
        at(ui, left.shrink(1. / scale), "fills-tree", |ui| {
            ui.set_clip_rect(ui.clip_rect().intersect(left));
            ui.label(
                RichText::new(match snapshot.as_ref() {
                    Some(data) => format!("Orders   ·   Value Date {}", data.date),
                    None => "Orders".into(),
                })
                .strong(),
            );
            ui.separator();
            egui::ScrollArea::vertical()
                .id_salt((view, "orders"))
                .max_height(ui.available_height())
                .bordered_show(ui, |ui| {
                    let Some(data) = snapshot.as_ref() else {
                        ui.label("Order fill history is unavailable.");
                        return;
                    };
                    if data.orders.is_empty() {
                        ui.label("No orders for this Fund and Value Date.");
                    }
                    for order in &data.orders {
                        let active = data
                            .selected
                            .as_ref()
                            .is_some_and(|key| key.0 == order.order_id && key.1 == order.attempt);
                        let (caption, color) = status(order);
                        let trade = quantity(order.trade_id);
                        let title = format!(
                            "{}:{trade} {} {}",
                            order.order_id,
                            timestamp(&order.order_time),
                            order.composition
                        )
                        .trim_end()
                        .to_owned();
                        egui::collapsing_header::CollapsingState::load_with_default_open(
                            ui.ctx(),
                            ui.make_persistent_id((view, order.order_id, &order.attempt)),
                            true,
                        )
                        .show_header(ui, |ui| {
                            ui.label(RichText::new("●").color(color));
                            if ui
                                .add_enabled(
                                    self.interaction_ready(publication),
                                    egui::Button::selectable(active, &title).wrap(),
                                )
                                .on_hover_text(title)
                                .clicked()
                                && !active
                            {
                                self.draft.insert("fillsView".into(), view.into());
                                self.draft
                                    .insert("fillsOrderId".into(), order.order_id.to_string());
                                self.draft
                                    .insert("fillsAttempt".into(), order.attempt.clone());
                                action = Some("select-fill-order");
                            }
                        })
                        .body(|ui| {
                            ui.label(RichText::new(format!("Status   {caption}")).color(color));
                            ui.label(format!(
                                "Order Quantity   {}",
                                quantity(order.order_quantity)
                            ));
                            ui.label(format!(
                                "Filled Quantity   {}",
                                quantity(order.filled_quantity)
                            ));
                            ui.label(format!("Fill DateTime   {}", timestamp(&order.last_fill)));
                        });
                    }
                });
        });
        at(ui, right.shrink(1. / scale), "fills-grid", |ui| {
            ui.set_clip_rect(ui.clip_rect().intersect(right));
            draw_grid(ui, view, selected);
        });
        for pane in [left, right] {
            ui.painter().rect_stroke(
                pane,
                0.,
                egui::Stroke::new(1. / scale, crate::presentation::BORDER),
                egui::StrokeKind::Inside,
            );
        }
        let footer = egui::Rect::from_min_max(
            egui::pos2(bounds.left(), panes.bottom() + 5. / scale),
            bounds.max,
        );
        at(ui, footer, "fills-actions", |ui| {
            action = self
                .draw_fill_controls(ui, publication, view, snapshot.as_ref())
                .or(action);
        });
        action
    }

    fn draw_fill_controls(
        &mut self,
        ui: &mut egui::Ui,
        publication: &ContractPublication,
        view: &str,
        snapshot: Option<&Snapshot>,
    ) -> Option<&'static str> {
        let mut action = None;
        let controls = snapshot
            .map(|data| data.controls.as_slice())
            .unwrap_or_default();
        let component_key = format!("{view}FillBroker");
        let mut component = self
            .draft
            .get(&component_key)
            .and_then(|id| controls.iter().position(|item| item.id == *id))
            .unwrap_or(0);
        ui.horizontal_wrapped(|ui| {
            let ready = self.interaction_ready(publication);
            if ui
                .add_enabled(
                    ready && controls.get(component).is_some(),
                    egui::Button::new("Reconcile"),
                )
                .clicked()
            {
                action = Some("reconcile-broker-order");
            }
            if controls.len() > 1 {
                egui::ComboBox::from_id_salt((view, "broker-component"))
                    .selected_text(&controls[component].caption)
                    .show_ui(ui, |ui| {
                        for (index, item) in controls.iter().enumerate() {
                            ui.selectable_value(&mut component, index, &item.caption);
                        }
                    });
            }
            let control = controls.get(component);
            if let Some(control) = control {
                let previous = self.draft.insert(component_key, control.id.clone());
                let price_key = format!("{view}FillPublishedPrice");
                if previous.as_ref() != Some(&control.id)
                    || self.draft.get(&price_key) != Some(&control.price)
                {
                    self.draft.insert(price_key, control.price.clone());
                    self.draft
                        .insert(format!("{view}FillPrice"), control.price.clone());
                }
            }
            if view == "blotter"
                && flag(publication, "submissionUnconfirmed")
                && ui
                    .add_enabled(ready, egui::Button::new("Recover Submission"))
                    .clicked()
            {
                action = Some("recover-submission");
            }
            let enabled = ready && control.is_some_and(|item| item.mutation.can_mutate);
            let price = format!("{view}FillPrice");
            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                if ui
                    .add_enabled(enabled, egui::Button::new("Cancel Unfilled"))
                    .clicked()
                {
                    action = Some("cancel-unfilled");
                }
                ui.allocate_ui_with_layout(
                    egui::vec2(85., ui.spacing().interact_size.y),
                    egui::Layout::top_down(egui::Align::Center),
                    |ui| {
                        ui.add_enabled_ui(enabled, |ui| {
                            let mutation = control.map(|item| &item.mutation);
                            let number = |key, default| {
                                mutation
                                    .and_then(|value| value.cell(key).number())
                                    .unwrap_or(default)
                            };
                            let tick = number("tick", 0.05);
                            let decimals = (0..=8)
                                .find(|n| {
                                    let scaled = tick * 10_f64.powi(*n as i32);
                                    (scaled - scaled.round()).abs() < 1e-8
                                })
                                .unwrap_or(8);
                            self.numeric(
                                ui,
                                &price,
                                "0",
                                number("minimum", -1_000_000.),
                                number("maximum", 1_000_000.),
                                tick,
                                decimals,
                            );
                        })
                    },
                );
                if ui
                    .add_enabled(enabled, egui::Button::new("Update Order Price"))
                    .clicked()
                {
                    action = Some("update-limit");
                }
            });
            if let Some(control) = control
                && matches!(
                    action,
                    Some("reconcile-broker-order" | "update-limit" | "cancel-unfilled")
                )
            {
                self.draft.insert("fillsView".into(), view.into());
                self.draft.insert(
                    "fillsOrderId".into(),
                    snapshot
                        .and_then(|data| data.selected.as_ref())
                        .map(|key| key.0.to_string())
                        .unwrap_or_default(),
                );
                self.draft.insert(
                    "fillsAttempt".into(),
                    snapshot
                        .and_then(|data| data.selected.as_ref())
                        .map(|key| key.1.clone())
                        .unwrap_or_default(),
                );
                self.draft
                    .insert("brokerOrderId".into(), control.id.clone());
                self.draft.insert(
                    "limitPrice".into(),
                    self.draft.get(&price).cloned().unwrap_or_default(),
                );
            }
        });
        action
    }
}

fn quantity(value: Option<impl ToString>) -> String {
    value.map_or_else(|| "—".into(), |value| value.to_string())
}

fn draw_grid(ui: &mut egui::Ui, view: &str, order: Option<&Order>) {
    ui.horizontal_wrapped(|ui| {
        ui.label(
            RichText::new(match order {
                Some(row) => format!(
                    "Fill Details   Order {}   Trade {}",
                    row.order_id,
                    quantity(row.trade_id)
                ),
                None => "Fill Details".into(),
            })
            .strong(),
        );
        if let Some(row) = order {
            ui.label(format!("Last Fill Date Time {}", timestamp(&row.last_fill)));
        }
    });
    let fills = order.map(|row| row.fills.as_slice()).unwrap_or_default();
    let spacing = ui.spacing().item_spacing;
    let body_height =
        (ui.available_height() - ui.spacing().scroll.allocated_width() - 24. - spacing.y).max(0.);
    let widths = fill_column_widths(
        ui.available_width(),
        spacing.x,
        ui.spacing().scroll.allocated_width(),
    );
    egui::ScrollArea::horizontal()
        .id_salt((view, "fill-columns"))
        .scroll_bar_visibility(egui::scroll_area::ScrollBarVisibility::AlwaysVisible)
        .auto_shrink([false, false])
        .bordered_show(ui, |ui| {
            use egui_extras::{Column, TableBuilder};
            let mut table = TableBuilder::new(ui)
                .id_salt((view, "fill-rows"))
                .fill_grid(fills.len(), 24., spacing.y, body_height)
                .striped(true)
                .resizable(true)
                .cell_layout(egui::Layout::left_to_right(egui::Align::Center));
            for (width, minimum) in widths.into_iter().zip(FILL_MINIMUM_WIDTHS) {
                table = table.column(Column::initial(width).at_least(minimum).clip(true));
            }
            table
                .header(24., |mut header| {
                    for label in [
                        "Contract",
                        "Side",
                        "Qty",
                        "Price",
                        "Commission",
                        "Fill DateTime",
                    ] {
                        header.col(|ui| {
                            let rect = ui.max_rect().expand2(egui::vec2(spacing.x / 2., 0.));
                            ui.painter().rect(
                                rect,
                                0.,
                                crate::presentation::COMMAND,
                                egui::Stroke::new(1.0_f32, crate::presentation::BORDER),
                                egui::StrokeKind::Inside,
                            );
                            ui.add(
                                crate::presentation::label_widget(
                                    RichText::new(label)
                                        .color(crate::presentation::CAPTION_FOREGROUND),
                                )
                                .truncate(),
                            );
                        });
                    }
                })
                .bordered_body(24., |mut body| {
                    if fills.is_empty() {
                        body.ui_mut().label("No fills recorded");
                    }
                    body.rows(24., fills.len(), |mut row| {
                        let fill = &fills[row.index()];
                        let side = if fill.quantity < 0 { "Sell" } else { "Buy" };
                        for (index, cell) in [
                            &fill.contract,
                            side,
                            &fill.quantity.unsigned_abs().to_string(),
                            &fill.price,
                            &fill.commission,
                            &timestamp(&fill.time),
                        ]
                        .into_iter()
                        .enumerate()
                        {
                            row.col(|ui| {
                                let label =
                                    ui.add(crate::presentation::label_widget(cell).truncate());
                                if index == 0 {
                                    label.on_hover_text(format!("{cell}\nFill ID {}", fill.id));
                                } else {
                                    label.on_hover_text(cell);
                                }
                            });
                        }
                    });
                });
        });
}

const FILL_MINIMUM_WIDTHS: [f32; 6] = [105., 35., 29., 48., 80., 160.];

fn fill_column_widths(available: f32, gap: f32, scrollbar: f32) -> [f32; 6] {
    let mut widths = FILL_MINIMUM_WIDTHS;
    let available = available - gap * (widths.len() - 1) as f32 - scrollbar;
    let spare = (available - widths.iter().sum::<f32>()).max(0.);
    widths[0] += spare / 2.;
    widths[5] += spare / 2.;
    widths
}
#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        backend::{
            schema::Registry,
            transport::{Backend, Settings},
        },
        state::trades::TradeController,
        trade_workspace::TradeWorkspace,
    };
    use egui_kittest::{Harness, kittest::Queryable};
    use serde_json::{Value, json};
    use std::{collections::BTreeSet, time::Duration};

    #[test]
    fn timestamps_follow_new_york_standard_and_daylight_time() {
        assert_eq!(
            timestamp("2026-11-12T14:14:22Z"),
            "2026-Nov-12 09:14:22 EST"
        );
        assert_eq!(
            timestamp("2026-07-12T13:14:22Z"),
            "2026-Jul-12 09:14:22 EDT"
        );
    }

    #[test]
    fn fill_columns_share_available_width_and_keep_minimums_when_narrow() {
        assert_eq!(fill_column_widths(300., 4., 12.), FILL_MINIMUM_WIDTHS);
        let widths = fill_column_widths(600., 4., 12.);
        assert_eq!(widths.iter().sum::<f32>() + 20. + 12., 600.);
        assert!(widths[0] > FILL_MINIMUM_WIDTHS[0] && widths[5] > FILL_MINIMUM_WIDTHS[5]);
    }

    #[test]
    fn status_color_and_rejection_reason_are_independent_of_selection() {
        let mut order = Order {
            order_id: 4201,
            trade_id: Some(4301),
            attempt: String::new(),
            status: "Filled".into(),
            order_time: String::new(),
            composition: String::new(),
            order_quantity: Some(2),
            filled_quantity: Some(1),
            last_fill: String::new(),
            rejection: String::new(),
            fills: Vec::new(),
        };
        assert_eq!(
            status(&order),
            ("Order Filled", Color32::from_rgb(112, 222, 148))
        );
        order.status = "PartiallyFilled".into();
        assert_eq!(
            status(&order),
            ("Partially Filled", Color32::from_rgb(242, 204, 97))
        );
        order.status = "Rejected".into();
        order.rejection = "Approval expired".into();
        assert_eq!(
            status(&order),
            ("Approval expired", Color32::from_rgb(255, 136, 136))
        );
    }

    #[test]
    fn clicking_another_order_requests_its_exact_attempt() {
        let mut row = Order {
            order_id: 4201,
            trade_id: Some(4301),
            attempt: uuid::Uuid::from_u128(1).to_string(),
            status: "Filled".into(),
            order_time: String::new(),
            composition: String::new(),
            order_quantity: Some(1),
            filled_quantity: Some(1),
            last_fill: String::new(),
            rejection: String::new(),
            fills: Vec::new(),
        };
        let selected = Some((row.order_id, row.attempt.clone()));
        let first = row.clone();
        row.order_id = 4202;
        row.trade_id = Some(4302);
        row.attempt = uuid::Uuid::from_u128(2).to_string();
        row.status = "Rejected".into();
        row.rejection = "Approval expired".into();
        let snapshot: Snapshot = serde_json::from_value(json!({
            "date": "2026-11-12", "orders": [first, row], "selected": selected, "controls": []
        }))
        .unwrap();
        let mut publication = ContractPublication::default();
        publication.result.documents.insert(
            "blotter-fund-fills".into(),
            rmp_serde::to_vec(&snapshot).unwrap().into(),
        );
        let mut harness = Harness::new_state(
            move |ctx, (workspace, intent): &mut (TradeWorkspace, Option<String>)| {
                egui::CentralPanel::default().show(ctx, |ui| {
                    if let Some(action) = workspace.draw_order_fills(ui, &publication, "blotter") {
                        *intent = Some(workspace.request(action, &publication));
                    }
                });
            },
            (TradeWorkspace::default(), None),
        );
        harness.set_size(egui::vec2(1000., 600.));
        harness.run();
        assert!(harness.query_by_label("Status   Order Filled").is_some());
        assert!(
            harness
                .query_by_label("Status   Approval expired")
                .is_some()
        );
        harness.get_by_label("4202:4302 —").click();
        harness.run();
        let intent: Value = serde_json::from_str(harness.state().1.as_deref().unwrap()).unwrap();
        assert_eq!(intent["intent"], "select-fill-order");
        assert_eq!(intent["fields"]["fillsOrderId"], "4202");
        assert_eq!(
            intent["fields"]["fillsAttempt"],
            uuid::Uuid::from_u128(2).to_string()
        );
    }

    async fn act(
        controller: &mut TradeController,
        backend: &Backend,
        registry: &Registry,
        generation: &mut u64,
        intent: &str,
        fields: Value,
        order: Option<&str>,
    ) -> ContractPublication {
        let publication = controller.execute(backend, registry, &json!({
            "schema": 1, "view": "trade-orders", "requestId": uuid::Uuid::new_v4().to_string(),
            "expectedGeneration": *generation, "intent": intent, "fields": fields, "originalId": order,
        })).await.unwrap();
        assert!(
            publication
                .outcomes
                .iter()
                .all(|outcome| outcome.state == "Completed"),
            "{intent}: {:?}",
            publication
                .outcomes
                .iter()
                .map(|outcome| &outcome.message)
                .collect::<Vec<_>>()
        );
        *generation = publication.generation;
        publication
    }

    /// Use the existing committed emulator trade and the same controller and renderer as the desktop.
    #[tokio::test]
    #[ignore = "Read-only test of existing Development trade 4301; requires IFM_VERIFY_NATS_URL"]
    async fn development_trade_4301_renders_four_existing_fills_in_both_views() {
        let backend = Backend::connect(Settings {
            url: std::env::var("IFM_VERIFY_NATS_URL").unwrap(),
            credentials_file: None,
            token: None,
            request_timeout: Duration::from_secs(30),
        })
        .await
        .unwrap();
        let registry = Registry::compiled().unwrap();
        let mut controller = TradeController::default();
        let mut generation = 0;
        act(
            &mut controller,
            &backend,
            &registry,
            &mut generation,
            "load",
            json!({}),
            None,
        )
        .await;
        let index = |rows: &[rmpv::Value], kind: &str, key: &str, id: i64| {
            rows.iter()
                .position(|row| {
                    registry
                        .project(&crate::services::trade_orders::kind(kind), row)
                        .unwrap()[key]
                        .as_i64()
                        == Some(id)
                })
                .unwrap()
                .to_string()
        };
        let portfolio = index(
            &controller.data.portfolios,
            "PortfolioReadModel",
            "PortfolioId",
            101,
        );
        act(
            &mut controller,
            &backend,
            &registry,
            &mut generation,
            "portfolio",
            json!({"index": portfolio}),
            None,
        )
        .await;
        let fund = index(
            &controller.data.funds,
            "FundMandateReadModel",
            "FundId",
            703,
        );
        act(
            &mut controller,
            &backend,
            &registry,
            &mut generation,
            "fund",
            json!({"index": fund}),
            None,
        )
        .await;
        let order = Some("4001");
        act(
            &mut controller,
            &backend,
            &registry,
            &mut generation,
            "order",
            json!({}),
            order,
        )
        .await;
        let trade = index(
            &controller.data.trades,
            "FundOrderTradeProjectionReadModel",
            "TradeId",
            4301,
        );
        act(
            &mut controller,
            &backend,
            &registry,
            &mut generation,
            "trade",
            json!({"index": trade}),
            order,
        )
        .await;

        let blotter = act(
            &mut controller,
            &backend,
            &registry,
            &mut generation,
            "refresh-execution",
            json!({}),
            order,
        )
        .await;
        act(
            &mut controller,
            &backend,
            &registry,
            &mut generation,
            "load-trade",
            json!({}),
            order,
        )
        .await;
        let loaded = act(
            &mut controller,
            &backend,
            &registry,
            &mut generation,
            "refresh-loaded-fills",
            json!({}),
            order,
        )
        .await;
        let mut ids = None;
        for (view, publication) in [("blotter", blotter), ("loaded", loaded)] {
            let snapshot = publication
                .result
                .document::<Snapshot>(&format!("{view}-fund-fills"))
                .unwrap();
            let matching = snapshot
                .orders
                .iter()
                .filter(|order| order.trade_id == Some(4301))
                .collect::<Vec<_>>();
            assert_eq!(matching.len(), 1);
            let order = matching[0];
            assert_eq!(order.order_id, 4201);
            assert_eq!(order.status, "Filled");
            assert_eq!(order.fills.len(), 4);
            let fill_ids = order
                .fills
                .iter()
                .map(|fill| fill.id.clone())
                .collect::<BTreeSet<_>>();
            assert_eq!(fill_ids.len(), 4);
            if let Some(previous) = &ids {
                assert_eq!(previous, &fill_ids);
            }
            ids = Some(fill_ids);
            assert_eq!(order.order_quantity, Some(1));
            assert_eq!(order.filled_quantity, Some(1));
            assert!(!order.order_time.is_empty() && !order.last_fill.is_empty());
            let expected_caption = "4201:4301 2026-Nov-12 09:09:28 EST ShortIronCondor=7950:7900 - 7175:7125 2026-Dec-11";
            let expected_last_fill = "Last Fill Date Time 2026-Nov-12 09:10:07 EST";
            let labels = order
                .fills
                .iter()
                .map(|fill| fill.contract.clone())
                .collect::<Vec<_>>();

            let mut workspace = TradeWorkspace::default();
            if view == "blotter" {
                workspace.open = true;
                workspace.tab = 1;
                workspace.compose_tab = 2;
                workspace.draft.insert("blotterTrade".into(), "4301".into());
            } else {
                workspace.loaded_open = true;
                workspace.loaded_show_fills = true;
            }
            let mut harness = Harness::new_state(
                move |ctx, workspace: &mut TradeWorkspace| {
                    assert!(
                        workspace.draw(ctx, &publication).is_none(),
                        "{view} unexpectedly emitted an intent"
                    );
                },
                workspace,
            );
            harness.set_size(egui::vec2(1700.0, 1100.0));
            harness.run_steps(125);
            for label in [
                expected_caption,
                expected_last_fill,
                "Fill Details   Order 4201   Trade 4301",
                "Status   Order Filled",
                "Order Quantity   1",
                "Filled Quantity   1",
                "Reconcile",
                "Update Order Price",
                "Cancel Unfilled",
            ] {
                assert!(
                    harness.query_by_label(label).is_some(),
                    "{view} missing {label}"
                );
            }
            for label in labels {
                assert!(
                    harness.query_by_label(label.as_str()).is_some(),
                    "{view} missing {label}"
                );
            }
            let reconcile = harness.get_by_label("Reconcile").rect();
            let update = harness.get_by_label("Update Order Price").rect();
            let cancel = harness.get_by_label("Cancel Unfilled").rect();
            let workspace = harness.state();
            let panel = if view == "blotter" {
                workspace.bounds
            } else {
                workspace.loaded_bounds
            }
            .unwrap();
            assert!(
                (reconcile.left() - panel.left()).abs() < 100.,
                "{view}: Reconcile is not at the left edge"
            );
            assert!(
                (cancel.right() - panel.right()).abs() < 100.,
                "{view}: Cancel is not at the right edge"
            );
            assert!(
                update.left() - reconcile.right() > 100.,
                "{view}: price controls are not on the right"
            );
            assert!(
                cancel.left() > update.right()
                    && (cancel.center().y - reconcile.center().y).abs() < 3.,
                "{view}: footer controls are out of order or misaligned"
            );
            println!("{view}: {expected_caption}");
            println!("{view}: {expected_last_fill}; four original fills and aligned footer");
        }
    }
}
