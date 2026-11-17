using System.Collections.Generic;
using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyActionAudio
{
    public enum Cue : byte
    {
        CuernudoSwipe, CuernudoTakeoff, CuernudoLanding,
        ReksaiSwipe, ReksaiHeavySwipe, ReksaiBurrow, ReksaiEmerge,
        JungleSwipe, JungleFrost,
        VenomwingSting, VenomwingSpit, VenomwingSplash,
        WormBite, WormRelease
    }

    private static readonly Dictionary<Cue, AudioClip> Clips = new();

    public static void Play(Cue cue, Vector3 point, Scene scene, float volume = .65f)
    {
        if (SceneTransitionVideo.IsPresent || !scene.isLoaded) return;

        if (!Clips.TryGetValue(cue, out var clip))
        {
            clip = Resources.Load<AudioClip>("Audio/Monsters/" + cue);
            Clips.Add(cue, clip);
        }
        if (clip == null) return;

        var emitter = new GameObject(cue.ToString());
        SceneManager.MoveGameObjectToScene(emitter, scene);
        emitter.transform.position = point;

        var audio = emitter.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.dopplerLevel = 0f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.minDistance = 3f;
        audio.maxDistance = 28f;
        audio.clip = clip;
        audio.volume = volume;
        audio.Play();
        Object.Destroy(emitter, clip.length + .1f);
    }
}
