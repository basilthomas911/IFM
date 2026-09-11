using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Views.Strategy;

/// <summary>Reusable read-only workflow details accordion.</summary>
public sealed class StrategyWorkflowDetailsAccordion : UserControl
{
    readonly Label _header = new() { AutoSize = true, ForeColor = Color.White, Font = new("Consolas", 9F), Padding = new(8) };
    readonly FlowLayoutPanel _content = new BufferedFlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Black };
    string? _expandedKey;
    Guid? _workflowId;
    long? _workflowRevision;
    string? _message;

    public StrategyWorkflowDetailsAccordion()
    {
        BackColor = Color.Black;
        Dock = DockStyle.Fill;
        _content.Controls.Add(_header);
        Controls.Add(_content);
        Resize += (_, _) => ResizeRows();
        ShowMessage("Select a strategy workflow to inspect its pipeline results.");
    }

    public void Bind(StrategyWorkflowDetails? details)
    {
        if (details is null) { ShowMessage("Select a strategy workflow to inspect its pipeline results."); return; }
        if (_workflowId == details.WorkflowId.Value && _workflowRevision == details.WorkflowRevision && Tag is not null)
            return;
        var sameWorkflow = _workflowId == details.WorkflowId.Value;
        var scroll = sameWorkflow ? _content.AutoScrollPosition : Point.Empty;
        _workflowId = details.WorkflowId.Value;
        _workflowRevision = details.WorkflowRevision;
        _message = null;
        if (!sameWorkflow) _expandedKey = "iti";
        Render(details, scroll, sameWorkflow);
    }

    void Render(StrategyWorkflowDetails details, Point scroll, bool restoreScroll)
    {
        _content.SuspendLayout();
        _content.Controls.Clear();
        _header.Text = details.Header;
        _header.AccessibleName = "Workflow summary";
        _content.Controls.Add(_header);
        foreach (var section in details.Sections) AddSection(section);
        ResizeRows();
        _content.ResumeLayout();
        if (restoreScroll) _content.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
        Tag = details;
    }

    public void ShowMessage(string message)
    {
        if (_workflowId is null && Tag is null && string.Equals(_message, message, StringComparison.Ordinal))
            return;
        _workflowId = null;
        _workflowRevision = null;
        _expandedKey = null;
        _message = message;
        Tag = null;
        _content.Controls.Clear();
        _header.Text = message;
        _content.Controls.Add(_header);
    }

    void AddSection(StrategyWorkflowDetailSection section)
    {
        var expanded = section.Key == _expandedKey;
        var button = new Button
        {
            AutoSize = false, Height = 38, TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat, ForeColor = StateColor(section.State), BackColor = Color.FromArgb(35, 35, 38),
            Text = $"{(expanded ? "v" : ">")} {section.Title} - {section.Summary}",
            Tag = section.Key, AccessibleName = section.AccessibleStatus,
            AccessibleDescription = expanded ? "Expanded. Activate to collapse." : "Collapsed. Activate to expand."
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 74);
        button.Click += (_, _) => { _expandedKey = expanded ? null : section.Key; RebindExpansion(); };
        _content.Controls.Add(button);
        if (!expanded) return;
        _content.Controls.Add(new TextBox
        {
            Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both,
            BorderStyle = BorderStyle.FixedSingle, BackColor = Color.Black, ForeColor = Color.White,
            Font = new("Consolas", 9F), Height = Math.Clamp(section.Content.Count(c => c == '\n') * 18 + 24, 120, 420),
            Text = section.Content
        });
    }

    void RebindExpansion()
    {
        if (Tag is not StrategyWorkflowDetails model)
            return;
        var scroll = _content.AutoScrollPosition;
        Render(model, scroll, true);
    }

    void ResizeRows()
    {
        var width = Math.Max(100, _content.ClientSize.Width - (_content.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0) - 8);
        foreach (Control control in _content.Controls) control.Width = width;
    }

    static Color StateColor(StrategyWorkflowDetailState state) => state switch
    {
        StrategyWorkflowDetailState.Failed => Color.Salmon,
        StrategyWorkflowDetailState.Processing => Color.Gold,
        StrategyWorkflowDetailState.Completed => Color.LightGreen,
        StrategyWorkflowDetailState.Stopped => Color.Khaki,
        _ => Color.Gainsboro
    };

    sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
    {
        public BufferedFlowLayoutPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
