using System.Text.Json.Serialization;

namespace OSCC;

public sealed class AppConfig
{
    public string OscHost { get; set; } = "127.0.0.1";
    public int OscPort { get; set; } = 9000;
    public List<KeyboardMapping> Mappings { get; set; } = new();
    public List<VoiceMapping> VoiceMappings { get; set; } = new();
    public VoiceRecognitionSettings VoiceRecognition { get; set; } = new();
    public AiTranslationSettings AiTranslation { get; set; } = new();
}

public sealed class VoiceRecognitionSettings
{
    public bool Enabled { get; set; }
    public string SourceType { get; set; } = "闊抽璁惧/绋嬪簭";
    public string AsrProvider { get; set; } = "鍗冮棶/鐧剧偧";
    public string AsrPreset { get; set; } = "瀹炴椂璇嗗埆";
    public string ApiEndpoint { get; set; } = "wss://dashscope.aliyuncs.com/api-ws/v1/inference";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "paraformer-realtime-v2";
    public bool AiSemanticMatching { get; set; } = true;
    public string AiProvider { get; set; } = "鍗冮棶/鐧剧偧";
    public string AiPreset { get; set; } = "鏈€寮?;
    public string AiEndpoint { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
    public string AiApiKey { get; set; } = "";
    public string AiModel { get; set; } = "qwen-max-latest";
    public double AiThreshold { get; set; } = 0.75;
    public string WatchTextFilePath { get; set; } = "";
    public string WatchAudioFolderPath { get; set; } = "";
    public List<string> MicrophoneDevices { get; set; } = new();
    public List<string> SpeakerDevices { get; set; } = new();
    public List<string> ProgramSources { get; set; } = new();
    public int PollMs { get; set; } = 500;
    public double VadThreshold { get; set; } = 0.004;
    public int VadPreRollMs { get; set; } = 700;
    public int VadVoiceStartMs { get; set; } = 200;
    public int VadSilenceEndMs { get; set; } = 900;
    public int VadMinUtteranceMs { get; set; } = 500;
    public int VadMaxUtteranceMs { get; set; } = 12000;
    public bool IgnoreCase { get; set; } = true;
    public bool TriggerOncePerRecognition { get; set; } = true;
}

public sealed class AiTranslationSettings
{
    public bool Enabled { get; set; }
    public string AiProvider { get; set; } = "鍗冮棶/鐧剧偧";
    public string AiPreset { get; set; } = "鏈€寮?;
    public string AiEndpoint { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
    public string AiApiKey { get; set; } = "";
    public string AiModel { get; set; } = "qwen-max-latest";
    public AiTranslationProfileSettings Others { get; set; } = AiTranslationProfileSettings.ForOthers();
    public AiTranslationProfileSettings Self { get; set; } = AiTranslationProfileSettings.ForSelf();

    // Legacy fields kept for old config compatibility.
    public string SourceType { get; set; } = "闊抽璁惧/绋嬪簭";
    public string AsrProvider { get; set; } = "鍗冮棶/鐧剧偧";
    public string AsrPreset { get; set; } = "瀹炴椂缈昏瘧杈撳叆";
    public string ApiEndpoint { get; set; } = "wss://dashscope.aliyuncs.com/api-ws/v1/inference";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "paraformer-realtime-v2";
}

public sealed class AiTranslationProfileSettings
{
    public bool Enabled { get; set; } = true;
    public string AsrProvider { get; set; } = "鍗冮棶/鐧剧偧";
    public string AsrPreset { get; set; } = "瀹炴椂缈昏瘧杈撳叆";
    public string ApiEndpoint { get; set; } = "wss://dashscope.aliyuncs.com/api-ws/v1/inference";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "paraformer-realtime-v2";
    public string TargetLanguage { get; set; } = "涓枃";
    public string OutputTarget { get; set; } = "SteamVR 鎺屽績瀛楀箷";
    public bool IncludeOriginal { get; set; } = true;
    public bool ChatboxNotify { get; set; }
    public string SteamVrHost { get; set; } = "127.0.0.1";
    public int SteamVrPort { get; set; } = 9002;
    public string SteamVrAddress { get; set; } = "/steamvr/subtitle";
    public List<string> MicrophoneDevices { get; set; } = new();
    public List<string> SpeakerDevices { get; set; } = new();
    public List<string> ProgramSources { get; set; } = new();
    public double VadThreshold { get; set; } = 0.004;
    public int VadPreRollMs { get; set; } = 700;
    public int VadVoiceStartMs { get; set; } = 200;
    public int VadSilenceEndMs { get; set; } = 900;
    public int VadMinUtteranceMs { get; set; } = 500;
    public int VadMaxUtteranceMs { get; set; } = 12000;

