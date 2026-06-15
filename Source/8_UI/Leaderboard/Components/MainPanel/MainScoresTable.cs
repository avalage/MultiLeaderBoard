using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BeatLeader.APIV2;
using BeatLeader.DataManager;
using BeatLeader.Manager;
using BeatLeader.Models;
using BeatLeader.UI;
using BeatSaberMarkupLanguage.Attributes;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace BeatLeader.Components {
    [ViewDefinition(Plugin.ResourcesPath + ".BSML.Leaderboard.Components.MainPanel.ScoresTable.bsml")]
    internal class MainScoresTable : AbstractScoresTable<ScoreRow> {
        #region Properties

        protected override int RowsCount => 10;
        protected override float RowWidth => 80;
        protected override float Spacing => 1.3f;
        protected override ScoreRowCellType CellTypeMask {
            get {
                var mask = PluginConfig.LeaderboardTableMask;
                return BeatLeaderDarkGoldTheme.Enabled ? mask | ScoreRowCellType.PerformancePoints : mask;
            }
        }

        #endregion

        #region Initialize/Dispose

        protected override void OnInitialize() {
            base.OnInitialize();

            ScoresRequest.StateChangedEvent += OnScoresRequestStateChanged;
            ClanScoresRequest.StateChangedEvent += OnScoresRequestStateChanged;
            LeaderboardsCache.CacheWasChangedEvent += OnLeaderboardsCacheChanged;

            LeaderboardState.IsVisibleChangedEvent += OnLeaderboardVisibleChanged;
            PluginConfig.LeaderboardTableMaskChangedEvent += OnLeaderboardTableMaskChanged;
            HiddenPlayersCache.HiddenPlayersUpdatedEvent += UpdateLayout;
            
            LeaderboardEvents.BattleRoyaleEnabledEvent += OnBattleRoyaleEnabledChanged;
            LeaderboardEvents.ScoreInfoButtonWasPressed += OnScoreClicked;
            
            OnLeaderboardTableMaskChanged(PluginConfig.LeaderboardTableMask);
        }

        protected override void OnDispose() {
            base.OnDispose();
            CancelScoreSaberPpRequest();

            ScoresRequest.StateChangedEvent -= OnScoresRequestStateChanged;
            ClanScoresRequest.StateChangedEvent -= OnScoresRequestStateChanged;
            LeaderboardsCache.CacheWasChangedEvent -= OnLeaderboardsCacheChanged;

            LeaderboardState.IsVisibleChangedEvent -= OnLeaderboardVisibleChanged;
            PluginConfig.LeaderboardTableMaskChangedEvent -= OnLeaderboardTableMaskChanged;
            HiddenPlayersCache.HiddenPlayersUpdatedEvent -= UpdateLayout;
            
            LeaderboardEvents.BattleRoyaleEnabledEvent -= OnBattleRoyaleEnabledChanged;
            LeaderboardEvents.ScoreInfoButtonWasPressed -= OnScoreClicked;
        }

        #endregion

        #region BattleRoyale

        protected override bool AllowExtraRow => !_battleRoyaleEnabled;

        private readonly HashSet<IScoreRowContent> _selectedContents = new();
        private bool _battleRoyaleEnabled;
        
        private void StartBattleRoyaleAnimation() {
            IEnumerator Coroutine() {
                yield return FadeOutCoroutine();
                yield return new WaitForSeconds(0.05f);
                yield return FadeInCoroutine(_content!);
            }

            StartCoroutine(Coroutine());
        }
        
        private void OnScoreClicked(Score score) {
            if (!_battleRoyaleEnabled) {
                return;
            }

            if (_selectedContents.Contains(score)) {
                _selectedContents.Remove(score);
            } else {
                _selectedContents.Add(score);
            }

            RefreshCells();
        }
        
        private void OnBattleRoyaleEnabledChanged(bool brEnabled) {
            _battleRoyaleEnabled = brEnabled;

            if (brEnabled) {
                _selectedContents.Clear();
            }

            RefreshCells();
            StartBattleRoyaleAnimation();
        }

        private void RefreshCells() {
            if (_content == null) {
                return;
            }

            if (_content.ExtraRowContent != null) {
                _extraRow.SetContent(_content.ExtraRowContent);
            }

            for (var i = 0; i < RowsCount; i++) {
                if (i >= _content.MainRowContents.Count) continue;

                var row = _mainRows[i];
                row.SetContent(_content.MainRowContents[i]);

                if (_battleRoyaleEnabled) {
                    row.SetCustomHighlight(content => content != null && _selectedContents.Contains(content));
                } else {
                    row.SetCustomHighlight(null);
                }
            }
        }

        #endregion

        #region Events

        private void OnScoresRequestStateChanged(WebRequests.IWebRequest<ScoresTableContent> instance, WebRequests.RequestState state, string? failReason) {
            if (state is not WebRequests.RequestState.Finished) {
                CancelScoreSaberPpRequest();
                _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
                PresentContent(null);
                return;
            }

            _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
            ClearScoreSaberPp(instance.Result);
            ApplyBeatLeaderPpVisibility(instance.Result);
            PresentContent(instance.Result);
            RequestScoreSaberPp(instance.Result);
        }

        private void OnLeaderboardVisibleChanged(bool isVisible) {
            if (isVisible) return;
            StartAnimation();
        }

        private void OnLeaderboardTableMaskChanged(ScoreRowCellType value) {
            UpdateLayout();
        }

        private void OnLeaderboardsCacheChanged() {
            if (_content == null || !ApplyBeatLeaderPpVisibility(_content)) {
                return;
            }

            RefreshCells();
            UpdateLayout();
        }

        #endregion

        #region BeatLeader PP visibility

        private static bool ApplyBeatLeaderPpVisibility(ScoresTableContent content) {
            var displayBeatLeaderPp = ShouldDisplayBeatLeaderPp();
            var changed = false;

            foreach (var score in EnumerateScoreRows(content)) {
                if (score.displayBeatLeaderPp == displayBeatLeaderPp) {
                    continue;
                }

                score.displayBeatLeaderPp = displayBeatLeaderPp;
                changed = true;
            }

            return changed;
        }

        private static bool ShouldDisplayBeatLeaderPp() {
            if (!LeaderboardsCache.TryGetLeaderboardInfo(LeaderboardState.SelectedLeaderboardKey, out var data)) {
                return false;
            }

            return FormatUtils.GetRankedStatus(data.DifficultyInfo) is RankedStatus.Ranked;
        }

        #endregion

        #region ScoreSaber PP

        private const string ScoreSaberApiBaseUrl = "https://scoresaber.com/api";
        private const float ScoreSaberRequestDebounceSeconds = 0.18f;
        private const float ScoreSaberNoFailAccuracyMultiplier = 0.5f;
        private const int ScoreSaberLeaderboardInfoTimeoutSeconds = 6;
        private static readonly TimeSpan ScoreSaberLeaderboardInfoCacheTtl = TimeSpan.FromMinutes(15);
        private static readonly Dictionary<string, ScoreSaberLeaderboardInfoCacheEntry> ScoreSaberLeaderboardInfoCache = new();
        // Matches PPCounter's ScoreSaber standardCurve from https://cdn.pulselane.dev/curves.json.
        private static readonly ScoreSaberCurvePoint[] ScoreSaberAccuracyCurve = {
            new(0, 0),
            new(0.6, 0.182232335),
            new(0.65, 0.586601),
            new(0.7, 0.6125566),
            new(0.75, 0.6451808),
            new(0.8, 0.6872269),
            new(0.825, 0.7150466),
            new(0.85, 0.746229053),
            new(0.875, 0.781693459),
            new(0.9, 0.825756133),
            new(0.91, 0.8488376),
            new(0.92, 0.872871041),
            new(0.93, 0.9039994),
            new(0.94, 0.9417363),
            new(0.95, 1),
            new(0.955, 1.0388633),
            new(0.96, 1.08718836),
            new(0.965, 1.155212),
            new(0.97, 1.24858081),
            new(0.9725, 1.30903327),
            new(0.975, 1.38071024),
            new(0.9775, 1.46647263),
            new(0.98, 1.570241),
            new(0.9825, 1.69753623),
            new(0.985, 1.85638881),
            new(0.9875, 2.058947),
            new(0.99, 2.32450628),
            new(0.99125, 2.49029064),
            new(0.9925, 2.68566775),
            new(0.99375, 2.91901565),
            new(0.995, 3.20220184),
            new(0.99625, 3.55261445),
            new(0.9975, 3.99679351),
            new(0.99825, 4.32502747),
            new(0.999, 4.715471),
            new(0.9995, 5.01954365),
            new(1, 5.36739445)
        };

        private Coroutine? _scoreSaberPpCoroutine;
        private int _scoreSaberPpRequestId;
        private ScoreSaberLeaderboardInfo _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
        private UnityWebRequest? _activeScoreSaberLeaderboardInfoRequest;

        private static void ClearScoreSaberPp(ScoresTableContent content) {
            foreach (var score in EnumerateScoreRows(content)) {
                score.scoreSaberPp = 0.0f;
                score.withScoreSaberPp = false;
                score.scoreSaberFcPp = 0.0f;
                score.withScoreSaberFcPp = false;
            }
        }

        private void RequestScoreSaberPp(ScoresTableContent? content) {
            CancelScoreSaberPpRequest();

            if (!BeatLeaderDarkGoldTheme.Enabled || content == null || !CellTypeMask.HasFlag(ScoreRowCellType.PerformancePoints)) {
                return;
            }

            var rows = EnumerateScoreRows(content).ToArray();
            if (rows.Length == 0) {
                return;
            }

            var leaderboardKey = LeaderboardState.SelectedLeaderboardKey;
            if (string.IsNullOrEmpty(leaderboardKey.Hash) || string.IsNullOrEmpty(leaderboardKey.Mode) || leaderboardKey.DiffId == 0) {
                return;
            }

            _scoreSaberPpCoroutine = StartCoroutine(LoadScoreSaberPpCoroutine(_scoreSaberPpRequestId, content, rows, leaderboardKey));
        }

        private void CancelScoreSaberPpRequest() {
            _scoreSaberPpRequestId++;
            if (_scoreSaberPpCoroutine == null) {
                DisposeActiveScoreSaberRequests();
                return;
            }

            StopCoroutine(_scoreSaberPpCoroutine);
            _scoreSaberPpCoroutine = null;
            DisposeActiveScoreSaberRequests();
        }

        private IEnumerator LoadScoreSaberPpCoroutine(int requestId, ScoresTableContent content, Score[] rows, LeaderboardKey leaderboardKey) {
            yield return new WaitForSecondsRealtime(ScoreSaberRequestDebounceSeconds);
            if (requestId != _scoreSaberPpRequestId || !ReferenceEquals(content, _content)) {
                yield break;
            }

            yield return LoadScoreSaberLeaderboardInfo(requestId, content, leaderboardKey);

            if (requestId != _scoreSaberPpRequestId || !ReferenceEquals(content, _content)) {
                yield break;
            }

            if (!_scoreSaberLeaderboardInfo.IsRanked) {
                _scoreSaberPpCoroutine = null;
                RefreshCells();
                UpdateLayout();
                yield break;
            }

            var anyEstimated = ApplyScoreSaberPp(rows, _scoreSaberLeaderboardInfo);
            _scoreSaberPpCoroutine = null;

            if (!anyEstimated) {
                yield break;
            }

            foreach (var row in rows) {
                if (row.withScoreSaberPp) {
                    LeaderboardEvents.NotifyScorePerformancePointsUpdated(row);
                }
            }

            RefreshCells();
            UpdateLayout();
        }

        private IEnumerator LoadScoreSaberLeaderboardInfo(int requestId, ScoresTableContent content, LeaderboardKey leaderboardKey) {
            var cacheKey = ScoreSaberLeaderboardCacheKey(leaderboardKey);
            if (TryGetCachedScoreSaberLeaderboardInfo(cacheKey, out var cachedInfo)) {
                _scoreSaberLeaderboardInfo = cachedInfo;
                yield break;
            }

            var url = ScoreSaberLeaderboardInfoUrl(leaderboardKey);
            using var request = UnityWebRequest.Get(url);
            _activeScoreSaberLeaderboardInfoRequest = request;
            request.timeout = ScoreSaberLeaderboardInfoTimeoutSeconds;
            request.SetRequestHeader("User-Agent", Plugin.UserAgent);
            yield return request.SendWebRequest();
            ClearActiveScoreSaberLeaderboardInfoRequest(request);

            if (requestId != _scoreSaberPpRequestId || !ReferenceEquals(content, _content)) {
                yield break;
            }

            if (request.isNetworkError || request.isHttpError) {
                Plugin.Log.Debug($"[ScoreSaberPP] Failed to load leaderboard info for {leaderboardKey.Hash}/{leaderboardKey.Mode}/{leaderboardKey.Diff}: {request.responseCode} {request.error}");
                _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
                yield break;
            }

            if (!TryReadScoreSaberLeaderboardInfo(request.downloadHandler.text, out var info)) {
                Plugin.Log.Info($"[ScoreSaberPP] Could not parse leaderboard info for {leaderboardKey.Hash}/{leaderboardKey.Mode}/{leaderboardKey.Diff}");
                _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
                yield break;
            }

            _scoreSaberLeaderboardInfo = info;
            ScoreSaberLeaderboardInfoCache[cacheKey] = new ScoreSaberLeaderboardInfoCacheEntry(
                info,
                DateTime.UtcNow.Add(ScoreSaberLeaderboardInfoCacheTtl)
            );
        }

        private static string ScoreSaberLeaderboardInfoUrl(LeaderboardKey leaderboardKey) {
            var hash = Uri.EscapeDataString(leaderboardKey.Hash);
            var mode = Uri.EscapeDataString($"Solo{leaderboardKey.Mode}");
            return $"{ScoreSaberApiBaseUrl}/leaderboard/by-hash/{hash}/info?difficulty={leaderboardKey.DiffId}&gameMode={mode}";
        }

        private static bool ApplyScoreSaberPp(IEnumerable<Score> rows, ScoreSaberLeaderboardInfo leaderboardInfo) {
            var updated = false;
            foreach (var row in rows) {
                if (!TryCalculateScoreSaberPp(row.accuracy, row.modifiers, leaderboardInfo, out var pp)) {
                    continue;
                }

                row.scoreSaberPp = pp;
                row.withScoreSaberPp = true;
                if (!row.fullCombo && TryCalculateScoreSaberPp(row.fcAccuracy, row.modifiers, leaderboardInfo, out var fcPp)) {
                    row.scoreSaberFcPp = fcPp;
                    row.withScoreSaberFcPp = true;
                }

                updated = true;
            }

            return updated;
        }

        private static bool TryCalculateScoreSaberPp(float accuracy, string? modifiers, ScoreSaberLeaderboardInfo leaderboardInfo, out float pp) {
            pp = 0.0f;

            var normalizedAccuracy = accuracy > 1.0f ? accuracy / 100.0f : accuracy;
            normalizedAccuracy *= ScoreSaberAccuracyModifierMultiplier(modifiers);
            normalizedAccuracy = Mathf.Clamp01(normalizedAccuracy);
            if (normalizedAccuracy <= 0.0f || !leaderboardInfo.CanEstimatePp) {
                return false;
            }

            var accuracyMultiplier = ScoreSaberCurveMultiplier(normalizedAccuracy);
            if (accuracyMultiplier <= 0.0) {
                return false;
            }

            var value = leaderboardInfo.MaxPp * accuracyMultiplier;

            pp = (float)Math.Round(value, 2);
            return pp > 0.0f;
        }

        private static float ScoreSaberAccuracyModifierMultiplier(string? modifiers) {
            return HasScoreSaberModifier(modifiers, "NF") ? ScoreSaberNoFailAccuracyMultiplier : 1.0f;
        }

        private static bool HasScoreSaberModifier(string? modifiers, string modifier) {
            if (string.IsNullOrWhiteSpace(modifiers)) {
                return false;
            }

            foreach (var item in modifiers.Split(',', ';', ' ')) {
                if (string.Equals(item.Trim(), modifier, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        private static double ScoreSaberCurveMultiplier(double accuracy) {
            if (accuracy >= ScoreSaberAccuracyCurve[^1].Accuracy) {
                return ScoreSaberAccuracyCurve[^1].Multiplier;
            }

            for (var i = 0; i < ScoreSaberAccuracyCurve.Length - 1; i++) {
                var lower = ScoreSaberAccuracyCurve[i];
                var upper = ScoreSaberAccuracyCurve[i + 1];
                if (accuracy < lower.Accuracy || accuracy > upper.Accuracy) {
                    continue;
                }

                var range = upper.Accuracy - lower.Accuracy;
                if (range <= 0.0) {
                    return upper.Multiplier;
                }

                var t = (accuracy - lower.Accuracy) / range;
                return lower.Multiplier + (upper.Multiplier - lower.Multiplier) * t;
            }

            return 0.0;
        }

        private static IEnumerable<Score> EnumerateScoreRows(ScoresTableContent content) {
            if (content.ExtraRowContent is Score extraScore) {
                yield return extraScore;
            }

            foreach (var row in content.MainRowContents) {
                if (row is Score score) {
                    yield return score;
                }
            }
        }

        private void DisposeActiveScoreSaberRequests() {
            if (_activeScoreSaberLeaderboardInfoRequest != null) {
                _activeScoreSaberLeaderboardInfoRequest.Abort();
                _activeScoreSaberLeaderboardInfoRequest.Dispose();
                _activeScoreSaberLeaderboardInfoRequest = null;
            }
        }

        private void ClearActiveScoreSaberLeaderboardInfoRequest(UnityWebRequest request) {
            if (ReferenceEquals(_activeScoreSaberLeaderboardInfoRequest, request)) {
                _activeScoreSaberLeaderboardInfoRequest = null;
            }
        }

        private static bool TryGetCachedScoreSaberLeaderboardInfo(string cacheKey, out ScoreSaberLeaderboardInfo info) {
            info = ScoreSaberLeaderboardInfo.Unknown;
            if (!ScoreSaberLeaderboardInfoCache.TryGetValue(cacheKey, out var cacheEntry)) {
                return false;
            }

            if (DateTime.UtcNow >= cacheEntry.ExpiresAt) {
                ScoreSaberLeaderboardInfoCache.Remove(cacheKey);
                return false;
            }

            info = cacheEntry.Info;
            return true;
        }

        private static string ScoreSaberLeaderboardCacheKey(LeaderboardKey leaderboardKey) {
            return $"{leaderboardKey.Hash}|{leaderboardKey.DiffId}|{NormalizeScoreSaberMode(leaderboardKey.Mode)}";
        }

        private static bool TryReadScoreSaberLeaderboardInfo(string json, out ScoreSaberLeaderboardInfo info) {
            info = ScoreSaberLeaderboardInfo.Unknown;

            try {
                var root = JObject.Parse(json);
                var infoToken = root["leaderboardInfo"] ?? root;

                var ranked = ReadBool(infoToken, "ranked") ?? false;
                var stars = ReadDouble(infoToken, "stars") ?? 0.0;
                var maxPp = ReadDouble(infoToken, "maxPP") ?? 0.0;

                info = new ScoreSaberLeaderboardInfo(ranked, stars, maxPp);
                return true;
            } catch (Exception ex) {
                Plugin.Log.Debug($"[ScoreSaberPP] Failed to parse leaderboard info: {ex.Message}");
                return false;
            }
        }

        private static string NormalizeScoreSaberMode(string mode) {
            return mode.StartsWith("Solo", StringComparison.OrdinalIgnoreCase) ? mode : $"Solo{mode}";
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

            return double.TryParse(value.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;
        }

        private readonly struct ScoreSaberLeaderboardInfoCacheEntry {
            public readonly ScoreSaberLeaderboardInfo Info;
            public readonly DateTime ExpiresAt;

            public ScoreSaberLeaderboardInfoCacheEntry(ScoreSaberLeaderboardInfo info, DateTime expiresAt) {
                Info = info;
                ExpiresAt = expiresAt;
            }
        }

        private readonly struct ScoreSaberLeaderboardInfo {
            public static readonly ScoreSaberLeaderboardInfo Unknown = new(false, 0.0, 0.0);

            public readonly bool IsRanked;
            public readonly double Stars;
            public readonly double MaxPp;

            public bool CanEstimatePp => MaxPp > 0.0;

            public ScoreSaberLeaderboardInfo(bool isRanked, double stars, double maxPp) {
                IsRanked = isRanked;
                Stars = stars;
                MaxPp = maxPp;
            }
        }

        private readonly struct ScoreSaberCurvePoint {
            public readonly double Accuracy;
            public readonly double Multiplier;

            public ScoreSaberCurvePoint(double accuracy, double multiplier) {
                Accuracy = accuracy;
                Multiplier = multiplier;
            }
        }

        #endregion
    }
}
