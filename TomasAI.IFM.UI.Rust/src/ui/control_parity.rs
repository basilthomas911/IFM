//! Native equivalents for WinForms report lists. Selection belongs to the row,
//! while data retrieval remains owned by the managed view model.
use eframe::egui::{self,Color32};
pub fn report_list(ui:&mut egui::Ui,id:&str,columns:&[(&str,f32)],row_count:usize,selected:Option<usize>,height:f32,mut text:impl FnMut(usize,usize)->String)->(Option<usize>,usize){
 let scale=ui.ctx().pixels_per_point();let row_height=24./scale;let bounds=egui::Rect::from_min_size(ui.cursor().min,egui::vec2(ui.available_width(),height));let mut clicked=None;let mut drawn=0;
 egui::ScrollArea::horizontal().id_salt(format!("{id}-horizontal")).auto_shrink([false,false]).max_width(bounds.width()).show(ui,|ui|{
  ui.set_min_width(columns.iter().map(|(_,w)|w/scale).sum::<f32>().max(bounds.width()-14./scale));
  ui.spacing_mut().item_spacing.x=0.;
  ui.visuals_mut().selection.bg_fill=Color32::from_rgb(0,120,215);
  let sizing=ui.is_sizing_pass();
  let mut table=egui_extras::TableBuilder::new(ui).id_salt(id).striped(false).sense(egui::Sense::click()).cell_layout(egui::Layout::left_to_right(egui::Align::Center)).min_scrolled_height((height-row_height).max(0.)).max_scroll_height((height-row_height).max(0.));
  for(_,width)in columns{let column=if sizing{egui_extras::Column::exact(*width/scale)}else{egui_extras::Column::initial(*width/scale).at_least(40./scale).clip(true)};table=table.column(column.resizable(true));}
  table=table.column(egui_extras::Column::remainder().at_least(0.));
  table.header(row_height,|mut row|{for(caption,_)in columns{row.col(|ui|{ui.painter().rect_filled(ui.max_rect(),0.,Color32::from_rgb(45,45,48));ui.add(crate::presentation::label_widget(egui::RichText::new(*caption).color(crate::presentation::CAPTION_FOREGROUND)).truncate());});}row.col(|ui|{ui.painter().rect_filled(ui.max_rect(),0.,Color32::from_rgb(45,45,48));});})
   .bordered_body(row_height, |body| {body.rows(row_height,row_count,|mut row|{drawn+=1;let index=row.index();row.set_selected(selected==Some(index));for column in 0..columns.len(){row.col(|ui|{ui.add(crate::presentation::label_widget(egui::RichText::new(text(index,column)).color(Color32::WHITE)).truncate());});}row.col(|_|{});if row.response().clicked(){clicked=Some(index);}});});
 });
 ui.painter().rect_stroke(bounds,0.,egui::Stroke::new(1./scale,Color32::from_gray(90)),egui::StrokeKind::Inside);(clicked,drawn)
}

/// WinForms CenterParent includes the native title bar and frame. eframe's
/// viewport rectangles may be unavailable; query our own UI thread's window.
#[cfg(target_os="windows")]
pub fn native_parent_center(scale:f32)->Option<egui::Pos2>{
 #[repr(C)]struct Rect{left:i32,top:i32,right:i32,bottom:i32}
 #[repr(C)]struct Point{x:i32,y:i32}
 #[link(name="user32")]unsafe extern "system"{fn GetActiveWindow()->*mut std::ffi::c_void;fn GetWindowRect(window:*mut std::ffi::c_void,rect:*mut Rect)->i32;fn ClientToScreen(window:*mut std::ffi::c_void,point:*mut Point)->i32;}
 unsafe{let window=GetActiveWindow();if window.is_null(){return None;}let mut r=Rect{left:0,top:0,right:0,bottom:0};let mut origin=Point{x:0,y:0};if GetWindowRect(window,&mut r)==0||ClientToScreen(window,&mut origin)==0{return None;}Some(egui::pos2(((r.left+r.right)as f32/2.-origin.x as f32)/scale,((r.top+r.bottom)as f32/2.-origin.y as f32)/scale))}
}
#[cfg(not(target_os="windows"))]
pub fn native_parent_center(_:f32)->Option<egui::Pos2>{None}


/// Draw rules only after egui has resolved the scroll viewport's final bounds.
/// During body layout its provisional clip can extend below a shrinking grid.
pub trait BorderedTable {
    fn bordered_body<F>(self, header_height: f32, contents: F) -> egui::scroll_area::ScrollAreaOutput<()>
    where F: for<'b> FnOnce(egui_extras::TableBody<'b>);
}
struct ColumnBorders {
    painter: egui::Painter,
    columns: Vec<f32>,
    clip: egui::Rect,
    spacing: f32,
    stroke: egui::Stroke,
}
impl ColumnBorders {
    fn capture(body: &mut egui_extras::TableBody<'_>) -> Self {
        let bounds=body.max_rect();let widths=body.widths().to_vec();let ui=body.ui_mut();
        let spacing=ui.spacing().item_spacing;let mut x=bounds.left();let mut columns=Vec::new();
        for width in widths.iter().take(widths.len().saturating_sub(1)) {
            x+=width;columns.push(x+spacing.x/2.);x+=spacing.x;
        }
        Self {painter:ui.painter().clone(),columns,clip:ui.clip_rect(),spacing:spacing.y,stroke:egui::Stroke::new(1./ui.ctx().pixels_per_point(),crate::presentation::BORDER)}
    }
    fn paint(self, viewport: egui::Rect, header_height: f32) {
        let mut grid=viewport;
        grid.min.y-=header_height+if header_height>0. {self.spacing}else{0.};
        let mut parent_clip=self.clip;
        parent_clip.min.y-=header_height+if header_height>0. {self.spacing}else{0.};
        let clip=grid.intersect(parent_clip);
        if !clip.is_positive() {return;}
        let painter=self.painter.with_clip_rect(clip);
        let inset=self.stroke.width/2.;
        for x in self.columns {
            if x>=clip.left() && x<=clip.right() {
                painter.vline(x,clip.top()+inset..=clip.bottom()-inset,self.stroke);
            }
        }
    }
}
macro_rules! bordered_table {
    ($table:ty) => {
        impl BorderedTable for $table {
            fn bordered_body<F>(self, header_height: f32, contents: F) -> egui::scroll_area::ScrollAreaOutput<()>
            where F: for<'b> FnOnce(egui_extras::TableBody<'b>) {
                let mut borders=None;
                let output=self.body(|mut body| {borders=Some(ColumnBorders::capture(&mut body));contents(body);});
                if let Some(borders)=borders {borders.paint(output.inner_rect,header_height);}
                output
            }
        }
    }
}
bordered_table!(egui_extras::Table<'_>);
bordered_table!(egui_extras::TableBuilder<'_>);
