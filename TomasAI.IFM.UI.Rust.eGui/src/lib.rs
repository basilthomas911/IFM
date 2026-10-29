//! IFM desktop UI components and the Rust-owned publication models.
//!
//! # Architecture
//!
//! The standalone executable uses native Rust services and controllers to call
//! the existing backend. Backend actors perform validation, persistence, pricing,
//! and trading decisions. A paint callback must not perform database or NATS I/O
//! or recompute a financial valuation. Screens were independently copied from
//! `TomasAI.IFM.UI.Rust`; that hybrid application's source and build are unchanged.
//!
//! `main.rs` owns the native window and background runtime. [`bridge`] retains
//! pure Rust display-format compatibility helpers from the screen copy, and view
//! modules keep only display state, selections, and unsaved drafts. Library re-exports
//! preserve the original flat import paths while source files are grouped by feature.
//!
//! # Publications and interaction
//!
//! Publications are immutable inputs to each frame. Match request IDs in outcomes
//! before clearing pending edits. A publication generation identifies content within
//! its stream; it is not a globally unique object identity. Trade display caches also
//! check a native identity token so restarted or replaced streams cannot reuse stale
//! decoded rows. Keep expensive decoding and cleanup outside pointer-only frames.
//!
//! # Layout and data loading
//!
//! Dimensions are egui logical points unless a helper explicitly converts physical
//! WinForms pixels using the viewport scale. [`presentation`] defines shared colors,
//! fonts, caption styling, tabs, and scrollbar conventions. [`control_parity`] clips
//! table rules to the final viewport and supports row-wide selection.
//!
//! Visible-row rendering and server paging are separate responsibilities. The
//! [`virtual_grid`] pilot keeps a full fixture dataset locally; the established-trade
//! plan grid requests an offset/page-size window from the backend. Do not use the
//! fixture as a pattern for downloading an entire production history.
//!
//! # Reading and extending the source
//!
//! Start with the module pages for [`bridge`], [`navigation`], [`trading`], and
//! [`portfolio`]. Each source file documents its role and local APIs. Build searchable
//! documentation, including private implementation details, with
//! `cargo doc --no-deps --document-private-items`. See the repository README for
//! build, fixture/live-mode, deployment, and desktop-test commands.

use crate::control_parity::BorderedTable as _;
/// Consistent outcome dialogs with accessible vector status icons.
pub mod status_dialog;
/// Native window identity uses the executable's Cargo package version at build time.
pub const WINDOW_TITLE: &str = concat!("IFM - eGui ", env!("CARGO_PKG_VERSION"));

/// Native actor transport and exact backend contract adapters.
pub mod backend;
/// Atomic native journals for pending client operations.
pub mod persistence;
/// Native frontend service implementations.
pub mod services;
pub mod date_picker;
/// Scoped controllers for the standalone views.
pub mod state;
// Source modules are grouped by responsibility; re-exports preserve the public API.
/// MessagePack encoding and versioned publication transport. These modules contain no UI rendering or domain calculations.
pub mod bridge;
pub use bridge::{publication_transfer, wire};
/// Native backup setup and retained history view.
#[path = "operations/database_backup.rs"]
pub mod database_backup;
/// Reference-data editor state machines. Views edit local drafts and send correlated intents; managed services validate and persist changes.
pub mod editors;
/// Reference manager window and its WinForms command rail.
pub mod reference;
/// System administration dialog and backup workflow presentation.
pub mod system_admin;
pub use editors::{calendar_editor, contract_editor, lookup_editor, yield_curve_editor};
/// Strategy workflow observations, published decision details, catalog definitions, and parameter-set administration.
pub mod strategy;
pub use strategy::{parameter_sets, strategy_catalog, strategy_inspection, strategy_workspace};
/// Trading workspace entry point. Rendering, local drafts, and viewport state live here; managed models own trading eligibility, valuation, and command execution.
pub mod trading;
pub use trading::trade_workspace;
/// Market Data views: futures/option contracts, yield curves, provider instruments, price charts, and the published market outlook.
pub mod market;
/// Portfolio Administration and its embedded Risk Policy editor.
pub mod portfolio;
pub use market::{market_charts, market_outlook, provider_browser};
/// Operational diagnostics and scheduled-task administration. Actions are sent through existing managed services and require the published eligibility/confirmation state.
pub mod operations;
pub use operations::{actor_health, operations_health, scheduled_tasks};
/// Shared UI conventions, WinForms-compatible table behavior, and the fixture virtual-grid renderer.
pub mod ui;
pub use ui::{control_parity, presentation, virtual_grid};
/// Shared UI string identifiers. The generated key module is paired with the managed catalog; edit catalog/strings.en.json and run the catalog generator.
pub mod localization;
pub use localization::strings;
/// Optional native diagnostics. Measurements support UI performance investigation and do not drive business state.
pub mod diagnostics;
pub use diagnostics::latency;
/// Main-window layout and sidebar views. The shell composes feature renderers and returns user actions to the host.
pub mod navigation;
pub use navigation::{shell, sidebar};

