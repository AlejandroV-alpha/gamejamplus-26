using Fusion;
using GameJam.Combat;
using GameJam.Multiplayer;
using GameJam.Survival;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.Grinder
{
    /// <summary>
    /// Cadaver de un monstruo que se puede arrastrar (clic izquierdo con las manos vacias) hasta la
    /// trituradora. Lo crea la autoridad del enemigo al morir. Lo que se ve es el propio cuerpo del
    /// monstruo convertido en ragdoll en cada cliente (CorpseRagdoll); este objeto de red solo lleva
    /// el punto de agarre, que la fisica sigue.
    /// Quien lo arrastra toma la autoridad del objeto, asi el arrastre responde sin retraso.
    /// </summary>
    public sealed class MonsterRemains : NetworkBehaviour
    {
        public const string PrefabPath = "Grinder/MonsterRemains";
        private const float GrabRange = 3f, ClaimRange = 10f, FallbackAfter = 3f, FeedFit = 1.35f, HandHeight = .8f;

        [Networked] public NetworkString<_32> Title { get; set; }
        [Networked] public int Value { get; set; }
        [Networked] public NetworkObject Carrier { get; set; }
        [Networked] public NetworkObject FedInto { get; set; }
        [Networked] public float FeedStart { get; set; }
        [Networked] private Vector3 SyncedPosition { get; set; }
        [Networked] private float SyncedYaw { get; set; }

        public bool IsFed => FedInto != null;
        public bool IsCarried => Carrier != null && Carrier.IsValid;
        public bool Ready => corpse != null || visual != null;
        /// <summary>Donde esta el cuerpo de verdad en este cliente.</summary>
        public Vector3 BodyPoint => corpse != null ? corpse.Center : transform.position;

        private CorpseRagdoll corpse;
        private Transform visual;      // contenedor animado al triturar (o masa generica)
        private Vector3 visualBaseScale = Vector3.one;
        private float feedSize = 1f;
        private float spawnedAt;
        private bool wantsGrab, grabbing;
        private static int inputFrame = -1;
        private static MonsterRemains promptTarget;

        // ------------------------------------------------------------------ creacion

        /// <summary>Lo llama la autoridad del enemigo en el momento de la muerte.</summary>
        public static void SpawnFor(NetworkRunner runner, EnemyCombatUI enemy, string title, float maxHealth)
        {
            if (runner == null || enemy == null) return;
            var prefab = Resources.Load<NetworkObject>(PrefabPath);
            if (prefab == null) { Debug.LogWarning("[Trituradora] Falta Resources/" + PrefabPath); return; }
            Vector3 position = enemy.transform.position;
            runner.Spawn(prefab, position, Quaternion.Euler(0, enemy.transform.eulerAngles.y, 0), null, (r, o) =>
            {
                var remains = o.GetComponent<MonsterRemains>();
                remains.Title = string.IsNullOrEmpty(title) ? "MONSTRUO" : title;
                // 2 minerales para los pequeños, hasta 8 para los grandes.
                remains.Value = Mathf.Clamp(Mathf.RoundToInt(maxHealth / 70f), 2, 8);
                remains.SyncedPosition = position;
                remains.SyncedYaw = enemy.transform.eulerAngles.y;
            });
        }

        public override void Spawned()
        {
            spawnedAt = Time.time;
            transform.position = SyncedPosition;
            transform.rotation = Quaternion.Euler(0, SyncedYaw, 0);
        }

        // ------------------------------------------------------------------ red

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (IsFed)
            {
                var grinder = FedInto.GetComponent<MonsterGrinder>();
                if (grinder != null) SyncedPosition = grinder.MouthPoint;
                if (Runner.SimulationTime - FeedStart >= MonsterGrinder.GrindSeconds) Runner.Despawn(Object);
                return;
            }

            if (Carrier != null && !Carrier.IsValid) Carrier = null;
            if (IsCarried)
            {
                var avatar = Carrier.GetComponent<MarsFusionAvatar>();
                if (avatar == null || !avatar.IsAlive || avatar.IsRagdoll) { Carrier = null; return; }
                Transform c = Carrier.transform;
                // Arrastrado por delante y a un lado, como al llevar a un compañero.
                Vector3 target = c.position + c.forward * 1.5f + c.right * .35f;
                Vector3 next = Vector3.Lerp(SyncedPosition, target, 1f - Mathf.Exp(-Runner.DeltaTime * 9f));
                next.y = Ground(next);
                SyncedPosition = next;
                SyncedYaw = Mathf.MoveTowardsAngle(SyncedYaw, c.eulerAngles.y + 90f, 220f * Runner.DeltaTime);

                var near = MonsterGrinder.Nearest(SyncedPosition);
                if (near != null && near.CanAccept && near.InIntake(SyncedPosition))
                {
                    Carrier = null;
                    FedInto = near.Object;
                    FeedStart = Runner.SimulationTime;
                    SyncedPosition = near.MouthPoint;
                    near.RPC_Feed(Object, Value);
                }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_Release(NetworkObject who)
        {
            if (Carrier == who) Carrier = null;
        }

        private float Ground(Vector3 p)
        {
            float best = p.y - 3f; bool found = false;
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.attachedRigidbody != null) continue;
                if (hit.collider.GetComponentInParent<MarsFusionAvatar>() != null || hit.collider.GetComponentInParent<EnemyCombatUI>() != null) continue;
                if (!found || hit.point.y > best) { best = hit.point.y; found = true; }
            }
            return found ? best : p.y;
        }

        // ------------------------------------------------------------------ presentacion

        public override void Render()
        {
            if (corpse == null && visual == null) TryClaimBody();

            float smooth = 1f - Mathf.Exp(-Time.deltaTime * (HasStateAuthority ? 30f : 14f));
            transform.position = Vector3.Lerp(transform.position, SyncedPosition, smooth);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, SyncedYaw, 0), smooth);

            if (IsFed)
            {
                if (corpse != null && visual == null)
                {
                    feedSize = Mathf.Max(.01f, Mathf.Max(corpse.VisualBounds().size.x, corpse.VisualBounds().size.z));
                    visual = corpse.Freeze(transform);
                    visualBaseScale = visual.localScale;
                }
                if (visual == null) return;
                // Se hunde poco a poco entre los rodillos, sacudiendose y encogiendose.
                float t = Mathf.Clamp01((Runner.SimulationTime - FeedStart) / MonsterGrinder.GrindSeconds);
                float fit = Mathf.Min(1f, FeedFit / feedSize);
                float scale = Mathf.Lerp(fit, fit * .35f, t * t);
                float sink = Mathf.Lerp(.25f, -.8f, Mathf.SmoothStep(0, 1, t));
                float shake = .06f * (1f - t * .5f);
                visual.localScale = visualBaseScale * scale;
                visual.localPosition = new Vector3(Mathf.Sin(Time.time * 47f) * shake, sink, Mathf.Cos(Time.time * 53f) * shake);
                visual.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 31f) * 9f, t * 160f, Mathf.Cos(Time.time * 27f) * 9f);
                return;
            }

            // Pilar de luz naranja sobre el cadaver libre: se ve de lejos y dice "esto se lleva".
            UpdateBeacon(corpse != null && !IsCarried);

            // Arrastre: el cuerpo cuelga de la mano y la fisica hace que se bambolee.
            if (corpse != null)
            {
                Vector3 hand = transform.position + Vector3.up * HandHeight;
                if (IsCarried)
                {
                    if (!corpse.IsGripped) corpse.Grab(hand);
                    corpse.MoveGrip(hand);
                }
                else if (corpse.IsGripped) corpse.Release();
            }
        }

        private LineRenderer beacon;
        private void UpdateBeacon(bool show)
        {
            if (!show) { if (beacon != null) beacon.enabled = false; return; }
            if (beacon == null)
            {
                beacon = new GameObject("CorpseBeacon").AddComponent<LineRenderer>();
                beacon.transform.SetParent(transform, false);
                beacon.material = new Material(Shader.Find("Sprites/Default"));
                beacon.positionCount = 2;
                beacon.useWorldSpace = true;
                beacon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beacon.receiveShadows = false;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(1f, .7f, .25f), 0), new GradientColorKey(new Color(1f, .5f, .1f), 1) },
                    new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(.7f, .08f), new GradientAlphaKey(.25f, .5f), new GradientAlphaKey(0f, 1) });
                beacon.colorGradient = g;
            }
            beacon.enabled = true;
            Vector3 b = BodyPoint;
            beacon.SetPosition(0, b);
            beacon.SetPosition(1, b + Vector3.up * 6f);
            // Haz fino que respira: se ve de lejos sin tapar al monstruo.
            float w = .1f + .04f * Mathf.Sin(Time.time * 4f);
            beacon.startWidth = w * 1.6f; beacon.endWidth = w * .3f;
        }

        private void TryClaimBody()
        {
            corpse = CorpseRagdoll.Claim(transform.position, ClaimRange);
            if (corpse == null && Time.time - spawnedAt > FallbackAfter) visual = Fallback();
        }

        // Si no hay cuerpo que reclamar: masa organica generica.
        private Transform Fallback()
        {
            var root = new GameObject("CorpseVisual").transform;
            root.SetParent(transform, false);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.32f, .12f, .2f) };
            mat.SetFloat("_Smoothness", .65f);
            for (int i = 0; i < 6; i++)
            {
                var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(blob.GetComponent<Collider>());
                blob.transform.SetParent(root, false);
                blob.transform.localPosition = new Vector3(Random.Range(-.5f, .5f), Random.Range(.2f, .5f), Random.Range(-.5f, .5f));
                blob.transform.localScale = Vector3.one * Random.Range(.45f, .8f);
                blob.GetComponent<Renderer>().sharedMaterial = mat;
            }
            feedSize = 1.4f;
            return root;
        }

        // ------------------------------------------------------------------ entrada local

        private void Update()
        {
            if (Object == null || !Object.IsValid) return;
            var local = MarsFusionAvatar.Local;

            // Agarre pedido: espera a tener la autoridad y entonces se engancha al jugador.
            if (wantsGrab && HasStateAuthority)
            {
                wantsGrab = false;
                if (!IsFed && !IsCarried && local != null)
                {
                    Carrier = local.Object; grabbing = true;
                    Vector3 body = BodyPoint;
                    SyncedPosition = new Vector3(body.x, Ground(body), body.z);
                }
            }

            bool held = Application.isFocused && Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (grabbing && (!held || local == null || Carrier != local.Object))
            {
                grabbing = false;
                if (local != null && Carrier == local.Object && Object.IsValid) RPC_Release(local.Object);
            }

            // Una sola vez por frame: el cadaver libre mas cercano al jugador local.
            if (inputFrame == Time.frameCount) return;
            inputFrame = Time.frameCount;
            promptTarget = null;
            if (local == null || !local.IsNetworkReady || !CanGrab(local)) return;
            MonsterRemains best = null; float bestD = GrabRange * GrabRange;
            foreach (var r in FindObjectsByType<MonsterRemains>(FindObjectsSortMode.None))
            {
                if (r.Object == null || !r.Object.IsValid || !r.Ready || r.IsFed || r.IsCarried) continue;
                float d = (r.BodyPoint - local.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = r; }
            }
            promptTarget = best;
            if (best != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                best.BeginGrab();
        }

        private static bool CanGrab(MarsFusionAvatar local)
        {
            if (!local.IsAlive || local.IsRagdoll) return false;
            if (local.Ragdoll != null && (local.Ragdoll.IsRagdolled || local.Ragdoll.IsRecovering)) return false;
            var revive = local.GetComponent<SurvivalRevive>();
            if (revive != null && (!revive.HandsEmpty || revive.IsDragging || revive.HasReviveTargetNearby)) return false;
            foreach (var r in FindObjectsByType<MonsterRemains>(FindObjectsSortMode.None))
                if (r.IsCarried && r.Carrier == local.Object) return false;
            return true;
        }

        private void BeginGrab()
        {
            wantsGrab = true;
            grabbing = true;
            if (!HasStateAuthority) Object.RequestStateAuthority();
        }

        /// <summary>Punto de agarre del cadaver que lleva este jugador (para estirar el brazo).</summary>
        public static bool TryGetCarriedBy(NetworkObject player, out Vector3 grabPoint)
        {
            grabPoint = default;
            if (player == null) return false;
            foreach (var r in FindObjectsByType<MonsterRemains>(FindObjectsSortMode.None))
                if (r.Object != null && r.Object.IsValid && r.IsCarried && r.Carrier == player)
                {
                    grabPoint = r.transform.position + Vector3.up * HandHeight;
                    return true;
                }
            return false;
        }

        private void OnGUI()
        {
            if (promptTarget != this || Event.current.type != EventType.Repaint) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(BodyPoint + Vector3.up * 1.2f);
            if (screen.z <= 0) return;
            var rows = new[]
            {
                new GameJam.UI.InteractionPromptCard.Row { Key = GameJam.UI.InteractionPromptCard.MouseLeft, Action = "Arrastrar a la trituradora", Tag = "MANTENER", Enabled = true },
            };
            GameJam.UI.InteractionPromptCard.Draw(screen, "RESTOS DE MONSTRUO", Title.ToString(), new Color(.95f, .65f, .2f), rows, 1f);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (promptTarget == this) promptTarget = null;
            if (corpse != null) Destroy(corpse.gameObject);
            if (beacon != null) Destroy(beacon.material);
            if (visual != null) Destroy(visual.gameObject);
        }
    }
}
