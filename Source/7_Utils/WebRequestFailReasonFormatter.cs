using System;

namespace BeatLeader.Utils {
    internal static class WebRequestFailReasonFormatter {
        public static string Format(string? failReason) {
            if (string.IsNullOrWhiteSpace(failReason)) {
                return "unknown error";
            }

            if (IsTimeout(failReason)) {
                return "request timed out";
            }

            const string aggregatePrefix = "AggregateException: ";
            if (failReason.StartsWith(aggregatePrefix, StringComparison.OrdinalIgnoreCase)) {
                failReason = failReason.Substring(aggregatePrefix.Length);
            }

            const int maxLength = 160;
            return failReason.Length <= maxLength ? failReason : failReason.Substring(0, maxLength - 3) + "...";
        }

        public static string? FormatOrNull(string? failReason) {
            return string.IsNullOrWhiteSpace(failReason) ? failReason : Format(failReason);
        }

        public static string Format(Exception ex) {
            var baseException = ex.GetBaseException();
            if (baseException is TimeoutException) {
                return "request timed out";
            }

            var message = string.IsNullOrWhiteSpace(baseException.Message)
                ? baseException.GetType().Name
                : $"{baseException.GetType().Name}: {baseException.Message}";

            return Format(message);
        }

        public static bool IsTimeout(string failReason) {
            return failReason.IndexOf("TimeoutException", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   failReason.IndexOf("failed after", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   failReason.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
