using System;
using System.Linq;
using System.Reflection;
using BeatLeader.Models;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using JetBrains.Annotations;
using UnityEngine;
using VRUIControls;
using Object = UnityEngine.Object;

namespace BeatLeader.Components {
    internal class AccuracyGraphPanel : ReeUIComponentV2 {
        #region Initialize

        private Component _accuracyGraphComponent;
        private MethodInfo _accuracyGraphConstructMethod;
        private MethodInfo _accuracyGraphSetupMethod;
        private MethodInfo _accuracyGraphSetCursorMethod;
        private PropertyInfo _accuracyGraphCanvasProperty;
        private bool _hasGraphData;

        private bool IsGraphReady => _graphContainer != null &&
            _accuracyGraphComponent != null &&
            _accuracyGraphSetupMethod != null &&
            _accuracyGraphSetCursorMethod != null;

        protected override void OnInitialize() {
            if (_graphContainer == null) {
                Plugin.Log.Info("[AccuracyGraph] Graph container is null; cannot initialize accuracy graph.");
                return;
            }

            if (BundleLoader.AccuracyGraphPrefab == null) {
                Plugin.Log.Info("[AccuracyGraph] BundleLoader.AccuracyGraphPrefab is null; original BeatLeader graph cannot be created.");
                return;
            }

            var go = Object.Instantiate(BundleLoader.AccuracyGraphPrefab, _graphContainer, false);
            if (go == null) {
                Plugin.Log.Info("[AccuracyGraph] AccuracyGraph prefab instantiation returned null.");
                return;
            }

            _accuracyGraphComponent = FindAccuracyGraphComponent(go);
            if (_accuracyGraphComponent == null) {
                LogPrefabComponents(go, "AccuracyGraph component is missing");
                return;
            }

            var graphType = _accuracyGraphComponent.GetType();
            _accuracyGraphConstructMethod = graphType.GetMethod("Construct", BindingFlags.Instance | BindingFlags.Public);
            _accuracyGraphSetupMethod = graphType.GetMethod("Setup", BindingFlags.Instance | BindingFlags.Public);
            _accuracyGraphSetCursorMethod = graphType.GetMethod("SetCursor", BindingFlags.Instance | BindingFlags.Public);
            _accuracyGraphCanvasProperty = graphType.GetProperty("Canvas", BindingFlags.Instance | BindingFlags.Public);

            if (_accuracyGraphSetupMethod == null || _accuracyGraphSetCursorMethod == null) {
                Plugin.Log.Info(
                    $"[AccuracyGraph] Original component type '{graphType.FullName}' from '{graphType.Assembly.GetName().Name}' " +
                    $"does not expose required Setup/SetCursor methods."
                );
                _accuracyGraphComponent = null;
                return;
            }

            if (_accuracyGraphConstructMethod != null) {
                try {
                    _accuracyGraphConstructMethod.Invoke(_accuracyGraphComponent, new object[] { _graphBackground });
                } catch (Exception exception) {
                    Plugin.Log.Info($"[AccuracyGraph] Construct invocation failed: {exception.GetBaseException().Message}");
                }
            } else {
                Plugin.Log.Info(
                    $"[AccuracyGraph] Original component type '{graphType.FullName}' has no Construct method; background material may be missing."
                );
            }

            var canvas = GetAccuracyGraphCanvas();
            Plugin.Log.Info(
                $"[AccuracyGraph] Original prefab initialized through component '{graphType.FullName}' from '{graphType.Assembly.GetName().Name}'; " +
                $"prefab='{BundleLoader.AccuracyGraphPrefab.name}', " +
                $"background={(_graphBackground != null ? _graphBackground.name : "<null>")}, " +
                $"canvas={(canvas != null ? canvas.name : "<null>")}."
            );
        }

        private static Component FindAccuracyGraphComponent(GameObject graphObject) {
            var components = graphObject.GetComponents<Component>();
            return components.FirstOrDefault(component => {
                if (component == null) {
                    return false;
                }

                var type = component.GetType();
                return type.Name == "AccuracyGraph" && type.FullName == "BeatLeader.AccuracyGraph";
            });
        }

