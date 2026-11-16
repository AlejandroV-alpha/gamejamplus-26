using GameJam.Combat;
using GameJam.Inventory;
using GameJam.Multiplayer;
using UnityEngine;

namespace GameJam.Showcase
{
    /// <summary>
    /// Interactive holographic combat & troll drone for live exhibition demos.
    /// Showcases floating damage numbers, crits, elemental colors, hit-stop, and magnetic loot orbs in any scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HologramCombatDummy : MonoBehaviour, ITakeDamage
    {
        private const float MaxHp = 120f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly string[] Taunts =
        {
            "¡PUNTERÍA 404!",
            "¿ESO FUE TODO?",
            "SKILL ISSUE!",
            "¡AY MI CHIP!",
            "¡MI ABUELA PEGA MÁS!",
            "¡CRÍTICO EN EL EGO!",
            "CALIBRANDO MANCO..."
        };

        private static readonly DamageKind[] CycleKinds =
        {
            DamageKind.Normal,
            DamageKind.Burn,
            DamageKind.Poison,
            DamageKind.Bleed,
            DamageKind.Normal
        };

        private float currentHp = MaxHp;
        private EnemyCombatUI combatUi;
        private Transform visualRoot;
        private Transform ringOuter;
        private Transform ringInner;
        private Transform headTransform;
        private Renderer[] bodyRenderers;
        private MaterialPropertyBlock mpb;
        private Material sharedHoloMat;
        private Material sharedAccentMat;
        private Light coreLight;
        private AudioSource audioSource;
        private AudioClip hitTone;
        private AudioClip boomTone;

        private float flashUntil;
        private float wobbleUntil;
        private Vector3 wobbleAxis = Vector3.right;
        private float respawnAt = -1f;
        private int hitCounter;
        private Vector3 basePosition;

        public static HologramCombatDummy SpawnInFrontOf(Transform actor)
        {
            if (actor == null) return null;

            HologramCombatDummy existing = FindAnyObjectByType<HologramCombatDummy>();
            Vector3 forward = Vector3.ProjectOnPlane(actor.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

            Vector3 spawnPos = actor.position + forward * 4.5f;
            if (Physics.Raycast(spawnPos + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 16f, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPos.y = hit.point.y;
            }

            if (existing != null)
            {
                existing.transform.position = spawnPos;
                existing.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
                existing.basePosition = spawnPos;
                existing.ResetDummy();
                EnemyDamagePopup.ShowCallout(spawnPos + Vector3.up * 2.1f, "DRON LISTO", "TARGET", new Color(.35f, 1f, .75f), 1.2f);
                return existing;
            }

            GameObject root = new GameObject("HologramCombatDummy_Expo");
            root.transform.position = spawnPos;
            root.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            HologramCombatDummy dummy = root.AddComponent<HologramCombatDummy>();
            dummy.basePosition = spawnPos;
            dummy.BuildVisuals();
            EnemyDamagePopup.ShowCallout(spawnPos + Vector3.up * 2.1f, "¡DISPÁRAME!", "EXPO DUMMY", new Color(.35f, 1f, .75f), 1.3f);
            return dummy;
        }

        private void BuildVisuals()
        {
            mpb = new MaterialPropertyBlock();

            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.15f, 0f);
            col.radius = 0.55f;
            col.height = 2.2f;

            visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = new Vector3(0f, 1.1f, 0f);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            sharedHoloMat = new Material(litShader) { name = "HoloDummyCoreMat" };
            sharedHoloMat.color = new Color(0.12f, 0.85f, 0.95f, 1f);
            if (sharedHoloMat.HasProperty(BaseColorId)) sharedHoloMat.SetColor(BaseColorId, new Color(0.12f, 0.85f, 0.95f, 1f));

            sharedAccentMat = new Material(litShader) { name = "HoloDummyAccentMat" };
            sharedAccentMat.color = new Color(0.32f, 0.95f, 0.25f, 1f);
            if (sharedAccentMat.HasProperty(BaseColorId)) sharedAccentMat.SetColor(BaseColorId, new Color(0.32f, 0.95f, 0.25f, 1f));

            // Core torso
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(core.GetComponent<Collider>());
            core.name = "HoloTorso";
            core.transform.SetParent(visualRoot, false);
            core.transform.localScale = new Vector3(0.68f, 0.72f, 0.68f);
            core.GetComponent<MeshRenderer>().sharedMaterial = sharedHoloMat;

            // Head visor
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(head.GetComponent<Collider>());
            head.name = "HoloHead";
            headTransform = head.transform;
            headTransform.SetParent(visualRoot, false);
            headTransform.localPosition = new Vector3(0f, 0.85f, 0f);
            headTransform.localScale = new Vector3(0.56f, 0.42f, 0.48f);
            head.GetComponent<MeshRenderer>().sharedMaterial = sharedHoloMat;

            // Glowing Eyes
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(eye.GetComponent<Collider>());
                eye.transform.SetParent(headTransform, false);
                eye.transform.localPosition = new Vector3(side * 0.22f, 0.05f, 0.52f);
                eye.transform.localScale = new Vector3(0.24f, 0.18f, 0.08f);
                eye.GetComponent<MeshRenderer>().sharedMaterial = sharedAccentMat;
            }

            // Outer & Inner rotating orbital shields
            ringOuter = CreateOrbitalRing("OuterRing", 0.95f, 6, sharedAccentMat);
            ringInner = CreateOrbitalRing("InnerRing", 0.72f, 4, sharedHoloMat);

            bodyRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);

            GameObject lightGo = new GameObject("HoloLight");
            lightGo.transform.SetParent(visualRoot, false);
            coreLight = lightGo.AddComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.color = new Color(0.25f, 0.95f, 0.65f);
            coreLight.intensity = 2.4f;
            coreLight.range = 4.5f;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.75f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 24f;
            hitTone = SynthesizeTone("DummyHit", 0.09f, 680f, 1340f, 0.55f);
            boomTone = SynthesizeTone("DummyBoom", 0.34f, 420f, 95f, 0.8f);

            combatUi = EnemyCombatUI.Attach(
                gameObject,
                "DRON TROLL M.K. IV",
                () => currentHp,
                MaxHp,
                () => respawnAt < 0f && currentHp > 0f);
        }

