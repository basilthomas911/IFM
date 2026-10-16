pub mod wire;
use crate::presentation::TradingLabelUi as _;
pub mod contract_editor;
pub mod lookup_editor;
pub mod presentation;
pub mod shell;
pub mod strategy_inspection;
pub mod strategy_workspace;
pub mod strings;
pub mod trade_workspace;
pub mod virtual_grid;
pub mod yield_curve_editor;
use eframe::egui;
use serde::Deserialize;
#[derive(Clone, Debug, Deserialize)]
pub struct Property {
    pub name: String,
    pub value: String,
}
#[derive(Clone, Debug, Deserialize)]
pub struct Node {
    pub id: String,
    pub label: String,
    pub properties: Vec<Property>,
    pub children: Vec<Node>,
}
#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WorkflowChoice {
    pub id: String,
    pub label: String,
    pub action: u32,
}
#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Snapshot {
    pub schema: u32,
    pub epoch: String,
    pub generation: u64,
    pub mode: String,
    pub workflow_id: String,
    pub revision: u64,
    pub roots: Vec<Node>,
    #[serde(default)]
    pub workflows: Vec<WorkflowChoice>,
    #[serde(default)]
    pub status: String,
    #[serde(default)]
    pub rows: Vec<strategy_workspace::WorkflowRow>,
    #[serde(default)]
    pub signals: Vec<strategy_workspace::GraphPoint>,
    #[serde(default)]
    pub summary: Vec<strategy_workspace::SummarySection>,
    #[serde(default)]
    pub header: String,
    #[serde(default)]
    pub page_number: usize,
    #[serde(default)]
    pub page_count: usize,
    #[serde(default)]
    pub total_rows: usize,
    #[serde(default)]
    pub has_more: bool,
    #[serde(default)]
    pub graph_start_utc: f64,
    #[serde(default)]
    pub graph_end_utc: f64,
    #[serde(default)]
    pub graph_start_market: Option<f64>,
    #[serde(default)]
    pub graph_end_market: Option<f64>,
    #[serde(default)]
    pub contract_id: String,
    #[serde(default)]
    pub time_frame: String,
}
#[derive(Clone, Deserialize)]
pub struct Catalog {
    pub version: u32,
    pub locale: String,
    pub strings: std::collections::BTreeMap<String, String>,
}
impl Catalog {
    pub fn text(&self, id: &str) -> &str {
        self.strings
            .get(id)
            .map(String::as_str)
            .unwrap_or("[Missing string]")
    }
}
#[derive(Default)]
pub struct DetailsView {
    pub selected: Option<String>,
}
fn find<'a>(nodes: &'a [Node], id: &str) -> Option<&'a Node> {
    for n in nodes {
        if n.id == id {
            return Some(n);
        }
        if let Some(found) = find(&n.children, id) {
            return Some(found);
        }
    }
    None
}
impl DetailsView {
    pub fn draw(&mut self, ctx: &egui::Context, snapshot: &Snapshot, catalog: &Catalog) {
        egui::CentralPanel::default()
            .frame(egui::Frame::NONE)
            .show(ctx, |ui| self.draw_inside(ui, snapshot, catalog));
    }
    pub fn draw_inside(&mut self, ui: &mut egui::Ui, snapshot: &Snapshot, catalog: &Catalog) {
        egui::TopBottomPanel::top("objects")
            .resizable(true)
            .default_height(ui.available_height() * presentation::DETAILS_TREE_RATIO)
            .min_height(80.0)
            .show_inside(ui, |ui| {
                egui::ScrollArea::vertical()
                    .id_salt("tree_scroll")
                    .auto_shrink([false, false])
                    .show(ui, |ui| {
                        for node in &snapshot.roots {
                            self.node(ui, node, catalog);
                        }
                    });
            });
        egui::CentralPanel::default().show_inside(ui, |ui| {
            if let Some(node) = self
                .selected
                .as_deref()
                .and_then(|id| find(&snapshot.roots, id))
            {
                let name_width = ui.available_width() / 2.0;
                egui_extras::TableBuilder::new(ui)
                    .id_salt("inspector")
                    .resizable(true)
                    .striped(false)
                    .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                    .column(egui_extras::Column::initial(name_width))
                    .column(egui_extras::Column::remainder())
                    .body(|body| {
                        body.rows(23.0, node.properties.len(), |mut row| {
                            let property = &node.properties[row.index()];
                            for value in [&property.name, &property.value] {
                                row.col(|ui| {
                                    ui.painter().rect_stroke(
                                        ui.max_rect(),
                                        0.0,
                                        egui::Stroke::new(1.0_f32, presentation::COMMAND),
                                        egui::StrokeKind::Inside,
                                    );
                                    ui.trading_label(value);
                                });
                            }
                        });
                    });
            } else {
                ui.trading_label(catalog.text(strings::SELECT));
            }
        });
    }
    fn node(&mut self, ui: &mut egui::Ui, node: &Node, catalog: &Catalog) {
        if node.children.is_empty() {
            if ui
                .selectable_label(self.selected.as_deref() == Some(&node.id), &node.label)
                .clicked()
            {
                self.selected = Some(node.id.clone());
            }
        } else {
            let response = egui::CollapsingHeader::new(&node.label)
                .id_salt(&node.id)
                .show(ui, |ui| {
                    if ui
                        .selectable_label(
                            self.selected.as_deref() == Some(&node.id),
                            format!("{} {}", catalog.text(strings::INSPECT), node.label),
                        )
                        .clicked()
                    {
                        self.selected = Some(node.id.clone());
                    }
                    for child in &node.children {
                        self.node(ui, child, catalog);
                    }
                });
            if response.header_response.clicked() {
                self.selected = Some(node.id.clone());
            }
        }
    }
}

/// Match WinForms Microsoft Sans Serif 10pt at 96 logical pixels per inch.
/// Read the installed Windows font; do not redistribute Microsoft's font file.
pub fn apply_winforms_typography(ctx: &egui::Context) -> std::io::Result<()> {
    let windows = std::env::var_os("WINDIR").unwrap_or_else(|| "C:\\Windows".into());
    let bytes = std::fs::read(std::path::PathBuf::from(windows).join("Fonts/micross.ttf"))?;
    let mut fonts = egui::FontDefinitions::default();
    fonts.font_data.insert(
        "Microsoft Sans Serif".into(),
        std::sync::Arc::new(egui::FontData::from_owned(bytes)),
    );
    fonts
        .families
        .get_mut(&egui::FontFamily::Proportional)
        .unwrap()
        .insert(0, "Microsoft Sans Serif".into());
    // WinForms uses the same face for all its controls, including log output.
    fonts.families.insert(
        egui::FontFamily::Monospace,
        fonts.families[&egui::FontFamily::Proportional].clone(),
    );
    ctx.set_fonts(fonts);
    presentation::apply(ctx);
    Ok(())
}

pub mod calendar_editor;

pub mod provider_browser;

pub mod scheduled_tasks;

pub mod operations_health;

pub mod actor_health;

pub mod parameter_sets;

pub mod strategy_catalog;

pub mod market_outlook;

pub mod market_charts;

pub mod sidebar;

pub mod control_parity;
