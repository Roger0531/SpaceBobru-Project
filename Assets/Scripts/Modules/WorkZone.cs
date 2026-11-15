using System;
using System.Collections.Generic;
using UnityEngine;

//3) додати до типів роботу solar panel(тут відбувається чистка сонячних панелей) і також energy(тут відбувається ремонт кабеля) також треба 
//public enum ZoneType { DistributionBlock, LifeSupport, Food, Science, Living }
public enum ZoneType { Core, LifeSupport, Food, Science, Living, Solar }
public enum WorkType { None, Repair, Heal, Research, Produce }

/// <summary>
/// Старий лічильник очок досліджень. Більше нічого не нараховує
/// (дослідження тепер через ResearchTree). Можна видалити, якщо його ніде не використовують.
/// </summary>
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
    public WorkType SecondaryWork => secondaryWork;
    [Tooltip("Скільки людей потрібно для лікування (Heal)")]
    [SerializeField, Min(1)] int healWorkersRequired = 1;
    [Tooltip("Only for Main Work = Produce: which resource this zone produces")]
    [SerializeField] ResourceType producedResource = ResourceType.Food;
    [SerializeField, Min(1)] int maxWorkers = 3;
    public int MaxWorkers => maxWorkers;
    [Tooltip("Result of one finished unit of work (produced units)")]
    [SerializeField] float amountPerTask = 20f;
    [Tooltip("Час одного циклу роботи (години) для працівника з характеристикою = 4. Якщо характеристика 8, то вдвічі довше. Дослідження сюди не входять: вони йдуть поза зоною (ResearchTree).")]
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
    [Tooltip("Скільки HP повертає одне завершене лікування")]
    [SerializeField, Min(0.1f)] float healHp = 3f;

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
    public WorkType GetTaskFor(Worker w)
    {
        if (!WorkEnabledOk) return WorkType.None;
        if (IsDamaged) return WorkType.Repair;

        WorkType work = ChosenWork(w);
        if (work == WorkType.Heal) return IsHealer(w) ? WorkType.Heal : WorkType.None;
        if (work == WorkType.Produce && IsStorageFull) return WorkType.None;

        // дослідження виконуються поза зоною (ResearchTree): у зоні такої роботи немає
        if (work == WorkType.Research) return WorkType.None;

        return work;
    }

    /// <summary>Яку з двох робіт зони виконує працівник: за наказом (друга робота) або основну.</summary>
    WorkType ChosenWork(Worker w)
    {
        if (w != null && secondaryWork != WorkType.None && w.PreferredWork == secondaryWork)
            return secondaryWork;
        return mainWork;
    }

    // лікують ті, хто обрав лікування, але лише якщо їх достатньо і є кого лікувати
    bool IsHealer(Worker w)
    {
        if (w == null || !Worker.All.Exists(x => x.NeedsHelp)) return false;

        GetParticipants(buffer);
        bool isMember = false;
        int count = 0;
        foreach (var p in buffer)
        {
            if (ChosenWork(p) != WorkType.Heal) continue;
            if (p == w) isMember = true;
            count++;
        }
        return isMember && count >= healWorkersRequired;
    }

    /// <summary>Роботи, які можна обрати в панелі наказів.</summary>
    public void GetOrderWorks(List<WorkType> result)
    {
        result.Clear();
        if (mainWork != WorkType.None && mainWork != WorkType.Repair) result.Add(mainWork);
        if (secondaryWork != WorkType.None && secondaryWork != mainWork) result.Add(secondaryWork);
    }

    /// <summary>null, якщо героя можна відправити сюди на цю роботу, інакше причина блокування.</summary>
    public string GetOrderBlockReason(Worker hero, WorkType work)
    {
        if (work == WorkType.Repair)
            return CanWorkerDo(hero, WorkType.Repair) ? null : "wrong role for repair";

        switch (work)
        {
            case WorkType.None: return "no tasks";
            case WorkType.Produce: return IsStorageFull ? "resource full" : null;
            case WorkType.Heal: return Worker.All.Exists(x => x.NeedsHelp) ? null : "nobody to heal";
            case WorkType.Research:
                return ResearchTree.Instance != null && ResearchTree.Instance.AvailableCount() > 0
                    ? null : "nothing to research";
            default: return null;
        }
    }

    // ---- Правила від модуля ----
    public bool CanWorkerDo(Worker w, WorkType task) =>
        module == null || module.CanWorkerDo(w, task);

    public float AdjustTaskHours(Worker w, WorkType task, float hours) =>
        module == null ? hours : module.AdjustTaskHours(w, task, hours);

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

    // ---- Результат роботи ----
    public void ApplyWork(WorkType work, Worker by = null)
    {
        switch (work)
        {
            case WorkType.Repair:
                RepairFully();
                break;
            case WorkType.Heal:
                HealPatient(healHp);
                break;
            case WorkType.Produce:
                if (ResourceManager.Instance != null)
                {
                    float bonus = module != null ? module.ProductionBonus : 0f;
                    ResourceManager.Instance.Change(producedResource, amountPerTask + bonus);
                }
                break;
        }
        OnWorkApplied?.Invoke(this, work);
    }

    void HealPatient(float amount)
    {
        Worker patient = null;
        foreach (var w in Worker.All)
        {
            if (w.NeedsHelp && (patient == null || w.Hp < patient.Hp))
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