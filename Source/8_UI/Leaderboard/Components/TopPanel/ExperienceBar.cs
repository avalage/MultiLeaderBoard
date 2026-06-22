using BeatLeader.APIV2;
using System.Reflection;
using BeatLeader.DataManager;
using BeatLeader.Manager;
using BeatLeader.Models;
using BeatLeader.UI;
using BeatLeader.UI.BSML_Addons.Components;
using BeatLeader.UI.Hub;
using BeatLeader.WebRequests;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using UploadReplayRequest = BeatLeader.APIV2.UploadReplayRequest;

namespace BeatLeader.Components {
    internal class ExperienceBar : ReeUIComponentV2 {
        #region Properties

        private static readonly BindingFlags ClickableImageReflectionFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly PropertyInfo? ClickableDefaultColorProperty = typeof(ClickableImage).GetProperty("DefaultColor", ClickableImageReflectionFlags);
        private static readonly PropertyInfo? ClickableHighlightColorProperty = typeof(ClickableImage).GetProperty("HighlightColor", ClickableImageReflectionFlags);
        private static readonly FieldInfo? ClickableDefaultColorField = typeof(ClickableImage).GetField("_defaultColor", ClickableImageReflectionFlags);
        private static readonly FieldInfo? ClickableHighlightColorField = typeof(ClickableImage).GetField("_highlightColor", ClickableImageReflectionFlags);

        private int _level;
        private float _gradientT;
        private float _expProgress;
        private float _sessionProgress;
        private float _currentExperience;
        private float _requiredExp;

        private bool _initialized;
        private int _levelUpValue;
        private int _levelUpCount;
        private bool _isIdle;
        private bool _reverse;
        private bool _isAnimated;
        private float _elapsedTime;
        private float _elapsedTime2;
        private readonly float _animationDuration = 3f;
        private float _targetValue;

        private float _highlight;

        #endregion

        #region Animation

        private void Update() {
            ForceDarkGoldColors();

            if (_initialized && _level != 100 && (_isIdle || _isAnimated)) {
                _elapsedTime += Time.deltaTime;
                if (_isIdle) {
                    // Idle highlight animation, slowly pulses the highlight value
                    float t = Mathf.Clamp01(_elapsedTime / _animationDuration);
                    if (!_reverse) {
                        _highlight = Mathf.Lerp(0f, 1f, t);
                    } else {
                        _highlight = Mathf.Lerp(1f, 0f, t);
                    }

                    if (_elapsedTime >= _animationDuration) {
                        _elapsedTime = 0f;
                        _reverse = !_reverse;
                    }
                } else if (_isAnimated) {
                    // Experience filling the bar animation with wave effect
                    if (_levelUpValue > 0) { // Level up animation
                        _elapsedTime2 += Time.deltaTime;
                        float t = Mathf.Clamp01(_elapsedTime2 * (_levelUpValue + 1) / _animationDuration);
                        float targetValue = 1 - _expProgress;
                        if (_levelUpCount != 0) { // Before final level
                            _sessionProgress = Mathf.Lerp(0f, targetValue, t);
                        } else { // Final level
                            _sessionProgress = Mathf.Lerp(0f, _targetValue, t);
                        }
                        // Consider the number of level ups to speed up the animation
                        if (_elapsedTime2 * (_levelUpValue + 1) >= _animationDuration) {
                            if (_levelUpCount != 0) { // Reset for next level up
                                _levelUpCount--;
                                SetLevelText(++_level);
                                _expProgress = 0f;
                                _sessionProgress = 0f;
                                _elapsedTime2 = 0f;
                            }
                        }
                    } else { // Non-level up animation
                        _gradientT = Mathf.Clamp01(_elapsedTime / _animationDuration);
                        _sessionProgress = Mathf.Lerp(0, _targetValue, _gradientT);
                    }

                    // Forcefully end animation if time exceeded
                    if (_elapsedTime >= _animationDuration) {
                        _level += _levelUpCount; // Add leftover level ups if still existing
                        SetLevelText(_level);
                        _sessionProgress = _targetValue;
                        _isAnimated = false;
                    }
                }

                SetMaterialProperties();
            }
        }

        private void SetMaterialProperties() {
            if (_level == 100) {
                _expProgress = 1;
                _sessionProgress = 0f;
            }

            var baseProgress = Mathf.Clamp01(_expProgress);
            var totalProgress = Mathf.Clamp01(_expProgress + _sessionProgress);
            if (totalProgress < baseProgress) {
                baseProgress = totalProgress;
            }

            SetHorizontalFill(_experienceBar.Image.rectTransform, 0.0f, baseProgress);
            SetHorizontalFill(_experienceBarSession.Image.rectTransform, baseProgress, totalProgress);
            ForceDarkGoldColors();
        }

