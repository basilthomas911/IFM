using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.Views.Reference;

/// <summary>Structured authoring for catalog metadata, topology, variant traits and parameter values.</summary>
public sealed class StrategyCatalogDefinitionEditor : DarkTradingView
{
    readonly StrategyCatalogDefinition source;
    readonly Dictionary<string, CatalogKey> choices;
    readonly TextBox code = Text("Catalog code"), name = Text("Catalog name"), description = Text("Description");
    readonly ComboBox parent = Combo("Related definition"), horizon = Combo("Timeframe"), side = Combo("Structure side", "Long", "Short", "Custom"), bias = Combo("Directional bias", "Balanced", "Bullish", "Bearish", "Custom"), premium = Combo("Premium mode", "None", "Credit", "Debit", "Custom");
    readonly NumericUpDown delta = Number("Target net delta", -10000, 10000), tolerance = Number("Balance tolerance", 0, 10000), minimumWing = Number("Minimum wing width", 0, 1000000), maximumWing = Number("Maximum wing width", 0, 1000000);
    readonly CheckBox symmetric = new() { Text = "Equal wing widths", AccessibleName = "Equal wing widths", AutoSize = true };
    readonly Dictionary<string, DataGridView> grids = [];
    readonly DataGridView settings = Grid("Additional parameters", "Parameter path", "Type", "Value");
    readonly DataGridView schema = Grid("Parameter schema", "Parameter", "Type", "Required", "Unit", "Minimum", "Maximum", "Min length", "Max length", "Choices");
    readonly Dictionary<string, CatalogProduct> products = [];
    readonly Font editFont = new(DarkTradingTheme.FontFamily, DarkTradingTheme.FontSize, FontStyle.Bold);
    public bool Editable { get; }

