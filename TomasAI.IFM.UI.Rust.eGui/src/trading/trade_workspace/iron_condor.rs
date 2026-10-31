//! Established Iron Condor trade view, plan window, and spread chart.
//!
//! Tables bind the authoritative position legs and published risk/price values.
//! Snapshot metrics are displayed, not recomputed locally. The trade-plan table uses
//! the backend total row count plus an offset/page-size window; unloaded visible rows
//! request a new window. The chart consumes its separately published recent series
//! and labels timestamps in America/New_York, including daylight-saving offsets.
//! Fixed reference dimensions are scaled into the available center workspace.

//! IronCondorTradeView parity. Geometry follows its runtime Resize and table weights.
use super::order_layout::{at, rect};
use super::*;
use crate::models::{
    cells::{Cell, Cells},
    trade_contracts::*,
    trade_presentation::{LegDisplay, SpreadDisplayRow, TradeDisplayRow, TradeInfoRow},
};
#[cfg(test)]
use serde_json::json;
/// Black trade-panel background color.
const BLACK: Color32 = Color32::BLACK;
/// White ordinary trade-value foreground.
const WHITE: Color32 = Color32::WHITE;
/// Gray trade-grid border/secondary indicator color.
const GRAY: Color32 = Color32::GRAY;
/// Dark-gray command and unselected-header background.
const COMMAND: Color32 = Color32::from_rgb(45, 45, 48);
/// Lime-green trade highlight color.
const LIME: Color32 = Color32::from_rgb(50, 205, 50);
/// Per-cell live direction, advanced only by a new market publication, never by repainting.
#[derive(Clone)]
struct LegValueChange {
    revision: u64,
    value: Option<f64>,
    background: Color32,
}
impl LegValueChange {
    /// Compares raw numeric values and keeps the last valid baseline through unavailable observations.
    fn update(&mut self, revision: u64, value: Option<f64>) -> Color32 {
        if self.revision != revision {
            self.background = match (self.value, value) {
                (Some(previous), Some(current)) if current > previous => {
                    Color32::from_rgb(0, 100, 0)
                }
                (Some(previous), Some(current)) if current < previous => {
                    Color32::from_rgb(139, 0, 0)
                }
                _ => BLACK,
            };
            self.revision = revision;
            if value.is_some() {
                self.value = value;
            }
        }
        self.background
    }
}
/// Normalize the displayed trade P&L against its corresponding maximum payoff magnitude.
/// Only the drawn fill is capped; the accompanying percentage can exceed 100%.
fn trade_pnl_fraction(pnl: Option<f64>, trade_limit: &(impl Cells + ?Sized)) -> Option<f64> {
    let pnl = pnl.filter(|value| value.is_finite())?;
    let maximum = number(trade_limit, if pnl < 0. { "maxLoss" } else { "maxProfit" })?.abs();
    if !maximum.is_finite() || maximum <= 0. {
        return None;
    }
    let fraction = pnl.abs() / maximum;
    fraction.is_finite().then_some(fraction)
}

/// Clone the first decoded trade row in a group, or return null when the group is absent.
fn one<'a>(display: &'a super::display_cache::TradeDisplayData, kind: &str) -> &'a TradeDisplayRow {
    static EMPTY: TradeDisplayRow = TradeDisplayRow::Empty;
    values(display, kind).first().unwrap_or(&EMPTY)
}
fn values<'a>(
    display: &'a super::display_cache::TradeDisplayData,
    kind: &str,
) -> &'a [TradeDisplayRow] {
    display.values(kind)
}
fn text(value: &(impl Cells + ?Sized), key: &str) -> String {
    value.cell(key).text()
}
/// Read a boolean eligibility/state flag from the published monitor context.
fn monitor_flag(s: &ContractPublication, key: &str) -> bool {
    let name = format!("monitor{}{}", key[..1].to_uppercase(), &key[1..]);
    s.result
        .context
        .get(&name)
        .map(|value| value == "True")
        .unwrap_or_else(|| flag(s, key))
}
// The live path renders one managed cache revision; historical rows retain their saved-data path.
/// Shape authoritative realtime position-leg data into call/put spread display rows. Does not calculate Greeks or revalue the position.
fn realtime_spreads(snapshot: &TradeRealtimeSnapshot, long: bool) -> Vec<SpreadDisplayRow> {
    let legs = snapshot.legs.as_deref().unwrap_or(&[]);
    [(2, "Put"), (1, "Call")]
        .into_iter()
        .map(|(right, name)| {
            let mut row = SpreadDisplayRow {
                spread: format!("{name}{}Spread", if long { "Debit" } else { "Credit" }),
                ..Default::default()
            };
            for bought in [false, true] {
                if let Some(leg) = legs.iter().find(|leg| {
                    leg.definition.as_ref().is_some_and(|definition| {
                        definition.put_call == Some(right)
                            && (definition.signed_quantity.unwrap_or_default() > 0) == bought
                    })
                }) {
                    let retained = leg
                        .last_valid_risk
                        .as_ref()
                        .or_else(|| {
                            leg.position
                                .as_ref()
                                .and_then(|position| position.option_risk.as_ref())
                        })
                        .or_else(|| {
                            (leg.pricing_status.as_deref() == Some("Available"))
                                .then_some(leg.observation.as_ref())
                                .flatten()
                        });
                    let display = LegDisplay {
                        strike: leg
                            .definition
                            .as_ref()
                            .and_then(|definition| definition.strike.clone()),
                        bid: leg.observation.as_ref().and_then(|quote| quote.bid_price),
                        ask: leg.observation.as_ref().and_then(|quote| quote.ask_price),
                        delta: retained.and_then(|quote| quote.delta),
                        gamma: retained.and_then(|quote| quote.gamma),
                        theta: retained.and_then(|quote| quote.theta),
                        vega: retained.and_then(|quote| quote.vega),
                        rho: retained.and_then(|quote| quote.rho),
                        iv: retained.and_then(|quote| quote.implied_volatility),
                        ..Default::default()
                    };
                    if bought {
                        row.long = display
                    } else {
                        row.short = display
                    }
                }
            }
            if let Some(spread) = snapshot
                .spreads
                .as_ref()
                .and_then(|spreads| spreads.iter().find(|spread| spread.put_call == Some(right)))
            {
                let signed = |field: &str| {
                    number(spread, field).map(|value| if long { value } else { value.abs() })
                };
                row.net_spread = signed("netSpread");
                row.trade_value = signed("tradeValue");
                row.forward_price = signed("forwardPrice");
                row.trade_pnl = number(spread, "tradePnl");
                row.otm_probability = number(spread, "otmProbability");
            }
            row
        })
        .collect()
}