        #endregion

        #region Initialize/Dispose

        protected override void OnInitialize() {
            _initialized = false;
            SetMaterial();
            GlobalSettingsView.ExperienceBarConfigEvent += OnExperienceBarConfigChanged;
            UserRequest.StateChangedEvent += OnProfileRequestStateChanged;
            ProfileManager.ProfileUpdatedEvent += OnProfileUpdated;
            if (ProfileManager.HasProfile && ProfileManager.Profile != null) {
                OnProfileUpdated(ProfileManager.Profile);
            }
            if (ConfigFileData.Instance.ExperienceBarEnabled) {
                UploadReplayRequest.StateChangedEvent += OnUploadStateChanged;
                PrestigeRequest.StateChangedEvent += OnPrestigeRequestStateChanged;
            } else {
                LevelText = "";
                NextLevelText = "";
                HoverHint = "";
                _experienceBarRoot.gameObject.SetActive(false);
            }
        }

        protected override void OnDispose() {
            GlobalSettingsView.ExperienceBarConfigEvent -= OnExperienceBarConfigChanged;
            UserRequest.StateChangedEvent -= OnProfileRequestStateChanged;
            ProfileManager.ProfileUpdatedEvent -= OnProfileUpdated;
            UploadReplayRequest.StateChangedEvent -= OnUploadStateChanged;
            PrestigeRequest.StateChangedEvent -= OnPrestigeRequestStateChanged;
        }

        #endregion

        #region Events

        [UIAction("on-click"), UsedImplicitly]
        private void OnClick() {
            LeaderboardEvents.NotifyPrestigeWasPressed();
        }

        private void OnExperienceBarConfigChanged(bool enabled) {
            _experienceBarRoot.gameObject.SetActive(enabled);
            ResetExperienceBarData();
            if (enabled && !_initialized) {
                UploadReplayRequest.StateChangedEvent += OnUploadStateChanged;
                PrestigeRequest.StateChangedEvent += OnPrestigeRequestStateChanged;
                SetLevelText(_level);
            } else if (!enabled && _initialized) {
                UploadReplayRequest.StateChangedEvent -= OnUploadStateChanged;
                PrestigeRequest.StateChangedEvent -= OnPrestigeRequestStateChanged;
                LevelText = "";
                NextLevelText = "";
                HoverHint = "";
            }
            _initialized = enabled;
        }

        private void OnProfileRequestStateChanged(IWebRequest<Player> instance, RequestState state, string? failReason) {
            if (state is not RequestState.Finished) {
                return;
            }

            ApplyProfileExperience(instance.Result);
        }

        private void OnProfileUpdated(Player player) {
            ApplyProfileExperience(player);
        }

        private void ApplyProfileExperience(Player player) {
            if (_initialized && (_isIdle || _isAnimated)) {
                return;
            }

            _level = player.level;
            _currentExperience = player.experience;
            _requiredExp = CalculateRequiredExperience(player.level, player.prestige);
            _expProgress = _requiredExp > 0 ? player.experience / _requiredExp : 0.0f;
            ResetExperienceBarData();
            if (ConfigFileData.Instance.ExperienceBarEnabled) {
                SetLevelText(_level);
                _experienceBarRoot.gameObject.SetActive(ConfigFileData.Instance.ExperienceBarEnabled);
                _initialized = true;
            }
        }

        private int CalculateRequiredExperience(int level, int prestige) {
            int requiredExp = 500 + (50 * level);
            if (prestige != 0) {
                requiredExp = (int)Mathf.Round(requiredExp * Mathf.Pow(1.2f, prestige));
            }
            return requiredExp;
        }

        private void SetLevelText(int level) {
            LevelText = level.ToString();
            if (level != 100) {
                NextLevelText = (level + 1).ToString();
                HoverHint = $"{_currentExperience} | {_requiredExp} to level {level + 1}";
            } else {
                NextLevelText = "Prestige";
                HoverHint = "You can prestige now!";
            }
        }

        private void ResetExperienceBarData(bool refreshVisual = true) {
            _levelUpCount = 0;
            _levelUpValue = 0;
            _targetValue = 0f;
            _sessionProgress = 0f;
            _gradientT = 0f;
            _highlight = 0f;
            _elapsedTime = 0f;
            _elapsedTime2 = 0f;
            _isAnimated = false;
            _isIdle = false;
            if (refreshVisual) {
                SetMaterialProperties();
            }
        }

