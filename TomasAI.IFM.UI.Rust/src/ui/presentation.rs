//! WinForms presentation baseline. Values are logical pixels at 96 DPI.
//! Source: UI.Net.Views/Presentation/DarkTradingTheme.cs and counterpart layouts.
use eframe::egui;

pub const FONT_SIZE: f32 = 10.0 * 96.0 / 72.0;
pub const BACKGROUND: egui::Color32 = egui::Color32::BLACK;
pub const FOREGROUND: egui::Color32 = egui::Color32::WHITE;
pub const BORDER: egui::Color32 = egui::Color32::GRAY;
pub const COMMAND: egui::Color32 = egui::Color32::from_rgb(45, 45, 48);
pub const HOVER: egui::Color32 = egui::Color32::from_rgb(62, 62, 64);
pub const PRESSED: egui::Color32 = egui::Color32::from_rgb(31, 31, 32);
pub const MAIN_CLIENT_SIZE: [f32; 2] = [2637.0, 872.0];
pub const PARAMETER_NAVIGATION_RATIO: f32 = 430.0 / 1200.0;
pub const CATALOG_NAVIGATION_RATIO: f32 = 0.29;
pub const DETAILS_TREE_RATIO: f32 = 140.0 / 300.0;
pub const SCHEDULE_LOG_TREE_RATIO: f32 = 0.4;

/// Set a deterministic dark palette for every editor, popup, tree and grid.
/// Status and chart colors deliberately remain at the individual call sites.
pub fn apply(ctx: &egui::Context) {
    let mut style = (*ctx.style()).clone();
    for font in style.text_styles.values_mut() {
        *font = egui::FontId::new(FONT_SIZE, egui::FontFamily::Proportional);
    }
    style.spacing.item_spacing = egui::vec2(4.0, 4.0);
    style.spacing.button_padding = egui::vec2(6.0, 3.0);
    style.spacing.interact_size.y = 23.0;
    style.spacing.indent = 19.0;
    style.spacing.window_margin = egui::Margin::same(3);
    let mut visuals = egui::Visuals::dark();
    visuals.override_text_color = Some(FOREGROUND);
    visuals.panel_fill = BACKGROUND;
    visuals.window_fill = BACKGROUND;
    visuals.extreme_bg_color = BACKGROUND;
    visuals.faint_bg_color = BACKGROUND;
    visuals.window_stroke = egui::Stroke::new(1.0_f32, BORDER);
    visuals.window_corner_radius = egui::CornerRadius::ZERO;
    visuals.menu_corner_radius = egui::CornerRadius::ZERO;
    visuals.selection.bg_fill = egui::Color32::from_rgb(0, 120, 215);
    visuals.selection.stroke = egui::Stroke::new(1.0_f32, FOREGROUND);
    visuals.hyperlink_color = FOREGROUND;
    for widget in [
        &mut visuals.widgets.noninteractive,
        &mut visuals.widgets.inactive,
        &mut visuals.widgets.hovered,
        &mut visuals.widgets.active,
        &mut visuals.widgets.open,
    ] {
        widget.corner_radius = egui::CornerRadius::ZERO;
        widget.bg_stroke = egui::Stroke::new(1.0_f32, BORDER);
        widget.fg_stroke = egui::Stroke::new(1.0_f32, FOREGROUND);
        widget.expansion = 0.0;
    }
    visuals.widgets.noninteractive.bg_fill = BACKGROUND;
    visuals.widgets.noninteractive.weak_bg_fill = BACKGROUND;
    visuals.widgets.inactive.bg_fill = COMMAND;
    visuals.widgets.inactive.weak_bg_fill = COMMAND;
    visuals.widgets.hovered.bg_fill = HOVER;
    visuals.widgets.hovered.weak_bg_fill = HOVER;
    visuals.widgets.active.bg_fill = PRESSED;
    visuals.widgets.active.weak_bg_fill = PRESSED;
    visuals.widgets.open.bg_fill = COMMAND;
    visuals.widgets.open.weak_bg_fill = COMMAND;
    visuals.disabled_alpha = 0.5;
    style.visuals = visuals;
    ctx.set_style(style);
}

/// WinForms IFMAppView.MarketDataFeedColors for a running/stopped feed.
/// The Rust control is display-only until feed command parity is implemented.
pub fn feed_command_palette(running: Option<bool>) -> (egui::Color32, egui::Color32) {
    match running {
        Some(true) => (egui::Color32::RED, egui::Color32::WHITE),
        Some(false) => (egui::Color32::from_rgb(50, 205, 50), egui::Color32::BLACK),
        None => (egui::Color32::from_rgb(105, 105, 105), egui::Color32::GRAY),
    }
}

