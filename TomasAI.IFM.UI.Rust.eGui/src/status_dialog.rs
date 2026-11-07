//! Presentation only: consistent status icons and centered acknowledgement.
use eframe::egui::{self, Color32, Pos2, Rect, Stroke, Vec2};

/// Outcome severity; colors supplement the visible text and icon shape.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum Severity {
    /// A failed operation requiring attention.
    #[default]
    Error,
    /// An explicitly verified successful outcome.
    Success,
    /// A warning that does not assert success or failure.
    Warning,
    /// A question requiring an explicit Yes or No decision.
    Question,
}
impl Severity {
    /// Screen-reader/visible status name.
    pub fn title(self) -> &'static str {
        match self {
            Self::Error => "Application Error",
            Self::Success => "Recovery Completed",
            Self::Warning => "Application Warning",
            Self::Question => "Confirmation",
        }
    }
}

/// Immutable notification for the single latest-dialog slot.
#[derive(Clone, Debug)]
pub struct Notice {
    /// UI-local monotonic sequence, independent of backend operation identity.
    pub sequence: u64,
    /// Original outcome text including backend operation correlation.
    pub message: String,
    /// Explicit outcome severity; never inferred from words in the message.
    pub severity: Severity,
}

/// Measured layout used by rendering and headless geometry verification.
pub struct Layout {
    /// Complete content rectangle.
    pub content: Rect,
    /// Rendered message bounds, including any wrapped lines.
    pub message: Rect,
    /// Fixed, equal-sized vector-icon rectangle.
    pub icon: Rect,
    /// Centered acknowledgement button.
    pub ok: Rect,
    /// Whether the user acknowledged this dialog in the current frame.
    pub acknowledged: bool,
    /// The No button for a confirmation.
    pub no: Option<Rect>,
    /// Whether the user selected No.
    pub declined: bool,
}


/// Draws equal-sized X/check/triangle icons without depending on glyph fonts.
pub fn icon(ui: &mut egui::Ui, severity: Severity) -> Rect {
    let (rect, response) = ui.allocate_exact_size(Vec2::splat(56.0), egui::Sense::hover());
    response.on_hover_text(severity.title());
    let area = rect.shrink(5.0);
    let painter = ui.painter();
    match severity {
        Severity::Error => {
            let stroke = Stroke::new(6.0_f32, Color32::from_rgb(225, 45, 45));
            painter.line_segment([area.left_top(), area.right_bottom()], stroke);
            painter.line_segment([area.right_top(), area.left_bottom()], stroke);
        }
        Severity::Success => {
            let stroke = Stroke::new(6.0_f32, Color32::from_rgb(25, 180, 70));
            let middle = Pos2::new(area.left() + area.width() * 0.35, area.bottom() - 4.0);
            painter.line_segment([Pos2::new(area.left(), area.center().y), middle], stroke);
            painter.line_segment([middle, Pos2::new(area.right(), area.top() + 4.0)], stroke);
        }
        Severity::Warning => {
            painter.add(egui::Shape::convex_polygon(
                vec![
                    Pos2::new(area.center().x, area.top()),
                    area.right_bottom(),
                    area.left_bottom(),
                ],
                Color32::from_rgb(250, 195, 30),
                Stroke::new(2.0_f32, Color32::from_rgb(120, 90, 0)),
            ));
            let x = area.center().x;
            painter.line_segment(
                [
                    Pos2::new(x, area.top() + 15.0),
                    Pos2::new(x, area.bottom() - 14.0),
                ],
                Stroke::new(4.0_f32, Color32::BLACK),
            );
            painter.circle_filled(Pos2::new(x, area.bottom() - 7.0), 2.5, Color32::BLACK);
        }
        Severity::Question => {
            painter.circle_stroke(area.center(), area.width() * 0.5,
                Stroke::new(3.0_f32, Color32::from_rgb(65, 155, 245)));
            painter.text(area.center(), egui::Align2::CENTER_CENTER, "?",
                egui::FontId::proportional(42.0), Color32::from_rgb(65, 155, 245));
        }
    }
    rect
}


/// Fits the message's longest line plus its icon, with room for the title and OK.
/// Long messages wrap only when that natural width exceeds the window bounds.
pub fn body(ui: &mut egui::Ui, message: &str, severity: Severity) -> Layout {
    message_body(ui, severity.title(), message, severity, None)
}


/// Uses the status dialog layout for a question with centered Yes and No buttons.
pub fn confirmation(
    ui: &mut egui::Ui,
    title: &str,
    message: &str,
    yes_enabled: bool,
    no_enabled: bool,
) -> Layout {
    message_body(ui, title, message, Severity::Question, Some((yes_enabled, no_enabled)))
}


/// Measures and draws the common title, message, icon and button row.
fn message_body(
    ui: &mut egui::Ui,
    title: &str,
    message: &str,
    severity: Severity,
    confirmation: Option<(bool, bool)>,
) -> Layout {
    let title = crate::presentation::dialog_title(title);
    let message_width = egui::WidgetText::from(message)
        .into_galley(
            ui,
            Some(egui::TextWrapMode::Extend),
            f32::INFINITY,
            egui::FontSelection::Default,
        )
        .size()
        .x;
    let title_width = egui::WidgetText::from(title.clone().heading())
        .into_galley(
            ui,
            Some(egui::TextWrapMode::Extend),
            f32::INFINITY,
            egui::TextStyle::Heading,
        )
        .size()
        .x;
    let gap = ui.spacing().item_spacing.x;
    let available = (ui.ctx().input(|input| input.screen_rect().width()) - 64.0).max(1.0);
    let width = (message_width + gap + 56.0)
        .max(title_width)
        .max(if confirmation.is_some() { 180.0 + gap } else { 90.0 })
        .min(available)
        .ceil();
    ui.set_width(width);
    let start = ui.cursor().min;
    ui.heading(title);
    ui.add_space(10.0);
    let mut icon_rect = Rect::NOTHING;
    let mut message_rect = Rect::NOTHING;
    ui.horizontal_top(|ui| {
        ui.allocate_ui(Vec2::new((width - gap - 56.0).max(1.0), 56.0), |ui| {
            message_rect = ui.add(egui::Label::new(message).wrap()).rect;
        });
        icon_rect = icon(ui, severity);
    });
    ui.add_space(14.0);
    let buttons_width = if confirmation.is_some() { 180.0 + gap } else { 90.0 };
    let (row, _) = ui.allocate_exact_size(Vec2::new(width, 30.0), egui::Sense::hover());
    let first = Rect::from_min_size(
        Pos2::new(row.center().x - buttons_width * 0.5, row.top()), Vec2::new(90.0, 30.0),
    );
    let (button, no) = match confirmation {
        Some((yes_enabled, no_enabled)) => {
            let yes = ui.add_enabled_ui(yes_enabled, |ui| ui.put(first, egui::Button::new("Yes"))).inner;
            let second = first.translate(Vec2::new(90.0 + gap, 0.0));
            let no = ui.add_enabled_ui(no_enabled, |ui| ui.put(second, egui::Button::new("No"))).inner;
            (yes, Some(no))
        }
        None => (ui.put(first, egui::Button::new("OK")), None),
    };
    Layout {
        content: Rect::from_min_max(start, Pos2::new(start.x + width, ui.cursor().min.y)),
        message: message_rect,
        icon: icon_rect,
        ok: button.rect,
        acknowledged: button.clicked(),
        no: no.as_ref().map(|button| button.rect),
        declined: no.is_some_and(|button| button.clicked()),
    }
}
