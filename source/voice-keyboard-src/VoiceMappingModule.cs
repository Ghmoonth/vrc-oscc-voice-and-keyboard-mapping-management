namespace OSCC;

public sealed class VoiceMappingModule : UserControl, IOsccModule
{
    private const string RecommendedAsrEndpoint = "wss://dashscope.aliyuncs.com/api-ws/v1/inference";
    private const string RecommendedAsrModel = "paraformer-realtime-v2";
    private const string RecommendedAiEndpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
    private const string RecommendedAiModel = "qwen-max-latest";
    private static readonly string[] SourceTypes = { "手动输入", "文字文件", "音频文件夹", "音频设备/程序" };
    private static readonly string[] MatchModes = { "AI 语义判断", "包含任意关键词", "包含全部关键词", "完全等于", "正则表达式" };
    private static readonly ProviderModelPreset[] AsrPresets =
    {
        new("千问/百炼", "实时识别", RecommendedAsrEndpoint, RecommendedAsrModel, "实时 WebSocket，适合语音命令"),
        new("OpenAI", "GPT-4o Transcribe", "https://api.openai.com/v1/audio/transcriptions", "gpt-4o-transcribe", "高质量转写"),
        new("OpenAI", "GPT-4o Mini Transcribe", "https://api.openai.com/v1/audio/transcriptions", "gpt-4o-mini-transcribe", "速度和成本更均衡"),
        new("OpenAI", "Whisper", "https://api.openai.com/v1/audio/transcriptions", "whisper-1", "通用兼容"),
        new("豆包/火山方舟", "音频转写", "https://ark.cn-beijing.volces.com/api/v3/audio/transcriptions", "doubao-asr", "如账号端点不同，请按控制台调整"),
        new("自定义", "自定义", "", "", "保留手动输入")
    };
    private static readonly ProviderModelPreset[] AiPresets =
    {
        new("千问/百炼", "最强", RecommendedAiEndpoint, RecommendedAiModel, "复杂意图判断"),
        new("千问/百炼", "均衡", RecommendedAiEndpoint, "qwen-plus-latest", "速度和效果均衡"),
        new("千问/百炼", "快速", RecommendedAiEndpoint, "qwen-turbo-latest", "低延迟"),
        new("OpenAI", "GPT-4.1", "https://api.openai.com/v1/chat/completions", "gpt-4.1", "强推理"),
        new("OpenAI", "GPT-4.1 Mini", "https://api.openai.com/v1/chat/completions", "gpt-4.1-mini", "低延迟"),
        new("OpenAI", "GPT-4o Mini", "https://api.openai.com/v1/chat/completions", "gpt-4o-mini", "通用低延迟"),
        new("豆包/火山方舟", "Pro", "https://ark.cn-beijing.volces.com/api/v3/chat/completions", "doubao-1-5-pro-32k", "中文意图判断"),
        new("豆包/火山方舟", "Lite", "https://ark.cn-beijing.volces.com/api/v3/chat/completions", "doubao-1-5-lite-32k", "低成本"),
        new("DeepSeek", "V4 Flash", "https://api.deepseek.com/chat/completions", "deepseek-v4-flash", "高速中文意图判断"),
        new("DeepSeek", "Chat", "https://api.deepseek.com/chat/completions", "deepseek-v4-flash", "默认 Chat 预设"),
        new("智谱 GLM", "4 Plus", "https://open.bigmodel.cn/api/paas/v4/chat/completions", "glm-4-plus", "国产通用"),
        new("智谱 GLM", "4 Flash", "https://open.bigmodel.cn/api/paas/v4/chat/completions", "glm-4-flash", "低延迟"),
        new("月之暗面 Kimi", "K2", "https://api.moonshot.cn/v1/chat/completions", "kimi-k2-0711-preview", "长文本"),
        new("月之暗面 Kimi", "Latest", "https://api.moonshot.cn/v1/chat/completions", "moonshot-v1-auto", "自动路由"),
        new("自定义", "自定义", "", "", "保留手动输入")
    };

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
    private readonly ComboBox source = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox asrProvider = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox asrPreset = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox endpoint = new();
    private readonly TextBox apiKey = new() { UseSystemPasswordChar = true };
    private readonly TextBox model = new();
    private readonly ComboBox aiProvider = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox aiPreset = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox asrEndpoint = new();
    private readonly TextBox asrKey = new() { UseSystemPasswordChar = true };
    private readonly TextBox asrModel = new();
    private readonly NumericUpDown threshold = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 1, Increment = 0.01m, Value = 0.75m };
    private readonly NumericUpDown vadThreshold = new() { DecimalPlaces = 3, Minimum = 0.001m, Maximum = 0.2m, Increment = 0.001m, Value = 0.004m };
    private readonly NumericUpDown vadPreRoll = new() { Minimum = 100, Maximum = 2000, Increment = 50, Value = 700 };
    private readonly NumericUpDown vadVoiceStart = new() { Minimum = 60, Maximum = 1000, Increment = 20, Value = 200 };
    private readonly NumericUpDown vadSilenceEnd = new() { Minimum = 200, Maximum = 3000, Increment = 50, Value = 900 };
    private readonly NumericUpDown vadMinUtterance = new() { Minimum = 100, Maximum = 5000, Increment = 50, Value = 500 };
    private readonly NumericUpDown vadMaxUtterance = new() { Minimum = 1000, Maximum = 60000, Increment = 500, Value = 12000 };
    private readonly TextBox textFile = new();
    private readonly TextBox audioFolder = new();
    private readonly CheckedListBox microphones = new() { CheckOnClick = true, BorderStyle = BorderStyle.None };
    private readonly CheckedListBox speakers = new() { CheckOnClick = true, BorderStyle = BorderStyle.None };
    private readonly CheckedListBox programs = new() { CheckOnClick = true, BorderStyle = BorderStyle.None };
    private readonly NumericUpDown pollMs = new() { Minimum = 100, Maximum = 10000, Increment = 100, Value = 500 };
    private readonly CheckBox enabled = new() { Text = "启用语音识别", AutoSize = true };
    private readonly CheckBox ignoreCase = new() { Text = "忽略大小写", AutoSize = true };
    private readonly CheckBox once = new() { Text = "每次识别只触发一次", AutoSize = true };
    private readonly TextBox manualText = new();
    private readonly TextBox recognized = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = Color.White
    };

    private readonly HttpClient http = new();
    private readonly HashSet<string> processedAudio = new(StringComparer.OrdinalIgnoreCase);
    private ModuleContext? context;
    private KeyboardActionEngine? engine;
    private FileSystemWatcher? textWatcher;
    private FileSystemWatcher? audioWatcher;
    private AudioCaptureSession? captureSession;
    private System.Windows.Forms.Timer? pollTimer;
    private long lastTextLength;
    private bool isRunning;

    public string Id => "voice-mapping";
    public string DisplayName => "语音参数控制";
    public bool IsRunning => isRunning;
    public Control View => this;

    public VoiceMappingModule()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        source.Items.AddRange(SourceTypes);
        asrProvider.Items.AddRange(AsrPresets.Select(p => p.Provider).Distinct().ToArray());
        aiProvider.Items.AddRange(AiPresets.Select(p => p.Provider).Distinct().ToArray());
        asrProvider.SelectedIndexChanged += (_, _) => UpdateAsrModelChoices(true);
        aiProvider.SelectedIndexChanged += (_, _) => UpdateAiModelChoices(true);
        asrPreset.SelectedIndexChanged += (_, _) => ApplyAsrPreset(true);
        aiPreset.SelectedIndexChanged += (_, _) => ApplyAiPreset(true);
        BuildUi();
    }

    public void Initialize(ModuleContext context)
    {
        this.context = context;
        engine = new KeyboardActionEngine(context.Osc, context.Log);
        LoadSettings();
        RefreshGrid();
    }

    public void Start()
    {
        SaveSettings();
        StopWatchers();
        isRunning = true;
        if (source.Text == "文字文件") StartTextWatcher();
        if (source.Text == "音频文件夹") StartAudioWatcher();
        if (source.Text is "音频设备" or "音频设备/程序") StartDeviceCapturePlaceholder();
        RequireContext().Log("语音模块已启动。");
    }

    public void Stop()
    {
        StopWatchers();
        isRunning = false;
        RequireContext().Log("语音模块已停止。");
    }

    public bool HandleMessage(ref Message message) => false;

    public new void Dispose()
    {
        StopWatchers();
        http.Dispose();
        base.Dispose();
    }

    private void BuildUi()
    {
        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            SplitterWidth = 6,
            Panel1MinSize = 120,
            Panel2MinSize = 120
        };
        root.Resize += (_, _) => ApplySplitRatio(root);
        root.HandleCreated += (_, _) => BeginInvoke((Action)(() => ApplySplitRatio(root)));
        Controls.Add(root);

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.White };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.Panel1.Controls.Add(left);
        ConfigureGrid();
        left.Controls.Add(grid, 0, 0);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0), BackColor = Color.White, WrapContents = false };
        toolbar.Controls.Add(Button("新增", AddMapping));
        toolbar.Controls.Add(Button("编辑", EditMapping));
        toolbar.Controls.Add(Button("删除", DeleteMapping));
        toolbar.Controls.Add(Button("测试触发", TestMapping));
        left.Controls.Add(toolbar, 0, 1);

        var rightScroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White,
            Padding = new Padding(0)
        };
        root.Panel2.Controls.Add(rightScroll);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            RowCount = 4,
            Padding = new Padding(10),
            BackColor = Color.White
        };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 840));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        rightScroll.Controls.Add(right);
        rightScroll.Resize += (_, _) =>
        {
            var target = Math.Max(260, rightScroll.ClientSize.Width - 20);
            if (Math.Abs(right.Width - target) > 8) right.Width = target;
        };
        right.Width = Math.Max(260, root.Panel2.ClientSize.Width - 20);
        right.Controls.Add(BuildSettings(), 0, 0);
        right.Controls.Add(BuildDevicePicker(), 0, 1);
        right.Controls.Add(BuildManualTest(), 0, 2);
        right.Controls.Add(BuildRecognizedBox(), 0, 3);
    }

    private Control BuildSettings()
    {
        var box = UiHelpers.Section("ASR + 意图 AI 设置", new Padding(10, 18, 10, 10), new Padding(0, 0, 0, 8));
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 22, BackColor = Color.White };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        for (var i = 0; i < 21; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        box.Controls.Add(table);

        AddRow(table, 0, "来源", source);
        AddRow(table, 1, "ASR 厂商", asrProvider);
        AddRow(table, 2, "ASR 模型", asrPreset);
        AddRow(table, 3, "ASR 地址", asrEndpoint);
        AddRow(table, 4, "ASR Key", asrKey);
        AddRow(table, 5, "模型 ID", asrModel);
        AddRow(table, 6, "LLM 厂商", aiProvider);
        AddRow(table, 7, "LLM 模型", aiPreset);
        AddRow(table, 8, "LLM 地址", endpoint);
        AddRow(table, 9, "LLM Key", apiKey);
        AddRow(table, 10, "模型 ID", model);
        AddRow(table, 11, "置信阈值", threshold);
        AddRow(table, 12, "VAD 阈值", vadThreshold);
        AddRow(table, 13, "前置缓存", vadPreRoll);
        AddRow(table, 14, "人声开始", vadVoiceStart);
        AddRow(table, 15, "静音结束", vadSilenceEnd);
        AddRow(table, 16, "最短句子", vadMinUtterance);
        AddRow(table, 17, "最大句子", vadMaxUtterance);
        AddFileRow(table, 18, "文字文件", textFile, BrowseTextFile);
        AddFolderRow(table, 19, "音频文件夹", audioFolder, BrowseAudioFolder);
        AddRow(table, 20, "轮询毫秒", pollMs);
        var switches = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = Color.White, Padding = new Padding(0, 4, 0, 0) };
        switches.Controls.Add(enabled);
        switches.Controls.Add(once);
        switches.Controls.Add(Button("推荐 ASR", ApplyRecommendedAsr));
        switches.Controls.Add(Button("推荐千问AI", ApplyRecommendedQwenAi));
        switches.Controls.Add(Button("保存设置", SaveSettings));
        switches.Controls.Add(Button("刷新设备", RefreshAudioDevices));
        table.Controls.Add(switches, 1, 21);
        table.SetColumnSpan(switches, 2);
        return box;
    }

    private Control BuildDevicePicker()
    {
        var box = UiHelpers.Section("音频来源选择（可多选）", new Padding(10, 18, 10, 10), new Padding(0, 0, 0, 8));
        var tabs = new TabControl { Dock = DockStyle.Fill };
        microphones.Dock = DockStyle.Fill;
        speakers.Dock = DockStyle.Fill;
        programs.Dock = DockStyle.Fill;
        tabs.TabPages.Add(SourcePage("麦克风输入", microphones));
        tabs.TabPages.Add(SourcePage("扬声器回环", speakers));
        tabs.TabPages.Add(SourcePage("程序声音", programs));
        box.Controls.Add(tabs);
        return box;
    }

    private Control BuildManualTest()
    {
        var box = UiHelpers.Section("手动文本模拟测试", new Padding(10, 18, 10, 8), new Padding(0, 0, 0, 8));
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.White };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        manualText.Dock = DockStyle.Fill;
        row.Controls.Add(manualText, 0, 0);
        row.Controls.Add(Button("AI 判断", async () => await ProcessTextWithAiAsync(manualText.Text)), 1, 0);
        box.Controls.Add(row);
        return box;
    }

    private Control BuildRecognizedBox()
    {
        var box = UiHelpers.Section("识别日志", new Padding(10, 18, 10, 10));
        box.Controls.Add(recognized);
        return box;
    }

    private static void ApplySplitRatio(SplitContainer split)
    {
        if (split.Width <= split.SplitterWidth + 260) return;
        var available = split.Width - split.SplitterWidth;
        var left = available * 5 / 8;
        var min = split.Panel1MinSize;
        var max = available - split.Panel2MinSize;
        if (max <= min) return;
        var target = Math.Clamp(left, min, max);
        if (Math.Abs(split.SplitterDistance - target) > 8)
        {
            split.SplitterDistance = target;
        }
    }

    private void ConfigureGrid()
    {
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 232, 252);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.White;
        grid.GridColor = Color.FromArgb(225, 225, 225);
        grid.Columns.Clear();
        grid.Columns.Add(TextColumn("备注", 140));
        grid.Columns.Add(TextColumn("控制意图", 230));
        grid.Columns.Add(TextColumn("判断方式", 104));
        grid.Columns.Add(TextColumn("参数名", 210));
        grid.Columns.Add(TextColumn("启用", 54));
        grid.Columns.Add(TextColumn("类型", 68));
        grid.Columns.Add(TextColumn("模式", 96));
        grid.Columns.Add(TextColumn("触发值", 82));
        grid.DoubleClick += (_, _) => EditMapping();
    }

    private void LoadSettings()
    {
        var s = RequireContext().Config.VoiceRecognition;
        enabled.Checked = s.Enabled;
        source.SelectedItem = s.SourceType == "音频设备" ? "音频设备/程序" : SourceTypes.Contains(s.SourceType) ? s.SourceType : "音频文件夹";
        var asr = ResolvePreset(AsrPresets, s.AsrProvider, s.AsrPreset, s.ApiEndpoint, s.Model) ?? AsrPresets[0];
        asrProvider.SelectedItem = asr.Provider;
        UpdateAsrModelChoices(false);
        asrPreset.SelectedItem = asr.Name;
        asrEndpoint.Text = s.ApiEndpoint;
        asrKey.Text = s.ApiKey;
        asrModel.Text = s.Model;
        var ai = ResolvePreset(AiPresets, s.AiProvider, s.AiPreset, s.AiEndpoint, s.AiModel) ?? AiPresets[0];
        aiProvider.SelectedItem = ai.Provider;
        UpdateAiModelChoices(false);
        aiPreset.SelectedItem = ai.Name;
        endpoint.Text = s.AiEndpoint;
        apiKey.Text = s.AiApiKey;
        model.Text = s.AiModel;
        threshold.Value = (decimal)Math.Clamp(s.AiThreshold, 0, 1);
        vadThreshold.Value = (decimal)Math.Clamp(s.VadThreshold, (double)vadThreshold.Minimum, (double)vadThreshold.Maximum);
        vadPreRoll.Value = Math.Clamp(s.VadPreRollMs, (int)vadPreRoll.Minimum, (int)vadPreRoll.Maximum);
        vadVoiceStart.Value = Math.Clamp(s.VadVoiceStartMs, (int)vadVoiceStart.Minimum, (int)vadVoiceStart.Maximum);
        vadSilenceEnd.Value = Math.Clamp(s.VadSilenceEndMs, (int)vadSilenceEnd.Minimum, (int)vadSilenceEnd.Maximum);
        vadMinUtterance.Value = Math.Clamp(s.VadMinUtteranceMs, (int)vadMinUtterance.Minimum, (int)vadMinUtterance.Maximum);
        vadMaxUtterance.Value = Math.Clamp(s.VadMaxUtteranceMs, (int)vadMaxUtterance.Minimum, (int)vadMaxUtterance.Maximum);
        textFile.Text = s.WatchTextFilePath;
        audioFolder.Text = s.WatchAudioFolderPath;
        pollMs.Value = Math.Clamp(s.PollMs, (int)pollMs.Minimum, (int)pollMs.Maximum);
        ignoreCase.Checked = s.IgnoreCase;
        once.Checked = s.TriggerOncePerRecognition;
        RefreshAudioDevices();
        CheckItems(microphones, s.MicrophoneDevices);
        CheckItems(speakers, s.SpeakerDevices);
        CheckItems(programs, s.ProgramSources);
    }

    private void SaveSettings()
    {
        var s = RequireContext().Config.VoiceRecognition;
        s.Enabled = enabled.Checked;
        s.SourceType = source.Text;
        s.AsrProvider = Convert.ToString(asrProvider.SelectedItem) ?? "自定义";
        s.AsrPreset = Convert.ToString(asrPreset.SelectedItem) ?? "自定义";
        s.ApiEndpoint = asrEndpoint.Text.Trim();
        s.ApiKey = asrKey.Text.Trim();
        s.Model = asrModel.Text.Trim();
        s.AiSemanticMatching = true;
        s.AiProvider = Convert.ToString(aiProvider.SelectedItem) ?? "自定义";
        s.AiPreset = Convert.ToString(aiPreset.SelectedItem) ?? "自定义";
        s.AiEndpoint = endpoint.Text.Trim();
        s.AiApiKey = apiKey.Text.Trim();
        s.AiModel = model.Text.Trim();
        s.AiThreshold = (double)threshold.Value;
        s.VadThreshold = (double)vadThreshold.Value;
        s.VadPreRollMs = (int)vadPreRoll.Value;
        s.VadVoiceStartMs = (int)vadVoiceStart.Value;
        s.VadSilenceEndMs = (int)vadSilenceEnd.Value;
        s.VadMinUtteranceMs = (int)vadMinUtterance.Value;
        s.VadMaxUtteranceMs = (int)vadMaxUtterance.Value;
        s.WatchTextFilePath = textFile.Text.Trim();
        s.WatchAudioFolderPath = audioFolder.Text.Trim();
        s.MicrophoneDevices = CheckedItems(microphones);
        s.SpeakerDevices = CheckedItems(speakers);
        s.ProgramSources = CheckedItems(programs);
        s.PollMs = (int)pollMs.Value;
        s.IgnoreCase = ignoreCase.Checked;
        s.TriggerOncePerRecognition = once.Checked;
        RequireContext().SaveConfig();
    }

    private void ApplyRecommendedAsr()
    {
        asrProvider.SelectedItem = "千问/百炼";
        UpdateAsrModelChoices(false);
        asrPreset.SelectedItem = "实时识别";
        ApplyAsrPreset(true);
        AppendRecognized("已填入推荐 ASR: DashScope 实时语音识别 WebSocket + paraformer-realtime-v2。");
    }

    private void ApplyRecommendedQwenAi()
    {
        aiProvider.SelectedItem = "千问/百炼";
        UpdateAiModelChoices(false);
        aiPreset.SelectedItem = "最强";
        ApplyAiPreset(true);
        AppendRecognized("已填入推荐 LLM: 千问兼容模式 + qwen-max-latest。");
    }

    private void ApplyAsrPreset(bool force)
    {
        var selected = FindPreset(AsrPresets, Convert.ToString(asrProvider.SelectedItem), Convert.ToString(asrPreset.SelectedItem));
        if (selected is null || selected.Provider == "自定义") return;
        if (!force && (!string.IsNullOrWhiteSpace(asrEndpoint.Text) || !string.IsNullOrWhiteSpace(asrModel.Text))) return;
        asrEndpoint.Text = selected.Endpoint;
        asrModel.Text = selected.Model;
    }

    private void ApplyAiPreset(bool force)
    {
        var selected = FindPreset(AiPresets, Convert.ToString(aiProvider.SelectedItem), Convert.ToString(aiPreset.SelectedItem));
        if (selected is null || selected.Provider == "自定义") return;
        if (!force && (!string.IsNullOrWhiteSpace(endpoint.Text) || !string.IsNullOrWhiteSpace(model.Text))) return;
        endpoint.Text = selected.Endpoint;
        model.Text = selected.Model;
    }

    private void UpdateAsrModelChoices(bool applyFirst)
    {
        UpdateModelChoices(asrPreset, AsrPresets, Convert.ToString(asrProvider.SelectedItem), applyFirst);
        if (applyFirst) ApplyAsrPreset(true);
    }

    private void UpdateAiModelChoices(bool applyFirst)
    {
        UpdateModelChoices(aiPreset, AiPresets, Convert.ToString(aiProvider.SelectedItem), applyFirst);
        if (applyFirst) ApplyAiPreset(true);
    }

    private static void UpdateModelChoices(ComboBox combo, IEnumerable<ProviderModelPreset> presets, string? provider, bool selectFirst)
    {
        var current = Convert.ToString(combo.SelectedItem);
        combo.Items.Clear();
        combo.Items.AddRange(presets.Where(p => p.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase)).Select(p => p.Name).ToArray());
        if (!string.IsNullOrWhiteSpace(current) && combo.Items.Contains(current)) combo.SelectedItem = current;
        else if (selectFirst && combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static ProviderModelPreset? FindPreset(IEnumerable<ProviderModelPreset> presets, string? provider, string? name)
    {
        return presets.FirstOrDefault(p => p.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase) && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static ProviderModelPreset? ResolvePreset(IEnumerable<ProviderModelPreset> presets, string provider, string preset, string endpoint, string modelId)
    {
        var direct = FindPreset(presets, provider, preset);
        if (direct is not null) return direct;
        var legacy = presets.FirstOrDefault(p => (p.Provider + " - " + p.Name).Equals(preset, StringComparison.OrdinalIgnoreCase));
        if (legacy is not null) return legacy;
        return presets.FirstOrDefault(p => p.Model.Equals(modelId, StringComparison.OrdinalIgnoreCase) && p.Endpoint.Equals(endpoint, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshGrid()
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        foreach (var mapping in RequireContext().Config.VoiceMappings)
        {
            var i = grid.Rows.Add(mapping.Note, DisplayIntent(mapping), mapping.MatchMode, mapping.Parameter, mapping.Enabled ? "是" : "否", mapping.Type, mapping.Mode, mapping.Value);
            grid.Rows[i].Tag = mapping;
        }
        grid.ResumeLayout();
    }

    private VoiceMapping? SelectedMapping() => grid.CurrentRow?.Tag as VoiceMapping;

    private void AddMapping()
    {
        using var editor = new VoiceMappingEditorForm(null, MatchModes);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is not null)
        {
            RequireContext().Config.VoiceMappings.Add(editor.Result);
            RequireContext().SaveConfig();
            RefreshGrid();
        }
    }

    private void EditMapping()
    {
        var selected = SelectedMapping();
        if (selected is null)
        {
            MessageBox.Show(this, "请先选择一条语音映射。", "未选择", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var editor = new VoiceMappingEditorForm(selected, MatchModes);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result is not null)
        {
            var index = RequireContext().Config.VoiceMappings.FindIndex(m => m.Id == selected.Id);
            RequireContext().Config.VoiceMappings[index] = editor.Result;
            RequireContext().SaveConfig();
            RefreshGrid();
        }
    }

    private void DeleteMapping()
    {
        var selected = SelectedMapping();
        if (selected is null) return;
        if (MessageBox.Show(this, $"删除语音映射 {DisplayIntent(selected)} -> {selected.Parameter}？", "删除映射", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        RequireContext().Config.VoiceMappings.RemoveAll(m => m.Id == selected.Id);
        RequireContext().SaveConfig();
        RefreshGrid();
    }

    private void TestMapping()
    {
        var selected = SelectedMapping();
        if (selected is null) return;
        RunMapping(selected);
    }

    private async Task ProcessTextWithAiAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        AppendRecognized("文本模拟: " + text);
        var match = await DecideTextIntentAsync(text);
        if (match is null)
        {
            AppendRecognized("AI 判断: 不触发。");
            return;
        }
        RunMapping(match);
    }

    private void RunMapping(VoiceMapping mapping)
    {
        var action = mapping.ToKeyboardMapping();
        engine?.Run(action);
        mapping.PullStateFrom(action);
        AppendRecognized("触发: " + DisplayIntent(mapping) + " -> " + mapping.Parameter);
    }

    private bool IsMatch(string text, VoiceMapping mapping)
    {
        var comparison = ignoreCase.Checked ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var keywords = SplitKeywords(mapping.Keywords);
        if (keywords.Length == 0) return false;
        return mapping.MatchMode switch
        {
            "包含全部关键词" => keywords.All(k => text.Contains(k, comparison)),
            "完全等于" => keywords.Any(k => string.Equals(text.Trim(), k, comparison)),
            "正则表达式" => keywords.Any(k => System.Text.RegularExpressions.Regex.IsMatch(text, k, ignoreCase.Checked ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None)),
            _ => keywords.Any(k => text.Contains(k, comparison))
        };
    }

    private static string[] SplitKeywords(string text) => text
        .Split(new[] { '\r', '\n', ',', '，', ';', '；', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToArray();

    private static string DisplayIntent(VoiceMapping mapping)
    {
        return string.IsNullOrWhiteSpace(mapping.Intent) ? mapping.Keywords : mapping.Intent;
    }

    private void StartTextWatcher()
    {
        if (!enabled.Checked) return;
        if (string.IsNullOrWhiteSpace(textFile.Text) || !File.Exists(textFile.Text))
        {
            RequireContext().Log("文字文件不存在，语音模块未能监听。");
            return;
        }
        lastTextLength = new FileInfo(textFile.Text).Length;
        textWatcher = new FileSystemWatcher(Path.GetDirectoryName(textFile.Text)!, Path.GetFileName(textFile.Text))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        textWatcher.Changed += (_, _) => BeginInvoke((Action)ReadTextFileAppend);
        RequireContext().Log("正在监听文字文件: " + textFile.Text);
    }

    private void ReadTextFileAppend()
    {
        try
        {
            using var fs = new FileStream(textFile.Text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < lastTextLength) lastTextLength = 0;
            fs.Position = lastTextLength;
            using var reader = new StreamReader(fs);
            var added = reader.ReadToEnd();
            lastTextLength = fs.Length;
            foreach (var line in added.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                _ = ProcessTextWithAiAsync(line);
            }
        }
        catch (Exception ex)
        {
            AppendRecognized("读取文字文件失败: " + ex.Message);
        }
    }

    private void StartAudioWatcher()
    {
        if (!enabled.Checked) return;
        if (string.IsNullOrWhiteSpace(apiKey.Text))
        {
            RequireContext().Log("请先填写语音识别 API Key。");
            return;
        }
        if (string.IsNullOrWhiteSpace(audioFolder.Text) || !Directory.Exists(audioFolder.Text))
        {
            RequireContext().Log("音频文件夹不存在，语音模块未能监听。");
            return;
        }
        audioWatcher = new FileSystemWatcher(audioFolder.Text)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };
        audioWatcher.Created += async (_, e) => await RecognizeAudioIfNeededAsync(e.FullPath);
        pollTimer = new System.Windows.Forms.Timer { Interval = (int)pollMs.Value };
        pollTimer.Tick += async (_, _) => await PollAudioFolderOnceAsync();
        pollTimer.Start();
        RequireContext().Log("正在监听音频文件夹: " + audioFolder.Text);
    }

    private void StartDeviceCapturePlaceholder()
    {
        var inputs = CheckedItems(microphones);
        var outputs = CheckedItems(speakers);
        var apps = CheckedItems(programs);
        if (inputs.Count == 0 && outputs.Count == 0 && apps.Count == 0)
        {
            RequireContext().Log("请选择至少一个麦克风、扬声器或程序来源。");
            return;
        }
        captureSession = new AudioCaptureSession(RecognizeAudioIfNeededAsync, AppendRecognized, CurrentVadOptions());
        var started = captureSession.Start(inputs, outputs);
        AppendRecognized("已启动音频监听: 麦克风 " + inputs.Count + " 个，扬声器 " + outputs.Count + " 个。");
        if (apps.Count > 0)
        {
            AppendRecognized("已保存程序来源 " + apps.Count + " 个；当前版本尚未启用单独程序音频捕获，程序音频可先通过对应扬声器回环监听。");
        }
        RequireContext().Log("音频设备监听已启动: " + started + " 个来源。");
    }

    private VadOptions CurrentVadOptions() => new()
    {
        Threshold = (double)vadThreshold.Value,
        PreRollMs = (int)vadPreRoll.Value,
        VoiceStartMs = (int)vadVoiceStart.Value,
        SilenceEndMs = (int)vadSilenceEnd.Value,
        MinUtteranceMs = (int)vadMinUtterance.Value,
        MaxUtteranceMs = (int)vadMaxUtterance.Value
    };

    private void RefreshAudioDevices()
    {
        var oldInputs = CheckedItems(microphones);
        var oldOutputs = CheckedItems(speakers);
        var oldPrograms = CheckedItems(programs);
        microphones.Items.Clear();
        speakers.Items.Clear();
        programs.Items.Clear();
        microphones.Items.AddRange(AudioSourceHelper.GetInputDevices());
        speakers.Items.AddRange(AudioSourceHelper.GetOutputDevices());
        programs.Items.AddRange(AudioSourceHelper.GetAudioProcesses());
        AddEmptyHint(microphones, "未发现麦克风输入设备");
        AddEmptyHint(speakers, "未发现扬声器输出设备");
        AddEmptyHint(programs, "未发现正在运行的程序");
        CheckItems(microphones, oldInputs);
        CheckItems(speakers, oldOutputs);
        CheckItems(programs, oldPrograms);
    }

    private async Task PollAudioFolderOnceAsync()
    {
        if (!Directory.Exists(audioFolder.Text)) return;
        var files = Directory.EnumerateFiles(audioFolder.Text).Where(IsAudioFile).OrderBy(File.GetLastWriteTimeUtc).ToList();
        foreach (var file in files)
        {
            if (processedAudio.Contains(file)) continue;
            await RecognizeAudioIfNeededAsync(file);
            break;
        }
    }

    private async Task RecognizeAudioIfNeededAsync(string path)
    {
        if (!IsAudioFile(path) || !processedAudio.Add(path)) return;
        try
        {
            AppendRecognized("完整句子送 ASR: " + Path.GetFileName(path));
            var text = await TranscribeFinalTextAsync(path);
            AppendRecognized("最终识别: " + text);
            var match = await DecideTextIntentAsync(text);
            if (match is null)
            {
                AppendRecognized("LLM 判断: 不触发。");
                return;
            }
            if (InvokeRequired) BeginInvoke((Action)(() => RunMapping(match)));
            else RunMapping(match);
        }
        catch (Exception ex)
        {
            AppendRecognized("语音流程失败: " + Path.GetFileName(path) + " " + ex.Message);
        }
    }

    private async Task<string> TranscribeFinalTextAsync(string audioPath)
    {
        if (string.IsNullOrWhiteSpace(asrKey.Text)) throw new InvalidOperationException("请先填写 ASR API Key。");
        var endpointText = asrEndpoint.Text.Trim();
        if (string.IsNullOrWhiteSpace(endpointText)) endpointText = RecommendedAsrEndpoint;
        if (endpointText.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ASR 地址不能填写 chat/completions。千问该端点不支持 audio_url 音频字段，请填写真正的语音识别/ASR 文件转写接口。");
        }
        if (!endpointText.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return await TranscribeByMultipartAsync(audioPath, endpointText, modelName: asrModel.Text.Trim());
        }
        var modelName = asrModel.Text.Trim();
        if (string.IsNullOrWhiteSpace(modelName)) modelName = RecommendedAsrModel;
        return await TranscribeByDashScopeRealtimeWebSocketAsync(audioPath, endpointText, modelName);
    }

    private async Task<string> TranscribeByMultipartAsync(string audioPath, string endpointText, string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) throw new InvalidOperationException("请先填写 ASR 模型。");
        AppendRecognized("请求 ASR 文件转写: " + endpointText);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpointText);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", asrKey.Text.Trim());
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(modelName), "model");
        var file = new StreamContent(File.OpenRead(audioPath));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        content.Add(file, "file", Path.GetFileName(audioPath));
        request.Content = content;
        using var response = await http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        AppendRecognized("ASR 文件转写返回: HTTP " + (int)response.StatusCode);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("ASR 返回错误: " + (int)response.StatusCode + " " + json);
        return ExtractAsrText(json);
    }

    private async Task<string> TranscribeByDashScopeRealtimeWebSocketAsync(string audioPath, string endpointText, string modelName)
    {
        AppendRecognized("请求 ASR 实时 WebSocket: " + endpointText);
        using var socket = new System.Net.WebSockets.ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "bearer " + asrKey.Text.Trim());
        socket.Options.SetRequestHeader("X-DashScope-DataInspection", "enable");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(70));
        await socket.ConnectAsync(new Uri(endpointText), timeout.Token);

        var taskId = Guid.NewGuid().ToString("N");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalTexts = new List<string>();

        var receiveTask = Task.Run(async () =>
        {
            var buffer = new byte[64 * 1024];
            var message = new MemoryStream();
            try
            {
                while (socket.State == System.Net.WebSockets.WebSocketState.Open && !timeout.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(buffer, timeout.Token);
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) break;
                    message.Write(buffer, 0, result.Count);
                    if (!result.EndOfMessage) continue;

                    var payload = System.Text.Encoding.UTF8.GetString(message.ToArray());
                    message.SetLength(0);
                    HandleDashScopeRealtimeEvent(payload, started, finished, finalTexts);
                    if (finished.Task.IsCompleted) break;
                }
            }
            catch (Exception ex)
            {
                started.TrySetException(ex);
                finished.TrySetException(ex);
            }
            finally
            {
                message.Dispose();
            }
        }, timeout.Token);

        var runTask = new
        {
            header = new { action = "run-task", task_id = taskId, streaming = "duplex" },
            payload = new
            {
                task_group = "audio",
                task = "asr",
                function = "recognition",
                model = modelName,
                parameters = new
                {
                    format = "pcm",
                    sample_rate = 16000,
                    language_hints = new[] { "zh", "en", "ja", "ko" },
                    punctuation_prediction_enabled = true,
                    inverse_text_normalization_enabled = true,
                    disfluency_removal_enabled = false
                },
                input = new { }
            }
        };
        await SendWebSocketJsonAsync(socket, runTask, timeout.Token);
        await started.Task.WaitAsync(timeout.Token);

        var pcm = ReadPcmDataFromWave(audioPath);
        for (var offset = 0; offset < pcm.Length; offset += 3200)
        {
            var count = Math.Min(3200, pcm.Length - offset);
            await socket.SendAsync(pcm.AsMemory(offset, count), System.Net.WebSockets.WebSocketMessageType.Binary, true, timeout.Token);
            await Task.Delay(20, timeout.Token);
        }

        var finishTask = new { header = new { action = "finish-task", task_id = taskId, streaming = "duplex" }, payload = new { input = new { } } };
        await SendWebSocketJsonAsync(socket, finishTask, timeout.Token);

        var text = await finished.Task.WaitAsync(timeout.Token);
        try
        {
            if (socket.State == System.Net.WebSockets.WebSocketState.Open)
            {
                await socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
            }
        }
        catch
        {
            // Closing is best-effort after the final ASR result has been received.
        }
        await receiveTask.WaitAsync(TimeSpan.FromSeconds(2)).ContinueWith(_ => { });
        AppendRecognized("ASR 最终文本: " + text);
        return text;
    }

    private static async Task SendWebSocketJsonAsync(System.Net.WebSockets.ClientWebSocket socket, object value, CancellationToken cancellationToken)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, System.Net.WebSockets.WebSocketMessageType.Text, true, cancellationToken);
    }

    private void HandleDashScopeRealtimeEvent(string json, TaskCompletionSource started, TaskCompletionSource<string> finished, List<string> finalTexts)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var eventName = "";
        if (root.TryGetProperty("header", out var header))
        {
            if (TryGetString(header, "event", out var ev)) eventName = ev;
            if (TryGetString(header, "error_message", out var errorMessage))
            {
                finished.TrySetException(new InvalidOperationException("ASR WebSocket 错误: " + errorMessage));
                return;
            }
        }

        if (eventName.Equals("task-started", StringComparison.OrdinalIgnoreCase))
        {
            AppendRecognized("ASR WebSocket 已开始接收音频。");
            started.TrySetResult();
            return;
        }
        if (eventName.Equals("result-generated", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetDashScopeSentence(root, out var text, out var isFinal) && !string.IsNullOrWhiteSpace(text))
            {
                AppendRecognized((isFinal ? "ASR 分句完成: " : "ASR 临时文本: ") + text);
                if (isFinal && (finalTexts.Count == 0 || finalTexts[^1] != text)) finalTexts.Add(text);
            }
            return;
        }
        if (eventName.Equals("task-finished", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetDashScopeSentence(root, out var text, out _) && !string.IsNullOrWhiteSpace(text)
                && (finalTexts.Count == 0 || finalTexts[^1] != text))
            {
                finalTexts.Add(text);
            }
            finished.TrySetResult(string.Join("", finalTexts).Trim());
            return;
        }
        if (eventName.Equals("task-failed", StringComparison.OrdinalIgnoreCase))
        {
            finished.TrySetException(new InvalidOperationException("ASR WebSocket 任务失败: " + json));
        }
    }

    private static bool TryGetDashScopeSentence(System.Text.Json.JsonElement root, out string text, out bool isFinal)
    {
        text = "";
        isFinal = false;
        if (!root.TryGetProperty("payload", out var payload)) return false;
        if (payload.TryGetProperty("output", out var output)) payload = output;
        if (!payload.TryGetProperty("sentence", out var sentence)) return false;
        if (!TryGetString(sentence, "text", out text)) return false;
        isFinal = sentence.TryGetProperty("end_time", out var endTime) && endTime.ValueKind != System.Text.Json.JsonValueKind.Null;
        return true;
    }

    private static byte[] ReadPcmDataFromWave(string path)
    {
        var data = File.ReadAllBytes(path);
        for (var i = 12; i <= data.Length - 8; i++)
        {
            if (data[i] != (byte)'d' || data[i + 1] != (byte)'a' || data[i + 2] != (byte)'t' || data[i + 3] != (byte)'a') continue;
            var length = BitConverter.ToInt32(data, i + 4);
            var start = i + 8;
            if (length <= 0 || start + length > data.Length) break;
            var pcm = new byte[length];
            Buffer.BlockCopy(data, start, pcm, 0, length);
            return pcm;
        }
        throw new InvalidOperationException("无法从录音文件读取 PCM 数据。");
    }


    private static string BuildIntentPrompt(IReadOnlyList<VoiceMapping> mappings)
    {
        var items = mappings.Select(m => new { intent = m.Id, meaning = DisplayIntent(m), note = m.Note });
        return "你是 VRChat OSC 参数控制的意图分类器。输入是 ASR 得到的最终识别文本，不是临时识别结果。" +
               "请判断文本是否符合下面任意一个控制意图。只允许返回 JSON，格式必须为 {\"intent\":\"映射 intent 或 none\",\"confidence\":0到1,\"arguments\":{}}。" +
               "如果只是闲聊、噪声、含义不明确、或没有足够相似的控制意图，intent 必须为 none。控制意图列表：" +
               System.Text.Json.JsonSerializer.Serialize(items);
    }

    private async Task<VoiceMapping?> DecideTextIntentAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(apiKey.Text))
        {
            AppendRecognized("请先填写音频理解 AI 的 API Key。");
            return null;
        }
        var candidates = RequireContext().Config.VoiceMappings.Where(m => m.Enabled).ToList();
        if (candidates.Count == 0) return null;

        var payload = new
        {
            model = model.Text.Trim(),
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = BuildIntentPrompt(candidates) + "\n\n最终识别文本：\n" + text
                }
            },
            temperature = 0,
            response_format = new { type = "json_object" }
        };
        return await RequestIntentDecisionAsync(payload, candidates);
    }

    private async Task<VoiceMapping?> RequestIntentDecisionAsync(object payload, List<VoiceMapping> candidates)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Text.Trim());
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Text.Trim());
        request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException((int)response.StatusCode + " " + json);
        var decision = ExtractDecisionJson(json);
        AppendRecognized("AI 返回: " + decision);
        using var doc = System.Text.Json.JsonDocument.Parse(decision);
        var root = doc.RootElement;
        var id = root.TryGetProperty("intent", out var idElement) ? idElement.GetString() : "";
        if (string.IsNullOrWhiteSpace(id) || id.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
        var confidence = root.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var value) ? value : 0;
        if (confidence < (double)threshold.Value) return null;
        return candidates.FirstOrDefault(m => m.Id == id);
    }

    private static string ExtractDecisionJson(string apiJson)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(apiJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == System.Text.Json.JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (TryGetString(first, "message", "content", out var content)) return StripCodeFence(content);
            if (TryGetString(first, "text", out content)) return StripCodeFence(content);
        }
        if (root.TryGetProperty("intent", out _)) return apiJson;
        if (TryGetString(root, "output", "text", out var text)) return StripCodeFence(text);
        return apiJson;
    }

    private static string ExtractAsrText(string apiJson)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(apiJson);
        var root = doc.RootElement;
        if (TryGetString(root, "text", out var text)) return text;
        if (TryGetString(root, "output", "text", out text)) return text;
        if (TryGetString(root, "result", "text", out text)) return text;
        if (root.TryGetProperty("choices", out var choices)
            && choices.ValueKind == System.Text.Json.JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (TryGetString(first, "text", out text)) return text;
            if (TryGetString(first, "message", "content", out text)) return text;
        }
        throw new InvalidOperationException("无法从 ASR 响应中解析最终文本: " + apiJson);
    }

    private void StopWatchers()
    {
        textWatcher?.Dispose();
        textWatcher = null;
        audioWatcher?.Dispose();
        audioWatcher = null;
        captureSession?.Dispose();
        captureSession = null;
        pollTimer?.Stop();
        pollTimer?.Dispose();
        pollTimer = null;
    }

    private void BrowseTextFile()
    {
        using var dialog = new OpenFileDialog { Filter = "Text files|*.txt;*.log;*.jsonl|All files|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) textFile.Text = dialog.FileName;
    }

    private void BrowseAudioFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK) audioFolder.Text = dialog.SelectedPath;
    }

    private void AppendRecognized(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke((Action)(() => AppendRecognized(text)));
            return;
        }
        UiHelpers.AppendLimited(recognized, text);
    }

    private static bool IsAudioFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".wav" or ".mp3" or ".m4a" or ".ogg" or ".flac" or ".webm";

    private static bool TryGetString(System.Text.Json.JsonElement element, string property, out string value)
    {
        value = "";
        if (element.TryGetProperty(property, out var found) && found.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            value = found.GetString() ?? "";
            return !string.IsNullOrWhiteSpace(value);
        }
        return false;
    }

    private static bool TryGetString(System.Text.Json.JsonElement element, string first, string second, out string value)
    {
        value = "";
        return element.TryGetProperty(first, out var nested) && TryGetString(nested, second, out value);
    }

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        var firstLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstLine >= 0 && lastFence > firstLine) return trimmed.Substring(firstLine + 1, lastFence - firstLine - 1).Trim();
        return trimmed;
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 78, Height = 28, Margin = new Padding(0, 0, 6, 4) };
        button.Click += (_, _) => action();
        return button;
    }

    private static DataGridViewTextBoxColumn TextColumn(string header, int width) => new()
    {
        HeaderText = header,
        Width = width,
        SortMode = DataGridViewColumnSortMode.NotSortable
    };

    private static TabPage SourcePage(string title, Control list)
    {
        var page = new TabPage(title) { BackColor = Color.White, Padding = new Padding(6) };
        page.Controls.Add(list);
        return page;
    }

    private static List<string> CheckedItems(CheckedListBox list)
    {
        return list.CheckedItems.Cast<object>().Select(x => Convert.ToString(x) ?? "").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    private static void CheckItems(CheckedListBox list, IEnumerable<string> values)
    {
        var set = values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < list.Items.Count; i++)
        {
            var text = Convert.ToString(list.Items[i]) ?? "";
            list.SetItemChecked(i, set.Contains(text));
        }
    }

    private static void AddEmptyHint(CheckedListBox list, string text)
    {
        if (list.Items.Count == 0) list.Items.Add(text);
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 2, 8, 2);
        table.Controls.Add(control, 1, row);
        table.SetColumnSpan(control, 2);
    }

    private static void AddFileRow(TableLayoutPanel table, int row, string label, TextBox text, Action browse)
    {
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White }, 0, row);
        text.Dock = DockStyle.Fill;
        text.Margin = new Padding(0, 2, 8, 2);
        table.Controls.Add(text, 1, row);
        table.Controls.Add(Button("选择", browse), 2, row);
    }

    private static void AddFolderRow(TableLayoutPanel table, int row, string label, TextBox text, Action browse) => AddFileRow(table, row, label, text, browse);

    private ModuleContext RequireContext() => context ?? throw new InvalidOperationException("Module is not initialized.");
}

