//! Shared native trading theme.
//!
//! This is the common source for colors, Microsoft Sans Serif sizing, caption colors,
//! tab headers, dialog titles, scrollbar contrast/width, and reference layout ratios.
//! Apply the style once to the context; use the widget helpers in feature views so
//! new controls follow the same palette. Conditional business/status colors remain
//! explicit at their call sites.

//! WinForms presentation baseline. Values are logical pixels at 96 DPI.
//! Source: UI.Net.Views/Presentation/DarkTradingTheme.cs and counterpart layouts.
use eframe::egui;

/// 10-point WinForms font size converted to logical pixels at 96 DPI.
pub const FONT_SIZE: f32 = 10.0 * 96.0 / 72.0;
/// Default black trading-surface background.
pub const BACKGROUND: egui::Color32 = egui::Color32::BLACK;
/// Default white value/text foreground.
pub const FOREGROUND: egui::Color32 = egui::Color32::WHITE;
/// Shared gray grid/control border color.
pub const BORDER: egui::Color32 = egui::Color32::GRAY;
/// Medium-gray scrollbar handle color, shared by idle, hovered, and dragged states.
pub const SCROLLBAR: egui::Color32 = egui::Color32::from_rgb(128, 128, 128);
/// Dark-gray command and unselected-header background.
pub const COMMAND: egui::Color32 = egui::Color32::from_rgb(45, 45, 48);
/// Command background while the pointer hovers.
pub const HOVER: egui::Color32 = egui::Color32::from_rgb(62, 62, 64);
/// Command background while the primary pointer button is held.
pub const PRESSED: egui::Color32 = egui::Color32::from_rgb(31, 31, 32);
/// Reference main-window client size in logical points for nonmaximized startup.
pub const MAIN_CLIENT_SIZE: [f32; 2] = [2637.0, 872.0];
/// Fraction of the parameter editor width reserved for navigation, based on the original form.
pub const PARAMETER_NAVIGATION_RATIO: f32 = 430.0 / 1200.0;
/// Fraction of the strategy catalog width reserved for navigation.
pub const CATALOG_NAVIGATION_RATIO: f32 = 0.29;
/// Fraction of details-view height reserved for its node tree.
pub const DETAILS_TREE_RATIO: f32 = 140.0 / 300.0;
/// Fraction of scheduler log space reserved for the execution tree.
pub const SCHEDULE_LOG_TREE_RATIO: f32 = 0.4;

/// Solid medium-gray handles shared by grids and their nested scroll areas.
pub fn grid_scroll_style(pixels_per_point: f32) -> egui::style::ScrollStyle {
    let mut scroll = egui::style::ScrollStyle::solid();
    scroll.bar_width = 16.0 / pixels_per_point;
    scroll.bar_inner_margin = 2.0 / pixels_per_point;
    scroll.handle_min_length = 24.0 / pixels_per_point;
    scroll.foreground_color = true;
    scroll
}

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
    style.spacing.scroll = grid_scroll_style(ctx.pixels_per_point());
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
        // egui scrollbars use the interaction state's foreground stroke; text
        // keeps its separate override above and explicit caption/status colors.
        widget.fg_stroke = egui::Stroke::new(1.0_f32, SCROLLBAR);
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

/// Map a published feed-health state to the main-toolbar label, with an Unknown fallback.
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
/// Draw a shared tab header, update the selected value on click, and mark the response changed only when selection changes.
pub fn tab_value<T:PartialEq>(ui:&mut egui::Ui,current:&mut T,value:T,label:&str)->egui::Response{
    let mut response=tab_label(ui,*current==value,label);
    if response.clicked()&&*current!=value{*current=value;response.mark_changed();}
    response
}


/// Shared foreground for field captions and grid headers in both desktop clients.
pub const CAPTION_FOREGROUND: egui::Color32 = egui::Color32::from_rgb(174, 198, 239);

/// Apply pastel-blue caption styling to text ending in a colon; leave ordinary values with their supplied styling.
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

/// Shared caption/value label helpers implementing the native trading text conventions.
pub trait TradingLabelUi {
    /// Draw a field caption with the shared pastel-blue foreground.
    fn trading_caption(&mut self, text: impl Into<egui::WidgetText>) -> egui::Response;
    /// Draw caption-aware trading text while retaining explicit value styling.
    fn trading_label(&mut self, text: impl Into<egui::WidgetText>) -> egui::Response;
    /// Draw trading text with the explicit foreground color requested by its caller.
    fn trading_colored_label(&mut self, color: impl Into<egui::Color32>, text: impl Into<egui::WidgetText>) -> egui::Response;
}
impl TradingLabelUi for egui::Ui {
    /// Draw a field caption with the shared pastel-blue foreground.
    fn trading_caption(&mut self, text: impl Into<egui::WidgetText>) -> egui::Response {
        self.add(caption_widget(text))
    }
    /// Draw caption-aware trading text while retaining explicit value styling.
    fn trading_label(&mut self, text: impl Into<egui::WidgetText>) -> egui::Response {
        self.add(label_widget(text))
    }
    /// Draw trading text with the explicit foreground color requested by its caller.
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

/// Shared caption styling for every dialog title.
pub fn dialog_title(text: impl Into<egui::RichText>) -> egui::RichText {
    text.into().color(CAPTION_FOREGROUND)
}

/// Checkbox with pastel-blue outline and check mark, preserving the supplied text color.
/// Styling is local so scrollbar handles and other controls retain their own colors.
pub fn checkbox<'a>(checked: &'a mut bool, text: impl Into<egui::WidgetText>) -> impl egui::Widget + 'a {
    let text = text.into();
    move |ui: &mut egui::Ui| {
        let previous = ui.style().clone();
        let visuals = ui.visuals_mut();
        for widget in [
            &mut visuals.widgets.noninteractive,
            &mut visuals.widgets.inactive,
            &mut visuals.widgets.hovered,
            &mut visuals.widgets.active,
            &mut visuals.widgets.open,
        ] {
            widget.bg_stroke.color = CAPTION_FOREGROUND;
            widget.fg_stroke.color = CAPTION_FOREGROUND;
        }
        let response = ui.add(egui::Checkbox::new(checked, text));
        ui.set_style(previous);
        response
    }
}