        private static void LogPrefabComponents(GameObject graphObject, string reason) {
            var components = graphObject.GetComponents<Component>()
                .Select(component => {
                    if (component == null) {
                        return "<missing>";
                    }

                    var type = component.GetType();
                    return $"{type.FullName} [{type.Assembly.GetName().Name}]";
                })
                .ToArray();

            Plugin.Log.Info(
                $"[AccuracyGraph] {reason} on prefab '{graphObject.name}'. Components: {string.Join(", ", components)}"
            );
        }

        #endregion

        #region SetScoreStats

        private float[] _points = Array.Empty<float>();
        private float _songDuration = 1.0f;
        private Rect _viewRect = Rect.zero;

        public void SetScoreStats(ScoreStats scoreStats) {
            _hasGraphData = false;

            var graph = scoreStats.scoreGraphTracker?.graph;
            if (graph == null || graph.Length == 0) {
                _points = Array.Empty<float>();
                Plugin.Log.Info("[AccuracyGraph] ScoreStats has no scoreGraphTracker graph; graph panel will stay empty.");
                return;
            }
            if (!IsGraphReady) {
                Plugin.Log.Info("[AccuracyGraph] SetScoreStats skipped because original graph prefab is not ready.");
                return;
            }

            _songDuration = Mathf.Max(1.0f, scoreStats.winTracker.endTime);
            _points = graph;

            AccuracyGraphUtils.PostProcessPoints(_points, out var positions, out _viewRect);
            try {
                _accuracyGraphSetupMethod.Invoke(_accuracyGraphComponent, new object[] { positions, _viewRect, GetCanvasRadius(), _songDuration });
            } catch (Exception exception) {
                Plugin.Log.Info($"[AccuracyGraph] Setup invocation failed: {exception.GetBaseException().Message}");
                return;
            }

            _hasGraphData = true;
            Plugin.Log.Info(
                $"[AccuracyGraph] Setup complete; rawPoints={_points.Length}, positions={positions.Count}, " +
                $"viewRect=({_viewRect.xMin:F3},{_viewRect.yMin:F3},{_viewRect.xMax:F3},{_viewRect.yMax:F3}), duration={_songDuration:F2}."
            );
        }

        #endregion

        #region CursorPosition

        private readonly Vector3[] _corners = new Vector3[4];
        private VRPointer _vrPointer;
        private Vector3 _lastPosition3D;
        private bool _cursorInitialized;

        private void OnEnable() {
            _vrPointer = FindObjectOfType<VRPointer>();
            _cursorInitialized = _vrPointer != null;
            _lastPosition3D = default;
        }

        private void LateUpdate() {
            if (!IsGraphReady || !_hasGraphData || !_graphContainer.gameObject.activeInHierarchy || !_cursorInitialized) return;
            if (_vrPointer == null) return;

            var cursorPosition3D = _vrPointer.cursorPosition;
            if (cursorPosition3D.Equals(_lastPosition3D)) return;
            _lastPosition3D = cursorPosition3D;

            CalculateCursorPosition(cursorPosition3D, out var normalized);
            UpdateCursor(normalized);
        }

        private void CalculateCursorPosition(Vector3 worldCursor, out Vector2 normalized) {
            var canvasRadius = GetCanvasRadius() * _graphContainer.lossyScale.x;
            var nonCurved = AccuracyGraphUtils.TransformPointFrom3DToCanvas(worldCursor, canvasRadius);

            _graphContainer.GetWorldCorners(_corners);

            normalized = new Vector2(
                new Range(_corners[0].x, _corners[3].x).GetRatio(nonCurved.x),
                new Range(_corners[0].y, _corners[1].y).GetRatio(nonCurved.y)
            );
        }

        #endregion

        #region UpdateCursor

        private float _targetViewTime;
        private float _currentViewTime;

        private void UpdateCursor(Vector2 normalized) {
            if (normalized.x < 0 || normalized.y < 0 || normalized.x > 1 || normalized.y > 1) return;
            var viewCursor = Rect.NormalizedToPoint(_viewRect, normalized);
            _targetViewTime = Mathf.Clamp01(viewCursor.x);
        }

