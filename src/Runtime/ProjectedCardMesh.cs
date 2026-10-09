using Tylevo.FieldAttachments.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    // Existing uGUI graphics, projected from one shared 3D plane. No camera/layer/material changes.
    // UI only: never attach components to the gun, write native transforms, or handle gameplay input.
    public sealed class ProjectedCardMesh : BaseMeshEffect
    {
        private PanelProjection? _projection;
        private Matrix4x4 _toPanel, _fromCanvas;
        private Vector2 _canvasOrigin;
        private float _width=OverlayLayout.PanelWidth, _height=OverlayLayout.PanelHeight;

        public void SetProjection(PanelProjection? projection, RectTransform panel, RectTransform canvas,
            float width=OverlayLayout.PanelWidth, float height=OverlayLayout.PanelHeight)
        {
            bool changed = projection != null || _projection != null;
            _projection = projection; _width=width; _height=height;
            if (projection != null)
            {
                _toPanel = panel.worldToLocalMatrix * transform.localToWorldMatrix;
                _fromCanvas = transform.worldToLocalMatrix * canvas.localToWorldMatrix;
                _canvasOrigin = new Vector2(canvas.rect.xMin, canvas.rect.yMax);
            }
            if (changed) graphic.SetVerticesDirty();
        }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || _projection == null) return;
            var vertex = new UIVertex();
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                Vector3 local = _toPanel.MultiplyPoint3x4(vertex.position);
                PanelPoint point = _projection.Map(local.x / _width, -local.y / _height);
                vertex.position = _fromCanvas.MultiplyPoint3x4(new Vector3(_canvasOrigin.x + point.X, _canvasOrigin.y - point.Y, 0));
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
