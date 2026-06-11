namespace OSCC;

public sealed class AiTranslationModule : UserControl, IOsccModule
{
    private static readonly ProviderModelPreset[] TranslationAsrPresets =
    {
        new("Qwen/DashScope", "Realtime ASR", "wss://dashscope.aliyuncs.com/api-ws/v1/inference", "paraformer-realtime-v2", "Realtime speech input"),
        new("OpenAI", "GPT-4o Transcribe", "https://api.openai.com/v1/audio/transcriptions", "gpt-4o-transcribe", "High quality transcription"),
        new("OpenAI", "GPT-4o Mini Transcribe", "https://api.openai.com/v1/audio/transcriptions", "gpt-4o-mini-transcribe", "Fast transcription"),
        new("OpenAI", "Whisper", "https://api.openai.com/v1/audio/transcriptions", "whisper-1", "Compatible transcription"),
        new("Doubao/Volcengine", "Audio Transcription", "https://ark.cn-beijing.volces.com/api/v3/audio/transcriptions", "doubao-asr", "Adjust model id in console if needed"),
        new("Custom", "Custom", "", "", "Manual input")
    };

    private static readonly ProviderModelPreset[] AiPresets =
    {
        new("Qwen/DashScope", "Max", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen-max-latest", "Best quality"),
        new("Qwen/DashScope", "Plus", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen-plus-latest", "Balanced"),
        new("Qwen/DashScope", "Turbo", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen-turbo-latest", "Fast"),
        new("OpenAI", "GPT-4.1", "https://api.openai.com/v1/chat/completions", "gpt-4.1", "Strong translation"),
        new("OpenAI", "GPT-4.1 Mini", "https://api.openai.com/v1/chat/completions", "gpt-4.1-mini", "Fast translation"),
        new("Doubao/Volcengine", "Pro", "https://ark.cn-beijing.volces.com/api/v3/chat/completions", "doubao-1-5-pro-32k", "Chinese friendly"),
        new("Doubao/Volcengine", "Lite", "https://ark.cn-beijing.volces.com/api/v3/chat/completions", "doubao-1-5-lite-32k", "Low cost"),
        new("DeepSeek", "V4 Flash", "https://api.deepseek.com/chat/completions", "deepseek-v4-flash", "Fast"),
        new("Custom", "Custom", "", "", "Manual input")
    };

    private static readonly LanguageOption[] TargetLanguages =
    {
        new("涓枃", "Chinese (Simplified)"),
        new("鑻辨枃", "English"),
        new("鏃ユ枃", "Japanese"),
        new("闊╂枃", "Korean"),
        new("娉曟枃", "French"),
        new("寰锋枃", "German"),
        new("瑗跨彮鐗欐枃", "Spanish")
    };

    private static readonly OutputOption[] OutputTargets =
    {
        new("Chatbox", "chatbox"),
        new("SteamVR 鎺屽績瀛楀箷", "steamvr"),
        new("涓よ€呭悓鏃?, "both")
    };

    private readonly ComboBox aiProvider = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox aiPreset = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox aiEndpoint = new();
    private readonly TextBox aiKey = new() { UseSystemPasswordChar = true };
    private readonly TextBox aiModel = new();
    private readonly TranslationProfileUi others = new("缈昏瘧鍒汉鐨勫０闊?);
    private readonly TranslationProfileUi self = new("缈昏瘧鑷繁鐨勫０闊?);
    private readonly TextBox logBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.None };
    private readonly SplitContainer translationSplit = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, BackColor = Color.White, Panel1MinSize = 260, Panel2MinSize = 0 };
    private readonly Button translationLogToggle = new() { Text = "鏄剧ず缈昏瘧鏃ュ織", Width = 116, Height = 28, Margin = new Padding(0, 0, 6, 4) };
    private readonly HttpClient http = new();
    private readonly List<AudioCaptureSession> captures = new();
    private SpeechAiServices? speech;
    private ModuleContext? context;
    private bool isRunning;
    private bool translationLogVisible;

    public string Id => "ai-translation";
    public string DisplayName => "AI 缈昏瘧";
    public bool IsRunning => isRunning;
    public Control View => this;

    public AiTranslationModule()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        aiProvider.Items.AddRange(AiPresets.Select(p => p.Provider).Distinct().Cast<object>().ToArray());
        aiProvider.SelectedIndexChanged += (_, _) => UpdateModelChoices(aiPreset, AiPresets, Convert.ToString(aiProvider.SelectedItem), true, ApplyAiPreset);
        aiPreset.SelectedIndexChanged += (_, _) => ApplyAiPreset();
        others.InitializePresetControls(TranslationAsrPresets);
        self.InitializePresetControls(TranslationAsrPresets);
        BuildUi();
    }

    public void Initialize(ModuleContext context)
    {
        this.context = context;
        speech = new SpeechAiServices(http, AppendLog);
        LoadSettings();
    }

    public void Start()
    {
        SaveSettings();
        Stop();
        var cfg = RequireContext().Config.AiTranslation;
        var count = 0;
        count += StartProfile("缈昏瘧鍒汉", cfg.Others);
        count += StartProfile("缈昏瘧鑷繁", cfg.Self);
        isRunning = count > 0;
        RequireContext().Log(isRunning ? "AI 缈昏瘧妯″潡宸插惎鍔紝鏉ユ簮鏁? " + count : "AI 缈昏瘧妯″潡娌℃湁鍙洃鍚潵婧愩€?);
    }

    public void Stop()
    {
        foreach (var capture in captures) capture.Dispose();
        captures.Clear();
        isRunning = false;
    }

    public bool HandleMessage(ref Message message) => false;

    public new void Dispose()
    {
        Stop();
        http.Dispose();
        base.Dispose();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.White, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        Controls.Add(root);

        translationSplit.Panel1.Controls.Add(BuildMainPanel());
        var logGroup = UiHelpers.Section("缈昏瘧鏃ュ織", new Padding(10, 18, 10, 10));
        logGroup.Controls.Add(logBox);
        translationSplit.Panel2.Controls.Add(logGroup);
        translationSplit.HandleCreated += (_, _) => BeginInvoke((Action)ApplyTranslationLogVisibility);
        translationSplit.Resize += (_, _) => ApplyTranslationLogVisibility();
        root.Controls.Add(translationSplit, 0, 0);
        root.Controls.Add(BuildFooterBar(), 0, 1);
    }

    private Control BuildMainPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.White };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(BuildLlmPanel(), 0, 0);
        panel.Controls.Add(BuildProfiles(), 0, 1);
        return panel;
    }

    private Control BuildLlmPanel()
    {
        var box = UiHelpers.Section("缈昏瘧 LLM 璁剧疆锛堜袱閮ㄥ垎鍏辩敤锛?, new Padding(10, 18, 10, 10));
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, BackColor = Color.White };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < 3; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        AddRow(table, 0, 0, "LLM 鍘傚晢", aiProvider);
        AddRow(table, 0, 2, "LLM 妯″瀷", aiPreset);
        AddRow(table, 1, 0, "LLM 鍦板潃", aiEndpoint);
        AddRow(table, 1, 2, "LLM Key", aiKey);
        AddRow(table, 2, 0, "妯″瀷 ID", aiModel);
        table.Controls.Add(Button("淇濆瓨璁剧疆", SaveSettings), 3, 2);
        box.Controls.Add(table);
        return box;
    }

    private Control BuildProfiles()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(ProfilePage("缈昏瘧鍒汉", others));
        tabs.TabPages.Add(ProfilePage("缈昏瘧鑷繁", self));
        return tabs;
    }

    private Control BuildFooterBar()
    {
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 6, 0, 0), BackColor = Color.White };
        translationLogToggle.Click += (_, _) => ToggleTranslationLog();
        footer.Controls.Add(translationLogToggle);
        return footer;
    }

    private static TabPage ProfilePage(string title, TranslationProfileUi profile)
    {
        var page = new TabPage(title) { BackColor = Color.White, Padding = new Padding(6) };
        page.Controls.Add(profile.Build());
        return page;
    }

    private void ToggleTranslationLog()
    {
        translationLogVisible = !translationLogVisible;
        ApplyTranslationLogVisibility();
    }

    private void ApplyTranslationLogVisibility()
    {
        if (!translationSplit.IsHandleCreated || translationSplit.Height <= 0) return;
        translationSplit.Panel2Collapsed = !translationLogVisible;
        translationLogToggle.Text = translationLogVisible ? "闅愯棌缈昏瘧鏃ュ織" : "鏄剧ず缈昏瘧鏃ュ織";
        if (!translationLogVisible) return;
        var available = translationSplit.Height - translationSplit.SplitterWidth;
        if (available <= translationSplit.Panel1MinSize) return;
        var target = Math.Max(translationSplit.Panel1MinSize, available / 2);
        if (Math.Abs(translationSplit.SplitterDistance - target) > 8) translationSplit.SplitterDistance = target;
    }

    private int StartProfile(string label, AiTranslationProfileSettings settings)
    {
        if (!settings.Enabled) return 0;
        var session = new AudioCaptureSession(path => ProcessUtteranceAsync(label, settings, path), AppendLog, ToVadOptions(settings));
        var started = session.Start(settings.MicrophoneDevices, settings.SpeakerDevices);
        if (started <= 0)
        {
            session.Dispose();
            AppendLog(label + ": 娌℃湁鍙洃鍚煶棰戞潵婧愩€?);
            return 0;
        }
        captures.Add(session);
        AppendLog(label + ": 宸插惎鍔?" + started + " 涓潵婧愩€?);
        return started;
    }

    private async Task ProcessUtteranceAsync(string label, AiTranslationProfileSettings settings, string path)
    {
        try
        {
            AppendLog(label + " 瀹屾暣鍙ュ瓙閫?ASR: " + Path.GetFileName(path));
            var text = await RequireSpeech().TranscribeAsync(path, settings.ApiKey, settings.ApiEndpoint, settings.Model);
            AppendLog(label + " 鍘熸枃: " + text);
            var cfg = RequireContext().Config.AiTranslation;
            var translated = await RequireSpeech().TranslateAsync(text, cfg.AiApiKey, cfg.AiEndpoint, cfg.AiModel, settings.TargetLanguage);
            AppendLog(label + " 璇戞枃(" + settings.TargetLanguage + "): " + translated);
            var output = settings.IncludeOriginal ? text + "\n" + translated : translated;
            var target = ResolveOutput(settings.OutputTarget);
            if (target.Key is "chatbox" or "both") RequireContext().Osc.SendChatbox(output, true, settings.ChatboxNotify);
            if (target.Key is "steamvr" or "both") await RequireSpeech().SendSteamVrSubtitleAsync(settings.SteamVrHost, settings.SteamVrPort, settings.SteamVrAddress, output);
        }
        catch (Exception ex)
        {
            AppendLog(label + " 缈昏瘧娴佺▼澶辫触: " + Path.GetFileName(path) + " " + ex.Message);
        }
    }

    private void LoadSettings()
    {
        var cfg = RequireContext().Config.AiTranslation;
        var ai = ResolvePreset(AiPresets, cfg.AiProvider, cfg.AiPreset, cfg.AiEndpoint, cfg.AiModel) ?? AiPresets[0];
        aiProvider.SelectedItem = ai.Provider;
        UpdateModelChoices(aiPreset, AiPresets, ai.Provider, false, ApplyAiPreset);
        aiPreset.SelectedItem = ai.Name;
        aiEndpoint.Text = string.IsNullOrWhiteSpace(cfg.AiEndpoint) ? ai.Endpoint : cfg.AiEndpoint;
        aiKey.Text = cfg.AiApiKey;
        aiModel.Text = string.IsNullOrWhiteSpace(cfg.AiModel) ? ai.Model : cfg.AiModel;
        others.Load(cfg.Others, TranslationAsrPresets);
        self.Load(cfg.Self, TranslationAsrPresets);
    }

    private void SaveSettings()
    {
        var cfg = RequireContext().Config.AiTranslation;
        cfg.AiProvider = Convert.ToString(aiProvider.SelectedItem) ?? "Custom";
        cfg.AiPreset = Convert.ToString(aiPreset.SelectedItem) ?? "Custom";
        cfg.AiEndpoint = aiEndpoint.Text.Trim();
        cfg.AiApiKey = aiKey.Text.Trim();
        cfg.AiModel = aiModel.Text.Trim();
        others.Save(cfg.Others);
        self.Save(cfg.Self);
        RequireContext().SaveConfig();
    }

    private void ApplyAiPreset()
    {
        var selected = FindPreset(AiPresets, Convert.ToString(aiProvider.SelectedItem), Convert.ToString(aiPreset.SelectedItem));
        if (selected is null || selected.Provider == "Custom") return;
        aiEndpoint.Text = selected.Endpoint;
        aiModel.Text = selected.Model;
    }

    private void AppendLog(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke((Action)(() => AppendLog(text)));
            return;
        }
        UiHelpers.AppendLimited(logBox, text);
    }

    private ModuleContext RequireContext() => context ?? throw new InvalidOperationException("Module is not initialized.");
    private SpeechAiServices RequireSpeech() => speech ?? throw new InvalidOperationException("Speech service is not initialized.");

    private static VadOptions ToVadOptions(AiTranslationProfileSettings s) => new()
    {
        Threshold = s.VadThreshold,
        PreRollMs = s.VadPreRollMs,
        VoiceStartMs = s.VadVoiceStartMs,
        SilenceEndMs = s.VadSilenceEndMs,
        MinUtteranceMs = s.VadMinUtteranceMs,
        MaxUtteranceMs = s.VadMaxUtteranceMs
    };

    private static void UpdateModelChoices(ComboBox combo, IEnumerable<ProviderModelPreset> presets, string? provider, bool selectFirst, Action apply)
    {
        var current = Convert.ToString(combo.SelectedItem);
        combo.Items.Clear();
        combo.Items.AddRange(presets.Where(p => p.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase)).Select(p => p.Name).Cast<object>().ToArray());
        if (!string.IsNullOrWhiteSpace(current) && combo.Items.Contains(current)) combo.SelectedItem = current;
        else if (selectFirst && combo.Items.Count > 0) combo.SelectedIndex = 0;
        if (selectFirst) apply();
    }

    private static ProviderModelPreset? FindPreset(IEnumerable<ProviderModelPreset> presets, string? provider, string? name)
    {
        return presets.FirstOrDefault(p => p.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase) && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static ProviderModelPreset? ResolvePreset(IEnumerable<ProviderModelPreset> presets, string provider, string preset, string endpoint, string modelId)
    {
        return FindPreset(presets, provider, preset)
            ?? presets.FirstOrDefault(p => (p.Provider + " - " + p.Name).Equals(preset, StringComparison.OrdinalIgnoreCase))
            ?? presets.FirstOrDefault(p => p.Model.Equals(modelId, StringComparison.OrdinalIgnoreCase) && p.Endpoint.Equals(endpoint, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddRow(TableLayoutPanel table, int row, int col, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White }, col, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 2, 10, 2);
        table.Controls.Add(control, col + 1, row);
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 2, 0, 2);
        table.Controls.Add(control, 1, row);
    }

    private static void AddSection(TableLayoutPanel table, int row, string title)
    {
        var label = new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White, Font = new Font(Control.DefaultFont.FontFamily, Control.DefaultFont.Size, FontStyle.Bold), Padding = new Padding(0, 8, 0, 0) };
        table.Controls.Add(label, 0, row);
        table.SetColumnSpan(label, 2);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 88, Height = 28, Margin = new Padding(0, 0, 6, 4) };
        button.Click += (_, _) => action();
        return button;
    }

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

    private sealed class TranslationProfileUi
    {
        private readonly string title;
        private readonly CheckBox enabled = new() { Text = "鍚敤杩欎竴閮ㄥ垎", AutoSize = true };
        private readonly ComboBox asrProvider = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox asrPreset = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox asrEndpoint = new();
        private readonly TextBox asrKey = new() { UseSystemPasswordChar = true };
        private readonly TextBox asrModel = new();
        private readonly ComboBox targetLanguage = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox outputTarget = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox includeOriginal = new() { Text = "杈撳嚭鍘熸枃+璇戞枃", AutoSize = true };
        private readonly CheckBox chatboxNotify = new() { Text = "Chatbox 閫氱煡闊?, AutoSize = true };
        private readonly TextBox steamHost = new();
        private readonly NumericUpDown steamPort = new() { Minimum = 1, Maximum = 65535, Value = 9002 };
        private readonly TextBox steamAddress = new();
        private readonly NumericUpDown vadThreshold = new() { DecimalPlaces = 3, Minimum = 0.001m, Maximum = 0.2m, Increment = 0.001m, Value = 0.004m };
        private readonly NumericUpDown vadPreRoll = new() { Minimum = 100, Maximum = 2000, Increment = 50, Value = 700 };
        private readonly NumericUpDown vadVoiceStart = new() { Minimum = 60, Maximum = 1000, Increment = 20, Value = 200 };
        private readonly NumericUpDown vadSilenceEnd = new() { Minimum = 200, Maximum = 3000, Increment = 50, Value = 900 };
        private readonly NumericUpDown vadMinUtterance = new() { Minimum = 100, Maximum = 5000, Increment = 50, Value = 500 };
        private readonly NumericUpDown vadMaxUtterance = new() { Minimum = 1000, Maximum = 60000, Increment = 500, Value = 12000 };
        private readonly CheckedListBox microphones = new() { CheckOnClick = true, BorderStyle = BorderStyle.None };
        private readonly CheckedListBox speakers = new() { CheckOnClick = true, BorderStyle = BorderStyle.None };
        private readonly CheckedListBox programs = new() { CheckOnClick = true, BorderStyle = BorderStyle.None };
        private ProviderModelPreset[] presets = Array.Empty<ProviderModelPreset>();

        public TranslationProfileUi(string title)
        {
            this.title = title;
            targetLanguage.Items.AddRange(TargetLanguages.Cast<object>().ToArray());
            outputTarget.Items.AddRange(OutputTargets.Cast<object>().ToArray());
        }

        public void InitializePresetControls(ProviderModelPreset[] values)
        {
            presets = values;
            asrProvider.Items.AddRange(values.Select(p => p.Provider).Distinct().Cast<object>().ToArray());
            asrProvider.SelectedIndexChanged += (_, _) => UpdateModelChoices(asrPreset, presets, Convert.ToString(asrProvider.SelectedItem), true, ApplyAsrPreset);
            asrPreset.SelectedIndexChanged += (_, _) => ApplyAsrPreset();
        }

        public Control Build()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 32));

            var settingsScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            var settings = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 24, BackColor = Color.White, Padding = new Padding(8) };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 24; i++) settings.RowStyles.Add(new RowStyle(SizeType.Absolute, i is 0 or 2 or 8 or 14 ? 38 : 30));
            settings.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.White, Font = new Font(Control.DefaultFont.FontFamily, Control.DefaultFont.Size, FontStyle.Bold) }, 0, 0);
            settings.SetColumnSpan(settings.Controls[0], 2);
            settings.Controls.Add(enabled, 1, 1);
            AddSection(settings, 2, "ASR 璇煶璇嗗埆璁剧疆");
            AddRow(settings, 3, "ASR 鍘傚晢", asrProvider);
            AddRow(settings, 4, "ASR 妯″瀷", asrPreset);
            AddRow(settings, 5, "ASR 鍦板潃", asrEndpoint);
            AddRow(settings, 6, "ASR Key", asrKey);
            AddRow(settings, 7, "妯″瀷 ID", asrModel);
            AddSection(settings, 8, "缈昏瘧杈撳嚭璁剧疆");
            AddRow(settings, 9, "鐩爣璇█", targetLanguage);
            AddRow(settings, 10, "杈撳嚭浣嶇疆", outputTarget);
            AddRow(settings, 11, "SteamVR Host", steamHost);
            AddRow(settings, 12, "SteamVR Port", steamPort);
            AddRow(settings, 13, "SteamVR 鍦板潃", steamAddress);
            AddSection(settings, 14, "VAD 鏂彞璁剧疆");
            AddRow(settings, 15, "VAD 闃堝€?, vadThreshold);
            AddRow(settings, 16, "鍓嶇疆缂撳瓨", vadPreRoll);
            AddRow(settings, 17, "浜哄０寮€濮?, vadVoiceStart);
            AddRow(settings, 18, "闈欓煶缁撴潫", vadSilenceEnd);
            AddRow(settings, 19, "鏈€鐭彞瀛?, vadMinUtterance);
            AddRow(settings, 20, "鏈€澶у彞瀛?, vadMaxUtterance);
            var flags = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White };
            flags.Controls.Add(includeOriginal);
            flags.Controls.Add(chatboxNotify);
            settings.Controls.Add(flags, 1, 21);
            settingsScroll.Controls.Add(settings);
            settingsScroll.Resize += (_, _) =>
            {
                var target = Math.Max(520, settingsScroll.ClientSize.Width - 20);
                if (Math.Abs(settings.Width - target) > 8) settings.Width = target;
            };
            root.Controls.Add(settingsScroll, 0, 0);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            microphones.Dock = DockStyle.Fill;
            speakers.Dock = DockStyle.Fill;
            programs.Dock = DockStyle.Fill;
            tabs.TabPages.Add(SourcePage("楹﹀厠椋庤緭鍏?, microphones));
            tabs.TabPages.Add(SourcePage("鎵０鍣ㄥ洖鐜?, speakers));
            tabs.TabPages.Add(SourcePage("绋嬪簭澹伴煶", programs));
            root.Controls.Add(tabs, 0, 1);
            return root;
        }

        public void Load(AiTranslationProfileSettings settings, ProviderModelPreset[] values)
        {
            RefreshAudioDevices();
            enabled.Checked = settings.Enabled;
            var preset = ResolvePreset(values, settings.AsrProvider, settings.AsrPreset, settings.ApiEndpoint, settings.Model) ?? values[0];
            asrProvider.SelectedItem = preset.Provider;
            UpdateModelChoices(asrPreset, values, preset.Provider, false, ApplyAsrPreset);
            asrPreset.SelectedItem = preset.Name;
            asrEndpoint.Text = string.IsNullOrWhiteSpace(settings.ApiEndpoint) ? preset.Endpoint : settings.ApiEndpoint;
            asrKey.Text = settings.ApiKey;
            asrModel.Text = string.IsNullOrWhiteSpace(settings.Model) ? preset.Model : settings.Model;
            targetLanguage.SelectedItem = ResolveLanguage(settings.TargetLanguage);
            outputTarget.SelectedItem = ResolveOutput(settings.OutputTarget);
            includeOriginal.Checked = settings.IncludeOriginal;
            chatboxNotify.Checked = settings.ChatboxNotify;
            steamHost.Text = string.IsNullOrWhiteSpace(settings.SteamVrHost) ? "127.0.0.1" : settings.SteamVrHost;
            steamPort.Value = Math.Clamp(settings.SteamVrPort, 1, 65535);
            steamAddress.Text = string.IsNullOrWhiteSpace(settings.SteamVrAddress) ? "/steamvr/subtitle" : settings.SteamVrAddress;
            vadThreshold.Value = (decimal)Math.Clamp(settings.VadThreshold, (double)vadThreshold.Minimum, (double)vadThreshold.Maximum);
            vadPreRoll.Value = Math.Clamp(settings.VadPreRollMs, (int)vadPreRoll.Minimum, (int)vadPreRoll.Maximum);
            vadVoiceStart.Value = Math.Clamp(settings.VadVoiceStartMs, (int)vadVoiceStart.Minimum, (int)vadVoiceStart.Maximum);
            vadSilenceEnd.Value = Math.Clamp(settings.VadSilenceEndMs, (int)vadSilenceEnd.Minimum, (int)vadSilenceEnd.Maximum);
            vadMinUtterance.Value = Math.Clamp(settings.VadMinUtteranceMs, (int)vadMinUtterance.Minimum, (int)vadMinUtterance.Maximum);
            vadMaxUtterance.Value = Math.Clamp(settings.VadMaxUtteranceMs, (int)vadMaxUtterance.Minimum, (int)vadMaxUtterance.Maximum);
            CheckItems(microphones, settings.MicrophoneDevices);
            CheckItems(speakers, settings.SpeakerDevices);
            CheckItems(programs, settings.ProgramSources);
        }

        public void Save(AiTranslationProfileSettings settings)
        {
            settings.Enabled = enabled.Checked;
            settings.AsrProvider = Convert.ToString(asrProvider.SelectedItem) ?? "Custom";
            settings.AsrPreset = Convert.ToString(asrPreset.SelectedItem) ?? "Custom";
            settings.ApiEndpoint = asrEndpoint.Text.Trim();
            settings.ApiKey = asrKey.Text.Trim();
            settings.Model = asrModel.Text.Trim();
            settings.TargetLanguage = SelectedLanguageInstruction();
            settings.OutputTarget = SelectedOutputKey();
            settings.IncludeOriginal = includeOriginal.Checked;
            settings.ChatboxNotify = chatboxNotify.Checked;
            settings.SteamVrHost = steamHost.Text.Trim();
            settings.SteamVrPort = (int)steamPort.Value;
            settings.SteamVrAddress = steamAddress.Text.Trim();
            settings.MicrophoneDevices = CheckedItems(microphones);
            settings.SpeakerDevices = CheckedItems(speakers);
            settings.ProgramSources = CheckedItems(programs);
            settings.VadThreshold = (double)vadThreshold.Value;
            settings.VadPreRollMs = (int)vadPreRoll.Value;
            settings.VadVoiceStartMs = (int)vadVoiceStart.Value;
            settings.VadSilenceEndMs = (int)vadSilenceEnd.Value;
            settings.VadMinUtteranceMs = (int)vadMinUtterance.Value;
            settings.VadMaxUtteranceMs = (int)vadMaxUtterance.Value;
        }

        private void ApplyAsrPreset()
        {
            var selected = FindPreset(presets, Convert.ToString(asrProvider.SelectedItem), Convert.ToString(asrPreset.SelectedItem));
            if (selected is null || selected.Provider == "Custom") return;
            asrEndpoint.Text = selected.Endpoint;
            asrModel.Text = selected.Model;
        }

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
            CheckItems(microphones, oldInputs);
            CheckItems(speakers, oldOutputs);
            CheckItems(programs, oldPrograms);
        }

        private string SelectedLanguageInstruction()
        {
            return targetLanguage.SelectedItem is LanguageOption option ? option.Instruction : ResolveLanguage(targetLanguage.Text).Instruction;
        }

        private string SelectedOutputKey()
        {
            return outputTarget.SelectedItem is OutputOption option ? option.Key : ResolveOutput(outputTarget.Text).Key;
        }
    }

    private sealed record LanguageOption(string Label, string Instruction)
    {
        public override string ToString() => Label;
    }

    private sealed record OutputOption(string Label, string Key)
    {
        public override string ToString() => Label;
    }

    private static LanguageOption ResolveLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TargetLanguages[0];
        return TargetLanguages.FirstOrDefault(x =>
                   x.Label.Equals(value, StringComparison.OrdinalIgnoreCase)
                   || x.Instruction.Equals(value, StringComparison.OrdinalIgnoreCase)
                   || value.Contains(x.Label, StringComparison.OrdinalIgnoreCase)
                   || value.Contains(x.Instruction, StringComparison.OrdinalIgnoreCase))
               ?? ResolveLegacyLanguage(value);
    }

    private static LanguageOption ResolveLegacyLanguage(string value)
    {
        if (value.Contains("en", StringComparison.OrdinalIgnoreCase) || value.Contains("鑻?, StringComparison.OrdinalIgnoreCase)) return TargetLanguages[1];
        if (value.Contains("ja", StringComparison.OrdinalIgnoreCase) || value.Contains("鏃?, StringComparison.OrdinalIgnoreCase)) return TargetLanguages[2];
        if (value.Contains("ko", StringComparison.OrdinalIgnoreCase) || value.Contains("闊?, StringComparison.OrdinalIgnoreCase)) return TargetLanguages[3];
        if (value.Contains("fr", StringComparison.OrdinalIgnoreCase) || value.Contains("娉?, StringComparison.OrdinalIgnoreCase)) return TargetLanguages[4];
        if (value.Contains("de", StringComparison.OrdinalIgnoreCase) || value.Contains("寰?, StringComparison.OrdinalIgnoreCase)) return TargetLanguages[5];
        if (value.Contains("es", StringComparison.OrdinalIgnoreCase) || value.Contains("瑗?, StringComparison.OrdinalIgnoreCase)) return TargetLanguages[6];
        return TargetLanguages[0];
    }

    private static OutputOption ResolveOutput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return OutputTargets[0];
        return OutputTargets.FirstOrDefault(x =>
                   x.Label.Equals(value, StringComparison.OrdinalIgnoreCase)
                   || x.Key.Equals(value, StringComparison.OrdinalIgnoreCase)
                   || value.Contains(x.Label, StringComparison.OrdinalIgnoreCase)
                   || value.Contains(x.Key, StringComparison.OrdinalIgnoreCase))
               ?? (value.Contains("SteamVR", StringComparison.OrdinalIgnoreCase) ? OutputTargets[1]
                   : value.Contains("涓?, StringComparison.OrdinalIgnoreCase) || value.Contains("both", StringComparison.OrdinalIgnoreCase) ? OutputTargets[2]
                   : OutputTargets[0]);
    }
}
