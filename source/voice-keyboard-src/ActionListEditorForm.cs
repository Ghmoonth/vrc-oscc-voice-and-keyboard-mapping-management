namespace OSCC;

public sealed class ActionListEditorForm : Form
{
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        BackgroundColor = Color.White
    };

    public List<ParameterAction> Result { get; private set; }

    public ActionListEditorForm(IEnumerable<ParameterAction> actions)
    {
        Text = "缂栬緫鍙傛暟鍔ㄤ綔鍒楄〃";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(860, 420);
        BackColor = Color.White;
        Result = actions.Select(a => a.Clone()).ToList();
        BuildUi();
        UiHelpers.ApplyControlStyle(this);
        RefreshGrid();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.White, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        grid.Columns.Add(TextColumn("鍙傛暟鍚?, 220));
        grid.Columns.Add(TextColumn("绫诲瀷", 72));
        grid.Columns.Add(TextColumn("妯″紡", 104));
        grid.Columns.Add(TextColumn("瑙﹀彂鍊?, 90));
        grid.Columns.Add(TextColumn("鎭㈠鍊?, 90));
        grid.Columns.Add(TextColumn("姝ヨ繘", 70));
        grid.Columns.Add(TextColumn("鏈€灏?, 70));
        grid.Columns.Add(TextColumn("鏈€澶?, 70));
        grid.Columns.Add(TextColumn("鑴夊啿ms", 80));
        root.Controls.Add(grid, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.White, Padding = new Padding(0, 8, 0, 0) };
        buttons.Controls.Add(Button("淇濆瓨", SaveAndClose));
        buttons.Controls.Add(Button("鍙栨秷", () => DialogResult = DialogResult.Cancel));
        buttons.Controls.Add(Button("鍒犻櫎", Delete));
        buttons.Controls.Add(Button("缂栬緫", Edit));
        buttons.Controls.Add(Button("鏂板", Add));
        root.Controls.Add(buttons, 0, 1);
    }

    private void RefreshGrid()
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        foreach (var action in Result)
        {
            var i = grid.Rows.Add(action.Parameter, action.Type, action.Mode, action.Value, action.OffValue, action.Step, action.Min, action.Max, action.DurationMs);
            grid.Rows[i].Tag = action;
        }
        grid.ResumeLayout();
    }

    private ParameterAction? Selected() => grid.CurrentRow?.Tag as ParameterAction;

    private void Add()
    {
        using var editor = new ParameterActionEditorForm(null);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is not null)
        {
            Result.Add(editor.Result);
            RefreshGrid();
        }
    }

    private void Edit()
    {
        var selected = Selected();
        if (selected is null) return;
        using var editor = new ParameterActionEditorForm(selected);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is not null)
        {
            var index = Result.IndexOf(selected);
            Result[index] = editor.Result;
            RefreshGrid();
        }
    }

    private void Delete()
    {
        var selected = Selected();
        if (selected is null) return;
        Result.Remove(selected);
        RefreshGrid();
    }

    private void SaveAndClose()
    {
        if (Result.Count == 0)
        {
            MessageBox.Show(this, "鑷冲皯闇€瑕佷竴鏉″弬鏁板姩浣溿€?, "鏃犳硶淇濆瓨", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        DialogResult = DialogResult.OK;
    }

    private static DataGridViewTextBoxColumn TextColumn(string header, int width) => new() { HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable };

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 82, Height = 30, Margin = new Padding(6, 0, 0, 0) };
        button.Click += (_, _) => action();
        return button;
    }
}

public sealed class ParameterActionEditorForm : Form
{
    private readonly TextBox parameter = new();
    private readonly ComboBox type = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox value = new();
    private readonly TextBox offValue = new();
    private readonly NumericUpDown step = new() { DecimalPlaces = 3, Increment = 0.01m, Minimum = -9999, Maximum = 9999 };
    private readonly NumericUpDown min = new() { DecimalPlaces = 3, Increment = 0.01m, Minimum = -9999, Maximum = 9999 };
    private readonly NumericUpDown max = new() { DecimalPlaces = 3, Increment = 0.01m, Minimum = -9999, Maximum = 9999 };
    private readonly NumericUpDown duration = new() { Minimum = 1, Maximum = 60000, Value = 250 };

    public ParameterAction? Result { get; private set; }

    public ParameterActionEditorForm(ParameterAction? action)
    {
        Text = "缂栬緫鍙傛暟鍔ㄤ綔";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 360);
        BackColor = Color.White;
        type.Items.AddRange(UiText.ParameterTypes);
        mode.Items.AddRange(UiText.ControlModes);
        var current = action?.Clone() ?? new ParameterAction();
        parameter.Text = current.Parameter;
        type.SelectedItem = current.Type;
        mode.SelectedItem = current.Mode;
        value.Text = current.Value;
        offValue.Text = current.OffValue;
        step.Value = ToDecimal(current.Step);
        min.Value = ToDecimal(current.Min);
        max.Value = ToDecimal(current.Max);
        duration.Value = current.DurationMs;
        BuildUi();
        UiHelpers.ApplyControlStyle(this);
    }

    private void BuildUi()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 9, Height = 290, Padding = new Padding(18), BackColor = Color.White };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(table);
        AddRow(table, 0, "鍙傛暟鍚?, parameter);
        AddRow(table, 1, "鍙傛暟绫诲瀷", type);
        AddRow(table, 2, "鎺у埗妯″紡", mode);
        AddRow(table, 3, "瑙﹀彂鍊?, value);
        AddRow(table, 4, "鎭㈠鍊?, offValue);
        AddRow(table, 5, "姝ヨ繘", step);
        AddRow(table, 6, "鏈€灏忓€?, min);
        AddRow(table, 7, "鏈€澶у€?, max);
        AddRow(table, 8, "鑴夊啿姣", duration);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(14), BackColor = Color.White };
        buttons.Controls.Add(Button("淇濆瓨", Save));
        buttons.Controls.Add(Button("鍙栨秷", () => DialogResult = DialogResult.Cancel));
        Controls.Add(buttons);
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(parameter.Text))
        {
            MessageBox.Show(this, "鍙傛暟鍚嶄笉鑳戒负绌恒€?, "鏃犳硶淇濆瓨", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            KeyboardActionEngine.Coerce(value.Text, Convert.ToString(type.SelectedItem) ?? "bool");
            KeyboardActionEngine.Coerce(offValue.Text, Convert.ToString(type.SelectedItem) ?? "bool");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "鍙傛暟鍊兼牸寮忎笉姝ｇ‘: " + ex.Message, "鏃犳硶淇濆瓨", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Result = new ParameterAction
        {
            Parameter = parameter.Text.Trim(),
            Type = Convert.ToString(type.SelectedItem) ?? "bool",
            Mode = Convert.ToString(mode.SelectedItem) ?? "toggle",
            Value = value.Text.Trim(),
            OffValue = offValue.Text.Trim(),
            Step = (double)step.Value,
            Min = (double)min.Value,
            Max = (double)max.Value,
            DurationMs = (int)duration.Value
        };
        DialogResult = DialogResult.OK;
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 2, 0, 2);
        table.Controls.Add(control, 1, row);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 82, Height = 30, Margin = new Padding(6, 0, 0, 0) };
        button.Click += (_, _) => action();
        return button;
    }

    private static decimal ToDecimal(double value) => Math.Clamp((decimal)value, -9999m, 9999m);
}
