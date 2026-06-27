using System;
using System.Threading.Tasks;
using BeatLeader.APIV2;
using BeatLeader.Core.Managers.ReplayEnhancer;
using BeatLeader.Manager;
using BeatLeader.Models;
using BeatLeader.WebRequests;
using JetBrains.Annotations;
using LeaderboardCore.Interfaces;
using UnityEngine;
using Zenject;
using UploadReplayRequest = BeatLeader.APIV2.UploadReplayRequest;

namespace BeatLeader.DataManager {
    internal class LeaderboardManager : MonoBehaviour, INotifyLeaderboardSet {
        #region Properties

        [Inject, UsedImplicitly]
        private BeatmapLevelsModel _beatmapLevelsModel;

        private ScoresScope _selectedScoreScope;
        private int _selectedScoreContext;
        private int _lastSelectedPage = 1;
        private BeatmapKey _lastSelectedBeatmap;
        private string? _lastUserRequestScoresRefreshKey;
        private float _lastUserRequestScoresRefreshTime;
        private string? _lastUploadScoresRefreshKey;
        private float _lastUploadScoresRefreshTime;
        private int _refreshRequestCounter;
        private string Scope => _selectedScoreScope.ToString().ToLowerInvariant();
        private string Context => ScoresContexts.ContextForId(_selectedScoreContext).Key;
        private const float UserRequestScoresRefreshCooldownSeconds = 45.0f;
        private const float UploadScoresRefreshCooldownSeconds = 12.0f;

        #endregion

        #region Initialize/Dispose section

        public void Start() {
            SetFakeBloomProperty();

            ScoresRequest.StateChangedEvent += OnScoresRequestStateChanged;
            ClanScoresRequest.StateChangedEvent += OnScoresRequestStateChanged;

            UploadReplayRequest.StateChangedEvent += OnUploadRequestStateChanged;
            UserRequest.StateChangedEvent += OnUserRequestStateChanged;

            PluginConfig.ScoresContextChangedEvent += OnScoresContextWasChanged;
            LeaderboardState.ScoresScopeChangedEvent += OnScoresScopeWasSelected;
            LeaderboardState.IsVisibleChangedEvent += OnIsVisibleChanged;
            LeaderboardEvents.UpButtonWasPressedAction += OnPreviousPageClick;
            LeaderboardEvents.AroundButtonWasPressedAction += OnAroundMeClick;
            LeaderboardEvents.DownButtonWasPressedAction += OnNextPageClick;
            LeaderboardEvents.CaptorClanWasClickedEvent += OnCaptorClanClick;
            ProfileManager.FriendsUpdatedEvent += OnFriendsUpdated;
            LeaderboardsCache.CacheWasChangedEvent += OnCacheUpdated;

            _selectedScoreContext = PluginConfig.ScoresContext;
            _selectedScoreScope = LeaderboardState.ScoresScope;
        }

        private void OnDestroy() {
            ScoresRequest.StateChangedEvent -= OnScoresRequestStateChanged;
            ClanScoresRequest.StateChangedEvent -= OnScoresRequestStateChanged;

            UploadReplayRequest.StateChangedEvent -= OnUploadRequestStateChanged;
            UserRequest.StateChangedEvent -= OnUserRequestStateChanged;

            PluginConfig.ScoresContextChangedEvent -= OnScoresContextWasChanged;
            LeaderboardState.ScoresScopeChangedEvent -= OnScoresScopeWasSelected;
            LeaderboardState.IsVisibleChangedEvent -= OnIsVisibleChanged;
            LeaderboardEvents.UpButtonWasPressedAction -= OnPreviousPageClick;
            LeaderboardEvents.AroundButtonWasPressedAction -= OnAroundMeClick;
            LeaderboardEvents.DownButtonWasPressedAction -= OnNextPageClick;
            LeaderboardEvents.CaptorClanWasClickedEvent -= OnCaptorClanClick;
            ProfileManager.FriendsUpdatedEvent -= OnFriendsUpdated;
            LeaderboardsCache.CacheWasChangedEvent -= OnCacheUpdated;
        }

        #endregion

