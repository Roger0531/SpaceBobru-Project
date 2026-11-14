using System;
using System.Collections.Generic;
using UnityEngine;

public enum ResourceType { Energy, Oxygen, Food }

[Serializable]
public class Resource
{
    public ResourceType type;
    public float max = 100f;
    public float value = 100f;
    public float changePerHour = 0f;
    public float perCrewPerHour = 0f;
}

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    [SerializeField]
    List<Resource> resources = new()
    {
        new Resource { type = ResourceType.Energy, max = 100f, value = 100f, changePerHour = -2f },
        new Resource { type = ResourceType.Oxygen, max = 100f, value = 100f, perCrewPerHour = 1f },
        new Resource { type = ResourceType.Food,   max = 10f,  value = 10f },
    };

    [SerializeField] float eatEveryHours = 24f;
    [SerializeField] float foodPerMeal = 1f;
    [SerializeField] float starveDamage = 10f;

    public event Action<ResourceType, float, float> OnResourceChanged;
    public event Action<ResourceType> OnResourceEmpty;
    public event Action<Worker> OnAte;
    public event Action<Worker> OnStarved;

    readonly Dictionary<Worker, float> hoursSinceMeal = new();
    readonly HashSet<Worker> hungry = new();

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
        int crew = Worker.All.Count;

        foreach (var r in resources)
        {
            float delta = r.changePerHour - r.perCrewPerHour * crew;
            if (delta != 0f) Change(r.type, delta);
        }

        foreach (var w in Worker.All)
            UpdateMeal(w);
    }

    void UpdateMeal(Worker w)
    {
        hoursSinceMeal.TryGetValue(w, out float hours);
        hours += 1f;

        if (hours >= eatEveryHours)
        {
            hours -= eatEveryHours;

            if (TrySpend(ResourceType.Food, foodPerMeal))
            {
                hungry.Remove(w);
                OnAte?.Invoke(w);
            }
            else
            {
                hungry.Add(w);
                w.ChangeHp(-starveDamage);
                OnStarved?.Invoke(w);
            }
        }

        hoursSinceMeal[w] = hours;
    }

    public void Change(ResourceType type, float amount)
    {
        var r = Get(type);
        if (r == null) return;

        r.value = Mathf.Clamp(r.value + amount, 0f, r.max);
        OnResourceChanged?.Invoke(type, r.value, r.max);

        if (r.value <= 0f && amount < 0f)
            OnResourceEmpty?.Invoke(type);
    }

    public bool TrySpend(ResourceType type, float amount)
    {
        var r = Get(type);
        if (r == null || r.value < amount) return false;
        Change(type, -amount);
        return true;
    }

    public Resource Get(ResourceType type) => resources.Find(r => r.type == type);

    public float GetValue(ResourceType type)
    {
        var r = Get(type);
        return r != null ? r.value : 0f;
    }

    public bool IsFull(ResourceType type)
    {
        var r = Get(type);
        return r != null && r.value >= r.max;
    }

    public bool IsHungry(Worker w) => hungry.Contains(w);
}