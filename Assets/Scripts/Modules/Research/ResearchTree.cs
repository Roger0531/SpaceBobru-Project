using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ResearchEffectKind
{
    None,
    LifeSupportOxygen,       // + кисню за цикл
    FoodOxygenPerPerson,     // - витрати кисню на людину
    FoodYield,               // + їжі за одне виробництво
    LivingRestEnergy,        // - енергії на захист під час відпочинку
    SolarEnergy,             // + енергії за цикл
    SolarBreakdownChance     // - шанс поломки (відсоткові пункти)
}
public class ResearchTree: MonoBehaviour
{
    public static ResearchTree Instance { get; private set; }

    [Tooltip("Час залежить від навички героя (4 = вказаний час, 8 = вдвічі довше). Вимкніть, щоб було однаково для всіх.")]
    [SerializeField] bool applyWorkerSkill = true;

    class ActiveResearch
    {
        public Worker worker;
        public float remaining;   // ігрові години, що лишились
    }

    readonly List<ResearchHext> hexes = new();
    readonly HashSet<ResearchHext> done = new();
    readonly Dictionary<ResearchHext, ActiveResearch> active = new();

    bool subscribed;

    public IReadOnlyList<ResearchHext> Hexes => hexes;

    /// <summary>Герой вирушив на дослідження (можна сховати його візуально).</summary>
    public event Action<Worker, ResearchHext> OnStarted;
    /// <summary>Дослідження завершено, ефект уже діє (герой повернувся).</summary>
    public event Action<Worker, ResearchHext> OnCompleted;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // знаходимо всі соти, навіть якщо екран досліджень зараз вимкнений
        hexes.AddRange(FindObjectsByType<ResearchHext>(FindObjectsInactive.Include));

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourTick += HandleHourTick;
            subscribed = true;
        }
    }

    void OnDestroy()
    {
        if (subscribed && TimeManager.Instance != null)
            TimeManager.Instance.OnHourTick -= HandleHourTick;

        if (Instance == this) Instance = null;
    }

    // ---------- Таймер ----------

    void HandleHourTick(int day, int hour)
    {
        CleanActive();

        List<ResearchHext> finished = null;
        foreach (var kv in active)
        {
            kv.Value.remaining -= 1f;
            if (kv.Value.remaining <= 0f)
                (finished ??= new List<ResearchHext>()).Add(kv.Key);
        }

        if (finished == null) return;

        foreach (var hex in finished)
        {
            var worker = active[hex].worker;
            active.Remove(hex);
            done.Add(hex);
            OnCompleted?.Invoke(worker, hex);
        }
    }

    /// <summary>Скасовує дослідження героїв, які вибули зі строю.</summary>
    void CleanActive()
    {
        List<ResearchHext> stale = null;
        foreach (var kv in active)
        {
            if (kv.Value.worker == null || kv.Value.worker.IsCritical)
                (stale ??= new List<ResearchHext>()).Add(kv.Key);
        }
        if (stale != null)
            foreach (var h in stale) active.Remove(h);
    }

    // ---------- Стан ----------

    public bool IsDone(ResearchHext h) => done.Contains(h);

    /// <summary>Дослідження зараз виконується.</summary>
    public bool IsClaimed(ResearchHext h)
    {
        CleanActive();
        return active.ContainsKey(h);
    }

    public Worker ClaimOwner(ResearchHext h)
    {
        CleanActive();
        return active.TryGetValue(h, out var a) ? a.worker : null;
    }

    public ResearchHext GetClaim(Worker w)
    {
        if (w == null) return null;
        CleanActive();
        foreach (var kv in active)
            if (kv.Value.worker == w) return kv.Key;
        return null;
    }

    /// <summary>Герой зараз на дослідженні.</summary>
    public bool HasClaim(Worker w) => GetClaim(w) != null;

    /// <summary>Скільки досліджень виконується просто зараз.</summary>
    public int ActiveCount
    {
        get
        {
            CleanActive();
            return active.Count;
        }
    }

    public bool PrerequisitesDone(ResearchHext h)
    {
        if (h.requires == null) return true;
        foreach (var r in h.requires)
            if (r != null && !done.Contains(r)) return false;
        return true;
    }

    /// <summary>Не виконане, не виконується, усі умови виконані.</summary>
    public bool IsAvailable(ResearchHext h) =>
        !done.Contains(h) && !IsClaimed(h) && PrerequisitesDone(h);

    public int AvailableCount()
    {
        int n = 0;
        foreach (var h in hexes)
            if (IsAvailable(h)) n++;
        return n;
    }

    // ---------- Відбій ----------

    /// <summary>Чи встигне цей герой завершити дослідження до відбою (о відбої всі повертаються на базу).</summary>
    public bool FitsBeforeBedtime(Worker w, ResearchHext h) =>
        w != null && HoursFor(w, h) <= w.HoursUntilBedtime() + 0.001f;

    /// <summary>Доступне й встигає до відбою.</summary>
    public bool IsAvailableFor(Worker w, ResearchHext h) =>
        IsAvailable(h) && FitsBeforeBedtime(w, h);

    /// <summary>Скільки досліджень цей герой може почати зараз.</summary>
    public int AvailableCountFor(Worker w)
    {
        int n = 0;
        foreach (var h in hexes)
            if (IsAvailableFor(w, h)) n++;
        return n;
    }

    // ---------- Запуск ----------

    /// <summary>Відправляє героя на дослідження. false, якщо сота недоступна, герой уже зайнятий або не встигає до відбою.</summary>
    public bool StartResearch(ResearchHext h, Worker w)
    {
        if (h == null || w == null || !IsAvailable(h) || HasClaim(w)) return false;
        if (!FitsBeforeBedtime(w, h)) return false;

        active[h] = new ActiveResearch { worker = w, remaining = HoursFor(w, h) };
        OnStarted?.Invoke(w, h);
        return true;
    }

    public float HoursFor(Worker w, ResearchHext h)
    {
        if (!applyWorkerSkill || w == null) return h.hours;

        float baseSkill = WorkManager.Instance != null ? WorkManager.Instance.BaseSkillHours : 4f;
        return h.hours * w.GetWorkHours(WorkType.Research) / Mathf.Max(0.1f, baseSkill);
    }

    // ---------- Бонуси ----------

    /// <summary>Сума бонусів завершених досліджень цього типу (або найбільший, якщо takeBest).</summary>
    public float GetBonus(ResearchEffectKind kind)
    {
        float sum = 0f, best = 0f;
        bool useBest = false;

        foreach (var h in done)
        {
            if (h.effect != kind) continue;

            sum += h.value;
            best = Mathf.Max(best, h.value);
            if (h.takeBest) useBest = true;
        }
        return useBest ? best : sum;
    }
}