        #region SetGlobalBloomShaderParameter

        private static readonly int FakeBloomAmountPropertyID = Shader.PropertyToID("_FakeBloomAmount");

        private static void SetFakeBloomProperty() {
            bool enableFakeBloom;

            try {
                var mainSystemInit = Resources.FindObjectsOfTypeAll<MainSystemInit>()[0];
                enableFakeBloom = mainSystemInit._settingsManager.settings.quality.mainEffect == BeatSaber.Settings.QualitySettings.MainEffectOption.Off;
            } catch (Exception) {
                enableFakeBloom = false;
            }

            Shader.SetGlobalFloat(FakeBloomAmountPropertyID, enableFakeBloom ? 1.0f : 0.0f);
        }

        #endregion

        #region Scores Update

        private bool _updateRequired;

        private void TryUpdateScores(string reason) {
            _updateRequired = true;
            var requestId = ++_refreshRequestCounter;
            Plugin.Log.Info($"[LeaderboardRefresh] Requested #{requestId}; reason={reason}; visible={LeaderboardState.IsVisible}; {BuildRefreshDebugString()}");

            if (!LeaderboardState.IsVisible) {
                Plugin.Log.Info($"[LeaderboardRefresh] Deferred #{requestId}; leaderboard is hidden.");
                return;
            }

            UpdateScores(reason, requestId);
        }

        private void UpdateScores(string reason, int requestId) {
            _updateRequired = false;
            Plugin.Log.Info($"[LeaderboardRefresh] Executing #{requestId}; reason={reason}; {BuildRefreshDebugString()}");

            switch (LeaderboardState.leaderboardType) {
                case LeaderboardType.SongDiffPlayerScores: {
                    LoadPlayerScores();
                    break;
                }
                case LeaderboardType.SongDiffClanScores: {
                    LoadClanScores();
                    break;
                }
                default: break;
            }
        }

        private async void LoadPlayerScores() {
            var beatmap = _lastSelectedBeatmap;
            var context = Context;
            var scope = Scope;
            var page = _lastSelectedPage;
            var userId = await GetScoresUserId();
            if (string.IsNullOrEmpty(userId)) {
                Plugin.Log.Info($"[LeaderboardRefresh] Player scores request skipped; user id is empty; {BuildRefreshDebugString()}");
                return;
            }

            if (!beatmap.Equals(_lastSelectedBeatmap)) {
                Plugin.Log.Info($"[LeaderboardRefresh] Player scores request skipped; selected beatmap changed before send; {BuildRefreshDebugString()}");
                return;
            }

            Plugin.Log.Info($"[LeaderboardRefresh] Sending player scores request; user={userId}; context={context}; scope={scope}; page={page}; {BuildRefreshDebugString()}");
            ScoresRequest.SendPage(beatmap, userId, context, scope, page);
        }

        private async void SeekPlayerScores() {
            var beatmap = _lastSelectedBeatmap;
            var context = Context;
            var scope = Scope;
            var userId = await GetScoresUserId();
            if (string.IsNullOrEmpty(userId) || !beatmap.Equals(_lastSelectedBeatmap)) return;
            Plugin.Log.Info($"[LeaderboardRefresh] Sending around-me scores request; user={userId}; context={context}; scope={scope}; {BuildRefreshDebugString()}");
            ScoresRequest.SendSeek(beatmap, userId, context, scope);
        }

        private void LoadClanScores() {
            Plugin.Log.Info($"[LeaderboardRefresh] Sending clan scores request; {BuildRefreshDebugString()}");
            ClanScoresRequest.Send(_lastSelectedBeatmap, _lastSelectedPage);
        }

        private static async Task<string?> GetScoresUserId() {
            if (ProfileManager.TryGetUserId(out var profileUserId) && !string.IsNullOrEmpty(profileUserId)) {
                return profileUserId;
            }

            try {
                var userInfo = await UserEnhancer.GetUserAsync();
                if (!string.IsNullOrEmpty(userInfo?.platformUserId)) {
                    Plugin.Log.Debug($"Using platform user id as leaderboard fallback: {userInfo.platformUserId}");
                    return userInfo.platformUserId;
                }
            } catch (Exception ex) {
                Plugin.Log.Debug($"Could not resolve fallback platform user id for leaderboard scores: {ex.Message}");
            }

            return null;
        }