        private void OnUploadStateChanged(IWebRequest<ScoreUploadResponse> instance, RequestState state, string? failReason) {
            if (_level == 100) return;

            if (state is RequestState.Started) {
                if (_levelUpValue == 0) {
                    _expProgress += _targetValue;
                } else {
                    _expProgress = _targetValue;
                }
                ResetExperienceBarData();
                _isIdle = true;
            }

            if (state is RequestState.Finished) {
                ResetExperienceBarData();

                if (instance.Result.Status != ScoreUploadStatus.Error) {
                    Player player = instance.Result.Score.Player;
                    _currentExperience = player.experience;
                    if (player.level == _level) {
                        _targetValue = _currentExperience / _requiredExp - _expProgress;
                    } else {
                        _levelUpCount = player.level - _level;
                        _levelUpValue = _levelUpCount;
                        _requiredExp = CalculateRequiredExperience(player.level, player.prestige);
                        _targetValue = _currentExperience / _requiredExp;
                    }

                    _isAnimated = true;
                }
            }
        }

        private void OnPrestigeRequestStateChanged(IWebRequest<Player> instance, RequestState state, string? failReason) {
            if (state is RequestState.Finished) {
                _level = 0;
                SetLevelText(_level);
                _expProgress = 0f;
                ResetExperienceBarData();
            }
        }

        #endregion

        #region Image & Material

        [UIComponent("experience-bar-root"), UsedImplicitly]
        private Transform _experienceBarRoot;

        [UIComponent("experience-bar-background"), UsedImplicitly]
        private BetterImage _experienceBarBackground;

        [UIComponent("experience-bar"), UsedImplicitly]
        private BetterImage _experienceBar;

        [UIComponent("experience-bar-session"), UsedImplicitly]
        private BetterImage _experienceBarSession;

        [UIComponent("experience-click-target"), UsedImplicitly]
        private ClickableImage _experienceClickTarget;

        private void SetMaterial() {
            _experienceBarBackground.Image.material = GameResources.UINoGlowMaterial;
            _experienceBarBackground.Image.type = Image.Type.Simple;
            _experienceBarBackground.Image.color = BeatLeaderDarkGoldTheme.ExperienceBarBackgroundColor;
            SetFullRect(_experienceBarBackground.Image.rectTransform);

            _experienceBar.Image.material = GameResources.UINoGlowMaterial;
            _experienceBar.Image.type = Image.Type.Simple;
            SetHorizontalFill(_experienceBar.Image.rectTransform, 0.0f, 0.0f);

            _experienceBarSession.Image.material = GameResources.UINoGlowMaterial;
            _experienceBarSession.Image.type = Image.Type.Simple;
            SetHorizontalFill(_experienceBarSession.Image.rectTransform, 0.0f, 0.0f);

            _experienceClickTarget.material = GameResources.UINoGlowMaterial;
            _experienceClickTarget.color = Color.clear;
            SetFullRect(_experienceClickTarget.rectTransform);
            ForceDarkGoldColors();
        }

        private void ForceDarkGoldColors() {
            if (!BeatLeaderDarkGoldTheme.Enabled || _experienceBarBackground == null || _experienceBar == null || _experienceBarSession == null || _experienceClickTarget == null) return;

            _experienceBarBackground.Image.color = BeatLeaderDarkGoldTheme.ExperienceBarBackgroundColor;
            _experienceBar.Image.color = BeatLeaderDarkGoldTheme.ExperienceBarProgressColor;
            _experienceBarSession.Image.color = BeatLeaderDarkGoldTheme.ExperienceBarSessionColor;
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

        #region LevelText

        private string _levelText = "";

        [UIValue("level-text"), UsedImplicitly]
        public string LevelText {
            get => _levelText;
            set {
                if (_levelText.Equals(value)) return;
                _levelText = value;
                NotifyPropertyChanged();
            }
        }

        private string _nextLevelText = "";

        [UIValue("next-level-text"), UsedImplicitly]
        public string NextLevelText {
            get => _nextLevelText;
            set {
                if (_nextLevelText.Equals(value)) return;
                _nextLevelText = value;
                NotifyPropertyChanged();
            }
        }

        private string _hoverHint = "";

        [UIValue("hover-hint"), UsedImplicitly]
        public string HoverHint {
            get => _hoverHint;
            set {
                if (_hoverHint.Equals(value)) return;
                _hoverHint = value;
                NotifyPropertyChanged();
            }
        }

        #endregion
    }
}
