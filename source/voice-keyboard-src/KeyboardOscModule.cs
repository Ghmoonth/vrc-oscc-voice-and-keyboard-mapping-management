namespace OSCC;

public sealed class KeyboardOscModule : UserControl, IOsccModule
{
    private readonly DataGridView grid = new()
    {
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        ReadOnly = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        Dock = DockStyle.Fill
    };

    private ModuleContext? context;
    private KeyboardActionEngine? engine;
    private HotkeyManager? hotkeys;
    private bool isRunning;

    public string Id => "keyboard";
    public string DisplayName => "閿洏鍙傛暟鎺у埗";
    public bool IsRunning => isRunning;
    public Control View => this;

    public KeyboardOscModule()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        BuildUi();
    }

    public void Initialize(ModuleContext context)
    {
        this.context = context;
        engine = new KeyboardActionEngine(context.Osc, context.Log);
        hotkeys = new HotkeyManager(context.Owner, TriggerMapping, context.Log);
        RefreshGrid();
    }

    public void Start()
    {
        RequireContext().SaveConfig();
        hotkeys?.RegisterAll(RequireContext().Config.Mappings);
        isRunning = true;
    }

    public void Stop()
    {
        hotkeys?.UnregisterAll();
        isRunning = false;
        RequireContext().Log("閿洏妯″潡宸插仠姝€?);
    }

    public bool HandleMessage(ref Message message) => hotkeys?.HandleMessage(ref message) == true;

    public new void Dispose()
    {
        hotkeys?.Dispose();
        base.Dispose();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.White };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        ConfigureGrid();
        root.Controls.Add(grid, 0, 0);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Color.White
        };
        toolbar.Controls.Add(Button("鏂板", AddMapping));
        toolbar.Controls.Add(Button("缂栬緫", EditMapping));
        toolbar.Controls.Add(Button("鍒犻櫎", DeleteMapping));
        toolbar.Controls.Add(Button("娴嬭瘯鍙戦€?, TestMapping));
        root.Controls.Add(toolbar, 0, 1);
    }

    private void ConfigureGrid()
    {
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 30, 30);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 248, 248);
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 232, 252);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.White;
        grid.GridColor = Color.FromArgb(225, 225, 225);
        grid.Columns.Clear();
        grid.Columns.Add(TextColumn("澶囨敞", 180));
        grid.Columns.Add(TextColumn("鍙傛暟鍚?, 250));
        grid.Columns.Add(TextColumn("鎸夐敭", 130));
        grid.Columns.Add(TextColumn("鍚敤", 58));
        grid.Columns.Add(TextColumn("绫诲瀷", 72));
        grid.Columns.Add(TextColumn("妯″紡", 108));
        grid.Columns.Add(TextColumn("瑙﹀彂鍊?, 104));
        grid.Columns.Add(TextColumn("鎭㈠鍊?, 104));
        grid.Columns.Add(TextColumn("姝ヨ繘", 78));
        grid.Columns.Add(TextColumn("鏈€灏?, 78));
        grid.Columns.Add(TextColumn("鏈€澶?, 78));
        grid.DoubleClick += (_, _) => EditMapping();
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 94, Height = 30, Margin = new Padding(0, 0, 10, 0) };
        button.Click += (_, _) => action();
        return button;
    }

    private static DataGridViewTextBoxColumn TextColumn(string header, int width) => new()
    {
        HeaderText = header,
        Width = width,
        SortMode = DataGridViewColumnSortMode.NotSortable
    };

    private void RefreshGrid()
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        foreach (var mapping in RequireContext().Config.Mappings)
        {
            var rowIndex = grid.Rows.Add(
                mapping.Note,
                mapping.Parameter,
                mapping.Hotkey,
                mapping.Enabled ? "鏄? : "鍚?,
                mapping.Type,
                mapping.Mode,
                mapping.Value,
                mapping.OffValue,
                mapping.Step,
                mapping.Min,
                mapping.Max);
            grid.Rows[rowIndex].Tag = mapping;
        }
        grid.ResumeLayout();
    }

    private KeyboardMapping? SelectedMapping() => grid.CurrentRow?.Tag as KeyboardMapping;

    private void AddMapping()
    {
        using var editor = new MappingEditorForm(null);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is not null)
        {
            RequireContext().Config.Mappings.Add(editor.Result);
            RequireContext().SaveConfig();
            RefreshGrid();
        }
    }

    private void EditMapping()
    {
        var selected = SelectedMapping();
        if (selected is null)
        {
            MessageBox.Show(this, UiText.SelectMappingMessage, UiText.SelectMappingTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var editor = new MappingEditorForm(selected);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is not null)
        {
            var index = RequireContext().Config.Mappings.FindIndex(m => m.Id == selected.Id);
            RequireContext().Config.Mappings[index] = editor.Result;
            RequireContext().SaveConfig();
            RefreshGrid();
        }
    }

    private void DeleteMapping()
    {
        var selected = SelectedMapping();
        if (selected is null)
        {
            MessageBox.Show(this, UiText.SelectMappingMessage, UiText.SelectMappingTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this, $"鍒犻櫎 {selected.Hotkey} -> {selected.Parameter}锛?, "鍒犻櫎鏄犲皠", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }
        RequireContext().Config.Mappings.RemoveAll(m => m.Id == selected.Id);
        RequireContext().SaveConfig();
        RefreshGrid();
    }

    private void TestMapping()
    {
        var selected = SelectedMapping();
        if (selected is null)
        {
            MessageBox.Show(this, UiText.SelectMappingMessage, UiText.SelectMappingTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        engine?.Run(selected);
    }

    private void TriggerMapping(string mappingId)
    {
        var mapping = RequireContext().Config.Mappings.FirstOrDefault(m => m.Id == mappingId);
        if (mapping is { Enabled: true })
        {
            engine?.Run(mapping);
        }
    }

    private ModuleContext RequireContext() => context ?? throw new InvalidOperationException("Module is not initialized.");
}
