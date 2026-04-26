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
    /// </summary>
    public sealed class GenericLlmClient : ILlmClient, IDisposable
    {
        private readonly HttpClient _http;
        private readonly LlmProviderProfile _profile;
        private readonly string _modelId;
        private readonly int _maxTokens;
        private readonly float _temperature;

        public string ClientId { get; }

        public GenericLlmClient(
            LlmProviderProfile profile,
            string modelId,
            int maxTokens = 4096,
            float temperature = 0.7f,
            string? apiKey = null,
            string? baseUrl = null,
            HttpClient? httpClient = null)
        {
            _profile     = profile;
            _modelId     = modelId;
            _maxTokens   = maxTokens;
            _temperature = temperature;
            ClientId     = $"{modelId}@{new Uri(baseUrl ?? profile.DefaultBaseUrl).Host}";

            _http = httpClient ?? new HttpClient
            {
                BaseAddress = new Uri(baseUrl ?? profile.DefaultBaseUrl),
                Timeout     = TimeSpan.FromMinutes(5)
            };
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            if (profile.AuthHeader != null && apiKey != null)
            {
                var headerValue = profile.AuthHeader.Value.Replace("{apiKey}", apiKey);
                if (profile.AuthHeader.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = headerValue.Split(' ', 2);
                    _http.DefaultRequestHeaders.Authorization = parts.Length == 2
                        ? new AuthenticationHeaderValue(parts[0], parts[1])
                        : new AuthenticationHeaderValue(headerValue);
                }
                else
                {
                    _http.DefaultRequestHeaders.Add(profile.AuthHeader.Name, headerValue);
                }
            }

            foreach (var h in profile.FixedHeaders)
                _http.DefaultRequestHeaders.Add(h.Name, h.Value);
        }

        public async Task<LlmResponse> CompleteAsync(PromptContext prompt, CancellationToken ct = default)
        {
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
            catch (Exception ex)
            {
                return LlmResponse.Failure(ex.Message);
            }
        }

        // ── Request building ─────────────────────────────────────────────────────

        private Dictionary<string, object> BuildRequestBody(PromptContext prompt)
        {
            var rb   = _profile.RequestBody;
            var body = new Dictionary<string, object>();

            // Fixed provider fields first (e.g. stream: false)
            foreach (var (k, v) in rb.FixedFields)
                body[k] = v;

            body[rb.ModelKey] = _modelId;

            if (rb.MaxTokensKey != null)
                body[rb.MaxTokensKey] = _maxTokens;

            SetNestedValue(body, rb.TemperaturePath, _temperature);

            // Messages
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
            if (dot < 0)
            {
                body[path] = value;
                return;
            }

            var key  = path[..dot];
            var rest = path[(dot + 1)..];

            if (!body.TryGetValue(key, out var existing) || existing is not Dictionary<string, object> nested)
            {
                nested  = new Dictionary<string, object>();
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

            var content      = ResolveString(root, rm.ContentPath) ?? string.Empty;
            var finishReason = rm.FinishReasonPath  != null ? ResolveString(root, rm.FinishReasonPath)  : null;
            var inputTokens  = rm.InputTokensPath   != null ? ResolveInt(root,    rm.InputTokensPath)   : null;
            var outputTokens = rm.OutputTokensPath  != null ? ResolveInt(root,    rm.OutputTokensPath)  : null;
            var modelId      = rm.ModelPath         != null ? ResolveString(root, rm.ModelPath)         : null;

            return LlmResponse.Success(content, finishReason, inputTokens, outputTokens, modelId);
        }

        // Walks a dot-separated path, treating integer segments as array indices.
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

        public void Dispose() => _http.Dispose();
    }
}
