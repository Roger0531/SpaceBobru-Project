using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("Екран")]
    [SerializeField] GameObject gameOverPanel;

    [Header("Що темніє (в однаковому порядку)")]
    [SerializeField] SpriteRenderer[] targets;   // HarchMod, RozpodBlock, Rover
    [SerializeField] Sprite[] darkSprites;       // HarchMod_1, RozpodBlock_1, Rover_1

    [Header("Умови програшу")]
    [SerializeField] float stormOxygenNeeded = 72f;
    [SerializeField] float stormEnergyNeeded = 90f;

    public bool IsOver { get; private set; }

    void Awake() => Instance = this;

    void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        ResourceManager.Instance.OnResourceEmpty += OnResourceEmpty;
        TimeManager.Instance.OnHourTick += OnHourTick;
        if (StormManager.Instance != null) StormManager.Instance.OnStormChanged += OnStormChanged;
    }

    void OnDestroy()
    {
        if (ResourceManager.Instance != null) ResourceManager.Instance.OnResourceEmpty -= OnResourceEmpty;
        if (TimeManager.Instance != null) TimeManager.Instance.OnHourTick -= OnHourTick;
        if (StormManager.Instance != null) StormManager.Instance.OnStormChanged -= OnStormChanged;
    }

    float Get(ResourceType t) => ResourceManager.Instance.GetValue(t);

    void OnResourceEmpty(ResourceType type)
    {
        if (type == ResourceType.Oxygen) Trigger("Закінчився кисень");
    }

    void OnHourTick(int day, int hour)
    {
        if (Worker.All.Count > 0 && Worker.All.TrueForAll(w => w.IsCritical))
            Trigger("Загинув увесь екіпаж");
    }

    void OnStormChanged(bool started)
    {
        if (started && (Get(ResourceType.Oxygen) < stormOxygenNeeded ||
                        Get(ResourceType.Energy) < stormEnergyNeeded))
            Trigger("База не пережила бурю");
    }

    [ContextMenu("Test Game Over")]
    void TestGameOver() => Trigger("Тест");

    public void Trigger(string reason)
    {
        if (IsOver) return;
        IsOver = true;
        Debug.Log("GAME OVER: " + reason);

        TimeManager.Instance.Pause();

        for (int i = 0; i < targets.Length && i < darkSprites.Length; i++)
        {
            if (targets[i] == null) continue;
            var anim = targets[i].GetComponent<Animator>();
            if (anim != null) anim.enabled = false;
            targets[i].sprite = darkSprites[i];
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}