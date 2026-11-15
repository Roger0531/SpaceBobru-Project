using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EventMissionLink
{
    public string eventName;
    public string choiceText;
    public string missionId;
    public int workerId = -1;
}

public class WorkManager : MonoBehaviour
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

    [Header("Rover missions from events")]
    [Tooltip("Вибір у події відкриває місію ровера. workerId = -1: людину обирають у панелі наказів; 1/2/3: їде одразу")]
    [SerializeField]
    List<EventMissionLink> eventMissionLinks = new()
    {
        new EventMissionLink { eventName = "", choiceText = "", missionId = "relay",   workerId = -1 },
        new EventMissionLink { eventName = "", choiceText = "", missionId = "pragyan", workerId = -1 },
    };

    int hoursSinceOrders;
    bool subscribed;
    EventManager eventManager;
    bool wasTimePausedBefore;
    float prevTimeScale = 1f;

    public bool WorkEnabled => workEnabled;
    public bool OrdersOpen { get; private set; }
    public float BaseSkillHours => baseSkillHours;
    public int HoursUntilNextOrders => Mathf.Max(0, orderIntervalHours - hoursSinceOrders);

    public event Action<bool> OnWorkEnabledChanged;
    public event Action OnOrdersTime;
    public event Action<List<Worker>> OnOrdersOpened;
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

        eventManager = EventManager.Instance;
        if (eventManager != null)
            eventManager.ChoiceSelected += HandleEventChoice;
    }

    void OnDestroy()
    {
        if (subscribed && TimeManager.Instance != null)
            TimeManager.Instance.OnHourTick -= HandleHourTick;

        if (eventManager != null)
            eventManager.ChoiceSelected -= HandleEventChoice;

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

    void HandleEventChoice(EventInfo e, EventChoice choice)
    {
        var mm = Rover.Instance;
        if (mm == null) return;

        foreach (var link in eventMissionLinks)
        {
            if (link.eventName != e.eventName || link.choiceText != choice.ChoiceText) continue;

            mm.Unlock(link.missionId);
            if (link.workerId > 0) mm.TrySend(link.workerId, link.missionId);
        }
    }

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

        if (TimeManager.Instance != null && !wasTimePausedBefore)
            TimeManager.Instance.Resume();

        OnOrdersClosed?.Invoke();
    }

    public bool IsAvailableForOrders(Worker w) =>
        w != null && w.isActiveAndEnabled && !w.IsBusy &&
        (Rover.Instance == null || !Rover.Instance.IsOnMission(w));

    public void GetAvailableWorkers(List<Worker> result)
    {
        result.Clear();
        foreach (var w in Worker.All)
            if (IsAvailableForOrders(w)) result.Add(w);
    }

    public bool TryGiveOrder(Worker w, int zoneId)
    {
        if (!IsAvailableForOrders(w)) return false;
        return w.TryAssignToZone(zoneId, teleport: true);
    }
}