        private Transform CreateOrbitalRing(string ringName, float radius, int nodes, Material mat)
        {
            Transform ring = new GameObject(ringName).transform;
            ring.SetParent(visualRoot, false);
            for (int i = 0; i < nodes; i++)
            {
                float ang = i * Mathf.PI * 2f / nodes;
                GameObject node = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(node.GetComponent<Collider>());
                node.transform.SetParent(ring, false);
                node.transform.localPosition = new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
                node.transform.localRotation = Quaternion.Euler(45f, ang * Mathf.Rad2Deg, 45f);
                node.transform.localScale = Vector3.one * 0.16f;
                node.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return ring;
        }

        public void TakeDamage(int damage)
        {
            if (respawnAt > 0f || damage <= 0) return;

            hitCounter++;
            bool isCrit = (hitCounter % 3 == 0);
            DamageKind kind = CycleKinds[hitCounter % CycleKinds.Length];
            float displayedDamage = isCrit ? damage * 25f : damage * 12f;

            currentHp = Mathf.Max(0f, currentHp - (isCrit ? 28f : 18f));
            flashUntil = Time.time + 0.09f;
            wobbleUntil = Time.time + 0.28f;

            MarsFusionAvatar local = MarsFusionAvatar.Local;
            if (local != null)
            {
                Vector3 dir = (transform.position - local.transform.position).normalized;
                wobbleAxis = Vector3.Cross(Vector3.up, dir.sqrMagnitude > 0.01f ? dir : Vector3.forward);
            }

            HitFeedbackHUD.Hit();
            if (combatUi != null)
            {
                combatUi.Damage(displayedDamage, kind, isCrit);
            }
            else
            {
                EnemyDamagePopup.Show(this, displayedDamage, transform.position + Vector3.up * 1.5f, kind, isCrit);
            }

            if (audioSource != null && hitTone != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.18f);
                audioSource.PlayOneShot(hitTone);
            }

            if (hitCounter % 2 == 0)
            {
                string taunt = Taunts[hitCounter % Taunts.Length];
                EnemyDamagePopup.ShowCallout(
                    transform.position + Vector3.up * 2.25f + Random.insideUnitSphere * 0.25f,
                    taunt,
                    "DRON TROLL",
                    new Color(0.35f, 0.95f, 1f),
                    1.05f);
            }

            // Harmless playful repulsor wave every 4 hits if player is very close
            if (hitCounter % 4 == 0 && local != null)
            {
                float distSq = (local.transform.position - transform.position).sqrMagnitude;
                if (distSq < 36f)
                {
                    local.ApplyLocalShowcaseImpulse((local.transform.position - transform.position).normalized * 9.5f, 5.5f);
                    EnemyDamagePopup.ShowCallout(local.transform.position + Vector3.up * 1.8f, "¡ONDA REPULSORA!", "BOOP!", new Color(1f, 0.65f, 0.2f), 1.2f);
                }
            }

            if (currentHp <= 0f)
            {
                TriggerDefeatExplosion(local);
            }
        }

