#if UNITY_EDITOR
using System.Collections.Generic;
using Fusion;
using UnityEditor;
using UnityEngine;

namespace GameJam.Grinder.EditorTools
{
    /// <summary>
    /// Genera el modelo low-poly de la trituradora (crema, verde salvia, naranja) con mallas facetadas,
    /// y los prefabs de la trituradora y de los restos de monstruo. Menu: Herramientas > Trituradora.
    /// Frente (puerta y luces) hacia -Z, tubo y bandeja hacia +X, motor detras (+Z), rejillas en -X.
    /// </summary>
    public static class MonsterGrinderBuilder
    {
        const string Folder = "Assets/02_Prefabs/Interactables/Grinder";
        const string PrefabPath = Folder + "/MonsterGrinder.prefab";
        const string MeshAsset = Folder + "/MonsterGrinderMeshes.asset";
        const string RemainsPath = "Assets/Resources/Grinder/MonsterRemains.prefab";

        static readonly List<Mesh> meshes = new List<Mesh>();
        static Material cream, creamLight, green, orange, olive, oliveDark, hose, metal, ledO, ledY, ledG, lightBar;

        [MenuItem("Herramientas/Trituradora/Reconstruir prefabs")]
        public static void Build()
        {
            EnsureFolder(Folder);
            EnsureFolder("Assets/Resources/Grinder");
            meshes.Clear();
            MakeMaterials();

            var root = new GameObject("MonsterGrinder");
            var model = Child("Model", root.transform);
            var legs = Child("Legs", root.transform);
            var colliders = Child("Colliders", root.transform);
            var net = Child("Network", root.transform);

            // ---- patas: troncos de piramide octogonales, mas anchos arriba
            foreach (var p in new[] { new Vector2(-.92f, -.62f), new Vector2(.92f, -.62f), new Vector2(-.92f, .62f), new Vector2(.92f, .62f) })
                Part("Leg", Prism(Oct(.34f, .34f, .09f), 0f, .4f, 1.25f), creamLight, legs, new Vector3(p.x, 0, p.y));

            // ---- cuerpo
            // Cuerpo hueco: base maciza y cuatro paredes, para que la boca deje ver los rodillos.
            Part("CoreBase", Prism(Rect(2.0f, 1.5f), .36f, 1.0f), cream, model, Vector3.zero);
            Part("WallFront", Prism(Rect(2.0f, .27f), 1.0f, 1.84f), cream, model, new Vector3(0, 0, -.615f));
            Part("WallBack", Prism(Rect(2.0f, .27f), 1.0f, 1.84f), cream, model, new Vector3(0, 0, .615f));
            Part("WallLeft", Prism(Rect(.28f, .96f), 1.0f, 1.84f), cream, model, new Vector3(-.86f, 0, 0));
            Part("WallRight", Prism(Rect(.28f, .96f), 1.0f, 1.84f), cream, model, new Vector3(.86f, 0, 0));
            // columnas verdes biseladas en las esquinas
            foreach (var p in new[] { new Vector2(-1f, -.74f), new Vector2(1f, -.74f), new Vector2(-1f, .74f), new Vector2(1f, .74f) })
                Part("Column", Prism(Oct(.5f, .5f, .14f), .32f, 1.9f), green, model, new Vector3(p.x, 0, p.y));
            // marco superior crema alrededor de la boca
            Part("RimFront", Prism(Rect(1.7f, .28f), 1.84f, 1.98f), cream, model, new Vector3(0, 0, .62f));
            Part("RimBack", Prism(Rect(1.7f, .28f), 1.84f, 1.98f), cream, model, new Vector3(0, 0, -.62f));
            Part("RimLeft", Prism(Rect(.26f, 1.0f), 1.84f, 1.98f), cream, model, new Vector3(-.82f, 0, 0));
            Part("RimRight", Prism(Rect(.26f, 1.0f), 1.84f, 1.98f), cream, model, new Vector3(.82f, 0, 0));
            // tapas naranjas en las esquinas del marco
            foreach (var p in new[] { new Vector2(-.82f, -.6f), new Vector2(.82f, -.6f), new Vector2(-.82f, .6f), new Vector2(.82f, .6f) })
                Part("CornerCap", Prism(Oct(.36f, .3f, .08f), 1.97f, 2.05f, .85f), orange, model, new Vector3(p.x, 0, p.y));

            // ---- tolva interior (paredes inclinadas oliva) y fondo
            Part("Hopper", Funnel(1.44f, .96f, 1.98f, .9f, .5f, 1.02f), olive, model, Vector3.zero);
            Part("HopperFloor", Prism(Rect(.95f, .55f), .98f, 1.02f), oliveDark, model, Vector3.zero);

            // ---- rodillos dentados (giran sobre X)
            var rollerA = Roller("RollerA", model, new Vector3(0, 1.28f, .13f));
            var rollerB = Roller("RollerB", model, new Vector3(0, 1.28f, -.13f));

            // ---- frente: puerta, bisagras, luces y barra blanca
            var front = Child("Front", model);
            Part("DoorFrame", PrismZ(Oct(1.42f, 1.0f, .14f), .75f, .79f), creamLight, front, new Vector3(-.05f, 1.08f, 0));
            Part("DoorPanel", PrismZ(Oct(1.3f, .88f, .12f), .78f, .82f), cream, front, new Vector3(-.05f, 1.08f, 0));
            foreach (float y in new[] { .82f, 1.34f })
            {
                Part("Hinge", PrismZ(Oct(.26f, .13f, .03f), .8f, .87f), green, front, new Vector3(-.62f, y, 0));
                Part("HingeBolt", PrismZ(Oct(.06f, .06f, .015f), .86f, .89f), oliveDark, front, new Vector3(-.55f, y, 0));
            }
            Part("LedHousing", PrismZ(Oct(.2f, .44f, .04f), .8f, .86f), oliveDark, front, new Vector3(.45f, 1.06f, 0));
            var leds = new List<Renderer>();
            float[] ys = { 1.19f, 1.06f, .93f };
            Material[] lm = { ledO, ledY, ledG };
            for (int i = 0; i < 3; i++)
                leds.Add(Part("Led" + i, PrismZ(Rect(.13f, .07f), .85f, .875f), lm[i], front, new Vector3(.45f, ys[i], 0)).GetComponent<Renderer>());
            var bar = Part("LightBar", PrismZ(Oct(.5f, .07f, .02f), .79f, .815f), lightBar, front, new Vector3(-.05f, 1.7f, 0)).GetComponent<Renderer>();

            // ---- lado izquierdo: rejillas
            var left = Child("LeftVents", model);
            Part("VentFrame", PrismX(Rect(.95f, .9f), -1.27f, -1.24f), oliveDark, left, new Vector3(0, 1.1f, 0));
            for (int i = 0; i < 6; i++)
                Part("Slat", PrismX(Oct(.85f, .07f, .02f), -1.31f, -1.25f), olive, left, new Vector3(0, .8f + i * .12f, 0));

            // ---- detras: motor verde con ranuras, rejilla y manguera
            var back = Child("Back", model);
            Part("Motor", Prism(Oct(.95f, .55f, .16f), 1.75f, 2.32f, .92f), green, back, new Vector3(-.25f, 0, -.72f));
            for (int i = 0; i < 3; i++)
                Part("MotorSlot", Prism(Rect(.12f, .3f), 2.3f, 2.335f), oliveDark, back, new Vector3(-.45f + i * .2f, 0, -.72f));
            Part("BackVent", PrismZ(Rect(.8f, .5f), -.79f, -.76f), oliveDark, back, new Vector3(-.25f, 1.05f, 0), true);
            for (int i = 0; i < 4; i++)
                Part("BackSlat", PrismZ(Oct(.72f, .07f, .02f), -.83f, -.77f), olive, back, new Vector3(-.25f, .88f + i * .11f, 0), true);
            Part("HosePort", PrismZ(Oct(.2f, .2f, .05f), -.8f, -.74f), green, back, new Vector3(.45f, .95f, 0), true);
            Part("Hose", Tube(new[]
            {
                new Vector3(.2f, 2.05f, -.72f), new Vector3(.5f, 2.08f, -.72f), new Vector3(.62f, 1.9f, -.82f),
                new Vector3(.6f, 1.45f, -.86f), new Vector3(.52f, 1.1f, -.84f), new Vector3(.45f, .98f, -.8f),
            }, .055f, 10), hose, back, Vector3.zero);
            Part("HoseClamp", PrismZ(Oct(.16f, .1f, .03f), -.89f, -.83f), creamLight, back, new Vector3(.6f, 1.55f, 0), true);

            // ---- lado derecho: brida, tubo en codo y bandeja naranja
            var chute = Child("Chute", model);
            Part("Flange", PrismX(Oct(.42f, .42f, .08f), 1.24f, 1.29f), green, chute, new Vector3(0, 1.45f, 0));
            Part("PipeOut", PrismX(Oct(.24f, .24f, .06f), 1.27f, 1.55f), creamLight, chute, new Vector3(0, 1.45f, 0));
            Part("PipeElbow", Prism(Oct(.26f, .26f, .07f), 1.3f, 1.58f), creamLight, chute, new Vector3(1.62f, 0, 0));
            Part("PipeDown", Prism(Oct(.24f, .24f, .06f), .98f, 1.32f), creamLight, chute, new Vector3(1.62f, 0, 0));
            Part("PipeLip", Prism(Oct(.3f, .3f, .08f), .94f, 1.0f), cream, chute, new Vector3(1.62f, 0, 0));
            var tray = Child("Tray", chute);
            tray.localPosition = new Vector3(1.66f, 0, 0);
            Part("TrayFloor", Prism(Oct(.8f, .9f, .1f), .5f, .58f), orange, tray, Vector3.zero);
            Part("TrayWallFront", Prism(Rect(.8f, .07f), .5f, .78f), orange, tray, new Vector3(0, 0, .42f));
            Part("TrayWallBack", Prism(Rect(.8f, .07f), .5f, .78f), orange, tray, new Vector3(0, 0, -.42f));
            Part("TrayWallOut", Prism(Rect(.07f, .9f), .5f, .78f), orange, tray, new Vector3(.37f, 0, 0));
            Part("TrayArm", Prism(Rect(.35f, .2f), .52f, .6f), green, tray, new Vector3(-.45f, 0, 0));

            // El frente mira a -Z (asi, visto de frente, la bandeja queda a la derecha como en el concepto).
            front.localScale = new Vector3(1, 1, -1);
            back.localScale = new Vector3(1, 1, -1);

            // ---- colisiones (no se sacuden) y puntos de referencia
            var body = colliders.gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0, 1.15f, 0); body.size = new Vector3(2.5f, 1.65f, 1.98f);
            var trayCol = Child("TrayCollider", colliders).gameObject.AddComponent<BoxCollider>();
            trayCol.center = new Vector3(1.66f, .54f, 0); trayCol.size = new Vector3(.8f, .08f, .9f);
            var legCol = Child("LegsCollider", colliders).gameObject.AddComponent<BoxCollider>();
            legCol.center = new Vector3(0, .2f, 0); legCol.size = new Vector3(2.1f, .4f, 1.5f);

