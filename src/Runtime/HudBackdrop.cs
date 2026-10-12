using UnityEngine;
using UnityEngine.UI;

namespace Tylevo.FieldAttachments.Runtime
{
    // A solid reading area with a soft right edge; no texture or material allocation.
    public sealed class HudBackdrop : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            Color opaque = color;
            Color clear = new Color(color.r, color.g, color.b, 0);
            float fadeStart = Mathf.Lerp(rect.xMin, rect.xMax, .62f);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), opaque, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), opaque, Vector2.zero);
            mesh.AddVert(new Vector3(fadeStart, rect.yMin), opaque, Vector2.zero);
            mesh.AddVert(new Vector3(fadeStart, rect.yMax), opaque, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), clear, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), clear, Vector2.zero);
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 1, 3);
            mesh.AddTriangle(2, 3, 4); mesh.AddTriangle(4, 3, 5);
        }
    }
}
