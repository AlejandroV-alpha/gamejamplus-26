using UnityEngine;

[DisallowMultipleComponent]
public sealed class b_NeptuneAudio : MonoBehaviour
{
    [Header("Assign music on Music_2D > Audio Source > Audio Resource")]
    public AudioSource musicSource;
    [Header("Wind is already assigned; these are local ambient sounds")]
    public AudioSource windSource;
    public AudioSource gustSource;
    [Header("Add ice cracking clips here to enable occasional 3D sounds")]
    public AudioClip[] iceCracks = new AudioClip[0];
    public AudioSource iceCrackSource;
    [Range(0, 1)] public float windVolume = .13f;
    [Range(0, 1)] public float gustVolume = .12f;
    [Range(0, 1)] public float crackingVolume = .3f;
    public Vector2 crackingInterval = new Vector2(24, 45);

    b_NeptuneWeather weather;
    Camera viewCamera;
    float nextCrack;

    void Start()
    {
        weather = FindFirstObjectByType<b_NeptuneWeather>();
        ScheduleCrack();
        var clip = Resources.Load<AudioClip>("Music/Neptune/MusicaAmbienteNeptuno");
        if (clip != null)
        {
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.Stop();
            musicSource.playOnAwake = false;
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.volume = 0f;
            musicSource.priority = 160;
            musicSource.Play();
        }
        // Offset the two wind loops so the gust layer does not double the same waveform.
        if (gustSource != null && gustSource.clip != null)
            gustSource.time = gustSource.clip.length * .37f;
    }

    void Update()
    {
        if (musicSource != null && musicSource.clip != null)
        {
            float targetVolume = GameJam.Multiplayer.SceneTransitionVideo.IsPresent ? 0f : .25f;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, targetVolume, Time.unscaledDeltaTime * .3f);
        }
        float gust = weather != null ? weather.GustStrength : 0;
        float smoothing = 1 - Mathf.Exp(-Time.deltaTime * 2);
        if (windSource != null)
            windSource.volume = Mathf.Lerp(windSource.volume, windVolume * (1 + gust * .35f), smoothing);
        if (gustSource != null)
            gustSource.volume = Mathf.Lerp(gustSource.volume, gustVolume * gust, smoothing);
        if (Time.time < nextCrack) return;
        ScheduleCrack();
        if (iceCrackSource == null || iceCrackSource.isPlaying || iceCracks == null || iceCracks.Length == 0) return;
        var clip = iceCracks[Random.Range(0, iceCracks.Length)];
        if (clip == null) return;
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null) return;
        var offset = Random.insideUnitCircle.normalized * Random.Range(12f, 30f);
        iceCrackSource.transform.position = viewCamera.transform.position + new Vector3(offset.x, -2, offset.y);
        iceCrackSource.pitch = Random.Range(.93f, 1.04f);
        iceCrackSource.PlayOneShot(clip, crackingVolume);
    }

    void ScheduleCrack()
    {
        float minimum = Mathf.Max(5, crackingInterval.x);
        nextCrack = Time.time + Random.Range(minimum, Mathf.Max(minimum, crackingInterval.y));
    }
}