#[cfg(test)]
mod realtime_cache_tests {
    use super::*;
    #[test]
    fn leg_value_colors_follow_updates_not_repaints_and_preserve_valid_baselines() {
        let mut cell = LegValueChange {
            revision: 1,
            value: Some(-0.4),
            background: BLACK,
        };
        assert_eq!(cell.update(2, Some(-0.3)), Color32::from_rgb(0, 100, 0));
        assert_eq!(cell.update(2, Some(-0.3)), Color32::from_rgb(0, 100, 0));
        assert_eq!(cell.update(3, Some(-0.3)), BLACK);
        assert_eq!(cell.update(4, Some(-0.5)), Color32::from_rgb(139, 0, 0));
        assert_eq!(cell.update(5, Some(0.)), Color32::from_rgb(0, 100, 0));
        assert_eq!(cell.update(6, None), BLACK);
        assert_eq!(cell.update(7, Some(0.)), BLACK);
    }
    #[test]
    fn pending_and_stale_pricing_keep_valid_greeks_and_new_zero_values_are_displayed() {
        let mut snapshot = json!({"legs":[{"definition":{"putCall":2,"signedQuantity":-1,"strike":"5000"},
            "quoteStatus":"Stale","pricingStatus":"Pending", "observation":{"bidPrice":9,"askPrice":11,"delta":0},
            "lastValidRisk":{"delta":-0.4,"gamma":0.01,"vega":12,"rho":0.1,"theta":-3,"impliedVolatility":0.2}}]});
        let rows = realtime_spreads(&serde_json::from_value(snapshot.clone()).unwrap(), false);
        assert_eq!(number(&rows[0], "shortDelta").unwrap(), -0.4);
        assert_eq!(number(&rows[0], "shortBid").unwrap(), 9.0);
        snapshot["legs"][0]["lastValidRisk"]["delta"] = json!(0);
        snapshot["legs"][0]["pricingStatus"] = json!("Stale");
        assert_eq!(
            number(
                &realtime_spreads(&serde_json::from_value(snapshot.clone()).unwrap(), false)[0],
                "shortDelta"
            )
            .unwrap(),
            0.0
        );
        let current: IronCondorTradePlanSnapshot =
            serde_json::from_value(json!({"forwardLossRatio":null,"dailyPnl":"0"})).unwrap();
        let previous: IronCondorTradePlanSnapshot =
            serde_json::from_value(json!({"forwardLossRatio":0.25,"dailyPnl":"10"})).unwrap();
        let detail = retained_plan_values(Some(&current), Some(&previous));
        assert_eq!(number(&detail, "forwardLossRatio").unwrap(), 0.25);
        assert_eq!(number(&detail, "dailyPnl").unwrap(), 0.0);
    }

    #[test]
    fn missing_current_plan_retains_only_numeric_cells_and_keeps_readiness_unknown() {
        let previous:IronCondorTradePlanSnapshot=serde_json::from_value(json!({"forwardLossRatio":0.25,"calculationStatus":"Complete","actionReason":"Previous reason"})).unwrap();
        let retained = retained_plan_values(None, Some(&previous));
        assert_eq!(number(&retained, "forwardLossRatio"), Some(0.25));
        assert!(retained.cell("isComplete").is_missing());
        assert!(retained.cell("actionReason").is_missing());
    }
    /// Regression check: whole snapshot binds all greeks and forward values without local valuation.
    #[test]
    fn whole_snapshot_binds_all_greeks_and_forward_values_without_local_valuation() {
        let snapshot = json!({"legs":[{"definition":{"putCall":2,"signedQuantity":-2,"strike":"5000"},
            "quoteStatus":"Available","pricingStatus":"Available",
            "observation":{"bidPrice":9,"askPrice":11,"delta":-0.4,"gamma":0.01,"theta":-3,"vega":12,"rho":0.1,"impliedVolatility":0.2}}],
            "spreads":[{"putCall":2,"netSpread":"-2","tradeValue":"-200","tradePnl":"10","otmProbability":0.7,"forwardPrice":"-3"}]});
        let rows = realtime_spreads(&serde_json::from_value(snapshot.clone()).unwrap(), false);
        assert_eq!(rows.len(), 2);
        assert_eq!(number(&rows[0], "shortVega").unwrap(), 12.0);
        assert_eq!(number(&rows[0], "shortRho").unwrap(), 0.1);
        assert_eq!(number(&rows[0], "forwardPrice").unwrap(), 3.0);
        assert_eq!(number(&rows[0], "tradeValue").unwrap(), 200.0);
        assert!(rows[1].cell("shortBid").is_missing());
        let mut stale = snapshot;
        stale["legs"][0]["pricingStatus"] = json!("Stale");
        let rows = realtime_spreads(&serde_json::from_value(stale).unwrap(), false);
        assert!(rows[0].cell("shortDelta").is_missing());
        assert_eq!(number(&rows[0], "shortBid").unwrap(), 9.0);
    }
}
/// Retain available numeric plan fields for display; current plan status still reports readiness.
#[derive(Clone, Copy)]
struct RetainedPlanValues<'a> {
    current: Option<&'a IronCondorTradePlanSnapshot>,
    previous: Option<&'a IronCondorTradePlanSnapshot>,
}
impl Cells for RetainedPlanValues<'_> {
    fn cell(&self, column: &str) -> Cell<'_> {
        let current = self.current.cell(column);
        if !current.is_missing() {
            return current;
        }
        let previous = self.previous.cell(column);
        let numeric = match &previous {
            Cell::Signed(_) | Cell::Unsigned(_) | Cell::Number(_) => true,
            Cell::Text(value) => value.parse::<f64>().is_ok(),
            _ => false,
        };
        if numeric { previous } else { current }
    }
}
impl RetainedPlanValues<'_> {
    fn is_complete(&self) -> bool {
        self.current
            .is_some_and(IronCondorTradePlanSnapshot::is_complete)
    }
    fn unavailable_reasons(&self) -> Option<&[String]> {
        self.current
            .and_then(|value| value.unavailable_reasons.as_deref())
    }
}
fn retained_plan_values<'a>(
    current: Option<&'a IronCondorTradePlanSnapshot>,
    previous: Option<&'a IronCondorTradePlanSnapshot>,
) -> RetainedPlanValues<'a> {
    RetainedPlanValues { current, previous }
}

