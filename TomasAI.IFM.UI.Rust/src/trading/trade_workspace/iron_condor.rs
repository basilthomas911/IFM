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
use serde_json::{Value, json};
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
                (Some(previous), Some(current)) if current > previous => Color32::from_rgb(0, 100, 0),
                (Some(previous), Some(current)) if current < previous => Color32::from_rgb(139, 0, 0),
                _ => BLACK,
            };
            self.revision = revision;
            if value.is_some() { self.value = value; }
        }
        self.background
    }
}
/// Clone the first decoded trade row in a group, or return null when the group is absent.
fn one(display: &super::display_cache::TradeDisplayData, kind: &str) -> Value {
    values(display, kind).first().cloned().unwrap_or_default()
}
/// Borrow all decoded rows for a requested trade group; an absent group yields an empty slice.
fn values<'a>(display: &'a super::display_cache::TradeDisplayData, kind: &str) -> &'a [Value] {
    display.values(kind)
}
/// Read a boolean eligibility/state flag from the published monitor context.
fn monitor_flag(s:&ContractPublication,key:&str)->bool {
    let name=format!("monitor{}{}",key[..1].to_uppercase(),&key[1..]);
    s.result.context.get(&name).map(|value|value=="True").unwrap_or_else(||flag(s,key))
}
// The live path renders one managed cache revision; historical rows retain their saved-data path.
/// Shape authoritative realtime position-leg data into call/put spread display rows. Does not calculate Greeks or revalue the position.
fn realtime_spreads(snapshot: &Value, long: bool) -> Vec<Value> {
    let legs = snapshot["legs"].as_array().cloned().unwrap_or_default();
    [(2, "Put"), (1, "Call")].into_iter().map(|(right, name)| {
        let mut row = json!({"spread":format!("{name}{}Spread",if long {"Debit"}else{"Credit"})});
        for (bought, prefix) in [(false, "short"), (true, "long")] {
            if let Some(leg) = legs.iter().find(|leg| leg["definition"]["putCall"] == right
                && (number(&leg["definition"], "signedQuantity").unwrap_or_default() > 0.) == bought) {
                row[format!("{prefix}Strike")] = leg["definition"]["strike"].clone();
                for (field, suffix, risk) in [
                    ("bidPrice","Bid",false),("askPrice","Ask",false),
                    ("delta","Delta",true),("gamma","Gamma",true),("theta","Theta",true),
                    ("vega","Vega",true),("rho","Rho",true),("impliedVolatility","IV",true)
                ] {
                    // Freshness is status metadata. A pending/failed calculation must not erase retained values.
                    let retained = if leg["lastValidRisk"].is_object() { &leg["lastValidRisk"] }
                        else if leg["position"]["optionRisk"].is_object() { &leg["position"]["optionRisk"] }
                        else if leg["pricingStatus"] == "Available" { &leg["observation"] } else { &Value::Null };
                    row[format!("{prefix}{suffix}")] = if risk { retained[field].clone() }
                        else { leg["observation"][field].clone() };
                }
            }
        }
        if let Some(spread) = snapshot["spreads"].as_array().and_then(|spreads|spreads.iter().find(|spread|spread["putCall"]==right)) {
            for field in ["netSpread","tradeValue","tradePnl","otmProbability","forwardPrice"] {
                row[field] = if !long && matches!(field,"netSpread" | "tradeValue" | "forwardPrice") {
                    number(spread,field).map(|value|json!(value.abs())).unwrap_or(Value::Null)
                } else { spread[field].clone() };
            }
        }
        row
    }).collect()
}
/// Local regression coverage for the surrounding module invariants.
#[cfg(test)]
mod realtime_cache_tests {
    use super::*;
    #[test]
    fn leg_value_colors_follow_updates_not_repaints_and_preserve_valid_baselines() {
        let mut cell = LegValueChange { revision: 1, value: Some(-0.4), background: BLACK };
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
        let mut snapshot=json!({"legs":[{"definition":{"putCall":2,"signedQuantity":-1,"strike":5000},
            "quoteStatus":"Stale","pricingStatus":"Pending", "observation":{"bidPrice":9,"askPrice":11,"delta":0},
            "lastValidRisk":{"delta":-0.4,"gamma":0.01,"vega":12,"rho":0.1,"theta":-3,"impliedVolatility":0.2}}]});
        let rows=realtime_spreads(&snapshot,false);
        assert_eq!(rows[0]["shortDelta"],-0.4); assert_eq!(rows[0]["shortBid"],9);
        snapshot["legs"][0]["lastValidRisk"]["delta"]=json!(0);
        snapshot["legs"][0]["pricingStatus"]=json!("Stale");
        assert_eq!(realtime_spreads(&snapshot,false)[0]["shortDelta"],0);
        let detail=retained_plan_values(&json!({"forwardLossRatio":null,"dailyPnl":0}),
            &json!({"forwardLossRatio":0.25,"dailyPnl":10}));
        assert_eq!(detail["forwardLossRatio"],0.25); assert_eq!(detail["dailyPnl"],0);
    }

