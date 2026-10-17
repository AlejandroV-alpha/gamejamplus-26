using UnityEngine;
using UnityEngine.UI;

namespace GameJam.Tablet
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TabletCrystalIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Shard(vh, -0.27f, -0.05f, 0.31f, 0.58f, -0.15f);
            Shard(vh, 0.04f, 0.02f, 0.37f, 0.92f, 0.04f);
            Shard(vh, 0.30f, -0.07f, 0.27f, 0.52f, 0.17f);
        }
        private void Shard(VertexHelper vh, float x, float y, float width, float height, float tilt)
        {
            var size = rectTransform.rect.size;
            var center = rectTransform.rect.center;
            Vector2 P(float u, float v) => center + Vector2.Scale(new Vector2(u, v), size);
            int start = vh.currentVertCount;
            vh.AddVert(P(x + tilt, y + height * 0.48f), color * 1.2f, Vector2.zero);
            vh.AddVert(P(x - width * 0.5f, y + height * 0.13f), color, Vector2.zero);
            vh.AddVert(P(x, y - height * 0.5f), color * 0.70f, Vector2.zero);
            vh.AddVert(P(x + width * 0.5f, y + height * 0.08f), color * 0.87f, Vector2.zero);
            vh.AddVert(P(x, y + height * 0.10f), Color.Lerp(color, Color.white, 0.26f), Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 4);
            vh.AddTriangle(start + 1, start + 2, start + 4);
            vh.AddTriangle(start + 2, start + 3, start + 4);
            vh.AddTriangle(start + 3, start, start + 4);
        }
    }
}