            var mouth = Point("Mouth", root.transform, new Vector3(0, 1.75f, 0));
            var traySpot = Point("TraySpot", root.transform, new Vector3(1.66f, .6f, 0));
            var vent = Point("Vent", root.transform, new Vector3(-.25f, 2.35f, .72f));
            var path = new[]
            {
                Point("Chute0", root.transform, new Vector3(.6f, 1.45f, 0)),
                Point("Chute1", root.transform, new Vector3(1.6f, 1.45f, 0)),
                Point("Chute2", root.transform, new Vector3(1.62f, 1.0f, 0)),
                Point("Chute3", root.transform, new Vector3(1.66f, .65f, 0)),
            };

            // ---- red y logica
            var no = net.gameObject.AddComponent<NetworkObject>();
            var grinder = net.gameObject.AddComponent<MonsterGrinder>();
            var so = new SerializedObject(grinder);
            so.FindProperty("mouth").objectReferenceValue = mouth;
            so.FindProperty("rollerA").objectReferenceValue = rollerA;
            so.FindProperty("rollerB").objectReferenceValue = rollerB;
            so.FindProperty("shakeRoot").objectReferenceValue = model;
            so.FindProperty("traySpot").objectReferenceValue = traySpot;
            so.FindProperty("ventPoint").objectReferenceValue = vent;
            so.FindProperty("lightBar").objectReferenceValue = bar;
            SetArray(so.FindProperty("chutePath"), path);
            SetArray(so.FindProperty("leds"), leds.ToArray());
            var minerals = new List<NetworkObject>();
            foreach (var name in new[] { "RedMineralPickup", "EnergyCrystalPickup", "IceCrystalPickup", "MetalPickup" })
            {
                var m = AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/02_Prefabs/Interactables/Materials/" + name + ".prefab");
                if (m != null) minerals.Add(m);
            }
            SetArray(so.FindProperty("mineralPrefabs"), minerals.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveMeshes();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // ---- restos arrastrables
            var remains = new GameObject("MonsterRemains");
            var rno = remains.AddComponent<NetworkObject>();
            rno.Flags |= NetworkObjectFlags.AllowStateAuthorityOverride;
            remains.AddComponent<MonsterRemains>();
            PrefabUtility.SaveAsPrefabAsset(remains, RemainsPath);
            Object.DestroyImmediate(remains);

            AssetDatabase.SaveAssets();
            Debug.Log("[Trituradora] Prefabs listos: " + PrefabPath + " y " + RemainsPath + " (minerales: " + minerals.Count + ")");
        }

