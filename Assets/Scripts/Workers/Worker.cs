using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorkerRole { Engineer = 1, Medic = 2, Scientist = 3 }

public abstract class Worker : MonoBehaviour
{
    public static readonly List<Worker> All = new();

    [Header("Identity")]
    [SerializeField] string displayName = "Worker";
    [SerializeField] Sprite icon;

    [Header("Health")]
    [SerializeField] float maxHp = 4f;
    [SerializeField] float hp = 4f;
    [Tooltip("Якщо HP не більше за це значення (і більше за 0), працівнику потрібна допомога")]
    [SerializeField] float helpThreshold = 1f;

    [Header("Work")]
    [SerializeField] int startZoneId = -1;
    [Tooltip("Телепортувати до стартової зони на початку гри")]
    [SerializeField] bool teleportToStartZone = true;
    //[SerializeField, Range(0, 23)] int shiftStart = 0;
    //[SerializeField, Range(0, 24)] int shiftEnd = 16;

    // ---------- Дані ----------

    public abstract WorkerRole Role { get; }
    public abstract float GetWorkHours(WorkType work);

    public int Id => (int)Role;
    public string DisplayName => displayName;
    public Sprite Icon => icon;

    public float Hp => hp;
    public float MaxHp => maxHp;
    public bool IsCritical => hp <= 0f;                          // вибув зі строю
    public bool NeedsHelp => hp > 0f && hp <= helpThreshold;     // на межі: потрібна допомога

    // ефективність від HP не залежить: працює повністю, поки не вибув зі строю
    public float Efficiency => IsCritical ? 0f : 1f;

    public WorkZone CurrentZone { get; private set; }

    /// <summary>Яку з двох робіт зони виконує працівник (лише для зон із другою роботою).</summary>
    public WorkType PreferredWork { get; private set; } = WorkType.None;

    /// <summary>Що працівник робить зараз за правилами зони.</summary>
    public WorkType CurrentTask =>
        CurrentZone != null ? CurrentZone.GetTaskFor(this) : WorkType.None;

    // ---------- Дослідження ----------

    /// <summary>Працівник на дослідженні: його немає в жодній зоні, поки воно не завершиться.</summary>
    public bool IsResearching =>
        ResearchTree.Instance != null && ResearchTree.Instance.HasClaim(this);

    /// <summary>Сота, яку працівник зараз досліджує (null, якщо немає).</summary>
    public ResearchHext CurrentResearch =>
        ResearchTree.Instance != null ? ResearchTree.Instance.GetClaim(this) : null;

    // ---------- Прогрес роботи ----------

    float progressHours;
    WorkType progressTask = WorkType.None;

    /// <summary>Працівник розпочав роботу і ще не завершив її, або перебуває на дослідженні.</summary>
    public bool IsBusy =>
        (progressTask != WorkType.None && progressHours > 0f) || IsResearching;

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
            var task = CurrentZone.GetTaskFor(this);
            return task == WorkType.None ? 0f : TaskHours(task);
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

    float TaskHours(WorkType task)
    {
        float baseSkill = WorkManager.Instance != null ? WorkManager.Instance.BaseSkillHours : 4f;
        float factor = GetWorkHours(task) / Mathf.Max(0.1f, baseSkill);
        float hours = CurrentZone.WorkHours * factor;
        hours = CurrentZone.AdjustTaskHours(this, task, hours);
        return Mathf.Max(0.1f, hours) / Efficiency;
    }

    // ---------- Події ----------

    public event Action<Worker> OnHpChanged;
    public event Action<Worker, WorkZone> OnZoneChanged;
    public event Action<Worker, WorkType> OnWorkDone;

    // ---------- Життєвий цикл ----------

    public static Worker FindById(int id) => All.Find(w => w.Id == id);

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    protected virtual void Start()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourTick += HandleHourTick;