fn number(value: &(impl Cells + ?Sized), key: &str) -> Option<f64> {
    value.cell(key).number()
}
fn formatted(value: &(impl Cells + ?Sized), key: &str) -> String {
    if value.cell(key).is_missing() {
        return "N/A".into();
    }
    if let Some(number) = number(value, key) {
        return if key.contains("Probability") || key.ends_with("IV") || key == "maxReturn" {
            format!("{:.2}%", number * 100.)
        } else if key.ends_with("Strike")
            || ["orderId", "tradeId", "quantity", "daysToExpiry"].contains(&key)
        {
            format!("{number:.0}")
        } else {
            format!("{number:.4}")
                .trim_end_matches('0')
                .trim_end_matches('.')
                .to_owned()
        };
    }
    text(value, key)
}
/// Paint a bounded trade-view label with explicit foreground, alignment, and font emphasis.
fn label(
    ui: &mut egui::Ui,
    r: egui::Rect,
    value: impl Into<String>,
    color: Color32,
    center: bool,
    bold: bool,
) {
    let value = value.into();
    let wrap = value.contains('\n');
    let mut text = egui::RichText::new(value).color(color);
    if bold {
        text = text.strong();
    }
    at(ui, r, &format!("caption-{}-{}", r.min.x, r.min.y), |ui| {
        ui.set_clip_rect(r.intersect(ui.clip_rect()));
        let layout = egui::Layout::left_to_right(egui::Align::Center).with_main_align(if center {
            egui::Align::Center
        } else {
            egui::Align::Min
        });
        ui.with_layout(layout, |ui| {
            let widget = egui::Label::new(text).halign(if center {
                egui::Align::Center
            } else {
                egui::Align::Min
            });
            ui.add_sized(
                r.size(),
                if wrap {
                    widget.wrap()
                } else {
                    widget.truncate()
                },
            );
        });
    });
}
/// Draw a read-only trade metric within fixed bounds using explicit background, foreground, and emphasis.
fn field(
    ui: &mut egui::Ui,
    r: egui::Rect,
    id: &str,
    value: String,
    bg: Color32,
    fg: Color32,
    bold: bool,
) {
    ui.painter().rect_filled(r, 0., bg);
    ui.painter().rect_stroke(
        r,
        0.,
        egui::Stroke::new(1.0_f32, Color32::from_gray(80)),
        egui::StrokeKind::Inside,
    );
    ui.push_id(id, |ui| label(ui, r.shrink(2.), value, fg, true, bold));
}
/// Render an already loaded read-only trade table and return its selected row index; delegates geometry/scrolling to the window-aware table helper.
fn table<T: Cells>(
    ui: &mut egui::Ui,
    r: egui::Rect,
    id: &str,
    rows: &[T],
    columns: &[(&str, &str, f32)],
    selected: Option<usize>,
) -> Option<usize> {
    table_window(ui, r, id, rows, columns, selected, None).0
}
/// Render a logical table using the loaded page. The window argument is (loaded offset, total row count); return clicked global index, missing (offset, page size), and first visible global index.
fn table_window<T: Cells>(
    ui: &mut egui::Ui,
    r: egui::Rect,
    id: &str,
    rows: &[T],
    columns: &[(&str, &str, f32)],
    selected: Option<usize>,
    window: Option<(usize, usize)>,
) -> (Option<usize>, Option<(usize, usize)>, Option<usize>) {
    let mut missing = None;
    let mut first_visible = None;
    let clicked = at(ui, r, id, |ui| {
        let outer = r;
        let r = outer.shrink(1. / ui.ctx().pixels_per_point());
        let clicked = crate::control_parity::grid_viewport(ui, outer, |ui| {
            // Reserve space for both bars inside the fixed grid rectangle. Floating bars
            // disappear at rest and the horizontal bar otherwise falls below the body.
            let p = ui.ctx().pixels_per_point();
            ui.spacing_mut().scroll = crate::presentation::grid_scroll_style(p);
            // The trading theme's inactive button fill is almost black. Scroll handles
            // need their own contrast against a visible track, including while idle.
            ui.visuals_mut().extreme_bg_color = Color32::from_gray(55);
            ui.visuals_mut().widgets.inactive.fg_stroke.color = crate::presentation::SCROLLBAR;
            ui.visuals_mut().widgets.hovered.fg_stroke.color = crate::presentation::SCROLLBAR;
            ui.visuals_mut().widgets.active.fg_stroke.color = crate::presentation::SCROLLBAR;
            let scroll_bar = ui.spacing().scroll.allocated_width();
            let visibility = if id.starts_with("condor-plans") {
                egui::scroll_area::ScrollBarVisibility::AlwaysVisible
            } else {
                egui::scroll_area::ScrollBarVisibility::VisibleWhenNeeded
            };
            let row_height = 24. / p;
            let mut clicked = None;
            egui::ScrollArea::horizontal()
                .id_salt((id, "scroll"))
                .scroll_bar_visibility(visibility)
                .auto_shrink([false, false])
                .max_width(r.width())
                .max_height(r.height())
                .show(ui, |ui| {
                    ui.set_min_width(
                        columns
                            .iter()
                            .map(|(_, _, w)| w / p)
                            .sum::<f32>()
                            .max(r.width() - 14. / p),
                    );
                    let body_height =
                        (r.height() - row_height - ui.spacing().item_spacing.y - scroll_bar)
                            .max(0.);
                    let mut grid = egui_extras::TableBuilder::new(ui)
                        .id_salt(id)
                        .scroll_bar_visibility(visibility)
                        .striped(false)
                        .sense(egui::Sense::click())
                        .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                        .auto_shrink([false, false])
                        .min_scrolled_height(body_height)
                        .max_scroll_height(body_height);
                    for (index, (_, _, width)) in columns.iter().enumerate() {
                        grid = grid.column(if index + 1 == columns.len() {
                            egui_extras::Column::remainder()
                                .at_least(*width / p)
                                .clip(true)
                                .resizable(true)
                        } else {
                            egui_extras::Column::initial(*width / p)
                                .at_least(40. / p)
                                .clip(true)
                                .resizable(true)
                        });
                    }
                    grid.header(row_height, |mut row| {
                        for (_, title, _) in columns {
                            row.col(|ui| {
                                ui.painter().rect_filled(ui.max_rect(), 0., COMMAND);
                                ui.add(
                                    egui::Label::new(
                                        egui::RichText::new(*title)
                                            .color(crate::presentation::CAPTION_FOREGROUND),
                                    )
                                    .truncate(),
                                );
                            });
                        }
                    })
                    .bordered_body(row_height, |body| {
                        body.rows(
                            row_height,
                            window.map_or(rows.len(), |(_, total)| total),
                            |mut row| {
                                let absolute_index = row.index();
                                first_visible.get_or_insert(absolute_index);
                                let offset = window.map_or(0, |(offset, _)| offset);
                                let index = absolute_index.checked_sub(offset);
                                let datum = index.and_then(|index| rows.get(index));
                                if datum.is_none() && window.is_some() && missing.is_none() {
                                    missing = Some((
                                        absolute_index.saturating_sub(2),
                                        ((r.height() / row_height).ceil() as usize + 4)
                                            .clamp(1, 100),
                                    ));
                                }

                                row.set_selected(selected == Some(absolute_index));
                                let color = if id.starts_with("condor-plans")
                                    && !matches!(datum.cell("isComplete"), Cell::Boolean(true))
                                {
                                    Color32::YELLOW
                                } else {
                                    WHITE
                                };
                                for (key, _, _) in columns {
                                    row.col(|ui| {
                                        let value = if datum.is_none() && window.is_some() {
                                            if *key == columns[0].0 {
                                                "Loading?".to_owned()
                                            } else {
                                                String::new()
                                            }
                                        } else {
                                            formatted(&datum, key)
                                        };
                                        ui.add(
                                            egui::Label::new(
                                                egui::RichText::new(&value).color(color),
                                            )
                                            .truncate(),
                                        )
                                        .on_hover_text(value);
                                    });
                                }
                                if row.response().clicked() {
                                    clicked = Some(absolute_index);
                                }
                            },
                        )
                    });
                });
            clicked
        });
        ui.painter().rect_stroke(
            outer,
            0.,
            egui::Stroke::new(1.0_f32, GRAY),
            egui::StrokeKind::Inside,
        );
        clicked
    });
    (clicked, missing, first_visible)
}
/// Choose an indicator color from the published monitor severity/state.
fn severity(v: &(impl Cells + ?Sized)) -> Color32 {
    if !matches!(v.cell("isComplete"), Cell::Boolean(true)) {
        return BLACK;
    }
    match text(v, "actionState").as_str() {
        "Normal" => LIME,
        "Warning" => Color32::YELLOW,
        "Critical" => Color32::from_rgb(255, 140, 0),
        "RedAlert" => Color32::RED,
        _ => BLACK,
    }
}
/// Limits chart sampling using monotonic time, independent of quote timestamps and repaint frequency.
#[derive(Clone, Default)]
struct SpreadChartClock {
    last_sample: Option<std::time::Instant>,
}
impl SpreadChartClock {
    /// Accept the first point immediately, then at most one point every sixty seconds.
    fn accept(&mut self, now: std::time::Instant) -> bool {
        if self
            .last_sample
            .is_some_and(|last| now.duration_since(last).as_secs() < 60)
        {
            return false;
        }
        self.last_sample = Some(now);
        true
    }
}

/// Merge the history/live boundary while retaining the latest sample in each UTC minute.
fn coalesce_chart_minutes(points: &mut Vec<[f64; 2]>) {
    points.sort_by(|a, b| b[0].total_cmp(&a[0]));
    points.dedup_by_key(|point| (point[0] / 60.).floor() as i64);
    points.reverse();
}

