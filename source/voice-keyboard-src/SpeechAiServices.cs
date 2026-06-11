using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace OSCC;

public sealed class SpeechAiServices
{
    private readonly HttpClient http;
    private readonly Action<string> log;

    public SpeechAiServices(HttpClient http, Action<string> log)
    {
        this.http = http;
        this.log = log;
    }

    public async Task<string> TranscribeAsync(string audioPath, string apiKey, string endpoint, string model)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Please fill ASR API Key first.");
        if (string.IsNullOrWhiteSpace(endpoint)) throw new InvalidOperationException("Please fill ASR endpoint first.");
        if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException("Please fill ASR model first.");
        if (endpoint.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return await TranscribeDashScopeRealtimeAsync(audioPath, apiKey, endpoint, model);
        }
        return await TranscribeMultipartAsync(audioPath, apiKey, endpoint, model);
    }

    public async Task<string> TranslateAsync(string text, string apiKey, string endpoint, string model, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Please fill translation LLM API Key first.");
        if (string.IsNullOrWhiteSpace(endpoint)) throw new InvalidOperationException("Please fill translation LLM endpoint first.");
        if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException("Please fill translation LLM model first.");
        targetLanguage = NormalizeTargetLanguage(targetLanguage);
        var payload = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are a real-time VRChat subtitle translator. Output only the translated text in the requested target language. Do not explain, do not add quotes, and do not default to English unless English is requested. Keep names, game terms, and emotional tone natural."
                },
                new
                {
                    role = "user",
                    content = "Translate the following text into " + targetLanguage + ". Output only " + targetLanguage + ":\n" + text
                }
            },
            temperature = 0.2
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Translation returned error: " + (int)response.StatusCode + " " + json);
        return ExtractChatText(json);
    }

    public async Task SendSteamVrSubtitleAsync(string host, int port, string address, string text)
    {
        using var udp = new UdpClient();
        var endpoint = new IPEndPoint((await Dns.GetHostAddressesAsync(host)).First(a => a.AddressFamily == AddressFamily.InterNetwork), port);
        var packet = BuildOscStringMessage(string.IsNullOrWhiteSpace(address) ? "/steamvr/subtitle" : address, text);
        await udp.SendAsync(packet, packet.Length, endpoint);
    }

    private async Task<string> TranscribeMultipartAsync(string audioPath, string apiKey, string endpoint, string model)
    {
        log("Request ASR file transcription: " + endpoint);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(model), "model");
        var file = new StreamContent(File.OpenRead(audioPath));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        content.Add(file, "file", Path.GetFileName(audioPath));
        request.Content = content;
        using var response = await http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        log("ASR file transcription returned HTTP " + (int)response.StatusCode);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("ASR returned error: " + (int)response.StatusCode + " " + json);
        return ExtractAsrText(json);
    }

    private async Task<string> TranscribeDashScopeRealtimeAsync(string audioPath, string apiKey, string endpoint, string model)
    {
        log("Request ASR realtime WebSocket: " + endpoint);
        using var socket = new System.Net.WebSockets.ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "bearer " + apiKey.Trim());
        socket.Options.SetRequestHeader("X-DashScope-DataInspection", "enable");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(70));
        await socket.ConnectAsync(new Uri(endpoint), timeout.Token);

        var taskId = Guid.NewGuid().ToString("N");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalTexts = new List<string>();
        var receiveTask = ReceiveDashScopeAsync(socket, started, finished, finalTexts, timeout.Token);

        await SendWebSocketJsonAsync(socket, new
        {
            header = new { action = "run-task", task_id = taskId, streaming = "duplex" },
            payload = new
            {
                task_group = "audio",
                task = "asr",
                function = "recognition",
                model,
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
        }, timeout.Token);
        await started.Task.WaitAsync(timeout.Token);

        var pcm = ReadPcmDataFromWave(audioPath);
        for (var offset = 0; offset < pcm.Length; offset += 3200)
        {
            var count = Math.Min(3200, pcm.Length - offset);
            await socket.SendAsync(pcm.AsMemory(offset, count), System.Net.WebSockets.WebSocketMessageType.Binary, true, timeout.Token);
            await Task.Delay(20, timeout.Token);
        }

        await SendWebSocketJsonAsync(socket, new { header = new { action = "finish-task", task_id = taskId, streaming = "duplex" }, payload = new { input = new { } } }, timeout.Token);
        var resultText = await finished.Task.WaitAsync(timeout.Token);
        try
        {
            if (socket.State == System.Net.WebSockets.WebSocketState.Open)
            {
                await socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
            }
        }
        catch { }
        await receiveTask.WaitAsync(TimeSpan.FromSeconds(2)).ContinueWith(_ => { });
        log("ASR final text: " + resultText);
        return resultText;
    }

    private async Task ReceiveDashScopeAsync(System.Net.WebSockets.ClientWebSocket socket, TaskCompletionSource started, TaskCompletionSource<string> finished, List<string> finalTexts, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        try
        {
            while (socket.State == System.Net.WebSockets.WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) break;
                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage) continue;
                var payload = Encoding.UTF8.GetString(message.ToArray());
                message.SetLength(0);
                HandleDashScopeEvent(payload, started, finished, finalTexts);
                if (finished.Task.IsCompleted) break;
            }
        }
        catch (Exception ex)
        {
            started.TrySetException(ex);
            finished.TrySetException(ex);
        }
    }

    private void HandleDashScopeEvent(string json, TaskCompletionSource started, TaskCompletionSource<string> finished, List<string> finalTexts)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var eventName = "";
        if (root.TryGetProperty("header", out var header))
        {
            if (TryGetString(header, "event", out var ev)) eventName = ev;
            if (TryGetString(header, "error_message", out var errorMessage))
            {
                finished.TrySetException(new InvalidOperationException("ASR WebSocket error: " + errorMessage));
                return;
            }
        }
        if (eventName.Equals("task-started", StringComparison.OrdinalIgnoreCase))
        {
            log("ASR WebSocket started receiving audio.");
            started.TrySetResult();
            return;
        }
        if (eventName.Equals("result-generated", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetDashScopeSentence(root, out var text, out var isFinal) && !string.IsNullOrWhiteSpace(text))
            {
                log((isFinal ? "ASR sentence final: " : "ASR partial: ") + text);
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
            finished.TrySetException(new InvalidOperationException("ASR WebSocket task failed: " + json));
        }
    }

    private static async Task SendWebSocketJsonAsync(System.Net.WebSockets.ClientWebSocket socket, object value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, System.Net.WebSockets.WebSocketMessageType.Text, true, cancellationToken);
    }

    private static string ExtractChatText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (TryGetString(first, "message", "content", out var content)) return StripCodeFence(content);
            if (TryGetString(first, "text", out content)) return StripCodeFence(content);
        }
        if (TryGetString(root, "output", "text", out var text)) return StripCodeFence(text);
        return json;
    }

    private static string ExtractAsrText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (TryGetString(root, "text", out var text)) return text;
        if (TryGetString(root, "output", "text", out text)) return text;
        if (TryGetString(root, "result", "text", out text)) return text;
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (TryGetString(first, "text", out text)) return text;
            if (TryGetString(first, "message", "content", out text)) return text;
        }
        throw new InvalidOperationException("Cannot parse final ASR text from response: " + json);
    }

    private static bool TryGetDashScopeSentence(JsonElement root, out string text, out bool isFinal)
    {
        text = "";
        isFinal = false;
        if (!root.TryGetProperty("payload", out var payload)) return false;
        if (payload.TryGetProperty("output", out var output)) payload = output;
        if (!payload.TryGetProperty("sentence", out var sentence)) return false;
        if (!TryGetString(sentence, "text", out text)) return false;
        isFinal = sentence.TryGetProperty("end_time", out var endTime) && endTime.ValueKind != JsonValueKind.Null;
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
        throw new InvalidOperationException("Cannot read PCM data from wave file.");
    }

    private static byte[] BuildOscStringMessage(string address, string text)
    {
        using var stream = new MemoryStream();
        WriteOscString(stream, address.StartsWith('/') ? address : "/" + address);
        WriteOscString(stream, ",s");
        WriteOscString(stream, text);
        return stream.ToArray();
    }

    private static void WriteOscString(Stream stream, string value)
    {
        stream.Write(Encoding.UTF8.GetBytes(value));
        stream.WriteByte(0);
        while (stream.Length % 4 != 0) stream.WriteByte(0);
    }

    private static bool TryGetString(JsonElement element, string property, out string value)
    {
        value = "";
        if (element.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.String)
        {
            value = found.GetString() ?? "";
            return !string.IsNullOrWhiteSpace(value);
        }
        return false;
    }

    private static bool TryGetString(JsonElement element, string first, string second, out string value)
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

    private static string NormalizeTargetLanguage(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Chinese (Simplified)";
        if (value.Contains("English", StringComparison.OrdinalIgnoreCase) || value.Contains("en", StringComparison.OrdinalIgnoreCase) || value.Contains("英", StringComparison.OrdinalIgnoreCase)) return "English";
        if (value.Contains("Japanese", StringComparison.OrdinalIgnoreCase) || value.Contains("ja", StringComparison.OrdinalIgnoreCase) || value.Contains("日", StringComparison.OrdinalIgnoreCase)) return "Japanese";
        if (value.Contains("Korean", StringComparison.OrdinalIgnoreCase) || value.Contains("ko", StringComparison.OrdinalIgnoreCase) || value.Contains("韩", StringComparison.OrdinalIgnoreCase)) return "Korean";
        if (value.Contains("French", StringComparison.OrdinalIgnoreCase) || value.Contains("fr", StringComparison.OrdinalIgnoreCase) || value.Contains("法", StringComparison.OrdinalIgnoreCase)) return "French";
        if (value.Contains("German", StringComparison.OrdinalIgnoreCase) || value.Contains("de", StringComparison.OrdinalIgnoreCase) || value.Contains("德", StringComparison.OrdinalIgnoreCase)) return "German";
        if (value.Contains("Spanish", StringComparison.OrdinalIgnoreCase) || value.Contains("es", StringComparison.OrdinalIgnoreCase) || value.Contains("西", StringComparison.OrdinalIgnoreCase)) return "Spanish";
        if (value.Contains("Chinese", StringComparison.OrdinalIgnoreCase) || value.Contains("zh", StringComparison.OrdinalIgnoreCase) || value.Contains("中", StringComparison.OrdinalIgnoreCase)) return "Chinese (Simplified)";
        return value.Trim();
    }
}
