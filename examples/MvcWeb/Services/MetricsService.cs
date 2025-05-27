using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MvcWeb.Services
{
    /// <summary>
    /// Centralized metrics service to provide consistent telemetry across the application
    /// Ensures no PII is exposed in metrics
    /// </summary>
    public class MetricsService
    {
        private static readonly ActivitySource ActivitySource = new("MvcWeb.Application");
        private static readonly Meter Meter = new("MvcWeb.Application");

        // HTTP Metrics
        public static readonly Counter<int> HttpRequestsCounter = Meter.CreateCounter<int>(
            "http_requests_total", "Total number of HTTP requests");
        public static readonly Histogram<double> HttpRequestDuration = Meter.CreateHistogram<double>(
            "http_request_duration_ms", "HTTP request duration in milliseconds");

        // Page View Metrics
        public static readonly Counter<int> PageViewsCounter = Meter.CreateCounter<int>(
            "page_views_total", "Total number of page views");
        public static readonly Counter<int> UniqueVisitorsCounter = Meter.CreateCounter<int>(
            "unique_visitors_total", "Total number of unique visitors (by session)");

        // Authentication Metrics (NO PII)
        public static readonly Counter<int> AuthenticationAttemptsCounter = Meter.CreateCounter<int>(
            "authentication_attempts_total", "Total authentication attempts");
        public static readonly Counter<int> AuthenticationSuccessCounter = Meter.CreateCounter<int>(
            "authentication_success_total", "Successful authentications");
        public static readonly Counter<int> AuthenticationFailuresCounter = Meter.CreateCounter<int>(
            "authentication_failures_total", "Failed authentications");

        // CMS Content Metrics
        public static readonly Counter<int> ContentViewsCounter = Meter.CreateCounter<int>(
            "content_views_total", "Total content views");
        public static readonly Counter<int> ContentCreationCounter = Meter.CreateCounter<int>(
            "content_creation_total", "Total content creation events");
        public static readonly Histogram<double> ContentLoadTimeCounter = Meter.CreateHistogram<double>(
            "content_load_time_ms", "Content load time in milliseconds");

        // Business Logic Metrics
        public static readonly Counter<int> WorkflowTransitionsCounter = Meter.CreateCounter<int>(
            "workflow_transitions_total", "Total workflow state transitions");
        public static readonly Counter<int> UserActionsCounter = Meter.CreateCounter<int>(
            "user_actions_total", "Total user actions");

        // Error Metrics
        public static readonly Counter<int> ErrorsCounter = Meter.CreateCounter<int>(
            "errors_total", "Total errors");
        public static readonly Counter<int> NotFoundCounter = Meter.CreateCounter<int>(
            "not_found_total", "Total 404 responses");

        // Cache Metrics
        public static readonly Counter<int> CacheHitsCounter = Meter.CreateCounter<int>(
            "cache_hits_total", "Total cache hits");
        public static readonly Counter<int> CacheMissesCounter = Meter.CreateCounter<int>(
            "cache_misses_total", "Total cache misses");

        // Security Metrics (NO PII)
        public static readonly Counter<int> SecurityEventsCounter = Meter.CreateCounter<int>(
            "security_events_total", "Total security events");
        public static readonly Counter<int> AccessDeniedCounter = Meter.CreateCounter<int>(
            "access_denied_total", "Total access denied events");

        /// <summary>
        /// Records an HTTP request with safe labels (no PII)
        /// </summary>
        public static void RecordHttpRequest(string method, string endpoint, int statusCode, double durationMs)
        {
            // Sanitize endpoint to remove any potential PII
            var sanitizedEndpoint = SanitizeEndpoint(endpoint);
            
            HttpRequestsCounter.Add(1, 
                new KeyValuePair<string, object?>("method", method),
                new KeyValuePair<string, object?>("endpoint", sanitizedEndpoint),
                new KeyValuePair<string, object?>("status_code", statusCode.ToString()));
            
            HttpRequestDuration.Record(durationMs,
                new KeyValuePair<string, object?>("method", method),
                new KeyValuePair<string, object?>("endpoint", sanitizedEndpoint));
        }

        /// <summary>
        /// Records a page view with safe categorization (no PII)
        /// </summary>
        public static void RecordPageView(string pageType, string userRole = "anonymous")
        {
            PageViewsCounter.Add(1,
                new KeyValuePair<string, object?>("page_type", pageType),
                new KeyValuePair<string, object?>("user_role", userRole));
        }

        /// <summary>
        /// Records authentication attempt (no usernames or emails)
        /// </summary>
        public static void RecordAuthenticationAttempt(bool success, string method = "password")
        {
            AuthenticationAttemptsCounter.Add(1,
                new KeyValuePair<string, object?>("method", method),
                new KeyValuePair<string, object?>("success", success.ToString()));

            if (success)
            {
                AuthenticationSuccessCounter.Add(1,
                    new KeyValuePair<string, object?>("method", method));
            }
            else
            {
                AuthenticationFailuresCounter.Add(1,
                    new KeyValuePair<string, object?>("method", method));
            }
        }

        /// <summary>
        /// Records content interaction (no content titles or user info)
        /// </summary>
        public static void RecordContentView(string contentType, string action = "view")
        {
            ContentViewsCounter.Add(1,
                new KeyValuePair<string, object?>("content_type", contentType),
                new KeyValuePair<string, object?>("action", action));
        }

        /// <summary>
        /// Records workflow transitions (no user info, just states)
        /// </summary>
        public static void RecordWorkflowTransition(string fromState, string toState, string workflowType)
        {
            WorkflowTransitionsCounter.Add(1,
                new KeyValuePair<string, object?>("from_state", fromState),
                new KeyValuePair<string, object?>("to_state", toState),
                new KeyValuePair<string, object?>("workflow_type", workflowType));
        }

        /// <summary>
        /// Records user action (no user identification, just action type)
        /// </summary>
        public static void RecordUserAction(string actionType, string category = "general")
        {
            UserActionsCounter.Add(1,
                new KeyValuePair<string, object?>("action_type", actionType),
                new KeyValuePair<string, object?>("category", category));
        }

        /// <summary>
        /// Records errors (sanitized error info, no PII)
        /// </summary>
        public static void RecordError(string errorType, string severity = "error", string component = "unknown")
        {
            ErrorsCounter.Add(1,
                new KeyValuePair<string, object?>("error_type", errorType),
                new KeyValuePair<string, object?>("severity", severity),
                new KeyValuePair<string, object?>("component", component));
        }

        /// <summary>
        /// Records security events (no user info, just event type)
        /// </summary>
        public static void RecordSecurityEvent(string eventType, string severity = "medium")
        {
            SecurityEventsCounter.Add(1,
                new KeyValuePair<string, object?>("event_type", eventType),
                new KeyValuePair<string, object?>("severity", severity));
        }

        /// <summary>
        /// Creates a new activity for tracing with safe tags (no PII)
        /// </summary>
        public static Activity? StartActivity(string operationName)
        {
            return ActivitySource.StartActivity(operationName);
        }

        /// <summary>
        /// Sanitizes endpoint paths to remove potential PII like IDs, emails, etc.
        /// </summary>
        private static string SanitizeEndpoint(string endpoint)
        {
            // Remove query parameters completely
            var cleanEndpoint = endpoint.Split('?')[0];
            
            // Replace GUIDs with placeholder
            cleanEndpoint = System.Text.RegularExpressions.Regex.Replace(
                cleanEndpoint, 
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", 
                "{id}");
            
            // Replace numeric IDs with placeholder
            cleanEndpoint = System.Text.RegularExpressions.Regex.Replace(
                cleanEndpoint, 
                @"/\d+(/|$)", 
                "/{id}$1");
            
            // Convert to lowercase for consistency
            return cleanEndpoint.ToLowerInvariant();
        }
    }
}