        // ================================================================== piezas

        static Transform Child(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Transform Point(string name, Transform parent, Vector3 pos)
        {
            var t = Child(name, parent);
            t.localPosition = pos;
            return t;
        }

        static GameObject Part(string name, Mesh mesh, Material mat, Transform parent, Vector3 pos, bool flipZ = false)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Transform Roller(string name, Transform parent, Vector3 pos)
        {
            var pivot = Child(name, parent);
            pivot.localPosition = pos;
            var gear = Star(12, .2f, .15f);
            Part("Core", PrismX(Oct(.2f, .2f, .06f), -.6f, .6f), oliveDark, pivot, Vector3.zero);
            for (int i = 0; i < 5; i++)
            {
                float x = -.5f + i * .25f;
                Part("Teeth", PrismX(gear, x - .07f, x + .07f), metal, pivot, Vector3.zero)
                    .transform.localRotation = Quaternion.Euler(i * 15f, 0, 0);
            }
            return pivot;
        }

        static void SetArray(SerializedProperty prop, Object[] items)
        {
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // ================================================================== mallas

        static Vector2[] Rect(float w, float d) => new[] { new Vector2(-w / 2, -d / 2), new Vector2(w / 2, -d / 2), new Vector2(w / 2, d / 2), new Vector2(-w / 2, d / 2) };

        // Rectangulo con esquinas biseladas (octogono).
        static Vector2[] Oct(float w, float d, float c)
        {
            float x = w / 2, z = d / 2;
            c = Mathf.Min(c, x * .9f, z * .9f);
            return new[]
            {
                new Vector2(-x + c, -z), new Vector2(x - c, -z), new Vector2(x, -z + c), new Vector2(x, z - c),
                new Vector2(x - c, z), new Vector2(-x + c, z), new Vector2(-x, z - c), new Vector2(-x, -z + c),
            };
        }

        static Vector2[] Star(int teeth, float rOut, float rIn)
        {
            var p = new List<Vector2>();
            for (int i = 0; i < teeth; i++)
            {
                float a0 = i * Mathf.PI * 2 / teeth, step = Mathf.PI * 2 / teeth;
                p.Add(Polar(rIn, a0));
                p.Add(Polar(rOut, a0 + step * .2f));
                p.Add(Polar(rOut, a0 + step * .5f));
                p.Add(Polar(rIn, a0 + step * .7f));
            }
            return p.ToArray();
        }

        static Vector2 Polar(float r, float a) => new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);

        // Prisma vertical (Y) de caras planas; topScale estrecha o ensancha la tapa superior.
        static Mesh Prism(Vector2[] poly, float y0, float y1, float topScale = 1f)
            => PrismAxis(poly, y0, y1, topScale, v => v);

        // Prisma a lo largo de X: poly en (z, y) local... se usa (y, z) del poligono.
        static Mesh PrismX(Vector2[] poly, float x0, float x1)
            => PrismAxis(poly, x0, x1, 1f, v => new Vector3(v.y, v.z, v.x));

        // Prisma a lo largo de Z (para caras frontales/traseras): poly en (x, y).
        static Mesh PrismZ(Vector2[] poly, float z0, float z1)
            => Flip(PrismAxis(poly, z0, z1, 1f, v => new Vector3(v.x, v.z, v.y)));   // este cambio de ejes invierte las caras

        static Mesh Flip(Mesh m)
        {
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
            m.triangles = t; m.RecalculateNormals();
            return m;
        }

        static Mesh PrismAxis(Vector2[] poly, float h0, float h1, float topScale, System.Func<Vector3, Vector3> map)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            int n = poly.Length;
            Vector3 B(int i) => map(new Vector3(poly[i].x, h0, poly[i].y));
            Vector3 T(int i) => map(new Vector3(poly[i].x * topScale, h1, poly[i].y * topScale));
            Vector2 c = Vector2.zero; foreach (var p in poly) c += p; c /= n;
            Vector3 cb = map(new Vector3(c.x, h0, c.y)), ct = map(new Vector3(c.x * topScale, h1, c.y * topScale));
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Tri(v, t, ct, T(j), T(i));
                Tri(v, t, cb, B(i), B(j));
                Quad(v, t, B(i), T(i), T(j), B(j));
            }
            return Finish(v, t);
        }

