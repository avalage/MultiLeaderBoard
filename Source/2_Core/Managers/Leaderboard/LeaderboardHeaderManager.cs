using System;
using BeatLeader.Components;
using BeatLeader.UI;
using BeatLeader.ViewControllers;
using HMUI;
using JetBrains.Annotations;
using Reactive;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Screen = HMUI.Screen;

#nullable disable

namespace BeatLeader {
    [UsedImplicitly]
    public class LeaderboardHeaderManager : IInitializable, ITickable, IDisposable {
        #region Initialize & Dispose
        
        [Inject, UsedImplicitly]
        private LeaderboardView _leaderboardView;

        [Inject, UsedImplicitly]
        private IReplayerViewNavigator _viewNavigator;
        
        [Inject, UsedImplicitly] 
        private SoloFreePlayFlowCoordinator _soloFlowCoordinator;
        
        public void Initialize() {
            LeaderboardState.IsVisibleChangedEvent += OnVisibilityChanged;
        }

        public void Dispose() {
            LeaderboardState.IsVisibleChangedEvent -= OnVisibilityChanged;
        }

        #endregion

        #region LazyInit

        private static readonly Color _legacyFunnyColor0 = new Color(1.0f, 0.0f, 0.4f, 3.0f);
        private static readonly Color _legacyFunnyColor1 = new Color(0.3f, 0.0f, 1.0f, 3.0f);
        private Color _boringColor0;
        private Color _boringColor1;

        private ImageView _headerImage;
        private ImageView _darkGoldHeaderBackground;
        private TextMeshProUGUI _headerText;
        private bool _initialized, _failed;
        private LeaderboardInfoPanel _infoPanel;
        private Sprite _originalHeaderSprite;
        private Material _originalHeaderMaterial;
        private Image.Type _originalHeaderType;
        private bool _originalHeaderPreserveAspect;
        private Color _originalHeaderColor;
        private Vector3 _originalHeaderScale;
        private const float DarkGoldHeaderBackgroundScaleX = 1.10f;
        private const float DarkGoldHeaderBackgroundScaleY = 1.08f;

        private void LazyInit() {
            if (_initialized || _failed) return;

            try {
                if (!TryFindHeader(out var header)) return;

                _headerText = header.GetComponentInChildren<TextMeshProUGUI>();
                _headerImage = header.GetComponentInChildren<ImageView>();
                if (_headerText == null || _headerImage == null) return;

                _infoPanel = new LeaderboardInfoPanel().WithRectExpand();
                _infoPanel.Use(_headerImage.transform);

                var wrapper = new ReplayerViewNavigatorWrapper(_viewNavigator, _soloFlowCoordinator);
                _infoPanel.Setup(wrapper);
   
                _boringColor0 = _headerImage.color0;
                _boringColor1 = _headerImage.color1;
                CaptureOriginalHeaderImage();

                if (BeatLeaderDarkGoldTheme.Enabled) {
                    ApplyDarkGoldHeaderImage();
                }

                _initialized = true;
            } catch (Exception e) {
                Plugin.Log.Error($"LeaderboardHeaderManager initialization failed: {e}");
                _failed = true;
            }
        }

        private bool TryFindHeader(out GameObject header) {
            var screen = _leaderboardView.gameObject.GetComponentInParent<Screen>();
            if (screen == null) {
                header = null;
                return false;
            }

            var headerTransform = FindDescendant(screen.transform, "HeaderPanel");
            if (headerTransform == null) {
                header = null;
                return false;
            }

            header = headerTransform.gameObject;
            return header != null;
        }

        private static Transform FindDescendant(Transform parent, string childName) {
            for (var i = 0; i < parent.childCount; i++) {
                var child = parent.GetChild(i);
                if (child.name == childName) return child;

                var descendant = FindDescendant(child, childName);
                if (descendant != null) return descendant;
            }

            return null;
        }

        #endregion

        #region Events

        private void OnVisibilityChanged(bool visible) {
            if (visible) {
                LazyInit();
                OnEnable();
            } else {
                OnDisable();
            }
        }

        private void OnEnable() {
            if (!_initialized) return;
            _isFunny = true;
            _idle = false;
            _toleranceCheck = 0;
            _headerText.enabled = false;
            if (BeatLeaderDarkGoldTheme.Enabled) {
                ApplyDarkGoldHeaderImage();
            }
            _infoPanel.Enabled = true;
        }