    public StrategyCatalogDefinitionEditor(StrategyCatalogDefinition definition, bool editable, IReadOnlyList<StrategyCatalogSummary> references, IReadOnlyList<CatalogProduct>? availableProducts = null)
    {
        foreach (var product in (availableProducts ?? []).Concat(definition.Products).Distinct())
            products.TryAdd($"{product.Symbol} - {product.Exchange} - {product.Currency} [{product.ProductId}]", product);
        source = StrategyCatalogJson.Read<StrategyCatalogDefinition>(StrategyCatalogJson.Write(definition)); Editable = editable;
        Name = "StrategyCatalogDefinitionEditor"; AccessibleName = "Strategy catalog definition details"; Dock = DockStyle.Fill;
        choices = references.GroupBy(x => x.Key).Select(x => x.First()).ToDictionary(x => $"{x.Name} [{x.Code}, v{x.Key.Version}, {x.Status}]", x => x.Key, StringComparer.Ordinal);
        foreach (var key in definition.Families.Concat(definition.Structures).Concat(definition.Variants).Concat(definition.Parameters.Select(x => x.ParameterSet)).Concat(definition.Parent is null ? [] : new[] { definition.Parent }))
            if (!choices.ContainsValue(key)) choices.Add($"Exact {key.Kind} {key.Id} v{key.Version}", key);
        var tabs = new TomasAI.IFM.UI.Net.Views.App.DarkTabControl { Dock = DockStyle.Fill, AccessibleName = "Catalog detail sections" };
        var basics = FormTable();
        code.Text = definition.Code; name.Text = definition.Name; description.Text = definition.Description; description.Multiline = true;
        code.ReadOnly = !editable || definition.Key.Version > 1;
        Field(basics, "Code", code); Field(basics, "Name", name); Field(basics, "Description", description, 85);
        Field(basics, "Version", new Label { Text = definition.Key.Version.ToString(), TextAlign = ContentAlignment.MiddleLeft });
        var parentKind = definition.Key.Kind switch { StrategyCatalogKind.Variant => StrategyCatalogKind.Structure, StrategyCatalogKind.ParameterSet => StrategyCatalogKind.ParameterSchema, StrategyCatalogKind.Deployment => StrategyCatalogKind.Strategy, _ => (StrategyCatalogKind?)null };
        if (parentKind is not null)
        {
            parent.Items.AddRange(choices.Where(x => x.Value.Kind == parentKind).Select(x => x.Key).Cast<object>().ToArray());
            parent.SelectedItem = definition.Parent is null ? null : Label(definition.Parent);
            Field(basics, parentKind == StrategyCatalogKind.Strategy ? "Strategy" : parentKind == StrategyCatalogKind.Structure ? "Structure" : "Parameter schema", parent);
        }
        if (definition.Key.Kind == StrategyCatalogKind.Deployment)
        {
            horizon.Items.AddRange(new object[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly });
            horizon.SelectedItem = definition.Horizon; Field(basics, "Timeframe", horizon);
        }
        AddPage(tabs, "Definition", basics);
        switch (definition.Key.Kind)
        {
            case StrategyCatalogKind.Strategy:
                ReferencePage(tabs, "Families", definition.Families, StrategyCatalogKind.Family);
                ReferencePage(tabs, "Structures", definition.Structures, StrategyCatalogKind.Structure); break;
            case StrategyCatalogKind.Structure:
                RowsPage(tabs, "Expiry groups", ["Group", "After group"], definition.ExpiryGroups.Select(x => new object?[] { x.Key, x.AfterGroup }));
                RowsPage(tabs, "Legs", ["Leg", "Instrument class", "Side", "Option right", "Ratio", "Expiry group"], definition.Legs.Select(x => new object?[] { x.Key, x.InstrumentClass, x.Side, x.OptionRight, x.Ratio, x.ExpiryGroup })); break;
            case StrategyCatalogKind.Variant:
                var traits = FormTable(); Select(side, definition.Side); Select(bias, definition.Bias); Select(premium, definition.PremiumMode);
                Field(traits, "Structure side", side); Field(traits, "Directional bias", bias); Field(traits, "Premium mode", premium);
                delta.Value = Setting("TargetNetDelta"); tolerance.Value = Setting("BalanceTolerance"); minimumWing.Value = Setting("MinimumWingWidth"); maximumWing.Value = Setting("MaximumWingWidth");
                symmetric.Checked = definition.Settings.TryGetProperty("SymmetricWings", out var sym) && sym.ValueKind == JsonValueKind.True;
                Field(traits, "Target net delta", delta); Field(traits, "Balance tolerance", tolerance); Field(traits, "Wing symmetry", symmetric);
                Field(traits, "Minimum wing width", minimumWing); Field(traits, "Maximum wing width", maximumWing);
                AddPage(tabs, "Variant", traits);
                RowsPage(tabs, "Variant legs", ["Leg", "Side", "Ratio"], definition.VariantLegs.Select(x => new object?[] { x.LegKey, x.Side, x.Ratio })); break;
            case StrategyCatalogKind.Deployment:
                ReferencePage(tabs, "Variants", definition.Variants, StrategyCatalogKind.Variant);
                var productGrid = RowsPage(tabs, "Products", ["Symbol", "Product ID", "Exchange", "Currency"], []);
                productGrid.Columns.RemoveAt(0);
                productGrid.Columns.Insert(0, new DataGridViewComboBoxColumn { HeaderText = "Symbol", Name = "Symbol", FlatStyle = FlatStyle.Flat,
                    DataSource = products.Keys.Order(StringComparer.Ordinal).ToArray() });
                for (var i = 1; i < 4; i++) productGrid.Columns[i].ReadOnly = true;
                productGrid.CurrentCellDirtyStateChanged += (_, _) => { if (productGrid.IsCurrentCellDirty) productGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
                productGrid.CellValueChanged += (_, e) =>
                {
                    if (e.RowIndex < 0 || e.ColumnIndex != 0) return;
                    var row = productGrid.Rows[e.RowIndex];
                    if (products.TryGetValue(Cell(row, 0), out var product))
                    { row.Cells[1].Value = product.ProductId; row.Cells[2].Value = product.Exchange; row.Cells[3].Value = product.Currency; }
                };
                foreach (var product in definition.Products) productGrid.Rows.Add(products.First(x => x.Value == product).Key, product.ProductId, product.Exchange, product.Currency);
                RowsPage(tabs, "Pipeline profiles", ["Role", "Stage", "Profile ID", "Version", "Hash"], definition.PipelineParameters.Select(x => new object?[] { x.Role, x.Kind, x.Id, x.Version, x.Hash }));
                var parameters = RowsPage(tabs, "Parameter sets", ["Role", "Parameter set"], []);
                ReferenceColumn(parameters, 1, StrategyCatalogKind.ParameterSet);
                foreach (var p in definition.Parameters) parameters.Rows.Add(p.Role, Label(p.ParameterSet));
                RowsPage(tabs, "Legacy mappings", ["Legacy ID", "Legacy version"], definition.LegacyFamilies.Select(x => new object?[] { x.Id, x.Version })); break;
            case StrategyCatalogKind.ParameterSchema:
                var shape = definition.Settings.Deserialize<CatalogParameterShape>() ?? new();
                PopulateShape(shape, "$", false);
                AddPage(tabs, "Parameter fields", schema); break;
        }
        if (definition.Key.Kind != StrategyCatalogKind.Family)
            RowsPage(tabs, "Capabilities", ["Role", "Capability", "Version"], definition.Capabilities.Select(x => new object?[] { x.Role, x.Code, x.Version }));
        if (definition.Key.Kind is not (StrategyCatalogKind.Family or StrategyCatalogKind.ParameterSchema))
        {
            PopulateSettings(definition.Settings, "");
            AddPage(tabs, definition.Key.Kind == StrategyCatalogKind.ParameterSet ? "Parameter values" : "Additional parameters", settings);
        }
        Controls.Add(tabs); DarkTradingTheme.Apply(this);
        if (editable) foreach (var text in Descendants(this).OfType<TextBox>().Where(x => !x.ReadOnly)) text.Font = editFont;
        foreach (var grid in grids.Values.Append(settings).Append(schema)) { grid.ReadOnly = !editable; grid.AllowUserToAddRows = editable; grid.AllowUserToDeleteRows = editable; }
        if (!editable) foreach (var control in Descendants(this).Where(c => c is TextBox or ComboBox or NumericUpDown or CheckBox)) control.Enabled = false;
    }

    protected override void Dispose(bool disposing) { if (disposing) editFont.Dispose(); base.Dispose(disposing); }

    public StrategyCatalogDefinition ReadDefinition()
    {
        foreach (var grid in grids.Values.Append(settings).Append(schema)) grid.EndEdit();
        if (string.IsNullOrWhiteSpace(code.Text) || string.IsNullOrWhiteSpace(name.Text)) throw new ArgumentException("Code and Name are required.");
        var json = ReadSettings();
        if (source.Key.Kind == StrategyCatalogKind.Variant)
        {
            json["TargetNetDelta"] = delta.Value; json["BalanceTolerance"] = tolerance.Value;
            json["SymmetricWings"] = symmetric.Checked; json["MinimumWingWidth"] = minimumWing.Value; json["MaximumWingWidth"] = maximumWing.Value;
        }
        if (source.Key.Kind == StrategyCatalogKind.ParameterSchema)
        {
            json = JsonSerializer.SerializeToNode(ReadShape())!.AsObject();
        }
        return source with
        {
            Code = code.Text.Trim(), Name = name.Text.Trim(), Description = description.Text,
            Parent = source.Key.Kind is StrategyCatalogKind.Variant or StrategyCatalogKind.ParameterSet or StrategyCatalogKind.Deployment ? Key(parent.SelectedItem?.ToString()) : null,
            Horizon = source.Key.Kind == StrategyCatalogKind.Deployment ? (TimeFrameType)(horizon.SelectedItem ?? throw new ArgumentException("Select a timeframe.")) : TimeFrameType.None,
            Side = source.Key.Kind == StrategyCatalogKind.Variant ? side.Text : "", Bias = source.Key.Kind == StrategyCatalogKind.Variant ? bias.Text : "", PremiumMode = source.Key.Kind == StrategyCatalogKind.Variant ? premium.Text : "",
            Settings = JsonSerializer.SerializeToElement(json), Families = References("Families"), Structures = References("Structures"), Variants = References("Variants"),
            Capabilities = Read("Capabilities", r => new CatalogCapability(Cell(r, 0), Cell(r, 1), Int(r, 2))),
            ExpiryGroups = Read("Expiry groups", r => new CatalogExpiryGroup(Cell(r, 0), EmptyNull(Cell(r, 1)))),
            Legs = Read("Legs", r => new CatalogLeg(Cell(r, 0), Cell(r, 1), Cell(r, 2), Cell(r, 3), Decimal(r, 4), Cell(r, 5))),
            VariantLegs = Read("Variant legs", r => new CatalogVariantLeg(Cell(r, 0), Cell(r, 1), Decimal(r, 2))),
            Products = Read("Products", r => products.TryGetValue(Cell(r, 0), out var product) ? product : throw new ArgumentException("Select a catalog symbol.")),
            PipelineParameters = Read("Pipeline profiles", r => new CatalogPipelineParameter(Cell(r, 0), Enum.Parse<CatalogPipelineParameterKind>(Cell(r, 1), true), Guid.Parse(Cell(r, 2)), Int(r, 3), Cell(r, 4))),
            Parameters = Read("Parameter sets", r => new CatalogParameterBinding(Cell(r, 0), Key(Cell(r, 1)))),
            LegacyFamilies = Read("Legacy mappings", r => new CatalogLegacyFamily(Int(r, 0), long.Parse(Cell(r, 1), CultureInfo.InvariantCulture)))
        };
    }

    decimal Setting(string key) => source.Settings.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n) ? n : 0;
    void ReferencePage(TabControl tabs, string title, CatalogKey[] keys, StrategyCatalogKind kind)
    {
        var grid = RowsPage(tabs, title, ["Definition"], []); ReferenceColumn(grid, 0, kind);
        foreach (var key in keys) grid.Rows.Add(Label(key));
    }
    void ReferenceColumn(DataGridView grid, int index, StrategyCatalogKind kind)
    {
        var column = new DataGridViewComboBoxColumn { HeaderText = grid.Columns[index].HeaderText, Name = grid.Columns[index].Name, FlatStyle = FlatStyle.Flat, DataSource = choices.Where(x => x.Value.Kind == kind).Select(x => x.Key).ToArray(), DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton };
        grid.Columns.RemoveAt(index); grid.Columns.Insert(index, column);
    }
    DataGridView RowsPage(TabControl tabs, string name, string[] headers, IEnumerable<object?[]> rows)
    {
        var grid = Grid(name, headers); grids[name] = grid;
        foreach (var row in rows) grid.Rows.Add(row);
        AddPage(tabs, name, grid); return grid;
    }
    static void AddPage(TabControl tabs, string name, Control content)
    {
        var page = new TabPage(name) { BackColor = Color.Black, ForeColor = Color.White, Padding = new Padding(6) };
        if (content is TableLayoutPanel)
        { var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; scroll.Controls.Add(content); page.Controls.Add(scroll); }
        else page.Controls.Add(content);
        tabs.TabPages.Add(page);
    }
    static DataGridView Grid(string name, params string[] columns)
    {
        var grid = PortfolioUiStyle.Grid(name); grid.Name = name.Replace(" ", ""); grid.ReadOnly = false; grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = true; grid.AllowUserToDeleteRows = true; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        foreach (var column in columns) grid.Columns.Add(column, column);
        grid.DataError += (_, e) => { e.ThrowException = false; if (e.RowIndex >= 0) grid.Rows[e.RowIndex].ErrorText = "Select a valid value."; };
        return grid;
    }
    static TableLayoutPanel FormTable() { var p = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 }; p.ColumnStyles.Add(new(SizeType.Absolute, 165)); p.ColumnStyles.Add(new(SizeType.Percent, 100)); return p; }
    static void Field(TableLayoutPanel panel, string caption, Control control, int height = 38)
    {
        var row = panel.RowCount++; panel.RowStyles.Add(new(SizeType.Absolute, height));
        panel.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = height > 38 ? ContentAlignment.TopRight : ContentAlignment.MiddleRight, Padding = height > 38 ? new Padding(0, 3, 0, 0) : Padding.Empty, Margin = new Padding(4) }, 0, row);
        control.Dock = DockStyle.Fill; control.Margin = new Padding(4); panel.Controls.Add(control, 1, row);
    }
    static TextBox Text(string label) => new() { Name = label.Replace(" ", ""), AccessibleName = label, BackColor = Color.Black, ForeColor = Color.White };
    static ComboBox Combo(string label, params string[] items) { var c = PortfolioUiStyle.Combo(label); c.Name = label.Replace(" ", ""); c.DropDownStyle = ComboBoxStyle.DropDownList; c.Items.AddRange(items); return c; }
    static NumericUpDown Number(string label, decimal min, decimal max) => new() { Name = label.Replace(" ", ""), AccessibleName = label, Minimum = min, Maximum = max, DecimalPlaces = 4, Increment = .01m };
    static void Select(ComboBox combo, string value) { if (!combo.Items.Contains(value)) combo.Items.Add(value); combo.SelectedItem = value; }
    string Label(CatalogKey key) => choices.Single(x => x.Value == key).Key;
    CatalogKey Key(string? label) => label is not null && choices.TryGetValue(label, out var key) ? key : throw new ArgumentException("Select an exact catalog definition.");
    CatalogKey[] References(string name) => Read(name, r => Key(Cell(r, 0)));
    T[] Read<T>(string name, Func<DataGridViewRow, T> read) => grids.TryGetValue(name, out var grid) ? Rows(grid).Select(read).ToArray() : [];
    static IEnumerable<DataGridViewRow> Rows(DataGridView grid) => grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow && r.Cells.Cast<DataGridViewCell>().Any(c => !string.IsNullOrWhiteSpace(c.Value?.ToString())));
    static string Cell(DataGridViewRow row, int index) => row.Cells[index].Value?.ToString()?.Trim() ?? "";
    static int Int(DataGridViewRow row, int index) => int.Parse(Cell(row, index), CultureInfo.InvariantCulture);
    static decimal Decimal(DataGridViewRow row, int index) => decimal.Parse(Cell(row, index), CultureInfo.InvariantCulture);
    static decimal? OptionalDecimal(DataGridViewRow row, int index) => Cell(row, index) == "" ? null : Decimal(row, index);
    static int? OptionalInt(DataGridViewRow row, int index) => Cell(row, index) == "" ? null : Int(row, index);
    static string? EmptyNull(string value) => value.Length == 0 ? null : value;
    static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));

    void PopulateSettings(JsonElement value, string path)
    {
        if (source.Key.Kind == StrategyCatalogKind.Variant && path is "TargetNetDelta" or "BalanceTolerance" or "SymmetricWings" or "MinimumWingWidth" or "MaximumWingWidth") return;
        if (value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Any())
            foreach (var property in value.EnumerateObject()) PopulateSettings(property.Value, path == "" ? Escape(property.Name) : path + "/" + Escape(property.Name));
        else if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0)
        {
            var index = 0; foreach (var child in value.EnumerateArray()) PopulateSettings(child, path + "/" + index++);
        }
        else if (path != "") settings.Rows.Add(path, value.ValueKind.ToString(), value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText());
    }
    JsonObject ReadSettings()
    {
        var root = new JsonObject();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in Rows(settings))
        {
            if (!seen.Add(Cell(row, 0))) throw new ArgumentException("Duplicate parameter path.");
            var path = Cell(row, 0).Split('/').Select(p => p.Replace("~1", "/").Replace("~0", "~")).ToArray();
            if (path.Length == 0 || path.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Parameter paths cannot be empty.");
            var type = Enum.Parse<JsonValueKind>(Cell(row, 1), true);
            JsonNode? value = type switch
            {
                JsonValueKind.String => JsonValue.Create(row.Cells[2].Value?.ToString() ?? ""), JsonValueKind.Number => JsonValue.Create(Decimal(row, 2)),
                JsonValueKind.True => JsonValue.Create(true), JsonValueKind.False => JsonValue.Create(false), JsonValueKind.Null => null,
                JsonValueKind.Object => new JsonObject(), JsonValueKind.Array => new JsonArray(), _ => throw new ArgumentException("Unsupported parameter type.")
            };
            JsonNode node = root;
            for (var i = 0; i < path.Length; i++)
            {
                var last = i == path.Length - 1;
                if (node is JsonObject obj)
                {
                    if (last) { if (obj.ContainsKey(path[i])) throw new ArgumentException("Duplicate parameter path."); obj[path[i]] = value; }
                    else node = obj[path[i]] ?? (obj[path[i]] = int.TryParse(path[i + 1], out _) ? new JsonArray() : new JsonObject())!;
                }
                else if (node is JsonArray array)
                {
                    var index = int.Parse(path[i], CultureInfo.InvariantCulture); if (index is < 0 or > 127) throw new ArgumentException("Array index must be 0-127.");
                    while (array.Count <= index) array.Add(null);
                    if (last) array[index] = value;
                    else node = array[index] ?? (array[index] = int.TryParse(path[i + 1], out _) ? new JsonArray() : new JsonObject())!;
                }
                else throw new ArgumentException("A parameter path overlaps a scalar value.");
            }
        }
        return root;
    }
    // $ is the root, /properties/<escaped-name> is an object field, /items is an array element.
    void PopulateShape(CatalogParameterShape shape, string path, bool required)
    {
        schema.Rows.Add(path, shape.Type, required, shape.Unit, shape.Minimum, shape.Maximum, shape.MinLength, shape.MaxLength, string.Join(",", shape.Choices));
        foreach (var property in shape.Properties) PopulateShape(property.Value, path + "/properties/" + Escape(property.Key), shape.Required.Contains(property.Key));
        if (shape.Items is not null) PopulateShape(shape.Items, path + "/items", false);
    }
    CatalogParameterShape ReadShape()
    {
        var fields = new Dictionary<string, (CatalogParameterShape Shape, bool Required)>(StringComparer.Ordinal);
        foreach (var row in Rows(schema))
        {
            var path = Cell(row, 0);
            if (!fields.TryAdd(path, (new CatalogParameterShape { Type = Enum.Parse<CatalogValueType>(Cell(row, 1), true),
                Unit = Cell(row, 3), Minimum = OptionalDecimal(row, 4), Maximum = OptionalDecimal(row, 5),
                MinLength = OptionalInt(row, 6), MaxLength = OptionalInt(row, 7), Choices = Cell(row, 8).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) },
                bool.TryParse(Cell(row, 2), out var req) && req))) throw new ArgumentException("Duplicate schema path.");
        }
        if (!fields.ContainsKey("$")) throw new ArgumentException("The parameter schema needs a $ root row.");
        var used = new HashSet<string>();
        CatalogParameterShape Build(string path, int depth)
        {
            if (depth > 12) throw new ArgumentException("Parameter schema is too deeply nested.");
            used.Add(path); var shape = fields[path].Shape; var properties = new Dictionary<string, CatalogParameterShape>(); var required = new List<string>();
            var prefix = path + "/properties/";
            foreach (var child in fields.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal) && !x[prefix.Length..].Contains('/')))
            {
                if (shape.Type != CatalogValueType.Object) throw new ArgumentException("Only object fields can contain properties.");
                var key = child[prefix.Length..].Replace("~1", "/").Replace("~0", "~");
                properties.Add(key, Build(child, depth + 1)); if (fields[child].Required) required.Add(key);
            }
            CatalogParameterShape? items = null;
            if (fields.ContainsKey(path + "/items"))
            { if (shape.Type != CatalogValueType.Array) throw new ArgumentException("Only arrays can have items."); items = Build(path + "/items", depth + 1); }
            return shape with { Properties = properties, Required = required.ToArray(), Items = items };
        }
        var root = Build("$", 0);
        if (used.Count != fields.Count) throw new ArgumentException("Schema paths must be $/properties/name or an array's /items, with every parent present.");
        return root;
    }
    static string Escape(string value) => value.Replace("~", "~0").Replace("/", "~1");
}
