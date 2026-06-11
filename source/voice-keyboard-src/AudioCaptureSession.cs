using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace OSCC;

public sealed class VadOptions
{
    private const int FrameMs = 20;
    public double Threshold { get; init; } = 0.018;
    public int PreRollMs { get; init; } = 500;
    public int VoiceStartMs { get; init; } = 200;
    public int SilenceEndMs { get; init; } = 900;
    public int MinUtteranceMs { get; init; } = 500;
    public int MaxUtteranceMs { get; init; } = 10000;
    public int PreRollFrames => Math.Max(1, PreRollMs / FrameMs);
    public int VoiceStartFrames => Math.Max(1, VoiceStartMs / FrameMs);
    public int SilenceEndFrames => Math.Max(1, SilenceEndMs / FrameMs);
    public int MinUtteranceFrames => Math.Max(1, MinUtteranceMs / FrameMs);
    public int MaxUtteranceFrames => Math.Max(MinUtteranceFrames, MaxUtteranceMs / FrameMs);
}

public sealed class AudioCaptureSession : IDisposable
{
    private readonly Func<string, Task> onUtteranceFile;
    private readonly Action<string> log;
    private readonly VadOptions options;
    private readonly List<SourceCapture> captures = new();

    public AudioCaptureSession(Func<string, Task> onUtteranceFile, Action<string> log, VadOptions options)
    {
        this.onUtteranceFile = onUtteranceFile;
        this.log = log;
        this.options = options;
    }