impl TradeWorkspace {
    /// Render the selected established trade in the center workspace and return any requested monitor/plan-page action.
    pub(super) fn draw_loaded_trade(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
    ) -> Option<&'static str> {
        if !self.loaded_open {
            self.clear_trade_display();
            return None;
        }
        let display = self.prepare_trade_display(s);
        let trade = one(&display, "established").established();
        if trade.is_none() {
            self.clear_trade_display();
            return None;
        }
        let trade = trade.unwrap();
        let identity = format!("{:?}", trade.id);
        if self.loaded_spread_id != identity {
            self.loaded_forward_points.clear();
            if !self.loaded_spread_id.is_empty() {
                self.loaded_view_states.insert(
                    self.loaded_spread_id.clone(),
                    (
                        std::mem::take(&mut self.loaded_spread_points),
                        self.draft.remove("positionIndex"),
                    ),
                );
            }
            self.loaded_spread_id = identity.clone();
            if let Some((points, index)) = self.loaded_view_states.remove(&identity) {
                self.loaded_spread_points = points;
                if let Some(index) = index {
                    self.draft.insert("positionIndex".into(), index);
                }
            }
        }
        let mut action = None;
        let panel = egui::CentralPanel::default()
            .frame(egui::Frame::new().fill(BLACK))
            .show(ctx, |ui| {
                let p = ctx.pixels_per_point();
                let mut bounds = ui.max_rect();
                if let Some(tab_action) = self.draw_workspace_tabs(ui, s, bounds) {
                    action = Some(tab_action);
                }
                bounds.min.y += 34. / p;
                let o = bounds.min;
                let w = bounds.width() * p;
                let h = bounds.height() * p;
                // Same split calculation as IFormControl.Resize: bottom third, 183px marks,
                // 120px four-contract pane, 79px limits, remaining width split after history, with 120px reassigned to the chart.
                let log = (h - 7.) / 3.;
                let upper = h - 7. - log;
                let marks = 183.;
                let graph_h = (upper - 6. - marks).max(180.);
                let chart_w = (((w - 650.) / 2.) + 120.).min((w - 640.).max(0.)).max(0.);
                let show_chart = chart_w >= 220. && upper >= marks + 180.;
                let left = if show_chart { w - chart_w } else { w };
                let history_h = (graph_h - 205.).max(75.);
                let rr = |x, y, ww, hh| rect(o, p, x, y, ww, hh);
                ui.spacing_mut().item_spacing = egui::Vec2::ZERO;
                let description = format!(
                    "{} | {} | {}.{}.{}.{} | {}",
                    s.result
                        .context
                        .get("fundName")
                        .cloned()
                        .unwrap_or_default(),
                    s.result
                        .context
                        .get("compositionType")
                        .cloned()
                        .unwrap_or_else(|| "Iron Condor".into()),
                    text(&trade.id, "portfolioId"),
                    text(&trade.id, "fundId"),
                    text(&trade.id, "orderId"),
                    text(&trade.id, "tradeId"),
                    text(trade, "status")
                );
                label(
                    ui,
                    rr(2., 0., left - 4., 34.),
                    description,
                    crate::presentation::CAPTION_FOREGROUND,
                    false,
                    false,
                );

                let history = values(&display, "position");
                let selected = self
                    .draft
                    .get("positionIndex")
                    .and_then(|i| i.parse::<usize>().ok())
                    .filter(|i| *i < history.len())
                    .or_else(|| history.len().checked_sub(1));
                let history_rect = rr(2., 35., left - 4., history_h - 34.);
                self.controls.insert("condor-history".into(), history_rect);
                let clicked = ui
                    .add_enabled_ui(monitor_flag(s, "canSelectPosition"), |ui| {
                        table(
                            ui,
                            history_rect,
                            "condor-history",
                            &history,
                            &[
                                ("orderId", "Order Id", 100.),
                                ("tradeId", "Trade Id", 100.),
                                ("tradeType", "Trade Type", 250.),
                                ("valueDate", "Value Date", 100.),
                                ("daysToExpiry", "Days To Expiry", 120.),
                                ("tradeStatus", "Trade Status", 150.),
                                ("commission", "Commission", 115.),
                                ("netSpread", "Net Spread", 115.),
                                ("tradePnl", "Daily P&L", 120.),
                            ],
                            selected,
                        )
                    })
                    .inner;
                if let Some(index) = clicked {
                    self.draft.insert("positionIndex".into(), index.to_string());
                    self.draft.insert("index".into(), index.to_string());
                    action = Some("position");
                }
                let legs = trade.legs.as_deref().unwrap_or(&[]);
                let details = TradeInfoRow {
                    trade,
                    trade_type: s.result.context.get("compositionType").map(String::as_str),
                    action: s.result.context.get("tradeAction").map(String::as_str),
                    quantity: legs
                        .iter()
                        .filter_map(|leg| number(leg, "signedQuantity"))
                        .map(f64::abs)
                        .reduce(f64::min),
                };
                let contracts_w = 216.;
                let info_rect = rr(2., history_h + 6., left - contracts_w - 2., 120.);
                self.controls.insert("condor-trade-info".into(), info_rect);
                table(
                    ui,
                    info_rect,
                    "condor-trade-info",
                    &[details],
                    &[
                        ("orderId", "Order Id", 120.),
                        ("tradeId", "Trade Id", 120.),
                        ("tradeType", "Trade Type", 150.),
                        ("quantity", "Quantity", 115.),
                        ("tradeDate", "Trade Date", 120.),
                        ("expiry", "Maturity Date", 120.),
                        ("state", "Trade State", 120.),
                        ("action", "Trade Action", 120.),
                    ],
                    None,
                );
                let contracts_rect = rr(left - contracts_w, history_h + 6., contracts_w - 2., 120.);
                self.controls
                    .insert("condor-contracts".into(), contracts_rect);
                table(
                    ui,
                    contracts_rect,
                    "condor-contracts",
                    &legs,
                    &[("contractId", "Option Contract Ids", 216.)],
                    None,
                );
                let limit = one(&display, "limits").limits();
                let limit_row = limit;
                let limits_rect = rr(2., graph_h - 79., left - 4., 79.);
                self.controls.insert("condor-limits".into(), limits_rect);
                table(
                    ui,
                    limits_rect,
                    "condor-limits",
                    &[limit_row],
                    &[
                        ("orderId", "Order Id", 100.),
                        ("riskMargin", "Risk Margin", 120.),
                        ("maxProfit", "Max Profit", 90.),
                        ("maxLoss", "Max Loss", 90.),
                        ("maxReturn", "Max Return", 110.),
                        ("maxLossLimit", "Max Loss Limit", 125.),
                        ("maxProfitLimit", "Max Profit Limit", 115.),
                        ("minProfitTarget", "Min Profit Target", 120.),
                        ("dailyProfitTarget", "Daily Profit Target", 140.),
                        ("fundBalance", "Fund Balance", 150.),
                    ],
                    None,
                );
                let position = one(&display, "position-details").position();
                if show_chart {
                    self.condor_chart(ui, s, trade, position, rr(left, 0., chart_w, graph_h));
                }
                ui.painter().hline(
                    bounds.x_range(),
                    o.y + graph_h / p,
                    egui::Stroke::new(1.0_f32, GRAY),
                );
                let plans = values(&display, "plan");
                let realtime = one(&display, "realtime").realtime();
                let plan = if let Some(realtime) = realtime {
                    realtime.plan.as_deref()
                } else {
                    plans.first().and_then(TradeDisplayRow::plan)
                };
                let detail = retained_plan_values(
                    plan.and_then(|plan| plan.iron_condor_trade_plan_snapshot.as_deref()),
                    realtime
                        .and_then(|value| value.last_available_plan.as_ref())
                        .and_then(|plan| plan.iron_condor_trade_plan_snapshot.as_deref()),
                );
                let history_row = selected.and_then(|index| history.get(index));
                let mut net =
                    number(&history_row, "netSpread").or_else(|| number(trade, "openingValue"));
                if let Some(realtime) = realtime {
                    net = number(&realtime.summary, "netSpread");
                } else if let Some(marks) = position.and_then(|position| position.legs.as_ref()) {
                    let quantity = marks
                        .iter()
                        .filter_map(|leg| number(leg, "signedQuantity"))
                        .map(f64::abs)
                        .reduce(f64::min)
                        .unwrap_or_default();
                    if quantity > 0. {
                        net = Some(
                            marks
                                .iter()
                                .map(|leg| {
                                    number(leg, "currentPrice").unwrap_or_default()
                                        * number(leg, "signedQuantity").unwrap_or_default()
                                })
                                .sum::<f64>()
                                / quantity,
                        );
                    }
                }
                let trade_pnl = number(&detail, "tradePnl");
                let asset = if let Some(realtime) = realtime {
                    formatted(&realtime.summary, "underlyingPrice")
                } else {
                    formatted(&detail, "underlyingPrice")
                };
                let summary_y = graph_h + 6.;
                let top_fields = [
                    ("Value Date:", formatted(&history_row, "valueDate"), BLACK),
                    (
                        "Trade Status:",
                        if let Some(message) = s
                            .result
                            .context
                            .get("monitorStatus")
                            .filter(|message| !message.is_empty())
                        {
                            message.clone()
                        } else if monitor_flag(s, "observationsLoading") {
                            "Loading saved position observations...".into()
                        } else if let Some(error) = s
                            .result
                            .context
                            .get("observationsError")
                            .filter(|e| !e.is_empty())
                        {
                            format!("Saved observations: {error}")
                        } else if plan.is_none() {
                            text(trade, "status")
                        } else {
                            format!("{}: {}", text(&plan, "state"), text(&plan, "explanation"))
                        },
                        BLACK,
                    ),
                    ("DTE:", formatted(&history_row, "daysToExpiry"), BLACK),
                    (
                        "Net Spread:",
                        net.map(|n| format!("{n:.2}"))
                            .unwrap_or_else(|| "N/A".into()),
                        BLACK,
                    ),
                    (
                        "Trade P&L:",
                        trade_pnl
                            .map(|n| format!("{n:.2}"))
                            .unwrap_or_else(|| "N/A".into()),
                        BLACK,
                    ),
                    ("Asset Price:", asset, BLACK),
                    (
                        "Fwd Loss:",
                        number(&detail, "forwardLossRatio")
                            .map(|n| format!("{:.1}%", n * 100.))
                            .unwrap_or_else(|| "N/A".into()),
                        severity(&detail),
                    ),
                ];
                // Full-width proportional header, with the same caption/value weights as tableLayoutPanel1.
                let weights = [
                    120., 120., 120., 90., 55., 45., 120., 80., 110., 96., 114., 90., 88., 70.,
                ];
                let feed_w = 165.;
                let usable = w - feed_w - 16.;
                let total: f32 = weights.iter().sum();
                let mut x = 7.;
                for (n, (caption, value, bg)) in top_fields.into_iter().enumerate() {
                    let cw = usable * weights[n * 2] / total;
                    let vw = usable * weights[n * 2 + 1] / total;
                    label(
                        ui,
                        rr(x, summary_y + 3., cw - 5., 28.),
                        caption,
                        crate::presentation::CAPTION_FOREGROUND,
                        true,
                        false,
                    );
                    x += cw;
                    let r = rr(x, summary_y + 4., vw - 5., 26.);
                    field(
                        ui,
                        r,
                        &format!("condor-summary-{n}"),
                        value,
                        bg,
                        WHITE,
                        n == 3 || n == 6,
                    );
                    x += vw;
                }
                at(
                    ui,
                    rr(w - feed_w - 4., summary_y + 4., feed_w, 26.),
                    "condor-feed",
                    |ui| {
                        let live = monitor_flag(s, "live");
                        let off = egui::RichText::new("LiveFeed OFF").color(Color32::RED);
                        let on =
                            egui::RichText::new("LiveFeed ON").color(Color32::from_rgb(0, 255, 0));
                        ui.visuals_mut().disabled_alpha = 1.0;
                        ui.add_enabled_ui(monitor_flag(s, "canLive") || live, |ui| {
                            egui::ComboBox::from_id_salt("condor-live-feed")
                                .selected_text(if live { on.clone() } else { off.clone() })
                                .width(feed_w / p - 6.)
                                .show_ui(ui, |ui| {
                                    if ui.selectable_label(!live, off).clicked() && live {
                                        action = Some("live-off");
                                    }
                                    if ui.selectable_label(live, on).clicked() && !live {
                                        action = Some("live-on");
                                    }
                                });
                        });
                    },
                );
                let progress = rr(10., summary_y + 45., w * 0.22, 14.);
                ui.painter()
                    .rect_filled(progress, 0., Color32::from_gray(230));
                if let (Some(pnl), Some(percent)) = (
                    trade_pnl,
                    trade_pnl_fraction(
                        trade_pnl,
                        &limit.and_then(|limit| limit.trade_limit.as_ref()),
                    ),
                ) {
                    let color = if pnl >= 0. { LIME } else { Color32::RED };
                    ui.painter().rect_filled(
                        egui::Rect::from_min_size(
                            progress.min,
                            egui::vec2(
                                progress.width() * percent.clamp(0., 1.) as f32,
                                progress.height(),
                            ),
                        ),
                        0.,
                        color,
                    );
                    field(
                        ui,
                        rr(w * 0.225, summary_y + 38., w * 0.12, 28.),
                        "condor-profit",
                        format!("{pnl:.2} @ {:.2}%", percent * 100.),
                        color,
                        WHITE,
                        true,
                    );
                } else {
                    label(
                        ui,
                        rr(w * 0.225, summary_y + 38., w * 0.12, 28.),
                        "N/A",
                        WHITE,
                        false,
                        true,
                    );
                }
                field(
                    ui,
                    rr(w * 0.35, summary_y + 36., w * 0.12, 32.),
                    "condor-plan-action",
                    text(&detail, "actionType"),
                    severity(&detail),
                    WHITE,
                    false,
                );
                let reason = if detail.is_complete() {
                    text(&detail, "actionReason")
                } else {
                    detail
                        .unavailable_reasons()
                        .map(|reasons| reasons.join("; "))
                        .unwrap_or_default()
                };
                label(
                    ui,
                    rr(w * 0.47, summary_y + 36., w * 0.525, 32.),
                    reason,
                    WHITE,
                    false,
                    false,
                );
                self.draw_condor_spreads(
                    ui,
                    s,
                    trade,
                    position,
                    rr(7., summary_y + 71., w - 14., 108.),
                );
                let log_y = upper + 7.;
                ui.painter().hline(
                    bounds.x_range(),
                    o.y + (log_y - 3.) / p,
                    egui::Stroke::new(1.0_f32, GRAY),
                );
                let plan_rows = &display.plans;
                let mut columns = vec![
                    ("actionDateTime", "ActionDateTime", 230.),
                    ("sequenceId", "SequenceId", 120.),
                    ("dailyPnl", "DailyPnl", 120.),
                    ("forwardLossRatio", "ForwardLossRatio", 120.),
                    ("forwardLoss", "ForwardLoss", 120.),
                    ("lossHeadroom", "LossHeadroom", 120.),
                    ("putForwardPrice", "PutForwardPrice ($)", 150.),
                    ("callForwardPrice", "CallForwardPrice ($)", 150.),
                    ("forwardDelta", "ForwardDelta", 120.),
                    ("netDelta", "NetDelta", 120.),
                    ("netGamma", "NetGamma", 120.),
                    ("netVega", "NetVega", 120.),
                    ("netTheta", "NetTheta", 120.),
                    ("estimatedExitCosts", "EstimatedExitCosts", 120.),
                    ("distanceToShortPut", "DistanceToShortPut", 120.),
                    ("distanceToShortCall", "DistanceToShortCall", 120.),
                    ("putSpreadPrice", "PutSpreadPrice", 120.),
                    ("callSpreadPrice", "CallSpreadPrice", 120.),
                    ("combinedSpreadPrice", "CombinedSpreadPrice", 120.),
                    ("currentPositionValue", "CurrentPositionValue", 120.),
                    ("estimatedCloseValue", "EstimatedCloseValue", 120.),
                    ("putOTMProbability", "PutOTMProbability", 120.),
                    ("callOTMProbability", "CallOTMProbability", 120.),
                    ("timeToExpiryDays", "TimeToExpiryDays", 120.),
                    ("scenarioHorizonSeconds", "ScenarioHorizonSeconds", 120.),
                    ("scenarioUnderlyingMove", "ScenarioUnderlyingMove", 120.),
                    ("scenarioVolatilityShift", "ScenarioVolatilityShift", 120.),
                    ("calculationStatus", "CalculationStatus", 120.),
                    ("exitRecommended", "ExitRecommended", 120.),
                    ("exitReason", "ExitReason", 320.),
                ];
                columns.insert(2, ("volatilityPrice", "VolatilityPrice", 140.));
                // Older generic plans still expose their explanation, without substituting blank legacy fields.
                if display.has_action_reason {
                    columns.insert(2, ("actionReason", "Action Reason", 320.));
                }
                let plan_rect = rr(0., log_y, w, h - log_y);
                self.controls.insert("condor-plans".into(), plan_rect);
                let context = &s.result.context;
                let offset = context
                    .get("planOffset")
                    .and_then(|v| v.parse().ok())
                    .unwrap_or(0);
                let total = context
                    .get("planTotal")
                    .and_then(|v| v.parse().ok())
                    .unwrap_or(plan_rows.len());
                // Live rows are pushed by the monitor. Database paging belongs exclusively to stopped mode.
                let live_plans = monitor_flag(s, "live");
                let virtual_mode = !live_plans && context.contains_key("planTotal");
                let scope = format!(
                    "{}:{}:{}",
                    context.get("activeTradeTab").cloned().unwrap_or_default(),
                    context.get("planDate").cloned().unwrap_or_default(),
                    if live_plans { "live" } else { "database" }
                );
                let page_id = if virtual_mode {
                    format!("condor-plans-{scope}")
                } else {
                    "condor-plans".to_owned()
                };
                let (_, missing, first_visible) = table_window(
                    ui,
                    plan_rect,
                    &page_id,
                    &plan_rows,
                    &columns,
                    None,
                    virtual_mode.then_some((offset, total)),
                );
                let latest = context
                    .get("latestPlanRevision")
                    .and_then(|v| v.parse::<u64>().ok())
                    .unwrap_or(0);
                let anchor = context
                    .get("planMaximumRevision")
                    .and_then(|v| v.parse::<u64>().ok())
                    .unwrap_or(0);
                let refresh =
                    virtual_mode && first_visible == Some(0) && anchor > 0 && latest > anchor;
                let needed = if refresh {
                    Some((
                        0,
                        ((plan_rect.height() / (20. / ui.ctx().pixels_per_point())).ceil()
                            as usize
                            + 4)
                        .clamp(1, 100),
                    ))
                } else {
                    missing
                };
                if let Some(error) = context.get("planError").filter(|e| !e.is_empty()) {
                    ui.painter().text(
                        plan_rect.center(),
                        egui::Align2::CENTER_CENTER,
                        error,
                        egui::FontId::proportional(12.),
                        Color32::RED,
                    );
                } else if let Some((offset, size)) =
                    needed.filter(|_| self.pending.is_none() && !s.busy)
                {
                    self.draft.insert("refresh".into(), refresh.to_string());
                    self.draft.insert("offset".into(), offset.to_string());
                    self.draft.insert("pageSize".into(), size.to_string());
                    self.draft.insert(
                        "tradeTabId".into(),
                        context.get("activeTradeTab").cloned().unwrap_or_default(),
                    );
                    action = Some("plans-page");
                }
            });
        self.loaded_bounds = Some(panel.response.rect);
        action
    }
    /// Render the published call/put spread and leg metrics within the fixed trade-view region.
    fn draw_condor_spreads(
        &self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        trade: &EstablishedTradeDefinition,
        position: Option<&StrategyPositionSnapshot>,
        bounds: egui::Rect,
    ) {
        let p = ui.ctx().pixels_per_point();
        let o = bounds.min;
        let w = bounds.width() * p;
        let columns = [
            ("spread", "Spread Type", 180.),
            ("shortStrike", "Short\nStrike", 80.),
            ("shortBid", "Short\nBid", 80.),
            ("shortAsk", "Short\nAsk", 80.),
            ("shortDelta", "Short\nDelta", 90.),
            ("shortGamma", "Short\nGamma", 90.),
            ("shortTheta", "Short\nTheta", 90.),
            ("shortVega", "Short\nVega", 90.),
            ("shortRho", "Short\nRho", 90.),
            ("shortIV", "Short\nIVol", 90.),
            ("longStrike", "Long\nStrike", 80.),
            ("longBid", "Long\nBid", 80.),
            ("longAsk", "Long\nAsk", 80.),
            ("longDelta", "Long\nDelta", 90.),
            ("longGamma", "Long\nGamma", 90.),
            ("longTheta", "Long\nTheta", 90.),
            ("longVega", "Long\nVega", 90.),
            ("longRho", "Long\nRho", 90.),
            ("longIV", "Long\nIVol", 90.),
            ("netSpread", "Net\nSpread", 70.),
            ("tradeValue", "Trade\nValue", 70.),
            ("tradePnl", "Trade\nPnl", 70.),
            ("otmProbability", "OTM\nProb", 70.),
            ("forwardPrice", "Fwd\nPrice", 70.),
        ];
        let total: f32 = columns.iter().skip(1).map(|c| c.2).sum();
        let mut x = 0.;
        let rows = self.condor_spreads(ui, s, trade, position);
        let realtime = one(&self.display_cache.as_ref().unwrap().model, "realtime").realtime();
        let revision = realtime
            .and_then(|value| value.snapshot_revision)
            .and_then(|value| u64::try_from(value).ok())
            .unwrap_or(s.generation);
        let change_scope = egui::Id::new((
            "option-leg-changes",
            format!("{:?}", trade.id),
            s.result.context.get("activeTradeTab"),
            realtime.and_then(|value| value.monitoring_generation.as_deref()),
        ));
        let live = monitor_flag(s, "live");
        for (i, (key, title, weight)) in columns.iter().enumerate() {
            let cw = if i == 0 {
                180.
            } else {
                (w - 180.) * weight / total
            };
            ui.push_id(*key, |ui| {
                label(
                    ui,
                    rect(o, p, x, 0., cw - 4., 52.),
                    *title,
                    crate::presentation::CAPTION_FOREGROUND,
                    true,
                    false,
                );
                for (n, row) in rows.iter().enumerate() {
                    let r = rect(o, p, x + 2., 58. + n as f32 * 25., cw - 6., 25.);
                    let background = if live
                        && ((key.starts_with("short") || key.starts_with("long"))
                            && !key.ends_with("Strike")
                            || matches!(
                                *key,
                                "netSpread"
                                    | "tradeValue"
                                    | "tradePnl"
                                    | "otmProbability"
                                    | "forwardPrice"
                            )) {
                        let id = change_scope.with((key, n));
                        let value = number(row, key).filter(|value| value.is_finite());
                        ui.ctx().data_mut(|data| {
                            let cell = data.get_temp_mut_or_insert_with(id, || LegValueChange {
                                revision,
                                value,
                                background: BLACK,
                            });
                            cell.update(revision, value)
                        })
                    } else {
                        BLACK
                    };
                    field(
                        ui,
                        r,
                        &format!("condor-{key}-{n}"),
                        formatted(row, key),
                        background,
                        if i == 0 {
                            Color32::MAGENTA
                        } else if key.ends_with("Strike") {
                            Color32::from_rgb(0, 191, 255)
                        } else {
                            WHITE
                        },
                        i == 0 || key.ends_with("Strike"),
                    );
                }
            });
            x += cw;
        }
    }
    /// Plot the published recent forward-spread series and trade-limit guides with Eastern-time labels.
    fn condor_chart(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        trade: &EstablishedTradeDefinition,
        _position: Option<&StrategyPositionSnapshot>,
        bounds: egui::Rect,
    ) {
        let p = ui.ctx().pixels_per_point();
        let o = bounds.min;
        let w = bounds.width() * p;
        let h = bounds.height() * p;
        self.controls.insert("condor-chart".into(), bounds);
        let chart_error = s
            .result
            .context
            .get("chartError")
            .filter(|error| !error.is_empty());
        label(
            ui,
            rect(o, p, 0., 8., w, 35.),
            chart_error.map_or(
                if monitor_flag(s, "chartLoading") {
                    "Loading six-hour chart history..."
                } else {
                    "Iron Condor Net / Forward Spread (price points)"
                },
                String::as_str,
            ),
            if chart_error.is_some() {
                Color32::RED
            } else {
                WHITE
            },
            true,
            false,
        );
        let display = self.display_cache.as_ref().unwrap().model.clone();
        let mut points = display.chart_points.clone();
        let mut forward_points = display.forward_chart_points.clone();
        let limit = one(&display, "limits").limits();
        let loss = number(
            &limit.and_then(|limit| limit.trade_limit.as_ref()),
            "maxLossLimit",
        )
        .map(|value| -value.abs());
        let win = number(
            &limit.and_then(|limit| limit.trade_limit.as_ref()),
            "maxProfitLimit",
        )
        .map(f64::abs);
        let mut end = display.chart_end_utc_seconds;
        if monitor_flag(s, "live") {
            let realtime = one(&display, "realtime").realtime();
            let clock_id = egui::Id::new((
                "condor-chart-clock",
                &self.loaded_spread_id,
                s.result.context.get("activeTradeTab"),
                realtime.and_then(|value| value.monitoring_generation.as_deref()),
            ));
            let mut clock = ui
                .ctx()
                .data_mut(|data| data.get_temp::<SpreadChartClock>(clock_id))
                .unwrap_or_default();
            let plan = realtime.and_then(|value| value.plan.as_ref());
            if let (Some(price), Some(at)) = (
                number(
                    &plan.and_then(|plan| plan.iron_condor_trade_plan_snapshot.as_deref()),
                    "combinedSpreadPrice",
                ),
                plan.and_then(|plan| plan.calculated_at_utc.as_ref())
                    .map(|value| value.as_str()),
            ) {
                if let Ok(at) = chrono::DateTime::parse_from_rfc3339(at) {
                    if clock.accept(std::time::Instant::now()) {
                        let time = at.timestamp_millis() as f64 / 1000.;
                        if self
                            .loaded_spread_points
                            .last()
                            .is_none_or(|point| point[0] < time)
                        {
                            if self.loaded_spread_points.last().is_some_and(|point| {
                                (point[0] / 60.).floor() == (time / 60.).floor()
                            }) {
                                self.loaded_spread_points.pop();
                            }
                            self.loaded_spread_points.push([time, price]);
                        }
                        if let Some(forward) = number(
                            &plan.and_then(|plan| plan.iron_condor_trade_plan_snapshot.as_deref()),
                            "forwardSpreadForChart",
                        ) {
                            if self
                                .loaded_forward_points
                                .last()
                                .is_none_or(|point| point[0] < time)
                            {
                                if self.loaded_forward_points.last().is_some_and(|point| {
                                    (point[0] / 60.).floor() == (time / 60.).floor()
                                }) {
                                    self.loaded_forward_points.pop();
                                }
                                self.loaded_forward_points.push([time, forward]);
                            }
                        }
                        end = Some(end.unwrap_or(time).max(time));
                    }
                    ui.ctx().data_mut(|data| data.insert_temp(clock_id, clock));
                }
            }
            if let Some(last) = self.loaded_spread_points.last() {
                end = Some(end.unwrap_or(last[0]).max(last[0]));
            }
            if let Some(end) = end {
                self.loaded_spread_points
                    .retain(|point| point[0] >= end - 21600.);
            }
            points.extend_from_slice(&self.loaded_spread_points);
            if let Some(end) = end {
                self.loaded_forward_points
                    .retain(|point| point[0] >= end - 21600.);
            }
            forward_points.extend_from_slice(&self.loaded_forward_points);
        }
        if let Some(end) = end {
            points.retain(|point| point[0] >= end - 21600. && point[0] <= end);
        }
        if let Some(end) = end {
            forward_points.retain(|point| point[0] >= end - 21600. && point[0] <= end);
        }
        coalesce_chart_minutes(&mut forward_points);
        coalesce_chart_minutes(&mut points);
        at(
            ui,
            rect(o, p, 16., 47., w - 32., h - 145.),
            "condor-spread-chart",
            |ui| {
                ui.visuals_mut().extreme_bg_color = BLACK;
                let mut chart = egui_plot::Plot::new(("condor-spread", format!("{:?}", trade.id)));
                if let Some(end) = end {
                    chart = chart.include_x(end - 21600.).include_x(end);
                }
                if let Some(loss) = loss {
                    chart = chart.include_y(loss);
                }
                if let Some(win) = win {
                    chart = chart.include_y(win);
                }
                chart
                    .width(ui.available_width())
                    .height(ui.available_height())
                    .show_background(false)
                    .show_grid(false)
                    .x_axis_formatter(|mark, _| {
                        chrono::DateTime::from_timestamp(mark.value as i64, 0)
                            .map(|d| {
                                d.with_timezone(&chrono_tz::America::New_York)
                                    .format("%m-%d %H:%M %Z")
                                    .to_string()
                            })
                            .unwrap_or_default()
                    })
                    .show(ui, |plot| {
                        if let Some(loss) = loss {
                            plot.hline(
                                egui_plot::HLine::new("LossLimit", loss).color(Color32::RED),
                            );
                        }
                        if let Some(win) = win {
                            plot.hline(
                                egui_plot::HLine::new("WinLimit", win).color(Color32::GREEN),
                            );
                        }
                        if !forward_points.is_empty() {
                            plot.points(
                                egui_plot::Points::new("ForwardSpread", forward_points.clone())
                                    .color(Color32::from_rgb(0, 191, 255))
                                    .radius(2.0_f32),
                            );
                            plot.line(
                                egui_plot::Line::new("ForwardSpread", forward_points)
                                    .color(Color32::from_rgb(0, 191, 255)),
                            );
                        }
                        plot.points(
                            egui_plot::Points::new("NetSpread", points.clone())
                                .color(Color32::YELLOW)
                                .radius(3.0_f32),
                        );
                        if points.len() > 1 {
                            plot.line(
                                egui_plot::Line::new("NetSpread", points).color(Color32::YELLOW),
                            );
                        }
                    });
            },
        );
        let colors = [
            Color32::RED,
            Color32::GREEN,
            Color32::from_rgb(0, 191, 255),
            Color32::YELLOW,
        ];
        let names = ["LossLimit", "WinLimit", "ForwardSpread", "NetSpread"];
        let legend_w = (w - 32.) / 4.;
        for (n, name) in names.iter().enumerate() {
            let col = if n == 1 {
                0
            } else if n == 0 {
                0
            } else {
                n - 1
            };
            let x = 20. + col as f32 * legend_w;
            let y = h - 70. + if n == 1 { 24. } else { 0. };
            ui.painter().line_segment(
                [
                    rect(o, p, x, y + 12., 0., 0.).min,
                    rect(o, p, x + 30., y + 12., 0., 0.).min,
                ],
                egui::Stroke::new(2.0_f32, colors[n]),
            );
            label(
                ui,
                rect(o, p, x + 36., y, legend_w - 36., 24.),
                *name,
                WHITE,
                false,
                false,
            );
        }
    }
    /// Select/shape the published spread data for this trade, preferring the authoritative realtime snapshot when available.
    fn condor_spreads(
        &self,
        _ui: &mut egui::Ui,
        s: &ContractPublication,
        trade: &EstablishedTradeDefinition,
        position: Option<&StrategyPositionSnapshot>,
    ) -> Vec<SpreadDisplayRow> {
        let display = &self.display_cache.as_ref().unwrap().model;
        let long = s
            .result
            .context
            .get("compositionType")
            .is_some_and(|value| value == "LongIronCondor");
        if let Some(realtime) = one(display, "realtime").realtime() {
            return realtime_spreads(realtime, long);
        }
        let legs = trade.legs.as_deref().unwrap_or(&[]);
        let marks = position
            .and_then(|position| position.legs.as_deref())
            .unwrap_or(&[]);
        let observations = values(display, "observation")
            .iter()
            .filter_map(TradeDisplayRow::observation)
            .map(|row| &row.value)
            .collect::<Vec<_>>();
        let mut rows = Vec::with_capacity(2);
        for (right, name) in [(2, "Put"), (1, "Call")] {
            let mut row = SpreadDisplayRow {
                spread: format!("{name}{}Spread", if long { "Debit" } else { "Credit" }),
                ..Default::default()
            };
            for bought in [false, true] {
                if let Some(leg) = legs.iter().find(|leg| {
                    leg.put_call == Some(right)
                        && (leg.signed_quantity.unwrap_or_default() > 0) == bought
                }) {
                    let quote = observations
                        .iter()
                        .find(|quote| quote.contract_id == leg.contract_id)
                        .copied();
                    let greeks = quote.filter(|quote| quote.greeks_available != Some(false));
                    let display = LegDisplay {
                        strike: leg.strike.clone(),
                        price: marks
                            .iter()
                            .find(|mark| mark.trade_leg_id == leg.trade_leg_id)
                            .and_then(|mark| mark.current_price.clone()),
                        bid: quote.and_then(|quote| quote.bid_price),
                        ask: quote.and_then(|quote| quote.ask_price),
                        delta: greeks.and_then(|quote| quote.delta),
                        gamma: greeks.and_then(|quote| quote.gamma),
                        theta: greeks.and_then(|quote| quote.theta),
                        vega: greeks.and_then(|quote| quote.vega),
                        rho: greeks.and_then(|quote| quote.rho),
                        iv: greeks.and_then(|quote| quote.implied_volatility),
                    };
                    if bought {
                        row.long = display
                    } else {
                        row.short = display
                    }
                }
            }
            let spread_marks = marks
                .iter()
                .filter(|mark| {
                    legs.iter().any(|leg| {
                        leg.trade_leg_id == mark.trade_leg_id && leg.put_call == Some(right)
                    })
                })
                .collect::<Vec<_>>();
            if spread_marks.len() == 2 {
                let quantity = spread_marks
                    .iter()
                    .filter_map(|mark| number(*mark, "signedQuantity"))
                    .map(f64::abs)
                    .fold(f64::INFINITY, f64::min);
                let (mut value, mut pnl, mut spread, mut monetary) = (0., 0., 0., true);
                for mark in spread_marks {
                    let signed = number(mark, "signedQuantity").unwrap_or_default();
                    let current = number(mark, "currentPrice").unwrap_or_default();
                    let tick = mark.option_tick.as_ref().or(mark.option_risk.as_ref());
                    let multiplier = tick
                        .filter(|tick| tick.contract_id == mark.contract_id)
                        .and_then(|tick| tick.pricing_inputs.as_ref())
                        .and_then(|inputs| number(inputs, "multiplier"))
                        .filter(|value| *value > 0.);
                    monetary &= multiplier.is_some();
                    let multiplier = multiplier.unwrap_or_default();
                    spread += current * signed;
                    value += current * signed * multiplier;
                    pnl += (current - number(mark, "openingPrice").unwrap_or_default())
                        * signed
                        * multiplier;
                }
                if quantity > 0. {
                    row.net_spread = Some((spread / quantity).abs());
                }
                row.trade_value = monetary.then_some(if long { value } else { value.abs() });
                row.trade_pnl = monetary.then_some(pnl);
            }
            let plan = position
                .and_then(|position| position.value_date.as_ref())
                .and_then(|date| display.plan_dates.get(date.as_str()))
                .and_then(|index| values(display, "plan").get(*index))
                .and_then(TradeDisplayRow::plan)
                .and_then(|plan| plan.iron_condor_trade_plan_snapshot.as_deref());
            if let Some(plan) = plan {
                row.forward_price = number(
                    plan,
                    if right == 2 {
                        "putForwardPrice"
                    } else {
                        "callForwardPrice"
                    },
                )
                .map(|value| if long { value } else { value.abs() });
                row.otm_probability = if right == 2 {
                    plan.put_otm_probability
                } else {
                    plan.call_otm_probability
                };
            }
            rows.push(row);
        }

        rows
    }
}

