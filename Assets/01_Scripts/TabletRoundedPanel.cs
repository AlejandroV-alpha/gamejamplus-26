using UnityEngine;
using UnityEngine.UI;

namespace GameJam.Tablet
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TabletRoundedPanel : MaskableGraphic
    {
        public float radius = 18;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            float r = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            vh.AddVert(rect.center, color, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - r : rect.xMin + r,
                    corner < 2 ? rect.yMax - r : rect.yMin + r);
                for (int step = 0; step <= 8; step++)
                {
                    float angle = (corner * 90 + step * 90f / 8) * Mathf.Deg2Rad;
                    vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, color, Vector2.zero);
                }
            }
            for (int i = 1; i <= 36; i++) vh.AddTriangle(0, i, i == 36 ? 1 : i + 1);
        }
    }
}