    public int Start(IEnumerable<string> microphones, IEnumerable<string> speakers)
    {
        Stop();
        foreach (var name in microphones)
        {
            var index = FindInputIndex(name);
            if (index < 0)
            {
                log("鏈壘鍒伴害鍏嬮: " + name);
                continue;
            }
            var waveIn = new WaveInEvent
            {
                DeviceNumber = index,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 20
            };
            captures.Add(new SourceCapture("楹﹀厠椋?, name, waveIn, onUtteranceFile, log, options));
        }

        using var enumerator = new MMDeviceEnumerator();
        var renderDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var name in speakers)
        {
            var device = renderDevices.FirstOrDefault(d => string.Equals(d.FriendlyName, name, StringComparison.OrdinalIgnoreCase));
            if (device is null)
            {
                log("鏈壘鍒版壃澹板櫒: " + name);
                continue;
            }
            captures.Add(new SourceCapture("鎵０鍣?, name, new WasapiLoopbackCapture(device), onUtteranceFile, log, options));
        }

        foreach (var capture in captures) capture.Start();
        return captures.Count;
    }

    public void Stop()
    {
        foreach (var capture in captures) capture.Dispose();
        captures.Clear();
    }

    public void Dispose() => Stop();

    private static int FindInputIndex(string name)
    {
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            if (string.Equals(WaveInEvent.GetCapabilities(i).ProductName, name, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private sealed class SourceCapture : IDisposable
    {
        private const int TargetSampleRate = 16000;
        private const int FrameMs = 20;
        private const int BytesPerFrame = TargetSampleRate * 2 * FrameMs / 1000;

        private readonly string kind;
        private readonly string name;
        private readonly IWaveIn capture;
        private readonly Func<string, Task> onUtteranceFile;
        private readonly Action<string> log;
        private readonly VadOptions options;
        private readonly object gate = new();
        private readonly Queue<byte[]> preRoll = new();
        private readonly List<byte[]> utteranceFrames = new();
        private readonly List<byte> pendingBytes = new();
        private bool recording;
        private int voiceFrames;
        private int silenceFrames;
        private bool disposed;

        public SourceCapture(string kind, string name, IWaveIn capture, Func<string, Task> onUtteranceFile, Action<string> log, VadOptions options)
        {
            this.kind = kind;
            this.name = name;
            this.capture = capture;
            this.onUtteranceFile = onUtteranceFile;
            this.log = log;
            this.options = options;
            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null) log(kind + " 鎹曡幏鍋滄: " + name + " " + e.Exception.Message);
                FlushUtterance();
            };
        }

        public void Start()
        {
            capture.StartRecording();
            log("杩炵画鐩戝惉" + kind + ": " + name + "锛孷AD 闃堝€?" + options.Threshold + "锛宲re-roll " + options.PreRollMs + "ms銆?);
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            var pcm16k = ConvertToPcm16kMono(e.Buffer, e.BytesRecorded, capture.WaveFormat);
            var readyFiles = new List<string>();
            lock (gate)
            {
                pendingBytes.AddRange(pcm16k);
                while (pendingBytes.Count >= BytesPerFrame)
                {
                    var frame = pendingBytes.Take(BytesPerFrame).ToArray();
                    pendingBytes.RemoveRange(0, BytesPerFrame);
                    var ready = ProcessFrame(frame);
                    if (ready != null) readyFiles.Add(ready);
                }
            }
            foreach (var file in readyFiles) _ = onUtteranceFile(file);
        }

        private string? ProcessFrame(byte[] frame)
        {
            var isVoice = IsVoice(frame);

            if (!recording)
            {
                preRoll.Enqueue(frame);
                while (preRoll.Count > options.PreRollFrames) preRoll.Dequeue();
                voiceFrames = isVoice ? voiceFrames + 1 : 0;
                if (voiceFrames >= options.VoiceStartFrames)
                {
                    recording = true;
                    silenceFrames = 0;
                    utteranceFrames.Clear();
                    utteranceFrames.AddRange(preRoll);
                    log(kind + " 妫€娴嬪埌璇煶寮€濮? " + name);
                }
                return null;
            }

            utteranceFrames.Add(frame);
            if (isVoice)
            {
                silenceFrames = 0;
            }
            else
            {
                silenceFrames++;
                if (silenceFrames >= options.SilenceEndFrames)
                {
                    return FinishUtterance("闈欓煶缁撴潫");
                }
            }
            if (utteranceFrames.Count >= options.MaxUtteranceFrames)
            {
                return FinishUtterance("杈惧埌鏈€澶у彞瀛愭椂闀?);
            }
            return null;
        }

        private string? FinishUtterance(string reason)
        {
            recording = false;
            voiceFrames = 0;
            silenceFrames = 0;
            preRoll.Clear();
            if (utteranceFrames.Count < options.MinUtteranceFrames)
            {
                utteranceFrames.Clear();
                return null;
            }

            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "oscc_voice_utterances"));
            var path = Path.Combine(Path.GetTempPath(), "oscc_voice_utterances", "oscc_sentence_" + Guid.NewGuid().ToString("N") + ".wav");
            using (var writer = new WaveFileWriter(path, new WaveFormat(TargetSampleRate, 16, 1)))
            {
                foreach (var f in utteranceFrames) writer.Write(f, 0, f.Length);
            }
            utteranceFrames.Clear();
            log(kind + " 璇煶缁撴潫(" + reason + ")锛屽凡淇濆瓨瀹屾暣鍙ュ瓙: " + Path.GetFileName(path));
            return path;
        }

        private void FlushUtterance()
        {
            string? ready = null;
            lock (gate)
            {
                if (recording) ready = FinishUtterance("鍋滄鐩戝惉");
            }
            if (ready != null) _ = onUtteranceFile(ready);
        }

        private bool IsVoice(byte[] frame)
        {
            if (frame.Length < 2) return false;
            double sum = 0;
            var samples = frame.Length / 2;
            for (var i = 0; i < frame.Length; i += 2)
            {
                var sample = BitConverter.ToInt16(frame, i) / 32768.0;
                sum += sample * sample;
            }
            var rms = Math.Sqrt(sum / samples);
            return rms >= options.Threshold;
        }

        private static byte[] ConvertToPcm16kMono(byte[] buffer, int bytesRecorded, WaveFormat format)
        {
            var mono = DecodeMonoSamples(buffer, bytesRecorded, format);
            var resampled = ResampleLinear(mono, format.SampleRate, TargetSampleRate);
            var output = new byte[resampled.Length * 2];
            for (var i = 0; i < resampled.Length; i++)
            {
                var clamped = Math.Clamp(resampled[i], -1f, 1f);
                var sample = (short)Math.Round(clamped * short.MaxValue);
                output[i * 2] = (byte)(sample & 0xff);
                output[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
            }
            return output;
        }

        private static float[] DecodeMonoSamples(byte[] buffer, int bytesRecorded, WaveFormat format)
        {
            var channels = Math.Max(1, format.Channels);
            if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            {
                var totalSamples = bytesRecorded / 4;
                var frames = totalSamples / channels;
                var mono = new float[frames];
                for (var f = 0; f < frames; f++)
                {
                    double sum = 0;
                    for (var ch = 0; ch < channels; ch++)
                    {
                        sum += BitConverter.ToSingle(buffer, (f * channels + ch) * 4);
                    }
                    mono[f] = (float)(sum / channels);
                }
                return mono;
            }

            if (format.BitsPerSample == 16)
            {
                var totalSamples = bytesRecorded / 2;
                var frames = totalSamples / channels;
                var mono = new float[frames];
                for (var f = 0; f < frames; f++)
                {
                    double sum = 0;
                    for (var ch = 0; ch < channels; ch++)
                    {
                        sum += BitConverter.ToInt16(buffer, (f * channels + ch) * 2) / 32768.0;
                    }
                    mono[f] = (float)(sum / channels);
                }
                return mono;
            }

            return Array.Empty<float>();
        }

        private static float[] ResampleLinear(float[] input, int sourceRate, int targetRate)
        {
            if (input.Length == 0) return input;
            if (sourceRate == targetRate) return input;
            var targetLength = Math.Max(1, (int)Math.Round(input.Length * (double)targetRate / sourceRate));
            var output = new float[targetLength];
            var ratio = (double)sourceRate / targetRate;
            for (var i = 0; i < targetLength; i++)
            {
                var pos = i * ratio;
                var idx = (int)pos;
                var frac = pos - idx;
                var a = input[Math.Min(idx, input.Length - 1)];
                var b = input[Math.Min(idx + 1, input.Length - 1)];
                output[i] = (float)(a + (b - a) * frac);
            }
            return output;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { capture.StopRecording(); } catch { }
            capture.Dispose();
            FlushUtterance();
        }
    }
}
