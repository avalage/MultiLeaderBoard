using System;
using System.Collections.Generic;
using System.Globalization;
using BeatLeader.Models;
using Newtonsoft.Json.Linq;

namespace BeatLeader {
    internal static class AccSaberApiProvider {
        private const string ApiBaseUrl = "https://api.accsaberreloaded.com/v1";
        private const string OverallCategoryId = "b0000000-0000-0000-0000-000000000005";
        private static readonly TimeSpan ScoreCacheTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan MissingScoreCacheTtl = TimeSpan.FromSeconds(25);
        private static readonly TimeSpan ProfileCacheTtl = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan DifficultyCacheTtl = TimeSpan.FromMinutes(15);
        private static readonly Dictionary<string, AccSaberScoreCacheEntry> ScoreCache = new();
        private static readonly Dictionary<string, AccSaberProfileCacheEntry> ProfileCache = new();
        private static readonly Dictionary<string, AccSaberDifficultyCacheEntry> DifficultyCache = new();

        public const int TimeoutSeconds = 5;

        public static bool CanRequestScore(LeaderboardKey leaderboardKey) {
            return !string.IsNullOrEmpty(leaderboardKey.Hash) &&
                !string.IsNullOrEmpty(leaderboardKey.Diff) &&
                IsStandardCharacteristic(leaderboardKey.Mode) &&
                TryNormalizeDifficulty(leaderboardKey.Diff, out _);
        }

        public static string BuildScoreUrl(LeaderboardKey leaderboardKey, string playerId) {
            var hash = Uri.EscapeDataString(leaderboardKey.Hash.ToLowerInvariant());
            var difficulty = Uri.EscapeDataString(NormalizeDifficulty(leaderboardKey.Diff));
            var escapedPlayerId = Uri.EscapeDataString(playerId);
            return $"{ApiBaseUrl}/users/{escapedPlayerId}/scores/by-hash/{hash}?difficulty={difficulty}&characteristic=Standard";
        }

        public static string BuildProfileUrl(string playerId, bool bypassCache = false) {
            var url = $"{ApiBaseUrl}/users/{Uri.EscapeDataString(playerId)}?statistics=true";
            return bypassCache ? $"{url}&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" : url;
        }

        public static string BuildDifficultyInfoUrl(LeaderboardKey leaderboardKey) {
            var hash = Uri.EscapeDataString(leaderboardKey.Hash.ToUpperInvariant());
            var difficulty = Uri.EscapeDataString(NormalizeDifficulty(leaderboardKey.Diff));
            return $"{ApiBaseUrl}/maps/hash/{hash}?difficulty={difficulty}";
        }

        public static bool TryGetCachedScore(LeaderboardKey leaderboardKey, string playerId, out AccSaberScoreInfo info) {
            info = default;
            var cacheKey = ScoreCacheKey(leaderboardKey, playerId);
            if (!ScoreCache.TryGetValue(cacheKey, out var cacheEntry)) {
                return false;
            }

            if (DateTime.UtcNow >= cacheEntry.ExpiresAt) {
                ScoreCache.Remove(cacheKey);
                return false;
            }

            info = cacheEntry.Info;
            return true;
        }

        public static void SaveScoreToCache(LeaderboardKey leaderboardKey, string playerId, AccSaberScoreInfo info) {
            ScoreCache[ScoreCacheKey(leaderboardKey, playerId)] = new AccSaberScoreCacheEntry(
                info,
                DateTime.UtcNow.Add(info.HasAp ? ScoreCacheTtl : MissingScoreCacheTtl)
            );
        }

        public static void ClearScoreCache(LeaderboardKey leaderboardKey, string playerId) {
            if (string.IsNullOrEmpty(playerId)) {
                return;
            }

            ScoreCache.Remove(ScoreCacheKey(leaderboardKey, playerId));
        }

        public static bool TryGetCachedProfile(string playerId, out AccSaberProfileInfo info) {
            info = default;
            if (!ProfileCache.TryGetValue(playerId, out var cacheEntry)) {
                return false;
            }

            if (DateTime.UtcNow >= cacheEntry.ExpiresAt) {
                ProfileCache.Remove(playerId);
                return false;
            }

            info = cacheEntry.Info;
            return true;
        }

        public static void SaveProfileToCache(string playerId, AccSaberProfileInfo info) {
            ProfileCache[playerId] = new AccSaberProfileCacheEntry(
                info,
                DateTime.UtcNow.Add(ProfileCacheTtl)
            );
        }

