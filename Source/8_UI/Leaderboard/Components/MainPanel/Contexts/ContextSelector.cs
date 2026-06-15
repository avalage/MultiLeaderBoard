using BeatLeader.Manager;
using BeatLeader.Models;
using BeatLeader.UI;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using JetBrains.Annotations;
using UnityEngine;

namespace BeatLeader.Components {
    internal class ContextSelector : ReeUIComponentV2 {
        #region Init / Dispose

        [UIComponent("main-button"), UsedImplicitly]
        private ClickableImage _mainButton;

        protected override void OnInitialize() {
            InitializeMainButton();
            PluginConfig.ScoresContextChangedEvent += ApplyContext;
            PluginConfig.ScoresContextListChangedEvent += ScoresContextListUpdated;
            ApplyContext(PluginConfig.ScoresContext);
        }

        protected override void OnDispose() {
            PluginConfig.ScoresContextChangedEvent -= ApplyContext;
            PluginConfig.ScoresContextListChangedEvent -= ScoresContextListUpdated;
        }

        #endregion

        #region MainButton

        private static Color SelectedColor => BeatLeaderDarkGoldTheme.SelectedIconColor;
        private static Color HoverColor => BeatLeaderDarkGoldTheme.FadedIconHoverColor;

        private void InitializeMainButton() {
            _mainButton.material = BundleLoader.UIAdditiveGlowMaterial;
            _mainButton.DefaultColor = SelectedColor;
            _mainButton.HighlightColor = HoverColor;
        }

        private void ApplyContext(int scoresContext) {
            _mainButton.sprite = ScoresContexts.ContextForId(scoresContext).Icon;
        }

        private void ScoresContextListUpdated() {
            ApplyContext(PluginConfig.ScoresContext);
        }

        [UIAction("main-button-on-click"), UsedImplicitly]
        private void MainButtonOnClick() {
            LeaderboardEvents.NotifyContextSelectorWasPressed();
        }

        #endregion
    }
}
