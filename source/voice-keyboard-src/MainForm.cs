namespace OSCC;

public sealed class MainForm : Form
{
    private readonly AppConfig config;
    private readonly OscClient osc;
    private readonly ModuleHost modules = new();
    private readonly Dictionary<IOsccModule, CheckBox> moduleSwitches = new();
    private readonly TextBox host = new() { Text = "127.0.0.1", Margin = new Padding(0, 6, 16, 6) };
    private readonly NumericUpDown port = new() { Minimum = 1, Maximum = 65535, Value = 9000, Margin = new Padding(0, 6, 16, 6) };
    private readonly TabControl moduleTabs = new() { Dock = DockStyle.Fill };
    private readonly SplitContainer contentSplit = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, BackColor = Color.White, Panel1MinSize = 220, Panel2MinSize = 0 };
    private readonly Button logToggle = new() { Text = "鏄剧ず鏃ュ織", Width = 92, Height = 28, Margin = new Padding(0, 0, 6, 4) };
    private readonly ContextMenuStrip moreMenu = new();
    private readonly FlowLayoutPanel moduleSwitchPanel = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        BackColor = Color.White,
        Padding = new Padding(0, 8, 0, 0)
    };
    private readonly TextBox log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None,
        BackColor = Color.White
    };
    private readonly Label status = new()
    {
        Text = "鏈惎鍔?,
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleRight,
        Margin = new Padding(0, 9, 0, 0)
    };
    private readonly NotifyIcon tray;
    private readonly Icon appIcon;
    private bool updatingSwitches;
    private bool logVisible;

    public MainForm()
    {
        Text = "OSCC";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1120, 680);
        Size = new Size(1280, 760);
        BackColor = Color.White;
        appIcon = LoadAppIcon();
        Icon = appIcon;

        config = ConfigStore.Load();
        host.Text = config.OscHost;
        port.Value = Math.Clamp(config.OscPort, 1, 65535);
        osc = new OscClient(config.OscHost, config.OscPort);

        tray = new NotifyIcon
        {
            Icon = appIcon,
            Text = "OSCC",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        tray.DoubleClick += (_, _) => RestoreFromTray();

        BuildUi();
        RegisterModules();
        UiHelpers.ApplyControlStyle(this);
        AppendLog("OSCC 宸插惎鍔ㄣ€?);
    }

    protected override void WndProc(ref Message m)
    {
        if (modules.HandleMessage(ref m))
        {
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        modules.Dispose();
        osc.Dispose();
        tray.Dispose();
        appIcon.Dispose();
        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            tray.ShowBalloonTip(1200, "OSCC", "宸叉渶灏忓寲鍒版墭鐩橈紝杩愯涓殑妯″潡浼氱户缁伐浣溿€?, ToolTipIcon.Info);
        }
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            RowCount = 4,
            ColumnCount = 1,
            BackColor = Color.White
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);

        root.Controls.Add(BuildTopBar(), 0, 0);
        root.Controls.Add(BuildModuleSwitchBar(), 0, 1);
        root.Controls.Add(BuildContentPanel(), 0, 2);
        root.Controls.Add(BuildFooterBar(), 0, 3);
    }

    private Control BuildTopBar()
    {
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 9, BackColor = Color.White };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));

        top.Controls.Add(UiHelpers.Button("甯姪", ShowHelp, width: 44), 0, 0);
        top.Controls.Add(HeaderLabel("OSC Host"), 1, 0);
        host.Dock = DockStyle.Fill;
        top.Controls.Add(host, 2, 0);
        top.Controls.Add(HeaderLabel("Port"), 3, 0);
        port.Dock = DockStyle.Fill;
        top.Controls.Add(port, 4, 0);
        top.Controls.Add(new Label { BackColor = Color.White }, 5, 0);
        top.Controls.Add(UiHelpers.Button("鍏ㄩ儴鍚姩", StartModules, primary: true), 6, 0);
        top.Controls.Add(UiHelpers.Button("鍏ㄩ儴鍋滄", StopModules), 7, 0);
        top.Controls.Add(UiHelpers.Button("鏇村", ShowMoreMenu), 8, 0);
        return top;
    }

    private void ShowMoreMenu()
    {
        if (moreMenu.Items.Count == 0)
        {
            moreMenu.Items.Add("淇濆瓨閰嶇疆", null, (_, _) => SaveConfig());
            moreMenu.Items.Add("鏈€灏忓寲鎵樼洏", null, (_, _) => MinimizeToTray());
            moreMenu.Items.Add(logVisible ? "闅愯棌鏃ュ織" : "鏄剧ず鏃ュ織", null, (_, _) => ToggleLog());
        }
        moreMenu.Items[^1].Text = logVisible ? "闅愯棌鏃ュ織" : "鏄剧ず鏃ュ織";
        moreMenu.Show(this, PointToClient(Cursor.Position));
    }

    private void ShowHelp()
    {
        using var help = new HelpForm();
        help.ShowDialog(this);
    }

    private Control BuildModuleSwitchBar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.White,
            Padding = new Padding(0, 2, 0, 6),
            Margin = new Padding(0)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.Controls.Add(new Label
        {
            Text = "妯″潡寮€鍏?,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.White,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0)
        }, 0, 0);
        moduleSwitchPanel.Margin = new Padding(0);
        bar.Controls.Add(moduleSwitchPanel, 1, 0);
        return bar;
    }

    private Control BuildFooterBar()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.White };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 292));
        footer.Controls.Add(status, 0, 0);
        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 7, 0, 0),
            BackColor = Color.White
        };
        logToggle.Click += (_, _) => ToggleLog();
        right.Controls.Add(logToggle);
        right.Controls.Add(UiHelpers.Button("鏈€灏忓寲鎵樼洏", MinimizeToTray));
        footer.Controls.Add(right, 1, 0);
        return footer;
    }

    private Control BuildContentPanel()
    {
        contentSplit.Panel1.Controls.Add(moduleTabs);
        contentSplit.Panel2.Controls.Add(BuildLogBox());
        contentSplit.HandleCreated += (_, _) => BeginInvoke((Action)ApplyLogVisibility);
        contentSplit.Resize += (_, _) => ApplyLogVisibility();
        return contentSplit;
    }

    private void ToggleLog()
    {
        logVisible = !logVisible;
        ApplyLogVisibility();
    }

    private void ApplyLogVisibility()
    {
        if (!contentSplit.IsHandleCreated || contentSplit.Height <= 0) return;
        contentSplit.Panel2Collapsed = !logVisible;
        logToggle.Text = logVisible ? "闅愯棌鏃ュ織" : "鏄剧ず鏃ュ織";
        if (!logVisible) return;
        var available = contentSplit.Height - contentSplit.SplitterWidth;
        if (available <= contentSplit.Panel1MinSize) return;
        var target = Math.Max(contentSplit.Panel1MinSize, available / 2);
        if (Math.Abs(contentSplit.SplitterDistance - target) > 8)
        {
            contentSplit.SplitterDistance = target;
        }
    }

    private Control BuildLogBox()
    {
        var box = UiHelpers.Section("鏃ュ織", new Padding(10, 18, 10, 10));
        log.Dock = DockStyle.Fill;
        box.Controls.Add(log);
        return box;
    }

    private void RegisterModules()
    {
        var context = new ModuleContext
        {
            Owner = this,
            Config = config,
            Osc = osc,
            Log = AppendLog,
            SaveConfig = SaveConfig
        };

        modules.Register(new KeyboardOscModule(), context);
        modules.Register(new VoiceMappingModule(), context);
        modules.Register(new AiTranslationModule(), context);

        foreach (var module in modules.Modules)
        {
            AddModuleTab(module);
            AddModuleSwitch(module);
        }
    }

    private void AddModuleTab(IOsccModule module)
    {
        var page = new TabPage(module.DisplayName) { BackColor = Color.White };
        module.View.Dock = DockStyle.Fill;
        page.Controls.Add(module.View);
        moduleTabs.TabPages.Add(page);
    }

    private void AddModuleSwitch(IOsccModule module)
    {
        var toggle = new CheckBox
        {
            Text = module.DisplayName,
            AutoSize = true,
            Margin = new Padding(0, 0, 24, 0),
            BackColor = Color.White
        };
        toggle.CheckedChanged += (_, _) =>
        {
            if (updatingSwitches)
            {
                return;
            }
            SetModuleRunning(module, toggle.Checked);
        };
        moduleSwitches[module] = toggle;
        moduleSwitchPanel.Controls.Add(toggle);
    }

    private void SetModuleRunning(IOsccModule module, bool shouldRun)
    {
        SaveConfig();
        if (shouldRun && !module.IsRunning)
        {
            module.Start();
        }
        else if (!shouldRun && module.IsRunning)
        {
            module.Stop();
        }
        SyncSwitches();
        UpdateStatus();
    }

    private void SyncSwitches()
    {
        updatingSwitches = true;
        foreach (var (module, toggle) in moduleSwitches)
        {
            toggle.Checked = module.IsRunning;
        }
        updatingSwitches = false;
    }

    private void UpdateStatus()
    {
        var count = modules.Modules.Count(m => m.IsRunning);
        status.Text = count == 0 ? "鏈惎鍔? : $"杩愯涓細{count}/{modules.Modules.Count}";
    }

    private static Label HeaderLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        BackColor = Color.White
    };

    private static Button Button(string text, Action action)
    {
        return UiHelpers.Button(text, action);
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("鏄剧ず绐楀彛", null, (_, _) => RestoreFromTray());
        menu.Items.Add("鍏ㄩ儴鍚姩", null, (_, _) => StartModules());
        menu.Items.Add("鍏ㄩ儴鍋滄", null, (_, _) => StopModules());
        menu.Items.Add("閫€鍑?, null, (_, _) => Close());
        return menu;
    }

    private void SaveConfig()
    {
        config.OscHost = host.Text.Trim();
        config.OscPort = (int)port.Value;
        ConfigStore.Save(config);
        ApplyOscTarget();
        AppendLog("閰嶇疆宸蹭繚瀛? " + ConfigStore.ConfigPath);
    }

    private void ApplyOscTarget() => osc.UpdateTarget(host.Text.Trim(), (int)port.Value);

    private void StartModules()
    {
        SaveConfig();
        modules.StartAll();
        SyncSwitches();
        UpdateStatus();
    }

    private void StopModules()
    {
        modules.StopAll();
        SyncSwitches();
        UpdateStatus();
    }

    private void MinimizeToTray() => WindowState = FormWindowState.Minimized;

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke((Action)(() => AppendLog(message)));
            return;
        }
        UiHelpers.AppendLimited(log, message);
    }

    private static Icon LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "app.ico");
        return File.Exists(path) ? new Icon(path) : (Icon)SystemIcons.Application.Clone();
    }
}
