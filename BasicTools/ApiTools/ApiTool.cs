namespace Valaiorp.BasicTools.ApiTools
{
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Tools.Contracts;
    using Valaiorp.Tools.Helpers;

    public enum ApiAction { Get, Post, Put, Delete, Patch }

    public sealed class ApiTool : ITool, IDisposable
    {
        private static readonly HttpClient _sharedClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        private readonly HttpClient _client;
        private readonly bool _ownedClient;

        public ApiTool() => _client = _sharedClient;
        public ApiTool(HttpClient client) { _client = client; _ownedClient = true; }

        public string Id => "api-tool";
        public string Name => "API Tool";
        public string Description => "HTTP/S calls. Parameters: method (GET|POST|PUT|DELETE|PATCH), url, body (JSON string), headers (JSON object string).";
        public ToolType Type => ToolType.External;
        public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
        {
            ["SupportedMethods"] = Enum.GetNames(typeof(ApiAction))
        };

        public async Task<ToolResult> ExecuteAsync(
            IExecutionContext context,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            try
            {
                var methodStr = parameters.GetString("method", "GET");
                var url       = parameters.GetString("url");
                var body      = parameters.GetString("body");
                var headers   = parameters.GetString("headers");

                if (!Enum.TryParse<ApiAction>(methodStr, true, out var action))
                    return ToolResult.BadRequest(new { Message = $"Unknown method '{methodStr}'." });

                if (string.IsNullOrWhiteSpace(url))
                    return ToolResult.BadRequest(new { Message = "Parameter 'url' is required." });

                using var request  = BuildRequest(action, url, body, headers);
                using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);

                var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var statusCode   = (int)response.StatusCode;
                var parsedBody   = TryParseJson(responseBody);
                var respHeaders  = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

                return response.IsSuccessStatusCode
                    ? new ToolResult { Status = statusCode, Results = new { StatusCode = statusCode, Body = parsedBody, Headers = respHeaders } }
                    : new ToolResult { Status = statusCode, Errors  = new { StatusCode = statusCode, Body = parsedBody } };
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        private static HttpRequestMessage BuildRequest(ApiAction action, string url, string? body, string? headersJson)
        {
            var method = action switch
            {
                ApiAction.Post   => HttpMethod.Post,
                ApiAction.Put    => HttpMethod.Put,
                ApiAction.Delete => HttpMethod.Delete,
                ApiAction.Patch  => HttpMethod.Patch,
                _                => HttpMethod.Get
            };
            var request = new HttpRequestMessage(method, url);

            if (!string.IsNullOrWhiteSpace(headersJson))
            {
                var hdrs = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
                if (hdrs != null)
                    foreach (var (k, v) in hdrs)
                        request.Headers.TryAddWithoutValidation(k, v);
            }

            if (!string.IsNullOrWhiteSpace(body) && action is not ApiAction.Get and not ApiAction.Delete)
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
