namespace OSCC;

public sealed class MappingEditorForm : Form
{
    private readonly CheckBox enabled = new() { Text = "鍚敤", AutoSize = true };
    private readonly TextBox note = new();
    private readonly TextBox parameter = new();
    private readonly TextBox hotkey = new();
    private readonly ComboBox type = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox value = new();
    private readonly TextBox offValue = new();
    private readonly NumericUpDown step = new() { DecimalPlaces = 3, Increment = 0.01m, Minimum = -9999, Maximum = 9999 };
    private readonly NumericUpDown min = new() { DecimalPlaces = 3, Increment = 0.01m, Minimum = -9999, Maximum = 9999 };
    private readonly NumericUpDown max = new() { DecimalPlaces = 3, Increment = 0.01m, Minimum = -9999, Maximum = 9999 };
    private readonly NumericUpDown duration = new() { Minimum = 1, Maximum = 60000, Value = 250 };
    private List<ParameterAction> actions = new();

    public KeyboardMapping? Result { get; private set; }

    public MappingEditorForm(KeyboardMapping? mapping)
    {
        Text = "缂栬緫鎸夐敭鏄犲皠";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(468, 504);
        BackColor = Color.White;

        type.Items.AddRange(UiText.ParameterTypes);
        mode.Items.AddRange(UiText.ControlModes);

        var current = mapping?.Clone() ?? new KeyboardMapping();
        enabled.Checked = current.Enabled;
        note.Text = current.Note;
        parameter.Text = current.Parameter;
        hotkey.Text = current.Hotkey;
        type.SelectedItem = current.Type;
        mode.SelectedItem = current.Mode;
        value.Text = current.Value;
        offValue.Text = current.OffValue;
        step.Value = ToDecimal(current.Step);
        min.Value = ToDecimal(current.Min);
        max.Value = ToDecimal(current.Max);
        duration.Value = current.DurationMs;
        actions = current.EffectiveActions().Select(a => a.Clone()).ToList();

        BuildLayout(current);
        UiHelpers.ApplyControlStyle(this);
    }

    private void BuildLayout(KeyboardMapping current)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(20, 18, 20, 8),
            ColumnCount = 3,
            RowCount = 13,
            Height = 458,
            BackColor = Color.White
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        for (var i = 0; i < 13; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        }

        table.Controls.Add(new Label { Text = "", BackColor = Color.White, Dock = DockStyle.Fill }, 0, 0);
        enabled.Margin = new Padding(0, 7, 0, 0);
        table.Controls.Add(enabled, 1, 0);
        table.SetColumnSpan(enabled, 2);

