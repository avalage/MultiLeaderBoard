using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BeatLeader.APIV2;
using BeatLeader.DataManager;
using BeatLeader.Manager;
using BeatLeader.Models;
using BeatLeader.UI;
using BeatSaberMarkupLanguage.Attributes;
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
            UploadReplayRequest.StateChangedEvent += OnUploadRequestStateChanged;
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
            CancelPerformancePointRequests();
            CancelFreshUploadAccSaberRefresh();

            ScoresRequest.StateChangedEvent -= OnScoresRequestStateChanged;
            ClanScoresRequest.StateChangedEvent -= OnScoresRequestStateChanged;
            UploadReplayRequest.StateChangedEvent -= OnUploadRequestStateChanged;
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
            var result = instance?.Result;
            Plugin.Log.Info($"[ScoresTable] ScoresRequest state={state}; failReason={failReason ?? "none"}; rows={result?.MainRowContents.Count.ToString() ?? "null"}; extraRow={result?.ExtraRowContent != null}; currentContent={_content != null}");
            if (state is not WebRequests.RequestState.Finished) {
                CancelPerformancePointRequests();
                _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
                PresentContent(null);
                return;
            }

            if (result == null) {
                Plugin.Log.Warn("[ScoresTable] Finished score request without result content.");
                PresentContent(null);
                return;
            }

            _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
            ClearScoreSaberPp(result);
            ClearAccSaberAp(result);
            ApplyBeatLeaderPpVisibility(result);
            PresentContent(result);
            Plugin.Log.Info($"[ScoresTable] Presented new scores content; rows={result.MainRowContents.Count}; page={result.CurrentPage}");
            RequestScoreSaberPp(result);
            RequestAccSaberAp(result);
        }

        private void OnLeaderboardVisibleChanged(bool isVisible) {
            if (isVisible) {
                if (_content != null) {
                    RequestScoreSaberPp(_content);
                    RequestAccSaberAp(_content);
                }

                return;
            }

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

        #region Performance point requests

        private void CancelPerformancePointRequests() {
            CancelScoreSaberPpRequest();
            CancelAccSaberApRequest();
        }

        #endregion

        #region ScoreSaber PP

        private const float ScoreSaberRequestDebounceSeconds = 0.18f;
        private const float ScoreSaberNoFailAccuracyMultiplier = 0.5f;
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
                Plugin.Log.Info($"[ScoreSaberPP] Request skipped; theme={BeatLeaderDarkGoldTheme.Enabled}; contentNull={content == null}; ppCell={CellTypeMask.HasFlag(ScoreRowCellType.PerformancePoints)}");
                return;
            }

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) {
                Plugin.Log.Info("[ScoreSaberPP] Request skipped; scores table is inactive.");
                return;
            }

            var rows = EnumerateScoreRows(content).ToArray();
            if (rows.Length == 0) {
                Plugin.Log.Info("[ScoreSaberPP] Request skipped; no score rows.");
                return;
            }

            var leaderboardKey = LeaderboardState.SelectedLeaderboardKey;
            if (string.IsNullOrEmpty(leaderboardKey.Hash) || string.IsNullOrEmpty(leaderboardKey.Mode) || leaderboardKey.DiffId == 0) {
                Plugin.Log.Info($"[ScoreSaberPP] Request skipped; invalid leaderboard key hash={leaderboardKey.Hash}, diff={leaderboardKey.Diff}, mode={leaderboardKey.Mode}, diffId={leaderboardKey.DiffId}.");
                return;
            }

            Plugin.Log.Info($"[ScoreSaberPP] Request scheduled; rows={rows.Length}; hash={leaderboardKey.Hash}; diff={leaderboardKey.Diff}; mode={leaderboardKey.Mode}.");
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
                Plugin.Log.Info($"[ScoreSaberPP] Leaderboard is not ranked or unknown; ranked={_scoreSaberLeaderboardInfo.IsRanked}; known={_scoreSaberLeaderboardInfo.IsKnown}.");
                _scoreSaberPpCoroutine = null;
                RefreshCells();
                UpdateLayout();
                yield break;
            }

            var anyEstimated = ApplyScoreSaberPp(rows, _scoreSaberLeaderboardInfo);
            _scoreSaberPpCoroutine = null;
            Plugin.Log.Info($"[ScoreSaberPP] Estimate finished; updated={anyEstimated}; rows={rows.Length}; maxPp={_scoreSaberLeaderboardInfo.MaxPp}.");

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
            if (ScoreSaberLeaderboardInfoProvider.TryGetCached(leaderboardKey, out var cachedInfo)) {
                _scoreSaberLeaderboardInfo = cachedInfo;
                Plugin.Log.Info($"[ScoreSaberPP] Leaderboard info cache hit; ranked={cachedInfo.IsRanked}; stars={cachedInfo.Stars}; maxPp={cachedInfo.MaxPp}.");
                yield break;
            }

            var url = ScoreSaberLeaderboardInfoProvider.BuildInfoUrl(leaderboardKey);
            Plugin.Log.Info($"[ScoreSaberPP] Loading leaderboard info: {url}");
            using var request = UnityWebRequest.Get(url);
            _activeScoreSaberLeaderboardInfoRequest = request;
            request.timeout = ScoreSaberLeaderboardInfoProvider.TimeoutSeconds;
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

            if (!ScoreSaberLeaderboardInfoProvider.TryRead(request.downloadHandler.text, out var info)) {
                Plugin.Log.Info($"[ScoreSaberPP] Could not parse leaderboard info for {leaderboardKey.Hash}/{leaderboardKey.Mode}/{leaderboardKey.Diff}");
                _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
                yield break;
            }

            _scoreSaberLeaderboardInfo = info;
            ScoreSaberLeaderboardInfoProvider.SaveToCache(leaderboardKey, info);
            Plugin.Log.Info($"[ScoreSaberPP] Leaderboard info loaded; ranked={info.IsRanked}; stars={info.Stars}; maxPp={info.MaxPp}.");
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

        private readonly struct ScoreSaberCurvePoint {
            public readonly double Accuracy;
            public readonly double Multiplier;

            public ScoreSaberCurvePoint(double accuracy, double multiplier) {
                Accuracy = accuracy;
                Multiplier = multiplier;
            }
        }

        #endregion

        #region AccSaber AP

        private const float AccSaberRequestDebounceSeconds = 0.24f;
        private const float AccSaberFreshUploadCacheBypassSeconds = 45.0f;
        private const float AccSaberFreshUploadRetryDelaySeconds = 7.0f;
        private const int AccSaberFreshUploadRetryCount = 5;
        private Coroutine? _accSaberApCoroutine;
        private int _accSaberApRequestId;
        private readonly List<UnityWebRequest> _activeAccSaberScoreRequests = new();
        private LeaderboardKey _lastUploadLeaderboardKey;
        private LeaderboardKey _freshUploadAccSaberLeaderboardKey;
        private string? _freshUploadAccSaberPlayerId;
        private float _freshUploadAccSaberExpiresAt;
        private Coroutine? _freshUploadAccSaberRefreshCoroutine;
        private int _freshUploadAccSaberRefreshRequestId;
        private UnityWebRequest? _activeFreshUploadAccSaberRequest;

        private static void ClearAccSaberAp(ScoresTableContent content) {
            foreach (var score in EnumerateScoreRows(content)) {
                score.accSaberAp = 0.0f;
                score.withAccSaberAp = false;
            }
        }

        private void RequestAccSaberAp(ScoresTableContent? content) {
            CancelAccSaberApRequest();

            if (!BeatLeaderDarkGoldTheme.Enabled || content == null || !CellTypeMask.HasFlag(ScoreRowCellType.PerformancePoints)) {
                Plugin.Log.Info($"[AccSaberAP] Request skipped; theme={BeatLeaderDarkGoldTheme.Enabled}; contentNull={content == null}; ppCell={CellTypeMask.HasFlag(ScoreRowCellType.PerformancePoints)}");
                return;
            }

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) {
                Plugin.Log.Info("[AccSaberAP] Request skipped; scores table is inactive.");
                return;
            }

            var leaderboardKey = LeaderboardState.SelectedLeaderboardKey;
            if (!AccSaberApiProvider.CanRequestScore(leaderboardKey)) {
                Plugin.Log.Info($"[AccSaberAP] Request skipped; leaderboard key is not supported hash={leaderboardKey.Hash}, diff={leaderboardKey.Diff}, mode={leaderboardKey.Mode}.");
                return;
            }

            var rows = EnumerateScoreRows(content)
                .Where(row => !string.IsNullOrEmpty(row.originalPlayer?.id))
                .ToArray();
            if (rows.Length == 0) {
                Plugin.Log.Info("[AccSaberAP] Request skipped; no score rows with player ids.");
                return;
            }

            var pendingRows = new List<Score>(rows.Length);
            var updatedFromCache = false;
            foreach (var row in rows) {
                var playerId = row.originalPlayer.id;
                if (AccSaberApiProvider.TryGetCachedScore(leaderboardKey, playerId, out var cachedInfo)) {
                    if (ApplyAccSaberAp(row, cachedInfo)) {
                        updatedFromCache = true;
                    }
                } else {
                    pendingRows.Add(row);
                }
            }

            if (updatedFromCache) {
                Plugin.Log.Info($"[AccSaberAP] Applied cached AP for rows; totalRows={rows.Length}; pendingRows={pendingRows.Count}.");
                NotifyPerformancePointsUpdated(rows);
                RefreshCells();
                UpdateLayout();
            }

            if (pendingRows.Count == 0) {
                Plugin.Log.Info("[AccSaberAP] No pending AP requests after cache lookup.");
                return;
            }

            Plugin.Log.Info($"[AccSaberAP] Request scheduled; pendingRows={pendingRows.Count}; hash={leaderboardKey.Hash}; diff={leaderboardKey.Diff}; mode={leaderboardKey.Mode}.");
            _accSaberApCoroutine = StartCoroutine(LoadAccSaberApCoroutine(_accSaberApRequestId, content, pendingRows.ToArray(), leaderboardKey));
        }

        private void CancelAccSaberApRequest() {
            _accSaberApRequestId++;
            if (_accSaberApCoroutine != null) {
                StopCoroutine(_accSaberApCoroutine);
                _accSaberApCoroutine = null;
            }

            DisposeActiveAccSaberRequests();
        }

        private IEnumerator LoadAccSaberApCoroutine(int requestId, ScoresTableContent content, Score[] rows, LeaderboardKey leaderboardKey) {
            yield return new WaitForSecondsRealtime(AccSaberRequestDebounceSeconds);
            if (requestId != _accSaberApRequestId || !ReferenceEquals(content, _content)) {
                yield break;
            }

            var requests = new List<AccSaberScoreRequest>(rows.Length);
            var updated = false;
            foreach (var row in rows) {
                var playerId = row.originalPlayer.id;
                if (AccSaberApiProvider.TryGetCachedScore(leaderboardKey, playerId, out var cachedInfo)) {
                    updated |= ApplyAccSaberAp(row, cachedInfo);
                    continue;
                }

                var request = UnityWebRequest.Get(AccSaberApiProvider.BuildScoreUrl(leaderboardKey, playerId));
                request.timeout = AccSaberApiProvider.TimeoutSeconds;
                request.SetRequestHeader("User-Agent", Plugin.UserAgent);
                _activeAccSaberScoreRequests.Add(request);
                requests.Add(new AccSaberScoreRequest(row, request));
                request.SendWebRequest();
            }

            Plugin.Log.Info($"[AccSaberAP] Sent AP requests; count={requests.Count}; hash={leaderboardKey.Hash}; diff={leaderboardKey.Diff}; mode={leaderboardKey.Mode}.");

            while (requests.Any(item => !item.Request.isDone)) {
                if (requestId != _accSaberApRequestId || !ReferenceEquals(content, _content)) {
                    yield break;
                }

                yield return null;
            }

            foreach (var item in requests) {
                var request = item.Request;
                ClearActiveAccSaberScoreRequest(request);

                var playerId = item.Score.originalPlayer.id;
                if (request.isNetworkError || request.isHttpError) {
                    if (request.responseCode == 404) {
                        AccSaberApiProvider.SaveScoreToCache(leaderboardKey, playerId, default);
                    } else {
                        Plugin.Log.Debug($"[AccSaberAP] Failed to load score for {playerId}: {request.responseCode} {request.error}");
                    }

                    request.Dispose();
                    continue;
                }

                if (AccSaberApiProvider.TryReadScore(request.downloadHandler.text, out var info)) {
                    AccSaberApiProvider.SaveScoreToCache(leaderboardKey, playerId, info);
                    updated |= ApplyAccSaberAp(item.Score, info);
                } else {
                    AccSaberApiProvider.SaveScoreToCache(leaderboardKey, playerId, default);
                }

                request.Dispose();
            }

            _accSaberApCoroutine = null;
            Plugin.Log.Info($"[AccSaberAP] AP requests finished; updated={updated}; rows={rows.Length}.");
            if (!updated || requestId != _accSaberApRequestId || !ReferenceEquals(content, _content)) {
                yield break;
            }

            NotifyPerformancePointsUpdated(rows);
            RefreshCells();
            UpdateLayout();
        }

        private static bool ApplyAccSaberAp(Score row, AccSaberScoreInfo info) {
            if (!info.HasAp || info.Ap <= 0.0f) {
                return false;
            }

            if (row.withAccSaberAp && Mathf.Approximately(row.accSaberAp, info.Ap)) {
                return false;
            }

            row.accSaberAp = info.Ap;
            row.withAccSaberAp = true;
            return true;
        }

        private void OnUploadRequestStateChanged(WebRequests.IWebRequest<ScoreUploadResponse> instance, WebRequests.RequestState state, string? failReason) {
            var result = instance?.Result;
            Plugin.Log.Info($"[AccSaberAP] Upload state={state}; status={result?.Status.ToString() ?? "null"}; failReason={failReason ?? "none"}.");
            switch (state) {
                case WebRequests.RequestState.Started:
                    _lastUploadLeaderboardKey = LeaderboardState.SelectedLeaderboardKey;
                    break;
                case WebRequests.RequestState.Finished:
                    RegisterFreshUploadAccSaberScore(result);
                    break;
            }
        }

        private void RegisterFreshUploadAccSaberScore(ScoreUploadResponse? response) {
            if (response?.Score?.originalPlayer == null || response.Status == ScoreUploadStatus.Error) {
                Plugin.Log.Info($"[AccSaberAP] Fresh upload registration skipped; responseNull={response == null}; scoreNull={response?.Score == null}; playerNull={response?.Score?.originalPlayer == null}; status={response?.Status.ToString() ?? "null"}.");
                return;
            }

            var playerId = response.Score.originalPlayer.id;
            if (string.IsNullOrEmpty(playerId) || !AccSaberApiProvider.CanRequestScore(_lastUploadLeaderboardKey)) {
                Plugin.Log.Info($"[AccSaberAP] Fresh upload registration skipped; playerId={playerId ?? "null"}; canRequest={AccSaberApiProvider.CanRequestScore(_lastUploadLeaderboardKey)}.");
                return;
            }

            _freshUploadAccSaberLeaderboardKey = _lastUploadLeaderboardKey;
            _freshUploadAccSaberPlayerId = playerId;
            _freshUploadAccSaberExpiresAt = Time.realtimeSinceStartup + AccSaberFreshUploadCacheBypassSeconds;
            AccSaberApiProvider.ClearScoreCache(_lastUploadLeaderboardKey, playerId);

            Plugin.Log.Info($"[AccSaberAP] Fresh upload registered; playerId={playerId}; hash={_lastUploadLeaderboardKey.Hash}; diff={_lastUploadLeaderboardKey.Diff}; mode={_lastUploadLeaderboardKey.Mode}.");
            ScheduleFreshUploadAccSaberRefresh(_lastUploadLeaderboardKey, playerId);
        }

        private bool IsFreshUploadAccSaberScore(LeaderboardKey leaderboardKey, string playerId) {
            return !string.IsNullOrEmpty(_freshUploadAccSaberPlayerId) &&
                string.Equals(_freshUploadAccSaberPlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
                _freshUploadAccSaberLeaderboardKey.Equals(leaderboardKey) &&
                Time.realtimeSinceStartup <= _freshUploadAccSaberExpiresAt;
        }

        private void ScheduleFreshUploadAccSaberRefresh(LeaderboardKey leaderboardKey, string playerId) {
            CancelFreshUploadAccSaberRefresh();
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) {
                Plugin.Log.Info("[AccSaberAP] Fresh upload refresh not scheduled; scores table is inactive.");
                return;
            }

            Plugin.Log.Info($"[AccSaberAP] Fresh upload refresh scheduled; playerId={playerId}; hash={leaderboardKey.Hash}; diff={leaderboardKey.Diff}; mode={leaderboardKey.Mode}; attempts={AccSaberFreshUploadRetryCount}.");
            _freshUploadAccSaberRefreshCoroutine = StartCoroutine(FreshUploadAccSaberRefreshCoroutine(
                ++_freshUploadAccSaberRefreshRequestId,
                leaderboardKey,
                playerId
            ));
        }

        private IEnumerator FreshUploadAccSaberRefreshCoroutine(int requestId, LeaderboardKey leaderboardKey, string playerId) {
            for (var attempt = 0; attempt < AccSaberFreshUploadRetryCount; attempt++) {
                if (attempt > 0) {
                    yield return new WaitForSecondsRealtime(AccSaberFreshUploadRetryDelaySeconds);
                }

                Plugin.Log.Info($"[AccSaberAP] Fresh upload refresh attempt {attempt + 1}/{AccSaberFreshUploadRetryCount}; playerId={playerId}; requestId={requestId}.");

                if (requestId != _freshUploadAccSaberRefreshRequestId || !IsFreshUploadAccSaberScore(leaderboardKey, playerId)) {
                    Plugin.Log.Info("[AccSaberAP] Fresh upload refresh stopped; request id or fresh score marker is no longer valid.");
                    yield break;
                }

                if (!TryGetCurrentScoreRow(playerId, out var score)) {
                    Plugin.Log.Info("[AccSaberAP] Fresh upload refresh attempt skipped; current score row was not found.");
                    continue;
                }

                if (score.withAccSaberAp) {
                    Plugin.Log.Info("[AccSaberAP] Fresh upload refresh stopped; score already has AccSaber AP.");
                    break;
                }

                if (!leaderboardKey.Equals(LeaderboardState.SelectedLeaderboardKey) || _content == null) {
                    Plugin.Log.Info("[AccSaberAP] Fresh upload refresh attempt skipped; selected leaderboard changed or content is null.");
                    continue;
                }

                yield return LoadFreshUploadAccSaberScoreCoroutine(requestId, leaderboardKey, score);
            }

            _freshUploadAccSaberRefreshCoroutine = null;
        }

        private IEnumerator LoadFreshUploadAccSaberScoreCoroutine(int requestId, LeaderboardKey leaderboardKey, Score score) {
            var playerId = score.originalPlayer?.id;
            if (string.IsNullOrEmpty(playerId) || !IsFreshUploadAccSaberScore(leaderboardKey, playerId)) {
                yield break;
            }

            AccSaberApiProvider.ClearScoreCache(leaderboardKey, playerId);

            using var request = UnityWebRequest.Get(AccSaberApiProvider.BuildScoreUrl(leaderboardKey, playerId));
            _activeFreshUploadAccSaberRequest = request;
            request.timeout = AccSaberApiProvider.TimeoutSeconds;
            request.SetRequestHeader("User-Agent", Plugin.UserAgent);
            yield return request.SendWebRequest();
            ClearActiveFreshUploadAccSaberRequest(request);

            if (requestId != _freshUploadAccSaberRefreshRequestId ||
                !leaderboardKey.Equals(LeaderboardState.SelectedLeaderboardKey) ||
                !IsFreshUploadAccSaberScore(leaderboardKey, playerId)) {
                yield break;
            }

            if (request.isNetworkError || request.isHttpError) {
                Plugin.Log.Debug($"[AccSaberAP] Fresh uploaded score refresh failed for {playerId}: {request.responseCode} {request.error}");
                yield break;
            }

            if (!AccSaberApiProvider.TryReadScore(request.downloadHandler.text, out var info)) {
                Plugin.Log.Debug($"[AccSaberAP] Fresh uploaded score for {playerId} returned no AP yet.");
                yield break;
            }

            AccSaberApiProvider.SaveScoreToCache(leaderboardKey, playerId, info);
            if (!ApplyAccSaberAp(score, info)) {
                yield break;
            }

            LeaderboardEvents.NotifyScorePerformancePointsUpdated(score);
            RefreshCells();
            UpdateLayout();
        }

        private bool TryGetCurrentScoreRow(string playerId, out Score score) {
            score = null!;
            if (_content == null) {
                return false;
            }

            foreach (var row in EnumerateScoreRows(_content)) {
                if (string.Equals(row.originalPlayer?.id, playerId, StringComparison.OrdinalIgnoreCase)) {
                    score = row;
                    return true;
                }
            }

            return false;
        }

        private void CancelFreshUploadAccSaberRefresh() {
            _freshUploadAccSaberRefreshRequestId++;
            if (_freshUploadAccSaberRefreshCoroutine == null) {
                DisposeActiveFreshUploadAccSaberRequest();
                return;
            }

            StopCoroutine(_freshUploadAccSaberRefreshCoroutine);
            _freshUploadAccSaberRefreshCoroutine = null;
            DisposeActiveFreshUploadAccSaberRequest();
        }

        private void DisposeActiveFreshUploadAccSaberRequest() {
            if (_activeFreshUploadAccSaberRequest == null) {
                return;
            }

            _activeFreshUploadAccSaberRequest.Abort();
            _activeFreshUploadAccSaberRequest.Dispose();
            _activeFreshUploadAccSaberRequest = null;
        }

        private void ClearActiveFreshUploadAccSaberRequest(UnityWebRequest request) {
            if (ReferenceEquals(_activeFreshUploadAccSaberRequest, request)) {
                _activeFreshUploadAccSaberRequest = null;
            }
        }

        private static void NotifyPerformancePointsUpdated(IEnumerable<Score> rows) {
            foreach (var row in rows) {
                LeaderboardEvents.NotifyScorePerformancePointsUpdated(row);
            }
        }

        private void DisposeActiveAccSaberRequests() {
            foreach (var request in _activeAccSaberScoreRequests) {
                request.Abort();
                request.Dispose();
            }

            _activeAccSaberScoreRequests.Clear();
        }

        private void ClearActiveAccSaberScoreRequest(UnityWebRequest request) {
            _activeAccSaberScoreRequests.Remove(request);
        }

        private readonly struct AccSaberScoreRequest {
            public readonly Score Score;
            public readonly UnityWebRequest Request;

            public AccSaberScoreRequest(Score score, UnityWebRequest request) {
                Score = score;
                Request = request;
            }
        }

        #endregion
    }
}