public sealed class VoiceMappingEditorForm : Form
{
    private readonly CheckBox enabled = new() { Text = "启用", AutoSize = true };
    private readonly TextBox note = new();
    private readonly TextBox intent = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox keywords = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox matchMode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox parameter = new();
    private readonly ComboBox type = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox value = new();
    private readonly TextBox offValue = new();
    private readonly NumericUpDown step = new() { DecimalPlaces = 3, Increment = 0.01m, Minimum = -9999, Maximum = 9999 };
    private readonly NumericUpDown min = new() { DecimalPlaces = 3, Increment = 0.01m, Minimum = -9999, Maximum = 9999 };
    private readonly NumericUpDown max = new() { DecimalPlaces = 3, Increment = 0.01m, Minimum = -9999, Maximum = 9999 };
    private readonly NumericUpDown duration = new() { Minimum = 1, Maximum = 60000, Value = 250 };
    private List<ParameterAction> actions = new();

    public VoiceMapping? Result { get; private set; }

    public VoiceMappingEditorForm(VoiceMapping? mapping, string[] matchModes)
    {
        Text = "编辑语音映射";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 690);
        BackColor = Color.White;

        type.Items.AddRange(UiText.ParameterTypes);
        mode.Items.AddRange(UiText.ControlModes);
        matchMode.Items.AddRange(matchModes);