    /// Regression check: whole snapshot binds all greeks and forward values without local valuation.
    #[test]
    fn whole_snapshot_binds_all_greeks_and_forward_values_without_local_valuation() {
        let snapshot=json!({"legs":[{"definition":{"putCall":2,"signedQuantity":-2,"strike":5000},
            "quoteStatus":"Available","pricingStatus":"Available",
            "observation":{"bidPrice":9,"askPrice":11,"delta":-0.4,"gamma":0.01,"theta":-3,"vega":12,"rho":0.1,"impliedVolatility":0.2}}],
            "spreads":[{"putCall":2,"netSpread":-2,"tradeValue":-200,"tradePnl":10,"otmProbability":0.7,"forwardPrice":-3}]});
        let rows=realtime_spreads(&snapshot,false);
        assert_eq!(rows.len(),2);
        assert_eq!(rows[0]["shortVega"],12);
        assert_eq!(rows[0]["shortRho"],0.1);
        assert_eq!(rows[0]["forwardPrice"],3.0);
        assert_eq!(rows[0]["tradeValue"],200.0);
        assert!(rows[1]["shortBid"].is_null());
        let mut stale=snapshot;
        stale["legs"][0]["pricingStatus"]=json!("Stale");
        let rows=realtime_spreads(&stale,false);
        assert!(rows[0]["shortDelta"].is_null());
        assert_eq!(rows[0]["shortBid"],9);
    }
}
/// Retain available numeric plan fields for display; current plan status still reports readiness.
fn retained_plan_values(current: &Value, previous: &Value) -> Value {
    let mut fields = current.as_object().cloned().unwrap_or_default();
    if let Some(previous) = previous.as_object() {
        for (key, value) in previous {
            if value.is_number() || value.as_str().is_some_and(|v| v.parse::<f64>().is_ok()) {
                if fields.get(key).is_none_or(Value::is_null) { fields.insert(key.clone(), value.clone()); }
            }
        }
    }
    Value::Object(fields)
}

