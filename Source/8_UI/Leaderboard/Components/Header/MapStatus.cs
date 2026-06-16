using BeatLeader.Models;
using BeatLeader.UI;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using JetBrains.Annotations;
using TMPro;

namespace BeatLeader.Components {
    internal class MapStatus : ReeUIComponentV2 {
        #region Components

        [UIComponent("background"), UsedImplicitly]
        private ImageView _background = null!;

        [UIComponent("status-text"), UsedImplicitly]
        private TextMeshProUGUI _statusText = null!;

        #endregion

        #region Init/Dispose

        protected override void OnInitialize() {
            _background.raycastTarget = true;
            SmoothHoverController.Custom(_background.gameObject, OnHoverStateChanged);
            GameplayModifiersPanelPatch.ModifiersChangedEvent += OnModifiersChanged;
            BeatLeaderDarkGoldTheme.ApplyPanel(_background, BeatLeaderDarkGoldTheme.PanelTransparent);
            _statusText.color = BeatLeaderDarkGoldTheme.PrimaryTextColor;
        }

        protected override void OnDispose() {
            GameplayModifiersPanelPatch.ModifiersChangedEvent -= OnModifiersChanged;
        }

        #endregion

        #region SetValues

        private GameplayModifiers? _gameplayModifiers;
        private RankedStatus _rankedStatus;
        private DiffInfo _diffInfo;
        private ScoreSaberLeaderboardInfo _scoreSaberLeaderboardInfo = ScoreSaberLeaderboardInfo.Unknown;
        private AccSaberDifficultyInfo _accSaberDifficultyInfo = AccSaberDifficultyInfo.Unknown;
        private RankedStarsDisplayMode _displayMode = RankedStarsDisplayMode.Both;

        public void SetActive(bool value) {
            _background.gameObject.SetActive(value);
            OnHoverStateChanged(false, 0.0f);
        }

        public void SetValues(
            RankedStatus rankedStatus,
            DiffInfo diffInfo,
            ScoreSaberLeaderboardInfo scoreSaberLeaderboardInfo,
            AccSaberDifficultyInfo accSaberDifficultyInfo,
            RankedStarsDisplayMode displayMode
        ) {
            _rankedStatus = rankedStatus;
            _diffInfo = diffInfo;
            _scoreSaberLeaderboardInfo = scoreSaberLeaderboardInfo;
            _accSaberDifficultyInfo = accSaberDifficultyInfo;
            _displayMode = displayMode;
            UpdateVisuals();
        }

        private void UpdateVisuals() {
            MapDifficultyPanel.NotifyDiffInfoChanged(_diffInfo);
            _statusText.text = _displayMode switch {
                RankedStarsDisplayMode.ScoreSaberOnly => ScoreSaberOnlyText(),
                RankedStarsDisplayMode.BeatLeaderOnly => BeatLeaderOnlyText(),
                RankedStarsDisplayMode.AccSaberOnly => AccSaberOnlyText(),
                _ => BothText()
            };
        }

        private string BeatLeaderOnlyText() {
            return BeatLeaderText(true);
        }

        private string ScoreSaberOnlyText() {
            if (_scoreSaberLeaderboardInfo.IsRanked) {
                return ScoreSaberText(true);
            }

            if (_rankedStatus is not RankedStatus.Ranked) {
                return BeatLeaderFallbackText();
            }

            return _scoreSaberLeaderboardInfo.IsKnown ? ScoreSaberText(true) : BeatLeaderFallbackText();
        }

        private string AccSaberOnlyText() {
            return _accSaberDifficultyInfo.IsKnown ? AccSaberText(true) : BeatLeaderFallbackText();
        }

        private string BothText() {
            var beatLeaderRanked = _rankedStatus is RankedStatus.Ranked;
            var scoreSaberRanked = _scoreSaberLeaderboardInfo.IsRanked;
            if (!beatLeaderRanked && !scoreSaberRanked) {
                return BeatLeaderFallbackText();
            }

            if (beatLeaderRanked && scoreSaberRanked) {
                return $"{BeatLeaderText(false)} <color={BeatLeaderDarkGoldTheme.TextMutedHtml}>/</color> {ScoreSaberText(false)}";
            }

            return beatLeaderRanked ? BeatLeaderText(true) : ScoreSaberText(true);
        }

        private string BeatLeaderFallbackText() {
            return _rankedStatus is RankedStatus.Unknown ? string.Empty : BeatLeaderText(true);
        }

        private string BeatLeaderText(bool includeStatus) {
            var stars = _diffInfo.stars;
            var modifiersApplied = false;
            if (_diffInfo.modifiersRating is { } rating &&
                _gameplayModifiers is { songSpeed: not GameplayModifiers.SongSpeed.Normal } modifiers) {
                stars = modifiers.songSpeed switch {
                    GameplayModifiers.SongSpeed.Slower => rating.ssStars,
                    GameplayModifiers.SongSpeed.Faster => rating.fsStars,
                    GameplayModifiers.SongSpeed.SuperFast => rating.sfStars,
                    _ => stars,
                };
                modifiersApplied = true;
            }

            var text = includeStatus ? _rankedStatus.ToString() : "BL";
            var modifiersIndicator = modifiersApplied ? $"<color={BeatLeaderDarkGoldTheme.SuccessGreenHtml}>[M]</color>" : string.Empty;
            if (_diffInfo.stars > 0) text += $": {FormatBeatLeaderStars(stars)} {modifiersIndicator}";
            return $"<color={BeatLeaderDarkGoldTheme.BeatLeaderPpHtml}>{text}</color>";
        }

        private static string FormatBeatLeaderStars(float value) {
            return $"{value:f2}<size=70%>\u2605</size>";
        }

        private string ScoreSaberText(bool includeStatus) {
            var status = _scoreSaberLeaderboardInfo.IsRanked
                ? "Ranked"
                : _scoreSaberLeaderboardInfo.IsQualified
                    ? "Qualified"
                    : _scoreSaberLeaderboardInfo.IsLoved
                        ? "Loved"
                        : "Unranked";

            var text = includeStatus ? status : "SS";
            if (_scoreSaberLeaderboardInfo.Stars > 0.0) {
                text += $": {_scoreSaberLeaderboardInfo.Stars:F2}<size=70%>★</size>";
            }

            return $"<color={BeatLeaderDarkGoldTheme.ScoreSaberPpHtml}>{text}</color>";
        }

        private string AccSaberText(bool includeStatus) {
            var text = "Acc";
            if (_accSaberDifficultyInfo.Complexity > 0.0f) {
                text += $": {_accSaberDifficultyInfo.Complexity:F2}<size=70%>\u2605</size>";
            }

            return $"<color={BeatLeaderDarkGoldTheme.AccSaberApHtml}>{text}</color>";
        }

        #endregion

        #region Callbacks

        private void OnHoverStateChanged(bool isHovered, float progress) {
            MapDifficultyPanel.NotifyMapStatusHoverStateChanged(_background.transform.position, isHovered, progress);
        }

        private void OnModifiersChanged(GameplayModifiers modifiers) {
            _gameplayModifiers = modifiers;
            UpdateVisuals();
        }

        #endregion
    }
}
