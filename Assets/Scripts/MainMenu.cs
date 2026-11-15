using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] string gameSceneName = "SampleScene";
    [SerializeField] AudioSource music;
    [SerializeField] float fadeOutSeconds = 1f;

    bool starting;

    void Start()
    {
        Time.timeScale = 1f;
        if (music != null && !music.isPlaying) music.Play();
    }

    public void StartGame()
    {
        if (starting) return;
        starting = true;
        StartCoroutine(FadeAndLoad());
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    IEnumerator FadeAndLoad()
    {
        if (music != null && fadeOutSeconds > 0f)
        {
            float startVolume = music.volume;
            float t = 0f;
            while (t < fadeOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                music.volume = Mathf.Lerp(startVolume, 0f, t / fadeOutSeconds);
                yield return null;
            }
        }

        SceneManager.LoadScene(gameSceneName);
    }
}