/// Read a numeric property from a JSON-shaped published value for formatting/plotting; missing values remain absent.
fn number(v: &Value, key: &str) -> Option<f64> {
    v[key]
        .as_f64()
        .or_else(|| v[key].as_str().and_then(|s| s.parse().ok()))
}
/// Convert a published metric into its display text, preserving unavailable values instead of inventing a financial result.
fn formatted(v: &Value, key: &str) -> String {
    if v[key].is_null() {
        return "N/A".into();
    }
    if let Some(n) = number(v, key) {
        return if key.contains("Probability") || key.ends_with("IV") || key == "maxReturn" {
            format!("{:.2}%", n * 100.)
        } else if key.ends_with("Strike")
            || ["orderId", "tradeId", "quantity", "daysToExpiry"].contains(&key)
        {
            format!("{n:.0}")
        } else {
            format!("{n:.4}")
                .trim_end_matches('0')
                .trim_end_matches('.')
                .to_owned()
        };
    }
    text(v, key)
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
fn table(
    ui: &mut egui::Ui,
    r: egui::Rect,
    id: &str,
    rows: &[Value],
    columns: &[(&str, &str, f32)],
    selected: Option<usize>,
) -> Option<usize> {
    table_window(ui, r, id, rows, columns, selected, None).0
}
/// Render a logical table using the loaded page. The window argument is (loaded offset, total row count); return clicked global index, missing (offset, page size), and first visible global index.
fn table_window(
    ui: &mut egui::Ui, r: egui::Rect, id: &str, rows: &[Value],
    columns: &[(&str, &str, f32)], selected: Option<usize>,
    window: Option<(usize, usize)>,
) -> (Option<usize>, Option<(usize, usize)>, Option<usize>) {
    let mut missing = None;
    let mut first_visible = None;
    let clicked = at(ui, r, id, |ui| {

        ui.set_clip_rect(r.intersect(ui.clip_rect()));
        ui.set_max_height(r.height());
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
        } else { egui::scroll_area::ScrollBarVisibility::VisibleWhenNeeded };
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
                let mut grid = egui_extras::TableBuilder::new(ui)
                    .id_salt(id)
                    .scroll_bar_visibility(visibility)
                    .striped(false)
                    .sense(egui::Sense::click())
                    .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                    .auto_shrink([false, false])
                    .min_scrolled_height((r.height() - row_height - scroll_bar).max(0.))
                    .max_scroll_height((r.height() - row_height - scroll_bar).max(0.));
                for (index, (_, _, width)) in columns.iter().enumerate() {
                    grid = grid.column(
                        if index + 1 == columns.len() {
                            egui_extras::Column::remainder().at_least(*width / p).clip(true).resizable(true)
                        } else {
                            egui_extras::Column::initial(*width / p).at_least(40. / p).clip(true).resizable(true)
                        },
                    );
                }
                grid.header(row_height, |mut row| {
                    for (_, title, _) in columns {
                        row.col(|ui| {
                            ui.painter().rect_filled(ui.max_rect(), 0., COMMAND);
                            ui.add(
                                egui::Label::new(egui::RichText::new(*title).color(crate::presentation::CAPTION_FOREGROUND))
                                    .truncate(),
                            );
                        });
                    }
                })
                .bordered_body(row_height, |body| {
                    body.rows(row_height, window.map_or(rows.len(), |(_, total)| total), |mut row| {
                        let absolute_index = row.index();
                        first_visible.get_or_insert(absolute_index);
                        let offset = window.map_or(0, |(offset, _)| offset);
                        let index = absolute_index.checked_sub(offset);
                        let datum = index.and_then(|index| rows.get(index));
                        if datum.is_none() && window.is_some() && missing.is_none() {
                            missing = Some((absolute_index.saturating_sub(2), ((r.height() / row_height).ceil() as usize + 4).clamp(1, 100)));
                        }
                        let empty = serde_json::Value::Null;
                        let datum = datum.unwrap_or(&empty);
                        row.set_selected(selected == Some(absolute_index));
                        let color = if id.starts_with("condor-plans") && datum["isComplete"] != true {
                            Color32::YELLOW
                        } else {
                            WHITE
                        };
                        for (key, _, _) in columns {
                            row.col(|ui| {
                                let value = if datum.is_null() && window.is_some() {
                                    if *key == columns[0].0 { "Loading?".to_owned() } else { String::new() }
                                } else { formatted(datum, key) };
                                ui.add(
                                    egui::Label::new(egui::RichText::new(&value).color(color))
                                        .truncate(),
                                )
                                .on_hover_text(value);
                            });
                        }
                        if row.response().clicked() {
                            clicked = Some(absolute_index);
                        }
                    })
                });
            });
        ui.painter().rect_stroke(
            r,
            0.,
            egui::Stroke::new(1.0_f32, GRAY),
            egui::StrokeKind::Inside,
        );
        clicked
    });
    (clicked, missing, first_visible)
}
/// Choose an indicator color from the published monitor severity/state.
fn severity(v: &Value) -> Color32 {
    if v["isComplete"] != true {
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
        if self.last_sample.is_some_and(|last| now.duration_since(last).as_secs() < 60) {
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
        let trade = one(&display, "established");
        if trade.is_null() { self.clear_trade_display(); return None; }
        let identity = trade["id"].to_string();
        if self.loaded_spread_id != identity {
            self.loaded_forward_points.clear();
            if !self.loaded_spread_id.is_empty() {
                self.loaded_view_states.insert(self.loaded_spread_id.clone(),(std::mem::take(&mut self.loaded_spread_points),self.draft.remove("positionIndex")));
            }
            self.loaded_spread_id = identity.clone();
            if let Some((points,index))=self.loaded_view_states.remove(&identity) {
                self.loaded_spread_points=points;
                if let Some(index)=index {self.draft.insert("positionIndex".into(),index);}
            }
        }
        let mut action = None;
        let panel=egui::CentralPanel::default().frame(egui::Frame::new().fill(BLACK)).show(ctx,|ui|{
            let p=ctx.pixels_per_point();
            let mut bounds=ui.max_rect();
            if let Some(tab_action)=self.draw_workspace_tabs(ui,s,bounds) {action=Some(tab_action);}
            bounds.min.y+=34./p;
            let o=bounds.min;
            let w=bounds.width()*p;let h=bounds.height()*p;
            // Same split calculation as IFormControl.Resize: bottom third, 183px marks,
            // 120px four-contract pane, 79px limits, remaining width split after history, with 120px reassigned to the chart.
            let log=(h-7.)/3.;let upper=h-7.-log;
            let marks=183.;let graph_h=(upper-6.-marks).max(180.);
            let chart_w=(((w-650.)/2.)+120.).min((w-640.).max(0.)).max(0.);let show_chart=chart_w>=220. && upper>=marks+180.;
            let left=if show_chart{w-chart_w}else{w};
            let history_h=(graph_h-205.).max(75.);
            let rr=|x,y,ww,hh|rect(o,p,x,y,ww,hh);
            ui.spacing_mut().item_spacing=egui::Vec2::ZERO;
            let description=format!("{} | {} | {}.{}.{}.{} | {}",s.result.context.get("fundName").cloned().unwrap_or_default(),s.result.context.get("compositionType").cloned().unwrap_or_else(||"Iron Condor".into()),text(&trade["id"],"portfolioId"),text(&trade["id"],"fundId"),text(&trade["id"],"orderId"),text(&trade["id"],"tradeId"),text(&trade,"status"));
            label(ui,rr(2.,0.,left-4.,34.),description,crate::presentation::CAPTION_FOREGROUND,false,false);

            let history=values(&display,"position");
            let selected=self.draft.get("positionIndex").and_then(|i|i.parse::<usize>().ok()).filter(|i|*i<history.len()).or_else(||history.len().checked_sub(1));
            let history_rect=rr(2.,35.,left-4.,history_h-34.);
            self.controls.insert("condor-history".into(),history_rect);
            let clicked=ui.add_enabled_ui(monitor_flag(s,"canSelectPosition"),|ui|table(ui,history_rect,"condor-history",&history,&[
                ("orderId","Order Id",100.),("tradeId","Trade Id",100.),("tradeType","Trade Type",250.),("valueDate","Value Date",100.),("daysToExpiry","Days To Expiry",120.),("tradeStatus","Trade Status",150.),("commission","Commission",115.),("netSpread","Net Spread",115.),("tradePnl","Daily P&L",120.)
            ],selected)).inner;
            if let Some(index)=clicked{self.draft.insert("positionIndex".into(),index.to_string());self.draft.insert("index".into(),index.to_string());action=Some("position");}
            let legs=trade["legs"].as_array().cloned().unwrap_or_default();
            let details=json!({"orderId":trade["id"]["orderId"],"tradeId":trade["id"]["tradeId"],"tradeType":s.result.context.get("compositionType"),"quantity":legs.iter().filter_map(|l|number(l,"signedQuantity")).map(f64::abs).reduce(f64::min),"tradeDate":text(&trade,"establishedAtUtc").get(..10).unwrap_or_default(),"expiry":legs.first().map(|v|v["expiry"].clone()),"state":trade["status"],"action":s.result.context.get("tradeAction")});
            let contracts_w=216.;
            let info_rect=rr(2.,history_h+6.,left-contracts_w-2.,120.);
            self.controls.insert("condor-trade-info".into(),info_rect);
            table(ui,info_rect,"condor-trade-info",&[details],&[("orderId","Order Id",120.),("tradeId","Trade Id",120.),("tradeType","Trade Type",150.),("quantity","Quantity",115.),("tradeDate","Trade Date",120.),("expiry","Maturity Date",120.),("state","Trade State",120.),("action","Trade Action",120.)],None);
            let contracts_rect=rr(left-contracts_w,history_h+6.,contracts_w-2.,120.);
            self.controls.insert("condor-contracts".into(),contracts_rect);
            table(ui,contracts_rect,"condor-contracts",&legs,&[("contractId","Option Contract Ids",216.)],None);
            let limit=one(&display,"limits");let mut limit_row=limit["tradeLimit"].clone();
            if limit_row.is_object(){limit_row["orderId"]=limit["orderId"].clone();limit_row["fundBalance"]=limit["fundBalance"].clone();}
            let limits_rect=rr(2.,graph_h-79.,left-4.,79.);
            self.controls.insert("condor-limits".into(),limits_rect);
            table(ui,limits_rect,"condor-limits",&[limit_row],&[("orderId","Order Id",100.),("riskMargin","Risk Margin",120.),("maxProfit","Max Profit",90.),("maxLoss","Max Loss",90.),("maxReturn","Max Return",110.),("maxLossLimit","Max Loss Limit",125.),("maxProfitLimit","Max Profit Limit",115.),("minProfitTarget","Min Profit Target",120.),("dailyProfitTarget","Daily Profit Target",140.),("fundBalance","Fund Balance",150.)],None);
            let position=one(&display,"position-details");
            if show_chart {self.condor_chart(ui,s,&trade,&position,rr(left,0.,chart_w,graph_h));}
            ui.painter().hline(bounds.x_range(),o.y+graph_h/p,egui::Stroke::new(1.0_f32,GRAY));
            let plans=values(&display,"plan");let realtime=one(&display,"realtime");
            let plan=if realtime.is_object(){realtime["plan"].clone()}else{plans.first().cloned().unwrap_or_default()};let detail=retained_plan_values(&plan["ironCondorTradePlanSnapshot"],
                &realtime["lastAvailablePlan"]["ironCondorTradePlanSnapshot"]);
            let history_row=selected.and_then(|i|history.get(i)).cloned().unwrap_or_default();
            let mut net=number(&history_row,"netSpread").or_else(||number(&trade,"openingValue"));
            if realtime.is_object(){net=number(&realtime["summary"],"netSpread");}
            else if let Some(marks)=position["legs"].as_array(){let qty=marks.iter().filter_map(|l|number(l,"signedQuantity")).map(f64::abs).reduce(f64::min).unwrap_or_default();if qty>0.{net=Some(marks.iter().map(|l|number(l,"currentPrice").unwrap_or_default()*number(l,"signedQuantity").unwrap_or_default()).sum::<f64>()/qty);}}
            let daily=number(&detail,"dailyPnl").or_else(||number(&history_row,"tradePnl"));
            let asset=if realtime.is_object(){formatted(&realtime["summary"],"underlyingPrice")}else{formatted(&detail,"underlyingPrice")};
            let summary_y=graph_h+6.;
            let top_fields=[("Value Date:",formatted(&history_row,"valueDate"),BLACK),
                ("Trade Status:",if let Some(message)=s.result.context.get("monitorStatus").filter(|message| !message.is_empty()){message.clone()}else if plan.is_null(){text(&trade,"status")}else{format!("{}: {}",text(&plan,"state"),text(&plan,"explanation"))},BLACK),
                ("DTE:",formatted(&history_row,"daysToExpiry"),BLACK),
                ("Net Spread:",net.map(|n|format!("{n:.2}")).unwrap_or_else(||"N/A".into()),BLACK),
                ("Daily P&L:",daily.map(|n|format!("{n:.2}")).unwrap_or_else(||"N/A".into()),BLACK),
                ("Asset Price:",asset,BLACK),
                ("Fwd Loss:",number(&detail,"forwardLossRatio").map(|n|format!("{:.1}%",n*100.)).unwrap_or_else(||"N/A".into()),severity(&detail))];
            // Full-width proportional header, with the same caption/value weights as tableLayoutPanel1.
            let weights=[120.,120.,120.,90.,55.,45.,120.,80.,110.,96.,114.,90.,88.,70.];
            let feed_w=165.;let usable=w-feed_w-16.;let total:f32=weights.iter().sum();let mut x=7.;
            for (n,(caption,value,bg)) in top_fields.into_iter().enumerate(){
                let cw=usable*weights[n*2]/total;let vw=usable*weights[n*2+1]/total;
                label(ui,rr(x,summary_y+3.,cw-5.,28.),caption,crate::presentation::CAPTION_FOREGROUND,true,false);x+=cw;
                let r=rr(x,summary_y+4.,vw-5.,26.);field(ui,r,&format!("condor-summary-{n}"),value,bg,WHITE,n==3||n==6);x+=vw;
            }
            at(ui,rr(w-feed_w-4.,summary_y+4.,feed_w,26.),"condor-feed",|ui|{
                let live=monitor_flag(s,"live");
                let off=egui::RichText::new("LiveFeed OFF").color(Color32::RED);
                let on=egui::RichText::new("LiveFeed ON").color(Color32::from_rgb(0,255,0));
                ui.visuals_mut().disabled_alpha=1.0;
                ui.add_enabled_ui(monitor_flag(s,"canLive")||live,|ui|{
                    egui::ComboBox::from_id_salt("condor-live-feed").selected_text(if live{on.clone()}else{off.clone()}).width(feed_w/p-6.).show_ui(ui,|ui|{
                        if ui.selectable_label(!live,off).clicked()&&live{action=Some("live-off");}
                        if ui.selectable_label(live,on).clicked()&&!live{action=Some("live-on");}
                    });
                });
            });
            let cumulative=number(&position,"unrealizedPnl").map(|n|n+number(&position,"realizedPnl").unwrap_or_default());
            let balance=number(&limit,"fundBalance");let progress=rr(10.,summary_y+45.,w*0.22,14.);
            ui.painter().rect_filled(progress,0.,Color32::from_gray(230));
            if let (Some(pnl),Some(balance))=(cumulative,balance.filter(|v|*v>0.)){
                let percent=(pnl.abs()/balance).clamp(0.,1.);let color=if pnl>=0.{LIME}else{Color32::RED};
                ui.painter().rect_filled(egui::Rect::from_min_size(progress.min,egui::vec2(progress.width()*percent as f32,progress.height())),0.,color);
                field(ui,rr(w*0.225,summary_y+38.,w*0.12,28.),"condor-profit",format!("{pnl:.2} @ {:.2}%",percent*100.),color,WHITE,true);
            }else{label(ui,rr(w*0.225,summary_y+38.,w*0.12,28.),"N/A",WHITE,false,true);}
            field(ui,rr(w*0.35,summary_y+36.,w*0.12,32.),"condor-plan-action",text(&detail,"actionType"),severity(&detail),WHITE,false);
            let reason=if detail.is_null(){text(&plan,"explanation")}else if detail["isComplete"]==true{text(&detail,"actionReason")}else{detail["unavailableReasons"].as_array().map(|r|r.iter().filter_map(Value::as_str).collect::<Vec<_>>().join("; ")).unwrap_or_default()};
            label(ui,rr(w*0.47,summary_y+36.,w*0.525,32.),reason,WHITE,false,false);
            self.draw_condor_spreads(ui,s,&trade,&position,rr(7.,summary_y+71.,w-14.,108.));
            let log_y=upper+7.;ui.painter().hline(bounds.x_range(),o.y+(log_y-3.)/p,egui::Stroke::new(1.0_f32,GRAY));
            let plan_rows=&display.plans;
            let mut columns=vec![("actionDateTime","ActionDateTime",230.),("sequenceId","SequenceId",120.),("dailyPnl","DailyPnl",120.),("forwardLossRatio","ForwardLossRatio",120.),("forwardLoss","ForwardLoss",120.),("lossHeadroom","LossHeadroom",120.),("putForwardPrice","PutForwardPrice ($)",150.),("callForwardPrice","CallForwardPrice ($)",150.),("forwardDelta","ForwardDelta",120.),("netDelta","NetDelta",120.),("netGamma","NetGamma",120.),("netVega","NetVega",120.),("netTheta","NetTheta",120.),("estimatedExitCosts","EstimatedExitCosts",120.),("distanceToShortPut","DistanceToShortPut",120.),("distanceToShortCall","DistanceToShortCall",120.),("putSpreadPrice","PutSpreadPrice",120.),("callSpreadPrice","CallSpreadPrice",120.),("combinedSpreadPrice","CombinedSpreadPrice",120.),("currentPositionValue","CurrentPositionValue",120.),("estimatedCloseValue","EstimatedCloseValue",120.),("putOTMProbability","PutOTMProbability",120.),("callOTMProbability","CallOTMProbability",120.),("timeToExpiryDays","TimeToExpiryDays",120.),("scenarioHorizonSeconds","ScenarioHorizonSeconds",120.),("scenarioUnderlyingMove","ScenarioUnderlyingMove",120.),("scenarioVolatilityShift","ScenarioVolatilityShift",120.),("calculationStatus","CalculationStatus",120.),("exitRecommended","ExitRecommended",120.),("exitReason","ExitReason",320.)];
            columns.insert(2, ("volatilityPrice", "VolatilityPrice", 140.));
            // Older generic plans still expose their explanation, without substituting blank legacy fields.
            if display.has_action_reason {columns.insert(2,("actionReason","Action Reason",320.));}
            let plan_rect=rr(0.,log_y,w,h-log_y);self.controls.insert("condor-plans".into(),plan_rect);
            let context = &s.result.context;
            let offset = context.get("planOffset").and_then(|v| v.parse().ok()).unwrap_or(0);
            let total = context.get("planTotal").and_then(|v| v.parse().ok()).unwrap_or(plan_rows.len());
            // Live rows are pushed by the monitor. Database paging belongs exclusively to stopped mode.
            let live_plans = monitor_flag(s,"live");
            let virtual_mode = !live_plans && context.contains_key("planTotal");
            let scope = format!("{}:{}:{}", context.get("activeTradeTab").cloned().unwrap_or_default(), context.get("planDate").cloned().unwrap_or_default(), if live_plans {"live"} else {"database"});
            let page_id = if virtual_mode { format!("condor-plans-{scope}") } else { "condor-plans".to_owned() };
            let (_, missing, first_visible) = table_window(ui,plan_rect,&page_id,&plan_rows,&columns,None,
                virtual_mode.then_some((offset,total)));
            let latest = context.get("latestPlanRevision").and_then(|v|v.parse::<u64>().ok()).unwrap_or(0);
            let anchor = context.get("planMaximumRevision").and_then(|v|v.parse::<u64>().ok()).unwrap_or(0);
            let refresh = virtual_mode && first_visible == Some(0) && anchor > 0 && latest > anchor;
            let needed = if refresh { Some((0, ((plan_rect.height() / (20. / ui.ctx().pixels_per_point())).ceil() as usize + 4).clamp(1, 100))) } else { missing };
            if let Some(error) = context.get("planError").filter(|e| !e.is_empty()) {
                ui.painter().text(plan_rect.center(), egui::Align2::CENTER_CENTER, error, egui::FontId::proportional(12.), Color32::RED);
            } else if let Some((offset, size)) = needed.filter(|_| self.pending.is_none() && !s.busy) {
                self.draft.insert("refresh".into(), refresh.to_string());
                self.draft.insert("offset".into(), offset.to_string());
                self.draft.insert("pageSize".into(), size.to_string());
                self.draft.insert("tradeTabId".into(), context.get("activeTradeTab").cloned().unwrap_or_default());
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
        trade: &Value,
        position: &Value,
        bounds: egui::Rect,
    ) {
        let p = ui.ctx().pixels_per_point();
        let o = bounds.min;
        let w = bounds.width() * p;
        let columns = [
            ("spread", "Spread Type", 180.),
            ("shortStrike", "Short\nStrike", 80.),
            ("shortBid", "Short Bid", 80.),
            ("shortAsk", "Short Ask", 80.),
            ("shortDelta", "Short Delta", 90.),
            ("shortGamma", "Short\nGamma", 90.),
            ("shortTheta", "Short Theta", 90.),
            ("shortVega", "Short Vega", 90.),
            ("shortRho", "Short Rho", 90.),
            ("shortIV", "Short IVol", 90.),
            ("longStrike", "Long\nStrike", 80.),
            ("longBid", "Long Bid", 80.),
            ("longAsk", "Long Ask", 80.),
            ("longDelta", "Long Delta", 90.),
            ("longGamma", "Long\nGamma", 90.),
            ("longTheta", "Long\nTheta", 90.),
            ("longVega", "Long Vega", 90.),
            ("longRho", "Long Rho", 90.),
            ("longIV", "Long IVol", 90.),
            ("netSpread", "Net\nSpread", 70.),
            ("tradeValue", "Trade\nValue", 70.),
            ("tradePnl", "Trade\nPnl", 70.),
            ("otmProbability", "OTM\nProb", 70.),
            ("forwardPrice", "Forward\nPrice", 70.),
        ];
        let total: f32 = columns.iter().skip(1).map(|c| c.2).sum();
        let mut x = 0.;
        let rows = self.condor_spreads(ui, s, trade, position);
        let realtime = one(&self.display_cache.as_ref().unwrap().model, "realtime");
        let revision = realtime["snapshotRevision"].as_u64().unwrap_or(s.generation);
        let change_scope = egui::Id::new(("option-leg-changes", trade["id"].to_string(),
            s.result.context.get("activeTradeTab"), realtime["monitoringGeneration"].as_str()));
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
                    let background = if live && ((key.starts_with("short") || key.starts_with("long")) && !key.ends_with("Strike")
                        || matches!(*key, "netSpread" | "tradeValue" | "tradePnl" | "otmProbability" | "forwardPrice")) {
                        let id = change_scope.with((key, n));
                        let value = number(row, key).filter(|value| value.is_finite());
                        ui.ctx().data_mut(|data| {
                            let cell = data.get_temp_mut_or_insert_with(id, || LegValueChange {
                                revision, value, background: BLACK,
                            });
                            cell.update(revision, value)
                        })
                    } else { BLACK };
                    field(
                        ui,
                        r,
                        &format!("condor-{key}-{n}"),
                        formatted(row, key),
                        background,
                        if i == 0 {
                            Color32::MAGENTA
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
        trade: &Value,
        _position: &Value,
        bounds: egui::Rect,
    ) {
        let p = ui.ctx().pixels_per_point();
        let o = bounds.min;
        let w = bounds.width() * p;
        let h = bounds.height() * p;
        self.controls.insert("condor-chart".into(), bounds);
        label(
            ui,
            rect(o, p, 0., 8., w, 35.),
            "Iron Condor Net / Forward Spread (price points)",
            WHITE,
            true,
            false,
        );
        let display = self.display_cache.as_ref().unwrap().model.clone();
        let mut points = display.chart_points.clone();
        let mut forward_points = display.forward_chart_points.clone();
        let limit = one(&display,"limits");
        let loss = number(&limit["tradeLimit"],"maxLossLimit").map(|value| -value.abs());
        let win = number(&limit["tradeLimit"],"maxProfitLimit").map(f64::abs);
        let chart = one(&display,"chart");
        let mut end = chart["chartEndUtc"].as_str().and_then(|at|chrono::DateTime::parse_from_rfc3339(at).ok()).map(|at|at.timestamp_millis() as f64/1000.);
        if monitor_flag(s,"live") {
            let realtime = one(&display,"realtime");
            let clock_id = egui::Id::new(("condor-chart-clock", &self.loaded_spread_id,
                s.result.context.get("activeTradeTab"), realtime["monitoringGeneration"].as_str()));
            let mut clock = ui.ctx().data_mut(|data| data.get_temp::<SpreadChartClock>(clock_id)).unwrap_or_default();
            let plan = &realtime["plan"];
            if let (Some(price),Some(at)) = (number(&plan["ironCondorTradePlanSnapshot"],"combinedSpreadPrice"),plan["calculatedAtUtc"].as_str()) {
                if let Ok(at) = chrono::DateTime::parse_from_rfc3339(at) {
                    if clock.accept(std::time::Instant::now()) {
                        let time = at.timestamp_millis() as f64/1000.;
                        if self.loaded_spread_points.last().is_none_or(|point|point[0]<time) {
                            if self.loaded_spread_points.last().is_some_and(|point|(point[0]/60.).floor()==(time/60.).floor()) { self.loaded_spread_points.pop(); }
                            self.loaded_spread_points.push([time,price]);
                        }
                        if let Some(forward) = number(&plan["ironCondorTradePlanSnapshot"],"forwardSpreadForChart") {
                            if self.loaded_forward_points.last().is_none_or(|point|point[0]<time) {
                                if self.loaded_forward_points.last().is_some_and(|point|(point[0]/60.).floor()==(time/60.).floor()) {self.loaded_forward_points.pop();}
                                self.loaded_forward_points.push([time,forward]);
                            }
                        }
                        end = Some(end.unwrap_or(time).max(time));
                    }
                    ui.ctx().data_mut(|data| data.insert_temp(clock_id, clock));
                }
            }
            if let Some(last) = self.loaded_spread_points.last() { end = Some(end.unwrap_or(last[0]).max(last[0])); }
            if let Some(end) = end {self.loaded_spread_points.retain(|point|point[0]>=end-21600.);}
            points.extend_from_slice(&self.loaded_spread_points);
            if let Some(end) = end {self.loaded_forward_points.retain(|point|point[0]>=end-21600.);}
            forward_points.extend_from_slice(&self.loaded_forward_points);
        }
        if let Some(end) = end {points.retain(|point|point[0]>=end-21600. && point[0]<=end);}
        if let Some(end) = end {forward_points.retain(|point|point[0]>=end-21600. && point[0]<=end);}
        coalesce_chart_minutes(&mut forward_points);
        coalesce_chart_minutes(&mut points);
        at(
            ui,
            rect(o, p, 16., 47., w - 32., h - 145.),
            "condor-spread-chart",
            |ui| {
                ui.visuals_mut().extreme_bg_color = BLACK;
                let mut chart = egui_plot::Plot::new(("condor-spread", trade["id"].to_string()));
                if let Some(end) = end { chart = chart.include_x(end-21600.).include_x(end); }
                if let Some(loss) = loss { chart = chart.include_y(loss); }
                if let Some(win) = win { chart = chart.include_y(win); }
                chart
                    .width(ui.available_width())
                    .height(ui.available_height())
                    .show_background(false)
                    .show_grid(false)
                    .x_axis_formatter(|mark, _| {
                        chrono::DateTime::from_timestamp(mark.value as i64, 0)
                            .map(|d| d.with_timezone(&chrono_tz::America::New_York).format("%m-%d %H:%M %Z").to_string())
                            .unwrap_or_default()
                    })
                    .show(ui, |plot| {
                        if let Some(loss) = loss {plot.hline(egui_plot::HLine::new("LossLimit",loss).color(Color32::RED));}
                        if let Some(win) = win {plot.hline(egui_plot::HLine::new("WinLimit",win).color(Color32::GREEN));}
                        if !forward_points.is_empty() {
                            plot.points(egui_plot::Points::new("ForwardSpread",forward_points.clone()).color(Color32::from_rgb(0,191,255)).radius(2.0_f32));
                            plot.line(egui_plot::Line::new("ForwardSpread",forward_points).color(Color32::from_rgb(0,191,255)));
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
        let names = [
            "LossLimit",
            "WinLimit",
            "ForwardSpread",
            "NetSpread",
        ];
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
        trade: &Value,
        position: &Value,
    ) -> Vec<Value> {
        let display = &self.display_cache.as_ref().unwrap().model;
        let realtime = one(display, "realtime");
        if realtime.is_object() {
            return realtime_spreads(&realtime, s.result.context.get("compositionType").is_some_and(|v|v=="LongIronCondor"));
        }
        let legs = trade["legs"].as_array().cloned().unwrap_or_default();
        let marks = position["legs"].as_array().cloned().unwrap_or_default();
        let observations: Vec<_> = values(display, "observation")
            .iter()
            .map(|v| v["value"].clone())
            .collect();
        let mut rows = Vec::new();
        for (right, name) in [(2, "PutCreditSpread"), (1, "CallCreditSpread")] {
            let long = s
                .result
                .context
                .get("compositionType")
                .is_some_and(|v| v == "LongIronCondor");
            let mut row =
                json!({"spread":if long {name.replace("Credit","Debit")}else{name.to_owned()}});
            for (bought, prefix) in [(false, "short"), (true, "long")] {
                if let Some(leg) = legs.iter().find(|l| {
                    l["putCall"] == right
                        && (number(l, "signedQuantity").unwrap_or_default() > 0.) == bought
                }) {
                    row[format!("{prefix}Strike")] = leg["strike"].clone();
                    if let Some(mark) = marks.iter().find(|m| m["tradeLegId"] == leg["tradeLegId"])
                    {
                        row[format!("{prefix}Price")] = mark["currentPrice"].clone();
                    }
                    if let Some(quote) = observations
                        .iter()
                        .find(|q| q["contractId"] == leg["contractId"])
                    {
                        for (key, suffix) in [
                            ("bidPrice", "Bid"),
                            ("askPrice", "Ask"),
                            ("delta", "Delta"),
                            ("gamma", "Gamma"),
                            ("theta", "Theta"),
                            ("vega", "Vega"),
                            ("rho", "Rho"),
                            ("impliedVolatility", "IV"),
                        ] {
                            row[format!("{prefix}{suffix}")] = if quote["greeksAvailable"] == false
                                && key != "bidPrice"
                                && key != "askPrice"
                            {
                                Value::Null
                            } else {
                                quote[key].clone()
                            };
                        }
                    }
                }
            }
            let spread_marks: Vec<_> = marks
                .iter()
                .filter(|m| {
                    legs.iter()
                        .any(|l| l["tradeLegId"] == m["tradeLegId"] && l["putCall"] == right)
                })
                .collect();
            if spread_marks.len() == 2 {
                let quantity = spread_marks
                    .iter()
                    .filter_map(|m| number(m, "signedQuantity"))
                    .map(f64::abs)
                    .fold(f64::INFINITY, f64::min);
                let mut value = 0.;
                let mut pnl = 0.;
                let mut spread = 0.;
                let mut monetary = true;
                for mark in spread_marks {
                    if legs.iter().any(|l| l["tradeLegId"] == mark["tradeLegId"]) {
                        let signed = number(mark, "signedQuantity").unwrap_or_default();
                        let current = number(mark, "currentPrice").unwrap_or_default();
                        let tick = if mark["optionTick"].is_object() {&mark["optionTick"]}else{&mark["optionRisk"]};
                        let multiplier = (tick["contractId"] == mark["contractId"]).then(||number(&tick["pricingInputs"],"multiplier")).flatten().filter(|value|*value>0.);
                        monetary &= multiplier.is_some();
                        let multiplier = multiplier.unwrap_or_default();
                        spread += current * signed;
                        value += current * signed * multiplier;
                        pnl += (current - number(mark, "openingPrice").unwrap_or_default())
                            * signed
                            * multiplier;
                    }
                }
                if quantity > 0. {
                    row["netSpread"] = json!((spread / quantity).abs());
                }
                row["tradeValue"] = if monetary {json!(if long {value}else{value.abs()})}else{Value::Null};
                row["tradePnl"] = if monetary {json!(pnl)}else{Value::Null};
            }
            let plan = display.plan_dates.get(&position["valueDate"].to_string())
                .and_then(|index| values(display, "plan").get(*index));
            if let Some(plan) = plan {
                let forward = number(&plan["ironCondorTradePlanSnapshot"],if right == 2 {"putForwardPrice"}else{"callForwardPrice"});
                row["forwardPrice"] = forward.map(|value|json!(if long {value}else{value.abs()})).unwrap_or(Value::Null);
                row["otmProbability"] = plan["ironCondorTradePlanSnapshot"][if right == 2 {
                    "putOTMProbability"
                } else {
                    "callOTMProbability"
                }]
                .clone();
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
            assert_eq!(at.with_timezone(&chrono_tz::America::New_York).format("%m-%d %H:%M %Z").to_string(), expected);
        }
    }
}

#[cfg(test)]
mod spread_chart_sampling_tests {
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
