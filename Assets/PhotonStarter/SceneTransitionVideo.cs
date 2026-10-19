using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

namespace GameJam.Multiplayer
{
    public sealed class SceneTransitionVideo : MonoBehaviour
    {
        public float fadeOutSeconds = .6f;
        private static SceneTransitionVideo current;
        public static bool IsBlockingInput => current != null && current.covering;
        private VideoPlayer player;
        private RenderTexture texture;
        private RawImage screen;
        private CanvasGroup group;
        private VideoClip clip;
        private string[] sequence;
        private int sequenceIndex;
        private System.Action sequenceFinished;
        private bool sequenceComplete, sequenceError, showingCredits;
        private TMPro.TextMeshProUGUI message;
        private Fusion.NetworkRunner campaignRunner;
        private TMPro.TMP_MeshInfo[] creditsMesh;
        private float creditsStarted, creditsHeight;
        private bool crawlEnded;
        private Texture2D spaceTexture, particleTexture;
        private Sprite particleSprite;
        private CanvasGroup creditsIntro;
        private CreditParticle[] creditParticles;
        private struct CreditParticle
        {
            public UnityEngine.UI.Image image;
            public Vector2 position;
            public float speed, phase, brightness;
            public bool dust;
        }
        public static bool IsCampaignTransition => current != null && current.sequence != null;

        // Resources keeps these clips included in builds without editing scene references.
        public static bool PlaySequence(string[] names, int destination, System.Action completed)
        {
            if (current != null || names == null || names.Length == 0) return false;
            var transition = Play(Resources.Load<VideoClip>("Videos/Transiciones/" + names[0]));
            transition.sequence = names;
            var session = FindFirstObjectByType<MarsFusionSession>();
            transition.campaignRunner = session != null ? session.Runner : null;
            transition.sequenceFinished = completed;
            transition.destinationIndex = destination;
            transition.GetComponent<Canvas>().sortingOrder = 3000;
            var scaler = transition.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            return true;
        }

