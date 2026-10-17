using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameJam.Characters.Editor
{
    // Original vector-like glyphs rasterized once in the Editor, never per player/frame.
    public static class VisorSpriteBuilder
    {
        public const string Folder = "Assets/05_Materials/Visor";
        [MenuItem("Tools/GameJam/Rebuild Visor Sprites")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Save("EyesIdle", (x,y) => Mathf.Min(Mathf.Min(Line(x,y,-.31f,.19f,-.10f,.12f,.018f), Line(x,y,.10f,.12f,.31f,.19f,.018f)), Mathf.Min(Ellipse(x,y,-.20f,.055f,.042f,.045f),Ellipse(x,y,.20f,.055f,.042f,.045f))));
            Save("EyesBlink", (x,y) => Mathf.Min(Line(x,y,-.31f,.12f,-.1f,.12f,.012f),Line(x,y,.1f,.12f,.31f,.12f,.012f)));
            Save("EyesHurt", (x,y) => Mathf.Min(Chevron(x,y,-.2f,.13f,1),Chevron(x,y,.2f,.13f,-1)));
            Save("EyesDead", (x,y) => Mathf.Min(Cross(x,y,-.2f,.14f),Cross(x,y,.2f,.14f)));
            Save("MouthSmile", (x,y) => { float t=Mathf.Clamp(x,-.23f,.23f); return Vector2.Distance(new Vector2(x,y),new Vector2(t,-.24f+1.5f*t*t))-.015f; });
            Save("MouthSmall", (x,y) => Ellipse(x,y,0,-.19f,.115f,.045f));
            Save("MouthOpen", (x,y) => Ellipse(x,y,0,-.19f,.14f,.10f));
            Save("MouthHurt", (x,y) => { float t=Mathf.Clamp(x,-.15f,.15f); return Vector2.Distance(new Vector2(x,y),new Vector2(t,-.16f-1.5f*t*t))-.014f; });
            Save("MouthDead", (x,y) => Line(x,y,-.14f,-.2f,.14f,-.2f,.012f));
            AssetDatabase.Refresh();
        }
        static float Line(float x,float y,float ax,float ay,float bx,float by,float radius)
        {
            var p=new Vector2(x-ax,y-ay); var d=new Vector2(bx-ax,by-ay);
            return (p-d*Mathf.Clamp01(Vector2.Dot(p,d)/d.sqrMagnitude)).magnitude-radius;
        }
        static float Chevron(float x,float y,float cx,float cy,float dir) => Mathf.Min(Line(x,y,cx-dir*.07f,cy+.07f,cx+dir*.05f,cy,.018f),Line(x,y,cx+dir*.05f,cy,cx-dir*.07f,cy-.07f,.018f));
        static float Cross(float x,float y,float cx,float cy) => Mathf.Min(Line(x,y,cx-.06f,cy-.06f,cx+.06f,cy+.06f,.017f),Line(x,y,cx-.06f,cy+.06f,cx+.06f,cy-.06f,.017f));
        static float Ellipse(float x,float y,float cx,float cy,float rx,float ry) => (new Vector2((x-cx)/rx,(y-cy)/ry).magnitude-1)*Mathf.Min(rx,ry);
        static void Save(string name,Func<float,float,float> distance)
        {
            const int size=256;
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float d=distance((x+.5f)/size-.5f,(y+.5f)/size-.5f);
                float core=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.002f,.003f,d));
                float glow=Mathf.Exp(-Mathf.Max(0,d)*120)*.27f;
                pixels[y*size+x]=new Color(.18f+core*.24f,1,.48f+core*.15f,Mathf.Max(core,glow));
            }
            tex.SetPixels(pixels);tex.Apply();
            string path=Folder+"/j_"+name+".png";
            File.WriteAllBytes(path,tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=256;
            importer.alphaIsTransparency=true;
            importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
