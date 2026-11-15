using System;
using TMPro;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Time")]
    [SerializeField] float secondsPerHour = 10f;
    [SerializeField] int StartDay = 1;
    [SerializeField] int StartHour = 6;
    [SerializeField] bool startPaused = false;

    [Header("Day schedule")]
    [Tooltip("Година підйому: починається робочий день, усі збираються на базі")]
    [SerializeField, Range(0, 23)] int wakeHour = 0;
    [Tooltip("Година відбою: робочий день закінчується, усі повертаються на базу, вмикається захист")]
    [SerializeField, Range(0, 23)] int bedtimeHour = 16;

    [Header("Clock display")]
    [Tooltip("Animator годинника (об'єкт Time): стани названі числом години, 0, 1, 2 ... 23")]
    [SerializeField] Animator timeAnimator;
    [Tooltip("Годинник змінюється й тоді, коли гру заморожено (Time.timeScale = 0)")]
    [SerializeField] bool animateWhilePaused = true;

    public int Day { get; private set; }
    public int Hour { get; private set; }
    public bool IsPaused { get; private set; }

    public int WakeHour => wakeHour;
    public int BedtimeHour => bedtimeHour;

    public float HourProgress => timer / secondsPerHour;

    public event Action<int, int> OnHourTick;
    public event Action<int> OnNewDay;
    public event Action OnWake;       // година підйому
    public event Action OnBedtime;    // година відбою
    public event Action<bool> OnPauseChanged;

    float timer;
    int shownHour = -1;   // остання година, показана на годиннику

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Day = StartDay;
        Hour = StartHour;
        IsPaused = startPaused;
    }

    void Start()
    {
        if (animateWhilePaused && timeAnimator != null)
            timeAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        RefreshClock();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (IsPaused) return;

        timer += Time.deltaTime;
        while (timer >= secondsPerHour && !IsPaused)
        {
            timer -= secondsPerHour;
            AdvanceHour();
        }
    }

    // годинник оновлюємо раз на кадр; якщо Animator був вимкнений, спробуємо ще раз
    void LateUpdate() => RefreshClock();

    void AdvanceHour()
    {
        Hour++;
        if (Hour >= 24)
        {
            Hour = 0;
            Day++;
            OnNewDay?.Invoke(Day);
        }

        OnHourTick?.Invoke(Day, Hour);

        // відбій і підйом обробляються ПІСЛЯ звичайного тіку години,
        // щоб робота, яка завершується саме о відбої, встигла завершитись
        if (Hour == bedtimeHour) OnBedtime?.Invoke();
        if (Hour == wakeHour) OnWake?.Invoke();
    }

    // ---------- Розклад дня ----------

    /// <summary>Чи робочий час у цю годину (між підйомом і відбоєм).</summary>
    public bool IsWorkHour(int hour) =>
        wakeHour <= bedtimeHour
            ? hour >= wakeHour && hour < bedtimeHour
            : hour >= wakeHour || hour < bedtimeHour;

    /// <summary>Скільки годин лишилось до відбою (0, якщо зараз не робочий час).</summary>
    public int HoursUntilBedtime(int hour)
    {
        if (!IsWorkHour(hour)) return 0;
        int left = (bedtimeHour - hour + 24) % 24;
        return left == 0 ? 24 : left;
    }

    // ---------- Годинник ----------

    void RefreshClock()
    {
        if (timeAnimator == null || !timeAnimator.isActiveAndEnabled) return;
        if (Hour == shownHour) return;   // година та сама: анімацію не перезапускаємо

        shownHour = Hour;
        timeAnimator.Play(Hour.ToString(), 0, 0f);
    }

    // ---------- Пауза ----------

    public void Pause() => SetPaused(true);
    public void Resume() => SetPaused(false);
    public void TogglePause() => SetPaused(!IsPaused);

    void SetPaused(bool value)
    {
        if (IsPaused == value) return;
        IsPaused = value;

        OnPauseChanged?.Invoke(IsPaused);
    }
}
