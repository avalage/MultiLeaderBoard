using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace BeatLeader {
    [MovedFrom(true, "BeatLeader", "BeatLeader", null)]
    public class AccuracyGraphLine : Graphic {
        #region Serialized

        [SerializeField] private int resolution = 500;
        [SerializeField] private float thickness = 0.2f;

        #endregion

        #region Start

        private GraphMeshHelper _graphMeshHelper;

        protected override void Start() {
            base.Start();
            EnsureGraphMeshHelper();
        }

        #endregion

        #region OnPopulateMesh

        protected override void OnPopulateMesh(VertexHelper vh) {
            EnsureGraphMeshHelper();

            var screenRect = RectTransformUtility.PixelAdjustRect(rectTransform, canvas);
            var screenViewTransform = new ScreenViewTransform(screenRect, _viewRect);

            if (_points != null) {
                _graphMeshHelper.SetPoints(_points);
            }
            _graphMeshHelper.PopulateMesh(vh, screenViewTransform, _canvasRadius);
        }

        #endregion

        #region Setup

        private List<Vector2>? _points;
        private float _canvasRadius;
        private Rect _viewRect = Rect.MinMaxRect(0, 0, 1, 1);

        public void Setup(List<Vector2> points, Rect viewRect, float canvasRadius) {
            _points = points;
            _viewRect = viewRect;
            _canvasRadius = canvasRadius;

            SetVerticesDirty();
        }

        #endregion

        #region Mesh Helper

        private void EnsureGraphMeshHelper() {
            _graphMeshHelper ??= new GraphMeshHelper(resolution, 1, thickness);
        }

        #endregion
    }
}
