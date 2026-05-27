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
            IsCloudProvider()
                ? Config.LLM.CloudModel
                : Config.LLM.LocalModel;

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments),
            "GTA5MOD2026");
        private static readonly bool EnableRawResponseDump = false;
        private static readonly bool EnableStreamChunkLog = false;

        public bool IsCloudProvider()
        {
            return !IsLocalProvider();
        }

        public bool IsLocalProvider()
        {
            return string.Equals(Config.LLM.Provider, "local",
                StringComparison.OrdinalIgnoreCase);
        }

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
            if (IsCloudProvider())
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

        public string ResolveEndpointForModel(string modelName)
        {
            if (IsCloudProvider())
                return Config.LLM.CloudEndpoint;

            if (!string.IsNullOrWhiteSpace(modelName)
                && string.Equals(modelName.Trim(),
                    Config.LLM.LightModel?.Trim(),
                    StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(
                    Config.LLM.LightEndpoint))
            {
                return Config.LLM.LightEndpoint;
            }

            return Config.LLM.LocalEndpoint;
        }

        private RequestBuildResult BuildRequest(
            string jsonPayload, bool stream,
            string endpointOverride = null)
        {
            var payloadObj = JObject.Parse(jsonPayload);
            string currentModel = ResolveCurrentModel(payloadObj);
            bool isQwen3 = !string.IsNullOrWhiteSpace(currentModel)
                && currentModel.ToLower().Contains("qwen3");

            if (stream)
                payloadObj["stream"] = true;
            else
                payloadObj.Remove("stream");

            if (isQwen3 && !IsCloudProvider())
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

            int maxTokens = payloadObj["max_tokens"]?.Value<int?>() ?? 0;
            if (isQwen3
                && Config.Performance.MaxTokensThinking > 0
                && Config.Performance.MaxTokensThinking > maxTokens)
            {
                payloadObj["max_tokens"] =
                    Config.Performance.MaxTokensThinking;
            }

            string endpoint = string.IsNullOrWhiteSpace(endpointOverride)
                ? ResolveEndpointForModel(currentModel)
                : endpointOverride;

            string cleanPayload = payloadObj
                .ToString(Newtonsoft.Json.Formatting.None);

            var request = new HttpRequestMessage(
                HttpMethod.Post, endpoint);
            request.Content = new StringContent(
                cleanPayload, Encoding.UTF8, "application/json");

            if (IsCloudProvider())
            {
                ApplyCloudAuthentication(request,
                    Config.LLM.CloudAPIKey);
            }

            return new RequestBuildResult
            {
                Request = request,
                Payload = cleanPayload,
                Endpoint = endpoint,
                Model = currentModel
            };
        }

        private void ApplyCloudAuthentication(
            HttpRequestMessage request, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return;

            string key = apiKey.Trim();
            int colon = key.IndexOf(':');
            if (colon > 0)
            {
                string headerName = key.Substring(0, colon).Trim();
                string headerValue = key.Substring(colon + 1).Trim();
                if (!string.IsNullOrWhiteSpace(headerName)
                    && !string.IsNullOrWhiteSpace(headerValue))
                {
                    request.Headers.TryAddWithoutValidation(
                        headerName, headerValue);
                    return;
                }
            }

            if (key.StartsWith("Bearer ",
                StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.TryAddWithoutValidation(
                    "Authorization", key);
                return;
            }

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", key);
        }

        public async Task<string> PostRawAsync(string endpoint,
            string jsonPayload, int timeoutSeconds = 30)
        {
            var build = BuildRequest(
                jsonPayload, false, endpoint);
            using (var cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(timeoutSeconds)))
            using (var request = build.Request)
            {
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
            Action<Exception> onError = null,
            string requestId = null,
            string stableId = null,
            int generation = 0)
        {
            if (!_npcPending.TryAdd(npcHandle, true))
            {
                _mainQueue.Enqueue(() => onError?.Invoke(
                    new InvalidOperationException(
                        "NPC request already pending.")));
                return;
            }

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
                                    aiResp.RequestId = requestId;
                                    aiResp.StableId = stableId;
                                    aiResp.HandleSnapshot = npcHandle;
                                    aiResp.Generation = generation;

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
            Action<Exception> onError = null,
            string requestId = null,
            string stableId = null,
            int generation = 0)
        {
            if (!_npcPending.TryAdd(npcHandle, true))
            {
                _mainQueue.Enqueue(() => onError?.Invoke(
                    new InvalidOperationException(
                        "NPC request already pending.")));
                return;
            }

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
                                                if (!string.IsNullOrEmpty(cd))
                                                    fullContent.Append(cd);
                                                if (!string.IsNullOrEmpty(rd))
                                                    fullReasoning.Append(rd);

                                                string dialogue =
                                                    ExtractPartialDialogue(
                                                        fullContent.ToString());

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

                                    finalText = ChooseModelText(
                                        fullContent.ToString(),
                                        fullReasoning.ToString());
                                    DebugLog($"After strip: [{finalText}]");

                                    var result = ParseModelText(
                                        finalText, false);
                                    result.NpcHandle = npcHandle;
                                    result.RequestId = requestId;
                                    result.StableId = stableId;
                                    result.HandleSnapshot = npcHandle;
                                    result.Generation = generation;

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
                    new[] { '，', '。', '！', '？', '…', '~', ',', '!', '?' });
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

        private string ChooseModelText(string contentText,
            string reasoningText)
        {
            string primary = StripThinkingTags(contentText ?? "")
                .Trim();
            if (!string.IsNullOrWhiteSpace(primary))
                return primary;

            string fallback = StripThinkingTags(reasoningText ?? "")
                .Trim();
            if (string.IsNullOrWhiteSpace(fallback))
                return fallback;

            var lines = fallback.Split(
                new[] { '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string candidate = lines[i].Trim();
                if (candidate.Contains("|")
                    || candidate.Contains("{")
                    || ContainsChinese(candidate))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private string NormalizeAction(string action)
        {
            // 全部受支持动作（与 ProcessResponses 的 dispatch 表一一对应）
            // speak / idle / wave / walk / flee / attack / aim / cower /
            // point / nod / shake_head / follow / call_cops / salute /
            // dance / drink
            if (string.IsNullOrWhiteSpace(action))
                return "speak";

            string raw = action.Trim();
            string normalized = raw.ToLowerInvariant()
                .Replace("-", "_").Replace(" ", "_");

            // 1) 精确匹配规范名
            switch (normalized)
            {
                case "speak":
                case "idle":
                case "wave":
                case "walk":
                case "flee":
                case "attack":
                case "aim":
                case "cower":
                case "point":
                case "nod":
                case "shake_head":
                case "follow":
                case "call_cops":
                case "salute":
                case "dance":
                case "drink":
                    return normalized;
                case "walk_to":
                    return "walk";
                case "shakehead":
                case "shake":
                case "no":
                    return "shake_head";
                case "callcops":
                case "call_police":
                case "police":
                    return "call_cops";
                case "fight":
                case "punch":
                case "shoot":
                    return "attack";
                case "yes":
                    return "nod";
            }

            // 2) 英文前缀匹配（覆盖训练数据的常见词汇）
            if (normalized.StartsWith("spe")) return "speak";
            if (normalized.StartsWith("id")) return "idle";
            if (normalized.StartsWith("wal")) return "walk";
            if (normalized.StartsWith("wav")) return "wave";
            if (normalized.StartsWith("fle")
                || normalized.StartsWith("run")
                || normalized.StartsWith("hide")
                || normalized.StartsWith("escape")) return "flee";
            if (normalized.StartsWith("atta")
                || normalized.StartsWith("hit")
                || normalized.StartsWith("kill")
                || normalized.StartsWith("punch")) return "attack";
            if (normalized.StartsWith("aim")
                || normalized.StartsWith("draw_gun")
                || normalized.StartsWith("draw_weapon")
                || normalized.StartsWith("target")
                || normalized.StartsWith("threaten")) return "aim";
            if (normalized.StartsWith("cow")
                || normalized.StartsWith("crouch")
                || normalized.StartsWith("duck")) return "cower";
            if (normalized.StartsWith("poi")) return "point";
            if (normalized.StartsWith("nod")
                || normalized.StartsWith("agree")) return "nod";
            if (normalized.StartsWith("shake")
                || normalized.StartsWith("deny")
                || normalized.StartsWith("refus")) return "shake_head";
            if (normalized.StartsWith("foll")) return "follow";
            if (normalized.StartsWith("call")
                || normalized.StartsWith("phone")
                || normalized.StartsWith("dial")
                || normalized.StartsWith("911")
                || normalized.StartsWith("110")) return "call_cops";
            if (normalized.StartsWith("salu")) return "salute";
            if (normalized.StartsWith("danc")) return "dance";
            if (normalized.StartsWith("drink")
                || normalized.StartsWith("sip")) return "drink";
            if (normalized.StartsWith("smoke")
                || normalized.StartsWith("stand")
                || normalized.StartsWith("wait")
                || normalized.StartsWith("watch")
                || normalized.StartsWith("look")
                || normalized.StartsWith("sit")) return "idle";
            if (normalized.StartsWith("smile")
                || normalized.StartsWith("greet")
                || normalized.StartsWith("clap")
                || normalized.StartsWith("hug")) return "wave";

            // 3) 中文关键词映射
            // call_cops: 报警 / 叫警察
            if (raw.Contains("报警") || raw.Contains("叫警察")
                || raw.Contains("叫条子") || raw.Contains("拨打110")
                || raw.Contains("打110") || raw.Contains("打911"))
                return "call_cops";
            // attack: 攻击/动手
            if (raw.Contains("攻击") || raw.Contains("打他")
                || raw.Contains("揍") || raw.Contains("动手")
                || raw.Contains("开打") || raw.Contains("开枪打")
                || raw.Contains("干掉") || raw.Contains("弄死"))
                return "attack";
            // aim: 瞄准/掏枪
            if (raw.Contains("瞄准") || raw.Contains("举枪")
                || raw.Contains("掏枪") || raw.Contains("拔枪")
                || raw.Contains("亮枪") || raw.Contains("指着枪"))
                return "aim";
            // cower: 蹲下/抱头/捂头
            if (raw.Contains("蹲") || raw.Contains("缩")
                || raw.Contains("抱头") || raw.Contains("捂头")
                || raw.Contains("跪") || raw.Contains("趴下"))
                return "cower";
            // flee: 逃 / 跑开 / 躲
            if (raw.Contains("逃") || raw.Contains("跑开")
                || raw.Contains("跑走") || raw.Contains("躲")
                || raw.Contains("撤") || raw.Contains("退后")
                || raw.Contains("溜") || raw.Contains("闪躲")
                || raw.Contains("窜"))
                return "flee";
            // point: 指
            if (raw.Contains("指着") || raw.Contains("指向")
                || raw.Contains("指了指") || raw.Contains("用手指"))
                return "point";
            // nod: 点头
            if (raw.Contains("点头") || raw.Contains("颔首"))
                return "nod";
            // shake_head: 摇头
            if (raw.Contains("摇头") || raw.Contains("摆头")
                || raw.Contains("否认") || raw.Contains("拒绝"))
                return "shake_head";
            // follow: 跟随
            if (raw.Contains("跟随") || raw.Contains("跟着")
                || raw.Contains("跟上") || raw.Contains("追上"))
                return "follow";
            // salute: 敬礼
            if (raw.Contains("敬礼") || raw.Contains("行礼"))
                return "salute";
            // dance: 跳舞
            if (raw.Contains("跳舞") || raw.Contains("起舞")
                || raw.Contains("舞蹈") || raw.Contains("扭动"))
                return "dance";
            // drink: 喝酒/喝水
            if (raw.Contains("喝") || raw.Contains("饮")
                || raw.Contains("灌") || raw.Contains("举杯"))
                return "drink";
            // wave: 招手 / 挥手 / 拥抱 / 微笑 (剩下的友好手势)
            if (raw.Contains("挥") || raw.Contains("招手")
                || raw.Contains("微笑") || raw.Contains("笑")
                || raw.Contains("拥抱") || raw.Contains("击掌")
                || raw.Contains("打招呼") || raw.Contains("鞠躬"))
                return "wave";
            // idle: 抽烟 / 等 / 观察 / 沉默
            if (raw.Contains("抽烟") || raw.Contains("抽")
                || raw.Contains("等") || raw.Contains("观察")
                || raw.Contains("打量") || raw.Contains("看着")
                || raw.Contains("站") || raw.Contains("坐")
                || raw.Contains("停") || raw.Contains("沉默")
                || raw.Contains("发呆") || raw.Contains("不动")
                || raw.Contains("无言") || raw.Contains("沉思")
                || raw.Contains("等待") || raw.Contains("打电话")
                || raw.Contains("掏手机"))
                return "idle";
            // walk: 移动（非逃跑）
            if (raw.Contains("走过") || raw.Contains("走向")
                || raw.Contains("走开") || raw.Contains("迈步")
                || raw.Contains("走") || raw.Contains("行走"))
                return "walk";

            return "speak";
        }

        private string NormalizeEmotion(string emotion)
        {
            // 全部受支持情绪：
            //   neutral / happy / angry / sad / scared / surprise / disgust
            // 兼容输入别名：fear -> scared
            if (string.IsNullOrWhiteSpace(emotion))
                return "neutral";

            string raw = emotion.Trim();
            string normalized = raw.ToLowerInvariant()
                .Replace("-", "_").Replace(" ", "_");

            // 1) 精确匹配规范名 + 常见别名
            switch (normalized)
            {
                case "neutral":
                case "happy":
                case "angry":
                case "sad":
                case "scared":
                case "surprise":
                case "disgust":
                    return normalized;
                case "fear":
                case "fearful":
                case "afraid":
                    return "scared";
                case "surprised":
                case "shocked":
                case "astonished":
                    return "surprise";
                case "disgusted":
                case "revolted":
                    return "disgust";
            }

            // 2) 英文前缀匹配
            if (normalized.StartsWith("sca")
                || normalized.StartsWith("fea")
                || normalized.StartsWith("anx")
                || normalized.StartsWith("worry")
                || normalized.StartsWith("nerv")
                || normalized.StartsWith("afraid"))
                return "scared";
            if (normalized.StartsWith("hap")
                || normalized.StartsWith("joy")
                || normalized.StartsWith("excit")
                || normalized.StartsWith("plea"))
                return "happy";
            if (normalized.StartsWith("ang")
                || normalized.StartsWith("rage")
                || normalized.StartsWith("furi")
                || normalized.StartsWith("hate")
                || normalized.StartsWith("annoy")
                || normalized.StartsWith("hostile"))
                return "angry";
            if (normalized.StartsWith("sa")
                || normalized.StartsWith("dep")
                || normalized.StartsWith("sorrow")
                || normalized.StartsWith("grief"))
                return "sad";
            if (normalized.StartsWith("sur")
                || normalized.StartsWith("shock")
                || normalized.StartsWith("amaze")
                || normalized.StartsWith("aston"))
                return "surprise";
            if (normalized.StartsWith("dis")
                || normalized.StartsWith("revolt")
                || normalized.StartsWith("repuls"))
                return "disgust";
            if (normalized.StartsWith("neu")
                || normalized.StartsWith("calm")
                || normalized.StartsWith("normal")
                || normalized.StartsWith("ind"))
                return "neutral";

            // 3) 中文关键词映射
            // disgust: 厌恶 / 反感 / 恶心 / 嫌弃
            if (raw.Contains("厌恶") || raw.Contains("反感")
                || raw.Contains("恶心") || raw.Contains("嫌弃")
                || raw.Contains("讨厌") || raw.Contains("作呕")
                || raw.Contains("鄙视"))
                return "disgust";
            // surprise: 惊讶 / 震惊 / 吃惊 / 意外
            if (raw.Contains("惊讶") || raw.Contains("震惊")
                || raw.Contains("吃惊") || raw.Contains("意外")
                || raw.Contains("诧异") || raw.Contains("愕然")
                || raw.Contains("懵") || raw.Contains("惊"))
                return "surprise";
            // angry: 愤怒 / 暴躁 / 嘲讽 / 敌意
            if (raw.Contains("怒") || raw.Contains("气")
                || raw.Contains("躁") || raw.Contains("恼")
                || raw.Contains("烦") || raw.Contains("敌意")
                || raw.Contains("不满") || raw.Contains("火")
                || raw.Contains("生气") || raw.Contains("嘲讽")
                || raw.Contains("讽刺") || raw.Contains("暴"))
                return "angry";
            // scared: 害怕 / 紧张 / 警惕 / 恐惧
            if (raw.Contains("怕") || raw.Contains("恐")
                || raw.Contains("慌") || raw.Contains("紧张")
                || raw.Contains("惧") || raw.Contains("不安")
                || raw.Contains("胆怯") || raw.Contains("发抖")
                || raw.Contains("吓") || raw.Contains("怀疑")
                || raw.Contains("警惕") || raw.Contains("戒备")
                || raw.Contains("提防"))
                return "scared";
            // happy: 开心 / 友好 / 兴奋
            if (raw.Contains("开心") || raw.Contains("高兴")
                || raw.Contains("快乐") || raw.Contains("兴奋")
                || raw.Contains("愉悦") || raw.Contains("欢")
                || raw.Contains("乐") || raw.Contains("喜")
                || raw.Contains("友好") || raw.Contains("温和")
                || raw.Contains("热情") || raw.Contains("满足")
                || raw.Contains("得意"))
                return "happy";
            // sad: 悲伤 / 失落
            if (raw.Contains("伤") || raw.Contains("悲")
                || raw.Contains("难过") || raw.Contains("沮丧")
                || raw.Contains("失望") || raw.Contains("失落")
                || raw.Contains("郁") || raw.Contains("痛")
                || raw.Contains("哀") || raw.Contains("绝望"))
                return "sad";
            // neutral: 中性 / 冷漠 / 困惑
            if (raw.Contains("平静") || raw.Contains("冷静")
                || raw.Contains("中性") || raw.Contains("普通")
                || raw.Contains("冷漠") || raw.Contains("淡然")
                || raw.Contains("无所谓") || raw.Contains("一般")
                || raw.Contains("正常") || raw.Contains("困惑")
                || raw.Contains("疑惑"))
                return "neutral";

            return "neutral";
        }

        private AIResponse ParseModelText(string text,
            bool keepSpecialTokens)
        {
            var result = new AIResponse
            {
                action = "speak",
                dialogue = "",
                emotion = "neutral"
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                result.dialogue = keepSpecialTokens ? "[无响应]" : "...";
                return result;
            }

            text = StripThinkingTags(text);
            text = StripNarration(text).Trim();

            string[] parts = text.Split('|');
            if (parts.Length >= 2)
            {
                result.dialogue = parts[0].Trim();
                result.action = NormalizeAction(parts[1]);
                if (parts.Length >= 3)
                    result.emotion = NormalizeEmotion(parts[2]);
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
                        .Replace(",\n}", "}")
                        .Replace(",\r\n}", "}")
                        .Replace(", }", "}")
                        .Replace(",}", "}");

                    int openB = 0;
                    int closeB = 0;
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
                    {
                        jsonStr += "}";
                        closeB++;
                    }

                    try
                    {
                        var parsed = JObject.Parse(jsonStr);
                        result.action = NormalizeAction(
                            (parsed["action"] ?? parsed["a"])
                            ?.ToString());
                        result.dialogue =
                            (parsed["dialogue"] ?? parsed["d"])
                            ?.ToString()?.Trim() ?? "";
                        result.emotion = NormalizeEmotion(
                            (parsed["emotion"] ?? parsed["e"])
                            ?.ToString());
                    }
                    catch
                    {
                        result.dialogue = ExtractDialogue(text);
                    }
                }
                else
                {
                    result.dialogue = ExtractDialogue(text);
                    result.action = "speak";
                    result.emotion = GuessEmotionFromText(result.dialogue);
                }
            }

            int maxLen = Config.Performance.MaxDialogueLength;
            result.dialogue = ValidateAndTruncateDialogue(
                result.dialogue, maxLen, keepSpecialTokens);
            result.action = NormalizeAction(result.action);
            result.emotion = NormalizeEmotion(result.emotion);

            if (string.IsNullOrWhiteSpace(result.dialogue))
                result.dialogue = keepSpecialTokens ? "[无响应]" : "...";

            return result;
        }

        private AIResponse ParsePipeFormat(string text)
        {
            return ParseModelText(text, false);
        }

        private AIResponse ParseAIResponse(string body)
        {
            var result = ParseModelText(null, true);

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

                string reasoningField = message["reasoning_content"]?.ToString();

                string text = null;

                string[] fieldNames = new[]
                {
                    "content",
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

                if (string.IsNullOrWhiteSpace(text))
                {
                    foreach (var prop in message.Children<JProperty>())
                    {
                        if (prop.Name == "role") continue;
                        if (prop.Name == "tool_calls") continue;
                        if (prop.Name == "reasoning_content") continue;
                        if (prop.Name == "reasoning") continue;

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
                    if (!string.IsNullOrWhiteSpace(reasoningField))
                    {
                        text = ChooseModelText(null, reasoningField);
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    DebugLog($"All fields empty. " +
                        $"Message: {message}");
                    result.dialogue = "[无响应]";
                    return result;
                }

                DebugLog($"Cleaned text: {text}");
                result = ParseModelText(text, true);

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

        /// <summary> 
        /// 从纯中文对话猜测情绪，用于微调模型不输出pipe格式时 
        /// </summary> 
        private string GuessEmotionFromText(string text) 
        { 
            if (string.IsNullOrEmpty(text)) return "neutral"; 
            
            if (text.Contains("滚") || text.Contains("死") 
                || text.Contains("杀") || text.Contains("弄") 
                || text.Contains("打") || text.Contains("妈") 
                || text.Contains("操") || text.Contains("混蛋") 
                || text.Contains("废物") || text.Contains("垃圾")) 
                return "angry"; 
            
            if (text.Contains("救命") || text.Contains("别") 
                || text.Contains("怕") || text.Contains("求") 
                || text.Contains("不要") || text.Contains("啊！") 
                || text.Contains("跑")) 
                return "scared"; 
            
            if (text.Contains("唉") || text.Contains("难过") 
                || text.Contains("伤心") || text.Contains("可惜") 
                || text.Contains("遗憾") || text.Contains("对不起")) 
                return "sad"; 
            
            if (text.Contains("哈哈") || text.Contains("嘿") 
                || text.Contains("太好") || text.Contains("棒") 
                || text.Contains("开心") || text.Contains("好啊") 
                || text.Contains("帅") || text.Contains("漂亮") 
                || text.Contains("兄弟") || text.Contains("朋友")) 
                return "happy"; 
            
            return "neutral"; 
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
