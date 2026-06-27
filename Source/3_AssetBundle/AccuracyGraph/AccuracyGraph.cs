using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace BeatLeader {
    [MovedFrom(true, "BeatLeader", "BeatLeader", null)]
    [RequireComponent(typeof(RectTransform))]
    public class AccuracyGraph: UIBehaviour {
        #region Serialized

        [SerializeField] private AccuracyGraphLine graphLine;
        [SerializeField] private Material backgroundMaterial;

        public Canvas Canvas => graphLine != null ? graphLine.canvas : null;

        #endregion

        #region Construct

        private Material _backgroundMaterialInstance;
        private bool _loggedMissingBackgroundMaterial;
        private bool _loggedMissingGraphLine;
        private bool _loggedMissingCursorProperty;
        private bool _loggedMissingViewRectProperty;
        private bool _loggedMissingSongDurationProperty;

        public void Construct(Image backgroundImage) {
            if (graphLine == null && !_loggedMissingGraphLine) {
                _loggedMissingGraphLine = true;
                Plugin.Log.Info("[AccuracyGraph] Prefab serialized graphLine is null.");
            }

            if (backgroundMaterial == null) {
                if (!_loggedMissingBackgroundMaterial) {
                    _loggedMissingBackgroundMaterial = true;
                    Plugin.Log.Info("[AccuracyGraph] Prefab serialized backgroundMaterial is null.");
                }
                return;
            }

            if (backgroundImage == null) {
                Plugin.Log.Info("[AccuracyGraph] Construct received null background image.");
                return;
            }

            _backgroundMaterialInstance = Instantiate(backgroundMaterial);
            backgroundImage.material = _backgroundMaterialInstance;
            Plugin.Log.Info(
                $"[AccuracyGraph] Constructed original background material; material='{backgroundMaterial.name}', " +
                $"shader='{(backgroundMaterial.shader != null ? backgroundMaterial.shader.name : "<null>")}', " +
                $"hasViewRect={_backgroundMaterialInstance.HasProperty(ViewRectPropertyId)}, " +
                $"hasDuration={_backgroundMaterialInstance.HasProperty(SongDurationPropertyId)}, " +
                $"hasCursor={_backgroundMaterialInstance.HasProperty(CursorPositionPropertyId)}."
            );
        }

        #endregion

        #region Setup

        private Rect _viewRect = Rect.MinMaxRect(0, 0, 1, 1);
        private float _songDuration = 1.0f;

        public void Setup(List<Vector2> positions, Rect viewRect, float canvasRadius, float songDuration) {
            _songDuration = songDuration;
            _viewRect = viewRect;

            if (graphLine == null) {
                if (!_loggedMissingGraphLine) {
                    _loggedMissingGraphLine = true;
                    Plugin.Log.Info("[AccuracyGraph] Setup skipped because graphLine is null.");
                }
                return;
            }

            graphLine.Setup(positions, _viewRect, canvasRadius);
            UpdateBackground();
        }

        #endregion

        #region Shader

        private static readonly int ViewRectPropertyId = Shader.PropertyToID("_ViewRect");
        private static readonly int SongDurationPropertyId = Shader.PropertyToID("_SongDuration");
        private static readonly int CursorPositionPropertyId = Shader.PropertyToID("_CursorPosition");

        private void UpdateBackground() {
            if (_backgroundMaterialInstance == null) {
                if (!_loggedMissingBackgroundMaterial) {
                    _loggedMissingBackgroundMaterial = true;
                    Plugin.Log.Info("[AccuracyGraph] Background material instance is null; shader grid/cursor cannot be updated.");
                }
                return;
            }

            var viewRectVector = new Vector4(_viewRect.xMin, _viewRect.yMin, _viewRect.xMax, _viewRect.yMax);
            if (_backgroundMaterialInstance.HasProperty(ViewRectPropertyId)) {
                _backgroundMaterialInstance.SetVector(ViewRectPropertyId, viewRectVector);
            } else if (!_loggedMissingViewRectProperty) {
                _loggedMissingViewRectProperty = true;
                Plugin.Log.Info("[AccuracyGraph] Background material has no _ViewRect property.");
            }

            if (_backgroundMaterialInstance.HasProperty(SongDurationPropertyId)) {
                _backgroundMaterialInstance.SetFloat(SongDurationPropertyId, _songDuration);
            } else if (!_loggedMissingSongDurationProperty) {
                _loggedMissingSongDurationProperty = true;
                Plugin.Log.Info("[AccuracyGraph] Background material has no _SongDuration property.");
            }
        }

        public void SetCursor(float viewTime) {
            if (_backgroundMaterialInstance == null) {
                if (!_loggedMissingBackgroundMaterial) {
                    _loggedMissingBackgroundMaterial = true;
                    Plugin.Log.Info("[AccuracyGraph] Cursor update skipped because background material instance is null.");
                }
                return;
            }

            if (_backgroundMaterialInstance.HasProperty(CursorPositionPropertyId)) {
                _backgroundMaterialInstance.SetFloat(CursorPositionPropertyId, viewTime);
            } else if (!_loggedMissingCursorProperty) {
                _loggedMissingCursorProperty = true;
                Plugin.Log.Info("[AccuracyGraph] Background material has no _CursorPosition property; hover timeline will be invisible.");
            }
        }

        #endregion
    }
}
