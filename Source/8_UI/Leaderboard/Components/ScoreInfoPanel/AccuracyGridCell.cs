using BeatLeader.UI;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

namespace BeatLeader.Components {
    internal class AccuracyGridCell : ReeUIComponentV2 {
        #region Events

        protected override void OnInitialize() {
            InitializeBackground();
        }

        private void OnHoverStateChanged(bool isHovered, float progress) {
            UpdateVisuals();
        }

        #endregion

        #region SetScore

        private float _score;
        private float _quality;

        public void SetScore(float score, float quality) {
            _score = score;
            _quality = quality;
            UpdateVisuals();
        }

        private void UpdateVisuals() {
            if (_score <= 0) {
                _backgroundImage.color = EmptyColor;
                _textComponent.text = "";
                return;
            }

            _backgroundImage.color = GetColor(_quality, _hoverController.Progress);
            _textComponent.text = FormatScore(_score, _hoverController.IsHovered);
        }

        #endregion

        #region Formatting & Color

        private static Color GoodColor => BeatLeaderDarkGoldTheme.AccuracyGridGoodColor;
        private static Color BadColor => BeatLeaderDarkGoldTheme.AccuracyGridBadColor;

        private static Color HoverColor => BeatLeaderDarkGoldTheme.AccuracyGridHoverColor;
        private static Color EmptyColor => BeatLeaderDarkGoldTheme.AccuracyGridEmptyColor;

        private static Color GetColor(float quality, float hover) {
            var t = quality * quality;
            var col = Color.Lerp(BadColor, GoodColor, t);
            col = Color.Lerp(col, HoverColor, hover);
            return col;
        }

        private static string FormatScore(float value, bool showAcc) {
            if (!showAcc) return $"{value:F1}";
            var acc = value / 1.15f;
            return $"<line-height=53%>{value:F1}\n<size=80%>{acc:F1}<size=50%>%";
        }

        #endregion

        #region Text

        [UIComponent("text-component"), UsedImplicitly]
        private TextMeshProUGUI _textComponent;

        #endregion

        #region Background

        [UIComponent("background"), UsedImplicitly]
        private ImageView _backgroundImage;

        private SmoothHoverController _hoverController;

        private void InitializeBackground() {
            _backgroundImage.material = BundleLoader.AccGridBackgroundMaterial;
            _backgroundImage.raycastTarget = true;
            _hoverController = SmoothHoverController.Custom(_backgroundImage.gameObject, OnHoverStateChanged);
        }

        #endregion
    }
}