use crate::presentation::TradingLabelUi as _;
use eframe::egui;
use serde::Deserialize;
/// A name/value pair already prepared for the read-only property grid.
#[derive(Clone, Debug, Deserialize)]
pub struct Property {
    /// Published or edited display name.
    pub name: String,
    /// Published or edited display value; formatting/validation belongs to its managed field contract.
    pub value: String,
}
/// Published details-tree node with stable identity, properties, and nested children.
#[derive(Clone, Debug, Deserialize)]
pub struct Node {
    /// Stable identity used to correlate selection, rows, or commands.
    pub id: String,
    /// Human-readable caption for this identity/value.
    pub label: String,
    /// Display-ready property pairs for the selected details node.
    pub properties: Vec<Property>,
    /// Nested published details-tree nodes.
    pub children: Vec<Node>,
}
/// Published workflow selector entry and the numeric bridge action that selects it.
#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WorkflowChoice {
    /// Stable identity used to correlate selection, rows, or commands.
    pub id: String,
    /// Human-readable caption for this identity/value.
    pub label: String,
    /// Numeric bridge action used to select this published workflow.
    pub action: u32,
}
/// Strategy observation publication including details tree, workflow page, summaries, and chart window.
#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Snapshot {
    /// Wire schema version used to interpret this publication.
    pub schema: u32,
    /// Publication-stream identity; changes invalidate caches even when generation values repeat.
    pub epoch: String,
    /// Publication generation used for change detection and request correlation.
    pub generation: u64,
    /// Published operating mode, such as Fixture or live composition.
    pub mode: String,
    /// Identity of the workflow represented by the publication.
    pub workflow_id: String,
    /// Published aggregate/workflow revision, distinct from a UI publication generation.
    pub revision: u64,
    /// Top-level published details-tree nodes.
    pub roots: Vec<Node>,
    /// Published workflow choices and their numeric selection actions.
    #[serde(default)]
    pub workflows: Vec<WorkflowChoice>,
    /// Display-ready status supplied by the managed projection or current view state.
    #[serde(default)]
    pub status: String,
    /// Rows supplied by the enclosing publication/model; they are not fetched during drawing.
    #[serde(default)]
    pub rows: Vec<strategy_workspace::WorkflowRow>,
    /// Published ITI signal points or parameter signal definitions, depending on the enclosing model.
    #[serde(default)]
    pub signals: Vec<strategy_workspace::GraphPoint>,
    /// Published workflow-summary sections or summary text.
    #[serde(default)]
    pub summary: Vec<strategy_workspace::SummarySection>,
    /// Published display heading for this view.
    #[serde(default)]
    pub header: String,
    /// Current workflow history page as published by the managed view.
    #[serde(default)]
    pub page_number: usize,
    /// Published number of workflow history pages.
    #[serde(default)]
    pub page_count: usize,
    /// Backend total row count, distinct from the currently loaded/rendered rows.
    #[serde(default)]
    pub total_rows: usize,
    /// Whether the managed result reports additional data beyond this page.
    #[serde(default)]
    pub has_more: bool,
    /// Chart-window start expressed as UTC Unix seconds.
    #[serde(default)]
    pub graph_start_utc: f64,
    /// Chart-window end expressed as UTC Unix seconds.
    #[serde(default)]
    pub graph_end_utc: f64,
    /// Optional market-time chart-window start supplied by the backend.
    #[serde(default)]
    pub graph_start_market: Option<f64>,
    /// Optional market-time chart-window end supplied by the backend.
    #[serde(default)]
    pub graph_end_market: Option<f64>,
    /// Published futures/option contract identity.
    #[serde(default)]
    pub contract_id: String,
    /// Selected/published Daily, Weekly, or Monthly observation horizon.
    #[serde(default)]
    pub time_frame: String,
}
/// Versioned localized UI text shared with the managed bridge.
#[derive(Clone, Deserialize)]
pub struct Catalog {
    /// Version identifier supplied by this contract.
    pub version: u32,
    /// Catalog locale identifier.
    pub locale: String,
    /// Localized display strings indexed by generated catalog keys.
    pub strings: std::collections::BTreeMap<String, String>,
}
impl Catalog {
    /// Return a localized string by key, falling back to the key itself when no catalog entry exists.
    pub fn text(&self, id: &str) -> &str {
        self.strings
            .get(id)
            .map(String::as_str)
            .unwrap_or("[Missing string]")
    }
}
/// Persistent selected node identity for the read-only workflow tree and property grid.
#[derive(Default)]
pub struct DetailsView {
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<String>,
}
/// Search the published details tree depth-first for a stable node ID; return None when absent.
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
    /// Render the workflow details tree/property grid as a standalone central panel.
    pub fn draw(&mut self, ctx: &egui::Context, snapshot: &Snapshot, catalog: &Catalog) {
        egui::CentralPanel::default()
            .frame(egui::Frame::NONE)
            .show(ctx, |ui| self.draw_inside(ui, snapshot, catalog));
    }
    /// Render a bounded details tree and property grid inside the supplied UI; selection is resolved by node ID.
    pub fn draw_inside(&mut self, ui: &mut egui::Ui, snapshot: &Snapshot, catalog: &Catalog) {
        egui::TopBottomPanel::top("objects")
            .resizable(true)
            .show_separator_line(false)
            .default_height(ui.available_height() * presentation::DETAILS_TREE_RATIO)
            .min_height(80.0)
            .show_inside(ui, |ui| {
                let tree_border = ui.available_rect_before_wrap();
                egui::ScrollArea::vertical()
                    .id_salt("tree_scroll")
                    .auto_shrink([false, false])
                    .show(ui, |ui| {
                        for node in &snapshot.roots {
                            self.node(ui, node, catalog);
                        }
                    });
                ui.painter().rect_stroke(
                    tree_border.intersect(ui.clip_rect()),
                    0.,
                    egui::Stroke::new(1. / ui.ctx().pixels_per_point(), presentation::BORDER),
                    egui::StrokeKind::Inside,
                );
            });
        egui::CentralPanel::default().show_inside(ui, |ui| {
            let property_grid_border = ui.available_rect_before_wrap();
            control_parity::grid_viewport(ui, property_grid_border, |ui| {
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
                        .bordered_body(0., |body| {
                            body.rows(23.0, node.properties.len(), |mut row| {
                                let property = &node.properties[row.index()];
                                for (column, value) in
                                    [&property.name, &property.value].into_iter().enumerate()
                                {
                                    row.col(|ui| {
                                        ui.painter().rect_stroke(
                                            ui.max_rect(),
                                            0.0,
                                            egui::Stroke::new(1.0_f32, presentation::COMMAND),
                                            egui::StrokeKind::Inside,
                                        );
                                        if column == 0 {
                                            ui.trading_caption(value);
                                        } else {
                                            ui.trading_label(value);
                                        }
                                    });
                                }
                            });
                        });
                } else {
                    ui.trading_caption(catalog.text(strings::SELECT));
                }
            });
            ui.painter().rect_stroke(
                property_grid_border.intersect(ui.clip_rect()),
                0.,
                egui::Stroke::new(1. / ui.ctx().pixels_per_point(), presentation::BORDER),
                egui::StrokeKind::Inside,
            );
        });
    }
    /// Draw one details-tree node recursively and update the selected node identity when clicked.
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

/// Retention follows two pages of the current console viewport.
pub mod console_capacity;
