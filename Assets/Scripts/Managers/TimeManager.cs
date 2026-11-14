using UnityEngine;
using System;
using System.Threading;
using UnityEngine;

public class TimeManager : MonoBehaviour
{

    public static TimeManager Instance { get; private set; }

    [SerializeField]
    float
        secondsPerHour = 10f;
    [SerializeField] int StartDay = 1;
    [SerializeField] int StartHour = 6;
    [SerializeField] bool startPaused = false;
    public int Day { get; private set; }
    public int Hour { get; private set; }
    public bool IsPaused { get; private set; }

    public float HourProgress => timer / secondsPerHour;
    public event Action<int, int> OnHourTick;
    public event Action<int> OnNewDay;
    public event Action<bool> OnPauseChanged;
    float timer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); return;
        }
        Instance = this;
        Day = StartDay;
        Hour = StartHour;
        IsPaused = startPaused;
    }

    void Update()
    {
        if (IsPaused) return;
        timer += Time.deltaTime;
        while (timer >= secondsPerHour)
        {
            timer -= secondsPerHour;
            AdvanceHour();
        }
    }

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
    }

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