        AddFullInputRow(table, 1, "澶囨敞", note);
        AddFullInputRow(table, 2, "鍙傛暟鍚?, parameter);
        AddHotkeyRow(table, 3);
        AddFullInputRow(table, 4, "鍙傛暟绫诲瀷", type);
        AddFullInputRow(table, 5, "鎺у埗妯″紡", mode);
        AddFullInputRow(table, 6, "瑙﹀彂鍊?, value);
        AddFullInputRow(table, 7, "鎭㈠鍊?, offValue);
        AddFullInputRow(table, 8, "姝ヨ繘", step);
        AddFullInputRow(table, 9, "鏈€灏忓€?, min);
        AddFullInputRow(table, 10, "鏈€澶у€?, max);
        AddFullInputRow(table, 11, "鑴夊啿姣", duration);
        table.Controls.Add(new Label { Text = "澶氬弬鏁?, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, BackColor = Color.White }, 0, 12);
        table.Controls.Add(ActionButton(), 1, 12);
        Controls.Add(table);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(14),
            BackColor = Color.White
        };
        var save = new Button { Text = "淇濆瓨", DialogResult = DialogResult.OK, Width = 88, Height = 30, Margin = new Padding(6, 0, 0, 0) };
        var cancel = new Button { Text = "鍙栨秷", DialogResult = DialogResult.Cancel, Width = 88, Height = 30, Margin = new Padding(6, 0, 0, 0) };
        save.Click += (_, _) =>
        {
            if (!ValidateMapping())
            {
                DialogResult = DialogResult.None;
                return;
            }

            Result = new KeyboardMapping
            {
                Id = current.Id,
                Enabled = enabled.Checked,
                Note = note.Text.Trim(),
                Parameter = parameter.Text.Trim(),
                Hotkey = hotkey.Text.Trim(),
                Type = Convert.ToString(type.SelectedItem) ?? "bool",
                Mode = Convert.ToString(mode.SelectedItem) ?? "toggle",
                Value = value.Text.Trim(),
                OffValue = offValue.Text.Trim(),
                Step = (double)step.Value,
                Min = (double)min.Value,
                Max = (double)max.Value,
                DurationMs = (int)duration.Value,
                Actions = actions.Select(a => a.Clone()).ToList()
            };
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static void AddFullInputRow(TableLayoutPanel table, int row, string label, Control control)
    {
        AddLabel(table, row, label);
        PrepareInput(control);
        table.Controls.Add(control, 1, row);
        table.SetColumnSpan(control, 2);
    }

    private void AddHotkeyRow(TableLayoutPanel table, int row)
    {
        AddLabel(table, row, "鎸夐敭");
        PrepareInput(hotkey);
        table.Controls.Add(hotkey, 1, row);

        var capture = new Button { Text = "鎹曡幏", Dock = DockStyle.Fill, Margin = new Padding(8, 3, 0, 3) };
        capture.Click += (_, _) => CaptureHotkey();
        table.Controls.Add(capture, 2, row);
    }

    private Button ActionButton()
    {
        var button = new Button { Text = "缂栬緫鍔ㄤ綔鍒楄〃 (" + actions.Count + ")", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
        button.Click += (_, _) =>
        {
            SyncPrimaryAction();
            using var editor = new ActionListEditorForm(actions);
            if (editor.ShowDialog(this) == DialogResult.OK)
            {
                actions = editor.Result.Select(a => a.Clone()).ToList();
                button.Text = "缂栬緫鍔ㄤ綔鍒楄〃 (" + actions.Count + ")";
                ApplyFirstActionToFields();
            }
        };
        return button;
    }

    private void SyncPrimaryAction()
    {
        var first = new ParameterAction
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
        if (actions.Count == 0) actions.Add(first);
        else actions[0] = first;
    }

    private void ApplyFirstActionToFields()
    {
        var first = actions.FirstOrDefault();
        if (first is null) return;
        parameter.Text = first.Parameter;
        type.SelectedItem = first.Type;
        mode.SelectedItem = first.Mode;
        value.Text = first.Value;
        offValue.Text = first.OffValue;
        step.Value = ToDecimal(first.Step);
        min.Value = ToDecimal(first.Min);
        max.Value = ToDecimal(first.Max);
        duration.Value = first.DurationMs;
    }

    private static void AddLabel(TableLayoutPanel table, int row, string text)
    {
        table.Controls.Add(new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 10, 0)
        }, 0, row);
    }

    private static void PrepareInput(Control control)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 3, 0, 3);
    }

    private void CaptureHotkey()
    {
        using var capture = new Form
        {
            Text = "鎸変笅涓€涓揩鎹烽敭",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(320, 92),
            MaximizeBox = false,
            MinimizeBox = false,
            KeyPreview = true,
            BackColor = Color.White
        };
        capture.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "璇锋寜涓嬬粍鍚堥敭锛屼緥濡?Ctrl + Alt + K",
            BackColor = Color.White
        });
        capture.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu)
            {
                return;
            }
            var parts = new List<string>();
            if (e.Control) parts.Add("Ctrl");
            if (e.Alt) parts.Add("Alt");
            if (e.Shift) parts.Add("Shift");
            parts.Add(e.KeyCode.ToString());
            hotkey.Text = string.Join("+", parts);
            capture.Close();
        };
        capture.ShowDialog(this);
    }

    private bool ValidateMapping()
    {
        if (string.IsNullOrWhiteSpace(parameter.Text) || string.IsNullOrWhiteSpace(hotkey.Text))
        {
            MessageBox.Show(this, "鍙傛暟鍚嶅拰鎸夐敭涓嶈兘涓虹┖銆?, "鏃犳硶淇濆瓨", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (!HotkeyManager.TryParse(hotkey.Text, out _, out _))
        {
            MessageBox.Show(this, "鎸夐敭鏃犳硶璇嗗埆锛岃浣跨敤 Ctrl+Alt+K銆丗8銆丆trl+Alt+Up 杩欐牱鐨勬牸寮忋€?, "鏃犳硶淇濆瓨", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        try
        {
            KeyboardActionEngine.Coerce(value.Text, Convert.ToString(type.SelectedItem) ?? "bool");
            KeyboardActionEngine.Coerce(offValue.Text, Convert.ToString(type.SelectedItem) ?? "bool");
            SyncPrimaryAction();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "鍙傛暟鍊兼牸寮忎笉姝ｇ‘: " + ex.Message, "鏃犳硶淇濆瓨", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private static decimal ToDecimal(double value) => Math.Clamp((decimal)value, -9999m, 9999m);
}
