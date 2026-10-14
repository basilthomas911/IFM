from pathlib import Path
import csv,re
root=Path(__file__).resolve().parents[3]
output=root/'Documents/system/IFM-WinForms-Control-Inventory.csv'
types={'Button':'egui::Button','TextBox':'egui::TextEdit','RichTextBox':'egui::TextEdit multiline','ComboBox':'egui::ComboBox','CheckBox':'egui::Checkbox','TreeView':'egui::CollapsingHeader','PropertyGrid':'typed read-only egui::Grid / editors','ListView':'viewport ScrollArea / egui_extras::TableBuilder','DataGridView':'egui_extras::TableBuilder','Chart':'egui_plot','TabControl':'selectable tabs; egui_dock if docking required','SplitContainer':'resizable panels','DateTimePicker':'egui_extras datepicker','NumericUpDown':'egui::DragValue','ProgressBar':'egui::ProgressBar','Label':'egui::Label','Panel':'egui panels','GroupBox':'egui::Frame','RadioButton':'egui::RadioButton','MenuStrip':'egui menu','ToolStrip':'egui toolbar','ListBox':'egui selectable list','CheckedListBox':'egui checkbox list','FlowLayoutPanel':'egui wrapped layout','TableLayoutPanel':'egui::Grid'}
rows=[]
for path in sorted((root/'TomasAI.IFM.UI.Net.Views').rglob('*.cs')):
 if 'obj' in path.parts or 'bin' in path.parts:continue
 text=path.read_text(encoding='utf-8-sig')
 classes=re.findall(r'class\s+(\w+)\s*:\s*([^\n{]+)',text)
 controls=[]
 for kind,replacement in types.items():
  for match in re.finditer(r'\b'+kind+r'\b',text):
   line=text.count('\n',0,match.start())+1
   controls.append((line,kind,replacement))
 for line,kind,replacement in controls:
  rows.append([str(path.relative_to(root)).replace('\\','/'),line,'; '.join(c[0] for c in classes),kind,replacement,'source occurrence; reconcile runtime-generated instances',text.splitlines()[line-1].strip()[:240]])
output.parent.mkdir(parents=True,exist_ok=True)
with output.open('w',newline='',encoding='utf-8') as f:
 w=csv.writer(f);w.writerow(['Source','Line','View','ControlType','RustCandidate','ReviewStatus','SourceExpression']);w.writerows(rows)
print(f'{len(rows)} control occurrences in {len(set(r[0] for r in rows))} files -> {output}')
