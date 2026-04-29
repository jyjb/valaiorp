namespace Valaiorp.LlmProviders.Clients
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.LlmProviders.Profiles;

    /// <summary>
    /// Single HTTP-based LLM client driven by a <see cref="LlmProviderProfile"/>.
    /// Built-in profiles: anthropic, openai, ollama (loaded via LlmProviderProfileLoader).
    /// Add any new provider by supplying a profile JSON — no C# code required.
    ///
    /// API key is resolved lazily on the first call to <see cref="CompleteAsync"/> so
    /// DI registration remains synchronous and vault-backed key providers never block
    /// the startup thread.
    /// </summary>
    public sealed class GenericLlmClient : ILlmClient, IDisposable
    {
        private readonly HttpClient _http;
        private readonly LlmProviderProfile _profile;
        private readonly string _modelId;
        private readonly int _maxTokens;
        private readonly float _temperature;
        private readonly Func<CancellationToken, Task<string?>> _apiKeyFactory;
        private readonly SemaphoreSlim _authInit = new(1, 1);
        private bool _authApplied;

        public string ClientId { get; }

        /// <summary>
        /// Convenience constructor with a pre-resolved key.
        /// Prefer the factory overload when the key comes from a secrets vault.
        /// </summary>
        public GenericLlmClient(
            LlmProviderProfile profile,
            string modelId,
            int maxTokens = 4096,
            float temperature = 0.7f,
            string? apiKey = null,
            string? baseUrl = null,
            HttpClient? httpClient = null)
            : this(profile, modelId, maxTokens, temperature,
                   _ => Task.FromResult(apiKey),
                   baseUrl, httpClient)
        { }

        /// <summary>
        /// Factory constructor — API key is resolved asynchronously on the first LLM call.
        /// Use this with vault-backed <see cref="IApiKeyProvider"/> implementations.
        /// </summary>
        public GenericLlmClient(
            LlmProviderProfile profile,
            string modelId,
            int maxTokens,
            float temperature,
            Func<CancellationToken, Task<string?>> apiKeyFactory,
            string? baseUrl = null,
            HttpClient? httpClient = null)
        {
            _profile        = profile;
            _modelId        = modelId;
            _maxTokens      = maxTokens;
            _temperature    = temperature;
            _apiKeyFactory  = apiKeyFactory;
            ClientId        = $"{modelId}@{new Uri(baseUrl ?? profile.DefaultBaseUrl).Host}";

            _http = httpClient ?? new HttpClient
            {
                BaseAddress = new Uri(baseUrl ?? profile.DefaultBaseUrl),
                Timeout     = TimeSpan.FromMinutes(5)
            };
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            foreach (var h in profile.FixedHeaders)
                _http.DefaultRequestHeaders.Add(h.Name, h.Value);
        }

        public async Task<LlmResponse> CompleteAsync(PromptContext prompt, CancellationToken ct = default)
        {
            await EnsureAuthAppliedAsync(ct).ConfigureAwait(false);

            try
            {
                var body    = BuildRequestBody(prompt);
                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(_profile.Endpoint, content, ct).ConfigureAwait(false);
                var json    = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                return response.IsSuccessStatusCode
                    ? ParseResponse(json)
                    : LlmResponse.Failure($"LLM API error {(int)response.StatusCode}: {json}");
            }
            catch (OperationCanceledException)
            {
                throw; // propagate cancellation/timeout — never swallow
            }
            catch (Exception ex)
            {
                return LlmResponse.Failure(ex.Message);
            }
        }

        // ── Auth init (once, async-safe) ─────────────────────────────────────────

        private async Task EnsureAuthAppliedAsync(CancellationToken ct)
        {
            if (_authApplied) return;

            await _authInit.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_authApplied) return;

                var apiKey = await _apiKeyFactory(ct).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(apiKey) && _profile.AuthHeader != null)
                    throw new InvalidOperationException(
                        $"No API key resolved for LLM provider '{_profile.DefaultBaseUrl}'. " +
                        "Set ApiKey, ApiKeyEnvVar, or ApiKeyFile in the Llm config section, " +
                        "or register a custom IApiKeyProvider.");

                if (_profile.AuthHeader != null && !string.IsNullOrWhiteSpace(apiKey))
                {
                    var headerValue = _profile.AuthHeader.Value.Replace("{apiKey}", apiKey);
                    if (_profile.AuthHeader.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = headerValue.Split(' ', 2);
                        _http.DefaultRequestHeaders.Authorization = parts.Length == 2
                            ? new AuthenticationHeaderValue(parts[0], parts[1])
                            : new AuthenticationHeaderValue(headerValue);
                    }
                    else
                    {
                        _http.DefaultRequestHeaders.TryAddWithoutValidation(_profile.AuthHeader.Name, headerValue);
                    }
                }

                _authApplied = true;
            }
            finally
            {
                _authInit.Release();
            }
        }

        // ── Request building ─────────────────────────────────────────────────────

        private Dictionary<string, object> BuildRequestBody(PromptContext prompt)
        {
            var rb   = _profile.RequestBody;
            var body = new Dictionary<string, object>();

            foreach (var (k, v) in rb.FixedFields)
                body[k] = v;

            body[rb.ModelKey] = _modelId;

            if (rb.MaxTokensKey != null)
                body[rb.MaxTokensKey] = _maxTokens;

            SetNestedValue(body, rb.TemperaturePath, _temperature);

            var messages   = new List<object>();
            var systemText = prompt.SystemPrompt ?? string.Empty;

            if (_profile.SystemPosition == "topLevel")
            {
                if (!string.IsNullOrWhiteSpace(systemText))
                    body["system"] = systemText;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(systemText))
                    messages.Add(new { role = "system", content = systemText });
            }

            foreach (var turn in prompt.ConversationHistory)
            {
                if (_profile.SystemPosition == "topLevel" && turn.Role == "system") continue;
                messages.Add(new { role = turn.Role, content = turn.Content });
            }

            messages.Add(new { role = "user", content = BuildUserContent(prompt) });
            body["messages"] = messages;

            return body;
        }

        private static void SetNestedValue(Dictionary<string, object> body, string path, object value)
        {
            var dot = path.IndexOf('.');
            if (dot < 0) { body[path] = value; return; }

            var key  = path[..dot];
            var rest = path[(dot + 1)..];

            if (!body.TryGetValue(key, out var existing) || existing is not Dictionary<string, object> nested)
            {
                nested    = new Dictionary<string, object>();
                body[key] = nested;
            }

            SetNestedValue(nested, rest, value);
        }

        private static string BuildUserContent(PromptContext prompt)
        {
            var sb = new StringBuilder();

            if (prompt.RagContext.Count > 0)
            {
                sb.AppendLine("Relevant context:");
                foreach (var chunk in prompt.RagContext) sb.AppendLine($"- {chunk}");
                sb.AppendLine();
            }

            if (prompt.MemoryContext.Count > 0)
            {
                sb.AppendLine("Memory:");
                foreach (var m in prompt.MemoryContext) sb.AppendLine($"- {m}");
                sb.AppendLine();
            }

            sb.Append(prompt.UserPrompt);
            return sb.ToString();
        }

        // ── Response parsing ─────────────────────────────────────────────────────

        private LlmResponse ParseResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var rm        = _profile.ResponseMapping;
            var root      = doc.RootElement;

            var finishReason = rm.FinishReasonPath  != null ? ResolveString(root, rm.FinishReasonPath)  : null;
            var inputTokens  = rm.InputTokensPath   != null ? ResolveInt(root,    rm.InputTokensPath)   : null;
            var outputTokens = rm.OutputTokensPath  != null ? ResolveInt(root,    rm.OutputTokensPath)  : null;
            var modelId      = rm.ModelPath         != null ? ResolveString(root, rm.ModelPath)         : null;

            // Native tool-call detection — checked before text content so that
            // a model that returns tool_use with no text content is handled correctly.
            var toolCalls = ParseToolCalls(root, finishReason);
            if (toolCalls.Count > 0)
                return LlmResponse.Success(string.Empty, finishReason, inputTokens, outputTokens, modelId, toolCalls);

            var content = ResolveString(root, rm.ContentPath) ?? string.Empty;
            return LlmResponse.Success(content, finishReason, inputTokens, outputTokens, modelId);
        }

        /// <summary>
        /// Detects and parses native tool/function calls from both Anthropic and OpenAI response shapes.
        ///
        /// Anthropic: stop_reason="tool_use", root.content[] contains items with type="tool_use"
        ///   { "id": "toolu_xxx", "type": "tool_use", "name": "tool_name", "input": { ... } }
        ///
        /// OpenAI: choices[0].finish_reason="tool_calls", choices[0].message.tool_calls[]
        ///   { "id": "call_xxx", "type": "function", "function": { "name": "...", "arguments": "{...}" } }
        /// </summary>
        private static IReadOnlyList<ToolCall> ParseToolCalls(JsonElement root, string? finishReason)
        {
            var calls = new List<ToolCall>();

            // ── Anthropic shape ───────────────────────────────────────────────────────
            if (finishReason is "tool_use" &&
                root.TryGetProperty("content", out var anthropicContent) &&
                anthropicContent.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in anthropicContent.EnumerateArray())
                {
                    if (!item.TryGetProperty("type", out var typeEl) ||
                        typeEl.GetString() != "tool_use")
                        continue;

                    var callId   = item.TryGetProperty("id",   out var idEl)   ? idEl.GetString()   ?? string.Empty : string.Empty;
                    var toolName = item.TryGetProperty("name", out var nameEl)  ? nameEl.GetString() ?? string.Empty : string.Empty;
                    var inputs   = new Dictionary<string, object>();

                    if (item.TryGetProperty("input", out var inputEl) &&
                        inputEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var kv in inputEl.EnumerateObject())
                            inputs[kv.Name] = JsonElementToObject(kv.Value);
                    }

                    calls.Add(new ToolCall { CallId = callId, ToolName = toolName, Inputs = inputs });
                }
                return calls;
            }

            // ── OpenAI shape ──────────────────────────────────────────────────────────
            if (finishReason is "tool_calls" &&
                root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array)
            {
                var first = choices.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object &&
                    first.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("tool_calls", out var toolCallsEl) &&
                    toolCallsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tc in toolCallsEl.EnumerateArray())
                    {
                        var callId   = tc.TryGetProperty("id",       out var idEl)   ? idEl.GetString()   ?? string.Empty : string.Empty;
                        var toolName = string.Empty;
                        var inputs   = new Dictionary<string, object>();

                        if (tc.TryGetProperty("function", out var fnEl))
                        {
                            toolName = fnEl.TryGetProperty("name", out var fnName) ? fnName.GetString() ?? string.Empty : string.Empty;

                            if (fnEl.TryGetProperty("arguments", out var argsEl))
                            {
                                var argsJson = argsEl.GetString() ?? "{}";
                                try
                                {
                                    using var argsDoc = JsonDocument.Parse(argsJson);
                                    foreach (var kv in argsDoc.RootElement.EnumerateObject())
                                        inputs[kv.Name] = JsonElementToObject(kv.Value);
                                }
                                catch (JsonException) { /* malformed arguments — leave inputs empty */ }
                            }
                        }

                        calls.Add(new ToolCall { CallId = callId, ToolName = toolName, Inputs = inputs });
                    }
                }
            }

            return calls;
        }

        private static object JsonElementToObject(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String  => el.GetString() ?? string.Empty,
            JsonValueKind.Number  => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True    => true,
            JsonValueKind.False   => false,
            JsonValueKind.Null    => (object)"",
            _                     => el.GetRawText()
        };

        private static JsonElement? ResolvePath(JsonElement root, string path)
        {
            var current = root;
            foreach (var segment in path.Split('.'))
            {
                if (int.TryParse(segment, out var index))
                {
                    if (current.ValueKind != JsonValueKind.Array) return null;
                    var items = current.EnumerateArray().ToList();
                    if (index >= items.Count) return null;
                    current = items[index];
                }
                else
                {
                    if (!current.TryGetProperty(segment, out var next)) return null;
                    current = next;
                }
            }
            return current;
        }

        private static string? ResolveString(JsonElement root, string path)
        {
            var el = ResolvePath(root, path);
            return el?.ValueKind == JsonValueKind.String ? el.Value.GetString() : null;
        }

        private static int? ResolveInt(JsonElement root, string path)
        {
            var el = ResolvePath(root, path);
            return el?.ValueKind == JsonValueKind.Number ? el.Value.GetInt32() : null;
        }

        public void Dispose()
        {
            _http.Dispose();
            _authInit.Dispose();
        }
    }
}
