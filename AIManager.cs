using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GTA5MOD2026
{
    public class AIManager : IDisposable
    {
        private static readonly HttpClient _http = new HttpClient()
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };

        private readonly ConcurrentQueue<Action> _mainQueue
            = new ConcurrentQueue<Action>();

        private readonly ConcurrentDictionary<int, bool>
            _npcPending
                = new ConcurrentDictionary<int, bool>();

        private readonly SemaphoreSlim _semaphore
            = new SemaphoreSlim(2, 2);

        public ModConfig Config { get; private set; }

        public string ModelName =>
            Config.LLM.Provider == "cloud"
                ? Config.LLM.CloudModel
                : Config.LLM.LocalModel;

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments),
            "GTA5MOD2026");
        private static readonly bool EnableRawResponseDump = false;
        private static readonly bool EnableStreamChunkLog = false;

        public AIManager()
        {
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12;

            Config = ModConfig.Load();
        }

        public void ProcessMainQueue()
        {
            int count = 0;
            while (_mainQueue.TryDequeue(out var action)
                && count < 5)
            {
                count++;
                try { action?.Invoke(); }
                catch { }
            }
        }

        public bool IsNpcPending(int npcHandle)
            => _npcPending.ContainsKey(npcHandle);

        private sealed class RequestBuildResult
        {
            public HttpRequestMessage Request { get; set; }
            public string Payload { get; set; }
            public string Endpoint { get; set; }
            public string Model { get; set; }
        }

        private string ResolveCurrentModel(JObject payloadObj)
        {
            if (Config.LLM.Provider == "cloud")
                return Config.LLM.CloudModel;

            string currentModel = Config.LLM.LocalModel;
            try
            {
                var modelToken = payloadObj["model"];
                if (modelToken != null)
                    currentModel = modelToken.ToString();
            }
            catch { }
            return currentModel;
        }

        private RequestBuildResult BuildRequest(
            string jsonPayload, bool stream)
        {
            var payloadObj = JObject.Parse(jsonPayload);
            if (stream)
                payloadObj["stream"] = true;
            else
                payloadObj.Remove("stream");

            string endpoint = Config.LLM.Provider == "cloud"
                ? Config.LLM.CloudEndpoint
                : Config.LLM.LocalEndpoint;

            string currentModel = ResolveCurrentModel(payloadObj);
            bool isQwen3 = currentModel.ToLower().Contains("qwen3");

            if (isQwen3 && Config.LLM.Provider != "cloud")
            {
                payloadObj["enable_thinking"] = false;
                payloadObj["chat_template_kwargs"] = new JObject
                {
                    ["enable_thinking"] = false
                };
                payloadObj.Remove("extra_body");
            }
            else
            {
                payloadObj.Remove("enable_thinking");
                payloadObj.Remove("chat_template_kwargs");
                payloadObj.Remove("extra_body");
            }

            string cleanPayload = payloadObj
                .ToString(Newtonsoft.Json.Formatting.None);

            var request = new HttpRequestMessage(
                HttpMethod.Post, endpoint);
            request.Content = new StringContent(
                cleanPayload, Encoding.UTF8, "application/json");

            if (Config.LLM.Provider == "cloud"
                && !string.IsNullOrEmpty(Config.LLM.CloudAPIKey))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer", Config.LLM.CloudAPIKey.Trim());
            }

            return new RequestBuildResult
            {
                Request = request,
                Payload = cleanPayload,
                Endpoint = endpoint,
                Model = currentModel
            };
        }

        public async Task<string> PostRawAsync(string endpoint,
            string jsonPayload, int timeoutSeconds = 30)
        {
            using (var cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(timeoutSeconds)))
            using (var request = new HttpRequestMessage(
                HttpMethod.Post, endpoint))
            {
                request.Content = new StringContent(
                    jsonPayload, Encoding.UTF8,
                    "application/json");

                if (Config.LLM.Provider == "cloud"
                    && !string.IsNullOrEmpty(Config.LLM.CloudAPIKey))
                {
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue(
                            "Bearer",
                            Config.LLM.CloudAPIKey.Trim());
                }

                using (var resp = await _http.SendAsync(
                    request, cts.Token).ConfigureAwait(false))
                {
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadAsStringAsync()
                        .ConfigureAwait(false);
                }
            }
        }

        public AIResponse ParsePublic(string body)
        {
            return ParseAIResponse(body);
        }

        public void RequestForNpcAsync(
            int npcHandle,
            string jsonPayload,
            Action<AIResponse> onSuccess,
            Action<Exception> onError = null)
        {
            if (!_npcPending.TryAdd(npcHandle, true))
                return;

            DebugLog($"Sending:\n{jsonPayload}");

            Task.Run(async () =>
            {
                try
                {
                    await _semaphore.WaitAsync()
                        .ConfigureAwait(false);
                    try
                    {
                        // 45 second timeout — enough for Qwen3.5
                        using (var cts
                            = new CancellationTokenSource(
                                TimeSpan.FromSeconds(45)))
                        {
                            var build = BuildRequest(
                                jsonPayload, false);
                            DebugLog($"=== REQUEST ===");
                            DebugLog($"Provider: {Config.LLM.Provider}");
                            DebugLog($"Endpoint: {build.Endpoint}");
                            DebugLog($"API Key: {(string.IsNullOrEmpty(Config.LLM.CloudAPIKey) ? "EMPTY!" : Config.LLM.CloudAPIKey.Substring(0, Math.Min(8, Config.LLM.CloudAPIKey.Length)) + "...")}");
                            DebugLog($"Model: {build.Model}");
                            DebugLog($"Payload: {build.Payload}");

                            using (var request = build.Request)
                            {
                                using (var resp = await _http.SendAsync(
                                    request, cts.Token)
                                    .ConfigureAwait(false))
                                {
                                    resp.EnsureSuccessStatusCode();

                                    var body = await resp.Content
                                        .ReadAsStringAsync()
                                        .ConfigureAwait(false);

                                    DebugLog($"Raw response:\n{body}");

                                    var aiResp = ParseAIResponse(body);
                                    aiResp.NpcHandle = npcHandle;

                                    _mainQueue.Enqueue(()
                                        => onSuccess?.Invoke(aiResp));
                                }
                            }
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"Error: {ex.Message}");
                    _mainQueue.Enqueue(()
                        => onError?.Invoke(ex));
                }
                finally
                {
                    _npcPending.TryRemove(npcHandle, out _);
                }
            });
        }

        public void StreamForNpcAsync(
            int npcHandle,
            string jsonPayload,
            Action<string> onPartialDialogue,
            Action<AIResponse> onComplete,
            Action<Exception> onError = null)
        {
            if (!_npcPending.TryAdd(npcHandle, true))
                return;

            Task.Run(async () =>
            {
                try
                {
                    await _semaphore.WaitAsync()
                        .ConfigureAwait(false);
                    try
                    {
                        var build = BuildRequest(
                            jsonPayload, true);

                        using (var request = build.Request)
                        {
                            using (var cts
                                = new CancellationTokenSource(
                                    TimeSpan.FromSeconds(45)))
                            {
                                using (var resp = await _http.SendAsync(
                                    request,
                                    HttpCompletionOption
                                        .ResponseHeadersRead,
                                    cts.Token)
                                    .ConfigureAwait(false))
                                {
                                    resp.EnsureSuccessStatusCode();

                                    var fullContent = new StringBuilder();
                                    var fullReasoning = new StringBuilder();
                                    var chunkLog = new StringBuilder();
                                    string lastDialogue = "";

                                    using (var stream = await resp.Content
                                        .ReadAsStreamAsync()
                                        .ConfigureAwait(false))
                                    using (var reader = new StreamReader(
                                        stream, Encoding.UTF8))
                                    {
                                        string line;
                                        while ((line = await reader
                                            .ReadLineAsync()
                                            .ConfigureAwait(false))
                                            != null)
                                        {
                                            if (cts.Token
                                                .IsCancellationRequested)
                                                break;

                                            if (!line.StartsWith("data: "))
                                                continue;

                                            string data = line
                                                .Substring(6).Trim();

                                            if (data == "[DONE]") break;

                                            try
                                            {
                                                var chunk = JObject
                                                    .Parse(data);
                                                var delta =
                                                    chunk["choices"]?[0]
                                                    ?["delta"];
                                                if (EnableStreamChunkLog)
                                                {
                                                    chunkLog.Append('[')
                                                        .Append(DateTime.Now)
                                                        .Append("] CHUNK: ")
                                                        .AppendLine(data);
                                                }

                                                if (delta == null) continue;

                                                string cd = delta["content"]
                                                    ?.ToString();
                                                string rd = delta[
                                                    "reasoning_content"]
                                                    ?.ToString();
                                                string token = cd;
                                                if (string.IsNullOrEmpty(token))
                                                    token = rd;

                                                if (!string.IsNullOrEmpty(token))
                                                    fullContent.Append(token);
                                                if (!string.IsNullOrEmpty(rd))
                                                    fullReasoning.Append(rd);

                                                string current =
                                                    fullContent.Length > 0
                                                        ? fullContent.ToString()
                                                        : fullReasoning.ToString();

                                                string dialogue =
                                                    ExtractPartialDialogue(
                                                        current);

                                                if (!string.IsNullOrEmpty(
                                                        dialogue)
                                                    && dialogue != lastDialogue)
                                                {
                                                    lastDialogue = dialogue;
                                                    _mainQueue.Enqueue(()
                                                        => onPartialDialogue
                                                            ?.Invoke(dialogue));
                                                }
                                            }
                                            catch { }
                                        }

                                        if (EnableStreamChunkLog
                                            && chunkLog.Length > 0)
                                        {
                                            try
                                            {
                                                string chunkPath = Path.Combine(
                                                    Environment.GetFolderPath(
                                                        Environment.SpecialFolder.MyDocuments),
                                                    "GTA5MOD2026", "stream_chunks.txt");
                                                File.AppendAllText(chunkPath,
                                                    chunkLog.ToString(),
                                                    Encoding.UTF8);
                                            }
                                            catch { }
                                        }
                                    }

                                    string finalText =
                                        fullContent.Length > 0
                                            ? fullContent.ToString()
                                            : fullReasoning.ToString();
                                    DebugLog($"Stream final text: [{finalText}]");

                                    finalText = StripThinkingTags(finalText);
                                    DebugLog($"After strip: [{finalText}]");

                                    var result = ParsePipeFormat(finalText);
                                    result.NpcHandle = npcHandle;

                                    _mainQueue.Enqueue(()
                                        => onComplete?.Invoke(result));
                                }
                            }
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                catch (Exception ex)
                {
                    _mainQueue.Enqueue(()
                        => onError?.Invoke(ex));
                }
                finally
                {
                    _npcPending.TryRemove(npcHandle, out _);
                }
            });
        }

        private string ExtractPartialDialogue(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            text = StripThinkingTags(text).Trim();
            text = StripNarration(text).Trim();
            if (string.IsNullOrEmpty(text)) return null;

            // ===== REJECT if it still looks like thinking =====
            if (text.StartsWith("Thinking", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("1.", StringComparison.Ordinal)
                || text.StartsWith("**", StringComparison.Ordinal)
                || text.StartsWith("Analysis", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Let me", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // ===== PIPE FORMAT: "你好啊！|speak|happy" =====
            int pipeIdx = text.IndexOf('|');
            if (pipeIdx > 0)
            {
                string beforePipe = text.Substring(0, pipeIdx).Trim();
                // Make sure it's actual dialogue, not JSON
                if (!beforePipe.Contains("{") 
                    && !beforePipe.Contains("\"")
                    && ContainsChinese(beforePipe))
                {
                    return beforePipe;
                }
            }

            // ===== JSON FORMAT: extract "d" field during streaming =====
            if (text.Contains("{"))
            {
                // Try to extract dialogue from partial/complete JSON
                // Look for "d" : "..." or "dialogue" : "..."
                string extracted = TryExtractJsonDialogue(text);
                if (!string.IsNullOrEmpty(extracted) 
                    && ContainsChinese(extracted))
                {
                    return extracted;
                }
                // JSON detected but can't extract yet — show nothing
                return null;
            }

            // ===== PLAIN TEXT (no pipe, no JSON) =====
            // Only show if it contains Chinese and looks like dialogue
            if (ContainsChinese(text) 
                && !IsContextLeak(text)
                && text.Length <= 30)
            {
                return text;
            }

            return null;
        }

        /// <summary>
        /// Extract dialogue value from partial or complete JSON
        /// Handles: {"d":"你好"} or { "d" : "你好", "a"...
        /// Works even with incomplete JSON during streaming
        /// </summary>
        private string TryExtractJsonDialogue(string text)
        {
            // Try keys: "d", "dialogue"
            string[] keys = new[] { "\"d\"", "\"dialogue\"" };

            foreach (var key in keys)
            {
                int keyIdx = text.IndexOf(key);
                if (keyIdx < 0) continue;

                // Find colon after key
                int colonIdx = text.IndexOf(':', keyIdx + key.Length);
                if (colonIdx < 0) continue;

                // Find opening quote of value
                int quoteStart = text.IndexOf('"', colonIdx + 1);
                if (quoteStart < 0) continue;

                // Find closing quote (handle escaped quotes)
                int pos = quoteStart + 1;
                while (pos < text.Length)
                {
                    if (text[pos] == '"' && text[pos - 1] != '\\')
                        break;
                    pos++;
                }

                if (pos < text.Length)
                {
                    // Found complete quoted value
                    string value = text.Substring(
                        quoteStart + 1, pos - quoteStart - 1);
                    if (!string.IsNullOrWhiteSpace(value))
                        return value.Trim();
                }
                else
                {
                    // Incomplete — quote not closed yet (still streaming)
                    // Show what we have so far
                    string partial = text.Substring(quoteStart + 1);
                    if (partial.Length > 1 && ContainsChinese(partial))
                        return partial.Trim();
                }
            }

            return null;
        }

        private string ValidateAndTruncateDialogue(
            string dialogue, int maxLen, bool keepSpecialTokens)
        {
            if (string.IsNullOrWhiteSpace(dialogue))
                return "";

            if (dialogue == "...")
                return dialogue;
            if (keepSpecialTokens
                && (dialogue == "[无响应]"
                    || dialogue == "[解析失败]"))
                return dialogue;

            if (dialogue.Length > maxLen)
            {
                string cut = dialogue.Substring(0, maxLen);
                int lastPunc = cut.LastIndexOfAny(
                    new[] { '，', '。', '！', '？',
                            '…', '~', ',', '!', '?' });
                dialogue = lastPunc > maxLen / 2
                    ? cut.Substring(0, lastPunc + 1)
                    : cut;
            }

            if (!ContainsChinese(dialogue))
            {
                DebugLog($"Non-Chinese rejected: [{dialogue}]");
                return "";
            }

            if (IsContextLeak(dialogue))
            {
                DebugLog($"Context leak rejected: [{dialogue}]");
                return "";
            }

            return dialogue;
        }

        private AIResponse ParsePipeFormat(string text)
        {
            var result = new AIResponse
            {
                action = "speak",
                dialogue = "",
                emotion = "neutral"
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                result.dialogue = "...";
                return result;
            }

            text = StripNarration(text);
            text = text.Trim();

            string[] parts = text.Split('|');

            if (parts.Length >= 2)
            {
                result.dialogue = parts[0].Trim();

                string action = parts[1].Trim().ToLower();
                if (action.StartsWith("spe")) action = "speak";
                else if (action.StartsWith("id")) action = "idle";
                else if (action.StartsWith("wa")) action = "wave";
                else if (action.StartsWith("fl")) action = "flee";
                else if (action.StartsWith("wal")) action = "walk";
                switch (action)
                {
                    case "speak":
                    case "idle":
                    case "wave":
                    case "flee":
                    case "walk":
                        result.action = action; break;
                    default:
                        result.action = "speak"; break;
                }

                if (parts.Length >= 3)
                {
                    string emotion = parts[2].Trim().ToLower();
                    if (emotion.StartsWith("hap")) emotion = "happy";
                    else if (emotion.StartsWith("ang")) emotion = "angry";
                    else if (emotion.StartsWith("sa")) emotion = "sad";
                    else if (emotion.StartsWith("sca")) emotion = "scared";
                    else if (emotion.StartsWith("neu")) emotion = "neutral";
                    switch (emotion)
                    {
                        case "happy":
                        case "angry":
                        case "sad":
                        case "scared":
                        case "neutral":
                            result.emotion = emotion; break;
                        default:
                            result.emotion = "neutral"; break;
                    }
                }
            }
            else
            {
                int firstBrace = text.IndexOf('{');
                int lastBrace = text.LastIndexOf('}');

                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    string jsonStr = text.Substring(
                        firstBrace, lastBrace - firstBrace + 1);

                    jsonStr = jsonStr
                        .Replace(",\n}", "}").Replace(",}", "}");

                    int openB = 0, closeB = 0;
                    foreach (char c in jsonStr)
                    {
                        if (c == '{') openB++;
                        if (c == '}') closeB++;
                    }
                    jsonStr = jsonStr.TrimEnd();
                    if (jsonStr.EndsWith(","))
                        jsonStr = jsonStr.Substring(
                            0, jsonStr.Length - 1);
                    while (closeB < openB)
                    { jsonStr += "}"; closeB++; }

                    try
                    {
                        var parsed = JObject.Parse(jsonStr);
                        result.action =
                            (parsed["action"] ?? parsed["a"])
                            ?.ToString()?.Trim() ?? "speak";
                        result.dialogue =
                            (parsed["dialogue"] ?? parsed["d"])
                            ?.ToString()?.Trim() ?? "";
                        result.emotion =
                            (parsed["emotion"] ?? parsed["e"])
                            ?.ToString()?.Trim() ?? "neutral";
                    }
                    catch
                    {
                        result.dialogue = ExtractDialogue(text);
                    }
                }
                else
                {
                    string plainText = text.Trim();
                    int newlineIdx = plainText.IndexOfAny(
                        new[] { '\n', '\r' });
                    if (newlineIdx > 0)
                        plainText = plainText.Substring(
                            0, newlineIdx).Trim();

                    int plainMaxLen = Config.Performance
                        .MaxDialogueLength;
                    if (plainText.Length > plainMaxLen)
                    {
                        int lastPunc = plainText.Substring(
                            0, plainMaxLen).LastIndexOfAny(
                            new[] { '，', '。', '！', '？', '…' });
                        plainText = lastPunc > plainMaxLen / 2
                            ? plainText.Substring(0, lastPunc + 1)
                            : plainText.Substring(0, plainMaxLen);
                    }

                    result.dialogue = plainText;
                    result.action = "speak";
                    result.emotion = "neutral";
                }
            }

            int maxLen = Config.Performance.MaxDialogueLength;
            result.dialogue = ValidateAndTruncateDialogue(
                result.dialogue, maxLen, false);

            if (string.IsNullOrWhiteSpace(result.dialogue))
                result.dialogue = "...";

            return result;
        }

        private AIResponse ParseAIResponse(string body)
        {
            var result = new AIResponse
            {
                action = "speak",
                dialogue = "",
                emotion = "neutral"
            };

            try
            {
                var jRoot = JObject.Parse(body);
                var message = jRoot["choices"]?[0]?["message"];
                if (EnableRawResponseDump)
                {
                    try
                    {
                        string dumpPath = Path.Combine(
                            LogDir, "raw_response_dump.txt");
                        File.WriteAllText(dumpPath,
                            $"[{DateTime.Now}]\n" +
                            $"FULL RESPONSE:\n{body}\n\n" +
                            $"MESSAGE OBJECT:\n{message?.ToString() ?? "NULL"}\n\n" +
                            $"ALL FIELDS:\n",
                            Encoding.UTF8);

                        if (message != null)
                        {
                            foreach (var prop in message.Children<JProperty>())
                            {
                                File.AppendAllText(dumpPath,
                                    $"  {prop.Name} = [{prop.Value}]\n",
                                    Encoding.UTF8);
                            }
                        }
                    }
                    catch { }
                }

                if (message == null)
                {
                    DebugLog("No message in response");
                    result.dialogue = "[无响应]";
                    return result;
                }

                string contentField = message["content"]?.ToString();
                string reasoningField = message["reasoning_content"]?.ToString();

                // Try ALL possible content fields
                // Different models/versions use different names
                string text = null;

                string[] fieldNames = new[]
                {
                    "content",
                    "reasoning_content",
                    "reasoning",
                    "text",
                    "response"
                };

                foreach (var field in fieldNames)
                {
                    string val = message[field]?.ToString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        text = val;
                        DebugLog($"Found text in field: {field}");
                        break;
                    }
                }

                // Last resort: dump ALL message fields
                if (string.IsNullOrWhiteSpace(text))
                {
                    // Try to get any string value from message
                    foreach (var prop in message.Children<JProperty>())
                    {
                        if (prop.Name == "role") continue;
                        if (prop.Name == "tool_calls") continue;

                        string val = prop.Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)
                            && val.Length > 2)
                        {
                            text = val;
                            DebugLog(
                                $"Found text in unknown field: " +
                                $"{prop.Name}");
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    DebugLog($"All fields empty. " +
                        $"Message: {message}");
                    result.dialogue = "[无响应]";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(text)
                    || text == "..."
                    || text.Trim().Length < 3)
                {
                    if (!string.IsNullOrWhiteSpace(reasoningField))
                    {
                        string fallback = StripThinkingTags(reasoningField);
                        var lines = fallback.Split(
                            new[] { '\n' },
                            StringSplitOptions.RemoveEmptyEntries);
                        string last = null;
                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if (trimmed.Contains("|")
                                || trimmed.Length > 2)
                                last = trimmed;
                        }
                        if (!string.IsNullOrWhiteSpace(last))
                            fallback = last;
                        text = fallback;
                    }
                }

                // Strip thinking tags
                text = StripThinkingTags(text);
                text = StripNarration(text);

                DebugLog($"Cleaned text: {text}");

                // Extract JSON
                int firstBrace = text.IndexOf('{');
                int lastBrace = text.LastIndexOf('}');

                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    string jsonStr = text.Substring(
                        firstBrace,
                        lastBrace - firstBrace + 1);

                    // Clean malformed JSON
                    jsonStr = jsonStr
                        .Replace(",\n}", "}")
                        .Replace(",\r\n}", "}")
                        .Replace(", }", "}")
                        .Replace(",}", "}");

                    // Fix truncated JSON — add missing closing brace
                    int openBraces = 0;
                    int closeBraces = 0;
                    foreach (char c in jsonStr)
                    {
                        if (c == '{') openBraces++;
                        if (c == '}') closeBraces++;
                    }
                    // Trim trailing whitespace/tabs first
                    jsonStr = jsonStr.TrimEnd();
                    // Remove trailing comma if exists
                    if (jsonStr.EndsWith(","))
                        jsonStr = jsonStr.Substring(0, jsonStr.Length - 1);
                    while (closeBraces < openBraces)
                    {
                        jsonStr += "}";
                        closeBraces++;
                    }

                    try
                    {
                        var parsed = JObject.Parse(jsonStr);

                        result.action =
                            (parsed["a"] ?? parsed["action"])
                            ?.ToString()?.Trim() ?? "speak";
                        result.dialogue =
                            (parsed["d"] ?? parsed["dialogue"])
                            ?.ToString()?.Trim() ?? "";
                        result.emotion =
                            (parsed["e"] ?? parsed["emotion"])
                            ?.ToString()?.Trim() ?? "neutral";
                    }
                    catch
                    {
                        // JSON parse failed — use raw text
                        DebugLog(
                            $"JSON parse failed: {jsonStr}");
                        result.dialogue = ExtractDialogue(text);
                    }
                }
                else
                {
                    // No JSON braces — try to extract dialogue
                    result.dialogue = ExtractDialogue(text);
                    result.action = "speak";
                }

                // Validate action
                switch (result.action)
                {
                    case "idle":
                    case "wave":
                    case "speak":
                    case "flee":
                    case "walk_to":
                        break;
                    default:
                        result.action = "speak";
                        break;
                }

                int maxLen = Config.Performance
                    .MaxDialogueLength;
                result.dialogue = ValidateAndTruncateDialogue(
                    result.dialogue, maxLen, true);

                if (string.IsNullOrWhiteSpace(result.dialogue))
                {
                    result.dialogue = "...";
                }

                DebugLog(
                    $"Final: action={result.action} " +
                    $"dialogue={result.dialogue} " +
                    $"emotion={result.emotion}");
            }
            catch (Exception ex)
            {
                DebugLog($"Parse exception: {ex.Message}");
                result.dialogue = "[解析失败]";
            }

            return result;
        }

        /// <summary>
        /// Try to extract just the dialogue text
        /// when JSON parsing completely fails
        /// </summary>
        private string ExtractDialogue(string text)
        {
            // Try to find dialogue value
            int dIdx = text.IndexOf("dialogue");
            if (dIdx < 0) dIdx = text.IndexOf("\"d\"");

            if (dIdx >= 0)
            {
                // Find the value after the key
                int colonIdx = text.IndexOf(':', dIdx);
                if (colonIdx >= 0)
                {
                    int quoteStart = text.IndexOf('"',
                        colonIdx + 1);
                    if (quoteStart >= 0)
                    {
                        int quoteEnd = text.IndexOf('"',
                            quoteStart + 1);
                        if (quoteEnd > quoteStart)
                        {
                            return text.Substring(
                                quoteStart + 1,
                                quoteEnd - quoteStart - 1);
                        }
                    }
                }
            }

            // Just return any Chinese text found
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c >= 0x4e00 && c <= 0x9fff)
                    sb.Append(c);
                else if (sb.Length > 0
                    && (c == '！' || c == '？'
                        || c == '…' || c == '，'))
                    sb.Append(c);
            }

            string chinese = sb.ToString();
            if (chinese.Length > 0)
                return chinese.Length > 20
                    ? chinese.Substring(0, 20)
                    : chinese;

            return "...";
        }

        private string StripThinkingTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string working = text;

            // ===== PASS 1: Remove XML <think>...</think> tags =====
            string outsideContent = "";
            string insideContent = "";

            while (true)
            {
                int start = working.IndexOf("<think>",
                    StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                {
                    outsideContent += working;
                    break;
                }

                outsideContent += working.Substring(0, start);

                int end = working.IndexOf("</think>", start,
                    StringComparison.OrdinalIgnoreCase);

                if (end < 0)
                {
                    insideContent += working.Substring(
                        start + "<think>".Length);
                    break;
                }

                insideContent += working.Substring(
                    start + "<think>".Length,
                    end - start - "<think>".Length);

                working = working.Substring(
                    end + "</think>".Length);
            }

            working = outsideContent.Trim();

            // If nothing outside tags, use inside content
            if (string.IsNullOrWhiteSpace(working)
                && !string.IsNullOrWhiteSpace(insideContent))
            {
                working = insideContent.Trim();
            }

            // ===== PASS 2: Remove plain text thinking blocks =====
            string[] thinkingHeaders = new[]
            {
                "Thinking Process:",
                "Thinking process:",
                "thinking process:",
                "Think:",
                "Analysis:",
                "Reasoning:",
                "Thought:",
                "Let me think",
                "Let me analyze",
                "[Thinking]",
                "思考过程:",
                "分析：",
                "思考：",
                "**Analyze",
                "1.  **Analyze",
                "1. **Analyze"
            };

            foreach (var header in thinkingHeaders)
            {
                int headerIdx = working.IndexOf(header,
                    StringComparison.OrdinalIgnoreCase);

                if (headerIdx < 0) continue;

                // Content before thinking
                string beforeThinking = working
                    .Substring(0, headerIdx).Trim();

                // Everything after header
                string afterHeader = working.Substring(headerIdx);

                // Try to find actual answer within thinking
                string answer = ExtractAnswerFromThinking(
                    afterHeader);

                if (!string.IsNullOrEmpty(answer))
                {
                    working = answer;
                }
                else if (!string.IsNullOrEmpty(beforeThinking)
                    && ContainsChinese(beforeThinking))
                {
                    working = beforeThinking;
                }
                else
                {
                    // Entire output is thinking — no useful content
                    DebugLog($"All thinking, no answer: [{working.Substring(0, Math.Min(80, working.Length))}]");
                    working = "";
                }
                break;
            }

            return working.Trim('\n', '\r', ' ', '\t');
        }

        private string StripNarration(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string working = text;

            while (true)
            {
                int start = working.IndexOf('*');
                if (start < 0) break;
                int end = working.IndexOf('*', start + 1);
                if (end < 0) break;
                working = working.Substring(0, start)
                    + working.Substring(end + 1);
            }

            while (true)
            {
                int start = working.IndexOf('（');
                if (start < 0) break;
                int end = working.IndexOf('）', start + 1);
                if (end < 0) break;
                working = working.Substring(0, start)
                    + working.Substring(end + 1);
            }

            while (true)
            {
                int start = working.IndexOf('(');
                if (start < 0) break;
                int end = working.IndexOf(')', start + 1);
                if (end < 0) break;
                string inside = working.Substring(start + 1,
                    end - start - 1);
                if (inside.Length < 20)
                {
                    working = working.Substring(0, start)
                        + working.Substring(end + 1);
                }
                else
                {
                    break;
                }
            }

            string[] prefixes = new[]
            {
                "我想了想，", "我决定", "我看着他，",
                "我微笑着说：", "我冷冷地说：", "我说：",
                "他说：", "她说：", "我回答：", "我回答道：",
                "我叹了口气，", "我皱眉说：",
                "I think", "I say", "I smile",
                "Let me", "Sure,", "Okay,"
            };

            string trimmed = working.Trim();
            foreach (var prefix in prefixes)
            {
                if (!trimmed.StartsWith(prefix,
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                trimmed = trimmed.Substring(prefix.Length).Trim();
                while (trimmed.Length > 0
                    && (trimmed[0] == '，'
                        || trimmed[0] == '"'
                        || trimmed[0] == '\''
                        || trimmed[0] == ':'
                        || trimmed[0] == '：'))
                {
                    trimmed = trimmed.Substring(1);
                }
                break;
            }

            return trimmed.Trim();
        }

        /// <summary>
        /// Extract the actual answer line from thinking text.
        /// Searches for pipe format or clean Chinese dialogue.
        /// </summary>
        private string ExtractAnswerFromThinking(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            string[] lines = text.Split(
                new[] { '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);

            // Search from END — answer is usually last line
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(line)) continue;

                // Skip markdown and analysis lines
                if (line.StartsWith("*")
                    || line.StartsWith("-")
                    || line.StartsWith("#")
                    || line.StartsWith("1.")
                    || line.StartsWith("2.")
                    || line.StartsWith("3.")
                    || line.Contains("**")
                    || line.Contains("Analyze")
                    || line.Contains("analyze")
                    || line.Contains("Process")
                    || line.Contains("process")
                    || line.Contains("Role:")
                    || line.Contains("Language:")
                    || line.Contains("Format:")
                    || line.Contains("Action")
                    || line.Contains("Emotion"))
                    continue;

                // Pipe format — definitely the answer
                if (line.Contains("|") && ContainsChinese(line))
                    return line;

                // Clean Chinese text, short enough to be dialogue
                if (ContainsChinese(line)
                    && line.Length < 40
                    && !line.Contains("{")
                    && !line.Contains("}"))
                    return line;
            }

            return null;
        }

        private bool ContainsChinese(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (c >= 0x4e00 && c <= 0x9fff) return true;
                if (c >= 0x3400 && c <= 0x4dbf) return true;
            }
            return false;
        }

        private bool IsContextLeak(string dialogue)
        {
            if (string.IsNullOrWhiteSpace(dialogue)) return true;

            string d = dialogue.Trim();

            // Exact match leak patterns
            string[] leakPatterns = new[]
            {
                "玩家说", "玩家回答", "Player", "player",
                "stranger", "acquaintance", "friend", "enemy",
                "hostile", "close_friend",
                "speak", "idle", "wave", "flee",
                "happy", "angry", "sad", "scared", "neutral",
                "action", "emotion", "dialogue",
                "Personality", "personality",
                "关系:", "性格:", "外貌:", "好感度:",
                "NPC", "GTA", "format", "Format",
                "JSON", "json",
                "Thinking", "thinking", "Process", "process",
                "Analyze", "analyze", "Analysis"
            };

            foreach (var pattern in leakPatterns)
            {
                if (d.Equals(pattern,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Short English-only text = probably leaked variable
            if (d.Length <= 6 && !ContainsChinese(d))
                return true;

            // Starts with number + contains "玩家"
            if (d.StartsWith("0") && d.Contains("玩家"))
                return true;

            // Contains markdown formatting = thinking leak
            if (d.Contains("**") || d.Contains("##"))
                return true;

            return false;
        }

        private void DebugLog(string message)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(LogDir, "debug_api.log"),
                    $"\n[{DateTime.Now}] {message}\n",
                    Encoding.UTF8);
            }
            catch { }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}
