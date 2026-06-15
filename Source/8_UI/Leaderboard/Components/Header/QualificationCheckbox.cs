using System;
using BeatLeader.UI;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using JetBrains.Annotations;
using UnityEngine;

namespace BeatLeader.Components {
    internal class QualificationCheckbox : ReeUIComponentV2 {
        #region Initialize

        protected override void OnInitialize() {
            InitializeMaterial();
            SetState(State.Neutral);

            _imageComponent.raycastTarget = true;
        }

        #endregion

        #region State

        private static Color NeutralColor => BeatLeaderDarkGoldTheme.QualificationNeutralColor;
        private static Color FailedColor => BeatLeaderDarkGoldTheme.QualificationFailedColor;
        private static Color OnHoldColor => BeatLeaderDarkGoldTheme.QualificationOnHoldColor;
        private static Color CheckedColor => BeatLeaderDarkGoldTheme.QualificationCheckedColor;

        public void SetState(State state) {
            _imageComponent.color = state switch {
                State.Neutral => NeutralColor,
                State.OnHold => OnHoldColor,
                State.Failed => FailedColor,
                State.Checked => CheckedColor,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }

        public enum State {
            Neutral,
            OnHold,
            Failed,
            Checked
        }

        #endregion

        #region Image

        [UIComponent("image-component"), UsedImplicitly] private ImageView _imageComponent;

        private void InitializeMaterial() {
            _imageComponent.material = BundleLoader.UIAdditiveGlowMaterial;
        }

        #endregion

        #region HoverHint

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