        private void Update() {
            if (!IsGraphReady || !_hasGraphData || !_graphContainer.gameObject.activeInHierarchy || float.IsNaN(_targetViewTime)) return;
            _currentViewTime = Mathf.Lerp(_currentViewTime, _targetViewTime, Time.deltaTime * 10.0f);
            var songTime = _currentViewTime * _songDuration;
            var accuracy = GetAccuracy(_currentViewTime);
            try {
                _accuracyGraphSetCursorMethod.Invoke(_accuracyGraphComponent, new object[] { _currentViewTime });
            } catch (Exception exception) {
                Plugin.Log.Info($"[AccuracyGraph] SetCursor invocation failed; cursor updates disabled: {exception.GetBaseException().Message}");
                _accuracyGraphSetCursorMethod = null;
                return;
            }

            CursorText = FormatCursorText(songTime, accuracy);
        }

        private static string FormatCursorText(float songTime, float accuracy) {
            var fullMinutes = Mathf.FloorToInt(songTime / 60.0f);
            var remainingSeconds = Mathf.FloorToInt(Mathf.Abs(songTime % 60.0f));
            return $"<color=#FFB000><bll>ls-song-time</bll>: </color>{fullMinutes}:{remainingSeconds:00}  <color=#FFB000><bll>ls-accuracy</bll>: </color>{accuracy * 100.0f:F2}<size=70%>%";
        }

        private float GetAccuracy(float viewTime) {
            if (_points.Length == 0) return 1.0f;

            var xStep = 1.0f / _points.Length;
            var x = xStep;

            for (var i = 1; i < _points.Length; i++, x += xStep) {
                if (x < viewTime) continue;
                var xRange = new Range(x - xStep, x);
                var yRange = new Range(_points[i - 1], _points[i]);
                var ratio = xRange.GetRatio(viewTime);
                return yRange.SlideBy(ratio);
            }

            return _points.Last();
        }

        #endregion

        #region GetCanvasRadius

        private readonly CurvedCanvasSettingsHelper _curvedCanvasSettingsHelper = new();

        private float GetCanvasRadius() {
            var canvas = GetAccuracyGraphCanvas();
            if (canvas == null) {
                Plugin.Log.Info("[AccuracyGraph] Canvas is null while calculating graph radius; using flat fallback radius.");
                return float.MaxValue;
            }

            var canvasSettings = _curvedCanvasSettingsHelper.GetCurvedCanvasSettings(canvas);
            return canvasSettings == null ? float.MaxValue : canvasSettings.radius;
        }

        private Canvas GetAccuracyGraphCanvas() {
            if (_accuracyGraphComponent == null || _accuracyGraphCanvasProperty == null) {
                return null;
            }

            try {
                return _accuracyGraphCanvasProperty.GetValue(_accuracyGraphComponent) as Canvas;
            } catch (Exception exception) {
                Plugin.Log.Info($"[AccuracyGraph] Canvas property read failed: {exception.GetBaseException().Message}");
                return null;
            }
        }

        #endregion

        #region SetActive

        public void SetActive(bool value) {
            Active = value;
        }

        #endregion

        #region Active

        private bool _active = true;

        [UIValue("active"), UsedImplicitly]
        private bool Active {
            get => _active;
            set {
                if (_active.Equals(value)) return;
                _active = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region UIComponents

        [UIComponent("graph-container"), UsedImplicitly]
        private RectTransform _graphContainer;

        [UIComponent("graph-container"), UsedImplicitly]
        private ImageView _graphBackground;

        [UIComponent("cursor-hint"), UsedImplicitly]
        private RectTransform _hintTransform;

        #endregion

        #region CursorText

        private string _cursorText = "";

        [UIValue("cursor-text"), UsedImplicitly]
        private string CursorText {
            get => _cursorText;
            set {
                if (_cursorText.Equals(value)) return;
                _cursorText = value;
                NotifyPropertyChanged();
            }
        }

        #endregion
    }
}
