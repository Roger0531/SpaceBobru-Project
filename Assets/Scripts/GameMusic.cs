using System.Collections;
using UnityEngine;

public class GameMusic : MonoBehaviour
{
    [SerializeField] AudioSource source;
    [SerializeField] AudioClip normalTrack;
    [SerializeField] AudioClip stormTrack;
    [SerializeField, Range(0f, 1f)] float volume = 0.6f;
    [SerializeField] float fadeSeconds = 1.5f;

    Coroutine fade;

    void Start()
    {
        source.loop = true;
        PlayWithFade(normalTrack);

        if (StormManager.Instance != null)
            StormManager.Instance.OnStormChanged += HandleStorm;
    }

    void OnDestroy()
    {
        if (StormManager.Instance != null)
            StormManager.Instance.OnStormChanged -= HandleStorm;
    }

    void HandleStorm(bool active)
    {
        var clip = active && stormTrack != null ? stormTrack : normalTrack;
        PlayWithFade(clip);
    }

    public void PlayWithFade(AudioClip clip)
    {
        if (clip == null) return;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(SwitchTrack(clip));
    }

    IEnumerator SwitchTrack(AudioClip clip)
    {
        if (source.isPlaying && source.clip != clip)
            yield return Fade(source.volume, 0f);

        if (source.clip != clip || !source.isPlaying)
        {
            source.clip = clip;
            source.volume = 0f;
            source.Play();
        }

        yield return Fade(source.volume, volume);
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