        if (startZoneId >= 0)
            TryAssignToZone(startZoneId, teleport: teleportToStartZone);
    }

    protected virtual void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourTick -= HandleHourTick;
    }

    public bool IsOnShift(int hour)
    {
        var tm = TimeManager.Instance;
        return tm == null || tm.IsWorkHour(hour);
    }

    /// <summary>Скільки ігрових годин лишилось до відбою (0, якщо зараз не робочий час).</summary>
    public float HoursUntilBedtime()
    {
        var tm = TimeManager.Instance;
        return tm == null ? 24f : tm.HoursUntilBedtime(tm.Hour);
    }

    // ---------- Щогодинна робота ----------

    void HandleHourTick(int day, int hour)
    {
        // дослідник у відрядженні: у зонах не працює
        if (IsResearching)
        {
            ResetProgress();
            return;
        }

        if (CurrentZone == null) return;

        if (IsCritical)
        {
            ResetProgress();
            return;
        }

        if (!IsOnShift(hour)) return;

        if (!CurrentZone.IsWorkerPresent(this))
        {
            ResetProgress();
            return;
        }

        var task = CurrentZone.GetTaskFor(this);
        if (task == WorkType.None || !CurrentZone.CanWorkerDo(this, task))
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

        float hours = TaskHours(task);
        while (hours > 0f && progressHours >= hours)
        {
            progressHours -= hours;
            CurrentZone.ApplyWork(task, this);
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

    // ---------- Переміщення та призначення ----------

    /// <summary>Миттєво переносить працівника у світову позицію.</summary>
    public void TeleportTo(Vector3 position)
    {
        position.z = transform.position.z; // 2D: зберігаємо z працівника
        transform.position = position;
        Physics2D.SyncTransforms();
    }

    public void AssignToZone(int zoneId) => TryAssignToZone(zoneId);

    /// <summary>Призначення за Id зони (для стартової зони). Id мають бути унікальними.</summary>
    public bool TryAssignToZone(int zoneId, bool teleport = false, WorkType preferredWork = WorkType.None)
    {
        var zone = WorkZone.FindById(zoneId);
        if (zone == null)
        {
            Debug.LogWarning($"{displayName}: zone with id {zoneId} not found");
            return false;
        }

        return TryAssignToZone(zone, teleport, preferredWork);
    }

    /// <param name="teleport">Якщо true, працівник одразу переноситься на вільну позицію зони.</param>
    /// <param name="preferredWork">Яку з двох робіт зони виконує працівник (наприклад, лікування в науковому блоці).</param>
    public bool TryAssignToZone(WorkZone zone, bool teleport = false, WorkType preferredWork = WorkType.None)
    {
        if (zone == null) return false;

        if (zone == CurrentZone)
        {
            // та сама зона: якщо змінилась робота, скидаємо прогрес
            if (PreferredWork != preferredWork)
            {
                PreferredWork = preferredWork;
                ResetProgress();
            }
            if (teleport) TeleportTo(zone.ReserveWorkPosition(this));
            return true;
        }

        if (zone.IsFull)
        {
            Debug.LogWarning($"{displayName}: zone '{zone.DisplayName}' is full");
            return false;
        }

        CurrentZone = zone;
        PreferredWork = preferredWork;
        ResetProgress();
        if (teleport) TeleportTo(zone.ReserveWorkPosition(this));
        OnZoneChanged?.Invoke(this, zone);
        return true;
    }

    public void Unassign()
    {
        CurrentZone = null;
        PreferredWork = WorkType.None;
        ResetProgress();
        OnZoneChanged?.Invoke(this, null);
    }

    // ---------- Здоров'я ----------

    public void ChangeHp(float amount)
    {
        hp = Mathf.Clamp(hp + amount, 0f, maxHp);

        // вибув зі строю: прогрес втрачено (дослідження скасує ResearchTree на найближчій годині)
        if (IsCritical) ResetProgress();

        OnHpChanged?.Invoke(this);
    }
}