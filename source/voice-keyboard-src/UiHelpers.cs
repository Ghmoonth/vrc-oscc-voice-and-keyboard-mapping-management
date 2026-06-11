namespace OSCC;

public static class UiHelpers
{
    public static readonly Color Border = Color.FromArgb(220, 220, 220);
    public static readonly Color Soft = Color.FromArgb(247, 247, 247);
    public static readonly Color Primary = Color.FromArgb(32, 32, 32);

    public static void ApplyControlStyle(Control root)
    {
        root.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
        EnableDoubleBuffering(root);
        ApplyRecursive(root);
    }

    public static Button Button(string text, Action action, bool primary = false, int width = 94)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 8, 0),
            BackColor = primary ? Primary : Color.White,
            ForeColor = primary ? Color.White : Color.Black,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = primary ? Primary : Border;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(56, 56, 56) : Soft;
        button.Click += (_, _) => action();
        return button;
    }

    public static SectionPanel Section(string title, Padding? padding = null, Padding? margin = null) => new()
    {
        Text = title,
        Dock = DockStyle.Fill,
        BackColor = Color.White,
        Padding = padding ?? new Padding(10, 18, 10, 10),
        Margin = margin ?? new Padding(0)
    };

    public static void AppendLimited(TextBox box, string message, int maxLines = 500)
    {
        box.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        if (box.Lines.Length <= maxLines) return;
        box.Lines = box.Lines.Skip(box.Lines.Length - maxLines).ToArray();
        box.SelectionStart = box.TextLength;
        box.ScrollToCaret();
    }

    private static void ApplyRecursive(Control root)
    {
        foreach (Control control in root.Controls)
        {
            EnableDoubleBuffering(control);
            switch (control)
            {
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = Border;
                    button.BackColor = button.BackColor == Color.Empty ? Color.White : button.BackColor;
                    button.Cursor = Cursors.Hand;
                    break;
                case TextBox textBox:
                    textBox.BorderStyle = textBox.Multiline && textBox.ReadOnly
                        ? BorderStyle.None
                        : BorderStyle.FixedSingle;
                    break;
                case ComboBox combo:
                    combo.FlatStyle = FlatStyle.Standard;
                    combo.IntegralHeight = false;
                    combo.MaxDropDownItems = 12;
                    break;
                case NumericUpDown numeric:
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case CheckedListBox checkedListBox:
                    checkedListBox.BorderStyle = BorderStyle.None;
                    break;
                case ListBox listBox:
                    listBox.BorderStyle = BorderStyle.None;
                    break;
                case DataGridView grid:
                    grid.EnableHeadersVisualStyles = false;
                    grid.BackgroundColor = Color.White;
                    grid.BorderStyle = BorderStyle.None;
                    grid.GridColor = Border;
                    grid.RowHeadersVisible = false;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = Soft;
                    grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(226, 226, 226);
                    grid.DefaultCellStyle.SelectionForeColor = Color.Black;
                    break;
            }
            ApplyRecursive(control);
        }
    }

    private static void EnableDoubleBuffering(Control control)
    {
        try
        {
            var property = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            property?.SetValue(control, true, null);
        }
        catch
        {
            // Double buffering is a best-effort resize smoothness optimization.
        }
    }
}

public sealed class SectionPanel : Panel
{
    public SectionPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(UiHelpers.Border);
        var rect = new Rectangle(0, 8, Width - 1, Height - 9);
        e.Graphics.DrawRectangle(pen, rect);

        if (string.IsNullOrWhiteSpace(Text)) return;
        var size = TextRenderer.MeasureText(Text, Font);
        var titleRect = new Rectangle(10, 0, Math.Min(size.Width + 10, Math.Max(0, Width - 20)), size.Height + 2);
        using var back = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(back, titleRect);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Point(14, 0), Color.FromArgb(40, 40, 40));
    }
}