    public static AiTranslationProfileSettings ForOthers() => new();

    public static AiTranslationProfileSettings ForSelf() => new()
    {
        TargetLanguage = "鑻辨枃",
        OutputTarget = "Chatbox",
        IncludeOriginal = false
    };
}

public sealed class KeyboardMapping
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;

    // Keep the old JSON name for compatibility. The UI now calls this field "澶囨敞".
    public string Project { get; set; } = "";

    [JsonIgnore]
    public string Note
    {
        get => Project;
        set => Project = value;
    }

    public string Parameter { get; set; } = "";
    public string Hotkey { get; set; } = "";
    public string Type { get; set; } = "bool";
    public string Mode { get; set; } = "toggle";
    public string Value { get; set; } = "true";
    public string OffValue { get; set; } = "false";
    public double Step { get; set; } = 0.1;
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 1;
    public int DurationMs { get; set; } = 250;
    public List<ParameterAction> Actions { get; set; } = new();

    [JsonIgnore]
    public object? State { get; set; }

    public KeyboardMapping Clone() => new()
    {
        Id = Id,
        Enabled = Enabled,
        Project = Project,
        Parameter = Parameter,
        Hotkey = Hotkey,
        Type = Type,
        Mode = Mode,
        Value = Value,
        OffValue = OffValue,
        Step = Step,
        Min = Min,
        Max = Max,
        DurationMs = DurationMs,
        Actions = Actions.Select(a => a.Clone()).ToList(),
        State = State
    };

    public List<ParameterAction> EffectiveActions()
    {
        if (Actions.Count > 0) return Actions;
        return new List<ParameterAction>
        {
            new()
            {
                Parameter = Parameter,
                Type = Type,
                Mode = Mode,
                Value = Value,
                OffValue = OffValue,
                Step = Step,
                Min = Min,
                Max = Max,
                DurationMs = DurationMs,
                State = State
            }
        };
    }
}

public sealed class ParameterAction
{
    public string Parameter { get; set; } = "";
    public string Type { get; set; } = "bool";
    public string Mode { get; set; } = "toggle";
    public string Value { get; set; } = "true";
    public string OffValue { get; set; } = "false";
    public double Step { get; set; } = 0.1;
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 1;
    public int DurationMs { get; set; } = 250;

    [JsonIgnore]
    public object? State { get; set; }

    public ParameterAction Clone() => new()
    {
        Parameter = Parameter,
        Type = Type,
        Mode = Mode,
        Value = Value,
        OffValue = OffValue,
        Step = Step,
        Min = Min,
        Max = Max,
        DurationMs = DurationMs,
        State = State
    };
}

public sealed class VoiceMapping
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Note { get; set; } = "";
    public string Keywords { get; set; } = "";
    public string Intent { get; set; } = "";
    public string MatchMode { get; set; } = "AI 璇箟鍒ゆ柇";
    public string Parameter { get; set; } = "";
    public string Type { get; set; } = "bool";
    public string Mode { get; set; } = "toggle";
    public string Value { get; set; } = "true";
    public string OffValue { get; set; } = "false";
    public double Step { get; set; } = 0.1;
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 1;
    public int DurationMs { get; set; } = 250;
    public List<ParameterAction> Actions { get; set; } = new();

    [JsonIgnore]
    public object? State { get; set; }

    public VoiceMapping Clone() => new()
    {
        Id = Id,
        Enabled = Enabled,
        Note = Note,
        Keywords = Keywords,
        Intent = Intent,
        MatchMode = MatchMode,
        Parameter = Parameter,
        Type = Type,
        Mode = Mode,
        Value = Value,
        OffValue = OffValue,
        Step = Step,
        Min = Min,
        Max = Max,
        DurationMs = DurationMs,
        Actions = Actions.Select(a => a.Clone()).ToList(),
        State = State
    };

    public KeyboardMapping ToKeyboardMapping() => new()
    {
        Id = Id,
        Enabled = Enabled,
        Note = Note,
        Parameter = Parameter,
        Hotkey = "璇煶:" + (string.IsNullOrWhiteSpace(Intent) ? Keywords : Intent),
        Type = Type,
        Mode = Mode,
        Value = Value,
        OffValue = OffValue,
        Step = Step,
        Min = Min,
        Max = Max,
        DurationMs = DurationMs,
        Actions = Actions.Select(a => a.Clone()).ToList(),
        State = State
    };

    public void PullStateFrom(KeyboardMapping mapping) => State = mapping.State;
}
