using System;
using System.Collections.Generic;
using UnityEngine;

//3) додати до типів роботу solar panel(тут відбувається чистка сонячних панелей) і також energy(тут відбувається ремонт кабеля) також треба 
//public enum ZoneType { DistributionBlock, LifeSupport, Food, Science, Living }
public enum ZoneType { Core, LifeSupport, Food, Science, Living, Solar }
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

    public static bool TrySpend(float amount)
    {
        if (amount > Points) return false;
        Points -= amount;
        OnChanged?.Invoke(Points);
        return true;
    }
}

public class WorkZone : MonoBehaviour
{
    public static readonly List<WorkZone> All = new();

    [Header("Identity")]
    [SerializeField] int id;
    [SerializeField] string displayName = "Module";
    [SerializeField] ZoneType type;

    [Header("Work")]
    [SerializeField] WorkType mainWork = WorkType.None;
    [Tooltip("Друга робота, що йде одночасно з основною. Зараз підтримується лише Heal (групова робота).")]
    [SerializeField] WorkType secondaryWork = WorkType.None;
    [Tooltip("Скільки людей потрібно для лікування (Heal)")]
    [SerializeField, Min(1)] int healWorkersRequired = 1;
    [Tooltip("Only for Main Work = Produce: which resource this zone produces")]
    [SerializeField] ResourceType producedResource = ResourceType.Food;
    [SerializeField, Min(1)] int maxWorkers = 3;
    public int MaxWorkers => maxWorkers;
    [Tooltip("Result of one finished unit of work (repair / heal / research / produced units)")]
    [SerializeField] float amountPerTask = 20f;
    [Tooltip("Час одного циклу роботи (години) для працівника з характеристикою = 4. Якщо характеристика 8, то вдвічі довше.")]
    [SerializeField, Min(0.1f)] float workHours = 4f;

    [Header("Positions for workers (one per worker slot)")]
    [Tooltip("Вимкнено: усі працівники з'являються в позиції об'єкта зони.")]
    [SerializeField] bool useManualPosition = true;
    [Tooltip("Світові координати. Кількість підлаштовується під Max Workers. Мають бути всередині колайдера зони.")]
    [SerializeField] Vector3[] manualPositions = new Vector3[3];
    readonly Dictionary<Worker, int> slots = new();

    [Header("Conditions to work")]
    [SerializeField] bool requireWorkerInside = true;
    [SerializeField] bool requireWorkEnabled = true;

    [Header("Condition")]
    [SerializeField] float maxCondition = 100f;
    [SerializeField] float condition = 100f;

    readonly List<Worker> inside = new();   // у порядку входу в колайдер
    readonly List<Worker> buffer = new();
    ZoneModule module;

    public int Id => id;
    public string DisplayName => displayName;
    public ZoneType Type => type;
    public float Condition => condition;
    public float MaxCondition => maxCondition;
    public float WorkHours => workHours;
    public int HealWorkersRequired => healWorkersRequired;
    public bool IsBroken => condition <= 0f;
    public bool IsDamaged => condition < maxCondition;
    public ZoneModule Module => module;

    /// <summary>Перша позиція (для гізмо та перевірок).</summary>
    public Vector3 WorkPosition => GetSlotPosition(0);

    Vector3 GetSlotPosition(int index)
    {
        if (!useManualPosition || manualPositions == null || manualPositions.Length == 0)
            return transform.position;
        return manualPositions[Mathf.Clamp(index, 0, manualPositions.Length - 1)];
    }

    /// <summary>Резервує вільну позицію для працівника й повертає її (для телепортації).</summary>
    public Vector3 ReserveWorkPosition(Worker w)
    {
        // прибираємо позиції тих, хто вже пішов із зони
        var stale = new List<Worker>();
        foreach (var kv in slots)
            if (kv.Key == null || kv.Key.CurrentZone != this) stale.Add(kv.Key);
        foreach (var s in stale) slots.Remove(s);

        if (slots.TryGetValue(w, out int own)) return GetSlotPosition(own);

        int count = Mathf.Max(1, manualPositions != null ? manualPositions.Length : 1);
        int free = -1;
        for (int i = 0; i < count && free < 0; i++)
        {
            bool taken = false;
            foreach (var kv in slots)
                if (kv.Value == i) { taken = true; break; }
            if (!taken) free = i;
        }
        if (free < 0) free = count - 1; // слоти скінчились: ставимо в останню

        slots[w] = free;
        return GetSlotPosition(free);
    }

    bool WorkEnabledOk =>
        !requireWorkEnabled || (WorkManager.Instance != null && WorkManager.Instance.WorkEnabled);

    bool IsStorageFull =>
        ResourceManager.Instance != null && ResourceManager.Instance.IsFull(producedResource);

    public List<Worker> Team => Worker.All.FindAll(w => w.CurrentZone == this);
    public bool IsFull => Team.Count >= maxWorkers;

    public event Action<WorkZone> OnConditionChanged;
    public event Action<WorkZone, WorkType> OnWorkApplied;

    public static WorkZone FindById(int id) => All.Find(z => z.id == id);

