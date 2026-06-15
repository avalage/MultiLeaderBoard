using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BeatLeader.APIV2;
using BeatLeader.DataManager;
using BeatLeader.Manager;
using BeatLeader.Models;
using BeatLeader.UI;
using BeatLeader.UI.Hub;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BeatLeader.Components {
    internal class PlayerInfo : ReeUIComponentV2 {
        #region Components

        [UIValue("avatar"), UsedImplicitly]
        private PlayerAvatar _avatar;

        [UIValue("country-flag"), UsedImplicitly]
        private CountryFlag _countryFlag;

        private Player player;
        private const string ScoreSaberPlayerApiBaseUrl = "https://scoresaber.com/api/player";
        private static readonly TimeSpan ScoreSaberProfileCacheTtl = TimeSpan.FromMinutes(10);
        private static readonly Dictionary<string, ScoreSaberProfileCacheEntry> ScoreSaberProfileCache = new();
        private Coroutine? _scoreSaberProfileCoroutine;
        private int _scoreSaberProfileRequestId;
        private UnityWebRequest? _activeScoreSaberProfileRequest;

        private void Awake() {
            _avatar = Instantiate<PlayerAvatar>(transform);
            _countryFlag = Instantiate<CountryFlag>(transform);
        }

        #endregion

        #region Initialize/Dispose

        protected override void OnInitialize() {
            InitializeMaterial();
            InitializeExperienceBar();

            UserRequest.StateChangedEvent += OnProfileRequestStateChanged;
            UploadReplayRequest.StateChangedEvent += OnUploadRequestStateChanged;
            PrestigePanel.PrestigeWasPressedEvent += IncrementPrestigeIcon;
            GlobalSettingsView.ExperienceBarConfigEvent += OnExperienceBarConfigChanged;
            PluginConfig.ScoresContextChangedEvent += ChangeScoreContext;
            PrestigeLevelsManager.IconsLoadedEvent += OnPrestigeIconsLoaded;
        }

        protected override void OnDispose() {
            CancelScoreSaberProfileRequest();

            UserRequest.StateChangedEvent -= OnProfileRequestStateChanged;
            UploadReplayRequest.StateChangedEvent -= OnUploadRequestStateChanged;
            PrestigePanel.PrestigeWasPressedEvent -= IncrementPrestigeIcon;
            GlobalSettingsView.ExperienceBarConfigEvent -= OnExperienceBarConfigChanged;
            PluginConfig.ScoresContextChangedEvent -= ChangeScoreContext;
            PrestigeLevelsManager.IconsLoadedEvent -= OnPrestigeIconsLoaded;
        }

        #endregion

        #region Events

        private void OnUploadRequestStateChanged(WebRequests.IWebRequest<ScoreUploadResponse> instance, WebRequests.RequestState state, string? failReason) {
            if (state is not WebRequests.RequestState.Finished || instance.Result?.Score?.Player == null) return;

            var updatedPlayer = instance.Result.Score.Player;
            if (instance.Result.Status != ScoreUploadStatus.Error) {
                UpdateExperienceBar(updatedPlayer);
            }

            if (instance.Result.Status != ScoreUploadStatus.Uploaded) return;
            OnProfileUpdated(updatedPlayer);
            player.contextExtensions = updatedPlayer.contextExtensions;
        }

        private void OnProfileRequestStateChanged(WebRequests.IWebRequest<Player> instance, WebRequests.RequestState state, string? failReason) {
            switch (state) {
                case WebRequests.RequestState.Uninitialized:
                    OnProfileRequestFailed("Error");
                    break;
                case WebRequests.RequestState.Failed:
                    OnProfileRequestFailed(failReason);
                    break;
                case WebRequests.RequestState.Started:
                    OnProfileRequestStarted();
                    break;
                case WebRequests.RequestState.Finished:
                    player = instance.Result;
                    OnProfileUpdated(player);
                    break;
                default: return;
            }
        }

        private void OnProfileRequestFailed(string reason) {
            NameText = reason;
            StatsActive = false;
            ScoreSaberStatsActive = false;
            ExperienceBarActive = false;
            CancelScoreSaberProfileRequest();
        }

        private void OnProfileRequestStarted() {
            NameText = "Loading...";
            StatsActive = false;
            ScoreSaberStatsActive = false;
            ExperienceBarActive = false;
            CancelScoreSaberProfileRequest();
        }

        private void OnProfileUpdated(Player player) {
            this.player = player;
            _countryFlag.SetCountry(player.country);
            _avatar.SetAvatar(player.avatar, player.profileSettings);

            var contextPlayer = player.ContextPlayer(PluginConfig.ScoresContext);
            SwapPrestigeIcon(player.prestige);
            NameText = FormatUtils.FormatUserName(player.name);
            GlobalRankText = FormatProfileRank(contextPlayer.rank, BeatLeaderDarkGoldTheme.BeatLeaderPpHtml);
            CountryRankText = FormatProfileRank(contextPlayer.countryRank, BeatLeaderDarkGoldTheme.BeatLeaderPpHtml);
            PpText = FormatProfilePp(contextPlayer.pp, BeatLeaderDarkGoldTheme.BeatLeaderPpHtml);
            StatsActive = true;
            UpdateExperienceBar(player);
            RequestScoreSaberProfile(player);
        }

        #endregion

        private void IncrementPrestigeIcon() {
            if (player == null) {
                return;
            }

            SwapPrestigeIcon(player.prestige + 1);
        }

        private void OnPrestigeIconsLoaded() {
            if (player != null) {
                SwapPrestigeIcon(player.prestige);
            }
        }

        private void SwapPrestigeIcon(int prestige) {
            _prestigeIcon.sprite = PrestigeIcon.GetBigPrestigeSprite(prestige);

            if (ConfigFileData.Instance.ExperienceBarEnabled) {
                _prestigeIcon.gameObject.SetActive(true);
            } else {
                _prestigeIcon.gameObject.SetActive(false);
            }
        }

        private void OnExperienceBarConfigChanged(bool enable) {
            if (enable) {
                _prestigeIcon.gameObject.SetActive(true);
                if (player != null) {
                    UpdateExperienceBar(player);
                }
            } else {
                _prestigeIcon.gameObject.SetActive(false);
                ExperienceBarActive = false;
            }
        }

        private void ChangeScoreContext(int context) {
            if (player != null) {
                OnProfileUpdated(player);
            }
        }

        #region ExperienceBar

        private static readonly BindingFlags ClickableImageReflectionFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly PropertyInfo? ClickableDefaultColorProperty = typeof(ClickableImage).GetProperty("DefaultColor", ClickableImageReflectionFlags);
        private static readonly PropertyInfo? ClickableHighlightColorProperty = typeof(ClickableImage).GetProperty("HighlightColor", ClickableImageReflectionFlags);
        private static readonly FieldInfo? ClickableDefaultColorField = typeof(ClickableImage).GetField("_defaultColor", ClickableImageReflectionFlags);
        private static readonly FieldInfo? ClickableHighlightColorField = typeof(ClickableImage).GetField("_highlightColor", ClickableImageReflectionFlags);

        [UIComponent("profile-experience-bar-root"), UsedImplicitly]
        private RectTransform _experienceBarRoot = null!;

        private ImageView? _experienceBarBackground;
        private ImageView? _experienceBar;
        private ImageView? _experienceBarSession;

        [UIComponent("profile-experience-click-target"), UsedImplicitly]
        private ClickableImage _experienceClickTarget = null!;

        [UIAction("on-experience-click"), UsedImplicitly]
        private void OnExperienceClick() {
            LeaderboardEvents.NotifyPrestigeWasPressed();
        }

        private void InitializeExperienceBar() {
            _experienceBarBackground = CreateExperienceLayer("ProfileExperienceBarBackground", BeatLeaderDarkGoldTheme.ExperienceBarBackgroundColor);
            _experienceBar = CreateExperienceLayer("ProfileExperienceBarProgress", BeatLeaderDarkGoldTheme.ExperienceBarProgressColor);
            _experienceBarSession = CreateExperienceLayer("ProfileExperienceBarSession", BeatLeaderDarkGoldTheme.ExperienceBarSessionColor);

            _experienceClickTarget.material = GameResources.UINoGlowMaterial;
            _experienceClickTarget.color = Color.clear;
            SetClickableColor(_experienceClickTarget, Color.clear, Color.clear);
            _experienceClickTarget.rectTransform.SetAsLastSibling();

            SetHorizontalFill(_experienceBar.rectTransform, 0.0f, 0.0f);
            SetHorizontalFill(_experienceBarSession.rectTransform, 0.0f, 0.0f);
            ExperienceBarActive = false;
        }

        private ImageView CreateExperienceLayer(string name, Color color) {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageView), typeof(LayoutElement));
            gameObject.layer = _experienceBarRoot.gameObject.layer;
            gameObject.transform.SetParent(_experienceBarRoot, false);
            gameObject.GetComponent<LayoutElement>().ignoreLayout = true;

            var image = gameObject.GetComponent<ImageView>();
            image.material = GameResources.UINoGlowMaterial;
            image.sprite = GameResources.Sprites.RoundRect;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            image.color = color;
            SetFullRect(image.rectTransform);
            return image;
        }

        private void UpdateExperienceBar(Player sourcePlayer) {
            if (!ConfigFileData.Instance.ExperienceBarEnabled) {
                ExperienceBarActive = false;
                return;
            }

            var level = sourcePlayer.level;
            var requiredExperience = CalculateRequiredExperience(sourcePlayer.level, sourcePlayer.prestige);
            var progress = level >= 100
                ? 1.0f
                : requiredExperience > 0
                    ? Mathf.Clamp01(sourcePlayer.experience / (float)requiredExperience)
                    : 0.0f;

            ExperienceLevelText = level.ToString(CultureInfo.InvariantCulture);
            if (level >= 100) {
                ExperienceNextLevelText = "Prestige";
                ExperienceHoverHint = "You can prestige now!";
            } else {
                ExperienceNextLevelText = (level + 1).ToString(CultureInfo.InvariantCulture);
                ExperienceHoverHint = $"{sourcePlayer.experience} | {requiredExperience} to level {level + 1}";
            }

            if (_experienceBar == null || _experienceBarSession == null) {
                ExperienceBarActive = false;
                return;
            }

            SetHorizontalFill(_experienceBar.rectTransform, 0.0f, progress);
            SetHorizontalFill(_experienceBarSession.rectTransform, progress, progress);
            ForceExperienceBarColors();
            ExperienceBarActive = true;
        }

        private static int CalculateRequiredExperience(int level, int prestige) {
            var requiredExperience = 500 + (50 * level);
            if (prestige != 0) {
                requiredExperience = (int)Mathf.Round(requiredExperience * Mathf.Pow(1.2f, prestige));
            }

            return requiredExperience;
        }

        private void ForceExperienceBarColors() {
            if (!BeatLeaderDarkGoldTheme.Enabled) {
                return;
            }

            if (_experienceBarBackground != null) {
                _experienceBarBackground.color = BeatLeaderDarkGoldTheme.ExperienceBarBackgroundColor;
            }

            if (_experienceBar != null) {
                _experienceBar.color = BeatLeaderDarkGoldTheme.ExperienceBarProgressColor;
            }

            if (_experienceBarSession != null) {
                _experienceBarSession.color = BeatLeaderDarkGoldTheme.ExperienceBarSessionColor;
            }

            _experienceClickTarget.color = Color.clear;
            SetClickableColor(_experienceClickTarget, Color.clear, Color.clear);
        }

        private static void SetFullRect(RectTransform rectTransform) {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        private static void SetHorizontalFill(RectTransform rectTransform, float from, float to) {
            rectTransform.anchorMin = new Vector2(Mathf.Clamp01(from), 0.0f);
            rectTransform.anchorMax = new Vector2(Mathf.Clamp01(to), 1.0f);
            rectTransform.pivot = new Vector2(0.0f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        private static void SetClickableColor(ClickableImage image, Color defaultColor, Color highlightColor) {
            ClickableDefaultColorProperty?.SetValue(image, defaultColor);
            ClickableHighlightColorProperty?.SetValue(image, highlightColor);
            ClickableDefaultColorField?.SetValue(image, defaultColor);
            ClickableHighlightColorField?.SetValue(image, highlightColor);
        }

        #endregion

        #region ScoreSaberProfile

        private void RequestScoreSaberProfile(Player player) {
            CancelScoreSaberProfileRequest();
            ScoreSaberStatsActive = false;

            if (!BeatLeaderDarkGoldTheme.Enabled || player == null || string.IsNullOrEmpty(player.id) || player.id == "0") {
                return;
            }

            if (TryGetCachedScoreSaberProfile(player.id, out var cachedProfile)) {
                ApplyScoreSaberProfile(cachedProfile);
            }

            _scoreSaberProfileCoroutine = StartCoroutine(LoadScoreSaberProfileCoroutine(_scoreSaberProfileRequestId, player.id));
        }

        private void CancelScoreSaberProfileRequest() {
            _scoreSaberProfileRequestId++;
            if (_scoreSaberProfileCoroutine != null) {
                StopCoroutine(_scoreSaberProfileCoroutine);
                _scoreSaberProfileCoroutine = null;
            }

            if (_activeScoreSaberProfileRequest != null) {
                _activeScoreSaberProfileRequest.Abort();
                _activeScoreSaberProfileRequest.Dispose();
                _activeScoreSaberProfileRequest = null;
            }
        }

        private IEnumerator LoadScoreSaberProfileCoroutine(int requestId, string playerId) {
            var url = $"{ScoreSaberPlayerApiBaseUrl}/{UnityWebRequest.EscapeURL(playerId)}/full";
            using var request = UnityWebRequest.Get(url);
            _activeScoreSaberProfileRequest = request;
            request.timeout = 4;
            request.SetRequestHeader("User-Agent", Plugin.UserAgent);
            yield return request.SendWebRequest();
            ClearActiveScoreSaberProfileRequest(request);

            if (requestId != _scoreSaberProfileRequestId) {
                yield break;
            }

            _scoreSaberProfileCoroutine = null;
            if (request.isNetworkError || request.isHttpError) {
                Plugin.Log.Debug($"[ScoreSaberProfile] Failed to load profile for {playerId}: {request.responseCode} {request.error}");
                yield break;
            }

            if (!TryReadScoreSaberProfile(request.downloadHandler.text, out var profile)) {
                yield break;
            }

            ScoreSaberProfileCache[playerId] = new ScoreSaberProfileCacheEntry(
                profile,
                DateTime.UtcNow.Add(ScoreSaberProfileCacheTtl)
            );
            ApplyScoreSaberProfile(profile);
        }

        private void ApplyScoreSaberProfile(ScoreSaberProfile profile) {
            ScoreSaberGlobalRankText = FormatProfileRank(profile.Rank, BeatLeaderDarkGoldTheme.ScoreSaberPpHtml);
            ScoreSaberCountryRankText = FormatProfileRank(profile.CountryRank, BeatLeaderDarkGoldTheme.ScoreSaberPpHtml);
            ScoreSaberPpText = FormatProfilePp(profile.Pp, BeatLeaderDarkGoldTheme.ScoreSaberPpHtml);
            ScoreSaberStatsActive = true;
        }

        private void ClearActiveScoreSaberProfileRequest(UnityWebRequest request) {
            if (ReferenceEquals(_activeScoreSaberProfileRequest, request)) {
                _activeScoreSaberProfileRequest = null;
            }
        }

        private static bool TryGetCachedScoreSaberProfile(string playerId, out ScoreSaberProfile profile) {
            profile = default;
            if (!ScoreSaberProfileCache.TryGetValue(playerId, out var cacheEntry)) {
                return false;
            }

            if (DateTime.UtcNow >= cacheEntry.ExpiresAt) {
                ScoreSaberProfileCache.Remove(playerId);
                return false;
            }

            profile = cacheEntry.Profile;
            return true;
        }

        private static bool TryReadScoreSaberProfile(string json, out ScoreSaberProfile profile) {
            profile = default;

            try {
                var root = JObject.Parse(json);
                var profileToken = root["playerInfo"] ?? root["player"] ?? root;
                var rank = ReadInt(profileToken, "rank", "globalRank");
                var countryRank = ReadInt(profileToken, "countryRank", "country_rank");
                var pp = ReadFloat(profileToken, "pp", "rankedPP");

                if (rank == null && countryRank == null && pp == null) {
                    return false;
                }

                profile = new ScoreSaberProfile(rank ?? -1, countryRank ?? -1, pp ?? -1.0f);
                return true;
            } catch (Exception ex) {
                Plugin.Log.Debug($"[ScoreSaberProfile] Failed to parse profile: {ex.Message}");
                return false;
            }
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

        private readonly struct ScoreSaberProfileCacheEntry {
            public readonly ScoreSaberProfile Profile;
            public readonly DateTime ExpiresAt;

            public ScoreSaberProfileCacheEntry(ScoreSaberProfile profile, DateTime expiresAt) {
                Profile = profile;
                ExpiresAt = expiresAt;
            }
        }

        private readonly struct ScoreSaberProfile {
            public readonly int Rank;
            public readonly int CountryRank;
            public readonly float Pp;

            public ScoreSaberProfile(int rank, int countryRank, float pp) {
                Rank = rank;
                CountryRank = countryRank;
                Pp = pp;
            }
        }

        #endregion

        #region Formatters

        private static string FormatProfileRank(int rank, string color) {
            var value = rank <= 0 ? "?" : rank.ToString(CultureInfo.InvariantCulture);
            return $"<color={color}><size=70%>#</size>{value}</color>";
        }

        private static string FormatProfilePp(float pp, string color) {
            var value = pp < 0 ? "?" : pp.ToString("F2", CultureInfo.InvariantCulture);
            return $"<color={color}>{value}<size=70%>pp</size></color>";
        }

        #endregion

        #region StatsActive

        private bool _statsActive;

        [UIValue("stats-active"), UsedImplicitly]
        public bool StatsActive {
            get => _statsActive;
            set {
                if (_statsActive.Equals(value)) return;
                _statsActive = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region ScoreSaberStatsActive

        private bool _scoreSaberStatsActive;

        [UIValue("score-saber-stats-active"), UsedImplicitly]
        public bool ScoreSaberStatsActive {
            get => _scoreSaberStatsActive;
            set {
                if (_scoreSaberStatsActive.Equals(value)) return;
                _scoreSaberStatsActive = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region PrestigeIcon

        [UIComponent("prestige-icon"), UsedImplicitly]
        private ImageView _prestigeIcon = null!;

        #endregion

        #region NameText

        private string _nameText = "";

        [UIValue("name-text"), UsedImplicitly]
        public string NameText {
            get => _nameText;
            set {
                if (_nameText.Equals(value)) return;
                _nameText = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region GlobalRankText

        private string _globalRankText = "";

        [UIValue("global-rank-text"), UsedImplicitly]
        public string GlobalRankText {
            get => _globalRankText;
            set {
                if (_globalRankText.Equals(value)) return;
                _globalRankText = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region CountryRankText

        private string _countryRankText = "";

        [UIValue("country-rank-text"), UsedImplicitly]
        public string CountryRankText {
            get => _countryRankText;
            set {
                if (_countryRankText.Equals(value)) return;
                _countryRankText = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region PpText

        private string _ppText = "";

        [UIValue("pp-text"), UsedImplicitly]
        public string PpText {
            get => _ppText;
            set {
                if (_ppText.Equals(value)) return;
                _ppText = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region ScoreSaberGlobalRankText

        private string _scoreSaberGlobalRankText = "";

        [UIValue("score-saber-global-rank-text"), UsedImplicitly]
        public string ScoreSaberGlobalRankText {
            get => _scoreSaberGlobalRankText;
            set {
                if (_scoreSaberGlobalRankText.Equals(value)) return;
                _scoreSaberGlobalRankText = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region ScoreSaberCountryRankText

        private string _scoreSaberCountryRankText = "";

        [UIValue("score-saber-country-rank-text"), UsedImplicitly]
        public string ScoreSaberCountryRankText {
            get => _scoreSaberCountryRankText;
            set {
                if (_scoreSaberCountryRankText.Equals(value)) return;
                _scoreSaberCountryRankText = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region ScoreSaberPpText

        private string _scoreSaberPpText = "";

        [UIValue("score-saber-pp-text"), UsedImplicitly]
        public string ScoreSaberPpText {
            get => _scoreSaberPpText;
            set {
                if (_scoreSaberPpText.Equals(value)) return;
                _scoreSaberPpText = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region ExperienceBarValues

        private bool _experienceBarActive;

        [UIValue("experience-bar-active"), UsedImplicitly]
        public bool ExperienceBarActive {
            get => _experienceBarActive;
            set {
                if (_experienceBarActive.Equals(value)) return;
                _experienceBarActive = value;
                NotifyPropertyChanged();
            }
        }

        private string _experienceLevelText = "";

        [UIValue("experience-level-text"), UsedImplicitly]
        public string ExperienceLevelText {
            get => _experienceLevelText;
            set {
                if (_experienceLevelText.Equals(value)) return;
                _experienceLevelText = value;
                NotifyPropertyChanged();
            }
        }

        private string _experienceNextLevelText = "";

        [UIValue("experience-next-level-text"), UsedImplicitly]
        public string ExperienceNextLevelText {
            get => _experienceNextLevelText;
            set {
                if (_experienceNextLevelText.Equals(value)) return;
                _experienceNextLevelText = value;
                NotifyPropertyChanged();
            }
        }

        private string _experienceHoverHint = "";

        [UIValue("experience-hover-hint"), UsedImplicitly]
        public string ExperienceHoverHint {
            get => _experienceHoverHint;
            set {
                if (_experienceHoverHint.Equals(value)) return;
                _experienceHoverHint = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region Material

        private Material _materialInstance;

        private void InitializeMaterial() {
            _materialInstance = Material.Instantiate(BundleLoader.PrestigeIconMaterial);
            _prestigeIcon.material = _materialInstance;
            _prestigeIcon.sprite = BundleLoader.TransparentPixel;
            _prestigeIcon.gameObject.SetActive(false);
        }

        #endregion
    }
}
