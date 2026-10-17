using UnityEngine;
using UnityEngine.UI;

namespace GameJam.Tablet
{
    // Geometry-based navigation symbols stay sharp and do not depend on font glyphs.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TabletNavigationIcon : MaskableGraphic
    {
        public enum Symbol { Back, Home, Close, Apps }
        public Symbol symbol;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float size = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
            Vector2 center = rectTransform.rect.center;
            void Line(Vector2 a, Vector2 b)
            {
                a = center + a * size; b = center + b * size;
                Vector2 normal = new Vector2(-(b-a).y,(b-a).x).normalized * size * 0.045f;
                int i = vh.currentVertCount;
                vh.AddVert(a-normal,color,Vector2.zero); vh.AddVert(a+normal,color,Vector2.zero);
                vh.AddVert(b+normal,color,Vector2.zero); vh.AddVert(b-normal,color,Vector2.zero);
                vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
            }
            if (symbol == Symbol.Back)
            {
                Line(new Vector2(0.35f,0),new Vector2(-0.32f,0));
                Line(new Vector2(-0.32f,0),new Vector2(-0.04f,0.28f));
                Line(new Vector2(-0.32f,0),new Vector2(-0.04f,-0.28f));
            }
            else if (symbol == Symbol.Home)
            {
                Line(new Vector2(-0.38f,0.02f),new Vector2(0,0.34f));
                Line(new Vector2(0,0.34f),new Vector2(0.38f,0.02f));
                Line(new Vector2(-0.27f,0.07f),new Vector2(-0.27f,-0.32f));
                Line(new Vector2(0.27f,0.07f),new Vector2(0.27f,-0.32f));
                Line(new Vector2(-0.27f,-0.32f),new Vector2(0.27f,-0.32f));
                Line(new Vector2(-0.07f,-0.32f),new Vector2(-0.07f,-0.07f));
                Line(new Vector2(-0.07f,-0.07f),new Vector2(0.07f,-0.07f));
                Line(new Vector2(0.07f,-0.07f),new Vector2(0.07f,-0.32f));
            }
            else if (symbol == Symbol.Apps)
            {
                for (int row = -1; row <= 1; row++)
                    for (int column = -1; column <= 1; column++)
                    {
                        Vector2 p = center + new Vector2(column, row) * size * 0.27f;
                        float half = size * 0.075f;
                        int i = vh.currentVertCount;
                        vh.AddVert(p + new Vector2(-half, -half), color, Vector2.zero);
                        vh.AddVert(p + new Vector2(-half, half), color, Vector2.zero);
                        vh.AddVert(p + new Vector2(half, half), color, Vector2.zero);
                        vh.AddVert(p + new Vector2(half, -half), color, Vector2.zero);
                        vh.AddTriangle(i, i+1, i+2);
                        vh.AddTriangle(i, i+2, i+3);
                    }
            }
            else
            {
                Line(new Vector2(-0.25f,-0.25f),new Vector2(0.25f,0.25f));
                Line(new Vector2(-0.25f,0.25f),new Vector2(0.25f,-0.25f));
            }
        }
    }
}
