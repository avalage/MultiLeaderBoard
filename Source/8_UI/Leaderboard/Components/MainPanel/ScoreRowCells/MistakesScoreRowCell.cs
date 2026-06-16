using BeatSaberMarkupLanguage.Attributes;
using BeatLeader.UI;
using JetBrains.Annotations;
using TMPro;

namespace BeatLeader.Components {
    internal class MistakesScoreRowCell : AbstractScoreRowCell {
        #region Implementation

        private static string GoodColor => BeatLeaderDarkGoldTheme.SuccessGreenHtml;
        private static string BadColor => BeatLeaderDarkGoldTheme.WarningOrangeHtml;

        public override void SetValue(object? value) {
            if (value == null) {
                textComponent.text = string.Empty;
            } else {
                var totalMistakes = (int)value;
                textComponent.text = totalMistakes == 0 ? $"<color={GoodColor}>FC" : $"<color={BadColor}>{totalMistakes}<size=70%>x";
            }

            isEmpty = false;
        }

        public override void SetAlpha(float value) {
            textComponent.alpha = value;
        }

        protected override float CalculatePreferredWidth() {
            return textComponent.preferredWidth;
        }

        #endregion

        #region Components

        [UIComponent("text-component"), UsedImplicitly]
        public TextMeshProUGUI textComponent;

        #endregion
    }
}
