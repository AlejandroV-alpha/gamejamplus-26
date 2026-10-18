using UnityEngine;

public sealed partial class b_NeptuneDiamondRain
{
    [Header("Diamond prefab rain")]
    public GameObject[] diamondPrefabs;
    [Range(2, 160)] public int maximumPrefabDiamonds = 128;
    [Range(1, 90)] public float prefabDiamondsPerSecond = 48;
    public Vector2 prefabDiamondSize = new Vector2(.18f, .36f);

    sealed class PrefabDrop
    {
        public GameObject root;
        public Light[] lights;
        public Color color;
        public Vector3 velocity, spin;
        public float age;
    }

    PrefabDrop[] prefabDrops;
    GameObject prefabPoolRoot;
    float spawnCredit;
    int nextDrop;
    Vector3 previousRainCamera, rainCameraVelocity;

    void PreparePrefabRain()
    {
        if (diamondPrefabs == null || diamondPrefabs.Length == 0) return;
        foreach (var prefab in diamondPrefabs) if (prefab == null) return;
        prefabPoolRoot = new GameObject("Diamond Prefabs - Reusable Local Pool");
        prefabPoolRoot.transform.SetParent(transform, false);
        prefabPoolRoot.SetActive(false);
        prefabDrops = new PrefabDrop[Mathf.Clamp(maximumPrefabDiamonds, 2, 160)];
        for (int i = 0; i < prefabDrops.Length; i++)
        {
            var container = new GameObject("Falling Diamond " + (i+1));
            container.layer = 2;
            container.transform.SetParent(prefabPoolRoot.transform, false);
            var visual = Instantiate(diamondPrefabs[i % diamondPrefabs.Length], container.transform);
            visual.transform.localPosition = Vector3.zero;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) {body.isKinematic = true;body.detectCollisions = false;}
            bool found = false;
            var bounds = new Bounds();
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (!found) {bounds = renderer.bounds;found = true;} else bounds.Encapsulate(renderer.bounds);
            }
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, .001f);
            visual.transform.localScale /= size;
            visual.transform.localPosition -= container.transform.InverseTransformVector(bounds.center-container.transform.position)/size;
            var lights = visual.GetComponentsInChildren<Light>(true);
            var color = lights.Length > 0 ? lights[0].color : Color.white;
            foreach (var light in lights) {light.enabled = true;light.shadows = LightShadows.None;}
            prefabDrops[i] = new PrefabDrop {root=container, lights=lights, color=color};
            container.SetActive(false);
        }
        spawnCredit = 0;
        nextDrop = 0;
        previousRainCamera = viewCamera != null ? viewCamera.transform.position : Vector3.zero;
        rainCameraVelocity = Vector3.zero;
        prefabPoolRoot.SetActive(true);
    }

    void UpdatePrefabRain(float envelope, float deltaTime)
    {
        if (prefabDrops == null) return;
        if (viewCamera == null) {ClearPrefabRain();return;}
        var cameraPosition = viewCamera.transform.position;
        if (deltaTime > .0001f)
        {
            var movement = cameraPosition-previousRainCamera;
            movement.y = 0;
            rainCameraVelocity = Vector3.Lerp(rainCameraVelocity,Vector3.ClampMagnitude(movement/deltaTime,15),Mathf.Clamp01(deltaTime*8));
        }
        previousRainCamera = cameraPosition;
        // Cover where the player will be when diamonds reach ground level.
        var ahead = Vector3.ProjectOnPlane(viewCamera.transform.forward,Vector3.up).normalized*6;
        var spawnCenter = cameraPosition+rainCameraVelocity*2.5f+ahead;
        spawnCredit = envelope > 0 ? Mathf.Min(spawnCredit + Mathf.Min(diamondsPerSecond, prefabDiamondsPerSecond)*envelope*deltaTime, 12) : 0;
        while (spawnCredit >= 1)
        {
            spawnCredit--;
            for (int attempt=0; attempt<prefabDrops.Length; attempt++)
            {
                var drop = prefabDrops[nextDrop];
                nextDrop = (nextDrop+1)%prefabDrops.Length;
                if (drop.root.activeSelf) continue;
                var spawn = spawnCenter + new Vector3(Random.Range(-22f,22f),Random.Range(14f,20f),Random.Range(-22f,22f));
                // Start above any roof, so rain cannot originate inside an ice cave.
                if (Physics.Raycast(new Vector3(spawn.x,cameraPosition.y+200,spawn.z),Vector3.down,out var roof,200,1,QueryTriggerInteraction.Ignore))
                    spawn.y = Mathf.Max(spawn.y,roof.point.y+8);
                drop.root.transform.SetPositionAndRotation(spawn,Random.rotation);
                drop.root.transform.localScale = Vector3.one*Random.Range(Mathf.Max(.05f,prefabDiamondSize.x),Mathf.Max(.05f,prefabDiamondSize.y));
                drop.velocity = new Vector3(Random.Range(1f,3f),Random.Range(-10f,-7f),Random.Range(-1.5f,.5f));
                drop.spin = Random.insideUnitSphere*160;
                drop.age = 0;
                drop.root.SetActive(true);
                break;
            }
        }
        foreach (var drop in prefabDrops)
        {
            if (!drop.root.activeSelf) continue;
            var previous = drop.root.transform.position;
            var step = drop.velocity*deltaTime;
            if (Physics.Raycast(previous,step.normalized,out var hit,step.magnitude+.08f,1,QueryTriggerInteraction.Ignore))
            {
                EmitImpact(hit.point,hit.normal,new Color(drop.color.r,drop.color.g,drop.color.b,.55f));
                drop.root.SetActive(false);
                continue;
            }
            drop.root.transform.position += step;
            drop.root.transform.Rotate(drop.spin*deltaTime,Space.Self);
            drop.age += deltaTime;
            if (drop.age > 8 || (drop.root.transform.position-cameraPosition).sqrMagnitude>220*220) drop.root.SetActive(false);
        }
    }

    void ClearPrefabRain()
    {
        spawnCredit = 0;
        if (prefabDrops == null) return;
        foreach (var drop in prefabDrops) drop.root.SetActive(false);
    }

    void DisposePrefabRain()
    {
        if (prefabPoolRoot != null) Remove(prefabPoolRoot);
        prefabPoolRoot = null;
        prefabDrops = null;
    }
}
