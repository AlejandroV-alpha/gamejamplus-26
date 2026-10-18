using UnityEngine;
using UnityEngine.UI;

namespace GameJam.UI
{
    /// <summary>Angular astronaut helmet silhouette with an open visual area for the animated visor sprites.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PlayerHelmetGraphic : MaskableGraphic
    {
        // Coordinates use the full portrait rectangle, with the chin toward negative Y.
        private static readonly Vector2[] Shell =
        {
            new Vector2(-.24f, .49f), new Vector2(.24f, .49f),
            new Vector2(.43f, .33f), new Vector2(.48f, .10f),
            new Vector2(.48f, -.20f), new Vector2(.31f, -.40f),
            new Vector2(.19f, -.48f), new Vector2(-.19f, -.48f),
            new Vector2(-.31f, -.40f), new Vector2(-.48f, -.20f),
            new Vector2(-.48f, .10f), new Vector2(-.43f, .33f)
        };
        private static readonly Vector2[] Visor =
        {
            new Vector2(-.23f, .33f), new Vector2(.23f, .33f),
            new Vector2(.34f, .22f), new Vector2(.34f, -.17f),
            new Vector2(.19f, -.32f), new Vector2(-.19f, -.32f),
            new Vector2(-.34f, -.17f), new Vector2(-.34f, .22f)
        };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Polygon(vh, Shell, new Color32(25, 39, 43, 255), 1.06f);
            Polygon(vh, Shell, Color.Lerp(color, Color.white, .22f));
            Polygon(vh, Shell, color, .91f);
            Polygon(vh, Visor, new Color32(41, 59, 61, 255), 1.10f);
            Polygon(vh, Visor, new Color32(3, 12, 14, 255));
            // Ear housings, dark pads and chin plate match the chunky helmet construction.
            Quad(vh, -.50f, -.22f, -.39f, .12f, Color.Lerp(color, Color.black, .44f));
            Quad(vh, .39f, -.22f, .50f, .12f, Color.Lerp(color, Color.black, .44f));
            Quad(vh, -.24f, -.43f, .24f, -.37f, Color.Lerp(color, Color.white, .35f));
            Quad(vh, -.17f, -.48f, .17f, -.44f, new Color32(25, 39, 43, 255));
        }

        private void Polygon(VertexHelper vh, Vector2[] points, Color tint, float scale = 1)
        {
            var rect = GetPixelAdjustedRect();
            int start = vh.currentVertCount;
            // Each polygon needs its own center. Using the portrait origin here
            // stretches the ear/chin triangles across the animated face.
            Vector2 center = Vector2.zero;
            foreach (var point in points) center += point;
            center /= points.Length;
            tint.a *= color.a;
            vh.AddVert(rect.center + Vector2.Scale(center * scale, rect.size), tint, Vector2.zero);
            foreach (var point in points)
                vh.AddVert(rect.center + Vector2.Scale(point * scale, rect.size), tint, Vector2.zero);
            for (int i = 0; i < points.Length; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Length);
        }

        private void Quad(VertexHelper vh, float left, float bottom, float right, float top, Color tint)
        {
            Polygon(vh, new[] { new Vector2(left, top), new Vector2(right, top), new Vector2(right, bottom), new Vector2(left, bottom) }, tint);
        }
    }
}
