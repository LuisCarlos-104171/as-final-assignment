using MvcWeb.Services;
using System.Diagnostics;

namespace MvcWeb.Middleware
{
    /// <summary>
    /// Middleware to capture comprehensive telemetry for all HTTP requests
    /// without exposing PII
    /// </summary>
    public class TelemetryMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TelemetryMiddleware> _logger;

        public TelemetryMiddleware(RequestDelegate next, ILogger<TelemetryMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var path = context.Request.Path.Value ?? "/";
            var method = context.Request.Method;
            
            // Generate or extract correlation ID for request tracing
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
                               ?? context.TraceIdentifier 
                               ?? Guid.NewGuid().ToString("N")[..8];
            
            // Add correlation ID to response headers
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            
            // Sanitize path to remove potential PII
            var sanitizedPath = SanitizePath(path);
            
            // Start a custom activity for detailed tracing
            using var activity = MetricsService.StartActivity($"HTTP {method} {sanitizedPath}");
            activity?.SetTag("http.method", method);
            activity?.SetTag("http.url", sanitizedPath);
            activity?.SetTag("correlation.id", correlationId);
            activity?.SetTag("user.role", context.User?.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous");
            
            try
            {
                // Record session-based unique visitor (no PII)
                var sessionId = context.Session.Id;
                if (!string.IsNullOrEmpty(sessionId))
                {
                    // Use session ID hash to track unique visitors without storing PII
                    var sessionHash = sessionId.GetHashCode().ToString();
                    MetricsService.UniqueVisitorsCounter.Add(1,
                        new KeyValuePair<string, object?>("session_hash", sessionHash.Substring(0, Math.Min(8, sessionHash.Length))));
                }

                // Record user agent category (no specific user agent string)
                var userAgent = context.Request.Headers.UserAgent.ToString();
                var userAgentCategory = CategorizeUserAgent(userAgent);
                
                // Record referrer domain (no specific URLs)
                var referrer = context.Request.Headers.Referer.ToString();
                var referrerDomain = ExtractDomain(referrer);

                await _next(context);

                stopwatch.Stop();
                
                var statusCode = context.Response.StatusCode;
                var duration = stopwatch.ElapsedMilliseconds;

                // Add tracing information to activity
                activity?.SetTag("http.status_code", statusCode);
                activity?.SetTag("http.response_time_ms", duration);
                activity?.SetStatus(statusCode >= 400 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);

                // Record comprehensive HTTP metrics
                MetricsService.RecordHttpRequest(method, sanitizedPath, statusCode, duration);

                // Record additional telemetry
                RecordAdditionalMetrics(context, sanitizedPath, userAgentCategory, referrerDomain, statusCode, duration, correlationId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                // Add exception information to tracing
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("exception.type", ex.GetType().Name);
                activity?.SetTag("exception.message", ex.Message);
                
                // Record error metrics
                MetricsService.RecordHttpRequest(method, sanitizedPath, 500, stopwatch.ElapsedMilliseconds);
                MetricsService.RecordError("middleware_exception", "error", "TelemetryMiddleware");
                
                _logger.LogError(ex, "Error in telemetry middleware for {Method} {Path} [CorrelationId: {CorrelationId}]", 
                    method, sanitizedPath, correlationId);
                throw;
            }
        }

        private void RecordAdditionalMetrics(HttpContext context, string sanitizedPath, string userAgentCategory, 
            string referrerDomain, int statusCode, double duration, string correlationId)
        {
            var userRole = context.User?.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous";
            
            // Record by path category
            var pathCategory = CategorizePath(sanitizedPath);
            MetricsService.RecordPageView(pathCategory, userRole);

            // Record by user agent category
            if (!string.IsNullOrEmpty(userAgentCategory))
            {
                MetricsService.UserActionsCounter.Add(1,
                    new KeyValuePair<string, object?>("action_type", "page_visit"),
                    new KeyValuePair<string, object?>("user_agent_category", userAgentCategory),
                    new KeyValuePair<string, object?>("path_category", pathCategory));
            }

            // Record referrer metrics (no PII)
            if (!string.IsNullOrEmpty(referrerDomain) && referrerDomain != "direct")
            {
                MetricsService.UserActionsCounter.Add(1,
                    new KeyValuePair<string, object?>("action_type", "external_referral"),
                    new KeyValuePair<string, object?>("referrer_domain", referrerDomain),
                    new KeyValuePair<string, object?>("path_category", pathCategory));
            }

            // Record slow requests
            if (duration > 1000) // Requests taking more than 1 second
            {
                MetricsService.UserActionsCounter.Add(1,
                    new KeyValuePair<string, object?>("action_type", "slow_request"),
                    new KeyValuePair<string, object?>("path_category", pathCategory),
                    new KeyValuePair<string, object?>("duration_bucket", GetDurationBucket(duration)));
            }

            // Record error responses
            if (statusCode >= 400)
            {
                var errorCategory = statusCode >= 500 ? "server_error" : "client_error";
                MetricsService.RecordError($"http_{statusCode}", "warning", "HttpResponse");
                
                if (statusCode == 404)
                {
                    MetricsService.NotFoundCounter.Add(1,
                        new KeyValuePair<string, object?>("path_category", pathCategory));
                }
                else if (statusCode == 401 || statusCode == 403)
                {
                    MetricsService.AccessDeniedCounter.Add(1,
                        new KeyValuePair<string, object?>("path_category", pathCategory),
                        new KeyValuePair<string, object?>("status_code", statusCode.ToString()));
                }
            }
        }

        private static string SanitizePath(string path)
        {
            // Remove potential PII from path
            if (string.IsNullOrEmpty(path)) return "/";
            
            // Remove query parameters
            var cleanPath = path.Split('?')[0];
            
            // Replace GUIDs with placeholder
            cleanPath = System.Text.RegularExpressions.Regex.Replace(
                cleanPath, 
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", 
                "{id}");
            
            // Replace numeric IDs with placeholder
            cleanPath = System.Text.RegularExpressions.Regex.Replace(
                cleanPath, 
                @"/\d+(/|$)", 
                "/{id}$1");
            
            return cleanPath.ToLowerInvariant();
        }

        private static string CategorizeUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "unknown";
            
            var ua = userAgent.ToLowerInvariant();
            
            if (ua.Contains("bot") || ua.Contains("spider") || ua.Contains("crawler"))
                return "bot";
            if (ua.Contains("mobile") || ua.Contains("android") || ua.Contains("iphone"))
                return "mobile";
            if (ua.Contains("tablet") || ua.Contains("ipad"))
                return "tablet";
            if (ua.Contains("chrome"))
                return "chrome";
            if (ua.Contains("firefox"))
                return "firefox";
            if (ua.Contains("safari"))
                return "safari";
            if (ua.Contains("edge"))
                return "edge";
                
            return "other";
        }

        private static string ExtractDomain(string referrer)
        {
            if (string.IsNullOrEmpty(referrer)) return "direct";
            
            try
            {
                var uri = new Uri(referrer);
                var domain = uri.Host.ToLowerInvariant();
                
                // Remove www prefix for consistency
                if (domain.StartsWith("www."))
                    domain = domain.Substring(4);
                    
                return domain;
            }
            catch
            {
                return "unknown";
            }
        }

        private static string CategorizePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/") return "home";
            
            var lowerPath = path.ToLowerInvariant();
            
            if (lowerPath.StartsWith("/account")) return "account";
            if (lowerPath.StartsWith("/article")) return "article";
            if (lowerPath.StartsWith("/submission")) return "submission";
            if (lowerPath.StartsWith("/cms")) return "cms";
            if (lowerPath.StartsWith("/page")) return "page";
            if (lowerPath.StartsWith("/post")) return "post";
            if (lowerPath.StartsWith("/archive")) return "archive";
            if (lowerPath.StartsWith("/manager")) return "manager";
            if (lowerPath.StartsWith("/api")) return "api";
            if (lowerPath.StartsWith("/assets") || lowerPath.StartsWith("/lib")) return "static";
            
            return "other";
        }

        private static string GetDurationBucket(double duration)
        {
            if (duration < 1000) return "fast";
            if (duration < 3000) return "slow";
            if (duration < 10000) return "very_slow";
            return "extremely_slow";
        }
    }
}