using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GameJam.Mars
{
    public sealed class b_AlienNPC : MonoBehaviour
    {
        public GameObject model;
        public Texture2D baseTexture;
        public AnimationClip walking, running, talking, drinking;
        public Vector3[] routeOffsets = { Vector3.zero, new Vector3(6,0,0), new Vector3(6,0,6), new Vector3(0,0,6) };
        public float speed = 0.8f;
        public float pauseSeconds = 5f;
        public float characterHeight = 1.7f;
        public float headingOffset;
        private Vector3 origin;
        private Transform visual;
        private float bottomOffset, pause;
        private int point, action, previousAction = -1;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[4];
        private readonly AnimationClip[] sources = new AnimationClip[4];
        private Material skin;

        [SerializeField] private float playerAvoidanceRadius = 2f;
        [SerializeField] private float playerAvoidanceStrength = 2.5f;

        private readonly Collider[] nearbyColliders = new Collider[32];
        private Transform avoidedPlayer;
        private float avoidanceSide = 1f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                ResolveAnimations();
                UnityEditor.EditorUtility.SetDirty(this);
                if (!Application.isPlaying && gameObject.scene.IsValid() && gameObject.scene.isLoaded)
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            };
        }
        private void ResolveAnimations()
        {
            const string folder = "Assets/06Bc_Models/AlienNpc/";
            string[] names = { "Walking", "Running", "Talk_with_Right_Hand_Open", "Stand_and_Drink" };
            AnimationClip[] loaded = new AnimationClip[4];
            for (int i = 0; i < names.Length; i++)
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(folder + "Meshy_AI_Jawa_biped_Animation_" + names[i] + "_withSkin.fbx"))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) { loaded[i] = clip; break; }
            walking = loaded[0]; running = loaded[1]; talking = loaded[2]; drinking = loaded[3];
        }