        #endregion

        #region Request Events

        private LeaderboardKey _uploadLeaderboardKey;

        private void OnUploadRequestStateChanged(IWebRequest<ScoreUploadResponse> instance, WebRequests.RequestState state, string? failReason) {
            if (!_lastSelectedBeatmap.IsValid()) return;

            var selectedKey = LeaderboardKey.FromBeatmap(_lastSelectedBeatmap);
            var status = instance.Result?.Status.ToString() ?? "null";
            Plugin.Log.Info($"[LeaderboardRefresh] Upload state={state}; status={status}; failReason={failReason ?? "none"}; selected={BuildLeaderboardKeyDebugString(selectedKey)}; upload={BuildLeaderboardKeyDebugString(_uploadLeaderboardKey)}");

            switch (state) {
                case WebRequests.RequestState.Started:
                    _uploadLeaderboardKey = selectedKey;
                    break;
                case WebRequests.RequestState.Finished:
                    if (!_uploadLeaderboardKey.Equals(selectedKey)) {
                        Plugin.Log.Info("[LeaderboardRefresh] Upload-finished refresh skipped; selected beatmap is different from upload beatmap.");
                        return;
                    }

                    var refreshKey = $"{BuildRefreshDebugString()}|uploadStatus={status}";
                    if (_lastUploadScoresRefreshKey == refreshKey &&
                        Time.realtimeSinceStartup - _lastUploadScoresRefreshTime < UploadScoresRefreshCooldownSeconds) {
                        Plugin.Log.Info($"[LeaderboardRefresh] Upload-finished refresh skipped by cooldown; key={refreshKey}");
                        return;
                    }

                    _lastUploadScoresRefreshKey = refreshKey;
                    _lastUploadScoresRefreshTime = Time.realtimeSinceStartup;
                    TryUpdateScores($"upload finished status={status}");
                    break;
            }
        }

        private void OnScoresRequestStateChanged(IWebRequest<ScoresTableContent> instance, WebRequests.RequestState state, string? failReason) {
            Plugin.Log.Info($"[LeaderboardRefresh] ScoresRequest state={state}; failReason={failReason ?? "none"}; resultPage={instance.Result?.CurrentPage.ToString() ?? "null"}; resultRows={instance.Result?.MainRowContents.Count.ToString() ?? "null"}; {BuildRefreshDebugString()}");
            if (state is not WebRequests.RequestState.Finished || LeaderboardState.leaderboardType is not LeaderboardType.SongDiffPlayerScores) return;
            _lastSelectedPage = instance.Result?.CurrentPage ?? 1;
        }

        private void OnUserRequestStateChanged(IWebRequest<Player> instance, WebRequests.RequestState state, string? failReason) {
            if (state is not WebRequests.RequestState.Finished) return;
            if (!_lastSelectedBeatmap.IsValid()) return;

            var refreshKey = BuildUserRequestScoresRefreshKey();
            if (_lastUserRequestScoresRefreshKey == refreshKey &&
                Time.realtimeSinceStartup - _lastUserRequestScoresRefreshTime < UserRequestScoresRefreshCooldownSeconds) {
                Plugin.Log.Info($"[LeaderboardRefresh] Skipping duplicate profile-update refresh: {refreshKey}");
                return;
            }

            _lastUserRequestScoresRefreshKey = refreshKey;
            _lastUserRequestScoresRefreshTime = Time.realtimeSinceStartup;
            TryUpdateScores("profile/user request finished");
        }

        private string BuildUserRequestScoresRefreshKey() {
            var leaderboardKey = LeaderboardKey.FromBeatmap(_lastSelectedBeatmap);
            return $"{LeaderboardState.leaderboardType}|{leaderboardKey.Hash}|{leaderboardKey.Diff}|{leaderboardKey.Mode}|{Context}|{Scope}|{_lastSelectedPage}";
        }

