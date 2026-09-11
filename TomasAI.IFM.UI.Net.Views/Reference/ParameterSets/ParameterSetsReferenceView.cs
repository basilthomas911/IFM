using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System.ComponentModel;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Reference.ParameterSets;
using TomasAI.IFM.UI.Net.Views.Presentation;
namespace TomasAI.IFM.UI.Net.Views.Reference.ParameterSets;

public sealed class ParameterSetsReferenceView:DarkTradingView,IControlCommand
{
 readonly IParameterSetsApi api;
 readonly ParameterSetEditorModel editor=new();
 readonly ListBox areas=new(){Dock=DockStyle.Fill,AccessibleName="Parameter Set"};
 readonly ListBox components=new(){Dock=DockStyle.Fill,AccessibleName="Parameter set components"};
 readonly ListBox versions=new(){Dock=DockStyle.Fill,AccessibleName="Parameter set versions"};
 readonly ComboBox horizon=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=110,AccessibleName="New parameter set horizon"};
 readonly TextBox name=new(){Width=260,AccessibleName="Parameter set name"};
 readonly Label childTitle=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Black,AccessibleName="Parameter detail list title"};
 readonly Label detailTitle=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Black,AccessibleName="Parameter editor title"};
 readonly DataGridView grid=new(){Dock=DockStyle.Fill,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AccessibleName="Regime Discovery signal requirements"};
 readonly DataGridView observationGrid=new(){Dock=DockStyle.Fill,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AccessibleName="Regime Discovery observation requirements"};
 readonly TabControl detailTabs=new(){Dock=DockStyle.Fill};
 readonly TabPage validationTab=new("Validation");
 readonly DataGridView validationGrid=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AccessibleName="Parameter validation issues"};
 readonly DataGridView parametersGrid=new(){Dock=DockStyle.Fill,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AccessibleName="Regime Discovery calculation parameters"};
 readonly ListBox parameterGroups=new(){Dock=DockStyle.Fill,AccessibleName="Calculation parameter groups"};
 readonly Label parameterGroupTitle=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(8,0,0,0),AccessibleName="Calculation parameter group title"};
 BindingList<FieldRow> fields=[];
 readonly Label status=new(){Dock=DockStyle.Bottom,Height=42,AutoEllipsis=true,AccessibleName="Parameter status"};
 readonly FlowLayoutPanel toolbar=new(){Dock=DockStyle.Top,Height=72,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true};
 readonly CancellationTokenSource lifetime=new();
 ParameterSetVersion[] loaded=[];
 ParameterComponentSummary[] catalogue=[];
 bool bindingCatalogue;
 bool bindingParameterGroups;
 BindingList<SignalRow> rows=[];
 BindingList<ObservationRow> observationRows=[];
 bool busy;
 EditMode editMode;
 public event EventHandler? StateChanged;
 public bool IsAdding=>editMode==EditMode.Add;
 public bool IsChanging=>editMode==EditMode.Change;
 public bool IsEditing=>editMode!=EditMode.View;
 public bool IsBusy=>busy;
 public bool CanAdd=>!busy&&!IsEditing&&components.SelectedItem is ParameterComponentSummary{CanEdit:true};
 public bool CanChange=>!busy&&!IsEditing&&editor.Selected is not null&&editor.CanEdit;
 public bool CanRemove=>!busy&&!IsEditing&&editor.Selected is {Status:ParameterVersionStatus.Published};
 public bool CanSave=>!busy&&IsEditing&&editor.IsEditing&&editor.Parameters is not null&&!string.IsNullOrWhiteSpace(name.Text);
 public bool CanChangeRemove=>CanChange;
 public bool CanImport=>false;

 public ParameterSetsReferenceView(IParameterSetsApi api)
 {
  this.api=api;Name=nameof(ParameterSetsReferenceView);
  Panel Header(string title,Label? value=null)
  {
   var panel=new Panel{Dock=DockStyle.Fill,BackColor=Color.Gray};
   var label=value??new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Black};
   label.Text=title;label.Font=new Font(label.Font,FontStyle.Regular);panel.Controls.Add(label);return panel;
  }

  var split=new SplitContainer{Size=new Size(1200,700),Dock=DockStyle.Fill,Orientation=Orientation.Vertical,SplitterDistance=430,SplitterWidth=5,Panel1MinSize=280,Panel2MinSize=520,AccessibleName="Parameter set master detail layout"};
  split.Panel1.BackColor=Color.FromArgb(64,64,64);split.Panel2.BackColor=Color.FromArgb(64,64,64);
  var navigation=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(0)};
  navigation.ColumnStyles.Add(new(SizeType.Percent,100));
  navigation.RowStyles.Add(new(SizeType.Absolute,30));
  navigation.RowStyles.Add(new(SizeType.Percent,45));
  navigation.RowStyles.Add(new(SizeType.Absolute,30));
  navigation.RowStyles.Add(new(SizeType.Percent,55));
  navigation.Controls.Add(Header("Parameter Set"),0,0);
  navigation.Controls.Add(areas,0,1);
  navigation.Controls.Add(Header(string.Empty,childTitle),0,2);
  navigation.Controls.Add(components,0,3);
  split.Panel1.Controls.Add(navigation);

  var editorLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(0)};
  editorLayout.ColumnStyles.Add(new(SizeType.Percent,100));
  editorLayout.RowStyles.Add(new(SizeType.Absolute,30));
  editorLayout.RowStyles.Add(new(SizeType.Absolute,125));
  editorLayout.RowStyles.Add(new(SizeType.Percent,100));
  editorLayout.Controls.Add(Header(string.Empty,detailTitle),0,0);
  editorLayout.Controls.Add(versions,0,1);

  var detail=new Panel{Dock=DockStyle.Fill};var tabs=detailTabs;
  var signalsTab=new TabPage("Signals");signalsTab.Controls.Add(grid);
  var observationsTab=new TabPage("Observations");observationsTab.Controls.Add(observationGrid);
  var parametersTab=new TabPage("Calculation parameters");
  var calculationSplit=new SplitContainer{Size=new Size(600,400),Dock=DockStyle.Fill,Orientation=Orientation.Vertical,SplitterWidth=5,Panel1MinSize=140,Panel2MinSize=300,SplitterDistance=190,AccessibleName="Calculation parameter group layout"};
  calculationSplit.Panel1.Controls.Add(parameterGroups);
  var propertyLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};
  propertyLayout.ColumnStyles.Add(new(SizeType.Percent,100));propertyLayout.RowStyles.Add(new(SizeType.Absolute,30));propertyLayout.RowStyles.Add(new(SizeType.Percent,100));
  propertyLayout.Controls.Add(parameterGroupTitle,0,0);propertyLayout.Controls.Add(parametersGrid,0,1);
  calculationSplit.Panel2.Controls.Add(propertyLayout);parametersTab.Controls.Add(calculationSplit);
  tabs.TabPages.Add(signalsTab);tabs.TabPages.Add(observationsTab);tabs.TabPages.Add(parametersTab);validationTab.Controls.Add(validationGrid);tabs.TabPages.Add(validationTab);
  detail.Controls.Add(tabs);detail.Controls.Add(toolbar);detail.Controls.Add(status);
  toolbar.Controls.Add(new Label{Text="New horizon",AutoSize=true,Margin=new Padding(3,7,3,0)});
  horizon.Items.AddRange(new object[]{TimeFrameType.Daily,TimeFrameType.Weekly,TimeFrameType.Monthly});horizon.SelectedIndex=0;toolbar.Controls.Add(horizon);
  toolbar.Controls.Add(new Label{Text="Name",AutoSize=true,Margin=new Padding(12,7,3,0)});toolbar.Controls.Add(name);
  Button AddAction(string label,Func<Task> action){var button=new Button{Text=label,AutoSize=true};button.Click+=async(_,_)=>await Run(action);toolbar.Controls.Add(button);return button;}
  AddAction("Refresh",RefreshAsync);AddAction("Legacy versions",LegacyAsync);AddAction("Payload",PayloadAsync);AddAction("Schema",SchemaAsync);
  AddAction("Intervals",IntervalsAsync);AddAction("Validate",ValidateAsync);AddAction("Publish",PublishAsync);AddAction("Assignment",AssignmentAsync);
  AddAction("Startup preview",StartupPreviewAsync);AddAction("Recent startups",ActiveGenerationsAsync);AddAction("Compare",CompareAsync);AddAction("Operations",OperationsAsync);

  parametersGrid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="Name",HeaderText="Parameter",ReadOnly=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=65});
  parametersGrid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="Value",HeaderText="Value",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=35});
  var signalMetrics=Enum.GetValues<SignalMetricsType>().Where(value=>value!=SignalMetricsType.Unknown).Cast<object>().ToArray();
  object[] timeFrames=[TimeFrameType.FifteenSeconds,TimeFrameType.OneMinute,TimeFrameType.FiveMinutes,
   TimeFrameType.FifteenMinutes,TimeFrameType.OneHour,TimeFrameType.FourHours,TimeFrameType.Daily];
  grid.Columns.Add(new DataGridViewCheckBoxColumn{DataPropertyName="Enabled",HeaderText="Included",Width=55});
  grid.Columns.Add(new DataGridViewComboBoxColumn{DataPropertyName="Metric",HeaderText="Metric",DataSource=signalMetrics,Width=145,FlatStyle=FlatStyle.Flat});
  grid.Columns.Add(new DataGridViewComboBoxColumn{DataPropertyName="TimeFrame",HeaderText="Timeframe",DataSource=timeFrames,Width=105,FlatStyle=FlatStyle.Flat});
  grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="PeriodLength",HeaderText="Period length",Width=85});
  grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="MaximumAgeSeconds",HeaderText="Maximum age (seconds)",Width=105});
  grid.Columns.Add(new DataGridViewCheckBoxColumn{DataPropertyName="Prepare",HeaderText="Prepare at startup",Width=90});
  grid.Columns.Add(new DataGridViewCheckBoxColumn{DataPropertyName="Monitor",HeaderText="Monitor",Width=65});
  grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
  grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize;
  grid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.True;

  var observationMetrics=Enum.GetValues<ObservationMetricsType>().Where(value=>value!=ObservationMetricsType.Unknown).Cast<object>().ToArray();
  observationGrid.Columns.Add(new DataGridViewCheckBoxColumn{DataPropertyName="Enabled",HeaderText="Included",Width=70});
  observationGrid.Columns.Add(new DataGridViewComboBoxColumn{DataPropertyName="Metric",HeaderText="Observation",DataSource=observationMetrics,Width=190,FlatStyle=FlatStyle.Flat});
  observationGrid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="MaximumAgeSeconds",HeaderText="Maximum age (seconds)",Width=125});
  observationGrid.Columns.Add(new DataGridViewCheckBoxColumn{DataPropertyName="Prepare",HeaderText="Prepare at startup",Width=105});
  observationGrid.Columns.Add(new DataGridViewCheckBoxColumn{DataPropertyName="Monitor",HeaderText="Monitor",Width=80});
  observationGrid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
  observationGrid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize;
  observationGrid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.True;
  void GridDataError(object? _,DataGridViewDataErrorEventArgs e)
  {
   e.ThrowException=false;status.Text="Period length and maximum age must be positive whole numbers.";
  }
  grid.DataError+=GridDataError;observationGrid.DataError+=GridDataError;
  editorLayout.Controls.Add(detail,0,2);split.Panel2.Controls.Add(editorLayout);Controls.Add(split);
  versions.SelectedIndexChanged+=(_,_)=>
  {
   if(versions.SelectedItem is not VersionChoice choice)return;
   if(editor.IsEditing)return;
   editor.Load(choice.Value,loaded.Where(x=>x.Reference.SetId==choice.Value.Reference.SetId).Max(x=>x.CatalogRevision));Bind();
  };
  areas.DisplayMember=nameof(AreaChoice.Name);components.DisplayMember=nameof(ParameterComponentSummary.Name);
  areas.SelectedIndexChanged+=async(_,_)=>{if(!bindingCatalogue&&!IsEditing)await Run(async()=>{BindComponents();await LoadVersionsAsync();});};
  components.SelectedIndexChanged+=async(_,_)=>{if(!bindingCatalogue&&!IsEditing)await Run(LoadVersionsAsync);};
  name.TextChanged+=(_,_)=>StateChanged?.Invoke(this,EventArgs.Empty);
  parameterGroups.SelectedIndexChanged+=(_,_)=>BindSelectedParameterGroup();
 } public bool HasWorkingCopy=>editor.IsEditing;
 void IControlCommand.Load(IAppRoot appRoot,Action<bool> dataLoaded)
  => _=Run(async()=>{await RefreshAsync();dataLoaded(CanChangeRemove);});
 public void Unload(){if(!lifetime.IsCancellationRequested)lifetime.Cancel();}
 public void Add(Action<bool> addAction)
 {
  if(editMode==EditMode.View)
   _=Run(async()=>{await NewAsync();editMode=EditMode.Add;addAction(false);});
  else if(editMode==EditMode.Add)
   _=Run(async()=>{await SaveAsync();editMode=EditMode.View;addAction(true);});
 }
 public void Change(Action<bool> changeAction)
 {
  if(editMode==EditMode.View)
   _=Run(async()=>{await EditAsync();editMode=EditMode.Change;changeAction(false);});
  else if(editMode==EditMode.Change)
   _=Run(async()=>{await SaveAsync();editMode=EditMode.View;changeAction(true);});
 }
 public void Remove(){if(CanRemove)_=Run(RetireAsync);}
 public void Import(){}
 public bool Close(Action<bool> closeAction)
 {
  if(editMode==EditMode.View)return true;
  editMode=EditMode.View;
  if(editor.Selected is {} selected)editor.Load(selected,editor.ExpectedRevision);
  else
  {
   editor.Clear();
   if(versions.Items.Count>0)versions.SelectedIndex=0;
  }
  Bind();closeAction(CanChangeRemove);return false;
 } public bool CanLeave()
 {
  if(busy){status.Text="Wait for the current parameter operation to finish before closing this editor.";return false;}
  return !HasWorkingCopy||MessageBox.Show(this,"Discard the working copy?","Parameter Sets",MessageBoxButtons.YesNo)==DialogResult.Yes;
 }
 async Task Run(Func<Task> action)
 {
  if(busy)return;busy=true;toolbar.Enabled=false;StateChanged?.Invoke(this,EventArgs.Empty);
  try{await action();}catch(OperationCanceledException)when(lifetime.IsCancellationRequested){}
  catch(Exception error){status.Text=error.Message;}
  finally{busy=false;if(!IsDisposed){toolbar.Enabled=true;StateChanged?.Invoke(this,EventArgs.Empty);}}
 }
 async Task RefreshAsync()
 {
  if(editor.IsEditing&&MessageBox.Show(this,"Discard the working copy?","Parameter Sets",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
  if(editor.IsEditing)editor.Clear();
  var discovery=await api.ComponentsAsync(lifetime.Token);
  if(!discovery.Success)throw new InvalidOperationException(discovery.ErrorMessage);
  catalogue=discovery.Value??[];
  bindingCatalogue=true;
  try{areas.Items.Clear();areas.Items.AddRange(catalogue.DistinctBy(x=>x.AreaCode).Select(x=>new AreaChoice(x.AreaCode,x.AreaName)).Cast<object>().ToArray());if(areas.Items.Count>0)areas.SelectedIndex=0;BindComponents();}
  finally{bindingCatalogue=false;}
  await LoadVersionsAsync();
 }
 sealed record AreaChoice(string Code,string Name);
 void BindComponents()
 {
  var wasBinding=bindingCatalogue;bindingCatalogue=true;
  try{components.Items.Clear();childTitle.Text=areas.SelectedItem is AreaChoice area?area.Name:string.Empty;if(areas.SelectedItem is AreaChoice selectedArea)components.Items.AddRange(catalogue.Where(x=>x.AreaCode==selectedArea.Code).Cast<object>().ToArray());if(components.Items.Count>0)components.SelectedIndex=0;else detailTitle.Text=string.Empty;}
  finally{bindingCatalogue=wasBinding;}
 }
 async Task LoadVersionsAsync()
 {
  if(components.SelectedItem is not ParameterComponentSummary component)return;
  if(editor.IsEditing){status.Text="Save or discard the working copy before changing components.";return;}
  var result=await api.VersionsAsync(token:lifetime.Token,componentCode:component.ComponentCode);if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
  loaded=result.Value??[];
  if(editor.SetId!=Guid.Empty&&editor.Selected?.Reference.ComponentCode==component.ComponentCode)
  {
   var exact=await api.StateAsync(editor.SetId,lifetime.Token);
   if(!exact.Success||exact.Value is null)throw new InvalidOperationException(exact.ErrorMessage);
   loaded=loaded.Where(x=>x.Reference.SetId!=editor.SetId).Concat(exact.Value.Versions).ToArray();
  }
  editor.Clear();Bind();
  versions.Items.Clear();versions.Items.AddRange(loaded.Select(x=>new VersionChoice(x)).Cast<object>().ToArray());
  if(versions.Items.Count>0)versions.SelectedIndex=0;else status.Text=string.Empty;
 }
 async Task NewAsync()
 {
  if(components.SelectedItem is ParameterComponentSummary component&&!component.CanEdit)throw new InvalidOperationException("No editor is registered for this component.");
  if(editor.IsEditing&&MessageBox.Show(this,"Discard the working copy?","Parameter Sets",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
  var result=await api.PreviewAsync(Guid.NewGuid(),lifetime.Token,(int)(TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType)horizon.SelectedItem!);if(!result.Success||result.Value is null)throw new InvalidOperationException(result.ErrorMessage);
  versions.ClearSelected();editor.New(result.Value);Bind();
 }
 async Task LegacyAsync()
 {
  using var dialog=new DarkTradingForm{Text="Legacy Regime Discovery versions (read only)",Width=1100,Height=680,StartPosition=FormStartPosition.CenterParent};
  var table=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  var payload=new TextBox{Dock=DockStyle.Bottom,Height=180,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both};
  var toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=36};var previous=new Button{Text="Previous"};var next=new Button{Text="Next"};var migrate=new Button{Text="Create exact draft",Width=150};
  toolbar.Controls.AddRange([previous,next,migrate]);dialog.Controls.Add(table);dialog.Controls.Add(payload);dialog.Controls.Add(toolbar);
  var offset=0;ParameterLegacyVersion[] rows=[];
  async Task LoadPage()
  {
   var result=await api.LegacyVersionsAsync(offset,lifetime.Token);if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
   rows=result.Value??[];table.DataSource=rows.Select(x=>new{x.Reference.Kind,x.Reference.SetId,x.Reference.Version,x.SchemaVersion,x.Status,x.Reference.PayloadSha256,x.Reference.Codec}).ToArray();
   previous.Enabled=offset>0;next.Enabled=rows.Length==100;
  }
  table.SelectionChanged+=(_,__)=>{var index=table.CurrentRow?.Index??-1;payload.Text=index>=0&&index<rows.Length?rows[index].PayloadJson:string.Empty;};
  async Task Page(int delta){try{offset=Math.Max(0,offset+delta);await LoadPage();}catch(Exception error){MessageBox.Show(dialog,error.Message,"Legacy inventory");}}
  previous.Click+=async (_,__)=>await Page(-100);next.Click+=async (_,__)=>await Page(100);
  migrate.Click+=async (_,__)=>
  {
   try
   {
    migrate.Enabled=false;var index=table.CurrentRow?.Index??-1;if(index<0||index>=rows.Length)return;
    var row=rows[index];var preview=await api.PreviewLegacyMigrationAsync(row.Reference.SetId,row.Reference.Version,lifetime.Token);
    if(!preview.Success||preview.Value is null)throw new InvalidOperationException(preview.ErrorMessage);
    var result=await api.CreateAsync(preview.Value,lifetime.Token);if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
    MessageBox.Show(dialog,"Exact draft created. The legacy version is preserved. Review, publish and assign the draft separately; activation is at next startup.","Migration draft");
   }
   catch(Exception error){MessageBox.Show(dialog,error.Message,"Migration draft",MessageBoxButtons.OK,MessageBoxIcon.Information);}
   finally{migrate.Enabled=true;}
  };
  await LoadPage();dialog.ShowDialog(this);
 }
 async Task ActiveGenerationsAsync()
 {
  var result=await api.StartupRunsAsync(lifetime.Token);
  if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
  using var dialog=new DarkTradingForm{Text="Recent parameter startup snapshots",Width=1000,Height=600,StartPosition=FormStartPosition.CenterParent};
  var runs=result.Value??[];
  var table=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
   DataSource=runs.Select(run=>new{run.RunId,run.CreatedAtUtc,run.CreatedBy,Assignments=run.Scopes.Length,run.Plan.Fingerprint}).ToArray()};
  table.CellDoubleClick+=async (_,args)=>
  {
   if(args.RowIndex<0||args.RowIndex>=runs.Length)return;
   try
   {
    var evidence=await api.StartupReportAsync(runs[args.RowIndex].RunId,lifetime.Token);
    if(!evidence.Success||evidence.Value is null)throw new InvalidOperationException(evidence.ErrorMessage);
    using var details=new Form{Text="Startup preparation evidence",Width=1000,Height=650,StartPosition=FormStartPosition.CenterParent};
    details.Controls.Add(new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
     DataSource=evidence.Value.Outcomes.Select(x=>new{x.Key.Producer,x.Key.Interval,x.Key.Period,x.Status,x.Detail}).ToArray()});
    details.Controls.Add(new Label{Dock=DockStyle.Bottom,Height=45,Text=$"Recorded {evidence.Value.RecordedAtUtc:u}; value date {evidence.Value.ValueDate}; contract {evidence.Value.ContractId}. Preparation evidence does not imply warm or fresh signals."});
    var monitor=new Button{Text="Refresh monitored signals",Dock=DockStyle.Top,Height=32};
    monitor.Click+=async (_,__)=>
    {
     try
     {
      monitor.Enabled=false;
      var current=await api.SignalMonitoringAsync(runs[args.RowIndex].RunId,lifetime.Token);
      if(!current.Success||current.Value is null)throw new InvalidOperationException(current.ErrorMessage);
      using var availability=new Form{Text="Monitored signal availability",Width=1150,Height=650,StartPosition=FormStartPosition.CenterParent};
      availability.Controls.Add(new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
       DataSource=current.Value.Rows.Select(x=>new{x.AssignmentId,x.AssignmentRevision,x.Observation.Metric,Interval=x.Observation.SignalKey.TimeFrame,x.IsRequired,x.MaximumAgeSeconds,x.Observation.Availability,x.Observation.IsWarm,x.Observation.MarketDataAsOfUtc}).ToArray()});
      availability.Controls.Add(new Label{Dock=DockStyle.Bottom,Height=45,Text=$"Captured {current.Value.CapturedAtUtc:u} for {current.Value.ContractId}. Only included rows with Monitor enabled are shown. This does not gate workflow admission."});
      availability.ShowDialog(details);
     }
     catch(Exception error){MessageBox.Show(details,error.Message,"Signal availability",MessageBoxButtons.OK,MessageBoxIcon.Information);}
     finally{monitor.Enabled=true;}
    };
    details.Controls.Add(monitor);
    details.ShowDialog(dialog);
   }
   catch(Exception error){MessageBox.Show(dialog,error.Message,"Startup evidence",MessageBoxButtons.OK,MessageBoxIcon.Information);}
  };
  dialog.Controls.Add(table);
  dialog.Controls.Add(new Label{Dock=DockStyle.Bottom,Height=48,Text="Double-click a startup for preparation evidence. Latest 100 snapshots for traceability. Assignments persist until changed or disabled. No shutdown or manual release is required; running workflows retain their captured version."});dialog.ShowDialog(this);
 }
 async Task StartupPreviewAsync()
 {
  var result=await api.PreviewStartupAsync(Guid.NewGuid(),lifetime.Token);
  if(!result.Success||result.Value is null)throw new InvalidOperationException(result.ErrorMessage);
  using var dialog=new DarkTradingForm{Text="Pending startup plan (preview only)",Width=1000,Height=650,StartPosition=FormStartPosition.CenterParent};
  var table=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
   DataSource=result.Value.Steps.Select(x=>new{Producer=x.Key.Producer,Interval=x.Key.Interval,x.Key.Period,x.Prepare,x.Monitor,Consumers=string.Join(", ",x.Consumers.Select(c=>c.Consumer).Distinct())}).ToArray()};
  dialog.Controls.Add(table);dialog.Controls.Add(new Label{Dock=DockStyle.Bottom,Height=50,Text="Fingerprint: "+result.Value.Fingerprint+". Preview does not activate producers or change the current startup. "+string.Join("; ",result.Value.Issues??[])});dialog.ShowDialog(this);
 }
 async Task PayloadAsync()
 {
  var payload=Capture();
  using var dialog=new DarkTradingForm{Text="Exact parameter payload",Width=850,Height=700,StartPosition=FormStartPosition.CenterParent};
  dialog.Controls.Add(new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Text=payload,AccessibleName="Exact parameter payload"});
  dialog.ShowDialog(this);await Task.CompletedTask;
 }
 async Task SchemaAsync()
 {
  var schemaVersion=editor.Selected?.SchemaVersion??editor.Parameters?.SchemaVersion??throw new InvalidOperationException("Select a parameter set first.");
  var result=await api.SchemaAsync(schemaVersion,lifetime.Token);
  if(!result.Success||result.Value is null)throw new InvalidOperationException(result.ErrorMessage);
  using var dialog=new DarkTradingForm{Text=$"Parameter schema {result.Value.Version} ({result.Value.Codec})",Width=850,Height=700,StartPosition=FormStartPosition.CenterParent};
  using var document=JsonDocument.Parse(result.Value.JsonSchema);
  dialog.Controls.Add(new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Text=JsonSerializer.Serialize(document.RootElement,new JsonSerializerOptions{WriteIndented=true}),AccessibleName="Registered parameter schema"});
  dialog.ShowDialog(this);
 }
 async Task IntervalsAsync()
 {
  if(!editor.IsEditing||editor.Parameters is null)throw new InvalidOperationException("Open a working copy first.");
  Capture();
  using var dialog=new DarkTradingForm{Text=$"{editor.Parameters.TargetHorizon} signal intervals",Width=650,Height=430,StartPosition=FormStartPosition.CenterParent};
  var intervals=new BindingList<IntervalRow>(editor.Parameters.Horizon.TimeFrames.Select(x=>new IntervalRow{TimeFrame=x.TimeFrame,Weight=x.Weight,MaximumAgeSeconds=x.MaximumAgeSeconds}).ToList());
  var table=new DataGridView{Dock=DockStyle.Fill,AutoGenerateColumns=false,DataSource=intervals,AllowUserToAddRows=true,AllowUserToDeleteRows=true,AccessibleName="Included signal intervals"};
  table.Columns.Add(new DataGridViewComboBoxColumn{DataPropertyName="TimeFrame",HeaderText="Interval",Width=180,DataSource=new[]{TimeFrameType.FifteenSeconds,TimeFrameType.OneMinute,TimeFrameType.FiveMinutes,TimeFrameType.FifteenMinutes,TimeFrameType.OneHour,TimeFrameType.FourHours,TimeFrameType.Daily}});
  table.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="Weight",HeaderText="Relative weight",Width=150});
  table.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="MaximumAgeSeconds",HeaderText="Max age (seconds)",Width=170});
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=42};
  var apply=new Button{Text="Apply",DialogResult=DialogResult.OK};var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel};buttons.Controls.Add(apply);buttons.Controls.Add(cancel);
  var help=new Label{Dock=DockStyle.Top,Height=55,Text="Every listed interval is included. Applying regenerates its signal requirements and calculation dependencies. Review the resulting signal list before saving."};
  dialog.Controls.Add(table);dialog.Controls.Add(help);dialog.Controls.Add(buttons);dialog.AcceptButton=apply;dialog.CancelButton=cancel;
  dialog.FormClosing+=(_,e)=>{if(dialog.DialogResult==DialogResult.OK&&!table.EndEdit()){e.Cancel=true;}};
  table.DataError+=(_,e)=>{e.ThrowException=false;dialog.DialogResult=DialogResult.None;};
  if(dialog.ShowDialog(this)!=DialogResult.OK)return;
  var source=editor.Parameters with {Horizon=editor.Parameters.Horizon with {TimeFrames=intervals.Select(x=>new RegimeDiscoveryTimeFrameConfiguration{TimeFrame=x.TimeFrame,Weight=x.Weight,MaximumAgeSeconds=x.MaximumAgeSeconds,IsRequired=true}).ToArray()}};
  var preview=await api.PreviewAsync(editor.SetId,lifetime.Token,(int)source.TargetHorizon,JsonSerializer.Serialize(source),true);
  if(!preview.Success||preview.Value is null)throw new InvalidOperationException(preview.ErrorMessage);
  editor.BeginEdit(preview.Value);Bind();
  status.Text="Intervals applied to the working copy. Review included signals and validate before publication.";
 }
 async Task EditAsync()
 {
  if(editor.Parameters is null)throw new InvalidOperationException("Select a version first.");
  if(editor.IsEditing)throw new InvalidOperationException("The working copy is already open.");
  var preview=await api.PreviewAsync(editor.SetId,lifetime.Token,(int)editor.Parameters.TargetHorizon,editor.Payload());
  if(!preview.Success||preview.Value is null)throw new InvalidOperationException(preview.ErrorMessage);
  editor.BeginEdit(preview.Value);Bind();
  status.Text="Working copy uses explicit membership. Review intervals and newly included calculation dependencies before publication.";
 }
 void Bind()
 {
  areas.Enabled=components.Enabled=versions.Enabled=horizon.Enabled=!editor.IsEditing;
  name.Text=editor.Name;name.ReadOnly=!editor.IsEditing;
  var parameters=editor.Parameters;
  var signalValues=parameters?.SignalMetrics??(parameters is null?[]:
   RegimeDiscoveryMetricConfigurationProjection.FromLegacySignals(parameters.ParameterSetId,parameters.SignalRequirements??[]));
  var observationValues=parameters?.ObservationMetrics??(parameters is null?[]:
   RegimeDiscoveryMetricConfigurationProjection.FromLegacyObservations(parameters.ParameterSetId,parameters.SignalRequirements??[]));
  rows=new BindingList<SignalRow>(signalValues.Select(x=>new SignalRow(x)).ToList());
  observationRows=new BindingList<ObservationRow>(observationValues.Select(x=>new ObservationRow(x)).ToList());
  grid.DataSource=rows;grid.ReadOnly=!editor.IsEditing;
  observationGrid.DataSource=observationRows;observationGrid.ReadOnly=!editor.IsEditing;
  grid.AllowUserToAddRows=grid.AllowUserToDeleteRows=editor.IsEditing;
  observationGrid.AllowUserToAddRows=observationGrid.AllowUserToDeleteRows=editor.IsEditing;
  fields=new BindingList<FieldRow>(editor.Parameters is null?[]:ParameterFieldEditorModel.Read(editor.Payload()).Select(x=>new FieldRow(x)).ToList());
  BindParameterGroups();parametersGrid.ReadOnly=!editor.IsEditing;
  detailTitle.Text=components.SelectedItem is ParameterComponentSummary component?component.Name:string.Empty;
  if(!editor.CanEdit){status.Text="Unsupported schema or fields. Exact payload is available for inspection; editing is disabled to preserve its contents.";return;}
  status.Text=$"{rows.Count} signals; {rows.Count(x=>x.Enabled)} included. {observationRows.Count} observations; {observationRows.Count(x=>x.Enabled)} included. "+(editor.IsEditing?"Working copy; Save commits a new version.":"Saved version is read-only.");
  StateChanged?.Invoke(this,EventArgs.Empty);
 }
 void BindParameterGroups()
 {
  var selected=parameterGroups.SelectedItem as string;
  bindingParameterGroups=true;
  try
  {
   parameterGroups.Items.Clear();
   parameterGroups.Items.AddRange(ParameterFieldEditorModel.Groups(fields.Select(row=>row.Field())).Cast<object>().ToArray());
   if(selected is not null&&parameterGroups.Items.Contains(selected))parameterGroups.SelectedItem=selected;
   else if(parameterGroups.Items.Count>0)parameterGroups.SelectedIndex=0;
  }
  finally{bindingParameterGroups=false;}
  BindSelectedParameterGroup();
 }
 void BindSelectedParameterGroup()
 {
  if(bindingParameterGroups)return;
  parametersGrid.EndEdit();
  if(parametersGrid.DataSource is not null)BindingContext[parametersGrid.DataSource]?.EndCurrentEdit();
  var selected=parameterGroups.SelectedItem as string;
  parameterGroupTitle.Text=selected??string.Empty;
  parametersGrid.DataSource=new BindingList<FieldRow>(selected is null?[]:fields.Where(row=>row.Group==selected).ToList());
 } string Capture()
 {
  grid.EndEdit();BindingContext[rows]?.EndCurrentEdit();observationGrid.EndEdit();BindingContext[observationRows]?.EndCurrentEdit();parametersGrid.EndEdit();if(parametersGrid.DataSource is not null)BindingContext[parametersGrid.DataSource]?.EndCurrentEdit();
  if(editor.IsEditing){editor.SetFields(fields.Select(x=>x.Field()));editor.Name=name.Text;editor.SetMetrics(rows.Select(x=>x.Value()).ToArray(),observationRows.Select(x=>x.Value()).ToArray());}
  return editor.Payload();
 }
 async Task ValidateAsync()
 {
  var result=await api.ValidateAsync(Capture(),editor.Parameters!.SchemaVersion,lifetime.Token);
  if(!result.Success||result.Value is null)throw new InvalidOperationException(result.ErrorMessage);
  validationGrid.DataSource=result.Value.Issues;detailTabs.SelectedTab=validationTab;
  status.Text=result.Value.IsValid?"Configuration is valid. Live availability is checked separately.":$"{result.Value.Issues.Length} validation issues. See the Validation tab.";
 }
 async Task SaveAsync()
 {
  if(!editor.IsEditing)throw new InvalidOperationException("Choose Edit as new draft first.");
  var json=Capture();var id=Guid.NewGuid();
  var result=editor.Selected is null
   ?await api.CreateAsync(new(){CommandId=id,EntityId=new(editor.SetId),Name=editor.Name,Description=editor.Description,PayloadJson=json,SchemaVersion=editor.Parameters!.SchemaVersion},lifetime.Token)
   :await api.SaveAsync(new(){CommandId=id,EntityId=new(editor.SetId),ExpectedRevision=editor.ExpectedRevision,Name=editor.Name,Description=editor.Description,PayloadJson=json,SchemaVersion=editor.Parameters!.SchemaVersion},lifetime.Token);
  if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
  editor.AcknowledgeSave();Bind();
  await LoadCommittedAsync(editor.SetId,id);
  status.Text="Draft saved and read from committed state. Publication and assignment are separate.";
 }
 async Task PublishAsync()
 {
  if(editor.IsEditing||editor.Selected is not {Status:ParameterVersionStatus.Draft} selected)throw new InvalidOperationException("Select a saved draft first.");
  var operationId=Guid.NewGuid();
  var result=await api.PublishAsync(new(){CommandId=operationId,EntityId=new(selected.Reference.SetId),Version=selected.Reference.Version,ExpectedRevision=editor.ExpectedRevision},lifetime.Token);
  if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
  await LoadCommittedAsync(selected.Reference.SetId,operationId);
  status.Text="Publication committed. This does not change the running assignment.";
 }
 async Task OperationsAsync()
 {
  if(editor.SetId==Guid.Empty)throw new InvalidOperationException("Select a parameter set first.");
  var result=await api.StateAsync(editor.SetId,lifetime.Token);
  if(!result.Success||result.Value is null)throw new InvalidOperationException(result.ErrorMessage);
  using var dialog=new DarkTradingForm{Text="Committed parameter operations",Width=1050,Height=500,StartPosition=FormStartPosition.CenterParent};
  var table=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AccessibleName="Committed parameter operations"};
  table.DataSource=result.Value.Audit is {Length:>0} audit
   ?(object)audit.OrderByDescending(x=>x.Revision).ToArray()
   :result.Value.Operations.OrderByDescending(x=>x.Revision).Select(x=>new{x.Revision,x.OperationId,Version=x.Reference.Version,x.RequestSha256,x.Reference.PayloadSha256}).ToArray();dialog.Controls.Add(table);dialog.ShowDialog(this);
 }
 async Task CompareAsync()
 {
  if(editor.Selected is not {} selected)throw new InvalidOperationException("Select a saved version first.");
  var result=await api.StateAsync(selected.Reference.SetId,lifetime.Token);
  if(!result.Success||result.Value is null)throw new InvalidOperationException(result.ErrorMessage);
  using var dialog=new DarkTradingForm{Text="Compare parameter versions",Width=1000,Height=600,StartPosition=FormStartPosition.CenterParent};
  var candidates=new ComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Compare against version"};
  var differences=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AccessibleName="Parameter differences"};
  candidates.Items.AddRange(result.Value.Versions.Select(x=>new VersionChoice(x)).Cast<object>().ToArray());
  candidates.SelectedIndexChanged+=(_,_)=>{if(candidates.SelectedItem is VersionChoice choice)differences.DataSource=ParameterComparisonModel.Compare(choice.Value.PayloadJson,selected.PayloadJson);};
  var help=new Label{Dock=DockStyle.Bottom,Height=35,Text=$"Before: selected comparison version. After: {selected.Name}, version {selected.Reference.Version}."};
  dialog.Controls.Add(differences);dialog.Controls.Add(candidates);dialog.Controls.Add(help);if(candidates.Items.Count>0)candidates.SelectedIndex=0;dialog.ShowDialog(this);
 }
 async Task RetireAsync()
 {
  if(editor.Parameters is null||editor.IsEditing||editor.Selected is not {Status:ParameterVersionStatus.Published} selected)throw new InvalidOperationException("Select a published version to retire.");
  if(MessageBox.Show(this,$"Retire {selected.Name}, version {selected.Reference.Version}?","Parameter Sets",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
  var id=Guid.NewGuid();var result=await api.RetireAsync(new(){CommandId=id,EntityId=new(selected.Reference.SetId),Version=selected.Reference.Version,ExpectedRevision=editor.ExpectedRevision},lifetime.Token);
  if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
  await LoadCommittedAsync(selected.Reference.SetId,id);status.Text="Version retired. Its saved payload is retained.";
 }
 async Task AssignmentAsync()
 {
  if(editor.Parameters is null||editor.IsEditing||editor.Selected is not {Status:ParameterVersionStatus.Published} selected)throw new InvalidOperationException("Select a published version to configure an assignment.");
  const string workflow=TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity.IntrinsicTimeStrategyWorkflowDefinition.Id;
  var query=await api.AssignmentAsync(workflow,(int)editor.Parameters!.TargetHorizon,lifetime.Token);
  if(!query.Success||query.Value is null)throw new InvalidOperationException(query.ErrorMessage);
  var current=query.Value;
  using var dialog=new DarkTradingForm{Text="Workflow parameter assignment",Width=590,Height=220,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false};
  var information=new Label{Dock=DockStyle.Fill,Padding=new Padding(12),Text=$"Workflow: {workflow}\nHorizon: {editor.Parameters.TargetHorizon}\nSelected: {selected.Name}, version {selected.Reference.Version}\nConfigured: {(current.Assignment is {Enabled:true} a?$"{a.Reference.SetId}, version {a.Reference.Version}":"None")}\nAssignment changes take effect at the next application startup."};
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=45};
  var assign=new Button{Text="Assign selected",AutoSize=true,DialogResult=DialogResult.Yes};var disable=new Button{Text="Disable",Enabled=current.Assignment?.Enabled==true,DialogResult=DialogResult.No};var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel};
  buttons.Controls.Add(assign);buttons.Controls.Add(disable);buttons.Controls.Add(cancel);dialog.Controls.Add(information);dialog.Controls.Add(buttons);dialog.CancelButton=cancel;
  var answer=dialog.ShowDialog(this);if(answer is not (DialogResult.Yes or DialogResult.No))return;
  var result=answer==DialogResult.Yes
   ?await api.AssignAsync(new(){CommandId=Guid.NewGuid(),EntityId=current.EntityId,Scope=current.Scope,Reference=selected.Reference,ExpectedRevision=current.Revision},lifetime.Token)
   :await api.DisableAssignmentAsync(new(){CommandId=Guid.NewGuid(),EntityId=current.EntityId,Scope=current.Scope,Reference=current.Assignment!.Reference,ExpectedRevision=current.Revision},lifetime.Token);
  if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
  var verified=await api.AssignmentAsync(workflow,(int)editor.Parameters.TargetHorizon,lifetime.Token);
  if(!verified.Success||verified.Value is null)throw new InvalidOperationException("Assignment committed; refresh its outcome before changing it again.");
  status.Text=$"Assignment revision {verified.Value.Revision} recorded. Pending until the next application startup.";
 }
 async Task RenameAsync()
 {
  if(editor.IsEditing||editor.Selected is not {} selected)throw new InvalidOperationException("Select a saved version before renaming the set.");
  using var dialog=new DarkTradingForm{Text="Parameter set metadata",Width=460,Height=250,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false};
  var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new Padding(12)};
  var title=new TextBox{Text=editor.Name,Dock=DockStyle.Fill,MaxLength=200,AccessibleName="Parameter set name"};
  var description=new TextBox{Text=editor.Description,Dock=DockStyle.Fill,Multiline=true,MaxLength=4000,AccessibleName="Parameter set description"};
  fields.ColumnStyles.Add(new(SizeType.Absolute,90));fields.ColumnStyles.Add(new(SizeType.Percent,100));
  fields.RowStyles.Add(new(SizeType.Absolute,32));fields.RowStyles.Add(new(SizeType.Percent,100));fields.RowStyles.Add(new(SizeType.Absolute,38));
  fields.Controls.Add(new Label{Text="Name"},0,0);fields.Controls.Add(title,1,0);
  fields.Controls.Add(new Label{Text="Description"},0,1);fields.Controls.Add(description,1,1);
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill};var save=new Button{Text="Save",DialogResult=DialogResult.OK};var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel};
  buttons.Controls.Add(save);buttons.Controls.Add(cancel);fields.Controls.Add(buttons,1,2);dialog.Controls.Add(fields);dialog.AcceptButton=save;dialog.CancelButton=cancel;
  if(dialog.ShowDialog(this)!=DialogResult.OK)return;
  var operationId=Guid.NewGuid();
  var result=await api.RenameAsync(new(){CommandId=operationId,EntityId=new(selected.Reference.SetId),Version=selected.Reference.Version,ExpectedRevision=editor.ExpectedRevision,Name=title.Text,Description=description.Text},lifetime.Token);
  if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
  await LoadCommittedAsync(selected.Reference.SetId,operationId);status.Text="Set metadata updated; version payloads are unchanged.";
 }
 async Task LoadCommittedAsync(Guid setId,Guid operationId)
 {
  var result=await api.StateAsync(setId,lifetime.Token);
  if(!result.Success||result.Value is null)throw new InvalidOperationException("Commit acknowledged; authoritative refresh failed. Refresh before making another change. "+result.ErrorMessage);
  var receipt=result.Value.Operations.SingleOrDefault(x=>x.OperationId==operationId)
   ??throw new InvalidOperationException("The operation outcome is not yet visible. Refresh before making another change.");
  loaded=loaded.Where(x=>x.Reference.SetId!=setId).Concat(result.Value.Versions).ToArray();
  versions.Items.Clear();versions.Items.AddRange(loaded.Select(x=>new VersionChoice(x)).Cast<object>().ToArray());
  versions.SelectedItem=versions.Items.Cast<VersionChoice>().Single(x=>x.Value.Reference==receipt.Reference);
 }
 protected override void Dispose(bool disposing){if(disposing){if(!lifetime.IsCancellationRequested)lifetime.Cancel();lifetime.Dispose();}base.Dispose(disposing);}
 enum EditMode{View,Add,Change}
 sealed record VersionChoice(ParameterSetVersion Value){public override string ToString()=>$"{Value.Name} - v{Value.Reference.Version} ({Value.Status})";}
 public sealed class IntervalRow
 {
  public TimeFrameType TimeFrame{get;set;}=TimeFrameType.OneMinute;
  public decimal Weight{get;set;}=1m;
  public int MaximumAgeSeconds{get;set;}=180;
 }
 public sealed class FieldRow(ParameterField descriptor)
 {
  public string Path=>descriptor.Path;
  public string Group=>descriptor.Group;
  public string Name=>descriptor.Name;
  public string Value{get;set;}=descriptor.Value;
  public ParameterField Field()=>descriptor with {Value=Value};
 }
 public sealed class SignalRow(RegimeDiscoverySignalMetricConfiguration original)
 {
  public bool Enabled{get;set;}=original.Enabled;
  public SignalMetricsType Metric{get;set;}=original.Metric;
  public TimeFrameType TimeFrame{get;set;}=original.TimeFrame;
  public int PeriodLength{get;set;}=original.PeriodLength;
  public int MaximumAgeSeconds{get;set;}=original.MaximumAgeSeconds;
  public bool Prepare{get;set;}=original.PrepareAtStartup;
  public bool Monitor{get;set;}=original.Monitor;
  public RegimeDiscoverySignalMetricConfiguration Value()
  {
   if(Metric==SignalMetricsType.Unknown)throw new ArgumentException("Select a signal metric.");
   if(TimeFrame==TimeFrameType.None)throw new ArgumentException("Select a signal timeframe.");
   if(PeriodLength<=0)throw new ArgumentException("Period length must be a positive whole number.");
   if(MaximumAgeSeconds<=0)throw new ArgumentException("Maximum age must be a positive whole number.");
   return original with {Enabled=Enabled,Metric=Metric,TimeFrame=TimeFrame,PeriodLength=PeriodLength,
    MaximumAgeSeconds=MaximumAgeSeconds,PrepareAtStartup=Prepare,Monitor=Monitor};
  }
 }
 public sealed class ObservationRow(RegimeDiscoveryObservationMetricConfiguration original)
 {
  public bool Enabled{get;set;}=original.Enabled;
  public ObservationMetricsType Metric{get;set;}=original.Metric;
  public int MaximumAgeSeconds{get;set;}=original.MaximumAgeSeconds;
  public bool Prepare{get;set;}=original.PrepareAtStartup;
  public bool Monitor{get;set;}=original.Monitor;
  public RegimeDiscoveryObservationMetricConfiguration Value()
  {
   if(Metric==ObservationMetricsType.Unknown)throw new ArgumentException("Select an observation metric.");
   if(MaximumAgeSeconds<=0)throw new ArgumentException("Maximum age must be a positive whole number.");
   return original with {Enabled=Enabled,Metric=Metric,MaximumAgeSeconds=MaximumAgeSeconds,
    PrepareAtStartup=Prepare,Monitor=Monitor};
  }
 }
}




