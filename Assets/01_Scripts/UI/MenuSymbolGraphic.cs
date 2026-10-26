using UnityEngine;
using UnityEngine.UI;

namespace GameJam.UI
{
    // Mesh icons stay crisp at any canvas scale and do not depend on font glyphs.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuSymbolGraphic : MaskableGraphic
    {
        public enum Symbol { Chevron, Check, Play, Home, Exit, Mission, Settings, Crew }
        public Symbol symbol;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (symbol)
            {
                case Symbol.Chevron: Line(mesh, -.3f,.15f,0,-.15f); Line(mesh,0,-.15f,.3f,.15f); break;
                case Symbol.Check: Line(mesh,-.32f,0,-.08f,-.23f); Line(mesh,-.08f,-.23f,.35f,.28f); break;
                case Symbol.Play: Triangle(mesh,new(-.22f,-.34f),new(-.22f,.34f),new(.34f,0)); break;
                case Symbol.Home: Line(mesh,-.4f,0,0,.35f); Line(mesh,0,.35f,.4f,0); Line(mesh,-.28f,.08f,-.28f,-.32f); Line(mesh,-.28f,-.32f,.28f,-.32f); Line(mesh,.28f,-.32f,.28f,.08f); break;
                case Symbol.Exit: Line(mesh,-.05f,.35f,-.35f,.35f); Line(mesh,-.35f,.35f,-.35f,-.35f); Line(mesh,-.35f,-.35f,-.05f,-.35f); Line(mesh,-.08f,0,.4f,0); Line(mesh,.2f,.2f,.4f,0); Line(mesh,.4f,0,.2f,-.2f); break;
                case Symbol.Mission: Line(mesh,0,.4f,.3f,-.25f); Line(mesh,.3f,-.25f,0,-.12f); Line(mesh,0,-.12f,-.3f,-.25f); Line(mesh,-.3f,-.25f,0,.4f); break;
                case Symbol.Settings:
                    for(int i=0;i<3;i++){float y=.28f-i*.28f;float x=i==1?.17f:-.13f;Line(mesh,-.38f,y,.38f,y);Line(mesh,x,y-.1f,x,y+.1f,.12f);} break;
                case Symbol.Crew:
                    Line(mesh,-.3f,-.3f,-.3f,-.1f); Line(mesh,-.3f,-.1f,.3f,-.1f); Line(mesh,.3f,-.1f,.3f,-.3f);
                    Line(mesh,-.12f,.12f,-.12f,.34f);Line(mesh,-.12f,.34f,.12f,.34f);Line(mesh,.12f,.34f,.12f,.12f);Line(mesh,.12f,.12f,-.12f,.12f);break;
            }
        }
        Vector2 Point(Vector2 p) => rectTransform.rect.center + Vector2.Scale(p, rectTransform.rect.size);
        void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
        {
            int n=mesh.currentVertCount; mesh.AddVert(Point(a),color,Vector2.zero);mesh.AddVert(Point(b),color,Vector2.zero);mesh.AddVert(Point(c),color,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);
        }
        void Line(VertexHelper mesh,float x1,float y1,float x2,float y2,float thickness=.075f)
        {
            Vector2 a=Point(new(x1,y1)),b=Point(new(x2,y2)); Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized * Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*thickness*.5f;
            int n=mesh.currentVertCount;mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
        }
    }
}