    void Awake() => module = GetComponent<ZoneModule>();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        if (!requireWorkerInside || !useManualPosition || manualPositions == null) return;
        if (!TryGetComponent<Collider2D>(out var col)) return;

        for (int i = 0; i < manualPositions.Length; i++)
        {
            if (!col.OverlapPoint(manualPositions[i]))
                Debug.LogWarning($"{displayName}: position {i + 1} {manualPositions[i]} is outside the zone collider, " +
                                 "a worker teleported there will not count as present.", this);
        }
    }

    void OnValidate()
    {
        int n = Mathf.Max(1, maxWorkers);
        if (manualPositions == null || manualPositions.Length != n)
            Array.Resize(ref manualPositions, n);
    }

    // ---- Присутність у колайдері ----
    public int WorkersInside => inside.Count;

    public bool IsWorkerPresent(Worker w)
    {
        if (!requireWorkerInside) return true;
        inside.RemoveAll(x => x == null || !x.isActiveAndEnabled);
        return inside.Contains(w);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var w = other.GetComponentInParent<Worker>();
        if (w != null && !inside.Contains(w)) inside.Add(w);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var w = other.GetComponentInParent<Worker>();
        if (w == null) return;

        inside.Remove(w);
        if (w.CurrentZone == this) w.CancelWork();
    }

    /// <summary>Призначені сюди працівники, які реально стоять у зоні (у порядку входу).</summary>
    public void GetParticipants(List<Worker> result)
    {
        result.Clear();
        if (requireWorkerInside)
        {
            inside.RemoveAll(x => x == null || !x.isActiveAndEnabled);
            foreach (var w in inside)
                if (w.CurrentZone == this && !w.IsCritical) result.Add(w);
        }
        else
        {
            foreach (var w in Worker.All)
                if (w.CurrentZone == this && !w.IsCritical) result.Add(w);
        }
    }

    // ---- Яка робота зараз у працівника ----
    bool HasWork(WorkType t) => mainWork == t || secondaryWork == t;

    public WorkType CurrentTask => GetTaskFor(null);

    public WorkType GetTaskFor(Worker w)
    {
        if (!WorkEnabledOk) return WorkType.None;
        if (IsDamaged) return WorkType.Repair;

        // групове лікування: перші N працівників у зоні, якщо є кого лікувати
        if (HasWork(WorkType.Heal) && IsHealer(w)) return WorkType.Heal;

        WorkType task = mainWork != WorkType.Heal ? mainWork : secondaryWork;
        if (task == WorkType.Heal) return WorkType.None;
        if (task == WorkType.Produce && IsStorageFull) return WorkType.None;
        return task;
    }

    bool IsHealer(Worker w)
    {
        if (w == null || !Worker.All.Exists(x => x.Hp < x.MaxHp)) return false;

        GetParticipants(buffer);
        if (buffer.Count < healWorkersRequired) return false;

        int idx = buffer.IndexOf(w);
        return idx >= 0 && idx < healWorkersRequired;
    }

    /// <summary>Скільки працівників зараз виконують цю роботу (на зміні, мають право її виконувати).</summary>
    public int CountDoing(WorkType task)
    {
        var list = new List<Worker>();
        GetParticipants(list);

        int hour = TimeManager.Instance != null ? TimeManager.Instance.Hour : 0;
        int n = 0;
        foreach (var w in list)
        {
            if (!w.IsOnShift(hour)) continue;
            if (GetTaskFor(w) != task || !CanWorkerDo(w, task)) continue;
            n++;
        }
        return n;
    }

    // ---- Правила від модуля ----
    public bool CanWorkerDo(Worker w, WorkType task) =>
        module == null || module.CanWorkerDo(w, task);

    public float AdjustTaskHours(Worker w, WorkType task, float hours) =>
        module == null ? hours : module.AdjustTaskHours(w, task, hours);

    // ---- Результат роботи ----
    public void ApplyWork(WorkType work)
    {
        switch (work)
        {
            case WorkType.Repair:
                RepairFully();
                break;
            case WorkType.Heal:
                HealMostInjured(amountPerTask / Mathf.Max(1, healWorkersRequired));
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

    // ---- Стан ----
    public void Break() => SetCondition(0f);
    public void RepairFully() => SetCondition(maxCondition);

    public void ChangeCondition(float amount) => SetCondition(condition + amount);

    void SetCondition(float value)
    {
        condition = Mathf.Clamp(value, 0f, maxCondition);
        OnConditionChanged?.Invoke(this);
    }

    // ---- Редактор ----
    [ContextMenu("Spread positions around this transform")]
    void SpreadPositions()
    {
        useManualPosition = true;
        int n = Mathf.Max(1, maxWorkers);
        manualPositions = new Vector3[n];
        for (int i = 0; i < n; i++)
            manualPositions[i] = transform.position + new Vector3((i - (n - 1) / 2f) * 0.6f, 0f, 0f);
    }

    void OnDrawGizmosSelected()
    {
        int n = useManualPosition && manualPositions != null ? manualPositions.Length : 1;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = GetSlotPosition(i);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(p, 0.1f);
            Gizmos.DrawLine(transform.position, p);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(p + Vector3.up * 0.2f, (i + 1).ToString());
#endif
        }
    }
}