        private void TriggerDefeatExplosion(MarsFusionAvatar killer)
        {
            respawnAt = Time.time + 2.6f;
            HitFeedbackHUD.Kill("DRON TROLL M.K. IV");
            if (audioSource != null && boomTone != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(boomTone);
            }

            EnemyDamagePopup.ShowCallout(
                transform.position + Vector3.up * 2.1f,
                "+1000 SKILL RECOVERED!",
                "DESTRUIDO",
                new Color(0.4f, 1f, 0.45f),
                1.4f);

            // Spawn magnetic LootOrbs towards the player
            Transform killerTarget = killer != null ? killer.transform : null;
            MaterialData rewardMat = null;
            if (killer != null)
            {
                var inv = killer.GetComponent<GameJam.Inventory.Inventory>();
                if (inv != null && inv.RegisteredMaterials != null)
                {
                    foreach (var m in inv.RegisteredMaterials)
                    {
                        if (m != null && m.kind == CarriedItemKind.Material) { rewardMat = m; break; }
                    }
                }
            }

            Vector3 center = transform.position + Vector3.up * 1.2f;
            for (int i = 0; i < 6; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(0.7f, 0.85f, 0f);
                Color orbCol = (i % 2 == 0) ? new Color(0.35f, 1f, 0.65f) : new Color(0.35f, 0.85f, 1f);
                LootOrb.Spawn(center, dir.normalized * Random.Range(4.5f, 7f), killerTarget, i == 0 ? rewardMat : null, sharedAccentMat, orbCol, 0.2f + i * 0.05f);
            }

            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        }

        private void ResetDummy()
        {
            currentHp = MaxHp;
            respawnAt = -1f;
            if (visualRoot != null) visualRoot.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (respawnAt > 0f)
            {
                if (Time.time >= respawnAt)
                {
                    ResetDummy();
                    EnemyDamagePopup.ShowCallout(transform.position + Vector3.up * 2.1f, "¡RECONSTRUIDO!", "ROUND 2", new Color(0.4f, 1f, 0.7f), 1.15f);
                }
                return;
            }

            float t = Time.time;
            if (visualRoot != null)
            {
                float hover = Mathf.Sin(t * 3.2f) * 0.14f;
                visualRoot.localPosition = new Vector3(0f, 1.1f + hover, 0f);

                if (t < wobbleUntil)
                {
                    float k = (wobbleUntil - t) / 0.28f;
                    float angle = Mathf.Sin(t * 48f) * 22f * k;
                    visualRoot.localRotation = Quaternion.AngleAxis(angle, wobbleAxis);
                }
                else
                {
                    visualRoot.localRotation = Quaternion.identity;
                }
            }

            if (ringOuter != null) ringOuter.Rotate(new Vector3(25f, 110f, 15f) * Time.deltaTime, Space.Self);
            if (ringInner != null) ringInner.Rotate(new Vector3(-40f, -140f, 30f) * Time.deltaTime, Space.Self);

            if (MarsFusionAvatar.Local != null && headTransform != null)
            {
                Vector3 toPlayer = MarsFusionAvatar.Local.transform.position + Vector3.up * 1.2f - headTransform.position;
                if (toPlayer.sqrMagnitude > 0.05f)
                {
                    headTransform.rotation = Quaternion.Slerp(headTransform.rotation, Quaternion.LookRotation(toPlayer.normalized, Vector3.up), Time.deltaTime * 8f);
                }
            }

            bool flashing = t < flashUntil;
            if (bodyRenderers != null)
            {
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    Renderer r = bodyRenderers[i];
                    if (r == null) continue;
                    if (flashing)
                    {
                        r.GetPropertyBlock(mpb);
                        mpb.SetColor(BaseColorId, new Color(5f, 5f, 5f, 1f));
                        mpb.SetColor(ColorId, new Color(5f, 5f, 5f, 1f));
                        r.SetPropertyBlock(mpb);
                    }
                    else if (r.HasPropertyBlock())
                    {
                        r.SetPropertyBlock(null);
                    }
                }
            }
        }

        private static AudioClip SynthesizeTone(string name, float duration, float startFreq, float endFreq, float volume)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] buffer = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float progress = i / (float)samples;
                float freq = Mathf.Lerp(startFreq, endFreq, progress);
                phase += 2f * Mathf.PI * freq / sampleRate;
                float env = Mathf.Exp(-progress * 5.5f) * Mathf.Clamp01(i / 48f);
                buffer[i] = (Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2f) * 0.3f) * env * volume;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (sharedHoloMat != null) Destroy(sharedHoloMat);
            if (sharedAccentMat != null) Destroy(sharedAccentMat);
        }
    }
}