/// Local regression coverage for the surrounding module invariants.
#[cfg(test)]
mod eastern_chart_time_tests {
    /// Regression check: eastern labels use the offset for the snapshot date.
    #[test]
    fn eastern_labels_use_the_offset_for_the_snapshot_date() {
        for (utc, expected) in [
            ("2026-10-16T20:33:09Z", "10-16 16:33 EDT"),
            ("2026-12-16T20:33:09Z", "12-16 15:33 EST"),
        ] {
            let at = chrono::DateTime::parse_from_rfc3339(utc).unwrap();
            assert_eq!(
                at.with_timezone(&chrono_tz::America::New_York)
                    .format("%m-%d %H:%M %Z")
                    .to_string(),
                expected
            );
        }
    }
}

#[cfg(test)]
mod spread_chart_sampling_tests {
    #[test]
    fn database_baseline_survives_new_session_live_points_and_boundary_duplicates() {
        let mut points = vec![[60., -1.], [120., -2.], [180., -3.]];
        points.extend_from_slice(&[[185., -3.5], [240., -4.]]);
        super::coalesce_chart_minutes(&mut points);
        assert_eq!(
            points,
            vec![[60., -1.], [120., -2.], [185., -3.5], [240., -4.]]
        );
    }
    use super::{SpreadChartClock, coalesce_chart_minutes};
    use std::time::{Duration, Instant};

