using UnityEngine;

[RequireComponent(typeof(WorkZone))]
public class ZoneModule: MonoBehaviour
{
    protected enum FaultState { None, Awaiting, Repairing, Penalized }

    [Header("Cycle")]
    [Tooltip("Раз на скільки ігрових годин блок витрачає/виробляє ресурси (Hour % cycleHours == 0)")]
    [SerializeField, Min(1)] protected int cycleHours = 4;

    [Header("Resources per cycle (відсоткові пункти від максимуму)")]
    [SerializeField] protected float energyUse;
    [SerializeField] protected float oxygenUse;
    [SerializeField] protected float energyProduced;
    [SerializeField] protected float oxygenProduced;

    [Header("Repair hours")]
    [SerializeField] float repairHoursEngineer = 4f;
    [SerializeField] float repairHoursOther = 8f;

    [Header("Event")]
    [Tooltip("id події з EventInfo для цього блоку (-1 = немає)")]
    [SerializeField] protected int eventId = -1;
    [Tooltip("Номер відповіді (1 або 2), яка означає «ремонтувати»")]
    [SerializeField, Range(1, 2)] protected int repairChoiceNumber = 1;

    [Header("Visuals")]
    [SerializeField] protected Animator animator;

    protected WorkZone zone;
    protected FaultState fault = FaultState.None;
    protected float energyProductionMul = 1f;
    protected float oxygenProductionMul = 1f;

    // ---------- Життєвий цикл ----------

    protected virtual void Reset() { }

    protected virtual void Awake() => zone = GetComponent<WorkZone>();

    protected virtual void Start()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourTick += HandleHourTick;
            TimeManager.Instance.OnNewDay += HandleNewDay;
        }
        if (EventManager.Instance != null)
        {
            EventManager.Instance.EventShown += HandleEventShown;
            EventManager.Instance.ChoiceSelected += HandleChoiceSelected;
        }
        if (WorkManager.Instance != null)
            WorkManager.Instance.OnOrdersClosed += HandleOrdersClosed;

        zone.OnWorkApplied += HandleWorkApplied;
    }

    protected virtual void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourTick -= HandleHourTick;
            TimeManager.Instance.OnNewDay -= HandleNewDay;
        }
        if (EventManager.Instance != null)
        {
            EventManager.Instance.EventShown -= HandleEventShown;
            EventManager.Instance.ChoiceSelected -= HandleChoiceSelected;
        }
        if (WorkManager.Instance != null)
            WorkManager.Instance.OnOrdersClosed -= HandleOrdersClosed;

        if (zone != null) zone.OnWorkApplied -= HandleWorkApplied;
    }

    // ---------- Години, цикл, дні ----------

    void HandleHourTick(int day, int hour)
    {
        OnHour(day, hour);
        if (hour % cycleHours == 0) OnCycle();
    }

    protected virtual void OnHour(int day, int hour) { }

    protected virtual void OnCycle() => ApplyBaseCycle();

    void HandleNewDay(int day)
    {
        if (fault == FaultState.Penalized) OnPenaltyDay();
    }

    protected virtual void OnPenaltyDay() { }

    /// <summary>Базові витрати/вироблення блоку за один цикл.</summary>
    protected void ApplyBaseCycle()
    {
        float energy = energyProduced * energyProductionMul - energyUse;
        float oxygen = oxygenProduced * oxygenProductionMul - oxygenUse;
        if (energy != 0f) AddEnergy(energy);
        if (oxygen != 0f) AddOxygen(oxygen);
    }

    // ---------- Події ----------

    protected virtual void HandleEventShown(EventInfo e)
    {
        if (eventId < 0 || e.eventID != eventId) return;
        BeginFault();
    }

    protected virtual void HandleChoiceSelected(EventInfo e, EventChoice c)
    {
        if (eventId < 0 || e.eventID != eventId) return;
        if (ChoiceNumber(e, c) != repairChoiceNumber) DeclineRepair();
    }

    protected static int ChoiceNumber(EventInfo e, EventChoice c) => e.choices.IndexOf(c) + 1;

    // ---------- Поломка з відстрочкою до наступних наказів ----------

    protected void BeginFault()
    {
        if (fault != FaultState.None) return;
        fault = FaultState.Awaiting;
        zone.Break();
        OnFaultBegan();
    }

    protected void DeclineRepair()
    {
        if (fault == FaultState.Awaiting) Penalize();
    }

    void Penalize()
    {
        fault = FaultState.Penalized;
        OnPenalty();
    }

    void HandleOrdersClosed()
    {
        if (fault != FaultState.Awaiting) return;

        if (HasAssignedWorker()) fault = FaultState.Repairing;
        else Penalize();
    }

    protected virtual bool HasAssignedWorker() =>
        Worker.All.Exists(w => w != null && w.CurrentZone == zone);

    void HandleWorkApplied(WorkZone z, WorkType work)
    {
        if (work == WorkType.Repair) OnRepairApplied();
    }

    protected virtual void OnRepairApplied()
    {
        if (fault == FaultState.None) return;

        bool wasPenalized = fault == FaultState.Penalized;
        fault = FaultState.None;
        OnFaultRepaired(wasPenalized);
    }

    protected virtual void OnFaultBegan() { }
    protected virtual void OnPenalty() { }
    protected virtual void OnFaultRepaired(bool wasPenalized) { }

    // ---------- Правила роботи ----------

    public virtual bool CanWorkerDo(Worker w, WorkType task) => true;

    public virtual float AdjustTaskHours(Worker w, WorkType task, float defaultHours)
    {
        if (task == WorkType.Repair)
            return w.Role == WorkerRole.Engineer ? repairHoursEngineer : repairHoursOther;
        return defaultHours;
    }

    // ---------- Допоміжне ----------

    protected static void AddEnergy(float percentPoints)
    {
        var rm = ResourceManager.Instance;
        if (rm != null) rm.ChangePercent(ResourceType.Energy, percentPoints);
    }

    protected static void AddOxygen(float percentPoints)
    {
        var rm = ResourceManager.Instance;
        if (rm != null) rm.ChangePercent(ResourceType.Oxygen, percentPoints);
    }

    protected void SetBool(string param, bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(param))
            animator.SetBool(param, value);
    }
}
