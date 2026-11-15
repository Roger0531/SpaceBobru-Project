using System;
using System.Collections.Generic;
using UnityEngine;
public class WorkManager: MonoBehaviour
{
    public static WorkManager Instance { get; private set; }

    [Header("Switch")]
    [SerializeField] bool workEnabled;

    [Header("Orders")]
    [Tooltip("Через скільки ігрових годин настає час надавати нові накази")]
    [SerializeField, Min(1)] int orderIntervalHours = 4;
    [Tooltip("Окрім TimeManager ставить на паузу все інше через Time.timeScale = 0")]
    [SerializeField] bool freezeWholeGame = true;

    [Header("Skills")]
    [Tooltip("Характеристика працівника, при якій робота триває рівно стільки, скільки вказано в зоні")]
    [SerializeField, Min(0.1f)] float baseSkillHours = 4f;

    int hoursSinceOrders;
    bool subscribed;
    bool wasTimePausedBefore;
    float prevTimeScale = 1f;

    public bool WorkEnabled => workEnabled;
    public bool OrdersOpen { get; private set; }
    public float BaseSkillHours => baseSkillHours;
    public int HoursUntilNextOrders => Mathf.Max(0, orderIntervalHours - hoursSinceOrders);

    public event Action<bool> OnWorkEnabledChanged;
    /// <summary>Настав час наказів (гра вже на паузі).</summary>
    public event Action OnOrdersTime;
    /// <summary>Панель наказів треба показати. Список: працівники, яким можна надати наказ.</summary>
    public event Action<List<Worker>> OnOrdersOpened;
    /// <summary>Накази підтверджено, гра продовжується.</summary>
    public event Action OnOrdersClosed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (Instance != this) return;
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

        // щоб гра не лишилась замороженою після перезавантаження сцени
        if (OrdersOpen && freezeWholeGame)
            Time.timeScale = prevTimeScale <= 0f ? 1f : prevTimeScale;

        if (Instance == this) Instance = null;
    }

    void HandleHourTick(int day, int hour)
    {
        if (!workEnabled || OrdersOpen) return;

        hoursSinceOrders++;
        if (hoursSinceOrders >= orderIntervalHours)
        {
            hoursSinceOrders = 0;
            OpenOrders();
        }
    }

    public void SetWorkEnabled(bool value)
    {
        if (workEnabled == value) return;
        workEnabled = value;
        if (value) hoursSinceOrders = 0;
        OnWorkEnabledChanged?.Invoke(value);
    }

    // ---- Панель наказів і пауза ----

    public void OpenOrders()
    {
        if (OrdersOpen) return;
        OrdersOpen = true;

        var time = TimeManager.Instance;
        wasTimePausedBefore = time != null && time.IsPaused;
        if (time != null) time.Pause();

        if (freezeWholeGame)
        {
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        var available = new List<Worker>();
        GetAvailableWorkers(available);

        OnOrdersTime?.Invoke();
        OnOrdersOpened?.Invoke(available);
    }

    public void CloseOrders()
    {
        if (!OrdersOpen) return;
        OrdersOpen = false;

        if (freezeWholeGame)
            Time.timeScale = prevTimeScale <= 0f ? 1f : prevTimeScale;

        // знімаємо паузу лише якщо гра не була на паузі до відкриття панелі
        if (TimeManager.Instance != null && !wasTimePausedBefore)
            TimeManager.Instance.Resume();

        OnOrdersClosed?.Invoke();
    }

    // ---- Накази ----

    /// <summary>Працівник, який почав роботу і не завершив її, наказів не отримує.</summary>
    public bool IsAvailableForOrders(Worker w) =>
        w != null && w.isActiveAndEnabled && !w.IsBusy;

    public void GetAvailableWorkers(List<Worker> result)
    {
        result.Clear();
        foreach (var w in Worker.All)
            if (IsAvailableForOrders(w)) result.Add(w);
    }

    /// <summary>Відправляє працівника в зону й миттєво переносить його в координати зони.</summary>
    public bool TryGiveOrder(Worker w, int zoneId)
    {
        if (!IsAvailableForOrders(w)) return false;
        return w.TryAssignToZone(zoneId, teleport: true);
    }
}
