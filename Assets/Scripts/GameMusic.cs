using System.Collections;
using UnityEngine;

public class GameMusic : MonoBehaviour
{
    [SerializeField] AudioSource source;
    [SerializeField] AudioClip[] playlist;
    [SerializeField] AudioClip stormTrack;
    [SerializeField, Range(0f, 1f)] float volume = 0.6f;
    [SerializeField] float fadeSeconds = 1.5f;

    int index;
    bool storm;
    bool switching;
    bool waitingStart;
    Coroutine fade;

    void Start()
    {
        source.loop = false;
        source.playOnAwake = false;

        Debug.Log($"Music: playlist has {(playlist == null ? 0 : playlist.Length)} tracks");

        if (playlist != null && playlist.Length > 0)
            SwitchTo(playlist[0], loop: false);

        if (StormManager.Instance != null)
            StormManager.Instance.OnStormChanged += HandleStorm;
    }

    void OnDestroy()
    {
        if (StormManager.Instance != null)
            StormManager.Instance.OnStormChanged -= HandleStorm;
    }

    void Update()
    {
        if (storm || switching || playlist == null || playlist.Length == 0) return;
        if (waitingStart)
        {
            var clip = source.clip;
            if (source.isPlaying || clip == null || clip.loadState == AudioDataLoadState.Failed)
                waitingStart = false;
            return;
        }

        if (!source.isPlaying)
            PlayNext();
    }

    void PlayNext()
    {
        for (int i = 0; i < playlist.Length; i++)
        {
            index = (index + 1) % playlist.Length;
            if (playlist[index] != null) break;
        }

        var clip = playlist[index];
        if (clip == null) return;

        if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();

        source.clip = clip;
        source.loop = false;
        source.volume = volume;
        source.Play();
        waitingStart = true;
        Debug.Log($"Music: now playing {clip.name}");
    }

    void HandleStorm(bool active)
    {
        storm = active && stormTrack != null;

        if (storm)
            SwitchTo(stormTrack, loop: true);
        else if (playlist != null && playlist.Length > 0)
            SwitchTo(playlist[index], loop: false);
    }

    void SwitchTo(AudioClip clip, bool loop)
    {
        if (clip == null) return;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(SwitchRoutine(clip, loop));
    }

    IEnumerator SwitchRoutine(AudioClip clip, bool loop)
    {
        switching = true;

        if (source.isPlaying)
            yield return Fade(source.volume, 0f);

        if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();

        source.clip = clip;
        source.loop = loop;
        source.volume = 0f;
        source.Play();
        waitingStart = true;
        Debug.Log($"Music: now playing {clip.name}");

        yield return Fade(0f, volume);

        switching = false;
        fade = null;
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(from, to, t / fadeSeconds);
            yield return null;
        }
        source.volume = to;
    }
}