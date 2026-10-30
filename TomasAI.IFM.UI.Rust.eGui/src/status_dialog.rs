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
}
impl Severity {
    /// Screen-reader/visible status name.
    pub fn title(self) -> &'static str {
        match self {Self::Error=>"Application Error",Self::Success=>"Recovery Completed",Self::Warning=>"Application Warning"}
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
    /// Fixed, equal-sized vector-icon rectangle.
    pub icon: Rect,
    /// Centered acknowledgement button.
    pub ok: Rect,
    /// Whether the user acknowledged this dialog in the current frame.
    pub acknowledged: bool,
}
/// Draws equal-sized X/check/triangle icons without depending on glyph fonts.
pub fn icon(ui: &mut egui::Ui, severity: Severity) -> Rect {
    let (rect,response)=ui.allocate_exact_size(Vec2::splat(56.0),egui::Sense::hover());
    response.on_hover_text(severity.title());
    let area=rect.shrink(5.0);
    let painter=ui.painter();
    match severity {
        Severity::Error=>{
            let stroke=Stroke::new(6.0_f32,Color32::from_rgb(225,45,45));
            painter.line_segment([area.left_top(),area.right_bottom()],stroke);
            painter.line_segment([area.right_top(),area.left_bottom()],stroke);
        }
        Severity::Success=>{
            let stroke=Stroke::new(6.0_f32,Color32::from_rgb(25,180,70));
            let middle=Pos2::new(area.left()+area.width()*0.35,area.bottom()-4.0);
            painter.line_segment([Pos2::new(area.left(),area.center().y),middle],stroke);
            painter.line_segment([middle,Pos2::new(area.right(),area.top()+4.0)],stroke);
        }
        Severity::Warning=>{
            painter.add(egui::Shape::convex_polygon(vec![Pos2::new(area.center().x,area.top()),area.right_bottom(),area.left_bottom()],Color32::from_rgb(250,195,30),Stroke::new(2.0_f32,Color32::from_rgb(120,90,0))));
            let x=area.center().x;
            painter.line_segment([Pos2::new(x,area.top()+15.0),Pos2::new(x,area.bottom()-14.0)],Stroke::new(4.0_f32,Color32::BLACK));
            painter.circle_filled(Pos2::new(x,area.bottom()-7.0),2.5,Color32::BLACK);
        }
    }
    rect
}
/// Renders message left, status icon right, and OK centered beneath both.
pub fn body(ui: &mut egui::Ui, message: &str, severity: Severity) -> Layout {
    let width=(ui.ctx().input(|input|input.screen_rect().width())-64.0).clamp(220.0,620.0);
    ui.set_width(width);
    let start=ui.cursor().min;
    ui.heading(crate::presentation::dialog_title(severity.title()));
    ui.add_space(10.0);
    let mut icon_rect=Rect::NOTHING;
    ui.horizontal_top(|ui| {
        ui.allocate_ui(Vec2::new(width-76.0,56.0),|ui|{ui.add(egui::Label::new(message).wrap());});
        icon_rect=icon(ui,severity);
    });
    ui.add_space(14.0);
    let button=ui.horizontal(|ui| {ui.add_space((width-90.0)*0.5);ui.add_sized([90.0,30.0],egui::Button::new("OK"))}).inner;
    Layout{content:Rect::from_min_max(start,Pos2::new(start.x+width,ui.cursor().min.y)),icon:icon_rect,ok:button.rect,acknowledged:button.clicked()}
}