#endif
        private void Start()
        {
#if UNITY_EDITOR
            ResolveAnimations();
#endif
            if (model == null || walking == null || talking == null || drinking == null || routeOffsets.Length < 2)
            {
                Debug.LogWarning("b_AlienNPC: faltan modelo, clips o puntos de ruta.", this);
                enabled = false;
                return;
            }
            origin = transform.position;
            visual = Instantiate(model, transform).transform;
            visual.name = "b_Alien_Visual";
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            foreach (var child in visual.GetComponentsInChildren<Transform>(true))
                if (child.name == "Icosphere")
                    foreach (var renderer in child.GetComponents<Renderer>()) renderer.enabled = false;
            bool found = false;
            Bounds bounds = default;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (found && bounds.size.y > 0.001f)
            {
                visual.localScale *= characterHeight / bounds.size.y;
                bottomOffset = (transform.position.y - bounds.min.y) * characterHeight / bounds.size.y;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null && baseTexture != null)
            {
                skin = new Material(shader) { name = "b_Alien_Piel" };
                skin.SetTexture("_BaseMap", baseTexture);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled) renderer.sharedMaterial = skin;
            }
            var animator = visual.GetComponentInChildren<Animator>();
            if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            graph = PlayableGraph.Create("b_Alien_Animaciones");
            mixer = AnimationMixerPlayable.Create(graph, 4);
            var output = AnimationPlayableOutput.Create(graph, "b_Alien", animator);
            output.SetSourcePlayable(mixer);
            sources[0]=walking; sources[1]=running; sources[2]=talking; sources[3]=drinking;
            for (int i=0;i<4;i++)
            {
                clips[i]=AnimationClipPlayable.Create(graph, sources[i] != null ? sources[i] : walking);
                clips[i].SetApplyFootIK(false);
                graph.Connect(clips[i],0,mixer,i);
            }
            mixer.SetInputWeight(0,1f);
            graph.Play();
            point = 1;
        }
        private void Update()
        {
            if (!graph.IsValid()) return;
            if (pause > 0f) pause -= Time.deltaTime;
            else
            {
                Vector3 goal=origin+routeOffsets[point];
                Vector3 direction=Vector3.ProjectOnPlane(goal-transform.position,Vector3.up);
                if (direction.magnitude < 0.2f)
                {
                    point=(point+1)%routeOffsets.Length;
                    pause=pauseSeconds;
                    action=point%2==0 ? 2 : 3;
                }
                else
                {
                    action=point==3 && running!=null ? 1 : 0;
                    Vector3 movementDirection = GetPlayerAvoidanceDirection(
                        direction.normalized
                    );

                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(movementDirection) *
                            Quaternion.Euler(0, headingOffset, 0),
                        90f * Time.deltaTime
                    );

                    float travelSpeed = action == 1 ? speed * 1.7f : speed;

                    transform.position += movementDirection *
                        Mathf.Min(travelSpeed * Time.deltaTime, direction.magnitude);
                }
            }
            // Sample colliders instead of assuming the terrain is flat.
            float nearest=float.PositiveInfinity;
            Vector3 ground=transform.position;
            foreach(var hit in Physics.RaycastAll(transform.position+Vector3.up*10f,Vector3.down,30f,~0,QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.collider.GetComponentInParent<MarsFusionAvatar>() != null)
                {
                    continue;
                }

                if (hit.normal.y < 0.55f || hit.distance >= nearest)
                {
                    continue;
                }
                nearest =hit.distance; ground.y=hit.point.y+bottomOffset;
            }
            if(nearest<float.PositiveInfinity)transform.position=ground;
            if(action!=previousAction) { clips[action].SetTime(0); previousAction=action; }
            for(int i=0;i<4;i++)
            {
                mixer.SetInputWeight(i,Mathf.MoveTowards(mixer.GetInputWeight(i),i==action?1f:0f,Time.deltaTime*5f));
                var source=sources[i]!=null?sources[i]:walking;
                if(source.length>0f&&clips[i].GetTime()>=source.length)clips[i].SetTime(clips[i].GetTime()%source.length);
            }
        }
        private void OnEnable() { if(graph.IsValid())graph.Play(); }
        private void OnDisable() { if(graph.IsValid())graph.Stop(); }
        private void OnDestroy()
        {
            if(graph.IsValid())graph.Destroy();
            if(skin!=null)Destroy(skin);
        }
        private void OnDrawGizmosSelected()
        {
            if(routeOffsets==null||routeOffsets.Length<2)return;
            Vector3 center=Application.isPlaying?origin:transform.position;
            Gizmos.color=Color.green;
            for(int i=0;i<routeOffsets.Length;i++)
            {
                Gizmos.DrawWireSphere(center+routeOffsets[i],0.3f);
                Gizmos.DrawLine(center+routeOffsets[i],center+routeOffsets[(i+1)%routeOffsets.Length]);
            }
        }


        /// <summary>
        /// Desvia el movimiento para rodear a los jugadores cercanos.
        /// </summary>
        private Vector3 GetPlayerAvoidanceDirection(Vector3 desiredDirection)
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position + Vector3.up,
                playerAvoidanceRadius,
                nearbyColliders,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            MarsFusionAvatar nearestPlayer = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                MarsFusionAvatar player =
                    nearbyColliders[i].GetComponentInParent<MarsFusionAvatar>();

                if (player == null)
                {
                    continue;
                }

                Vector3 offset = player.transform.position - transform.position;
                offset.y = 0f;

                float distance = offset.magnitude;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPlayer = player;
                }
            }

            if (nearestPlayer == null)
            {
                avoidedPlayer = null;
                return desiredDirection;
            }

            Vector3 toPlayer = nearestPlayer.transform.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude < 0.001f)
            {
                return desiredDirection;
            }

            Vector3 right = Vector3.Cross(Vector3.up, desiredDirection).normalized;

            if (avoidedPlayer != nearestPlayer.transform)
            {
                avoidedPlayer = nearestPlayer.transform;

                if (Vector3.Dot(toPlayer, right) >= 0f)
                {
                    avoidanceSide = -1f;
                }
                else
                {
                    avoidanceSide = 1f;
                }
            }

            Vector3 away = -toPlayer.normalized;
            Vector3 sideways = right * avoidanceSide;

            float proximity = Mathf.Clamp01(
                (playerAvoidanceRadius - nearestDistance) / playerAvoidanceRadius
            );

            Vector3 steering = desiredDirection +
                (sideways * 2f + away) * proximity * playerAvoidanceStrength;

            return steering.normalized;
        }

    }
}
