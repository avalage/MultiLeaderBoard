using System;
using BeatLeader.UI;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using JetBrains.Annotations;
using TMPro;

namespace BeatLeader.Components {
    internal class RankedStarsModeButton : ReeUIComponentV2 {
        [UIComponent("background"), UsedImplicitly]
        private ImageView _background = null!;

        [UIComponent("mode-text"), UsedImplicitly]
        private TextMeshProUGUI _modeText = null!;

        public Action? OnClick { get; set; }

        private string _text = string.Empty;

        public string Text {
            get => _text;
            set {
                if (_text == value) return;
                _text = value;
                if (_modeText != null) {
                    _modeText.text = value;
                }
            }
        }

        protected override void OnInitialize() {
            _background.raycastTarget = true;
            BeatLeaderDarkGoldTheme.ApplyPanel(_background, BeatLeaderDarkGoldTheme.PanelTransparent);

            _modeText.richText = true;
            _modeText.alignment = TextAlignmentOptions.Center;
            _modeText.fontSize = 3.5f;
            _modeText.enableAutoSizing = true;
            _modeText.fontSizeMin = 2.7f;
            _modeText.fontSizeMax = 3.5f;
            _modeText.text = _text;
        }

        [UIAction("mode-button-on-click"), UsedImplicitly]
        private void ModeButtonOnClick() {
            OnClick?.Invoke();
        }
    }
}