        public static void ClearProfileCache(string playerId) {
            if (string.IsNullOrEmpty(playerId)) {
                return;
            }

            ProfileCache.Remove(playerId);
        }

        public static bool TryGetCachedDifficulty(LeaderboardKey leaderboardKey, out AccSaberDifficultyInfo info) {
            info = AccSaberDifficultyInfo.Unknown;
            var cacheKey = DifficultyCacheKey(leaderboardKey);
            if (!DifficultyCache.TryGetValue(cacheKey, out var cacheEntry)) {
                return false;
            }

            if (DateTime.UtcNow >= cacheEntry.ExpiresAt) {
                DifficultyCache.Remove(cacheKey);
                return false;
            }

            info = cacheEntry.Info;
            return true;
        }

        public static void SaveDifficultyToCache(LeaderboardKey leaderboardKey, AccSaberDifficultyInfo info) {
            DifficultyCache[DifficultyCacheKey(leaderboardKey)] = new AccSaberDifficultyCacheEntry(
                info,
                DateTime.UtcNow.Add(DifficultyCacheTtl)
            );
        }

        public static bool TryReadScore(string json, out AccSaberScoreInfo info) {
            info = default;

            try {
                var root = JObject.Parse(json);
                var ap = ReadFloat(root, "ap");
                if (ap == null || ap <= 0.0f) {
                    return false;
                }

                info = new AccSaberScoreInfo(ap.Value, true);
                return true;
            } catch (Exception ex) {
                Plugin.Log.Debug($"[AccSaberAP] Failed to parse score: {ex.Message}");
                return false;
            }
        }

        public static bool TryReadProfile(string json, out AccSaberProfileInfo info) {
            info = default;

            try {
                var root = JObject.Parse(json);
                var statsToken = FindOverallStatsToken(root["statistics"]);
                if (statsToken == null) {
                    return false;
                }

                var rank = ReadInt(statsToken, "ranking", "rank");
                var countryRank = ReadInt(statsToken, "countryRanking", "countryRank");
                var ap = ReadFloat(statsToken, "ap", "weightedAp");

                if (rank == null && countryRank == null && ap == null) {
                    return false;
                }

                info = new AccSaberProfileInfo(rank ?? -1, countryRank ?? -1, ap ?? -1.0f);
                return true;
            } catch (Exception ex) {
                Plugin.Log.Debug($"[AccSaberProfile] Failed to parse profile: {ex.Message}");
                return false;
            }
        }

        public static bool TryReadDifficulty(string json, LeaderboardKey leaderboardKey, out AccSaberDifficultyInfo info) {
            info = AccSaberDifficultyInfo.Unknown;

            try {
                var root = JToken.Parse(json);
                var difficultyToken = FindDifficultyToken(root, NormalizeDifficulty(leaderboardKey.Diff));
                if (difficultyToken == null) {
                    return false;
                }

                var complexity = ReadFloat(difficultyToken, "complexity") ?? 0.0f;
                if (complexity <= 0.0f) {
                    return false;
                }

                var status = ReadString(difficultyToken, "status", "rankedStatus") ?? string.Empty;
                var categoryCode = ReadString(difficultyToken, "categoryCode", "categoryId") ?? string.Empty;

                info = new AccSaberDifficultyInfo(true, complexity, status, categoryCode);
                return true;
            } catch (Exception ex) {
                Plugin.Log.Debug($"[AccSaberHeader] Failed to parse difficulty info: {ex.Message}");
                return false;
            }
        }

        private static JToken? FindOverallStatsToken(JToken? statisticsToken) {
            if (statisticsToken is JArray statsArray) {
                foreach (var item in statsArray) {
                    var categoryId = item["categoryId"]?.Value<string>();
                    if (string.Equals(categoryId, OverallCategoryId, StringComparison.OrdinalIgnoreCase)) {
                        return item;
                    }
                }

                return statsArray.Count > 0 ? statsArray[0] : null;
            }

            if (statisticsToken is JObject statsObject) {
                return statsObject["overall"] ?? statsObject["overall_acc"] ?? statsObject;
            }

            return null;
        }

        private static string ScoreCacheKey(LeaderboardKey leaderboardKey, string playerId) {
            return $"{playerId}|{leaderboardKey.Hash.ToUpperInvariant()}|{NormalizeDifficulty(leaderboardKey.Diff)}|Standard";
        }

        private static string DifficultyCacheKey(LeaderboardKey leaderboardKey) {
            return $"{leaderboardKey.Hash.ToUpperInvariant()}|{NormalizeDifficulty(leaderboardKey.Diff)}|Standard";
        }

