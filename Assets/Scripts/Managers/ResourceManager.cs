using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum ResourceType { Energy, Oxygen, Food, None }

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
        new Resource { type = ResourceType.Energy, max = 100f, value = 100f },
        new Resource { type = ResourceType.Oxygen, max = 100f, value = 100f },
        new Resource { type = ResourceType.Food,   max = 10f,  value = 10f },
    };

    [Header("Meals")]
    [SerializeField] float eatEveryHours = 24f;
    [SerializeField] float foodPerMeal = 1f;
    [Tooltip("Шкала HP до 4: голодування забирає 1 пункт")]
    [SerializeField] float starveDamage = 1f;

    [Header("Energy indicator")]
    [SerializeField] Animator energyAnimator;
    [SerializeField] TextMeshProUGUI txtEnergyCount;
    [Tooltip("Стани аніматора називаються 0, 5, 10 ... 100 (крок у відсотках)")]
    [SerializeField, Min(1)] int energyAnimationStep = 5;

    [Header("Oxygen indicator")]
    [Tooltip("Текст кількості кисню у відсотках")]
    [SerializeField] TextMeshProUGUI txtOxygenCount;

    [Header("Food indicator")]
    [Tooltip("Стани аніматора називаються 0, 1, 2 ... максимум їжі")]
    [SerializeField] Animator foodAnimator;

    [Header("UI animation")]
    [Tooltip("Індикатори рухаються й тоді, коли гру заморожено (Time.timeScale = 0)")]
    [SerializeField] bool animateWhilePaused = true;

    int lastEnergyStep = -1;
    int lastFoodStep = -1;
    bool energyDirty = true;
    bool oxygenDirty = true;
    bool foodDirty = true;

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
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourTick += HandleHourTick;

        if (animateWhilePaused)
        {
            if (energyAnimator != null) energyAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (foodAnimator != null) foodAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        energyDirty = true;
        oxygenDirty = true;
        foodDirty = true;
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourTick -= HandleHourTick;

        if (Instance == this) Instance = null;
    }

    // ---------- Індикатори ----------

    // Оновлюємо раз на кадр, коли всі модулі вже внесли зміни за тік:
    // так анімація не перезапускається через проміжні значення.
    void LateUpdate()
    {
        if (energyDirty) energyDirty = !RefreshEnergyUI();
        if (oxygenDirty) oxygenDirty = !RefreshOxygenUI();
        if (foodDirty) foodDirty = !RefreshFoodUI();
    }

    /// <returns>true, якщо індикатор оновлено (або оновлювати нічого); false, якщо спробувати ще раз наступного кадру.</returns>
    bool RefreshEnergyUI()
    {
        float percent = GetPercent(ResourceType.Energy);

        // текст завжди показує точне значення, а не крок аніматора
        if (txtEnergyCount != null) txtEnergyCount.text = $"{percent:0.#}%";

        if (energyAnimator == null) return true;
        if (!energyAnimator.isActiveAndEnabled) return false;

        int step = Mathf.Clamp(Mathf.FloorToInt(percent / energyAnimationStep) * energyAnimationStep, 0, 100);
        if (step == lastEnergyStep) return true;   // стан той самий: не перезапускаємо анімацію

        lastEnergyStep = step;
        energyAnimator.Play(step.ToString(), 0, 0f);
        return true;
    }

    bool RefreshOxygenUI()
    {
        if (txtOxygenCount != null)
            txtOxygenCount.text = $"{GetPercent(ResourceType.Oxygen):0.#}%";
        return true;
    }

    bool RefreshFoodUI()
    {
        if (foodAnimator == null) return true;
        if (!foodAnimator.isActiveAndEnabled) return false;

        // стан відповідає точній кількості їжі: 0, 1, 2 ... максимум
        int step = Mathf.Max(0, Mathf.FloorToInt(GetValue(ResourceType.Food)));
        if (step == lastFoodStep) return true;

        lastFoodStep = step;
        foodAnimator.Play(step.ToString(), 0, 0f);
        return true;
    }

    // ---------- Харчування ----------

    void HandleHourTick(int day, int hour)
    {
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

    // ---------- Ресурси ----------

    public void Change(ResourceType type, float amount)
    {
        var r = Get(type);
        if (r == null) return;

        r.value = Mathf.Clamp(r.value + amount, 0f, r.max);
        OnResourceChanged?.Invoke(type, r.value, r.max);

        // індикатори оновить LateUpdate (раз на кадр)
        if (type == ResourceType.Energy) energyDirty = true;
        else if (type == ResourceType.Oxygen) oxygenDirty = true;
        else if (type == ResourceType.Food) foodDirty = true;

        if (r.value <= 0f && amount < 0f)
            OnResourceEmpty?.Invoke(type);
    }

    /// <summary>Змінює ресурс на відсоткові пункти від максимуму (для енергії й кисню 1% = 1 одиниця).</summary>
    public void ChangePercent(ResourceType type, float percentPoints)
    {
        var r = Get(type);
        if (r == null) return;
        Change(type, r.max * percentPoints / 100f);
    }

    public bool TrySpend(ResourceType type, float amount)
    {
        var r = Get(type);
        if (r == null || r.value < amount) return false;
        Change(type, -amount);
        return true;
    }

    public bool TrySpendPercent(ResourceType type, float percentPoints)
    {
        var r = Get(type);
        if (r == null) return false;
        return TrySpend(type, r.max * percentPoints / 100f);
    }

    public Resource Get(ResourceType type) => resources.Find(r => r.type == type);

    public float GetValue(ResourceType type)
    {
        var r = Get(type);
        return r != null ? r.value : 0f;
    }

    /// <summary>Скільки відсотків від максимуму зараз.</summary>
    public float GetPercent(ResourceType type)
    {
        var r = Get(type);
        return r != null && r.max > 0f ? r.value / r.max * 100f : 0f;
    }

    public bool IsFull(ResourceType type)
    {
        var r = Get(type);
        return r != null && r.value >= r.max;
    }

    public bool IsHungry(Worker w) => hungry.Contains(w);
}