        // Paredes inclinadas de la tolva, con las caras hacia dentro.
        static Mesh Funnel(float wTop, float dTop, float yTop, float wBot, float dBot, float yBot)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            Vector3[] top = { new Vector3(-wTop / 2, yTop, -dTop / 2), new Vector3(wTop / 2, yTop, -dTop / 2), new Vector3(wTop / 2, yTop, dTop / 2), new Vector3(-wTop / 2, yTop, dTop / 2) };
            Vector3[] bot = { new Vector3(-wBot / 2, yBot, -dBot / 2), new Vector3(wBot / 2, yBot, -dBot / 2), new Vector3(wBot / 2, yBot, dBot / 2), new Vector3(-wBot / 2, yBot, dBot / 2) };
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Quad(v, t, bot[i], bot[j], top[j], top[i]);
            }
            return Finish(v, t);
        }

        static Mesh Tube(Vector3[] pts, float r, int sides)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            var rings = new List<Vector3[]>();
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 fwd = (pts[Mathf.Min(i + 1, pts.Length - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(fwd, Vector3.up).sqrMagnitude > .01f ? Vector3.Cross(fwd, Vector3.up).normalized : Vector3.right;
                Vector3 up = Vector3.Cross(side, fwd);
                var ring = new Vector3[sides];
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2 / sides;
                    ring[s] = pts[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * r;
                }
                rings.Add(ring);
            }
            for (int i = 0; i < rings.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int k = (s + 1) % sides;
                {
                    // Ambas caras: la manguera se ve bien desde cualquier lado.
                    Quad(v, t, rings[i][s], rings[i + 1][s], rings[i + 1][k], rings[i][k]);
                    Quad(v, t, rings[i][k], rings[i + 1][k], rings[i + 1][s], rings[i][s]);
                }
                }
            return Finish(v, t);
        }

        static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            t.Add(v.Count); v.Add(a); t.Add(v.Count); v.Add(b); t.Add(v.Count); v.Add(c);
        }

        static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Tri(v, t, a, b, c); Tri(v, t, a, c, d);
        }

        static Mesh Finish(List<Vector3> v, List<int> t)
        {
            var m = new Mesh { name = "Grinder_" + meshes.Count };
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            meshes.Add(m);
            return m;
        }

        static void SaveMeshes()
        {
            var old = AssetDatabase.LoadAssetAtPath<Object>(MeshAsset);
            if (old != null) AssetDatabase.DeleteAsset(MeshAsset);
            var holder = ScriptableObject.CreateInstance<GrinderMeshBundle>();
            AssetDatabase.CreateAsset(holder, MeshAsset);
            foreach (var m in meshes) AssetDatabase.AddObjectToAsset(m, holder);
            AssetDatabase.SaveAssets();
        }

        // ================================================================== materiales

        static void MakeMaterials()
        {
            cream = Mat("Cream", new Color(.91f, .84f, .74f));
            creamLight = Mat("CreamLight", new Color(.97f, .92f, .82f));
            green = Mat("SageGreen", new Color(.37f, .53f, .42f));
            orange = Mat("Orange", new Color(.96f, .66f, .2f));
            olive = Mat("Olive", new Color(.45f, .48f, .33f));
            oliveDark = Mat("OliveDark", new Color(.27f, .3f, .21f));
            hose = Mat("Hose", new Color(.26f, .27f, .26f), .45f);
            metal = Mat("GearMetal", new Color(.36f, .37f, .35f), .55f, .6f);
            ledO = Mat("LedOrange", new Color(1f, .5f, .15f), .6f, 0f, true);
            ledY = Mat("LedYellow", new Color(1f, .82f, .2f), .6f, 0f, true);
            ledG = Mat("LedGreen", new Color(.55f, 1f, .3f), .6f, 0f, true);
            lightBar = Mat("LightBar", new Color(.95f, .98f, 1f), .6f, 0f, true);
        }

        static Material Mat(string name, Color c, float smooth = .25f, float metallic = 0f, bool emissive = false)
        {
            string path = Folder + "/M_Grinder_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            if (emissive) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.5f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
#endif
