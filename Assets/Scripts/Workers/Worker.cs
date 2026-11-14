using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorkerRole { Engineer = 1, Medic = 2, Scientist = 3 }

public abstract class Worker : MonoBehaviour
{
    public static readonly List<Worker> All = new();

    [SerializeField] string displayName = "Worker";
    [SerializeField] float maxHp = 100f;
    [SerializeField] float hp = 3f;
    [SerializeField] int startZoneId = -1;

    [SerializeField, Range(0, 23)] int shiftStart = 6;
    [SerializeField, Range(0, 24)] int shiftEnd = 22;

    public abstract WorkerRole Role { get; }
    public int Id => (int)Role;
    public string DisplayName => displayName;
    public float Hp => hp;
    public float MaxHp => maxHp;
    public bool IsCritical => hp <= 0f;
    public WorkZone CurrentZone { get; private set; }

    public float Efficiency => IsCritical ? 0f : (hp < maxHp * 0.5f ? 0.5f : 1f);

    public abstract float GetWorkHours(WorkType work);

    public float CurrentTaskHours
    {
        get
        {
            if (CurrentZone == null || Efficiency <= 0f) return 0f;
            var task = CurrentZone.CurrentTask;
            if (task == WorkType.None) return 0f;
            return Mathf.Max(0.1f, GetWorkHours(task)) / Efficiency;
        }
    }

    public float WorkProgress
    {
        get
        {
            float hours = CurrentTaskHours;
            return hours > 0f ? Mathf.Clamp01(progressHours / hours) : 0f;
        }
    }

    public event Action<Worker> OnHpChanged;
    public event Action<Worker, WorkZone> OnZoneChanged;
    public event Action<Worker, WorkType> OnWorkDone;

    float progressHours;
    WorkType progressTask = WorkType.None;

    public bool IsOnShift(int hour) =>
        shiftStart <= shiftEnd
            ? hour >= shiftStart && hour < shiftEnd
            : hour >= shiftStart || hour < shiftEnd;


    public static Worker FindById(int id) => All.Find(w => w.Id == id);

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    protected virtual void Start()
    {
        TimeManager.Instance.OnHourTick += HandleHourTick;
        if (startZoneId >= 0) AssignToZone(startZoneId);
    }

    protected virtual void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourTick -= HandleHourTick;
    }

    void HandleHourTick(int day, int hour)
    {
        if (CurrentZone == null || IsCritical) return;
        if (!IsOnShift(hour)) return;

        var task = CurrentZone.CurrentTask;
        if (task == WorkType.None)
        {
            ResetProgress();
            return;
        }


        if (task != progressTask)
        {
            progressTask = task;
            progressHours = 0f;
        }

        progressHours += 1f;

        float hours = CurrentTaskHours;
        while (hours > 0f && progressHours >= hours)
        {
            progressHours -= hours;
            CurrentZone.ApplyWork(task);
            OnWorkDone?.Invoke(this, task);
        }
    }

    void ResetProgress()
    {
        progressHours = 0f;
        progressTask = WorkType.None;
    }


    public void AssignToZone(int zoneId) => TryAssignToZone(zoneId);

    public bool TryAssignToZone(int zoneId)
    {
        var zone = WorkZone.FindById(zoneId);
        if (zone == null)
        {
            Debug.LogWarning($"{displayName}: zone with id {zoneId} not found");
            return false;
        }
        if (zone == CurrentZone) return true;
        if (zone.IsFull)
        {
            Debug.LogWarning($"{displayName}: zone '{zone.DisplayName}' is full");
            return false;
        }

        CurrentZone = zone;
        ResetProgress();
        OnZoneChanged?.Invoke(this, zone);
        return true;
    }

    public void Unassign()
    {
        CurrentZone = null;
        ResetProgress();
        OnZoneChanged?.Invoke(this, null);
    }

    public void ChangeHp(float amount)
    {
        hp = Mathf.Clamp(hp + amount, 0f, maxHp);
        OnHpChanged?.Invoke(this);
    }
}