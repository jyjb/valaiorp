namespace Valaiorp.BasicTools.ApiTools
{
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Contracts;

    public enum ApiAction { Get, Post, Put, Delete, Patch }

    public sealed class ApiTool : ITool, IDisposable
    {
        // Shared across all default instances; never disposed.
        private static readonly HttpClient _sharedClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        private readonly HttpClient _client;
        private readonly bool _ownedClient;

        public ApiTool() => _client = _sharedClient;

        public ApiTool(HttpClient client) { _client = client; _ownedClient = true; }

        public string Id => "api-tool";
        public string Name => "API Tool";
        public string Description => "HTTP/S API calls: GET, POST, PUT, DELETE, PATCH. Input: action|url[|bodyJson][|headersJson]";
        public ToolType Type => ToolType.External;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            { "SupportedActions", Enum.GetNames(typeof(ApiAction)) },
            { "InputFormat", "action|url[|bodyJson][|headersJson]" }
        };

        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            string input,
            CancellationToken ct = default)
        {
            try
            {
                var parts = input.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return ToolResult.BadRequest(new { Message = "Invalid input. Use: action|url[|bodyJson][|headersJson]" });

                if (!Enum.TryParse<ApiAction>(parts[0].Trim(), true, out var action))
                    return ToolResult.BadRequest(new { Message = $"Invalid action: {parts[0].Trim()}" });

                var url         = parts[1].Trim();
                var body        = parts.Length > 2 ? parts[2].Trim() : null;
                var headersJson = parts.Length > 3 ? parts[3].Trim() : null;

                using var request  = BuildRequest(action, url, body, headersJson);
                using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);

                var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var statusCode   = (int)response.StatusCode;
                var parsedBody   = TryParseJson(responseBody);
                var headers      = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

                return response.IsSuccessStatusCode
                    ? new ToolResult { Status = statusCode, Results = new { StatusCode = statusCode, Body = parsedBody, Headers = headers } }
                    : new ToolResult { Status = statusCode, Errors  = new { StatusCode = statusCode, Body = parsedBody } };
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        private static HttpRequestMessage BuildRequest(ApiAction action, string url, string? body, string? headersJson)
        {
            var method = action switch
            {
                ApiAction.Get    => HttpMethod.Get,
                ApiAction.Post   => HttpMethod.Post,
                ApiAction.Put    => HttpMethod.Put,
                ApiAction.Delete => HttpMethod.Delete,
                ApiAction.Patch  => HttpMethod.Patch,
                _                => HttpMethod.Get
            };

            var request = new HttpRequestMessage(method, url);

            if (!string.IsNullOrEmpty(headersJson))
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
                if (headers != null)
                    foreach (var (key, value) in headers)
                        request.Headers.TryAddWithoutValidation(key, value);
            }

            if (!string.IsNullOrEmpty(body) && action is not ApiAction.Get and not ApiAction.Delete)
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            return request;
        }

        private static object TryParseJson(string body)
        {
            try { return JsonSerializer.Deserialize<JsonElement>(body); }
            catch { return body; }
        }

        public void Dispose() { if (_ownedClient) _client.Dispose(); }
    }
}
