using System;
using UnityEngine;

public class StormManager : MonoBehaviour
{
    public static StormManager Instance { get; private set; }

    [SerializeField] bool useSchedule = true;
    [SerializeField] int stormDay = 7;
    [SerializeField] int stormStartHour = 0;
    [SerializeField] int stormDurationHours = 24;

    public bool IsActive { get; private set; }
    public int HoursLeft { get; private set; }

    public event Action<bool> OnStormChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        TimeManager.Instance.OnHourTick += HandleHourTick;
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourTick -= HandleHourTick;
    }

    void HandleHourTick(int day, int hour)
    {
        if (IsActive)
        {
            HoursLeft--;
            if (HoursLeft <= 0) EndStorm();
            return;
        }

        if (useSchedule && day == stormDay && hour == stormStartHour)
            StartStorm();
    }

    public void StartStorm()
    {
        if (IsActive) return;
        IsActive = true;
        HoursLeft = stormDurationHours;
        Debug.Log("Solar storm started");
        OnStormChanged?.Invoke(true);
    }

    public void EndStorm()
    {
        if (!IsActive) return;
        IsActive = false;
        HoursLeft = 0;
        Debug.Log("Solar storm ended");
        OnStormChanged?.Invoke(false);
    }
}