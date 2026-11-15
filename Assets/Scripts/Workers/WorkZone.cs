using System;
using System.Collections.Generic;
using UnityEngine;

//3) додати до типів роботу solar panel(тут відбувається чистка сонячних панелей) і також energy(тут відбувається ремонт кабеля) також треба 
//public enum ZoneType { DistributionBlock, LifeSupport, Food, Science, Living }
public enum WorkType { None, Repair, Heal, Research, Produce }

public static class Research
{
    public static float Points { get; private set; }
    public static event Action<float> OnChanged;

    public static void Add(float amount)
    {
        Points += amount;
        OnChanged?.Invoke(Points);
    }
}

public class WorkZone : MonoBehaviour
{
    public static readonly List<WorkZone> All = new();

    [Header("Identity")]
    [SerializeField] int id;
    [SerializeField] string displayName = "Module";
    //[SerializeField] ZoneType type;

    [Header("Work")]
    [SerializeField] WorkType mainWork = WorkType.None;
    [Tooltip("Only for Main Work = Produce: which resource this zone produces")]
    [SerializeField] ResourceType producedResource = ResourceType.Food;
    [SerializeField, Min(1)] int maxWorkers = 3;
    [Tooltip("Result of one finished unit of work (repair / heal / research / produced units)")]
    [SerializeField] float amountPerTask = 20f;
    [Tooltip("Час одного циклу роботи (години) для працівника з характеристикою = 4. Якщо характеристика 8, то вдвічі довше.")]
    [SerializeField, Min(0.1f)] float workHours = 4f;

    [Header("Position for workers")]
    [Tooltip("Увімкнено: використовувати координати нижче. Вимкнено: позиція самого об'єкта зони.")]
    [SerializeField] bool useManualPosition = true;
    [Tooltip("Світові координати, куди телепортується працівник після наказу. Мають бути всередині Trigger-колайдера зони.")]
    [SerializeField] Vector3 manualPosition;

    [Header("Conditions to work")]
    [Tooltip("Працювати лише коли працівник стоїть у колайдері зони")]
    [SerializeField] bool requireWorkerInside = true;
    [Tooltip("Працювати лише коли в WorkManager увімкнено workEnabled")]
    [SerializeField] bool requireWorkEnabled = true;

    [Header("Condition")]
    [SerializeField] float maxCondition = 100f;
    [SerializeField] float condition = 100f;

    readonly HashSet<Worker> workersInside = new();

    public int Id => id;
    public string DisplayName => displayName;
    //public ZoneType Type => type;
    public float Condition => condition;
    public float MaxCondition => maxCondition;
    public float WorkHours => workHours;
    public bool IsBroken => condition <= 0f;
    public bool IsDamaged => condition < maxCondition;

    /// <summary>Куди потрапляє працівник після наказу.</summary>
    public Vector3 WorkPosition => useManualPosition ? manualPosition : transform.position;

    /// <summary>Чи дозволив WorkManager роботу.</summary>
    bool WorkEnabledOk =>
        !requireWorkEnabled || (WorkManager.Instance != null && WorkManager.Instance.WorkEnabled);

    public WorkType CurrentTask
    {
        get
        {
            if (!WorkEnabledOk) return WorkType.None;
            if (IsDamaged) return WorkType.Repair;
            if (mainWork == WorkType.Produce && IsStorageFull) return WorkType.None;
            return mainWork;
        }
    }

    bool IsStorageFull =>
        ResourceManager.Instance != null && ResourceManager.Instance.IsFull(producedResource);

    public List<Worker> Team => Worker.All.FindAll(w => w.CurrentZone == this);
    public bool IsFull => Team.Count >= maxWorkers;

    public event Action<WorkZone> OnConditionChanged;
    public event Action<WorkZone, WorkType> OnWorkApplied;

    public static WorkZone FindById(int id) => All.Find(z => z.id == id);

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        // підказка, якщо координати лежать поза колайдером: працівник там не зможе працювати
        if (requireWorkerInside && TryGetComponent<Collider>(out var col) &&
            !col.bounds.Contains(WorkPosition))
        {
            Debug.LogWarning($"{displayName}: WorkPosition {WorkPosition} is outside the zone collider, " +
                             "workers teleported there will not count as present.", this);
        }
    }

    // ---- Присутність у колайдері ----
    public int WorkersInside => workersInside.Count;

    public bool IsWorkerPresent(Worker w)
    {
        if (!requireWorkerInside) return true;
        workersInside.RemoveWhere(x => x == null || !x.isActiveAndEnabled);
        return workersInside.Contains(w);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var w = other.GetComponentInParent<Worker>();
        if (w != null) workersInside.Add(w);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var w = other.GetComponentInParent<Worker>();
        if (w == null) return;

        workersInside.Remove(w);
        if (w.CurrentZone == this) w.CancelWork();
    }

    // ---- Результат роботи ----
    public void ApplyWork(WorkType work)
    {
        switch (work)
        {
            case WorkType.Repair:
                ChangeCondition(amountPerTask);
                break;
            case WorkType.Heal:
                HealMostInjured(amountPerTask);
                break;
            case WorkType.Research:
                Research.Add(amountPerTask);
                break;
            case WorkType.Produce:
                if (ResourceManager.Instance != null)
                    ResourceManager.Instance.Change(producedResource, amountPerTask);
                break;
        }
        OnWorkApplied?.Invoke(this, work);
    }

    void HealMostInjured(float amount)
    {
        Worker patient = null;
        foreach (var w in Worker.All)
        {
            if (w.Hp < w.MaxHp && (patient == null || w.Hp < patient.Hp))
                patient = w;
        }
        if (patient != null) patient.ChangeHp(amount);
    }

    public void ChangeCondition(float amount)
    {
        condition = Mathf.Clamp(condition + amount, 0f, maxCondition);
        OnConditionChanged?.Invoke(this);
    }

    // ---- Редактор ----
    [ContextMenu("Set position from this transform")]
    void SetPositionFromTransform()
    {
        manualPosition = transform.position;
        useManualPosition = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(WorkPosition, 0.4f);
        Gizmos.DrawLine(transform.position, WorkPosition);
    }
}