        private void OnDisable() {
            if (!_initialized) return;
            _isFunny = false;
            _idle = false;
            _toleranceCheck = 0;
            _headerText.enabled = true;
            _infoPanel.Enabled = false;
            if (BeatLeaderDarkGoldTheme.Enabled) {
                RestoreOriginalHeaderImage();
            }
        }

        #endregion

        #region Animation

        private const float Tolerance = 0.001f;
        private float _toleranceCheck;
        private bool _idle = true;
        private bool _isFunny;

        public void Tick() {
            if (_idle) return;

            Color target0, target1;

            if (_isFunny) {
                target0 = BeatLeaderDarkGoldTheme.Enabled ? Color.clear : _legacyFunnyColor0;
                target1 = BeatLeaderDarkGoldTheme.Enabled ? Color.clear : _legacyFunnyColor1;
            } else {
                target0 = _boringColor0;
                target1 = _boringColor1;
            }

            var t = Time.deltaTime * 10;
            _toleranceCheck = Mathf.Lerp(_toleranceCheck, 1, t);
            if (1 - _toleranceCheck < Tolerance) {
                ClampTo(target0, target1);
                _idle = true;
                return;
            }

            LerpTo(target0, target1, t);
        }

        private void LerpTo(Color color0, Color color1, float t) {
            _headerImage.color0 = Color.Lerp(_headerImage.color0, color0, t);
            _headerImage.color1 = Color.Lerp(_headerImage.color1, color1, t);
        }

        private void ClampTo(Color color0, Color color1) {
            _headerImage.color0 = color0;
            _headerImage.color1 = color1;
            if (BeatLeaderDarkGoldTheme.Enabled && _darkGoldHeaderBackground != null && _darkGoldHeaderBackground.gameObject.activeSelf) {
                _headerImage.color = Color.clear;
            }
        }

        private void CaptureOriginalHeaderImage() {
            _originalHeaderSprite = _headerImage.sprite;
            _originalHeaderMaterial = _headerImage.material;
            _originalHeaderType = _headerImage.type;
            _originalHeaderPreserveAspect = _headerImage.preserveAspect;
            _originalHeaderColor = _headerImage.color;
            _originalHeaderScale = _headerImage.rectTransform.localScale;
        }

        private void ApplyDarkGoldHeaderImage() {
            _darkGoldHeaderBackground ??= CreateDarkGoldHeaderBackground();
            _darkGoldHeaderBackground.gameObject.SetActive(true);
            _darkGoldHeaderBackground.transform.SetAsFirstSibling();

            _headerImage.material = GameResources.UINoGlowMaterial;
            _headerImage.color = Color.clear;
            _headerImage.color0 = Color.clear;
            _headerImage.color1 = Color.clear;
            _headerImage.rectTransform.localScale = _originalHeaderScale;
        }

        private ImageView CreateDarkGoldHeaderBackground() {
            var gameObject = new GameObject("DarkGoldHeaderBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageView));
            gameObject.transform.SetParent(_headerImage.transform, false);

            var image = gameObject.GetComponent<ImageView>();
            image.material = GameResources.UINoGlowMaterial;
            image.sprite = BeatLeaderDarkGoldTheme.LeaderboardHeaderSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = false;

            var rectTransform = image.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = new Vector3(
                DarkGoldHeaderBackgroundScaleX,
                DarkGoldHeaderBackgroundScaleY,
                1.0f
            );

            return image;
        }

        private void RestoreOriginalHeaderImage() {
            if (_darkGoldHeaderBackground != null) {
                _darkGoldHeaderBackground.gameObject.SetActive(false);
            }

            _headerImage.material = _originalHeaderMaterial;
            _headerImage.sprite = _originalHeaderSprite;
            _headerImage.type = _originalHeaderType;
            _headerImage.preserveAspect = _originalHeaderPreserveAspect;
            _headerImage.color = _originalHeaderColor;
            _headerImage.color0 = _boringColor0;
            _headerImage.color1 = _boringColor1;
            _headerImage.rectTransform.localScale = _originalHeaderScale;
        }

        #endregion
    }
}