        private void OnFriendsUpdated() {
            if (_selectedScoreScope is not ScoresScope.Friends) return;
            TryUpdateScores("friends updated while friends scope is selected");
        }

        private void OnCacheUpdated() {
            if (LeaderboardState.leaderboardType is not LeaderboardType.SongDiffClanScores) return;
            if (!LeaderboardsCache.TryGetLeaderboardInfo(LeaderboardState.SelectedLeaderboardKey, out var cacheEntry)) return;
            if (FormatUtils.GetRankedStatus(cacheEntry.DifficultyInfo) is RankedStatus.Ranked) return;
            LeaderboardState.leaderboardType = LeaderboardType.SongDiffPlayerScores;
            TryUpdateScores("leaderboards cache updated and clan leaderboard is not ranked");
        }

        #endregion

        #region UI Events

        private void OnIsVisibleChanged(bool isVisible) {
            if (!isVisible || !_updateRequired) return;
            UpdateScores("leaderboard became visible with pending update", ++_refreshRequestCounter);
        }

        public void OnLeaderboardSet(BeatmapKey beatmapKey) {
            var level = _beatmapLevelsModel.GetBeatmapLevel(beatmapKey.levelId);
            if (level == null) return;

            Plugin.Log.Debug($"OnLeaderboardSet: {beatmapKey.levelId}, diff: {beatmapKey.difficulty}");
            _lastSelectedBeatmap = beatmapKey;
            _lastSelectedPage = 1;

            TryUpdateScores("leaderboard set");

            LeaderboardState.SelectedBeatmapLevel = level;
            LeaderboardState.SelectedBeatmapKey = beatmapKey;
        }

        private void OnScoresScopeWasSelected(ScoresScope scope) {
            Plugin.Log.Debug($"Attempt to switch score scope from [{_selectedScoreScope}] to [{scope}]");

            if (_selectedScoreScope != scope) {
                LeaderboardState.leaderboardType = LeaderboardType.SongDiffPlayerScores;
                _selectedScoreScope = scope;
                _lastSelectedPage = 1;

                TryUpdateScores($"score scope changed to {scope}");
            }
        }

        private void OnScoresContextWasChanged(int context) {
            Plugin.Log.Debug($"Attempt to switch score context from [{_selectedScoreContext}] to [{context}]");

            if (_selectedScoreContext != context) {
                LeaderboardState.leaderboardType = LeaderboardType.SongDiffPlayerScores;
                _selectedScoreContext = context;
                _lastSelectedPage = 1;

                TryUpdateScores($"score context changed to {context}");
            }
        }

        private void OnPreviousPageClick() {
            if (_lastSelectedPage <= 1) {
                _lastSelectedPage = 1;
                return;
            }

            _lastSelectedPage--;
            TryUpdateScores("previous page pressed");
        }

        private void OnNextPageClick() {
            _lastSelectedPage++;
            TryUpdateScores("next page pressed");
        }

        private void OnAroundMeClick() {
            SeekPlayerScores();
        }

        private void OnCaptorClanClick() {
            LeaderboardState.leaderboardType = LeaderboardState.leaderboardType switch {
                LeaderboardType.SongDiffPlayerScores => LeaderboardType.SongDiffClanScores,
                _ => LeaderboardType.SongDiffPlayerScores
            };
            _lastSelectedPage = 1;
            TryUpdateScores("captor clan toggle pressed");
        }

        private string BuildRefreshDebugString() {
            if (!_lastSelectedBeatmap.IsValid()) {
                return $"type={LeaderboardState.leaderboardType}; beatmap=invalid; context={Context}; scope={Scope}; page={_lastSelectedPage}";
            }

            return $"type={LeaderboardState.leaderboardType}; selected={BuildLeaderboardKeyDebugString(LeaderboardKey.FromBeatmap(_lastSelectedBeatmap))}; context={Context}; scope={Scope}; page={_lastSelectedPage}";
        }

        private static string BuildLeaderboardKeyDebugString(LeaderboardKey leaderboardKey) {
            return $"hash={leaderboardKey.Hash}, diff={leaderboardKey.Diff}, mode={leaderboardKey.Mode}";
        }

        #endregion
    }
}
