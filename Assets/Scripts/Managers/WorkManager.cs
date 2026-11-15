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
    [Tooltip("Головний вимикач роботи (його вмикаєте ви, наприклад після вступу)")]
    [SerializeField] bool workEnabled;
    [Tooltip("На відбої WorkManager вимикається на ніч (немає наказів і роботи), на підйомі вмикається знову")]
    [SerializeField] bool lockWorkAtNight = true;

    [Header("Orders")]
    [Tooltip("Через скільки ігрових годин настає час надавати нові накази")]
    [SerializeField, Min(1)] int orderIntervalHours = 4;
    [Tooltip("Окрім TimeManager ставить на паузу все інше через Time.timeScale = 0")]
    [SerializeField] bool freezeWholeGame = true;
    [Tooltip("Одразу відкривати накази, коли роботу ввімкнено (на старті гри, після підйому або SetWorkEnabled(true))")]
    [SerializeField] bool ordersWhenEnabled = true;

    [Header("Base (living module)")]
    [Tooltip("Id житлового модуля: сюди всі повертаються на відбої й збираються на підйомі")]
    [SerializeField] int gatherZoneId = 4;
    [Tooltip("На годині підйому всі вільні працівники збираються на базі")]
    [SerializeField] bool gatherAtDayStart = true;
    [Tooltip("Також зібрати всіх на старті гри")]
    [SerializeField] bool gatherAtGameStart = true;
    [Tooltip("На годині відбою всі повертаються на базу (крім місій ровера й дослідників, що ще не завершили)")]
    [SerializeField] bool returnAtBedtime = true;

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
    ResearchTree researchTree;
    bool wasTimePausedBefore;
    float prevTimeScale = 1f;
    bool ordersPending;   // настав час наказів, але панель ще чекає закриття панелей подій
    bool gatherPending;   // зібрати всіх на старті гри, коли всі Start уже виконались
    bool nightLock;       // ніч: робота вимкнена, всі на базі

    // ремонтники, відправлені одразу після події: заблоковані від наказів, поки зона не полагоджена
    readonly Dictionary<Worker, WorkZone> dispatched = new();

    /// <summary>Робота зараз увімкнена: головний вимикач увімкнено й не ніч. Це значення читають зони.</summary>
    public bool WorkEnabled => workEnabled && !nightLock;

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

        var tm = TimeManager.Instance;
        if (tm != null)
        {
            tm.OnHourTick += HandleHourTick;
            tm.OnWake += HandleWake;
            tm.OnBedtime += HandleBedtime;
            subscribed = true;

            // гра стартує вночі: одразу в нічному режимі
            nightLock = lockWorkAtNight && !tm.IsWorkHour(tm.Hour);
        }

        eventManager = EventManager.Instance;
        if (eventManager != null)
            eventManager.ChoiceSelected += HandleEventChoice;

        researchTree = ResearchTree.Instance;
        if (researchTree != null)
            researchTree.OnCompleted += HandleResearchCompleted;

        ValidateZoneIds();

        if (WorkEnabled && ordersWhenEnabled) ordersPending = true;
        gatherPending = gatherAtGameStart;
    }

    void OnDestroy()
    {
        if (subscribed && TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourTick -= HandleHourTick;
            TimeManager.Instance.OnWake -= HandleWake;
            TimeManager.Instance.OnBedtime -= HandleBedtime;
        }

        if (eventManager != null)
            eventManager.ChoiceSelected -= HandleEventChoice;

        if (researchTree != null)
            researchTree.OnCompleted -= HandleResearchCompleted;

        if (OrdersOpen && freezeWholeGame)
            Time.timeScale = prevTimeScale <= 0f ? 1f : prevTimeScale;

        if (Instance == this) Instance = null;
    }

    /// <summary>Попереджає, якщо в двох зон однаковий Id: за Id шукаються стартові зони й база.</summary>
    void ValidateZoneIds()
    {
        var seen = new Dictionary<int, WorkZone>();
        foreach (var z in WorkZone.All)
        {
            if (seen.TryGetValue(z.Id, out var other))
                Debug.LogError($"WorkManager: zones '{other.DisplayName}' and '{z.DisplayName}' have the same Id {z.Id}. " +
                               "Give every zone a unique Id.", z);
            else
                seen[z.Id] = z;
        }
    }

    void HandleHourTick(int day, int hour)
    {
        if (!WorkEnabled || OrdersOpen) return;

        hoursSinceOrders++;
        if (hoursSinceOrders >= orderIntervalHours)
        {
            hoursSinceOrders = 0;
            ordersPending = true;
        }
    }

    void LateUpdate()
    {
        // старт гри: збираємо після того, як усі Worker.Start уже виконались
        if (gatherPending)
        {
            gatherPending = false;
            GatherAll();
        }

        if (!ordersPending || OrdersOpen) return;

        // вночі накази не відкриваються
        if (!WorkEnabled)
        {
            ordersPending = false;
            return;
        }

        var ev = EventManager.Instance;
        if (ev != null && ev.IsBusy) return; // чекаємо, поки закриються панелі подій

        ordersPending = false;
        OpenOrders();
    }

    /// <summary>Головний вимикач роботи. Вночі робота лишається вимкненою незалежно від нього.</summary>
    public void SetWorkEnabled(bool value)
    {
        if (workEnabled == value) return;

        bool before = WorkEnabled;
        workEnabled = value;

        if (value && !nightLock)
        {
            hoursSinceOrders = 0;
            if (ordersWhenEnabled) ordersPending = true;
        }

        if (before != WorkEnabled) OnWorkEnabledChanged?.Invoke(WorkEnabled);
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

    // ---------- Підйом і відбій ----------

    /// <summary>Підйом: усі збираються на базі, робота вмикається, відкриваються ранкові накази.</summary>
    void HandleWake()
    {
        if (gatherAtDayStart) GatherAll();
        SetNightLock(false);
    }

    /// <summary>Відбій: усі повертаються на базу й залишаються там, WorkManager вимикається до ранку.
    /// Не чіпаємо місії ровера та дослідників, що ще не завершили (вони мають встигати до відбою).</summary>
    void HandleBedtime()
    {
        if (returnAtBedtime)
        {
            foreach (var w in Worker.All)
            {
                if (w == null) continue;
                if (Rover.Instance != null && Rover.Instance.IsOnMission(w)) continue;
                if (researchTree != null && researchTree.HasClaim(w)) continue;

                SendToBase(w);
            }
        }

        SetNightLock(true);
    }

    /// <summary>Вмикає або вимикає нічний режим.</summary>
    void SetNightLock(bool value)
    {
        if (!lockWorkAtNight) value = false;
        if (nightLock == value) return;

        bool before = WorkEnabled;
        nightLock = value;

        if (value)
        {
            // ніч: відкладених наказів немає
            ordersPending = false;
        }
        else if (workEnabled)
        {
            // ранок: відлік наказів з нуля, перші накази одразу
            hoursSinceOrders = 0;
            if (ordersWhenEnabled) ordersPending = true;
        }

        if (before != WorkEnabled) OnWorkEnabledChanged?.Invoke(WorkEnabled);
    }

    /// <summary>Дослідник, що завершив дослідження після відбою, одразу повертається на базу.</summary>
    void HandleResearchCompleted(Worker w, ResearchHext hex)
    {
        if (!returnAtBedtime || w == null) return;

        var tm = TimeManager.Instance;
        if (tm != null && !tm.IsWorkHour(tm.Hour)) SendToBase(w);
    }

    void SendToBase(Worker w)
    {
        var zone = WorkZone.FindById(gatherZoneId);
        if (zone == null || w == null) return;

        w.TryAssignToZone(zone, teleport: true);
    }

    /// <summary>Збирає всіх вільних працівників на базі (житловий модуль).
    /// Зайняті (дослідження, довга робота, ремонт після події, місія ровера) лишаються на місці.</summary>
    public void GatherAll()
    {
        var zone = WorkZone.FindById(gatherZoneId);
        if (zone == null)
        {
            Debug.LogWarning($"WorkManager: gather zone with id {gatherZoneId} not found");
            return;
        }

        foreach (var w in Worker.All)
        {
            if (!IsAvailableForOrders(w)) continue;

            if (!TryGiveOrder(w, zone))
                Debug.LogWarning($"WorkManager: {w.DisplayName} could not be gathered " +
                                 $"(zone '{zone.DisplayName}' is full?)");
        }
    }

    // ---------- Панель наказів і пауза ----------

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

    // ---------- Накази ----------

    public bool IsAvailableForOrders(Worker w)
    {
        if (w == null || !w.isActiveAndEnabled || w.IsBusy) return false;
        if (Rover.Instance != null && Rover.Instance.IsOnMission(w)) return false;
        if (ResearchTree.Instance != null && ResearchTree.Instance.HasClaim(w)) return false;
        if (IsLockedOnRepair(w)) return false;
        return true;
    }

    /// <summary>Працівника відправили лагодити зону одразу після події, і вона ще не полагоджена.</summary>
    public bool IsLockedOnRepair(Worker w)
    {
        if (w == null || !dispatched.TryGetValue(w, out var zone)) return false;

        // замок діє, поки зона зламана, а працівник у ній і не вибув зі строю
        if (zone != null && zone.IsDamaged && w.CurrentZone == zone && !w.IsCritical)
            return true;

        dispatched.Remove(w);
        return false;
    }

    public void GetAvailableWorkers(List<Worker> result)
    {
        result.Clear();
        foreach (var w in Worker.All)
            if (IsAvailableForOrders(w)) result.Add(w);
    }

    /// <summary>Відправляє працівника в зону й телепортує його. Працює з самою зоною, а не з Id.</summary>
    public bool TryGiveOrder(Worker w, WorkZone zone, WorkType work = WorkType.None)
    {
        if (zone == null || !IsAvailableForOrders(w)) return false;
        return w.TryAssignToZone(zone, teleport: true, preferredWork: work);
    }

    /// <summary>Те саме за Id зони (Id мають бути унікальними).</summary>
    public bool TryGiveOrder(Worker w, int zoneId, WorkType work = WorkType.None) =>
        TryGiveOrder(w, WorkZone.FindById(zoneId), work);

    // ---------- Негайний ремонт після події ----------

    /// <summary>
    /// Відправляє найкращого вільного ремонтника в пошкоджену зону одразу, не чекаючи панелі наказів.
    /// onlyIfExclusive = true: лише якщо ремонтувати цю зону вміє одна роль (наприклад, лише інженер).
    /// Вночі (робота вимкнена) нікого не відправляє: усі лишаються на базі до ранкових наказів.
    /// </summary>
    public bool TryDispatchRepair(WorkZone zone, bool onlyIfExclusive)
    {
        if (!WorkEnabled) return false;
        if (zone == null || !zone.IsDamaged) return false;

        var roles = new HashSet<WorkerRole>();
        Worker best = null;
        float bestHours = float.MaxValue;

        foreach (var w in Worker.All)
        {
            if (!zone.CanWorkerDo(w, WorkType.Repair)) continue;
            roles.Add(w.Role);

            if (w.IsCritical || !IsAvailableForOrders(w)) continue;

            float hours = RepairHours(zone, w);
            if (hours < bestHours)
            {
                best = w;
                bestHours = hours;
            }
        }

        if (best == null) return false;
        if (onlyIfExclusive && roles.Count != 1) return false;

        if (!TryGiveOrder(best, zone)) return false;

        dispatched[best] = zone;
        return true;
    }

    float RepairHours(WorkZone zone, Worker w)
    {
        float factor = w.GetWorkHours(WorkType.Repair) / Mathf.Max(0.1f, baseSkillHours);
        return Mathf.Max(0.1f, zone.AdjustTaskHours(w, WorkType.Repair, zone.WorkHours * factor));
    }
}