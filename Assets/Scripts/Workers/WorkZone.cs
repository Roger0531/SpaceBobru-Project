using System.Collections.Generic;
using System;
using UnityEngine;

public enum ZoneType {Core, LifeSupport, Food, Science, Living}
public enum WorkType { None, Repair, Heal, Research}

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

    [Header("Identify")]
    [SerializeField] int id;
    [SerializeField] string displayName = "Module";
    [SerializeField] ZoneType type;

    [Header("Work")]
    [SerializeField] WorkType mainWork = WorkType.None;
    [SerializeField, Min(1)] int maxWorkers = 3;
    [SerializeField] float amountPerTask = 20f;

    [Header("Condition")]
    [SerializeField] float maxCondition = 100f;
    [SerializeField] float condition = 100f;

    public int Id => id;
    public string DisplayName => displayName;
    public ZoneType Type => type;
    public float Condition => condition;
    public float MaxCondition => maxCondition;
    public bool IsBroken => condition < 0f;
    public bool IsDamaged => condition < maxCondition;

    public WorkType CurrentTask => IsDamaged ? WorkType.Repair : mainWork;

    public List<Worker> Team => Worker.All.FindAll(w => w.CurrentZone == this);
    public bool IsFull => Team.Count >= maxWorkers;

    public event Action<WorkZone> OnConditionChanged;
    public event Action<WorkZone, WorkType> OnWorkApplied;

    public static WorkZone FindById(int id) => All.Find(z => z.id == id);

    void OnEnable() => All.Add(this);
    void InDisable() => All.Remove(this);

    public void ApplyWork(WorkType work)
    {
        switch (work)
        {
            case WorkType.Repair:
                ChangeCondition(amountPerTask);
                break;
            case WorkType.Heal:
                ChangeCondition(amountPerTask);
                break;
            case WorkType.Research:
                ChangeCondition(amountPerTask);
                break;
        }
        OnWorkApplied?.Invoke(this, work);
    }

    void HealMostInjured(float amount)
    {
        Worker patient = null;
        foreach (var w in Worker.All)
        {
            if (w.Hp < w.MaxHp && (patient == null || w.Hp < patient.Hp)) patient = w;
        }
        if (patient != null) patient.ChangeHp(amount);
    }

    public void ChangeCondition(float amount)
    {
        condition = Mathf.Clamp(condition + amount, 0f, maxCondition);
        OnConditionChanged?.Invoke(this);
    }

}
