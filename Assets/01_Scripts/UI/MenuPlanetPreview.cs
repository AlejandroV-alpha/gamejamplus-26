using UnityEngine;
using UnityEngine.UI;

namespace GameJam.UI
{
    // A real mesh, isolated camera and RenderTexture keep the miniature independent of the lobby.
    public sealed class MenuPlanetPreview : MonoBehaviour
    {
        private GameObject stage;
        private Transform planet, moon;
        private Camera previewCamera;
        private RenderTexture target;
        private Material surface, satelliteMaterial, ringMaterial;
        private Texture2D terrain;
        private CanvasGroup visibility;
        private float angle;

        private void Start()
        {
            visibility = GetComponentInParent<CanvasGroup>();
            stage = new GameObject("MenuPlanetStage");
            stage.transform.position = new Vector3(20000, 20000, 20000);
            const int previewLayer = 31;
            target = new RenderTexture(384, 384, 24) { name = "MenuMarsPreview", antiAliasing = 2 };
            var image = gameObject.AddComponent<RawImage>();
            image.texture = target;
            image.raycastTarget = false;
            var cameraObject = new GameObject("PlanetCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -5);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 1.65f;
            previewCamera.nearClipPlane = .1f;
            previewCamera.farClipPlane = 12;
            previewCamera.cullingMask = 1 << previewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.035f, .055f, .075f, 1);
            previewCamera.targetTexture = target;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            surface = new Material(shader);
            terrain = new Texture2D(512, 256, TextureFormat.RGBA32, true);
            terrain.wrapModeU = TextureWrapMode.Repeat;
            terrain.wrapModeV = TextureWrapMode.Clamp;
            var pixels = new Color[512 * 256];
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 512; x++)
                {
                    float longitude = x / 512f * Mathf.PI * 2;
                    float latitude = y / 255f * Mathf.PI;
                    // Spherical sampling avoids a visible seam as the globe rotates.
                    float u = Mathf.Cos(longitude) * Mathf.Sin(latitude);
                    float v = Mathf.Sin(longitude) * Mathf.Sin(latitude);
                    float h = Mathf.Cos(latitude);
                    float noise = Mathf.PerlinNoise(u * 3 + h * 2 + 10, v * 3 + h * 2 + 10);
                    float detail = Mathf.PerlinNoise(u * 19 + 30, v * 19 + h * 9 + 30);
                    Color color = Color.Lerp(new Color(.19f, .065f, .035f), new Color(.8f, .37f, .15f), noise);
                    color *= .8f + detail * .3f;
                    float ice = Mathf.SmoothStep(0, 1, (Mathf.Abs(h) - .96f) / .035f);
                    pixels[y * 512 + x] = Color.Lerp(color, new Color(.8f, .78f, .68f), ice);
                }
            terrain.SetPixels(pixels);
            terrain.Apply(true, true);
            surface.mainTexture = terrain;
            if (surface.HasProperty("_Smoothness")) surface.SetFloat("_Smoothness", .12f);
            planet = MakeSphere("Mars", Vector3.zero, 2f, surface, previewLayer);
            planet.localRotation = Quaternion.Euler(12, 0, -18);

            satelliteMaterial = new Material(shader);
            satelliteMaterial.color = new Color(.32f, .29f, .24f);
            moon = MakeSphere("Moon", new Vector3(1.28f, .3f, 0), .24f, satelliteMaterial, previewLayer);
            var lightObject = new GameObject("PreviewKeyLight");
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(25, -35, 0);
            var key = lightObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.8f;
            key.color = new Color(1, .87f, .7f);
            key.cullingMask = 1 << previewLayer;
            key.shadows = LightShadows.None;

            ringMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            ringMaterial.color = new Color(.19f, .49f, .6f);
            var orbitObject = new GameObject("OrbitalPath");
            orbitObject.layer = previewLayer;
            orbitObject.transform.SetParent(stage.transform, false);
            orbitObject.transform.localRotation = Quaternion.Euler(62, 15, -18);
            var line = orbitObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 128;
            line.widthMultiplier = .009f;
            line.sharedMaterial = ringMaterial;
            for (int i = 0; i < 128; i++)
            {
                float a = i * Mathf.PI * 2 / 128;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * 1.38f, Mathf.Sin(a) * 1.38f, 0));
            }
        }

        private Transform MakeSphere(string label, Vector3 position, float scale, Material material, int layer)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = label;
            sphere.layer = layer;
            sphere.transform.SetParent(stage.transform, false);
            sphere.transform.localPosition = position;
            sphere.transform.localScale = Vector3.one * scale;
            Destroy(sphere.GetComponent<Collider>());
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            return sphere.transform;
        }

        private void Update()
        {
            if (previewCamera == null) return;
            bool visible = visibility == null || visibility.alpha > .01f;
            previewCamera.enabled = visible;
            if (!visible) return;
            planet.Rotate(Vector3.up, 12f * Time.unscaledDeltaTime, Space.Self);
            angle += Time.unscaledDeltaTime * .3f;
            moon.localPosition = new Vector3(Mathf.Cos(angle) * 1.38f, Mathf.Sin(angle) * .45f, Mathf.Sin(angle) * .8f);
        }

        private void OnDisable() { if (previewCamera != null) previewCamera.enabled = false; }
        private void OnDestroy()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (stage != null) Destroy(stage);
            if (target != null) { target.Release(); Destroy(target); }
            if (surface != null) Destroy(surface);
            if (satelliteMaterial != null) Destroy(satelliteMaterial);
            if (ringMaterial != null) Destroy(ringMaterial);
            if (terrain != null) Destroy(terrain);
        }
    }
}