        public static void ShowCredits()
        {
            if (current == null || current.showingCredits) return;
            current.showingCredits = true;
            current.ReleaseVideo();
            var credits = Resources.Load<TextAsset>("Credits/Authors");
            current.ShowMessage("<size=86><b>CRÉDITOS</b></size>\n"
                + "<size=32><cspace=8>EQUIPO CREADOR</cspace></size>\n\n\n"
                + FormatCredits(credits != null ? credits.text : "Equipo creador del juego")
                + "\n\n\n<size=64><b>GRACIAS POR JUGAR</b></size>");
            var button = new GameObject("VolverAlLobby").AddComponent<UnityEngine.UI.Button>();
            button.transform.SetParent(current.transform, false);
            var background = button.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(.025f, .035f, .065f, .88f);
            var border = button.gameObject.AddComponent<UnityEngine.UI.Outline>();
            border.effectColor = new Color(.85f, .65f, .2f, .7f);
            border.effectDistance = new Vector2(1, -1);
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, .86f, .5f);
            colors.pressedColor = new Color(.7f, .55f, .25f);
            button.colors = colors;
            button.targetGraphic = background;
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0, 22);
            rect.sizeDelta = new Vector2(340, 60);
            var label = new GameObject("Texto").AddComponent<TMPro.TextMeshProUGUI>();
            label.transform.SetParent(button.transform, false);
            Stretch(label.rectTransform);
            label.text = "Volver al lobby";
            label.fontSize = 24;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = new Color(1f, .85f, .48f);
            label.raycastTarget = false;
            button.onClick.AddListener(current.ReturnToLobby);
            current.BeginCreditsCrawl();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static string FormatCredits(string source)
        {
            var formatted = new System.Text.StringBuilder();
            foreach (var raw in source.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("<b>"))
                    formatted.Append("<size=60>").Append(line).Append("</size>");
                else if (line.Contains("@"))
                    formatted.Append("<size=32><color=#D9B862>").Append(line).Append("</color></size>");
                else if (line.Contains("AGRADECIMIENTOS"))
                    formatted.Append("<size=74><b>AGRADECIMIENTOS</b></size>");
                else if (line.Length > 0)
                    formatted.Append("<size=42>").Append(line).Append("</size>");
                formatted.Append('\n');
                if (line.Length == 0) formatted.Append('\n');
            }
            return formatted.ToString();
        }

        private void BuildSpaceBackground()
        {
            // One background texture and one shared particle sprite keep the canvas inexpensive.
            spaceTexture = new Texture2D(512, 288, TextureFormat.RGBA32, false);
            spaceTexture.name = "CreditsDeepSpace";
            spaceTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[512 * 288];
            for (int y = 0; y < 288; y++)
                for (int x = 0; x < 512; x++)
                {
                    float u = x / 511f, v = y / 287f;
                    float clouds = Mathf.PerlinNoise(u * 5f + 13f, v * 4f + 7f)
                        * .65f + Mathf.PerlinNoise(u * 13f, v * 11f) * .35f;
                    float sides = Mathf.Pow(Mathf.Abs(u - .5f) * 2f, 1.3f);
                    float veil = Mathf.Pow(clouds, 2.8f) * (.15f + sides * .85f);
                    Color nebula = Color.Lerp(new Color(.025f, .10f, .19f),
                        new Color(.14f, .035f, .19f), u);
                    pixels[y * 512 + x] = new Color(.002f, .004f, .012f) + nebula * veil;
                }
            spaceTexture.SetPixels(pixels);
            spaceTexture.Apply(false, true);
            var space = new GameObject("EspacioProfundo").AddComponent<UnityEngine.UI.RawImage>();
            space.transform.SetParent(transform, false);
            space.transform.SetSiblingIndex(1);
            space.texture = spaceTexture;
            space.raycastTarget = false;
            Stretch(space.rectTransform);

            particleTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            particleTexture.name = "CreditsStarGlow";
            particleTexture.wrapMode = TextureWrapMode.Clamp;
            var glow = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                    glow[y * 32 + x] = new Color(1, 1, 1,
                        Mathf.Exp(-radius * radius * 7f) * Mathf.Clamp01((1f - radius) * 5f));
                }
            particleTexture.SetPixels(glow);
            particleTexture.Apply(false, true);
            particleSprite = Sprite.Create(particleTexture, new Rect(0, 0, 32, 32), Vector2.one * .5f);
            var random = new System.Random(2026);
            creditParticles = new CreditParticle[220];
            for (int i = 0; i < creditParticles.Length; i++)
            {
                bool dust = i >= 185;
                var star = new GameObject(dust ? "PolvoEstelar" : "Estrella").AddComponent<UnityEngine.UI.Image>();
                star.transform.SetParent(transform, false);
                star.transform.SetSiblingIndex(2);
                star.raycastTarget = false;
                star.sprite = particleSprite;
                float depth = (float)random.NextDouble();
                float size = dust ? Mathf.Lerp(10, 25, depth) : Mathf.Lerp(2, 9, depth * depth);
                star.rectTransform.sizeDelta = Vector2.one * size;
                creditParticles[i] = new CreditParticle
                {
                    image = star, dust = dust,
                    position = new Vector2((float)random.NextDouble(), (float)random.NextDouble()),
                    phase = (float)random.NextDouble() * Mathf.PI * 2,
                    speed = dust ? .008f + depth * .008f : .0008f + depth * .002f,
                    brightness = dust ? .08f + depth * .10f : .3f + depth * .65f
                };
            }
        }

        private void BeginCreditsCrawl()
        {
            BuildSpaceBackground();
            var intro = new GameObject("IntroduccionCreditos").AddComponent<TMPro.TextMeshProUGUI>();
            intro.transform.SetParent(transform, false);
            Stretch(intro.rectTransform);
            intro.text = "<size=32><cspace=8>EL FINAL DE UNA MISIÓN</cspace></size>\n\n"
                + "<size=86><b>UNA HISTORIA\nHECHA EN EQUIPO</b></size>";
            intro.alignment = TMPro.TextAlignmentOptions.Center;
            intro.color = new Color(1f, .82f, .22f);
            intro.raycastTarget = false;
            creditsIntro = intro.gameObject.AddComponent<CanvasGroup>();
            creditsIntro.alpha = 0;
            message.enableAutoSizing = false;
            message.fontSize = 48;
            message.color = new Color(1f, .84f, .22f);
            message.fontStyle = TMPro.FontStyles.Normal;
            message.characterSpacing = .6f;
            message.lineSpacing = 12;
            message.alignment = TMPro.TextAlignmentOptions.Top;
            message.overflowMode = TMPro.TextOverflowModes.Overflow;
            message.raycastTarget = false;
            var rect = message.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1420, 10000);
            message.ForceMeshUpdate();
            creditsHeight = message.preferredHeight;
            creditsMesh = message.textInfo.CopyMeshInfoVertexData();
            creditsStarted = Time.realtimeSinceStartup;
            UpdateCreditsCrawl();
        }

        private void UpdateCreditsCrawl()
        {
            float elapsed = Time.realtimeSinceStartup - creditsStarted;
            if (creditParticles != null)
                for (int i = 0; i < creditParticles.Length; i++)
                {
                    var particle = creditParticles[i];
                    var position = particle.position;
                    position.y = Mathf.Repeat(position.y - elapsed * particle.speed, 1f);
                    position.x = Mathf.Repeat(position.x + Mathf.Sin(elapsed * .08f + particle.phase)
                        * (particle.dust ? .008f : .0008f), 1f);
                    particle.image.rectTransform.anchorMin = particle.image.rectTransform.anchorMax = position;
                    particle.image.rectTransform.anchoredPosition = Vector2.zero;
                    Color tint = particle.dust ? new Color(.7f, .8f, 1f) : new Color(.84f, .91f, 1f);
                    tint.a = particle.brightness * (.8f + .2f * Mathf.Sin(elapsed * .7f + particle.phase));
                    particle.image.color = tint;
                }
            if (creditsIntro != null)
            {
                creditsIntro.alpha = Mathf.Clamp01(elapsed / 1.2f) * (1f - Mathf.Clamp01((elapsed - 3.5f) / 1.5f));
                if (elapsed > 5f) { Destroy(creditsIntro.gameObject); creditsIntro = null; }
            }
            if (crawlEnded || creditsMesh == null || message == null) return;
            float travel = Mathf.Max(0, elapsed - 5f) * 65f - 120f;
            float canvasHeight = ((RectTransform)transform).rect.height;
            float baseY = 100f;
            float projectionHeight = Mathf.Max(400f, canvasHeight * .86f - baseY);
            var info = message.textInfo;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var mesh = info.meshInfo[m];
                for (int v = 0; v < mesh.vertexCount; v++)
                {
                    Vector3 original = creditsMesh[m].vertices[v];
                    float distance = travel + original.y;
                    float depth = Mathf.Max(0f, distance);
                    float perspective = 1f / (1f + depth / 1100f);
                    mesh.vertices[v] = new Vector3(original.x * perspective,
                        baseY + projectionHeight * depth / (depth + 1100f), original.z);
                    var color = creditsMesh[m].colors32[v];
                    color.a = (byte)(255f * Mathf.Clamp01(distance / 70f)
                        * (1f - Mathf.SmoothStep(0, 1, (distance - 2000f) / 1400f)));
                    mesh.colors32[v] = color;
                }
                mesh.mesh.vertices = mesh.vertices;
                mesh.mesh.colors32 = mesh.colors32;
                mesh.mesh.RecalculateBounds();
                message.UpdateGeometry(mesh.mesh, m);
            }
            if (travel <= creditsHeight + 3400f) return;
            crawlEnded = true;
            creditsMesh = null;
            message.alignment = TMPro.TextAlignmentOptions.Center;
            message.rectTransform.anchorMin = Vector2.zero;
            message.rectTransform.anchorMax = Vector2.one;
            message.rectTransform.offsetMin = new Vector2(100, 120);
            message.rectTransform.offsetMax = new Vector2(-100, -70);
            message.text = "<size=76><b>GRACIAS POR JUGAR</b></size>\n\n"
                + "<size=28><cspace=5>HASTA LA PRÓXIMA MISIÓN</cspace></size>";
            message.ForceMeshUpdate();
        }

        private async void ReturnToLobby()
        {
            var session = FindFirstObjectByType<MarsFusionSession>();
            if (session == null || session.Busy) return;
            try { await session.Disconnect(); }
            catch (System.Exception exception) { Debug.LogException(exception); }
            if (this != null) Destroy(gameObject);
        }

        private void ShowMessage(string text)
        {
            if (message == null)
            {
                message = new GameObject("Mensaje").AddComponent<TMPro.TextMeshProUGUI>();
                message.transform.SetParent(transform, false);
                Stretch(message.rectTransform);
                message.rectTransform.offsetMin = new Vector2(100, 70);
                message.rectTransform.offsetMax = new Vector2(-100, -70);
                message.alignment = TMPro.TextAlignmentOptions.Center;
                message.fontSize = 42;
                message.enableAutoSizing = true;
                message.fontSizeMin = 18;
                message.fontSizeMax = 42;
                message.color = Color.white;
            }
            message.text = text;
            message.gameObject.SetActive(true);
        }

        private void ReleaseVideo()
        {
            if (player != null)
            {
                player.prepareCompleted -= Prepared;
                player.frameReady -= FrameReady;
                player.loopPointReached -= Ended;
                player.errorReceived -= Failed;
                player.Stop();
                player.targetTexture = null;
                Destroy(player);
                player = null;
            }
            if (screen != null) { Destroy(screen.gameObject); screen = null; }
            if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
            preparing = playing = false;
            lastFrame = -1;
        }

        private void RetrySequenceClip()
        {
            ReleaseVideo();
            sequenceError = false;
            if (message != null) message.gameObject.SetActive(false);
            clip = Resources.Load<VideoClip>("Videos/Transiciones/" + sequence[sequenceIndex]);
            PrepareVideo();
        }
        private int destinationIndex;
        private float coverAt, waitStarted, playbackStarted, lastProgress, fadeStart = -1f;
        private long lastFrame = -1;
        private bool covering, preparing, playing, finished;
        public static bool IsPresent => current != null;
        public static bool TryGetPlaybackTime(out double seconds)
        {
            seconds = 0;
            if (current == null || current.player == null || !current.playing || current.finished) return false;
            seconds = current.player.time;
            return true;
        }
        public static void CancelCurrent()
        {
            if (current != null) Destroy(current.gameObject);
        }

        public static SceneTransitionVideo Play(VideoClip clip, int destinationIndex = -1, float delay = 0f)
        {
            if (current != null) return current;
            var go = new GameObject("j_SceneTransitionVideo");
            DontDestroyOnLoad(go);
            var transition = go.AddComponent<SceneTransitionVideo>();
            current = transition;
            transition.clip = clip;
            transition.destinationIndex = destinationIndex;
            transition.coverAt = Time.realtimeSinceStartup + Mathf.Max(0, delay);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            go.AddComponent<CanvasScaler>();
            transition.group = go.AddComponent<CanvasGroup>();
            transition.group.alpha = 0;
            transition.group.blocksRaycasts = false;
            var black = new GameObject("Negro").AddComponent<Image>();
            black.transform.SetParent(go.transform, false);
            black.color = Color.black;
            Stretch(black.rectTransform);
            return transition;
        }

        private bool SceneReady()
        {
            if (destinationIndex < 0) return true;
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.buildIndex != destinationIndex) return false;
            var session = FindFirstObjectByType<MarsFusionSession>();
            if (session == null || session.Runner == null || !session.Runner.IsRunning) return false;
            var manager = session.Runner.GetComponent<Fusion.NetworkSceneManagerDefault>();
            return (manager == null || !manager.IsBusy)
                && session.Runner.GetPlayerObject(session.Runner.LocalPlayer) != null;
        }

        private void PrepareVideo()
        {
            preparing = true;
            waitStarted = Time.realtimeSinceStartup;
            if (clip == null) { Finish("no-clip"); return; }
            float scale = Mathf.Min(1f, 1280f / Mathf.Max(1, clip.width));
            texture = new RenderTexture(Mathf.Max(1, Mathf.RoundToInt(clip.width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(clip.height * scale)), 0) { name = "TransitionVideoRT" };
            screen = new GameObject("Video").AddComponent<RawImage>();
            screen.transform.SetParent(transform, false);
            screen.texture = texture;
            screen.color = new Color(1,1,1,0);
            Stretch(screen.rectTransform);
            var aspect = screen.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = (float)clip.width / Mathf.Max(1, clip.height);
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.clip = clip;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = texture;
            player.audioOutputMode = sequence != null ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.isLooping = false;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.sendFrameReadyEvents = true;
            player.prepareCompleted += Prepared;
            player.frameReady += FrameReady;
            player.loopPointReached += Ended;
            player.errorReceived += Failed;
            Debug.Log("[Transition video] stage=preparing scene=" + SceneManager.GetActiveScene().buildIndex);
            player.Prepare();
        }

        private void Prepared(VideoPlayer source)
        {
            if (finished) return;
            playing = true;
            playbackStarted = lastProgress = Time.realtimeSinceStartup;
            source.Play();
            Debug.Log("[Transition video] stage=playing");
        }
        private void FrameReady(VideoPlayer source, long frame)
        {
            if (finished) return;
            screen.color = Color.white;
            if (frame != lastFrame) { lastFrame = frame; lastProgress = Time.realtimeSinceStartup; }
        }
        private void Ended(VideoPlayer source) => Finish("completed");
        private void Failed(VideoPlayer source, string error)
        {
            Debug.LogWarning("[Transition video] decoder-error=" + error);
            Finish("decoder-error");
        }
        private void Finish(string reason)
        {
            if (finished || sequenceError || sequenceComplete) return;
            if (sequence != null)
            {
                if (reason != "completed")
                {
                    sequenceError = true;
                    ReleaseVideo();
                    ShowMessage("No se pudo reproducir el video:\n" + sequence[sequenceIndex]
                        + "\n\nPulsa R para reintentar.");
                    Debug.LogError("[Campaign video] " + sequence[sequenceIndex] + ": " + reason);
                    return;
                }
                sequenceIndex++;
                ReleaseVideo();
                if (sequenceIndex < sequence.Length)
                {
                    RetrySequenceClip();
                    return;
                }
                sequenceComplete = true;
                waitStarted = Time.realtimeSinceStartup;
                ShowMessage("Esperando al resto de la tripulación...");
                sequenceFinished?.Invoke();
                return;
            }
            finished = true;
            fadeStart = Time.realtimeSinceStartup;
            if (player != null) player.Pause();
            Debug.Log("[Transition video] stage=closing reason=" + reason);
        }
        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (sequence != null && (campaignRunner == null || !campaignRunner.IsRunning))
            {
                Destroy(gameObject);
                return;
            }
            if (!covering)
            {
                if (now < coverAt) return;
                covering = true;
                group.alpha = 1;
                group.blocksRaycasts = true;
                waitStarted = now;
            }
            if (sequenceError)
            {
                if (UnityEngine.InputSystem.Keyboard.current != null
                    && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame) RetrySequenceClip();
                return;
            }
            if (showingCredits)
            {
                UpdateCreditsCrawl();
                return;
            }
            if (sequenceComplete && !finished)
            {
                if (destinationIndex >= 0 && SceneReady())
                {
                    if (message != null) message.gameObject.SetActive(false);
                    finished = true;
                    fadeStart = now;
                }
                else return;
            }
            if (!finished)
            {
                if (!preparing && (sequence != null || SceneReady())) PrepareVideo();
                else if (!preparing && now - waitStarted > 60) Finish("scene-timeout");
                else if (preparing && !playing && now - waitStarted > 12) Finish("prepare-timeout");
                else if (playing && now - lastProgress > 6) Finish("playback-stalled");
                else if (playing && now - playbackStarted > (float)clip.length + 10) Finish("playback-timeout");
            }
            if (fadeStart < 0) return;
            group.alpha = 1 - Mathf.Clamp01((now - fadeStart) / Mathf.Max(.01f, fadeOutSeconds));
            if (group.alpha <= 0) Destroy(gameObject);
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private void OnDestroy()
        {
            if (current == this) current = null;
            ReleaseVideo();
            if (spaceTexture != null) Destroy(spaceTexture);
            if (particleSprite != null) Destroy(particleSprite);
            if (particleTexture != null) Destroy(particleTexture);
        }
    }
}
