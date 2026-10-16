//! IronCondorTradeView parity. Geometry follows its runtime Resize and table weights.
use super::order_layout::{at, rect};
use super::*;
use serde_json::{Value, json};
const BLACK: Color32 = Color32::BLACK;
const WHITE: Color32 = Color32::WHITE;
const GRAY: Color32 = Color32::GRAY;
const COMMAND: Color32 = Color32::from_rgb(45, 45, 48);
const LIME: Color32 = Color32::from_rgb(50, 205, 50);
fn one(s: &ContractPublication, kind: &str) -> Value {
    values(s, kind).into_iter().next().unwrap_or_default()
}
fn values(s: &ContractPublication, kind: &str) -> Vec<Value> {
    s.result
        .rows
        .iter()
        .filter(|r| r.fields.get("kind").is_some_and(|k| k == kind))
        .map(|r| {
            let mut value=data(r);
            if kind=="plan" {
                if let Some(display)=r.fields.get("actionDateTimeEastern") {
                    value["actionDateTimeEastern"]=if display.is_empty(){Value::Null}else{json!(display)};
                }
            }
            value
        })
        .collect()
}
fn monitor_flag(s:&ContractPublication,key:&str)->bool {
    let name=format!("monitor{}{}",key[..1].to_uppercase(),&key[1..]);
    s.result.context.get(&name).map(|value|value=="True").unwrap_or_else(||flag(s,key))
}
// The live path renders one managed cache revision; historical rows retain their saved-data path.
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
                    row[format!("{prefix}{suffix}")] = if risk && leg["pricingStatus"] != "Available"
                        || !risk && leg["quoteStatus"] != "Available" {Value::Null}
                        else {leg["observation"][field].clone()};
                }
            }
        }
        if let Some(spread) = snapshot["spreads"].as_array().and_then(|spreads|spreads.iter().find(|spread|spread["putCall"]==right)) {
            for field in ["netSpread","tradeValue","tradePnl","otmProbability","forwardPrice"] {
                row[field] = spread[field].clone();
            }
        }
        row
    }).collect()
}
#[cfg(test)]
mod realtime_cache_tests {
    use super::*;
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
        assert_eq!(rows[0]["forwardPrice"],-3);
        assert_eq!(rows[0]["tradeValue"],-200);
        assert!(rows[1]["shortBid"].is_null());
        let mut stale=snapshot;
        stale["legs"][0]["pricingStatus"]=json!("Stale");
        let rows=realtime_spreads(&stale,false);
        assert!(rows[0]["shortDelta"].is_null());
        assert_eq!(rows[0]["shortBid"],9);
    }
}
fn number(v: &Value, key: &str) -> Option<f64> {
    v[key]
        .as_f64()
        .or_else(|| v[key].as_str().and_then(|s| s.parse().ok()))
}
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
fn table(
    ui: &mut egui::Ui,
    r: egui::Rect,
    id: &str,
    rows: &[Value],
    columns: &[(&str, &str, f32)],
    selected: Option<usize>,
) -> Option<usize> {
    at(ui, r, id, |ui| {
        ui.set_clip_rect(r.intersect(ui.clip_rect()));
        ui.set_max_height(r.height());
        let p = ui.ctx().pixels_per_point();
        let row_height = 24. / p;
        let mut clicked = None;
        egui::ScrollArea::horizontal()
            .id_salt((id, "scroll"))
            .auto_shrink([false, false])
            .max_width(r.width())
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
                    .striped(false)
                    .sense(egui::Sense::click())
                    .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                    .min_scrolled_height((r.height() - row_height).max(0.))
                    .max_scroll_height((r.height() - row_height).max(0.));
                for (_, _, width) in columns {
                    grid = grid.column(
                        egui_extras::Column::initial(*width / p)
                            .at_least(40. / p)
                            .clip(true)
                            .resizable(true),
                    );
                }
                grid = grid.column(egui_extras::Column::remainder().at_least(0.));
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
                    row.col(|ui| {
                        ui.painter().rect_filled(ui.max_rect(), 0., COMMAND);
                    });
                })
                .bordered_body(row_height, |body| {
                    body.rows(row_height, rows.len(), |mut row| {
                        let index = row.index();
                        row.set_selected(selected == Some(index));
                        let color = if id == "condor-plans" && rows[index]["isComplete"] != true {
                            Color32::YELLOW
                        } else {
                            WHITE
                        };
                        for (key, _, _) in columns {
                            row.col(|ui| {
                                let value = formatted(&rows[index], key);
                                ui.add(
                                    egui::Label::new(egui::RichText::new(&value).color(color))
                                        .truncate(),
                                )
                                .on_hover_text(value);
                            });
                        }
                        row.col(|_| {});
                        if row.response().clicked() {
                            clicked = Some(index);
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
    })
}
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
impl TradeWorkspace {
    pub(super) fn draw_loaded_trade(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
    ) -> Option<&'static str> {
        let trade = one(s, "established");
        if !self.loaded_open || trade.is_null() {
            return None;
        }
        let identity = trade["id"].to_string();
        if self.loaded_spread_id != identity {
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
            // 120px four-contract pane, 79px limits, remaining width split after 640px history.
            let log=(h-7.)/3.;let upper=h-7.-log;
            let marks=183.;let graph_h=(upper-6.-marks).max(180.);
            let chart_w=((w-650.)/2.).max(0.);let show_chart=chart_w>=220. && upper>=marks+180.;
            let left=if show_chart{w-chart_w}else{w};
            let history_h=(graph_h-205.).max(75.);
            let rr=|x,y,ww,hh|rect(o,p,x,y,ww,hh);
            ui.spacing_mut().item_spacing=egui::Vec2::ZERO;
            let description=format!("{} | {} | {}.{}.{}.{} | {}",s.result.context.get("fundName").cloned().unwrap_or_default(),s.result.context.get("compositionType").cloned().unwrap_or_else(||"Iron Condor".into()),text(&trade["id"],"portfolioId"),text(&trade["id"],"fundId"),text(&trade["id"],"orderId"),text(&trade["id"],"tradeId"),text(&trade,"status"));
            label(ui,rr(2.,0.,left-4.,34.),description,WHITE,false,false);

            let history=values(s,"position");
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
            let limit=one(s,"limits");let mut limit_row=limit["tradeLimit"].clone();
            if limit_row.is_object(){limit_row["orderId"]=limit["orderId"].clone();limit_row["fundBalance"]=limit["fundBalance"].clone();}
            let limits_rect=rr(2.,graph_h-79.,left-4.,79.);
            self.controls.insert("condor-limits".into(),limits_rect);
            table(ui,limits_rect,"condor-limits",&[limit_row],&[("orderId","Order Id",100.),("riskMargin","Risk Margin",120.),("maxProfit","Max Profit",90.),("maxLoss","Max Loss",90.),("maxReturn","Max Return",110.),("maxLossLimit","Max Loss Limit",125.),("maxProfitLimit","Max Profit Limit",115.),("minProfitTarget","Min Profit Target",120.),("dailyProfitTarget","Daily Profit Target",140.),("fundBalance","Fund Balance",150.)],None);
            let position=one(s,"position-details");
            if show_chart {self.condor_chart(ui,s,&trade,&position,rr(left,0.,chart_w,graph_h));}
            ui.painter().hline(bounds.x_range(),o.y+graph_h/p,egui::Stroke::new(1.0_f32,GRAY));
            let plans=values(s,"plan");let realtime=one(s,"realtime");
            let plan=if realtime.is_object(){realtime["plan"].clone()}else{plans.first().cloned().unwrap_or_default()};let detail=&plan["ironCondorTradePlanSnapshot"];
            let history_row=selected.and_then(|i|history.get(i)).cloned().unwrap_or_default();
            let mut net=number(&history_row,"netSpread").or_else(||number(&trade,"openingValue"));
            if realtime.is_object(){net=number(&realtime["summary"],"netSpread");}
            else if let Some(marks)=position["legs"].as_array(){let qty=marks.iter().filter_map(|l|number(l,"signedQuantity")).map(f64::abs).reduce(f64::min).unwrap_or_default();if qty>0.{net=Some(marks.iter().map(|l|number(l,"currentPrice").unwrap_or_default()*number(l,"signedQuantity").unwrap_or_default()).sum::<f64>()/qty);}}
            let daily=number(detail,"dailyPnl").or_else(||number(&history_row,"tradePnl"));
            let asset=if realtime.is_object(){formatted(&realtime["summary"],"underlyingPrice")}else{formatted(detail,"underlyingPrice")};
            let summary_y=graph_h+6.;
            let top_fields=[("Value Date:",formatted(&history_row,"valueDate"),BLACK),
                ("Trade Status:",if plan.is_null(){text(&trade,"status")}else{format!("{}: {}",text(&plan,"state"),text(&plan,"explanation"))},BLACK),
                ("DTE:",formatted(&history_row,"daysToExpiry"),BLACK),
                ("Net Spread:",net.map(|n|format!("{n:.2}")).unwrap_or_else(||"N/A".into()),BLACK),
                ("Daily P&L:",daily.map(|n|format!("{n:.2}")).unwrap_or_else(||"N/A".into()),BLACK),
                ("Asset Price:",asset,BLACK),
                ("Fwd Loss:",if realtime.is_object() && realtime["planStatus"]!="Available" {"N/A".into()}else{number(detail,"forwardLossRatio").map(|n|format!("{:.1}%",n*100.)).unwrap_or_else(||"N/A".into())},severity(detail))];
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
            field(ui,rr(w*0.35,summary_y+36.,w*0.12,32.),"condor-plan-action",text(detail,"actionType"),severity(detail),WHITE,false);
            let reason=if detail.is_null(){text(&plan,"explanation")}else if detail["isComplete"]==true{text(detail,"actionReason")}else{detail["unavailableReasons"].as_array().map(|r|r.iter().filter_map(Value::as_str).collect::<Vec<_>>().join("; ")).unwrap_or_default()};
            label(ui,rr(w*0.47,summary_y+36.,w*0.525,32.),reason,WHITE,false,false);
            self.draw_condor_spreads(ui,s,&trade,&position,rr(7.,summary_y+71.,w-14.,108.));
            let log_y=upper+7.;ui.painter().hline(bounds.x_range(),o.y+(log_y-3.)/p,egui::Stroke::new(1.0_f32,GRAY));
            let plan_rows:Vec<_>=plans.into_iter().map(|v|{
                let mut row=if v["ironCondorTradePlanSnapshot"].is_object(){v["ironCondorTradePlanSnapshot"].clone()}else{json!({"actionDateTime":v["calculatedAtUtc"],"actionType":v["action"],"actionState":v["state"],"actionReason":v["explanation"],"dailyPnl":v["totalPnl"]})};
                if let Some(display)=v.get("actionDateTimeEastern") {row["actionDateTime"]=display.clone();}
                row
            }).collect();
            let mut columns=vec![("actionDateTime","ActionDateTime",230.),("sequenceId","SequenceId",120.),("dailyPnl","DailyPnl",120.),("forwardLossRatio","ForwardLossRatio",120.),("forwardLoss","ForwardLoss",120.),("dailyLossLimit","DailyLossLimit",120.),("lossHeadroom","LossHeadroom",120.),("forwardLossHeadroom","ForwardLossHeadroom",150.),("forwardDailyPnl","ForwardDailyPnl",120.),("putForwardPrice","PutForwardPrice",120.),("callForwardPrice","CallForwardPrice",120.),("forwardDelta","ForwardDelta",120.),("netDelta","NetDelta",120.),("netGamma","NetGamma",120.),("netVega","NetVega",120.),("netTheta","NetTheta",120.),("estimatedExitCosts","EstimatedExitCosts",120.),("distanceToShortPut","DistanceToShortPut",120.),("distanceToShortCall","DistanceToShortCall",120.),("underlyingPrice","UnderlyingPrice",120.),("putSpreadPrice","PutSpreadPrice",120.),("callSpreadPrice","CallSpreadPrice",120.),("combinedSpreadPrice","CombinedSpreadPrice",120.),("combinedForwardPrice","CombinedForwardPrice",120.),("currentPositionValue","CurrentPositionValue",120.),("forwardPositionValue","ForwardPositionValue",120.),("estimatedCloseValue","EstimatedCloseValue",120.),("putOTMProbability","PutOTMProbability",120.),("callOTMProbability","CallOTMProbability",120.),("timeToExpiryDays","TimeToExpiryDays",120.),("scenarioHorizonSeconds","ScenarioHorizonSeconds",120.),("scenarioUnderlyingMove","ScenarioUnderlyingMove",120.),("scenarioVolatilityShift","ScenarioVolatilityShift",120.),("calculationStatus","CalculationStatus",120.),("exitRecommended","ExitRecommended",120.),("exitReason","ExitReason",320.)];
            // Older generic plans still expose their explanation, without substituting blank legacy fields.
            if plan_rows.iter().any(|v|!v["actionReason"].is_null()){columns.insert(2,("actionReason","Action Reason",320.));}
            let plan_rect=rr(0.,log_y,w,h-log_y);self.controls.insert("condor-plans".into(),plan_rect);
            table(ui,plan_rect,"condor-plans",&plan_rows,&columns,None);

        });
        self.loaded_bounds = Some(panel.response.rect);
        action
    }
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
                    field(
                        ui,
                        r,
                        &format!("condor-{key}-{n}"),
                        formatted(row, key),
                        BLACK,
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
    fn condor_chart(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        trade: &Value,
        position: &Value,
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
            "Iron Condor Net Spread Path",
            WHITE,
            true,
            false,
        );
        let mut points: Vec<[f64; 2]> = values(s, "position")
            .iter()
            .filter_map(|v| {
                chrono::NaiveDate::parse_from_str(&text(v, "valueDate"), "%Y-%m-%d")
                    .ok()
                    .and_then(|d| {
                        number(v, "netSpread").map(|n| {
                            [
                                d.and_hms_opt(0, 0, 0).unwrap().and_utc().timestamp() as f64,
                                n,
                            ]
                        })
                    })
            })
            .collect();
        if points.is_empty() {
            if let (Some(n), Ok(d)) = (
                number(trade, "openingValue"),
                chrono::DateTime::parse_from_rfc3339(&text(trade, "establishedAtUtc")),
            ) {
                points.push([d.timestamp() as f64, n]);
            }
        }
        if monitor_flag(s, "live") {
            if let Some(legs) = position["legs"].as_array() {
                let qty = legs
                    .iter()
                    .filter_map(|v| number(v, "signedQuantity"))
                    .map(f64::abs)
                    .reduce(f64::min)
                    .unwrap_or_default();
                if qty > 0. {
                    if let Ok(date) =
                        chrono::DateTime::parse_from_rfc3339(&text(position, "asOfUtc"))
                    {
                        let point = [
                            date.timestamp() as f64,
                            legs.iter()
                                .map(|v| {
                                    number(v, "currentPrice").unwrap_or_default()
                                        * number(v, "signedQuantity").unwrap_or_default()
                                })
                                .sum::<f64>()
                                / qty,
                        ];
                        if self.loaded_spread_points.last() != Some(&point) {
                            self.loaded_spread_points.push(point);
                        }
                        if self.loaded_spread_points.len() > 500 {
                            self.loaded_spread_points.remove(0);
                        }
                    }
                }
            }
            points.extend_from_slice(&self.loaded_spread_points);
        }
        at(
            ui,
            rect(o, p, 16., 47., w - 32., h - 145.),
            "condor-spread-chart",
            |ui| {
                ui.visuals_mut().extreme_bg_color = BLACK;
                let mut chart = egui_plot::Plot::new(("condor-spread", trade["id"].to_string()));
                if points.len() == 1 {
                    chart = chart
                        .include_x(points[0][0] - 300.)
                        .include_x(points[0][0] + 300.)
                        .include_y(points[0][1]-0.5)
                        .include_y(points[0][1]+0.5);
                }
                chart
                    .width(ui.available_width())
                    .height(ui.available_height())
                    .show_background(false)
                    .show_grid(false)
                    .x_axis_formatter(|mark, _| {
                        chrono::DateTime::from_timestamp(mark.value as i64, 0)
                            .map(|d| d.format("%m-%d %H:%M").to_string())
                            .unwrap_or_default()
                    })
                    .show(ui, |plot| {
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
            Color32::from_rgb(255, 165, 0),
        ];
        let names = [
            "LossLimit",
            "WinLimit",
            "ForwardSpread",
            "NetSpread",
            "ForwardSpreadLimit",
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
    fn condor_spreads(
        &self,
        _ui: &mut egui::Ui,
        s: &ContractPublication,
        trade: &Value,
        position: &Value,
    ) -> Vec<Value> {
        let realtime = one(s, "realtime");
        if realtime.is_object() {
            return realtime_spreads(&realtime, s.result.context.get("compositionType").is_some_and(|v|v=="LongIronCondor"));
        }
        let legs = trade["legs"].as_array().cloned().unwrap_or_default();
        let marks = position["legs"].as_array().cloned().unwrap_or_default();
        let observations: Vec<_> = values(s, "observation")
            .into_iter()
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
                for mark in spread_marks {
                    if let Some(def) = legs.iter().find(|l| l["tradeLegId"] == mark["tradeLegId"]) {
                        let signed = number(mark, "signedQuantity").unwrap_or_default();
                        let current = number(mark, "currentPrice").unwrap_or_default();
                        let multiplier = number(def, "cashMultiplier").unwrap_or_default();
                        spread += current * signed;
                        value += current * signed * multiplier;
                        pnl += (current - number(mark, "openingPrice").unwrap_or_default())
                            * signed
                            * multiplier;
                    }
                }
                if quantity > 0. {
                    row["netSpread"] = json!(spread / quantity);
                }
                row["tradeValue"] = json!(value);
                row["tradePnl"] = json!(pnl);
            }
            let plan = values(s, "plan")
                .into_iter()
                .find(|v| v["valueDate"] == position["valueDate"]);
            if let Some(plan) = plan {
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
