using System;
using System.Collections.Generic;
using System.Globalization;
using BeatLeader.Models;
using Newtonsoft.Json.Linq;

namespace BeatLeader {
    internal static class ScoreSaberLeaderboardInfoProvider {
        private const string ApiBaseUrl = "https://scoresaber.com/api";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
        private static readonly Dictionary<string, ScoreSaberLeaderboardInfoCacheEntry> Cache = new();

        public const int TimeoutSeconds = 6;

        public static bool CanRequest(LeaderboardKey leaderboardKey) {
            return !string.IsNullOrEmpty(leaderboardKey.Hash) &&
                !string.IsNullOrEmpty(leaderboardKey.Mode) &&
                leaderboardKey.DiffId != 0;
        }

        public static string BuildInfoUrl(LeaderboardKey leaderboardKey) {
            var hash = Uri.EscapeDataString(leaderboardKey.Hash);
            var mode = Uri.EscapeDataString(NormalizeMode(leaderboardKey.Mode));
            return $"{ApiBaseUrl}/leaderboard/by-hash/{hash}/info?difficulty={leaderboardKey.DiffId}&gameMode={mode}";
        }

        public static bool TryGetCached(LeaderboardKey leaderboardKey, out ScoreSaberLeaderboardInfo info) {
            info = ScoreSaberLeaderboardInfo.Unknown;
            var cacheKey = CacheKey(leaderboardKey);
            if (!Cache.TryGetValue(cacheKey, out var cacheEntry)) {
                return false;
            }

            if (DateTime.UtcNow >= cacheEntry.ExpiresAt) {
                Cache.Remove(cacheKey);
                return false;
            }

            info = cacheEntry.Info;
            return true;
        }

        public static void SaveToCache(LeaderboardKey leaderboardKey, ScoreSaberLeaderboardInfo info) {
            Cache[CacheKey(leaderboardKey)] = new ScoreSaberLeaderboardInfoCacheEntry(
                info,
                DateTime.UtcNow.Add(CacheTtl)
            );
        }

        public static bool TryRead(string json, out ScoreSaberLeaderboardInfo info) {
            info = ScoreSaberLeaderboardInfo.Unknown;

            try {
                var root = JObject.Parse(json);
                var infoToken = root["leaderboardInfo"] ?? root;

                var ranked = ReadBool(infoToken, "ranked") ?? false;
                var qualified = ReadBool(infoToken, "qualified") ?? false;
                var loved = ReadBool(infoToken, "loved") ?? false;
                var stars = ReadDouble(infoToken, "stars") ?? 0.0;
                var maxPp = ReadDouble(infoToken, "maxPP") ?? 0.0;

                info = new ScoreSaberLeaderboardInfo(true, ranked, qualified, loved, stars, maxPp);
                return true;
            } catch (Exception ex) {
                Plugin.Log.Debug($"[ScoreSaberPP] Failed to parse leaderboard info: {ex.Message}");
                return false;
            }
        }

        public static string NormalizeMode(string mode) {
            return mode.StartsWith("Solo", StringComparison.OrdinalIgnoreCase) ? mode : $"Solo{mode}";
        }

        private static string CacheKey(LeaderboardKey leaderboardKey) {
            return $"{leaderboardKey.Hash.ToUpperInvariant()}|{leaderboardKey.DiffId}|{NormalizeMode(leaderboardKey.Mode)}";
        }

        private static bool? ReadBool(JToken token, params string[] names) {
            foreach (var name in names) {
                var value = token[name];
                if (value == null) {
                    continue;
                }

                if (value.Type == JTokenType.Boolean) {
                    return value.Value<bool>();
                }

                if (value.Type == JTokenType.Integer) {
                    return value.Value<int>() != 0;
                }

                if (bool.TryParse(value.Value<string>(), out var result)) {
                    return result;
                }

                if (int.TryParse(value.Value<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericResult)) {
                    return numericResult != 0;
                }
            }

            return null;
        }

        private static double? ReadDouble(JToken token, string name) {
            var value = token[name];
            if (value == null) {
                return null;
            }

            if (value.Type is JTokenType.Float or JTokenType.Integer) {
                return value.Value<double>();
            }

            return double.TryParse(value.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
        }

        private readonly struct ScoreSaberLeaderboardInfoCacheEntry {
            public readonly ScoreSaberLeaderboardInfo Info;
            public readonly DateTime ExpiresAt;

            public ScoreSaberLeaderboardInfoCacheEntry(ScoreSaberLeaderboardInfo info, DateTime expiresAt) {
                Info = info;
                ExpiresAt = expiresAt;
            }
        }
    }

    internal readonly struct ScoreSaberLeaderboardInfo {
        public static readonly ScoreSaberLeaderboardInfo Unknown = new(false, false, false, false, 0.0, 0.0);

        public readonly bool IsKnown;
        public readonly bool IsRanked;
        public readonly bool IsQualified;
        public readonly bool IsLoved;
        public readonly double Stars;
        public readonly double MaxPp;

        public bool CanEstimatePp => MaxPp > 0.0;

        public ScoreSaberLeaderboardInfo(bool isKnown, bool isRanked, bool isQualified, bool isLoved, double stars, double maxPp) {
            IsKnown = isKnown;
            IsRanked = isRanked;
            IsQualified = isQualified;
            IsLoved = isLoved;
            Stars = stars;
            MaxPp = maxPp;
        }
    }
}