/// Same semantic palette as WinForms IFMAppView.MarketDataFeedHealthColors.
pub fn feed_health_palette(state: &str) -> (egui::Color32, egui::Color32) {
    match state {
        "Healthy" => (egui::Color32::from_rgb(50, 205, 50), egui::Color32::BLACK),
        "Intermittent" => (egui::Color32::YELLOW, egui::Color32::BLACK),
        "Critical" => (egui::Color32::RED, egui::Color32::WHITE),
        "Recovering" | "OffHoursDegraded" => {
            (egui::Color32::from_rgb(255, 140, 0), egui::Color32::BLACK)
        }
        "OffHoursActive" => (egui::Color32::from_rgb(70, 130, 180), egui::Color32::WHITE),
        _ => (egui::Color32::from_rgb(105, 105, 105), egui::Color32::WHITE),
    }
}

pub fn feed_health_text(state: &str) -> &'static str {
    match state {
        "Healthy" => "Feed Health: Green",
        "Intermittent" => "Feed Health: Yellow",
        "Critical" => "Feed Health: Red",
        "Recovering" => "Feed Health: Orange / Resetting",
        "OffHoursActive" => "Feed Health: Off-hours Active",
        "OffHoursDegraded" => "Feed Health: Off-hours Degraded",
        "Inactive" => "Feed Health: Stopped",
        _ => "Feed Health: Unknown",
    }
}

/// Tab headers share the grid/header palette; selected tabs keep the blue highlight.
pub fn tab_label(ui:&mut egui::Ui,selected:bool,label:&str)->egui::Response{
    let fill=if selected{ui.visuals().selection.bg_fill}else{COMMAND};
    let text=egui::RichText::new(label).color(if selected{FOREGROUND}else{egui::Color32::from_rgb(211,211,211)});
    let text=if selected{text}else{text.font(egui::FontId::new(FONT_SIZE,egui::FontFamily::Proportional))};
    let button=egui::Button::selectable(selected,text).frame(true).frame_when_inactive(true).fill(fill);
    ui.add(if selected{button}else{button.stroke(egui::Stroke::NONE)})
}
pub fn tab_value<T:PartialEq>(ui:&mut egui::Ui,current:&mut T,value:T,label:&str)->egui::Response{
    let mut response=tab_label(ui,*current==value,label);
    if response.clicked()&&*current!=value{*current=value;response.mark_changed();}
    response
}


/// Shared foreground for field captions and grid headers in both desktop clients.
pub const CAPTION_FOREGROUND: egui::Color32 = egui::Color32::from_rgb(174, 198, 239);

pub fn label_text(text: impl Into<egui::WidgetText>) -> egui::WidgetText {
    let text = text.into();
    if text.text().trim_end().ends_with(':') {
        text.color(CAPTION_FOREGROUND)
    } else {
        text
    }
}

/// Retains the native label's sizing, wrapping and interaction options.
pub fn label_widget(text: impl Into<egui::WidgetText>) -> egui::Label {
    egui::Label::new(label_text(text))
}

/// Field caption, including captions without trailing punctuation.
pub fn caption_widget(text: impl Into<egui::WidgetText>) -> egui::Label {
    egui::Label::new(text.into().color(CAPTION_FOREGROUND))
}

pub trait TradingLabelUi {
    fn trading_caption(&mut self, text: impl Into<egui::WidgetText>) -> egui::Response;
    fn trading_label(&mut self, text: impl Into<egui::WidgetText>) -> egui::Response;
    fn trading_colored_label(&mut self, color: impl Into<egui::Color32>, text: impl Into<egui::WidgetText>) -> egui::Response;
}
impl TradingLabelUi for egui::Ui {
    fn trading_caption(&mut self, text: impl Into<egui::WidgetText>) -> egui::Response {
        self.add(caption_widget(text))
    }
    fn trading_label(&mut self, text: impl Into<egui::WidgetText>) -> egui::Response {
        self.add(label_widget(text))
    }
    fn trading_colored_label(&mut self, color: impl Into<egui::Color32>, text: impl Into<egui::WidgetText>) -> egui::Response {
        self.add(label_widget(text.into().color(color)))
    }
}

/// Mirrors the shared WinForms shell command state without issuing backend work during rendering.
pub fn feed_command_button(ui: &mut egui::Ui, state: &crate::sidebar::FeedControl) -> bool {
    let label = if state.active { "Stop Market Feeds" } else { "Start Market Feeds" };
    let enabled = state.ready && state.can_toggle && !state.busy;
    let (background, foreground) = feed_command_palette(enabled.then_some(state.active));
    ui.scope(|ui| {
        ui.visuals_mut().disabled_alpha = 1.0;
        ui.add_enabled(enabled,
            egui::Button::new(egui::RichText::new(label).color(foreground)).fill(background).frame(true))
            .on_disabled_hover_text(if state.busy { "Waiting for the backend feed command to complete." } else { "Feed command unavailable until the shell and market session are ready." })
            .clicked()
    }).inner
}