        var current = mapping?.Clone() ?? new VoiceMapping();
        enabled.Checked = current.Enabled;
        note.Text = current.Note;
        intent.Text = string.IsNullOrWhiteSpace(current.Intent) ? current.Keywords : current.Intent;
        keywords.Text = current.Keywords;
        matchMode.SelectedItem = matchModes.Contains(current.MatchMode) ? current.MatchMode : matchModes[0];
        parameter.Text = current.Parameter;
        type.SelectedItem = current.Type;
        mode.SelectedItem = current.Mode;
        value.Text = current.Value;
        offValue.Text = current.OffValue;
        step.Value = ToDecimal(current.Step);
        min.Value = ToDecimal(current.Min);
        max.Value = ToDecimal(current.Max);
        duration.Value = current.DurationMs;
        actions = current.ToKeyboardMapping().EffectiveActions().Select(a => a.Clone()).ToList();

        BuildLayout(current);
        UiHelpers.ApplyControlStyle(this);
    }

    private void BuildLayout(VoiceMapping current)
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.White };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        Controls.Add(root);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22, 18, 22, 8), ColumnCount = 2, RowCount = 15, BackColor = Color.White };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(table, 0, 0);
        table.Controls.Add(enabled, 1, 0);
        AddRow(table, 1, "备注", note, 34);
        AddRow(table, 2, "控制意图", intent, 86);
        AddRow(table, 3, "关键词兜底", keywords, 52);
        AddRow(table, 4, "判断方式", matchMode, 34);
        AddRow(table, 5, "多参数动作", ActionButton(), 38);
        AddRow(table, 6, "参数名", parameter, 34);
        AddRow(table, 7, "参数类型", type, 34);
        AddRow(table, 8, "控制模式", mode, 34);
        AddRow(table, 9, "触发值", value, 34);
        AddRow(table, 10, "恢复值", offValue, 34);
        AddRow(table, 11, "步进", step, 34);
        AddRow(table, 12, "最小值", min, 34);
        AddRow(table, 13, "最大值", max, 34);
        AddRow(table, 14, "脉冲毫秒", duration, 34);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(14), BackColor = Color.White };
        var save = new Button { Text = "保存", DialogResult = DialogResult.OK, Width = 88, Height = 30, Margin = new Padding(6, 0, 0, 0) };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 88, Height = 30, Margin = new Padding(6, 0, 0, 0) };
        save.Click += (_, _) =>
        {
            if (!ValidateMapping())
            {
                DialogResult = DialogResult.None;
                return;
            }
            Result = new VoiceMapping
            {
                Id = current.Id,
                Enabled = enabled.Checked,
                Note = note.Text.Trim(),
                Intent = intent.Text.Trim(),
                Keywords = keywords.Text.Trim(),
                MatchMode = Convert.ToString(matchMode.SelectedItem) ?? "AI 语义判断",
                Parameter = parameter.Text.Trim(),
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
        root.Controls.Add(buttons, 0, 1);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private bool ValidateMapping()
    {
        if (string.IsNullOrWhiteSpace(intent.Text) || string.IsNullOrWhiteSpace(parameter.Text))
        {
            MessageBox.Show(this, "控制意图和参数名不能为空。", "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            MessageBox.Show(this, "触发值或恢复值无法转换: " + ex.Message, "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private Button ActionButton()
    {
        var button = new Button { Text = "编辑动作列表 (" + actions.Count + ")", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
        button.Click += (_, _) =>
        {
            SyncPrimaryAction();
            using var editor = new ActionListEditorForm(actions);
            if (editor.ShowDialog(this) == DialogResult.OK)
            {
                actions = editor.Result.Select(a => a.Clone()).ToList();
                button.Text = "编辑动作列表 (" + actions.Count + ")";
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

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control, int height)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 3, 0, 3);
        table.Controls.Add(control, 1, row);
    }

    private static decimal ToDecimal(double value)
    {
        var clamped = Math.Clamp(value, -9999, 9999);
        return decimal.Round((decimal)clamped, 3);
    }
}

public sealed record ProviderModelPreset(string Provider, string Name, string Endpoint, string Model, string Description);
