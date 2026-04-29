namespace Valaiorp.BasicTools.ApiTools
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using Valaiorp.BasicTools;
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

                // SSRF + scheme validation
                var urlError = ValidateUrl(url);
                if (urlError is not null)
                {
                    ToolSecurityLog.Write("api-tool", "BlockedRequest", context.Id,
                        new { url, method = methodStr, reason = urlError });
                    return ToolResult.BadRequest(new { Message = urlError });
                }

                ToolSecurityLog.Write("api-tool", "Request", context.Id,
                    new { url, method = methodStr });

                using var request  = BuildRequest(action, url, body, headers);
                using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);

                var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var statusCode   = (int)response.StatusCode;
                var parsedBody   = TryParseJson(responseBody);
                var respHeaders  = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

                ToolSecurityLog.Write("api-tool", "Response", context.Id,
                    new { url, statusCode, success = response.IsSuccessStatusCode });

                return response.IsSuccessStatusCode
                    ? new ToolResult { Status = statusCode, Results = new { StatusCode = statusCode, Body = parsedBody, Headers = respHeaders } }
                    : new ToolResult { Status = statusCode, Errors  = new { StatusCode = statusCode, Body = parsedBody } };
            }
            catch (Exception ex) { return ToolResult.Error(ex); }
        }

        // Returns an error message string when the URL is not allowed, null when it is safe.
        private static string? ValidateUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return "Invalid URL format.";

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return $"Only HTTP/HTTPS URLs are permitted. Scheme '{uri.Scheme}' is not allowed.";

            var host = uri.Host;

            // Require HTTPS for any non-loopback host
            if (!IsLoopback(host) && uri.Scheme != Uri.UriSchemeHttps)
                return "HTTPS is required for non-loopback URLs.";

            // Block private/link-local IP ranges (SSRF protection)
            if (IsPrivateOrLinkLocal(host))
                return $"Requests to private or link-local addresses are not permitted (SSRF protection). Host: {host}";

            return null;
        }

        private static bool IsLoopback(string host)
        {
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
            if (IPAddress.TryParse(host, out var ip)) return IPAddress.IsLoopback(ip);
            return false;
        }

        private static bool IsPrivateOrLinkLocal(string host)
        {
            if (!IPAddress.TryParse(host, out var ip)) return false;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                if (b[0] == 10) return true;                              // 10.0.0.0/8
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true; // 172.16.0.0/12
                if (b[0] == 192 && b[1] == 168) return true;              // 192.168.0.0/16
                if (b[0] == 169 && b[1] == 254) return true;              // 169.254.0.0/16 (link-local/AWS metadata)
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
                if (ip.Equals(IPAddress.IPv6Loopback)) return true;
            }

            return false;
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