        private static bool IsStandardCharacteristic(string mode) {
            return string.Equals(mode, "Standard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "SoloStandard", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDifficulty(string difficulty) {
            return TryNormalizeDifficulty(difficulty, out var normalized) ? normalized : difficulty.ToUpperInvariant();
        }

        private static bool TryNormalizeDifficulty(string difficulty, out string normalized) {
            normalized = difficulty switch {
                "Easy" => "EASY",
                "Normal" => "NORMAL",
                "Hard" => "HARD",
                "Expert" => "EXPERT",
                "ExpertPlus" => "EXPERT_PLUS",
                _ => string.Empty
            };
            return !string.IsNullOrEmpty(normalized);
        }

        private static int? ReadInt(JToken token, params string[] names) {
            foreach (var name in names) {
                var value = token[name];
                if (value == null) {
                    continue;
                }

                if (value.Type == JTokenType.Integer) {
                    return value.Value<int>();
                }

                if (int.TryParse(value.Value<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)) {
                    return result;
                }
            }

            return null;
        }

        private static string? ReadString(JToken token, params string[] names) {
            foreach (var name in names) {
                var value = token[name];
                if (value == null) {
                    continue;
                }

                return value.Value<string>();
            }

            return null;
        }

        private static float? ReadFloat(JToken token, params string[] names) {
            foreach (var name in names) {
                var value = token[name];
                if (value == null) {
                    continue;
                }

                if (value.Type is JTokenType.Float or JTokenType.Integer) {
                    return value.Value<float>();
                }

                if (float.TryParse(value.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)) {
                    return result;
                }
            }

            return null;
        }

        private static JToken? FindDifficultyToken(JToken root, string normalizedDifficulty) {
            if (root is JArray rootArray) {
                return FindDifficultyInArray(rootArray, normalizedDifficulty);
            }

            var difficulties = root["difficulties"];
            if (difficulties is JArray difficultiesArray) {
                return FindDifficultyInArray(difficultiesArray, normalizedDifficulty);
            }

            return root["complexity"] != null ? root : null;
        }

        private static JToken? FindDifficultyInArray(JArray array, string normalizedDifficulty) {
            foreach (var item in array) {
                var difficulty = ReadString(item, "difficulty", "difficultyName", "difficultyString");
                if (string.Equals(difficulty, normalizedDifficulty, StringComparison.OrdinalIgnoreCase)) {
                    return item;
                }
            }

            return array.Count > 0 ? array[0] : null;
        }

        private readonly struct AccSaberScoreCacheEntry {
            public readonly AccSaberScoreInfo Info;
            public readonly DateTime ExpiresAt;

            public AccSaberScoreCacheEntry(AccSaberScoreInfo info, DateTime expiresAt) {
                Info = info;
                ExpiresAt = expiresAt;
            }
        }

        private readonly struct AccSaberProfileCacheEntry {
            public readonly AccSaberProfileInfo Info;
            public readonly DateTime ExpiresAt;

            public AccSaberProfileCacheEntry(AccSaberProfileInfo info, DateTime expiresAt) {
                Info = info;
                ExpiresAt = expiresAt;
            }
        }

        private readonly struct AccSaberDifficultyCacheEntry {
            public readonly AccSaberDifficultyInfo Info;
            public readonly DateTime ExpiresAt;

            public AccSaberDifficultyCacheEntry(AccSaberDifficultyInfo info, DateTime expiresAt) {
                Info = info;
                ExpiresAt = expiresAt;
            }
        }
    }

    internal readonly struct AccSaberScoreInfo {
        public readonly float Ap;
        public readonly bool HasAp;

        public AccSaberScoreInfo(float ap, bool hasAp) {
            Ap = ap;
            HasAp = hasAp;
        }
    }

    internal readonly struct AccSaberProfileInfo {
        public readonly int Rank;
        public readonly int CountryRank;
        public readonly float Ap;

        public AccSaberProfileInfo(int rank, int countryRank, float ap) {
            Rank = rank;
            CountryRank = countryRank;
            Ap = ap;
        }
    }

    internal readonly struct AccSaberDifficultyInfo {
        public static readonly AccSaberDifficultyInfo Unknown = new(false, 0.0f, string.Empty, string.Empty);

        public readonly bool IsKnown;
        public readonly float Complexity;
        public readonly string Status;
        public readonly string CategoryCode;

        public AccSaberDifficultyInfo(bool isKnown, float complexity, string status, string categoryCode) {
            IsKnown = isKnown;
            Complexity = complexity;
            Status = status;
            CategoryCode = categoryCode;
        }
    }
}
