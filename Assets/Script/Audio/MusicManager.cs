using System.Collections;
using UnityEngine;

// Per-track playback settings. Plain serializable data — shows as a foldout in the inspector.
[System.Serializable]
public class MusicTrack
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("Seconds into the clip to begin playback.")]
    public float startTime = 0f;
    [Tooltip("0 = play/loop the whole clip. >0 = stop after this many seconds (loop ignored).")]
    public float duration = 0f;
    public bool loop = true;
}

public class MusicManager : MonoBehaviour
{
    // INPUT:  MusicTrack requests from SceneMusic / BossMusicCue
    // OUTPUT: exactly one music AudioSource, alive across every scene
    // USE:    never placed by hand — Instance creates it on first use

    private static MusicManager instance;
    public static MusicManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("MusicManager");
                instance = go.AddComponent<MusicManager>();   // Awake runs inside AddComponent
            }
            return instance;
        }
    }

    private AudioSource source;
    private MusicTrack current;
    private Coroutine pending;

    // INPUT:  none
    // OUTPUT: singleton enforced, persists across scenes, AudioSource created
    // USE:    Unity
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;           // 2D — no distance falloff
        source.ignoreListenerPause = true;  // keeps playing through timeScale = 0 pause/cutscene
    }

    // INPUT:  track settings, optional delay in REAL seconds (unaffected by timeScale)
    // OUTPUT: track scheduled; replaces any previously pending request
    // USE:    SceneMusic.Start, BossMusicCue.Trigger
    public void Play(MusicTrack track, float delay = 0f)
    {
        if (track == null || track.clip == null) return;
        if (pending != null) StopCoroutine(pending);
        pending = StartCoroutine(PlayRoutine(track, delay));
    }

    // INPUT:  none
    // OUTPUT: music stopped, pending request cancelled
    // USE:    silence before a cutscene, game over stingers, etc.
    public void Stop()
    {
        if (pending != null) { StopCoroutine(pending); pending = null; }
        source.Stop();
        current = null;
    }

    // INPUT:  track, delay
    // OUTPUT: waits (realtime), then starts the clip at startTime; stops after duration if set
    // USE:    Play
    private IEnumerator PlayRoutine(MusicTrack track, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        // Same clip, same offset, already playing → leave it alone (level restart, menu→menu)
        bool same = current != null && current.clip == track.clip
                    && Mathf.Approximately(current.startTime, track.startTime) && source.isPlaying;
        if (!same)
        {
            source.clip = track.clip;
            source.time = Mathf.Clamp(track.startTime, 0f, Mathf.Max(0f, track.clip.length - 0.01f));
            source.loop = track.loop && track.duration <= 0f;
            source.Play();
            current = track;
        }
        source.volume = track.volume;

        if (track.duration > 0f)
        {
            yield return new WaitForSecondsRealtime(track.duration);
            if (current == track) source.Stop();
        }
        pending = null;
    }
}