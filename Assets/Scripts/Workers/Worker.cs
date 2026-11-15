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

    /// <summary>
    /// Тривалість одного циклу роботи в годинах:
    /// час зони * (характеристика працівника / базове значення) / ефективність.
    /// Характеристика = 4 -> рівно час зони, 8 -> вдвічі довше.
    /// </summary>
    public float CurrentTaskHours
    {
        get
        {
            if (CurrentZone == null || Efficiency <= 0f) return 0f;
            var task = CurrentZone.CurrentTask;
            if (task == WorkType.None) return 0f;

            float baseSkill = WorkManager.Instance != null ? WorkManager.Instance.BaseSkillHours : 4f;
            float factor = GetWorkHours(task) / Mathf.Max(0.1f, baseSkill);
            return Mathf.Max(0.1f, CurrentZone.WorkHours * factor) / Efficiency;
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

    /// <summary>Працівник розпочав роботу і ще не завершив її.</summary>
    public bool IsBusy => progressTask != WorkType.None && progressHours > 0f;

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
        if (CurrentZone == null) return;

        // критичний стан: роботу кинуто, щоб працівник не "застряг" у статусі зайнятого
        if (IsCritical)
        {
            ResetProgress();
            return;
        }

        if (!IsOnShift(hour)) return;

        // працівник має стояти в колайдері своєї зони
        if (!CurrentZone.IsWorkerPresent(this))
        {
            ResetProgress();
            return;
        }

        // CurrentTask = None, якщо WorkManager ще не дозволив роботу
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

    /// <summary>Викликається зоною, коли працівник вийшов із колайдера.</summary>
    public void CancelWork() => ResetProgress();

    /// <summary>Миттєво переносить працівника у світову позицію.</summary>
    public void TeleportTo(Vector3 position)
    {
        position.z = transform.position.z; // 2D: зберігаємо z працівника
        transform.position = position;
        Physics.SyncTransforms();
        Physics2D.SyncTransforms();
    }

    public void AssignToZone(int zoneId) => TryAssignToZone(zoneId);

    /// <param name="teleport">Якщо true, працівник одразу переноситься в WorkPosition нової зони.</param>
    public bool TryAssignToZone(int zoneId, bool teleport = false)
    {
        var zone = WorkZone.FindById(zoneId);
        if (zone == null)
        {
            Debug.LogWarning($"{displayName}: zone with id {zoneId} not found");
            return false;
        }

        if (zone == CurrentZone)
        {
            // той самий наказ: лише переносимо, якщо працівник пішов з місця
            if (teleport) TeleportTo(zone.WorkPosition);
            return true;
        }

        if (zone.IsFull)
        {
            Debug.LogWarning($"{displayName}: zone '{zone.DisplayName}' is full");
            return false;
        }

        CurrentZone = zone;
        ResetProgress();
        if (teleport) TeleportTo(zone.WorkPosition);
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