    #[test]
    fn history_and_live_points_keep_only_the_latest_sample_per_minute() {
        let mut points = vec![[61., -11.], [40., -10.], [65., -12.], [120., -13.]];
        coalesce_chart_minutes(&mut points);
        assert_eq!(points, vec![[40., -10.], [65., -12.], [120., -13.]]);
    }

    #[test]
    fn chart_accepts_first_point_then_waits_sixty_seconds() {
        let mut clock = SpreadChartClock::default();
        let start = Instant::now();
        assert!(clock.accept(start));
        for seconds in 0..60 {
            assert!(!clock.accept(start + Duration::from_secs(seconds)));
        }
        assert!(clock.accept(start + Duration::from_secs(60)));
        assert!(!clock.accept(start + Duration::from_secs(119)));
        assert!(clock.accept(start + Duration::from_secs(120)));
    }
}

#[cfg(test)]
mod trade_pnl_bar_tests {
    use super::trade_pnl_fraction;
    use serde_json::json;

    #[test]
    fn gain_and_loss_use_their_own_trade_maximum_not_fund_or_exit_limits() {
        let limits = json!({"maxProfit": 2000., "maxLoss": -8000.,
            "maxProfitLimit": 500., "maxLossLimit": 1000., "fundBalance": 100000.});
        assert_eq!(trade_pnl_fraction(Some(500.), &limits), Some(0.25));
        assert_eq!(trade_pnl_fraction(Some(-2000.), &limits), Some(0.25));
        assert_eq!(trade_pnl_fraction(Some(0.), &limits), Some(0.));
        assert_eq!(
            trade_pnl_fraction(Some(-2000.), &json!({"maxLoss":8000.})),
            Some(0.25)
        );
    }

    #[test]
    fn preserves_over_limit_percentage_and_requires_valid_pnl_and_maximum() {
        assert_eq!(
            trade_pnl_fraction(Some(-1500.), &json!({"maxLoss":1000.})),
            Some(1.5)
        );
        for limits in [json!({}), json!({"maxLoss":0.}), json!({"maxProfit":2000.})] {
            assert_eq!(trade_pnl_fraction(Some(-500.), &limits), None);
        }
        assert_eq!(trade_pnl_fraction(None, &json!({"maxProfit":2000.})), None);
        assert_eq!(
            trade_pnl_fraction(Some(f64::NAN), &json!({"maxProfit":2000.})),
            None
        );
    }
}
